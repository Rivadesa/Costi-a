"""D4.3: matriz rol x estacion y billete efimero del hub sobre HTTP real. Ambito propio."""
import json
import secrets
import unittest
import urllib.error
import urllib.request
import native_http as h

def request(path, data=None, token=None, query=''):
    body = json.dumps(data).encode() if data is not None else None
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    if body is not None: headers['Idempotency-Key'] = secrets.token_hex(16)
    req = urllib.request.Request(h.BASE + '/api/native/v1' + path + query, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e:
        try: return e.code, json.loads(e.read())
        except ValueError: return e.code, {}

def pair_device(admin, name, role, station):
    _, issued = request('/auth/pairings', {}, token=admin)
    _, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': name})
    request(f"/auth/pairings/{claimed['pairingId']}/approve", {'role': role, 'station': station}, token=admin)
    _, creds = request(f"/auth/pairings/{claimed['pairingId']}/collect", {'pollSecret': claimed['pollSecret']})
    return creds['deviceToken']

class StationChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.ENV['COSTINA_TENANT'] = 'd4-station'
        h.NativeHttp.setUpClass()
        admin = h.KEYS['main']
        cls.cold = pair_device(admin, 'estacion-fria', 'kitchen', 'cold')
        cls.pase = pair_device(admin, 'pase-cocina', 'kitchen', 'pase')
        cls.sid = h.open_table('M1')
        h.mutate(cls.sid, 'start'); h.mutate(cls.sid, 'fire-next')

    @classmethod
    def tearDownClass(cls): h.stop_server()

    def prep_ids(self):
        preps = h.dining(self.sid)['data']['courses'][0]['preparations']
        return ([p['id'] for p in preps if p['stationId'] == 'cold'],
                [p['id'] for p in preps if p['stationId'] == 'hot'])

    def act(self, token, action, **fields):
        return request('/services/' + self.sid + '/commands/' + action,
                       dict(expectedVersion=h.dining(self.sid)['version'], **fields), token=token)

    def test_01_device_marks_only_its_own_station(self):
        cold_ids, hot_ids = self.prep_ids()
        status, body = self.act(self.cold, 'preparation-ready', courseId='p1', itemId=hot_ids[0])
        self.assertEqual(409, status); self.assertEqual('wrong_station', body['error'])
        self.assertEqual(200, self.act(self.cold, 'preparation-ready', courseId='p1', itemId=cold_ids[0])[0])

    def test_02_affordances_mirror_the_station(self):
        _, view = request('/services/' + self.sid, token=self.cold)
        for p in view['data']['courses'][0]['preparations']:
            if p['stationId'] == 'cold' and p['state'] in ('Fired', 'Preparing'):
                self.assertIn('preparation-ready', p['actions'])
            if p['stationId'] != 'cold':
                self.assertEqual([], [a for a in p['actions'] if a.startswith('preparation-')])

    def test_03_course_validation_belongs_to_the_pass_station(self):
        cold_ids, hot_ids = self.prep_ids()
        for pid in cold_ids + hot_ids:
            token = self.cold if pid in cold_ids else h.KEYS['kitchen']
            self.act(token, 'preparation-ready', courseId='p1', itemId=pid)
        status, body = self.act(self.cold, 'ready', courseId='p1')
        self.assertEqual(409, status); self.assertEqual('wrong_station', body['error'])
        self.assertNotIn('ready', request('/services/' + self.sid, token=self.cold)[1]['data']['courses'][0]['actions'])
        self.assertIn('ready', request('/services/' + self.sid, token=self.pase)[1]['data']['courses'][0]['actions'])
        self.assertEqual(200, self.act(self.pase, 'ready', courseId='p1')[0])

    def test_05_restriction_review_is_announced_to_the_pass_station_only(self):
        # D6.4: la accion vive en la ELABORACION; el espejo debe filtrarla por estacion igual que el comando.
        sid = h.open_table('M2')
        h.mutate(sid, 'start'); h.mutate(sid, 'fire-next')
        h.mutate(sid, 'declare-restriction', guestPosition=1, kind='Allergy', substance='marisco', severity='Severe')
        def announced(token):
            view = request('/services/' + sid, token=token)[1]['data']
            return [p['id'] for p in view['courses'][0]['preparations'] if 'review-preparation' in p['actions']]
        pending = announced(self.pase)
        self.assertTrue(pending)
        self.assertEqual([], announced(self.cold))
        self.assertEqual(pending, announced(h.KEYS['kitchen']))   # el chef con sesion, sin estacion, no tiene restriccion
        _, board = request('/board', token=self.cold)
        self.assertEqual([], [a for row in board for c in row['service']['courses'] for p in c['preparations'] for a in p['actions'] if a == 'review-preparation'])
        fields = dict(courseId='p1', itemId=pending[0], decision='adapt', note='sin marisco')
        status, body = request('/services/' + sid + '/commands/review-preparation', dict(expectedVersion=h.dining(sid)['version'], **fields), token=self.cold)
        self.assertEqual(409, status); self.assertEqual('wrong_station', body['error'])
        status, _ = request('/services/' + sid + '/commands/review-preparation', dict(expectedVersion=h.dining(sid)['version'], **fields), token=self.pase)
        self.assertEqual(200, status)

    def test_06_a_rejected_retry_does_not_prove_the_first_attempt_was_not_applied(self):
        # D6.6 (revision externa): el permiso se comprueba ANTES de mirar si la clave ya se ejecuto. Un puesto de sala aplica
        # una orden, pierde la respuesta y se re-empareja como cocina con el MISMO nombre (mismo actor): el reintento identico
        # recibe 403 con el eco de la clave... de una orden que SI se aplico. Por eso los clientes preguntan por la clave
        # antes de cerrar un rechazo como "no aplicada".
        def raw(path, data, token, key):
            req = urllib.request.Request(h.BASE + '/api/native/v1' + path, data=json.dumps(data).encode(),
                headers={'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token, 'Idempotency-Key': key})
            try:
                with urllib.request.urlopen(req, timeout=15) as r: return r.status, r.headers.get('Idempotency-Key'), json.loads(r.read())
            except urllib.error.HTTPError as e: return e.code, e.headers.get('Idempotency-Key'), json.loads(e.read() or b'{}')
        admin = h.KEYS['main']
        sid = h.open_table('M3'); h.mutate(sid, 'start')
        waiter = pair_device(admin, 'puesto-cambia-de-rol', 'service', 'sala-1')
        key, body = secrets.token_hex(16), dict(expectedVersion=h.dining(sid)['version'], reason='salen un momento')
        status, echoed, _ = raw('/services/' + sid + '/commands/pause', body, waiter, key)
        self.assertEqual((200, key), (status, echoed))
        version = h.dining(sid)['version']
        device = next(d for d in request('/auth/devices', token=admin)[1] if d['name'] == 'puesto-cambia-de-rol' and not d.get('revokedAt'))
        self.assertEqual(200, request('/auth/devices/' + device['id'] + '/revoke', {}, token=admin)[0])
        self.assertEqual(401, raw('/services/' + sid + '/commands/pause', body, waiter, key)[0])          # revocado: 401, que ningun cliente toma por cierre
        cook = pair_device(admin, 'puesto-cambia-de-rol', 'kitchen', 'cold')
        status, echoed, rejection = raw('/services/' + sid + '/commands/pause', body, cook, key)       # MISMOS bytes y clave
        self.assertEqual((403, key), (status, echoed)); self.assertIn('error', rejection)
        self.assertEqual(version, h.dining(sid)['version'])                                              # ni duplicada ni deshecha
        status, lookup = request('/commands/' + key, token=cook)                                         # mismo actor: el registro manda
        self.assertEqual(200, status); self.assertTrue(lookup['found']); self.assertEqual(key, lookup['key'])

    def test_04_session_users_have_no_station_restriction(self):
        # El chef con sesion (sin estacion) marco arriba una elaboracion 'hot' sin restriccion;
        # el dispositivo de una estacion nunca gana permisos de rol por tener estacion.
        self.assertEqual(403, request('/checkout/accounts', token=self.cold)[0])

    def test_05_hub_ticket_is_single_use_and_only_for_the_hub(self):
        status, issued = request('/auth/hub-token', {}, token=self.cold)
        self.assertEqual(200, status)
        ticket = issued['hubToken']
        status, first = request('/events/negotiate', {}, query='?access_token=' + ticket)
        self.assertEqual(200, status); self.assertIn('connectionId', first)
        self.assertEqual(401, request('/events/negotiate', {}, query='?access_token=' + ticket)[0])
        self.assertEqual(401, request('/events/negotiate', {}, query='?access_token=inventado')[0])
        self.assertEqual(401, request('/session', None, query='?access_token=' + ticket)[0])
        self.assertEqual(401, request('/auth/hub-token', {})[0])
        # D5.2: la query con el billete nunca llega al log (lineas de peticion de ASP.NET filtradas).
        log = open('d1-http-server.log', encoding='utf8', errors='replace').read()
        self.assertNotIn(ticket, log); self.assertNotIn('access_token', log)

if __name__ == '__main__': unittest.main()
