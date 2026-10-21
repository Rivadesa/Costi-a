"""D3.6 (F07, #37): cuentas cerradas anticipadamente admiten reapertura auditada y devoluciones desde el
puesto principal; un comandero (sala) solo marca consumos a mayores, sin ver importes.
C3b: la ronda en una orden (add-consumptions), la defensa contra el doble cargo con la misma clave desde otro actor,
el texto de la linea sin la presentacion por defecto y el aviso operativo de los consumos."""
import json
import secrets
import unittest
import native_http as h

MONEY = ('pricecents', 'paidcents', 'balancecents', 'creditcents', 'totalcents', 'amountcents', 'refundedcents')

def account_cmd(sid, action, role='main', **fields):
    return h.request('/checkout/services/' + sid + '/commands/' + action,
                     dict(expectedVersion=h.account(sid)['version'], **fields), role=role)

def round_(sid, lines, role='service', key=None, version=None):
    """C3b: una ronda = UNA orden add-consumptions con todas sus lineas."""
    return h.request('/dining/services/' + sid + '/commands/add-consumptions',
                     {'expectedVersion': h.dining(sid)['version'] if version is None else version, 'lines': lines}, role=role, key=key)

def pair_device(name):
    """Comandero emparejado de verdad (actor device:<nombre>), como los de la revision externa del 19-10. Su token se guarda
    en h.KEYS con su nombre como si fuera un rol, para usar h.request."""
    issued = h.ok('/auth/pairings', {})
    claimed = h.ok('/auth/pairings/claim', {'code': issued['code'], 'deviceName': name}, role='anonimo')
    h.ok('/auth/pairings/' + claimed['pairingId'] + '/approve', {'role': 'service', 'station': 'sala-1'})
    h.KEYS[name] = h.ok('/auth/pairings/' + claimed['pairingId'] + '/collect', {'pollSecret': claimed['pollSecret']}, role='anonimo')['deviceToken']
    return name

def notices(sid):
    """Avisos operativos de los consumos de un servicio en el outbox (C3b)."""
    return int(h.sql("SELECT count(*) FROM core.outbox WHERE tenant='d3-checkout' AND aggregate_id='" + sid + "' AND type='service.consumptions_changed'"))

class CheckoutChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.ENV['COSTINA_TENANT'] = 'd3-checkout'
        h.NativeHttp.setUpClass()
        cls.sid = h.open_table('M1')
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_prepaid_and_closed_account_rejects_changes_until_reopened(self):
        h.charge(self.sid, 'payment', paymentId='prepay', method='card', amountCents=30000)
        h.charge(self.sid, 'close')
        data = h.account(self.sid)['data']
        self.assertEqual('Closed', data['state']); self.assertEqual(['reopen'], data['actions'])
        status, body = account_cmd(self.sid, 'add-product', productId='water', quantity=1)
        self.assertEqual(409, status); self.assertIn(b'account_closed', body)
        status, body = h.request('/dining/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=h.dining(self.sid)['version'], productId='water', quantity=1), role='service')
        self.assertEqual(409, status); self.assertIn(b'account_closed', body)   # el comandero recibe rechazo explicito
        self.assertEqual(403, account_cmd(self.sid, 'reopen', role='service', reason='x')[0])   # sala no gestiona la cuenta
        self.assertEqual(422, account_cmd(self.sid, 'reopen', reason='')[0])                    # motivo obligatorio
        self.assertEqual(200, account_cmd(self.sid, 'reopen', reason='bebida pedida tras cerrar')[0])
        data = h.account(self.sid)['data']
        self.assertEqual('Open', data['state']); self.assertIn('add-product', data['actions']); self.assertNotIn('reopen', data['actions'])
        self.assertEqual(1, len(data['payments']))

    def test_02_waiter_adds_consumption_without_seeing_money(self):
        self.assertIn('add-consumption', h.ok('/session', role='service')['actions'])
        self.assertNotIn('add-consumption', h.ok('/session', role='kitchen')['actions'])
        version = h.dining(self.sid)['version']
        status, body = h.request('/dining/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=2), role='kitchen')
        self.assertEqual(403, status)   # cocina no marca consumos
        status, body = h.request('/dining/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=2), role='service')
        self.assertEqual(200, status, body)
        response = json.loads(body)
        self.assertEqual({'version', 'consumptionId', 'productId', 'presentationId', 'quantity'}, set(response))   # E3: presentacion vendida
        for field in MONEY: self.assertNotIn(field, body.decode().lower())
        self.assertEqual(version, response['version'])   # el contexto operativo no cambia por un consumo
        self.assertEqual(800, h.account(self.sid)['data']['balanceCents'])   # 2 x 400 fijados por el catalogo
        status, body = h.request('/dining/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=1, unitPriceCents=1), role='service')
        self.assertEqual(422, status)   # ningun precio libre desde un comandero
        self.assertEqual(404, h.request('/dining/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='no-existe', quantity=1), role='service')[0])
        self.assertIn('add-consumptions', h.ok('/session', role='service')['actions'])        # C3b: la ronda en una orden
        self.assertNotIn('add-consumptions', h.ok('/session', role='kitchen')['actions'])

    def test_03_overpayment_credit_is_refunded_before_an_honest_close(self):
        h.charge(self.sid, 'payment', paymentId='pay2', method='cash', amountCents=1400)
        data = h.account(self.sid)['data']
        self.assertEqual(600, data['creditCents']); self.assertIn('refund', data['actions']); self.assertNotIn('close', data['actions'])
        status, body = account_cmd(self.sid, 'refund', refundId='rf1', method='cash', amountCents=700, reason='cobro de más')
        self.assertEqual(409, status); self.assertIn(b'refund_exceeds_credit', body)
        self.assertEqual(422, account_cmd(self.sid, 'refund', refundId='rf1', method='cash', amountCents=600, reason='')[0])
        self.assertEqual(403, account_cmd(self.sid, 'refund', role='service', refundId='rf1', method='cash', amountCents=600, reason='x')[0])
        self.assertEqual(200, account_cmd(self.sid, 'refund', refundId='rf1', method='cash', amountCents=600, reason='cobro de más')[0])
        data = h.account(self.sid)['data']
        self.assertEqual(0, data['creditCents']); self.assertEqual(600, data['refundedCents']); self.assertEqual(30800, data['paidCents'])
        self.assertEqual(2, len(data['payments'])); self.assertEqual(1, len(data['refunds'])); self.assertIn('close', data['actions'])
        h.charge(self.sid, 'close')
        self.assertEqual('Closed', h.account(self.sid)['data']['state'])
        status, body = account_cmd(self.sid, 'refund', refundId='rf2', method='cash', amountCents=1, reason='x')
        self.assertEqual(409, status); self.assertIn(b'account_closed', body)

    def test_04_history_and_events_are_audited(self):
        for kind in ('account.reopened', 'payment.refunded'):
            self.assertEqual('1', h.sql("SELECT count(*) FROM core.outbox WHERE tenant='d3-checkout' AND type='" + kind + "'"))
        self.assertEqual('1', h.sql("SELECT count(*) FROM core.audit WHERE tenant='d3-checkout' AND action='account.reopened' AND payload->'data'->>'reason'='bebida pedida tras cerrar'"))
        for role in ('service', 'kitchen'):
            self.assertEqual(403, h.request('/checkout/services/' + self.sid, role=role)[0])

    def test_05_a_round_is_one_all_or_nothing_order_without_money(self):
        # C3b: la ronda viaja como UNA orden con varias lineas; todo o nada; una linea mala dice cual es (422 con su indice).
        sid = h.open_table('M2')
        version = h.dining(sid)['version']
        before = len(h.account(sid)['data']['charges'])
        lines = [{'productId': 'water', 'quantity': 2}, {'productId': 'wine', 'presentationId': 'glass', 'quantity': 1, 'guestPosition': 1}]
        key = secrets.token_hex(16)
        status, body = round_(sid, lines, key=key)
        self.assertEqual(200, status, body)
        response = json.loads(body)
        self.assertEqual({'version', 'consumptionIds'}, set(response))                  # sin dinero
        for field in MONEY: self.assertNotIn(field, body.decode().lower())
        self.assertEqual(version, response['version'])                                  # la version del servicio no se mueve
        charges = h.account(sid)['data']['charges']
        self.assertEqual(response['consumptionIds'], [c['id'] for c in charges[before:]])
        water, wine = charges[before:]
        self.assertEqual(('water', 'bottle', 2, None, 'consumption', 800), (water['productId'], water['presentationId'], water['quantity'], water['guestPosition'], water['origin'], water['totalCents']))
        self.assertEqual(('wine', 'glass', 1, 1, 'consumption', 950), (wine['productId'], wine['presentationId'], wine['quantity'], wine['guestPosition'], wine['origin'], wine['totalCents']))
        self.assertTrue(water['at'] and wine['at'])                                    # hora de la linea
        # Misma clave, mismo actor: la misma respuesta y ningun cargo mas.
        status, again = round_(sid, lines, key=key, version=version)
        self.assertEqual((200, response), (status, json.loads(again)))
        self.assertEqual(before + 2, len(h.account(sid)['data']['charges']))
        # Todo o nada: con una linea mala no se apunta ninguna, y el rechazo dice cual (indice desde 0).
        for bad, index, code in (
                ([{'productId': 'water', 'quantity': 1}, {'productId': 'no-existe', 'quantity': 1}], 1, 'not_found'),
                ([{'productId': 'water', 'quantity': 0}], 0, 'invalid_request'),
                ([{'productId': 'water', 'quantity': 1}, {'productId': 'water', 'quantity': 100}], 1, 'invalid_request'),
                ([{'productId': 'water'}], 0, 'invalid_request'),                         # cantidad obligatoria
                ([{'quantity': 1}], 0, 'invalid_request'),                               # producto obligatorio
                ([{'productId': 'water', 'quantity': 1, 'guestPosition': 3}], 0, 'invalid_request'),   # la mesa es de 2
                ([{'productId': 'wine', 'quantity': 1}], 0, 'invalid_request'),           # dos presentaciones: hay que elegir
                ([{'productId': 'water', 'quantity': 1}, {'productId': 'croquetas', 'presentationId': 'unit', 'quantity': 1}], 1, 'use_add_dish')):
            status, body = round_(sid, bad)
            self.assertEqual(422, status, (bad, body))
            self.assertEqual({'error': code, 'line': index}, json.loads(body), bad)
        self.assertEqual(before + 2, len(h.account(sid)['data']['charges']))
        status, body = round_(sid, [{'productId': 'water', 'quantity': 1}] * 31)
        self.assertEqual((409, 'too_many_lines'), (status, json.loads(body)['error']))
        self.assertEqual(200, round_(sid, [{'productId': 'water', 'quantity': 1}] * 30)[0])   # 30 lineas, el tope
        self.assertEqual(422, round_(sid, [])[0])
        self.assertEqual(422, round_(sid, [{'productId': 'water', 'quantity': 1, 'unitPriceCents': 1}])[0])   # ningun precio libre
        self.assertEqual(403, round_(sid, lines, role='kitchen')[0])                     # cocina no apunta consumos
        self.assertEqual(409, round_(sid, lines, version=version + 7)[0])                # version_conflict
        self.assertEqual(before + 32, len(h.account(sid)['data']['charges']))

    def test_06_the_same_key_from_two_device_actors_charges_once(self):
        # Revision externa del 19-10 (diferido de D6.7): la idempotencia del servidor es por actor, asi que la MISMA clave
        # reenviada por otro actor (el comandero emparejado de nuevo con otro nombre) era una orden nueva y cobraba dos veces.
        # Ahora el identificador de cada linea sale del servicio, la clave y el indice, y la cuenta no admite dos iguales.
        sid = h.open_table('M3')
        first, second = pair_device('comandero-c3b-a'), pair_device('comandero-c3b-b')
        before = len(h.account(sid)['data']['charges'])
        body = {'expectedVersion': h.dining(sid)['version'], 'lines': [{'productId': 'water', 'quantity': 1}, {'productId': 'wine', 'presentationId': 'glass', 'quantity': 2}]}
        key = secrets.token_hex(16)
        path = '/dining/services/' + sid + '/commands/add-consumptions'
        status_a, body_a = h.request(path, body, role=first, key=key)
        status_b, body_b = h.request(path, body, role=second, key=key)
        self.assertEqual((200, 200), (status_a, status_b), (body_a, body_b))
        self.assertEqual(before + 2, len(h.account(sid)['data']['charges']), 'the same order charged twice')
        self.assertEqual(json.loads(body_a)['consumptionIds'], json.loads(body_b)['consumptionIds'])
        # La misma clave con OTRAS lineas desde un tercer actor: rechazo claro y nada apuntado.
        status, rejected = h.request(path, dict(body, lines=[{'productId': 'water', 'quantity': 3}]), role='service', key=key)
        self.assertEqual((409, 'duplicate_consumption'), (status, json.loads(rejected)['error']))
        self.assertEqual(before + 2, len(h.account(sid)['data']['charges']))

    def test_06b_the_same_single_consumption_from_two_device_actors_charges_once(self):
        # Lo mismo con la orden de un solo consumo (add-consumption), que el comandero de C3a sigue usando.
        sid = h.open_table('M5')
        first, second = pair_device('comandero-c3b-c'), pair_device('comandero-c3b-d')
        before = len(h.account(sid)['data']['charges'])
        single = {'expectedVersion': h.dining(sid)['version'], 'productId': 'water', 'quantity': 1}
        key = secrets.token_hex(16)
        path = '/dining/services/' + sid + '/commands/add-consumption'
        status_a, body_a = h.request(path, single, role=first, key=key)
        status_b, body_b = h.request(path, single, role=second, key=key)
        self.assertEqual((200, 200), (status_a, status_b), (body_a, body_b))
        self.assertEqual(before + 1, len(h.account(sid)['data']['charges']), 'the same consumption charged twice')
        self.assertEqual(json.loads(body_a), json.loads(body_b))
        status, rejected = h.request(path, dict(single, quantity=2), role='service', key=key)
        self.assertEqual((409, 'duplicate_consumption'), (status, json.loads(rejected)['error']))
        self.assertEqual(before + 1, len(h.account(sid)['data']['charges']))

    def test_07_line_text_and_operational_notice_from_the_table_and_from_checkout(self):
        # C3b: la linea dice "Tonica", no "Tonica · Unidad" (la presentacion por defecto no se escribe), desde la ronda, desde el
        # consumo suelto y desde Caja; y cada cambio de consumos (tambien anular en Caja) deja el aviso operativo del servicio.
        h.ok('/erp/catalog/commands/product-create', {'id': 'tonica', 'name': 'Tónica', 'categoryId': 'bebidas', 'taxId': 'iva-10'})
        h.ok('/erp/catalog/commands/price-set', {'tariffId': 'general', 'productId': 'tonica', 'presentationId': 'unit', 'priceCents': 250})
        sid = h.open_table('M4')
        self.assertEqual(0, notices(sid))
        before = len(h.account(sid)['data']['charges'])
        self.assertEqual(200, round_(sid, [{'productId': 'tonica', 'quantity': 1}, {'productId': 'wine', 'presentationId': 'glass', 'quantity': 1}])[0])
        self.assertEqual(1, notices(sid))                                               # una orden, un aviso
        self.assertEqual(200, h.request('/dining/services/' + sid + '/commands/add-consumption',
            {'expectedVersion': h.dining(sid)['version'], 'productId': 'tonica', 'quantity': 1}, role='service')[0])
        self.assertEqual(2, notices(sid))
        self.assertEqual(200, account_cmd(sid, 'add-product', productId='tonica', quantity=1)[0])
        self.assertEqual(3, notices(sid))
        charges = h.account(sid)['data']['charges'][before:]
        self.assertEqual(['Tónica', 'Vino de ensayo · Copa', 'Tónica', 'Tónica'], [c['description'] for c in charges])
        self.assertEqual(['consumption', 'consumption', 'consumption', 'checkout'], [c['origin'] for c in charges])
        self.assertEqual(200, account_cmd(sid, 'void-charge', chargeId=charges[-1]['id'], reason='no la quiso')[0])
        self.assertEqual(4, notices(sid))                                               # anular en Caja tambien avisa a sala
        # Auditado con la clave de su orden, y sin datos: el aviso solo dice QUE servicio cambio (la sala relee el tablero).
        self.assertEqual('4', h.sql("SELECT count(*) FROM core.audit WHERE tenant='d3-checkout' AND aggregate_id='" + sid + "' AND action='service.consumptions_changed' AND command_key<>''"))
        self.assertEqual('{}', h.sql("SELECT DISTINCT payload->>'data' FROM core.outbox WHERE tenant='d3-checkout' AND aggregate_id='" + sid + "' AND type='service.consumptions_changed'"))
        # El menu y los platos de la carta conservan su texto (la demo llama "Racion" a la presentacion por defecto de un plato).
        menu = h.account(sid)['data']['charges'][0]
        self.assertEqual(('menu-lab-tasting', 'menu', 'Menú de ensayo · Por persona'), (menu['productId'], menu['origin'], menu['description']))

if __name__ == '__main__': unittest.main()
