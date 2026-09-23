"""E4a (Hito 6): OFERTA del nucleo - degustaciones y menus cerrados con pases y platos - por HTTP real contra PostgreSQL.
Una oferta cerrada se vende como producto del catalogo por persona (E3) a la tarifa de la sala; solo el puesto principal
edita; nunca se borra, se desactiva; solo las ofertas vigentes se pueden abrir; en un menu cerrado cada comensal elige su
plato antes de disparar el pase (choose), y el precio del menu queda congelado en la cuenta con su origen.
"""
import datetime
import json
import re
import unittest

import native_http as h

ok, request = h.ok, h.request
MONEY = re.compile(r'(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)', re.I)

def command(action, role='main', **body):
    return request('/erp/offers/commands/' + action, body, role=role)

def offers():
    return ok('/erp/offers')

def offer(oid):
    return next(o for o in offers() if o['id'] == oid)

def money_keys(value, path=''):
    if isinstance(value, dict):
        for k, v in value.items():
            if MONEY.search(k): yield path + '/' + k
            yield from money_keys(v, path + '/' + k)
    elif isinstance(value, list):
        for i, v in enumerate(value): yield from money_keys(v, path + '/' + str(i))

def free_table():
    occupied = {r['service']['tableId'] for r in ok('/dining/board')}
    free = [t['id'] for t in ok('/configuration')['tables'] if t['id'] not in occupied and t['zoneId'] != 'terraza-z']
    if free: return free[0]
    assert request('/organization/commands/table-create', dict(id='OFF1', name='Oferta 1', capacity=4, zoneId='sala'))[0] in (200, 409)
    return 'OFF1'

def open_with(table, offer_id, pax=2, role='main', **extra):
    return request('/dining/services', dict(tableId=table, pax=pax, offerId=offer_id, **extra), role=role)

class Offers(unittest.TestCase):
    @classmethod
    def setUpClass(cls): h.start_server()
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def release(self, sid):
        h.mutate(sid, 'cancel-unstarted', reason='prueba')
        version = next(r['occupancyVersion'] for r in ok('/dining/board') if r['service']['id'] == sid)
        ok('/dining/occupancy/' + sid + '/release', {'expectedVersion': version, 'reason': 'prueba'})

    def test_01_demo_offers_are_core_data_sold_as_catalog_products(self):
        all_offers = offers()
        self.assertEqual([(o['id'], o['kind'], o['productId']) for o in all_offers], [('LAB-TASTING', 'tasting', 'menu-lab-tasting'), ('LAB-DAILY', 'set-menu', 'menu-lab-daily')])
        tasting = offer('LAB-TASTING')
        self.assertEqual([(c['id'], [d['id'] for d in c['dishes']]) for c in tasting['courses']], [('p1', ['frio', 'caliente']), ('p2', ['principal'])])
        self.assertEqual(tasting['courses'][0]['dishes'][0]['stationId'], 'cold')
        # El producto-menu vive en el catalogo (categoria 'menus', presentacion 'person', precio en la general).
        product = next(p for p in ok('/erp/catalog')['products'] if p['id'] == 'menu-lab-tasting')
        self.assertEqual((product['categoryId'], [(s['id'], s['prices'][0]['priceCents']) for s in product['presentations']]), ('menus', [('person', 15000)]))
        for role in ('service', 'kitchen'):
            self.assertEqual(request('/erp/offers', role=role)[0], 403, role)
            self.assertEqual(command('offer-create', role=role, id='x', name='x')[0], 403, role)
        # Configuracion operativa (todos los roles): ofertas vigentes con pases y platos, sin dinero; 'menus' sigue como alias (id, name).
        for role in ('main', 'service', 'kitchen'):
            cfg = ok('/configuration', role=role)
            self.assertEqual([(o['id'], o['kind'], len(o['courses'])) for o in cfg['offers']], [('LAB-TASTING', 'tasting', 2), ('LAB-DAILY', 'set-menu', 2)], role)
            self.assertEqual([m['id'] for m in cfg['menus']], ['LAB-TASTING', 'LAB-DAILY'])
            self.assertEqual(list(money_keys(cfg['offers'])), [])
        self.assertEqual({'id', 'name', 'kind', 'courses'}, set(cfg['offers'][0])); self.assertEqual({'id', 'name', 'dishes'}, set(cfg['offers'][0]['courses'][0]))

    def test_02_offers_courses_and_dishes_are_edited_and_never_deleted(self):
        # Alta de un menu cerrado: nace con su producto-menu (categoria menus, presentacion person) y su precio en la general.
        status, body = command('offer-create', id='DEGUSTA', name='Degustación de otoño', kind='tasting', priceCents=9500, service='dinner')
        self.assertEqual(status, 200, body)
        self.assertEqual(json.loads(body)['productId'], 'menu-degusta')
        product = next(p for p in ok('/erp/catalog')['products'] if p['id'] == 'menu-degusta')
        self.assertEqual((product['categoryId'], product['taxId'], product['presentations'][0]['id'], product['presentations'][0]['prices'][0]['priceCents']), ('menus', 'iva-10', 'person', 9500))
        self.assertEqual(command('offer-create', id='DEGUSTA', name='Otra')[0], 409)                        # codigo unico
        self.assertEqual(json.loads(command('offer-create', id='CARTA', name='Carta', kind='a-la-carte')[1])['error'], 'kind_unsupported')   # E4b
        self.assertEqual(command('offer-create', id='X1', name='x', productId='no-existe')[0], 404)
        self.assertEqual(json.loads(command('offer-create', id='X1', name='x', productId='water')[1])['error'], 'presentation_required')   # no se vende por persona
        for bad in (dict(id='X1', name='x', weekdays=0), dict(id='X1', name='x', validFrom='2026-10-10', validTo='2026-10-01'), dict(id='X1', name='x', validFrom='hoy'), dict(id='con espacio', name='x')):
            self.assertIn(command('offer-create', **bad)[0], (409, 422), bad)
        # Pases y platos: la estacion tiene que existir y estar activa; el producto del plato (opcional) tambien.
        self.assertEqual(command('course-create', offerId='DEGUSTA', id='c1', name='Entrantes')[0], 200)
        self.assertEqual(command('course-create', offerId='DEGUSTA', id='c1', name='Otro')[0], 409)
        self.assertEqual(command('course-create', offerId='no-existe', id='c1', name='x')[0], 404)
        self.assertEqual(command('dish-create', offerId='DEGUSTA', courseId='c1', id='ostra', name='Ostra', stationId='cold')[0], 200)
        self.assertEqual(command('dish-create', offerId='DEGUSTA', courseId='c1', id='x', name='x', stationId='no-existe')[0], 404)
        self.assertEqual(command('dish-create', offerId='DEGUSTA', courseId='c1', id='x', name='x', stationId='cold', productId='no-existe')[0], 404)
        self.assertEqual(command('dish-create', offerId='DEGUSTA', courseId='c1', id='vieira', name='Vieira', stationId='hot', productId='wine')[0], 200)
        self.assertEqual(command('dish-update', offerId='DEGUSTA', courseId='c1', id='vieira', name='Vieira a la brasa', productId='')[0], 200)
        self.assertEqual([(d['id'], d['name'], d['productId']) for d in offer('DEGUSTA')['courses'][0]['dishes']], [('ostra', 'Ostra', None), ('vieira', 'Vieira a la brasa', None)])
        self.assertEqual(command('offer-update', id='DEGUSTA', name='Degustación de otoño 2026', weekdays=96)[0], 200)   # sabado y domingo
        self.assertEqual((offer('DEGUSTA')['name'], offer('DEGUSTA')['weekdays'], offer('DEGUSTA')['service']), ('Degustación de otoño 2026', 96, 'dinner'))
        # Vigencia: cena de fin de semana -> hoy y ahora solo si coincide; se comprueba con una oferta claramente NO vigente.
        self.assertEqual(command('offer-update', id='DEGUSTA', validFrom='2099-01-01', validTo='2099-12-31')[0], 200)
        self.assertNotIn('DEGUSTA', [o['id'] for o in ok('/configuration')['offers']])
        self.assertEqual(json.loads(open_with('M1', 'DEGUSTA')[1])['error'], 'offer_unavailable')
        self.assertEqual(command('offer-update', id='DEGUSTA', validFrom='', validTo='', service='any', weekdays=127)[0], 200)
        self.assertIn('DEGUSTA', [o['id'] for o in ok('/configuration')['offers']])
        # Desactivar un pase deja la oferta sin pases -> no se puede abrir (offer_incomplete) ni aparece; reactivar la devuelve.
        self.assertEqual(command('course-deactivate', offerId='DEGUSTA', id='c1')[0], 200)
        self.assertNotIn('DEGUSTA', [o['id'] for o in ok('/configuration')['offers']])
        self.assertEqual(json.loads(open_with('M1', 'DEGUSTA')[1])['error'], 'offer_incomplete')
        self.assertEqual(command('course-reactivate', offerId='DEGUSTA', id='c1')[0], 200)
        self.assertEqual(command('offer-deactivate', id='DEGUSTA')[0], 200)
        self.assertFalse(offer('DEGUSTA')['active']); self.assertNotIn('DEGUSTA', [o['id'] for o in ok('/configuration')['offers']])
        self.assertEqual(command('offer-reactivate', id='DEGUSTA')[0], 200)
        self.assertEqual(command('nada', id='x')[0], 403)

    def test_03_a_tasting_opens_as_before_and_charges_the_menu_product_at_the_zone_tariff(self):
        table = free_table()
        status, body = open_with(table, 'LAB-TASTING', pax=2)
        self.assertEqual(status, 200, body); sid = json.loads(body)['serviceId']
        dining = h.dining(sid)['data']
        self.assertEqual(dining['offerId'], 'LAB-TASTING')
        self.assertEqual([(c['id'], c['choiceRequired'], len(c['preparations'])) for c in dining['courses']], [('p1', False, 4), ('p2', False, 2)])
        charge = h.account(sid)['data']['charges'][0]
        self.assertEqual((charge['description'], charge['quantity'], charge['unitPriceCents'], charge['productId'], charge['presentationId'], charge['tariffId']), ('Menú de ensayo · Por persona', 2, 15000, 'menu-lab-tasting', 'person', 'general'))
        self.assertNotIn('choose', [a for c in dining['courses'] for a in c.get('actions', [])])
        # menuId sigue valiendo como alias una version.
        self.release(sid)
        status, body = request('/dining/services', dict(tableId=table, pax=1, menuId='LAB-TASTING'))
        self.assertEqual(status, 200, body); self.release(json.loads(body)['serviceId'])
        # Un menu sin precio no abre mesas (offer_unpriced) hasta fijarlo en el catalogo.
        self.assertEqual(command('offer-create', id='SINPRECIO', name='Sin precio', kind='tasting')[0], 200)
        self.assertEqual(command('course-create', offerId='SINPRECIO', id='c1', name='Único')[0], 200)
        self.assertEqual(command('dish-create', offerId='SINPRECIO', courseId='c1', id='d1', name='Plato', stationId='hot')[0], 200)
        self.assertEqual(json.loads(open_with(table, 'SINPRECIO')[1])['error'], 'offer_unpriced')
        self.assertEqual(request('/erp/catalog/commands/price-set', dict(tariffId='general', productId='menu-sinprecio', presentationId='person', priceCents=1000))[0], 200)
        status, body = open_with(table, 'SINPRECIO', pax=3)
        self.assertEqual(status, 200, body)
        self.assertEqual(h.account(json.loads(body)['serviceId'])['data']['totalCents'], 3000)
        self.release(json.loads(body)['serviceId'])
        self.assertEqual(request('/dining/services', dict(tableId=table, pax=1))[0], 422)                   # sin oferta

    def test_04_a_set_menu_needs_every_guest_to_choose_before_firing(self):
        table = free_table()
        status, body = open_with(table, 'LAB-DAILY', pax=2)
        self.assertEqual(status, 200, body); sid = json.loads(body)['serviceId']
        dining = h.dining(sid)['data']
        self.assertEqual([(c['id'], c['choiceRequired'], len(c['preparations'])) for c in dining['courses']], [('primeros', True, 0), ('segundos', True, 0)])
        self.assertEqual(h.account(sid)['data']['charges'][0]['unitPriceCents'], 1800)
        self.assertIn('choose', dining['courses'][0]['actions'])                                            # main y sala eligen; cocina no
        self.assertIn('choose', ok('/dining/services/' + sid, role='service')['data']['courses'][0]['actions'])
        self.assertNotIn('choose', ok('/dining/services/' + sid, role='kitchen')['data']['courses'][0].get('actions', []))
        h.mutate(sid, 'start')
        status, body = h.request('/dining/services/' + sid + '/commands/fire-next', dict(expectedVersion=h.dining(sid)['version']))
        self.assertEqual((status, json.loads(body)['error']), (409, 'choice_missing'))
        # Elecciones: comensal 1 ensalada, comensal 2 sopa (desde sala); cambiar de idea antes de disparar vale; plato ajeno no.
        status, body = h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=1, dishId='ensalada'), role='service')
        self.assertEqual(status, 200, body)
        self.assertEqual(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=2, dishId='pescado'))[0], 404)   # no es de ese pase
        self.assertEqual(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=3, dishId='sopa'))[0], 409)      # comensal inexistente
        self.assertEqual(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=1, dishId='sopa'))[0], 200)     # cambia de idea
        self.assertEqual(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=2, dishId='sopa'))[0], 200)
        course = h.dining(sid)['data']['courses'][0]
        self.assertEqual(sorted((p['id'], p['guestPosition'], p['stationId']) for p in course['preparations']), [('sopa-1', 1, 'hot'), ('sopa-2', 2, 'hot')])
        h.mutate(sid, 'fire-next')
        self.assertEqual(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='primeros', guestPosition=1, dishId='ensalada'))[0], 409)   # ya disparado
        self.assertNotIn('choose', h.dining(sid)['data']['courses'][0].get('actions', []))
        # Cocina trabaja el pase como siempre; el segundo pase sigue pidiendo elecciones.
        for prep in ('sopa-1', 'sopa-2'):
            h.mutate(sid, 'preparation-start', courseId='primeros', itemId=prep); h.mutate(sid, 'preparation-ready', courseId='primeros', itemId=prep)
        h.mutate(sid, 'ready', courseId='primeros'); h.mutate(sid, 'serve', courseId='primeros')
        status, body = h.request('/dining/services/' + sid + '/commands/fire-next', dict(expectedVersion=h.dining(sid)['version']))
        self.assertEqual(json.loads(body)['error'], 'choice_missing')
        # Un plato desactivado no se puede elegir; omitir el pase sigue siendo posible sin elecciones.
        self.assertEqual(command('dish-deactivate', offerId='LAB-DAILY', courseId='segundos', id='carne')[0], 200)
        self.assertEqual(json.loads(h.request('/dining/services/' + sid + '/commands/choose', dict(expectedVersion=h.dining(sid)['version'], courseId='segundos', guestPosition=1, dishId='carne'))[1])['error'], 'dish_unavailable')
        self.assertEqual(command('dish-reactivate', offerId='LAB-DAILY', courseId='segundos', id='carne')[0], 200)
        h.mutate(sid, 'skip', courseId='segundos', reason='sin tiempo')
        h.mutate(sid, 'complete')
        self.assertEqual(h.dining(sid)['data']['state'], 'Completed')

    def test_05_commands_are_idempotent_and_audited(self):
        key = 'offer-' + datetime.datetime.now().strftime('%H%M%S%f')
        status, body = request('/erp/offers/commands/course-create', dict(offerId='LAB-DAILY', id='postres', name='Postres'), key=key)
        self.assertEqual(status, 200, body)
        self.assertEqual(request('/erp/offers/commands/course-create', dict(offerId='LAB-DAILY', id='postres', name='Postres'), key=key), (200, body))
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='offer.course_created' AND aggregate_id='LAB-DAILY'"), '1')
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='course.chosen'"), '3')

if __name__ == '__main__':
    unittest.main(verbosity=1)
