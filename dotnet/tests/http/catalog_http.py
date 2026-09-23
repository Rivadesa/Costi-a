"""E3 (Hito 6): catalogo y tarifas del nucleo - impuestos, categorias, productos, presentaciones, tarifas y precios con
vigencia - por HTTP real contra PostgreSQL. Solo el puesto principal edita; nunca se borra, se desactiva; el precio se
congela en la cuenta con su producto, presentacion y tarifa (la de la sala o la general); los catalogos operativos no
llevan dinero y solo listan vendibles con precio vigente; la importacion CSV es idempotente por la huella del fichero.
"""
import datetime
import json
import os
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

import native_http as h

ok, request = h.ok, h.request
MONEY = re.compile(r'(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)', re.I)   # la regla de la PWA

def command(action, role='main', **body):
    return request('/erp/catalog/commands/' + action, body, role=role)

def catalog():
    return ok('/erp/catalog')

def product(pid):
    return next(p for p in catalog()['products'] if p['id'] == pid)

def money_keys(value, path=''):
    if isinstance(value, dict):
        for k, v in value.items():
            if MONEY.search(k): yield path + '/' + k
            yield from money_keys(v, path + '/' + k)
    elif isinstance(value, list):
        for i, v in enumerate(value): yield from money_keys(v, path + '/' + str(i))

def free_table():
    """Una mesa libre de una sala SIN tarifa propia (otras suites dejan mesas abiertas); si no queda ninguna, se crea una en 'sala'."""
    occupied = {r['service']['tableId'] for r in ok('/dining/board')}
    free = [t['id'] for t in ok('/configuration')['tables'] if t['id'] not in occupied and t['zoneId'] != 'terraza-z']
    if free: return free[0]
    assert request('/organization/commands/table-create', dict(id='CAT1', name='Catálogo 1', capacity=2, zoneId='sala'))[0] in (200, 409)
    return 'CAT1'

class Catalog(unittest.TestCase):
    @classmethod
    def setUpClass(cls): h.start_server()
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def release(self, sid):
        h.mutate(sid, 'cancel-unstarted', reason='prueba')
        version = next(r['occupancyVersion'] for r in ok('/dining/board') if r['service']['id'] == sid)
        ok('/dining/occupancy/' + sid + '/release', {'expectedVersion': version, 'reason': 'prueba'})

    def test_01_demo_catalog_is_relational_and_only_main_edits_it(self):
        c = catalog()
        self.assertEqual([t['id'] for t in c['taxes']], ['iva-21', 'iva-10', 'iva-4', 'iva-0'])           # lo que existe siempre
        self.assertEqual([t['id'] for t in c['tariffs']], ['general'])
        self.assertEqual([(x['id'], x['parentId']) for x in c['categories']], [('bebidas', None), ('vinos', 'bebidas')])
        wine = product('wine')
        self.assertEqual((wine['categoryId'], wine['taxId'], wine['reference']), ('vinos', 'iva-21', '3754'))
        self.assertEqual({(p['id'], p['prices'][0]['tariffId'], p['prices'][0]['priceCents']) for p in wine['presentations']}, {('glass', 'general', 950), ('bottle', 'general', 4200)})
        self.assertEqual(c['today'], datetime.date.today().isoformat())
        for role in ('service', 'kitchen'):
            self.assertEqual(request('/erp/catalog', role=role)[0], 403, role)
            self.assertEqual(command('product-create', role=role, id='x', name='x')[0], 403, role)
        # Catalogo operativo de sala: vendibles (producto + presentacion) con su categoria y sin una sola clave de dinero.
        status, body = request('/catalog', role='service')
        items = json.loads(body)
        self.assertEqual(status, 200)
        self.assertEqual([(i['id'], i['presentationId'], i['categoryName']) for i in items], [('water', 'bottle', 'Bebidas'), ('wine', 'glass', 'Vinos'), ('wine', 'bottle', 'Vinos')])
        self.assertEqual(list(money_keys(items)), [])
        # Caja: lo mismo con el precio de la tarifa general como referencia.
        priced = ok('/checkout/catalog')
        self.assertEqual([(i['id'], i['presentationId'], i['priceCents'], i['tariffId']) for i in priced], [('water', 'bottle', 400, 'general'), ('wine', 'glass', 950, 'general'), ('wine', 'bottle', 4200, 'general')])

    def test_02_taxes_categories_products_and_presentations_are_edited_and_never_deleted(self):
        self.assertEqual(command('tax-create', id='iva-x', name='IVA especial', rate=10.5)[0], 200)
        self.assertEqual(command('tax-create', id='iva-x', name='Otro', rate=1)[0], 409)                    # codigo unico
        for bad in (dict(id='t2', name='x', rate=100.5), dict(id='t2', name='x', rate=10.123), dict(id='t2', name='x'), dict(id='con espacio', name='x', rate=1)):
            self.assertIn(command('tax-create', **bad)[0], (409, 422), bad)
        self.assertEqual(command('category-create', id='comida', name='Comida', color='#b5673a', sort=2)[0], 200)
        self.assertEqual(command('category-create', id='tapas', name='Tapas', parentId='comida')[0], 200)
        self.assertEqual(command('category-create', id='tapas', name='Otra')[0], 409)
        self.assertEqual(command('category-create', id='x1', name='x', parentId='no-existe')[0], 404)
        self.assertEqual(command('category-create', id='x1', name='x', color='rojo')[0], 409)              # invalid_color
        status, body = command('category-update', id='comida', parentId='tapas')                            # ciclo
        self.assertEqual((status, json.loads(body)['error']), (409, 'category_cycle'))
        self.assertEqual(command('category-create', id='n3', name='n3', parentId='tapas')[0], 200)
        self.assertEqual(command('category-create', id='n4', name='n4', parentId='n3')[0], 200)
        self.assertEqual(json.loads(command('category-create', id='n5', name='n5', parentId='n4')[1])['error'], 'category_depth')
        # Producto: nace con la presentacion 'unit' y sin precio; hasta fijarlo no es vendible.
        status, body = command('product-create', id='coffee', name='Café', categoryId='comida', taxId='iva-10', reference='C-1')
        self.assertEqual((status, json.loads(body)['presentationId']), (200, 'unit'), body)
        self.assertEqual(command('product-create', id='coffee', name='Otro')[0], 409)
        self.assertEqual(command('product-create', id='p2', name='x', taxId='no-existe')[0], 404)
        self.assertEqual(command('product-create', id='p2', name='x', categoryId='no-existe')[0], 404)
        self.assertNotIn('coffee', [i['id'] for i in ok('/catalog')])
        self.assertEqual(command('presentation-create', productId='coffee', id='double', name='Doble', sort=1)[0], 200)
        self.assertEqual(command('presentation-create', productId='coffee', id='double', name='Doble')[0], 409)
        self.assertEqual(command('presentation-create', productId='no-existe', id='x', name='x')[0], 404)
        status, body = command('price-set', tariffId='general', productId='coffee', presentationId='unit', priceCents=150)
        self.assertEqual((status, json.loads(body)['changed'], json.loads(body)['validFrom']), (200, True, datetime.date.today().isoformat()), body)
        self.assertFalse(json.loads(command('price-set', tariffId='general', productId='coffee', presentationId='unit', priceCents=150)[1])['changed'])   # mismo precio, misma fecha
        for bad in (dict(tariffId='general', productId='coffee', presentationId='unit', priceCents=-1), dict(tariffId='general', productId='coffee', presentationId='unit'),
                    dict(tariffId='general', productId='coffee', presentationId='unit', priceCents=1, validFrom='01/10/2026')):
            self.assertIn(command('price-set', **bad)[0], (409, 422), bad)
        self.assertEqual(command('price-set', tariffId='general', productId='coffee', presentationId='nada', priceCents=1)[0], 404)
        self.assertEqual(command('price-set', tariffId='no-existe', productId='coffee', presentationId='unit', priceCents=1)[0], 404)
        self.assertEqual([(i['id'], i['presentationId']) for i in ok('/catalog') if i['id'] == 'coffee'], [('coffee', 'unit')])   # 'double' sin precio no se vende
        self.assertEqual(command('price-set', tariffId='general', productId='coffee', presentationId='double', priceCents=250)[0], 200)
        self.assertEqual([(i['id'], i['presentationId']) for i in ok('/catalog') if i['id'] == 'coffee'], [('coffee', 'unit'), ('coffee', 'double')])
        self.assertEqual(command('product-update', id='coffee', name='Café solo', reference='')[0], 200)
        self.assertEqual((product('coffee')['name'], product('coffee')['reference']), ('Café solo', None))
        # Desactivar: desaparece de los catalogos operativos y sigue en el completo, marcado; reactivar lo devuelve.
        self.assertEqual(command('product-deactivate', id='coffee')[0], 200)
        self.assertNotIn('coffee', [i['id'] for i in ok('/catalog')])
        self.assertFalse(product('coffee')['active'])
        self.assertEqual(command('product-reactivate', id='coffee')[0], 200)
        self.assertIn('coffee', [i['id'] for i in ok('/catalog')])
        self.assertEqual(command('presentation-deactivate', productId='coffee', id='double')[0], 200)
        self.assertEqual([i['presentationId'] for i in ok('/catalog') if i['id'] == 'coffee'], ['unit'])
        # Lo que esta en uso no se desactiva: categoria con productos activos, impuesto con productos activos, tarifa general nunca.
        self.assertEqual(json.loads(command('category-deactivate', id='comida')[1])['error'], 'category_in_use')
        self.assertEqual(json.loads(command('tax-deactivate', id='iva-10')[1])['error'], 'tax_in_use')
        self.assertEqual(json.loads(command('tariff-deactivate', id='general')[1])['error'], 'reserved_tariff')
        self.assertEqual(command('tax-deactivate', id='iva-x')[0], 200)
        self.assertEqual(command('product-update', id='coffee', taxId='iva-x')[0], 409)                     # tax_inactive
        self.assertEqual(command('tax-reactivate', id='iva-x')[0], 200)
        self.assertEqual(command('category-deactivate', id='n4')[0], 200)
        self.assertEqual(command('category-deactivate', id='n3')[0], 200)
        self.assertEqual(json.loads(command('category-reactivate', id='n4')[1])['error'], 'category_inactive')   # el padre primero
        self.assertEqual(command('nada', id='x')[0], 403)                                                  # accion no anunciada
        self.assertEqual(command('product-update', id='no-existe', name='x')[0], 404)

    def test_03_prices_have_validity_and_the_zone_tariff_is_frozen_in_the_account(self):
        self.assertEqual(command('tariff-create', id='terraza', name='Terraza', sort=1)[0], 200)
        self.assertEqual(command('price-set', tariffId='terraza', productId='wine', presentationId='glass', priceCents=1200)[0], 200)
        tomorrow = (datetime.date.today() + datetime.timedelta(days=1)).isoformat()
        self.assertEqual(command('price-set', tariffId='terraza', productId='wine', presentationId='glass', priceCents=1300, validFrom=tomorrow)[0], 200)   # programado
        self.assertEqual(request('/organization/commands/zone-create', dict(id='terraza-z', name='Terraza', sort=9, tariffId='terraza'))[0], 200)
        self.assertEqual(request('/organization/commands/zone-create', dict(id='z-bad', name='x', tariffId='no-existe'))[0], 404)
        self.assertEqual(next(z for z in ok('/organization')['zones'] if z['id'] == 'terraza-z')['tariffId'], 'terraza')
        self.assertEqual(request('/organization/commands/table-create', dict(id='TZ1', name='Terraza 1', capacity=4, zoneId='terraza-z'))[0], 200)
        self.assertEqual(json.loads(command('tariff-deactivate', id='terraza')[1])['error'], 'tariff_in_use')
        # Consumo desde sala en la terraza: precio de la tarifa de la sala HOY (no el programado), congelado con su origen.
        sid = h.open_table('TZ1')
        version = h.dining(sid)['version']
        status, body = request('/dining/services/' + sid + '/commands/add-consumption', dict(expectedVersion=version, productId='wine', presentationId='glass', quantity=2), role='service')
        self.assertEqual(status, 200, body)
        self.assertEqual(set(json.loads(body)), {'version', 'consumptionId', 'productId', 'presentationId', 'quantity'})
        self.assertEqual(list(money_keys(json.loads(body))), [])
        line = [c for c in h.account(sid)['data']['charges'] if c.get('productId') == 'wine'][0]
        self.assertEqual((line['description'], line['unitPriceCents'], line['presentationId'], line['tariffId'], line['totalCents']), ('Vino de ensayo · Copa', 1200, 'glass', 'terraza', 2400))
        # Sin presentacion con varias: 422; presentacion inexistente o desactivada: 409; agua (una sola presentacion) sin indicarla: vale.
        self.assertEqual(request('/dining/services/' + sid + '/commands/add-consumption', dict(expectedVersion=version, productId='wine', quantity=1), role='service')[0], 422)
        self.assertEqual(json.loads(request('/dining/services/' + sid + '/commands/add-consumption', dict(expectedVersion=version, productId='wine', presentationId='magnum', quantity=1), role='service')[1])['error'], 'product_unavailable')
        self.assertEqual(request('/dining/services/' + sid + '/commands/add-consumption', dict(expectedVersion=version, productId='water', quantity=1), role='service')[0], 200)
        water = [c for c in h.account(sid)['data']['charges'] if c.get('productId') == 'water'][0]
        self.assertEqual((water['unitPriceCents'], water['presentationId'], water['tariffId']), (400, 'bottle', 'general'))   # la terraza no fija el agua: respaldo general
        # La caja tambien vende por presentacion; un producto sin precio no se vende (price_missing) aunque este activo.
        self.assertEqual(command('product-create', id='nopricing', name='Sin precio')[0], 200)
        status, body = h.request('/checkout/services/' + sid + '/commands/add-product', dict(expectedVersion=h.account(sid)['version'], productId='nopricing', quantity=1))
        self.assertEqual((status, json.loads(body)['error']), (409, 'price_missing'))
        status, body = h.request('/checkout/services/' + sid + '/commands/add-product', dict(expectedVersion=h.account(sid)['version'], productId='wine', presentationId='bottle', quantity=1))
        self.assertEqual(status, 200, body)
        bottle = [c for c in json.loads(body)['data']['charges'] if c.get('productId') == 'wine' and c.get('presentationId') == 'bottle'][0]
        self.assertEqual((bottle['unitPriceCents'], bottle['tariffId']), (4200, 'general'))                # la terraza no fija la botella
        # Cambiar el precio despues no toca los cargos ya emitidos.
        self.assertEqual(command('price-set', tariffId='terraza', productId='wine', presentationId='glass', priceCents=1250)[0], 200)
        self.assertEqual([c['unitPriceCents'] for c in h.account(sid)['data']['charges'] if c.get('productId') == 'wine' and c.get('presentationId') == 'glass'], [1200])
        self.release(sid)
        # Una mesa de la sala (sin tarifa propia) vende a la tarifa general.
        sid = h.open_table(free_table())
        self.assertEqual(request('/dining/services/' + sid + '/commands/add-consumption', dict(expectedVersion=h.dining(sid)['version'], productId='wine', presentationId='glass', quantity=1), role='service')[0], 200)
        self.assertEqual([(c['unitPriceCents'], c['tariffId']) for c in h.account(sid)['data']['charges'] if c.get('productId') == 'wine'], [(950, 'general')])
        self.release(sid)
        self.assertEqual(request('/organization/commands/zone-update', dict(id='terraza-z', tariffId=''))[0], 200)   # vuelve a la general
        self.assertIsNone(next(z for z in ok('/organization')['zones'] if z['id'] == 'terraza-z')['tariffId'])
        self.assertEqual(command('tariff-deactivate', id='terraza')[0], 200)
        self.assertEqual(command('price-set', tariffId='terraza', productId='wine', presentationId='glass', priceCents=1)[0], 409)   # tariff_inactive

    def test_04_csv_import_creates_and_updates_without_deleting_and_is_idempotent(self):
        folder = Path(tempfile.mkdtemp(prefix='costina-catalog-'))
        csv = folder / 'verial.csv'
        rows = ['reference;name;category;parent;presentation;price;tax;active',
                '3760;El Rapolao 2023;Tintos;Vinos;Botella;40,00;21;1',
                '3761;"Ladredo, Bierzo 2021";Tintos;Vinos;Botella;"1.250,50";21;1',
                '3762;Agua con gas;Bebidas;;Botella;2.5;10;0']
        csv.write_text('\n'.join(rows) + '\n', encoding='utf8')
        def run(path):
            return subprocess.run(['dotnet', str(h.SERVER), 'import-catalog', str(path)], env=h.ENV, capture_output=True, text=True, encoding='utf8')
        first = run(csv)
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self.assertIn('products created 3, updated 0; categories created 1; presentations created 3; prices set 3', first.stdout)
        tinto = product('3760')
        self.assertEqual((tinto['name'], tinto['categoryId'], tinto['taxId'], tinto['reference'], tinto['active']), ('El Rapolao 2023', 'tintos', 'iva-21', '3760', True))
        self.assertEqual([(p['id'], p['name'], p['prices'][0]['priceCents']) for p in tinto['presentations']], [('botella', 'Botella', 4000)])
        self.assertEqual(next(c for c in catalog()['categories'] if c['id'] == 'tintos')['parentId'], 'vinos')       # 'Vinos' ya existia: se reutiliza
        self.assertEqual(product('3761')['presentations'][0]['prices'][0]['priceCents'], 125050)
        self.assertFalse(product('3762')['active'])                                                                # nace desactivado
        prices_before = h.sql("SELECT count(*) FROM core.prices WHERE tenant='d1-tenant'")
        again = run(csv)                                                                                            # mismo fichero: misma respuesta, nada nuevo
        self.assertEqual(again.returncode, 0, again.stderr)
        self.assertEqual(h.sql("SELECT count(*) FROM core.prices WHERE tenant='d1-tenant'"), prices_before)
        self.assertEqual(h.sql("SELECT count(*) FROM core.commands WHERE tenant='d1-tenant' AND actor='cli:import-catalog'"), '1')
        # Un fichero cambiado (precio y nombre) actualiza sin crear ni desactivar nada; el producto desactivado a mano sigue asi.
        self.assertEqual(command('product-deactivate', id='3761')[0], 200)
        csv.write_text('\n'.join(rows[:1] + ['3760;El Rapolao 2023 (nueva anada);Tintos;Vinos;Botella;41,00;21;1'] + rows[2:]) + '\n', encoding='utf8')
        third = run(csv)
        self.assertEqual(third.returncode, 0, third.stdout + third.stderr)
        self.assertIn('products created 0, updated 1; categories created 0; presentations created 0; prices set 1', third.stdout)
        self.assertEqual((product('3760')['name'], product('3760')['presentations'][0]['prices'][-1]['priceCents']), ('El Rapolao 2023 (nueva anada)', 4100))
        self.assertFalse(product('3761')['active'])
        self.assertEqual(h.sql("SELECT count(*) FROM core.products WHERE tenant='d1-tenant' AND id IN ('3760','3761','3762')"), '3')
        # Un fichero con un error no aplica nada (una transaccion) y lo dice con la linea.
        bad = folder / 'bad.csv'
        bad.write_text('reference;name;price;tax\n3770;Bueno;1,00;10\n3771;;2,00;10\n', encoding='utf8')
        failed = run(bad)
        self.assertNotEqual(failed.returncode, 0)
        self.assertIn('Line 3', failed.stdout + failed.stderr)
        self.assertEqual(h.sql("SELECT count(*) FROM core.products WHERE tenant='d1-tenant' AND id='3770'"), '0')
        unknown = folder / 'tax.csv'
        unknown.write_text('reference;name;price;tax\n3772;Raro;1,00;7\n', encoding='utf8')
        failed = run(unknown)
        self.assertNotEqual(failed.returncode, 0); self.assertIn("tax '7'", failed.stdout + failed.stderr)

    def test_05_commands_are_idempotent_and_audited(self):
        key = 'catalog-' + os.urandom(6).hex()
        status, body = request('/erp/catalog/commands/category-create', dict(id='postres', name='Postres'), key=key)
        self.assertEqual(status, 200, body)
        self.assertEqual(request('/erp/catalog/commands/category-create', dict(id='postres', name='Postres'), key=key), (200, body))   # replay
        self.assertIn(request('/erp/catalog/commands/category-create', dict(id='postres', name='Otro'), key=key)[0], (409, 422))       # misma clave, otro cuerpo
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='catalog.category_created' AND aggregate_id='postres'"), '1')
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='catalog.price_set' AND aggregate_id='coffee'"), '2')   # unit y double

if __name__ == '__main__':
    unittest.main(verbosity=1)
