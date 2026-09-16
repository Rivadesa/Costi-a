"""D3.2: restricciones por comensal y protocolo de acuse de cocina sobre HTTP real."""
import unittest
import native_http as h

def act(sid, action, role='main', **fields):
    return h.ok('/services/' + sid + '/commands/' + action,
                dict(expectedVersion=h.dining(sid)['version'], **fields), role=role)

def free_table():
    occupied = {b['service']['tableId'] for b in h.ok('/board')}
    return next('M' + str(i) for i in range(1, 9) if 'M' + str(i) not in occupied)

class RestrictionChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.NativeHttp.setUpClass()
        cls.sid = h.open_table(free_table())
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_declare_structured_restriction_from_service_role(self):
        act(self.sid, 'declare-restriction', role='service', guestPosition=1, kind='allergy',
            substance='marisco', severity='severe')
        data = h.dining(self.sid)['data']
        self.assertFalse(data['restrictionsPendingAck'])
        self.assertEqual(1, len(data['restrictions']))
        r = data['restrictions'][0]
        self.assertEqual(['Allergy', 1, 'marisco', 'Severe'],
                         [r['kind'], r['guestPosition'], r['substance'], r['severity']])
        status, _ = h.request('/services/' + self.sid + '/commands/declare-restriction',
            dict(expectedVersion=h.dining(self.sid)['version'], guestPosition=1, kind='allergy',
                 substance='MARISCO', severity='mild'))
        self.assertEqual(409, status)  # duplicado, insensible a mayusculas

    def test_02_kitchen_sees_applicable_restrictions_per_preparation(self):
        act(self.sid, 'start'); act(self.sid, 'fire-next')
        cocina = h.ok('/services/' + self.sid, role='kitchen')['data']
        preps = cocina['courses'][0]['preparations']
        applicable = [p for p in preps if p.get('guestPosition') in (None, 1)]
        self.assertTrue(applicable)
        for p in applicable:
            self.assertTrue(any(r['substance'] == 'marisco' and r['severity'] == 'Severe'
                                for r in p['restrictions']))

    def test_03_change_after_firing_blocks_until_kitchen_acknowledges(self):
        act(self.sid, 'declare-restriction', role='service', kind='intolerance',
            substance='lactosa', severity='moderate')
        data = h.dining(self.sid)['data']
        self.assertTrue(data['restrictionsPendingAck'])
        self.assertIn('acknowledge-restrictions',
                      h.ok('/services/' + self.sid, role='kitchen')['data']['actions'])
        for p in h.dining(self.sid)['data']['courses'][0]['preparations']:
            act(self.sid, 'preparation-ready', role='kitchen', courseId='p1', itemId=p['id'])
        status, body = h.request('/services/' + self.sid + '/commands/ready',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1'), role='kitchen')
        self.assertEqual(409, status)
        self.assertIn(b'restrictions_unacknowledged', body)
        status, _ = h.request('/services/' + self.sid + '/commands/acknowledge-restrictions',
            dict(expectedVersion=h.dining(self.sid)['version']), role='service')
        self.assertEqual(403, status)  # sala no reconoce trabajo de cocina
        act(self.sid, 'acknowledge-restrictions', role='kitchen')
        act(self.sid, 'ready', role='kitchen', courseId='p1')

    def test_04_removal_requires_reason_and_new_acknowledgement(self):
        rid = h.dining(self.sid)['data']['restrictions'][0]['id']
        act(self.sid, 'remove-restriction', role='service', restrictionId=rid,
            reason='el cliente lo descarta')
        data = h.dining(self.sid)['data']
        self.assertTrue(data['restrictionsPendingAck'])
        self.assertEqual(1, len(data['restrictions']))
        act(self.sid, 'acknowledge-restrictions', role='kitchen')
        self.assertFalse(h.dining(self.sid)['data']['restrictionsPendingAck'])

    def test_05_events_flow_to_outbox_for_realtime(self):
        rows = h.sql("SELECT count(*) FROM native_d1.outbox WHERE type LIKE 'restriction.%'")
        self.assertGreaterEqual(int(rows), 4)

if __name__ == '__main__': unittest.main()
