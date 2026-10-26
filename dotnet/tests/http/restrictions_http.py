"""D3.2/D3.5: restricciones por comensal y protocolo de revision por elaboracion sobre HTTP real.
E4e (ADR-016): cocina decide cada plato afectado ANTES de que salga su pase (tambien con la mesa Sentados), el bloqueo es por pase,
fire-next sale desde Sentados y la mesa se abre con sus alergias en la misma orden."""
import json
import re
import unittest
import native_http as h

MONEY = re.compile(r'(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)', re.I)

def act(sid, action, role='main', **fields):
    return h.ok('/dining/services/' + sid + '/commands/' + action,
                dict(expectedVersion=h.dining(sid)['version'], **fields), role=role)

def attempt(sid, action, role='main', **fields):
    status, body = h.request('/dining/services/' + sid + '/commands/' + action,
                             dict(expectedVersion=h.dining(sid)['version'], **fields), role=role)
    return status, json.loads(body or b'{}')

def preps(sid, role='main', course=0):
    return h.ok('/dining/services/' + sid, role=role)['data']['courses'][course]['preparations']

def money_keys(value):
    if isinstance(value, dict):
        for k, v in value.items():
            if MONEY.search(k): yield k
            yield from money_keys(v)
    elif isinstance(value, list):
        for v in value: yield from money_keys(v)

def open_with(table, **extra):
    return h.request('/dining/services', dict(tableId=table, pax=2, offerId='LAB-TASTING', **extra))

def services_on(table):
    return [r['service']['id'] for r in h.ok('/dining/board') if r['service']['tableId'] == table]

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
        # E4e: con la mesa Sentados, todo el menu del comensal 1 (tambien lo no enviado) espera la decision de cocina.
        self.assertTrue(data['restrictionsPendingAck'])
        self.assertEqual(['frio-1', 'caliente-1', 'principal-1'],
                         [p['id'] for c in data['courses'] for p in c['preparations'] if p['reviewPending']])
        self.assertEqual(1, len(data['restrictions']))
        r = data['restrictions'][0]
        self.assertEqual(['Allergy', 1, 'marisco', 'Severe'],
                         [r['kind'], r['guestPosition'], r['substance'], r['severity']])
        status, _ = h.request('/dining/services/' + self.sid + '/commands/declare-restriction',
            dict(expectedVersion=h.dining(self.sid)['version'], guestPosition=1, kind='allergy',
                 substance='MARISCO', severity='mild'))
        self.assertEqual(409, status)  # duplicado, insensible a mayusculas

    def test_02_kitchen_reviews_before_sending_and_fire_next_starts_from_seated(self):
        # E4e: sin la decision de cocina el primer pase no sale, y el servidor no lo anuncia.
        self.assertNotIn('fire-next', h.ok('/dining/services/' + self.sid, role='service')['data']['actions'])
        status, body = attempt(self.sid, 'fire-next', role='service')
        self.assertEqual((409, 'restrictions_unreviewed'), (status, body['error']))
        # Cocina revisa con la mesa Sentados, en un pase sin enviar; sala no decide por cocina.
        self.assertEqual(403, attempt(self.sid, 'review-preparation', role='service', courseId='p1', itemId='frio-1',
                                      decision='unaffected', note='x')[0])
        act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId='frio-1', decision='adapt', note='vieira en vez de ostra')
        act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId='caliente-1', decision='unaffected', note='no lleva marisco')
        data = h.ok('/dining/services/' + self.sid, role='service')['data']
        self.assertEqual('Open', data['state'])
        self.assertIn('fire-next', data['actions'])   # sale el revisado aunque el segundo pase siga esperando
        act(self.sid, 'fire-next', role='service')     # desde Sentados: empieza el servicio y envia el primer pase en una orden
        data = h.dining(self.sid)['data']
        self.assertEqual(('InService', 'Fired'), (data['state'], data['courses'][0]['state']))
        self.assertEqual('2|1', h.sql("SELECT count(*)||'|'||count(DISTINCT command_key) FROM core.audit WHERE tenant='d3-restrictions' "
                                      f"AND aggregate_id='{self.sid}' AND action IN ('service.started','course.fired')"))   # UNA orden, dos eventos
        self.assertTrue(data['courses'][1]['preparations'][0]['reviewPending'])   # principal-1, sin enviar, sigue esperando
        frio = next(p for p in preps(self.sid, 'kitchen') if p['id'] == 'frio-1')
        self.assertEqual(('Adapt', 'vieira en vez de ostra', 'Preparación fría'), (frio['review']['decision'], frio['review']['note'], frio['name']))
        applicable = [p for p in preps(self.sid, 'kitchen') if p.get('guestPosition') in (None, 1)]
        self.assertTrue(applicable)
        for p in applicable:
            self.assertTrue(any(r['substance'] == 'marisco' and r['severity'] == 'Severe'
                                for r in p['restrictions']))
            self.assertFalse(p['reviewPending'])  # revisado antes de enviar

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
        status, body = h.request('/dining/services/' + self.sid + '/commands/ready',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1'), role='kitchen')
        self.assertEqual(409, status)
        self.assertIn(b'restrictions_unreviewed', body)
        status, _ = h.request('/dining/services/' + self.sid + '/commands/review-preparation',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1', itemId=pending[0]['id'],
                 decision='unaffected', note='sin lacteos'), role='service')
        self.assertEqual(403, status)  # sala no decide por cocina
        status, _ = h.request('/dining/services/' + self.sid + '/commands/review-preparation',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1', itemId=pending[0]['id'],
                 decision='unaffected', note=''), role='kitchen')
        self.assertEqual(422, status)  # la nota es obligatoria
        for i, p in enumerate(pending):
            act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId=p['id'],
                decision='adapt' if i == 0 else 'unaffected', note='sin nata' if i == 0 else 'no lleva lacteos')
            still = any(q['reviewPending'] for q in preps(self.sid))
            self.assertEqual(i < len(pending) - 1, still)  # cada elaboracion exige su decision
        self.assertTrue(h.dining(self.sid)['data']['restrictionsPendingAck'])   # E4e: el segundo pase, sin enviar, tambien espera
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
        status, body = h.request('/dining/services/' + self.sid + '/commands/serve',
            dict(expectedVersion=h.dining(self.sid)['version'], courseId='p1'))
        self.assertEqual(409, status); self.assertIn(b'restrictions_unreviewed', body)
        for p in affected:
            act(self.sid, 'review-preparation', role='kitchen', courseId='p1', itemId=p['id'], decision='remake', note='sin frutos secos')
        course = h.ok('/dining/services/' + self.sid, role='kitchen')['data']['courses'][0]
        self.assertEqual('Preparing', course['state']); self.assertIsNone(course['readyAt'])
        for p in course['preparations']:
            if p['id'] in {a['id'] for a in affected}:
                self.assertEqual('Fired', p['state']); self.assertEqual('Remake', p['review']['decision'])
                act(self.sid, 'preparation-ready', role='kitchen', courseId='p1', itemId=p['id'])
        act(self.sid, 'ready', role='kitchen', courseId='p1'); act(self.sid, 'serve', role='service', courseId='p1')

    def test_05_removal_requires_reason_and_new_review(self):
        # E4e: el bloqueo es del pase que va a salir: p2 recibio las restricciones de test_03 y test_04 sin haberse enviado.
        status, body = attempt(self.sid, 'fire-next', role='service')
        self.assertEqual((409, 'restrictions_unreviewed'), (status, body['error']))
        waiting = [p for p in preps(self.sid, course=1) if p['reviewPending']]
        self.assertEqual(['principal-1', 'principal-2'], [p['id'] for p in waiting])
        status, body = attempt(self.sid, 'review-preparation', role='kitchen', courseId='p2', itemId='principal-2',
                               decision='remake', note='otra merluza')
        self.assertEqual((409, 'nothing_to_remake'), (status, body['error']))   # rehacer es solo para lo enviado
        for p in waiting:
            act(self.sid, 'review-preparation', role='kitchen', courseId='p2', itemId=p['id'], decision='adapt', note='sin lacteos ni frutos secos')
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
        rows = h.sql("SELECT count(*) FROM core.outbox WHERE tenant='d3-restrictions' AND type LIKE 'restriction.%'")
        self.assertGreaterEqual(int(rows), 8)
        reviewed = h.sql("SELECT count(*) FROM core.outbox WHERE tenant='d3-restrictions' AND type='restriction.reviewed'")
        self.assertGreaterEqual(int(reviewed), 4)

    def test_07_a_table_opens_with_its_restrictions_in_one_command(self):
        # E4e: abrir con alergias es UNA orden (una sola orden incierta) y una mala no abre nada.
        for bad, code in ((dict(kind='banana', substance='x', severity='severe'), 'invalid_restriction'),
                          (dict(kind='allergy', substance='x', severity='muchisima'), 'invalid_restriction'),
                          (dict(substance='x', severity='severe'), 'invalid_restriction'),
                          (dict(guestPosition=5, kind='allergy', substance='x', severity='severe'), 'invalid_guest')):
            status, body = open_with('M2', restrictions=[bad])
            self.assertEqual((409, code), (status, json.loads(body)['error']), bad)
        status, body = open_with('M2', restrictions=[dict(guestPosition=1, kind='allergy', substance='gluten', severity='severe'),
                                                     dict(guestPosition=1, kind='Allergy', substance='Gluten', severity='mild')])
        self.assertEqual((409, 'duplicate_restriction'), (status, json.loads(body)['error']))
        self.assertEqual(422, open_with('M2', restrictions=[None])[0])
        self.assertEqual(422, open_with('M2', restrictions=[dict(guestPosition=1, kind='allergy', substance=' ', severity='severe')])[0])
        self.assertEqual([], services_on('M2'))                                     # todo o nada: ninguna mesa a medias
        status, body = open_with('M2', restrictions=[dict(guestPosition=1, kind='allergy', substance='gluten', severity='severe'),
                                                     dict(kind='preference', substance='sin cilantro', severity='mild')])
        self.assertEqual(200, status, body)
        opened = json.loads(body)
        self.assertEqual([], list(money_keys(opened))); self.assertEqual(1, opened['version'])
        data = h.dining(opened['serviceId'])['data']
        self.assertEqual([('Allergy', 1, 'gluten', 'Severe'), ('Preference', None, 'sin cilantro', 'Mild')],
                         [(r['kind'], r['guestPosition'], r['substance'], r['severity']) for r in data['restrictions']])
        # Una preferencia de toda la mesa afecta a todo el menu: todo nace por revisar.
        self.assertTrue(all(p['reviewPending'] for c in data['courses'] for p in c['preparations']))
        self.assertEqual('2|1', h.sql("SELECT count(*) FILTER (WHERE action='restriction.declared')||'|'||count(DISTINCT command_key) "
                                      f"FROM core.audit WHERE tenant='d3-restrictions' AND aggregate_id='{opened['serviceId']}'"))
        type(self).opened = opened['serviceId']

    def test_08_the_pass_reviews_a_seated_table_course_by_course_and_skipping_withdraws(self):
        sid = self.opened
        self.assertEqual(403, attempt(sid, 'review-preparation', role='service', courseId='p1', itemId='frio-1', decision='unaffected', note='x')[0])
        for p in preps(sid, 'kitchen'):
            self.assertIn('review-preparation', p['actions'])                     # anunciado con la mesa Sentados, en un pase sin enviar
            act(sid, 'review-preparation', role='kitchen', courseId='p1', itemId=p['id'], decision='adapt' if p['guestPosition'] == 1 else 'unaffected',
                note='pan de maiz' if p['guestPosition'] == 1 else 'no lleva cilantro')
        act(sid, 'fire-next', role='service')
        self.assertEqual(('InService', 'Fired', 'Pending'), (h.dining(sid)['data']['state'], *[c['state'] for c in h.dining(sid)['data']['courses']]))
        for p in preps(sid, 'kitchen'):
            act(sid, 'preparation-ready', role='kitchen', courseId='p1', itemId=p['id'])
        act(sid, 'ready', role='kitchen', courseId='p1'); act(sid, 'serve', role='service', courseId='p1')
        self.assertNotIn('fire-next', h.ok('/dining/services/' + sid, role='service')['data']['actions'])
        status, body = attempt(sid, 'fire-next', role='service')
        self.assertEqual((409, 'restrictions_unreviewed'), (status, body['error']))
        # Omitir el pase retira sus revisiones pendientes: lo que no se cocina no se revisa (y el evento lo cuenta).
        act(sid, 'skip', role='service', courseId='p2', reason='no quieren el segundo')
        data = h.dining(sid)['data']
        self.assertFalse(data['restrictionsPendingAck'])
        self.assertEqual('2', h.sql("SELECT payload->'data'->>'reviews_withdrawn' FROM core.audit WHERE tenant='d3-restrictions' "
                                    f"AND aggregate_id='{sid}' AND action='course.skipped'"))
        act(sid, 'complete')

if __name__ == '__main__': unittest.main()
