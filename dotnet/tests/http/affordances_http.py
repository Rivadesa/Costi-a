"""D3.1: affordances calculadas por el dominio y filtradas por rol sobre HTTP real.
E4e: las ordenes de grupo se anuncian tras el filtro de rol y estacion, y la ruta las permite como a sus individuales."""
import secrets
import unittest
import native_http as h

# E4e: con algo enviado, cocina ve tambien las ordenes de grupo de lo que puede marcar.
KITCHEN = {'preparation-start', 'preparation-ready', 'ready', 'preparations-start', 'preparations-ready'}

def free_table():
    occupied = {b['service']['tableId'] for b in h.ok('/dining/board')}
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
        sala = h.ok('/dining/services/' + self.sid, role='service')['data']
        self.assertIn('start', sala['actions']); self.assertNotIn('cancel-unstarted', sala['actions'])
        cocina = h.ok('/dining/services/' + self.sid, role='kitchen')['data']
        self.assertEqual(set(), collect(cocina) - KITCHEN)

    def test_02_kitchen_flow_actions_are_consistent_with_middleware(self):
        h.mutate(self.sid, 'start'); h.mutate(self.sid, 'fire-next')
        cocina = h.ok('/dining/services/' + self.sid, role='kitchen')['data']
        preps = cocina['courses'][0]['preparations']
        self.assertTrue(all('preparation-start' in p['actions'] for p in preps))
        self.assertEqual(set(), collect(cocina) - KITCHEN)
        for p in h.dining(self.sid)['data']['courses'][0]['preparations']:
            h.mutate(self.sid, 'preparation-ready', courseId='p1', itemId=p['id'])
        cocina = h.ok('/dining/services/' + self.sid, role='kitchen')['data']
        self.assertIn('ready', cocina['courses'][0]['actions'])
        self.assertNotIn('serve', cocina['courses'][0]['actions'])
        h.mutate(self.sid, 'ready', courseId='p1')
        vista = h.ok('/dining/services/' + self.sid, role='service')['data']
        self.assertIn('serve', vista['courses'][0]['actions'])
        self.assertNotIn('complete', vista['actions'])

    def test_03_release_affordance_only_for_main_after_completion(self):
        h.mutate(self.sid, 'serve', courseId='p1')
        for course in h.dining(self.sid)['data']['courses']:
            if course['state'] == 'Pending': h.mutate(self.sid, 'skip', courseId=course['id'], reason='affordance test')
        main = h.dining(self.sid)['data']
        self.assertIn('complete', main['actions'])
        self.assertNotIn('complete', h.ok('/dining/services/' + self.sid, role='service')['data']['actions'])
        h.mutate(self.sid, 'complete')
        row = next(b for b in h.ok('/dining/board') if b['service']['id'] == self.sid)
        self.assertIn('release', row['occupancy']['actions'])
        fila = next(b for b in h.ok('/dining/board', role='service') if b['service']['id'] == self.sid)
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
        row = next(b for b in h.ok('/dining/board') if b['service']['id'] == self.sid)
        self.assertIn('release', row['occupancy']['actions'])
        h.ok('/dining/occupancy/' + self.sid + '/release',
             {'expectedVersion': row['occupancyVersion'], 'reason': 'affordance test'})
        sid = type(self).reopened = h.open_table(row['service']['tableId'])
        response = h.mutate(sid, 'start')
        self.assertIsNone(response['data'].get('actions'))
        self.assertTrue(all(c.get('actions') is None for c in response['data']['courses']))

    def test_06_round_is_a_session_action_of_main_and_service_only(self):
        # C3b: la ronda (add-consumptions) se anuncia como accion de SESION a principal y sala, y la politica de la ruta, que es la
        # misma funcion, la niega a cocina con 403 antes de mirar nada. Nunca aparece en las affordances de una mesa.
        for role, expected in (('main', True), ('service', True), ('kitchen', False)):
            self.assertEqual(expected, 'add-consumptions' in h.ok('/session', role=role)['actions'], role)
        # La mesa reabierta en test_05 (en el ambito d1-tenant, compartido por varias suites, no quedan mesas libres).
        sid = self.reopened
        body = {'expectedVersion': h.dining(sid)['version'], 'lines': [{'productId': 'water', 'quantity': 1}]}
        self.assertEqual(403, h.request('/dining/services/' + sid + '/commands/add-consumptions', body, role='kitchen')[0])
        self.assertEqual(200, h.request('/dining/services/' + sid + '/commands/add-consumptions', body, role='service')[0])
        for role in ('main', 'service', 'kitchen'):
            self.assertNotIn('add-consumptions', collect(h.ok('/dining/services/' + sid, role=role)['data']), role)

    def test_07_group_commands_are_announced_after_the_role_and_station_filter(self):
        # E4e: cada orden de grupo se anuncia EN EL PASE si alguna de sus elaboraciones anuncia la individual DESPUES de filtrar por rol
        # y estacion: hereda la matriz. La sala nunca ve review-preparations; un comandero de sala no ve preparations-* en un pase de
        # cocina; la estacion de lo suyo y el pase (de cualquier estacion), si. Dispositivos reales, emparejados en esta ejecucion.
        run = secrets.token_hex(3)
        for name, role, station in (('comandero-' + run, 'service', 'sala-1'), ('fria-' + run, 'kitchen', 'cold'), ('pase-' + run, 'kitchen', 'pase')):
            pair_device(name, role, station)
        viewers = ('main', 'service', 'kitchen', 'comandero-' + run, 'fria-' + run, 'pase-' + run)
        sid = self.reopened   # empezada en test_05, sin nada enviado
        h.mutate(sid, 'declare-restriction', guestPosition=1, kind='Allergy', substance='gluten', severity='Severe')
        def first(viewer): return h.ok('/dining/services/' + sid, role=viewer)['data']['courses'][0]
        def coherent(course, viewer):
            for individual, group in GROUPS:
                announced = any(individual in (p.get('actions') or []) for p in course['preparations'])
                self.assertEqual(announced, group in course['actions'], (viewer, group))
        reviewers = {v for v in viewers if 'review-preparations' in first(v)['actions']}
        self.assertEqual({'main', 'kitchen', 'pase-' + run}, reviewers)
        for viewer in viewers: coherent(first(viewer), viewer)
        items = [dict(courseId=c['id'], itemId=p['id'], decision='unaffected', note='no lleva gluten')
                 for c in h.dining(sid)['data']['courses'] for p in c['preparations'] if p['reviewPending']]
        h.ok('/dining/services/' + sid + '/commands/review-preparations', dict(expectedVersion=h.dining(sid)['version'], items=items), role='pase-' + run)
        h.mutate(sid, 'fire-next')
        markers = {v for v in viewers if {'preparations-start', 'preparations-ready'} <= set(first(v)['actions'])}
        self.assertEqual({'main', 'service', 'kitchen', 'fria-' + run, 'pase-' + run}, markers)   # la sala con sesion conserva los suyos
        room = first('comandero-' + run)
        self.assertEqual([], [a for p in room['preparations'] for a in p['actions'] if a.startswith('preparation')])
        for viewer in viewers: coherent(first(viewer), viewer)

    def test_08_each_group_command_is_allowed_exactly_like_its_individual(self):
        # E4e: Allows es la politica de la ruta y la del filtro: por rol, la orden de grupo vale lo mismo que su individual. Una version
        # vieja da 409 donde el rol puede y 403 donde no, sin tocar nada.
        sid = self.reopened
        for role in ('main', 'service', 'kitchen'):
            for individual, group in GROUPS:
                answers = [h.request('/dining/services/' + sid + '/commands/' + action, {'expectedVersion': 1}, role=role)[0] for action in (individual, group)]
                self.assertEqual(403 if role == 'service' and individual == 'review-preparation' else 409, answers[0], (role, individual))
                self.assertEqual(answers[0], answers[1], (role, group))

GROUPS = (('preparation-start', 'preparations-start'), ('preparation-ready', 'preparations-ready'), ('review-preparation', 'review-preparations'))

def pair_device(name, role, station):
    issued = h.ok('/auth/pairings', {})
    claimed = h.ok('/auth/pairings/claim', {'code': issued['code'], 'deviceName': name}, role='anonimo')
    h.ok('/auth/pairings/' + claimed['pairingId'] + '/approve', {'role': role, 'station': station})
    h.KEYS[name] = h.ok('/auth/pairings/' + claimed['pairingId'] + '/collect', {'pollSecret': claimed['pollSecret']}, role='anonimo')['deviceToken']

if __name__ == '__main__': unittest.main()
