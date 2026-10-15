"""E3 (Hito 6): catalogo y tarifas del nucleo - impuestos, categorias, productos, presentaciones, tarifas y precios con
vigencia - por HTTP real contra PostgreSQL. Solo el puesto principal edita; nunca se borra, se desactiva; el precio se
congela en la cuenta con su producto, presentacion y tarifa (la de la sala o la general); los catalogos operativos no
llevan dinero y solo listan vendibles con precio vigente; la importacion CSV es idempotente por la huella del fichero.
E3.2: imagen vigente de cada articulo (JPEG, PNG o WebP por su firma, hasta 2 MB): solo main la pone o la quita, cualquier
identidad la lee con ETag = huella; los catalogos llevan imageSha; la importacion toma las fotos de la carpeta del CSV.
"""
import base64
import datetime
import hashlib
import http.client
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

def import_csv(path):
    return subprocess.run(['dotnet', str(h.SERVER), 'import-catalog', str(path)], env=h.ENV, capture_output=True, text=True, encoding='utf8')

# E3.2: el motor no decodifica imagenes, solo mira su firma: bastan la firma y unos bytes cualesquiera detras.
def png(size=2048): return b'\x89PNG\r\n\x1a\n' + os.urandom(size - 8)
def jpeg(size=2048): return b'\xff\xd8\xff\xe0' + os.urandom(size - 4)
def webp(size=2048): return b'RIFF' + (size - 8).to_bytes(4, 'little') + b'WEBP' + os.urandom(size - 12)
def sha(data): return hashlib.sha256(data).hexdigest()

def set_image(pid, data, content_type, role='main'):
    return command('product-image-set', role=role, productId=pid, contentType=content_type, data=base64.b64encode(data).decode('ascii'))

def read_image(pid, role='main', etag=None):
    status, body = request('/catalog/images/' + pid, role=role, headers={'If-None-Match': etag} if etag else None)
    return status, body, {k.lower(): v for k, v in h.LAST_HEADERS.items()}

def declared_only(path, length):
    """Una orden que ANUNCIA un cuerpo de `length` bytes y no envia ninguno: el motor la rechaza al empezar a leer (tope de Kestrel)
    sin esperar el cuerpo, y la prueba no depende de como corte el sistema una conexion con megas sin leer."""
    connection = http.client.HTTPConnection('127.0.0.1', int(h.PORT), timeout=15)
    try:
        connection.putrequest('POST', '/api/native/v1' + path)
        for name, value in (('Authorization', 'Bearer ' + h.KEYS['main']), ('Content-Type', 'application/json'),
                            ('Idempotency-Key', os.urandom(8).hex()), ('Content-Length', str(length))):
            connection.putheader(name, value)
        connection.endheaders()
        response = connection.getresponse()
        return response.status, response.read()
    finally:
        connection.close()

def subst_drive(folder):
    """Windows: una unidad virtual (subst) libre sobre `folder` ('Q:'...), o None si no queda letra."""
    for letter in 'QRSTUVWXYZ':
        drive = letter + ':'
        if not os.path.exists(drive + '\\') and subprocess.run(['subst', drive, str(folder)], capture_output=True).returncode == 0:
            return drive
    return None

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
        self.assertEqual([(x['id'], x['parentId']) for x in c['categories']], [('_cocina', None), ('bebidas', None), ('_bodega', None), ('vinos', 'bebidas'), ('comida', None), ('menus', None)])   # E4a/E4b: menus y platos de la carta de ensayo; E3.3: las dos secciones
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
        self.assertEqual([(i['id'], i['presentationId'], i['categoryName']) for i in items], [('water', 'bottle', 'Bebidas'), ('wine', 'glass', 'Vinos'), ('wine', 'bottle', 'Vinos'), ('croquetas', 'unit', 'Comida'), ('lubina', 'unit', 'Comida'), ('tarta', 'unit', 'Comida')])
        self.assertEqual(list(money_keys(items)), [])
        # Caja: lo mismo con el precio de la tarifa general como referencia.
        priced = ok('/checkout/catalog')
        self.assertEqual([(i['id'], i['presentationId'], i['priceCents'], i['tariffId']) for i in priced], [('water', 'bottle', 400, 'general'), ('wine', 'glass', 950, 'general'), ('wine', 'bottle', 4200, 'general'), ('croquetas', 'unit', 800, 'general'), ('lubina', 'unit', 2400, 'general'), ('tarta', 'unit', 600, 'general')])

    def test_02_taxes_categories_products_and_presentations_are_edited_and_never_deleted(self):
        self.assertEqual(command('tax-create', id='iva-x', name='IVA especial', rate=10.5)[0], 200)
        self.assertEqual(command('tax-create', id='iva-x', name='Otro', rate=1)[0], 409)                    # codigo unico
        for bad in (dict(id='t2', name='x', rate=100.5), dict(id='t2', name='x', rate=10.123), dict(id='t2', name='x'), dict(id='con espacio', name='x', rate=1)):
            self.assertIn(command('tax-create', **bad)[0], (409, 422), bad)
        self.assertEqual(command('category-create', id='cocina', name='Comida', color='#b5673a', sort=2)[0], 200)
        self.assertEqual(command('category-create', id='tapas', name='Tapas', parentId='cocina')[0], 200)
        self.assertEqual(command('category-create', id='tapas', name='Otra')[0], 409)
        self.assertEqual(command('category-create', id='x1', name='x', parentId='no-existe')[0], 404)
        self.assertEqual(command('category-create', id='x1', name='x', color='rojo')[0], 409)              # invalid_color
        status, body = command('category-update', id='cocina', parentId='tapas')                            # ciclo
        self.assertEqual((status, json.loads(body)['error']), (409, 'category_cycle'))
        self.assertEqual(command('category-create', id='n3', name='n3', parentId='tapas')[0], 200)
        self.assertEqual(command('category-create', id='n4', name='n4', parentId='n3')[0], 200)
        self.assertEqual(json.loads(command('category-create', id='n5', name='n5', parentId='n4')[1])['error'], 'category_depth')
        # Producto: nace con la presentacion 'unit' y sin precio; hasta fijarlo no es vendible.
        status, body = command('product-create', id='coffee', name='Café', categoryId='cocina', taxId='iva-10', reference='C-1')
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
        self.assertEqual(json.loads(command('category-deactivate', id='cocina')[1])['error'], 'category_in_use')
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
        # E3.2: Configuracion elige la tarifa de la sala con las tarifas que trae /organization (sin leer el catalogo).
        self.assertEqual(next(t for t in ok('/organization')['tariffs'] if t['id'] == 'terraza'), {'id': 'terraza', 'name': 'Terraza', 'sort': 1, 'active': True})
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
        self.assertFalse(next(t for t in ok('/organization')['tariffs'] if t['id'] == 'terraza')['active'])   # E3.2: sigue, marcada
        self.assertEqual(command('price-set', tariffId='terraza', productId='wine', presentationId='glass', priceCents=1)[0], 409)   # tariff_inactive

    def test_04_csv_import_creates_and_updates_without_deleting_and_is_idempotent(self):
        folder = Path(tempfile.mkdtemp(prefix='costina-catalog-'))
        csv = folder / 'verial.csv'
        rows = ['reference;name;category;parent;presentation;price;tax;active',
                '3760;El Rapolao 2023;Tintos;Vinos;Botella;40,00;21;1',
                '3761;"Ladredo, Bierzo 2021";Tintos;Vinos;Botella;"1.250,50";21;1',
                '3762;Agua con gas;Bebidas;;Botella;2.5;10;0']
        csv.write_text('\n'.join(rows) + '\n', encoding='utf8')
        run = import_csv
        first = run(csv)
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self.assertIn('products created 3, updated 0; categories created 1; presentations created 3; prices set 3; images set 0.', first.stdout)   # E3.2: sin columna image
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
        self.assertIn('products created 0, updated 1; categories created 0; presentations created 0; prices set 1; images set 0.', third.stdout)
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

    def test_04b_csv_import_keeps_the_same_region_apart_under_each_type(self):
        """E3.1: en una bodega real la misma region cuelga de varios tipos (Rioja en Tintos y en Blancos). El importador reconoce
        la categoria por nombre Y padre: cada vino queda bajo su tipo y un segundo fichero con los mismos nombres no crea nada nuevo."""
        folder = Path(tempfile.mkdtemp(prefix='costina-catalog-'))
        csv = folder / 'bodega.csv'
        csv.write_text('\n'.join(['reference;name;category;parent;presentation;price;tax;active',
                                  '3780;Rioja tinto de prueba;Rioja;Tintos;Botella;20,00;21;1',
                                  '3781;Rioja blanco de prueba;Rioja;Blancos;Botella;18,00;21;1',
                                  '3782;Albarino de prueba;Rías Baixas;Blancos;Botella;15,00;21;1',
                                  '3783;Mencia de prueba;Rías Baixas;Tintos;Botella;16,00;21;1']) + '\n', encoding='utf8')
        run = import_csv
        first = run(csv)
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self.assertIn('categories created 5;', first.stdout)                         # Blancos, Rioja x2 y Rias Baixas x2 (Tintos ya existia)
        categories = {c['id']: c for c in catalog()['categories']}
        def under(reference):
            category = categories[product(reference)['categoryId']]
            return category['name'], categories[category['parentId']]['name']
        self.assertEqual([under(r) for r in ('3780', '3781', '3782', '3783')],
                         [('Rioja', 'Tintos'), ('Rioja', 'Blancos'), ('Rías Baixas', 'Blancos'), ('Rías Baixas', 'Tintos')])
        self.assertNotEqual(product('3780')['categoryId'], product('3781')['categoryId'])
        self.assertNotEqual(product('3782')['categoryId'], product('3783')['categoryId'])
        self.assertEqual(categories['tintos']['parentId'], 'vinos')                 # el tipo existente se reutiliza donde esta
        # Otro fichero con los mismos tipos y regiones reutiliza esas categorias; una fila sin padre usa la que ya existe con ese nombre.
        more = folder / 'mas.csv'
        more.write_text('\n'.join(['reference;name;category;parent;presentation;price;tax;active',
                                   '3784;Otro Rioja blanco;Rioja;Blancos;Botella;19,00;21;1',
                                   '3785;Sin tipo;Blancos;;Botella;9,00;21;1']) + '\n', encoding='utf8')
        second = run(more)
        self.assertEqual(second.returncode, 0, second.stdout + second.stderr)
        self.assertIn('categories created 0;', second.stdout)
        self.assertEqual(product('3784')['categoryId'], product('3781')['categoryId'])
        self.assertEqual(categories[product('3785')['categoryId']]['name'], 'Blancos')

    def test_05_commands_are_idempotent_and_audited(self):
        key = 'catalog-' + os.urandom(6).hex()
        status, body = request('/erp/catalog/commands/category-create', dict(id='postres', name='Postres'), key=key)
        self.assertEqual(status, 200, body)
        self.assertEqual(request('/erp/catalog/commands/category-create', dict(id='postres', name='Postres'), key=key), (200, body))   # replay
        self.assertIn(request('/erp/catalog/commands/category-create', dict(id='postres', name='Otro'), key=key)[0], (409, 422))       # misma clave, otro cuerpo
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='catalog.category_created' AND aggregate_id='postres'"), '1')
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='catalog.price_set' AND aggregate_id='coffee'"), '2')   # unit y double

    def test_06_product_images_are_set_read_replaced_and_removed_only_by_main(self):
        """E3.2: una imagen vigente por articulo. Solo main la pone o la quita; cualquier identidad valida la lee (una foto no es
        dinero) con su tipo, ETag = huella y cache privada que revalida; los catalogos llevan imageSha; nada se borra."""
        first = png(5000)
        status, body = set_image('water', first, 'image/png')
        self.assertEqual(status, 200, body)
        self.assertEqual(json.loads(body), {'productId': 'water', 'sha256': sha(first), 'bytes': 5000, 'contentType': 'image/png', 'changed': True})
        self.assertFalse(json.loads(set_image('water', first, 'image/png')[1])['changed'])              # la misma imagen ya vigente: nada cambia
        status, body, headers = read_image('water')
        self.assertEqual((status, body), (200, first))
        self.assertEqual((headers['content-type'], headers['etag'], headers['x-content-type-options']), ('image/png', '"' + sha(first) + '"', 'nosniff'))
        self.assertEqual(headers['cache-control'], 'private, no-cache')
        status, body, headers = read_image('water', etag='"' + sha(first) + '"')                       # el cliente ya la tiene
        self.assertEqual((status, body, headers['etag']), (304, b'', '"' + sha(first) + '"'))
        # Sala y cocina la leen pero no la cambian ni la quitan; sin credencial, nada.
        for role in ('service', 'kitchen'):
            self.assertEqual(read_image('water', role=role)[:2], (200, first), role)
            self.assertEqual(set_image('water', png(100), 'image/png', role=role)[0], 403, role)
            self.assertEqual(command('product-image-remove', role=role, productId='water')[0], 403, role)
        self.assertEqual(request('/catalog/images/water', role='nadie')[0], 401)
        # imageSha en el catalogo de edicion y en los operativos (null sin imagen), sin ninguna clave de dinero en el de sala.
        self.assertEqual((product('water')['imageSha'], product('wine')['imageSha']), (sha(first), None))
        for path, role in (('/catalog', 'service'), ('/catalog', 'main'), ('/checkout/catalog', 'main')):
            items = json.loads(request(path, role=role)[1])
            self.assertEqual({(i['id'], i['imageSha']) for i in items if i['id'] in ('water', 'wine')}, {('water', sha(first)), ('wine', None)}, path)
        self.assertEqual(list(money_keys(json.loads(request('/catalog', role='service')[1]))), [])
        # Sustituir: otro formato y otra huella; la anterior ya no sirve como ETag.
        second = jpeg(7000)
        self.assertTrue(json.loads(set_image('water', second, 'image/jpeg')[1])['changed'])
        status, body, headers = read_image('water', etag='"' + sha(first) + '"')
        self.assertEqual((status, body, headers['content-type'], headers['etag']), (200, second, 'image/jpeg', '"' + sha(second) + '"'))
        # WebP, y una imagen de justo 2 MB: ~2,8 MB de base64, por encima del tope general de 32 KB, que solo ESTA orden sube.
        self.assertEqual(set_image('wine', webp(3000), 'image/webp')[0], 200)
        largest = png(2 * 1024 * 1024)
        status, body = set_image('lubina', largest, 'image/png')
        self.assertEqual((status, json.loads(body)['bytes']), (200, 2 * 1024 * 1024), body)
        self.assertEqual(read_image('lubina')[1], largest)
        # Lo que no es una imagen segura se rechaza con su codigo y la vigente sigue igual.
        for data, declared, error in ((b'<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>', 'image/svg+xml', 'image_invalid'),
                                      (b'<html><script>alert(1)</script></html>', 'image/png', 'image_invalid'),   # HTML disfrazado
                                      (png(100), 'image/jpeg', 'image_invalid'),                                  # el tipo declarado no es el de los bytes
                                      (b'', 'image/png', 'image_invalid'),
                                      (png(2 * 1024 * 1024 + 1), 'image/png', 'image_too_large')):
            status, body = set_image('water', data, declared)
            self.assertEqual((status, json.loads(body)['error']), (409, error), declared)
        self.assertEqual(command('product-image-set', productId='water', contentType='image/png', data='no es base64!')[0], 422)
        self.assertEqual(set_image('no-existe', png(100), 'image/png')[0], 404)
        self.assertEqual(read_image('water')[1], second)
        # El tope del cuerpo es explicito por orden: product-image-set admite hasta 4 MB de JSON y cualquier otra orden 32 KB. Por
        # encima, el motor la rechaza al empezar a leer, antes de aplicar nada.
        self.assertEqual(declared_only('/erp/catalog/commands/product-image-set', 4 * 1024 * 1024 + 1)[0], 422)
        self.assertEqual(declared_only('/erp/catalog/commands/category-create', 32 * 1024 + 1)[0], 422)
        self.assertEqual(read_image('water')[1], second)
        # Quitar: la lectura da 404 y imageSha es null; quitarla otra vez no cambia nada; la fila sigue (inactiva) y ponerla la reactiva.
        status, body = command('product-image-remove', productId='water')
        self.assertEqual((status, json.loads(body)), (200, {'productId': 'water', 'changed': True}))
        self.assertFalse(json.loads(command('product-image-remove', productId='water')[1])['changed'])
        self.assertEqual(read_image('water')[0], 404)
        self.assertIsNone(product('water')['imageSha'])
        self.assertEqual([i['imageSha'] for i in ok('/catalog') if i['id'] == 'water'], [None])
        self.assertEqual(h.sql("SELECT count(*) || ':' || bool_or(active) FROM core.product_images WHERE tenant='d1-tenant' AND product_id='water'"), '1:false')
        self.assertEqual(read_image('croquetas')[0], 404)                                               # nunca tuvo
        self.assertTrue(json.loads(set_image('water', second, 'image/jpeg')[1])['changed'])
        self.assertEqual(read_image('water')[:2], (200, second))
        # Auditoria: huella, tamano y tipo de cada cambio real; nunca los bytes (ni en la auditoria ni en la respuesta guardada).
        audit = h.sql("SELECT string_agg(action || '|' || (payload->'data'->>'sha256') || '|' || coalesce(payload->'data'->>'bytes', '-') || '|' || coalesce(payload->'data'->>'content_type', '-'), ',' ORDER BY occurred_at, id) "
                      "FROM core.audit WHERE tenant='d1-tenant' AND aggregate_id='water' AND action LIKE 'catalog.product_image%'")
        self.assertEqual(audit.split(','), ['catalog.product_image_set|' + sha(first) + '|5000|image/png', 'catalog.product_image_set|' + sha(second) + '|7000|image/jpeg',
                                            'catalog.product_image_removed|' + sha(second) + '|-|-', 'catalog.product_image_set|' + sha(second) + '|7000|image/jpeg'])
        self.assertLess(int(h.sql("SELECT max(octet_length(payload::text)) FROM core.audit WHERE tenant='d1-tenant' AND action LIKE 'catalog.product_image%'")), 1000)
        self.assertLess(int(h.sql("SELECT max(octet_length(response)) FROM core.commands WHERE tenant='d1-tenant' AND response LIKE '%\"sha256\"%'")), 1000)

    def test_07_import_takes_images_from_the_csv_folder_and_never_from_outside(self):
        """E3.2: columna 'image' = fichero de la carpeta del CSV. Crea, es idempotente (la huella cubre CSV e imagenes), una foto
        cambiada con el mismo CSV cambia solo esa imagen, una celda vacia no quita nada, nada sale de la carpeta y un CSV en la raiz
        de una unidad (memoria USB) tambien vale."""
        base = Path(tempfile.mkdtemp(prefix='costina-images-'))
        folder = base / 'bodega'; (folder / 'fotos').mkdir(parents=True)
        tinto, blanco = png(3000), jpeg(4000)
        (folder / 'fotos' / 'verial-3790.png').write_bytes(tinto)
        (folder / 'verial-3791.jpg').write_bytes(blanco)
        header = 'reference;name;category;presentation;price;tax;image'
        csv = folder / 'bodega.csv'
        csv.write_text('\n'.join([header, '3790;Tinto con foto;Tintos;Botella;20,00;21;fotos/verial-3790.png',
                                  '3791;Blanco con foto;Blancos;Botella;18,00;21;verial-3791.jpg',
                                  '3792;Sin foto;Blancos;Botella;12,00;21;']) + '\n', encoding='utf8')
        first = import_csv(csv)
        self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
        self.assertIn('products created 3, updated 0;', first.stdout); self.assertIn('prices set 3; images set 2.', first.stdout)
        shas = lambda: {r: product(r)['imageSha'] for r in ('3790', '3791', '3792')}
        self.assertEqual(shas(), {'3790': sha(tinto), '3791': sha(blanco), '3792': None})
        self.assertEqual(read_image('3790')[:2], (200, tinto)); self.assertEqual(read_image('3791')[2]['content-type'], 'image/jpeg')
        stamp = lambda product_id: h.sql(f"SELECT sha256 || '@' || updated_at FROM core.product_images WHERE tenant='d1-tenant' AND product_id='{product_id}'")
        commands = lambda: h.sql("SELECT count(*) FROM core.commands WHERE tenant='d1-tenant' AND actor='cli:import-catalog'")
        before, imported = (stamp('3790'), stamp('3791')), commands()
        again = import_csv(csv)                                                                          # mismo CSV, mismas fotos: repeticion
        self.assertEqual((again.returncode, again.stdout), (0, first.stdout), again.stderr)
        self.assertEqual(((stamp('3790'), stamp('3791')), commands()), (before, imported))
        # Una foto cambiada con el CSV igual es otra importacion: cambia SOLO esa imagen.
        newer = png(3500)
        (folder / 'fotos' / 'verial-3790.png').write_bytes(newer)
        changed = import_csv(csv)
        self.assertEqual(changed.returncode, 0, changed.stdout + changed.stderr)
        self.assertIn('products created 0, updated 0; categories created 0; presentations created 0; prices set 0; images set 1.', changed.stdout)
        self.assertEqual((product('3790')['imageSha'], stamp('3791')), (sha(newer), before[1]))
        # Una celda vacia nunca quita la imagen; una imagen quitada a mano sigue quitada mientras el fichero traiga la misma.
        (folder / 'sin.csv').write_text(header + '\n3791;Blanco con foto;Blancos;Botella;18,00;21;\n', encoding='utf8')
        empty = import_csv(folder / 'sin.csv')
        self.assertEqual(empty.returncode, 0, empty.stdout + empty.stderr); self.assertIn('images set 0.', empty.stdout)
        self.assertEqual(product('3791')['imageSha'], sha(blanco))
        self.assertEqual(command('product-image-remove', productId='3791')[0], 200)
        (folder / 'otra.csv').write_text(header + '\n3791;Blanco con foto;Blancos;Botella;18,50;21;verial-3791.jpg\n', encoding='utf8')
        kept = import_csv(folder / 'otra.csv')
        self.assertEqual(kept.returncode, 0, kept.stdout + kept.stderr); self.assertIn('prices set 1; images set 0.', kept.stdout)
        self.assertIsNone(product('3791')['imageSha'])
        # Nada fuera de la carpeta del CSV: ni '..', ni ruta absoluta, ni un fichero que no existe. Error con su linea, nada aplicado.
        (base / 'fuera.png').write_bytes(png(500))
        for name in ('../fuera.png', str(base / 'fuera.png'), 'fotos/../../fuera.png', '..\\fuera.png', 'no-existe.png'):
            (folder / 'mal.csv').write_text(header + '\n3793;Fuera;Blancos;Botella;5,00;21;' + name + '\n', encoding='utf8')
            refused = import_csv(folder / 'mal.csv')
            self.assertNotEqual(refused.returncode, 0, name); self.assertIn('Line 2', refused.stdout + refused.stderr, name)
        # Una imagen invalida (SVG con extension .png, PNG con extension .jpg, un .txt) falla con SU linea y no aplica ni la fila buena.
        (folder / 'falsa.png').write_bytes(b'<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>')
        (folder / 'cambiada.jpg').write_bytes(png(500))
        (folder / 'notas.txt').write_bytes(png(500))
        for name in ('falsa.png', 'cambiada.jpg', 'notas.txt'):
            (folder / 'mal.csv').write_text('\n'.join([header, '3794;Bueno;Blancos;Botella;5,00;21;verial-3791.jpg', '3795;Malo;Blancos;Botella;5,00;21;' + name]) + '\n', encoding='utf8')
            refused = import_csv(folder / 'mal.csv')
            self.assertNotEqual(refused.returncode, 0, name); self.assertIn('Line 3 (3795)', refused.stdout + refused.stderr, name)
        self.assertEqual(h.sql("SELECT count(*) FROM core.products WHERE tenant='d1-tenant' AND id IN ('3793','3794','3795')"), '0')
        # Un CSV en la RAIZ de una unidad (una memoria USB: E:\catalogo.csv junto a E:\catalogo-imagenes\) encuentra sus fotos.
        # Solo en Windows: una unidad virtual (subst) sobre una carpeta temporal, que se quita al acabar.
        if os.name == 'nt':
            usb = base / 'usb'; (usb / 'catalogo-imagenes').mkdir(parents=True)
            memoria = webp(2500)
            (usb / 'catalogo-imagenes' / 'verial-3796.webp').write_bytes(memoria)
            (usb / 'catalogo.csv').write_text(header + '\n3796;Blanco de la memoria;Blancos;Botella;9,00;21;catalogo-imagenes/verial-3796.webp\n', encoding='utf8')
            drive = subst_drive(usb)
            self.assertIsNotNone(drive, 'no free drive letter for subst')
            try:
                at_root = import_csv(drive + '\\catalogo.csv')
            finally:
                subprocess.run(['subst', drive, '/d'], capture_output=True)
            self.assertEqual(at_root.returncode, 0, at_root.stdout + at_root.stderr); self.assertIn('images set 1.', at_root.stdout)
            self.assertEqual(product('3796')['imageSha'], sha(memoria))

    def test_08_e4d_creates_without_a_code_take_it_from_the_name(self):
        # E4d: sin codigo, el servidor lo genera del nombre (unico en su lista) y sin orden va al final.
        self.assertEqual(json.loads(command('tax-create', name='IVA súper reducido', rate=4)[1])['id'], 'iva-super-reducido')
        first = json.loads(command('category-create', name='Postres caseros')[1])
        self.assertEqual(first['id'], 'postres-caseros')
        second = json.loads(command('category-create', name='Postres caseros')[1])
        self.assertEqual((second['id'], second['sort']), ('postres-caseros-2', first['sort'] + 10))
        self.assertEqual(json.loads(command('product-create', name='Tarta de Santiago', categoryId='postres-caseros')[1])['id'], 'tarta-de-santiago')
        presentation = json.loads(command('presentation-create', productId='tarta-de-santiago', name='Ración')[1])
        self.assertEqual((presentation['id'], presentation['sort']), ('racion', 10))
        self.assertEqual(json.loads(command('tariff-create', name='Terraza de verano')[1])['id'], 'terraza-de-verano')
        self.assertIn(command('product-create', name='')[0], (409, 422))
        self.assertEqual(command('product-create', id='tarta-de-santiago', name='Otra')[0], 409)   # un codigo enviado se respeta y choca

    def test_09_e33_sections_glass_sales_moves_tax_and_import_keeps_links(self):
        # E3.3: seccion, familia, ruta, referencia y copa en los tres catalogos; las secciones reservadas; glass-sale-set, products-move y
        # products-tax-set; /catalog con ETag; la reimportacion conserva familia, IVA y estacion.
        full = catalog()
        self.assertEqual([(c['id'], c['name'], c['parentId']) for c in full['categories'] if c['id'].startswith('_')], [('_cocina', 'Cocina', None), ('_bodega', 'Bodega y bebidas', None)])
        wine = product('wine')
        self.assertEqual((wine['section'], wine['family'], wine['categoryPath']), ('bodega', 'Bebidas', ['Bebidas', 'Vinos']))   # sin estacion y sin seccion: bodega
        self.assertEqual([(p['id'], p['glass']) for p in wine['presentations']], [('glass', True), ('bottle', False)])
        menu = next(p for p in full['products'] if p['id'] == 'menu-lab-tasting')
        self.assertIsNone(menu['section'])                                                                           # los productos-menu no son de ninguna
        row = next(i for i in ok('/catalog') if i['id'] == 'wine' and i['presentationId'] == 'glass')
        self.assertEqual((row['section'], row['family'], row['categoryPath'], row['reference'], row['glass']), ('bodega', 'Bebidas', ['Bebidas', 'Vinos'], '3754', True))
        self.assertEqual(list(money_keys(ok('/catalog'))), [])
        till = next(i for i in ok('/checkout/catalog') if i['id'] == 'wine' and i['presentationId'] == 'bottle')
        self.assertEqual((till['section'], till['glass'], till['reference']), ('bodega', False, '3754'))
        # ETag: la misma lectura responde 304 con If-None-Match.
        status, body = request('/catalog', role='service')
        etag = h.LAST_HEADERS.get('ETag') or h.LAST_HEADERS.get('etag')
        self.assertEqual((status, bool(etag), h.LAST_HEADERS.get('Cache-Control') or h.LAST_HEADERS.get('cache-control')), (200, True, 'private, no-cache'))
        self.assertEqual(request('/catalog', role='service', headers={'If-None-Match': etag})[0], 304)
        # Secciones reservadas: ni alta con su codigo, ni colgarlas de otra, ni desactivarlas; cambiar el nombre, si.
        self.assertEqual(json.loads(command('category-create', id='_cocina', name='Otra')[1])['error'], 'reserved_category')
        self.assertEqual(json.loads(command('category-update', id='_bodega', parentId='bebidas')[1])['error'], 'reserved_category')
        self.assertEqual(json.loads(command('category-deactivate', id='_cocina')[1])['error'], 'reserved_category')
        self.assertEqual(command('category-update', id='_bodega', name='Bodega')[0], 200)
        self.assertEqual(command('category-update', id='_bodega', name='Bodega y bebidas')[0], 200)
        # Mover familias y productos: la familia bajo la seccion es la raiz de antes.
        self.assertEqual(command('category-update', id='bebidas', parentId='_bodega')[0], 200)
        self.assertEqual((product('wine')['section'], product('wine')['family'], product('wine')['categoryPath']), ('bodega', 'Bebidas', ['Bebidas', 'Vinos']))
        status, body = command('products-move', productIds=['wine', 'water'], categoryId='_cocina')
        self.assertEqual((status, json.loads(body)['moved']), (200, 2), body)
        self.assertEqual((product('wine')['section'], product('wine')['family'], product('wine')['categoryPath']), ('cocina', None, []))
        self.assertEqual(command('products-move', productIds=['wine'], categoryId='vinos')[0], 200)
        self.assertEqual(command('products-move', productIds=['water'], categoryId='bebidas')[0], 200)
        self.assertEqual(command('products-move', productIds=['no-existe'], categoryId='vinos')[0], 404)
        self.assertEqual(command('products-move', productIds=['wine'], categoryId='no-existe')[0], 404)
        self.assertEqual(command('category-update', id='bebidas', parentId='')[0], 200)
        # IVA en bloque.
        status, body = command('products-tax-set', productIds=['wine', 'water'], taxId='iva-4')
        self.assertEqual((status, json.loads(body)['changed']), (200, 2), body)
        self.assertEqual((product('wine')['taxId'], product('water')['taxId']), ('iva-4', 'iva-4'))
        self.assertEqual(command('products-tax-set', productIds=['wine'], taxId='iva-21')[0], 200)
        self.assertEqual(command('products-tax-set', productIds=['water'], taxId='iva-10')[0], 200)
        self.assertEqual(command('products-tax-set', productIds=['wine'], taxId='no-existe')[0], 404)
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='catalog.product_updated' AND aggregate_id='water'") >= '2', True)
        # Vinos por copa en bloque: crea la copa con su precio, repite igual con la misma clave, para con stop y reactiva despues.
        key = 'glass-' + datetime.datetime.now().strftime('%H%M%S%f')
        status, body = request('/erp/catalog/commands/glass-sale-set', dict(items=[dict(productId='water', priceCents=250)]), key=key)
        self.assertEqual((status, json.loads(body)), (200, {'marked': 1, 'priced': 1, 'stopped': 0}), body)
        self.assertEqual(request('/erp/catalog/commands/glass-sale-set', dict(items=[dict(productId='water', priceCents=250)]), key=key), (200, body))
        glass = next(i for i in ok('/checkout/catalog') if i['id'] == 'water' and i['presentationId'] == 'copa')
        self.assertEqual((glass['presentation'], glass['glass'], glass['priceCents']), ('Copa', True, 250))
        self.assertEqual(json.loads(command('glass-sale-set', items=[dict(productId='water', priceCents=300)])[1]), {'marked': 0, 'priced': 1, 'stopped': 0})
        self.assertEqual(json.loads(command('glass-sale-set', stop=['water'])[1]), {'marked': 0, 'priced': 0, 'stopped': 1})
        self.assertNotIn(('water', 'copa'), [(i['id'], i['presentationId']) for i in ok('/catalog')])
        self.assertEqual(json.loads(command('glass-sale-set', items=[dict(productId='water', priceCents=300)])[1]), {'marked': 1, 'priced': 0, 'stopped': 0})   # reactivada, mismo precio
        self.assertIn(('water', 'copa'), [(i['id'], i['presentationId']) for i in ok('/catalog')])
        self.assertEqual(command('glass-sale-set', items=[dict(productId='no-existe', priceCents=1)])[0], 404)
        self.assertIn(command('glass-sale-set', items=[dict(productId='water', priceCents=1)] * 301)[0], (409, 422))
        self.assertIn(command('glass-sale-set')[0], (409, 422))
        self.assertEqual(command('glass-sale-set', role='service', items=[dict(productId='water', priceCents=1)])[0], 403)
        self.assertEqual(json.loads(command('glass-sale-set', stop=['water'])[1])['stopped'], 1)
        # La reimportacion conserva familia, IVA y estacion de un producto que ya existe (solo nombre, referencia, precio e imagen).
        self.assertEqual(command('product-update', id='wine', stationId='cold')[0], 200)
        folder = Path(tempfile.mkdtemp(prefix='costina-e33-'))
        csv = folder / 'tienda.csv'
        csv.write_text('code;reference;name;category;parent;presentation;price;tax;active\nwine;3754;Vino de ensayo 2024;Blancos;Vinos;Botella;45,00;10;1\n', encoding='utf8')
        result = import_csv(csv)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('1 existing product(s) brought another family or tax', result.stdout)
        wine = product('wine')
        self.assertEqual((wine['name'], wine['categoryId'], wine['taxId'], wine['stationId']), ('Vino de ensayo 2024', 'vinos', 'iva-21', 'cold'))
        self.assertEqual(max((p for p in next(s for s in wine['presentations'] if s['id'] == 'bottle')['prices'] if p['tariffId'] == 'general'), key=lambda p: p['validFrom'])['priceCents'], 4500)
        self.assertEqual(command('product-update', id='wine', name='Vino de ensayo', stationId='')[0], 200)

if __name__ == '__main__':
    unittest.main(verbosity=1)
