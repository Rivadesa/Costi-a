"""D3.2/D3.5: restricciones por comensal y protocolo de revision por elaboracion sobre HTTP real."""
import unittest
import native_http as h

def act(sid, action, role='main', **fields):
    return h.ok('/services/' + sid + '/commands/' + action,
                dict(expectedVersion=h.dining(sid)['version'], **fields), role=role)

def preps(sid, role='main'):
    return h.ok('/services/' + sid, role=role)['data']['courses'][0]['preparations']

class RestrictionChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Ambito propio: mesas limpias y determinismo, sin depender del estado
        # que dejaron las demas suites en la base compartida del job.
        h.ENV['COSTINA_TENANT'] = 'd3-restrictions'
        h.NativeHttp.setUpClass()
        cls.sid = h.open_table('M1')
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
        applicable = [p for p in preps(self.sid, 'kitchen') if p.get('guestPosition') in (None, 1)]
        self.assertTrue(applicable)
        for p in applicable:
            self.assertTrue(any(r['substance'] == 'marisco' and r['severity'] == 'Severe'
                                for r in p['restrictions']))
            self.assertFalse(p['reviewPending'])  # declarado antes de enviar: nada que revisar

    def test_03_change_after_firing_blocks_until_each_affected_preparation_is_reviewed(self):
        # Intolerancia de mesa entera: afecta a TODAS las elaboraciones enviadas.
        act(self.sid, 'declare-restriction', role='service', kind='intolerance',
            substance='lactosa', severity='moderate')
        data = h.dining(self.sid)['data']
        self.assertTrue(data['restrictionsPendingAck'])
        self.assertNotIn('acknowledge-restrictions', data['actions'])
        pending = preps(self.sid, 'kitchen')
        self.assertTrue(all(p['reviewPending'] and 'review-preparation' in p['actions'] for p in pending))
        for p in pending:
            act(self.sid, 'preparation-ready', role='kitchen', courseId='p1', itemId=p['id'])
        status, body = h.request('/services/' + self.sid + '/commands/ready',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1'), role='kitchen')
        self.assertEqual(409, status)
        self.assertIn(b'restrictions_unreviewed', body)
        status, _ = h.request('/services/' + self.sid + '/commands/review-preparation',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1', itemId=pending[0]['id'],
                 decision='unaffected', note='sin lacteos'), role='service')
        self.assertEqual(403, status)  # sala no decide por cocina
        status, _ = h.request('/services/' + self.sid + '/commands/review-preparation',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1', itemId=pending[0]['id'],
                 decision='unaffected', note=''), role='kitchen')
        self.assertEqual(422, status)  # la nota es obligatoria
        for i, p in enumerate(pending):
            act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId=p['id'],
                decision='adapt' if i == 0 else 'unaffected', note='sin nata' if i == 0 else 'no lleva lacteos')
            still = h.dining(self.sid)['data']['restrictionsPendingAck']
            self.assertEqual(i < len(pending) - 1, still)  # cada elaboracion exige su decision
        reviewed = preps(self.sid)
        self.assertEqual('Adapt', reviewed[0]['review']['decision']); self.assertEqual('sin nata', reviewed[0]['review']['note'])
        act(self.sid, 'ready', role='kitchen', courseId='p1')

    def test_04_change_on_ready_course_and_remake_withdraw_validation(self):
        act(self.sid, 'declare-restriction', role='service', guestPosition=2, kind='allergy',
            substance='frutos secos', severity='severe')
        course = h.dining(self.sid)['data']['courses'][0]   # rol main: 'serve' solo desaparece por la revision pendiente
        self.assertEqual('Ready', course['state']); self.assertNotIn('serve', course['actions'])
        affected = [p for p in course['preparations'] if p['reviewPending']]
        self.assertTrue(affected); self.assertTrue(all(p.get('guestPosition') in (None, 2) for p in affected))
        status, body = h.request('/services/' + self.sid + '/commands/serve',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1'))
        self.assertEqual(409, status); self.assertIn(b'restrictions_unreviewed', body)
        for p in affected:
            act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId=p['id'], decision='remake', note='sin frutos secos')
        course = h.ok('/services/' + self.sid, role='kitchen')['data']['courses'][0]
        self.assertEqual('Preparing', course['state']); self.assertIsNone(course['readyAt'])
        for p in course['preparations']:
            if p['id'] in {a['id'] for a in affected}:
                self.assertEqual('Fired', p['state']); self.assertEqual('Remake', p['review']['decision'])
                act(self.sid, 'preparation-ready', role='kitchen', courseId='p1', itemId=p['id'])
        act(self.sid, 'ready', role='kitchen', courseId='p1'); act(self.sid, 'serve', role='service', courseId='p1')

    def test_05_removal_requires_reason_and_new_review(self):
        act(self.sid, 'fire-next')
        rid = h.dining(self.sid)['data']['restrictions'][0]['id']
        act(self.sid, 'remove-restriction', role='service', restrictionId=rid,
            reason='el cliente lo descarta')
        data = h.dining(self.sid)['data']
        self.assertTrue(data['restrictionsPendingAck'])
        self.assertEqual(2, len(data['restrictions']))
        second = h.dining(self.sid)['data']['courses'][1]['preparations']   # el pase enviado ahora es p2
        self.assertTrue(any(p['reviewPending'] for p in second))
        for p in [p for p in second if p['reviewPending']]:
            act(self.sid, 'review-preparation', role='main', courseId='p2', itemId=p['id'], decision='unaffected', note='revisado')
        self.assertFalse(h.dining(self.sid)['data']['restrictionsPendingAck'])

    def test_06_events_flow_to_outbox_for_realtime(self):
        rows = h.sql("SELECT count(*) FROM native_d1.outbox WHERE tenant='d3-restrictions' AND type LIKE 'restriction.%'")
        self.assertGreaterEqual(int(rows), 8)
        reviewed = h.sql("SELECT count(*) FROM native_d1.outbox WHERE tenant='d3-restrictions' AND type='restriction.reviewed'")
        self.assertGreaterEqual(int(reviewed), 4)

if __name__ == '__main__': unittest.main()
