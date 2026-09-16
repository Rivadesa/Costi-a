"""D3.6 (F07, #37): cuentas cerradas anticipadamente admiten reapertura auditada y devoluciones desde el
puesto principal; un comandero (sala) solo marca consumos a mayores, sin ver importes."""
import json
import unittest
import native_http as h

MONEY = ('pricecents', 'paidcents', 'balancecents', 'creditcents', 'totalcents', 'amountcents', 'refundedcents')

def account_cmd(sid, action, role='main', **fields):
    return h.request('/checkout/services/' + sid + '/commands/' + action,
                     dict(expectedVersion=h.account(sid)['version'], **fields), role=role)

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
        status, body = h.request('/services/' + self.sid + '/commands/add-consumption',
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
        status, body = h.request('/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=2), role='kitchen')
        self.assertEqual(403, status)   # cocina no marca consumos
        status, body = h.request('/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=2), role='service')
        self.assertEqual(200, status, body)
        response = json.loads(body)
        self.assertEqual({'version', 'chargeId', 'productId', 'quantity'}, set(response))
        for field in MONEY: self.assertNotIn(field, body.decode().lower())
        self.assertEqual(version, response['version'])   # el contexto operativo no cambia por un consumo
        self.assertEqual(800, h.account(self.sid)['data']['balanceCents'])   # 2 x 400 fijados por el catalogo
        status, body = h.request('/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='water', quantity=1, unitPriceCents=1), role='service')
        self.assertEqual(422, status)   # ningun precio libre desde un comandero
        self.assertEqual(404, h.request('/services/' + self.sid + '/commands/add-consumption',
            dict(expectedVersion=version, productId='no-existe', quantity=1), role='service')[0])

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
            self.assertEqual('1', h.sql("SELECT count(*) FROM native_d1.outbox WHERE tenant='d3-checkout' AND type='" + kind + "'"))
        self.assertEqual('1', h.sql("SELECT count(*) FROM native_d1.audit WHERE tenant='d3-checkout' AND action='account.reopened' AND payload->'data'->>'reason'='bebida pedida tras cerrar'"))
        for role in ('service', 'kitchen'):
            self.assertEqual(403, h.request('/checkout/services/' + self.sid, role=role)[0])

if __name__ == '__main__': unittest.main()
