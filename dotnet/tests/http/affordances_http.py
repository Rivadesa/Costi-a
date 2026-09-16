"""D3.1: affordances calculadas por el dominio y filtradas por rol sobre HTTP real."""
import unittest
import native_http as h

KITCHEN = {'preparation-start', 'preparation-ready', 'ready'}

def free_table():
    occupied = {b['service']['tableId'] for b in h.ok('/board')}
    return next('M' + str(i) for i in range(1, 9) if 'M' + str(i) not in occupied)

def collect(view):
    actions = set(view.get('actions') or [])
    for course in view['courses']:
        actions |= set(course.get('actions') or [])
        for prep in course['preparations']:
            actions |= set(prep.get('actions') or [])
    return actions

class AffordanceChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.NativeHttp.setUpClass()
        cls.sid = h.open_table(free_table())
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_open_state_actions_per_role(self):
        main = h.dining(self.sid)['data']
        self.assertIn('start', main['actions']); self.assertIn('cancel-unstarted', main['actions'])
        sala = h.ok('/services/' + self.sid, role='service')['data']
        self.assertIn('start', sala['actions']); self.assertNotIn('cancel-unstarted', sala['actions'])
        cocina = h.ok('/services/' + self.sid, role='kitchen')['data']
        self.assertEqual(set(), collect(cocina) - KITCHEN)

    def test_02_kitchen_flow_actions_are_consistent_with_middleware(self):
        h.mutate(self.sid, 'start'); h.mutate(self.sid, 'fire-next')
        cocina = h.ok('/services/' + self.sid, role='kitchen')['data']
        preps = cocina['courses'][0]['preparations']
        self.assertTrue(all('preparation-start' in p['actions'] for p in preps))
        self.assertEqual(set(), collect(cocina) - KITCHEN)
        for p in h.dining(self.sid)['data']['courses'][0]['preparations']:
            h.mutate(self.sid, 'preparation-ready', courseId='p1', itemId=p['id'])
        cocina = h.ok('/services/' + self.sid, role='kitchen')['data']
        self.assertIn('ready', cocina['courses'][0]['actions'])
        self.assertNotIn('serve', cocina['courses'][0]['actions'])
        h.mutate(self.sid, 'ready', courseId='p1')
        vista = h.ok('/services/' + self.sid, role='service')['data']
        self.assertIn('serve', vista['courses'][0]['actions'])
        self.assertNotIn('complete', vista['actions'])

    def test_03_release_affordance_only_for_main_after_completion(self):
        h.mutate(self.sid, 'serve', courseId='p1')
        for course in h.dining(self.sid)['data']['courses']:
            if course['state'] == 'Pending': h.mutate(self.sid, 'skip', courseId=course['id'], reason='affordance test')
        main = h.dining(self.sid)['data']
        self.assertIn('complete', main['actions'])
        self.assertNotIn('complete', h.ok('/services/' + self.sid, role='service')['data']['actions'])
        h.mutate(self.sid, 'complete')
        row = next(b for b in h.ok('/board') if b['service']['id'] == self.sid)
        self.assertIn('release', row['occupancy']['actions'])
        fila = next(b for b in h.ok('/board', role='service') if b['service']['id'] == self.sid)
        self.assertEqual([], fila['occupancy']['actions'])

    def test_04_account_actions_follow_balance(self):
        data = h.account(self.sid)['data']
        self.assertIn('payment', data['actions']); self.assertNotIn('close', data['actions'])
        self.assertEqual(len(data['charges']), len(data['voidableChargeIds']))
        h.charge(self.sid, 'payment', paymentId='aff-pay', method='card', amountCents=data['balanceCents'])
        data = h.account(self.sid)['data']
        self.assertIn('close', data['actions'])

    def test_05_command_responses_stay_action_free(self):
        # Libera la mesa del servicio completado en test_03 usando su propia affordance
        # (no depende de que queden mesas libres en la base compartida del job).
        row = next(b for b in h.ok('/board') if b['service']['id'] == self.sid)
        self.assertIn('release', row['occupancy']['actions'])
        h.ok('/occupancy/' + self.sid + '/release',
             {'expectedVersion': row['occupancyVersion'], 'reason': 'affordance test'})
        sid = h.open_table(row['service']['tableId'])
        response = h.mutate(sid, 'start')
        self.assertIsNone(response['data'].get('actions'))
        self.assertTrue(all(c.get('actions') is None for c in response['data']['courses']))

if __name__ == '__main__': unittest.main()
