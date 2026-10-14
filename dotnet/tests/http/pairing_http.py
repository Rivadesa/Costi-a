"""D4.2: emparejamiento de dispositivos y revocacion sobre HTTP real. Ambito propio."""
import base64
import json
import os
import socket
import subprocess
import unittest
import urllib.error
import urllib.request
import native_http as h

PASSWORD = 'clave-de-ensayo-larga-1'
# B.2: puerto de la conexion sencilla en el laboratorio (alli solo existe si se pide, y solo en loopback).
LAN_PORT = os.environ.get('COSTINA_TEST_LAN_PORT', str(int(h.PORT) + 11))
LAN = 'http://127.0.0.1:' + LAN_PORT

def lan(path, data=None, token=None, key=None, base=None):
    """Peticion por el puerto de la conexion sencilla (o por base); devuelve (estado, tipo, cuerpo en texto)."""
    body = json.dumps(data).encode() if data is not None else None
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    if key: headers['Idempotency-Key'] = key
    req = urllib.request.Request((base or LAN) + path, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, r.headers.get('Content-Type', ''), r.read().decode()
    except urllib.error.HTTPError as e: return e.code, e.headers.get('Content-Type', ''), e.read().decode()

def request(path, data=None, token=None):
    body = json.dumps(data).encode() if data is not None else None
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(h.BASE + '/api/native/v1' + path, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e: return e.code, json.loads(e.read())

class PairingChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.ENV['COSTINA_TENANT'] = 'd4-pairing'
        h.ENV['COSTINA_LAN_HTTP_PORT'] = LAN_PORT
        h.NativeHttp.setUpClass()
        result = subprocess.run(['dotnet', str(h.SERVER), 'create-user', 'jefe', 'main'],
                                env=h.ENV, input=PASSWORD, text=True, capture_output=True)
        assert result.returncode == 0, result.stderr
        cls.admin = request('/auth/login', {'username': 'jefe', 'password': PASSWORD})[1]['token']
    @classmethod
    def tearDownClass(cls):
        h.sql('UPDATE core.installation SET lan_http=false')   # ajuste de la INSTALACION: la base la comparten las demas suites
        h.stop_server()
        h.ENV.pop('COSTINA_LAN_HTTP_PORT', None)

    def test_01_full_pairing_flow_delivers_secret_exactly_once(self):
        status, issued = request('/auth/pairings', {}, token=self.admin)
        self.assertEqual(200, status)
        status, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'tablet-sala'})
        self.assertEqual(200, status)
        pid, poll = claimed['pairingId'], claimed['pollSecret']
        self.assertIn('tablet-sala', [p['deviceName'] for p in request('/auth/pairings/pending', token=self.admin)[1]])
        self.assertEqual('pending', request(f'/auth/pairings/{pid}/collect', {'pollSecret': poll})[1]['status'])
        status, _ = request(f'/auth/pairings/{pid}/approve', {'role': 'service', 'station': 'sala-1'}, token=self.admin)
        self.assertEqual(200, status)
        status, creds = request(f'/auth/pairings/{pid}/collect', {'pollSecret': poll})
        self.assertEqual(200, status)
        self.assertEqual(['service', 'sala-1'], [creds['role'], creds['station']])
        self.assertTrue(creds['deviceToken'].startswith('dev.'))
        type(self).device = creds
        # Entrega unica: el segundo collect no vuelve a dar el secreto.
        self.assertEqual(404, request(f'/auth/pairings/{pid}/collect', {'pollSecret': poll})[0])

    def test_02_device_token_authenticates_with_role_station_and_actor(self):
        status, session = request('/session', token=self.device['deviceToken'])
        self.assertEqual(200, status)
        self.assertEqual(['service', 'device:tablet-sala', 'sala-1'],
                         [session['role'], session['actor'], session['station']])
        self.assertEqual(403, request('/checkout/accounts', token=self.device['deviceToken'])[0])

    def test_03_codes_are_single_use_short_lived_and_generic_on_failure(self):
        status, issued = request('/auth/pairings', {}, token=self.admin)
        code = issued['code']
        request('/auth/pairings/claim', {'code': code, 'deviceName': 'primera'})
        self.assertEqual(401, request('/auth/pairings/claim', {'code': code, 'deviceName': 'segunda'})[0])
        _, issued = request('/auth/pairings', {}, token=self.admin)
        h.sql("UPDATE core.pairings SET expires_at = now() - interval '1 second' "
              "WHERE tenant='d4-pairing' AND status='issued'")
        self.assertEqual(401, request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'tarde'})[0])
        self.assertEqual(401, request('/auth/pairings/claim', {'code': 'codigo-inventado', 'deviceName': 'x'})[0])

    def test_04_only_main_manages_pairings_and_devices(self):
        for path, data in [('/auth/pairings', {}), ('/auth/pairings/x/approve', {'role': 'main', 'station': 'p'}),
                           ('/auth/devices/x/revoke', {})]:
            self.assertEqual(403, request(path, data, token=self.device['deviceToken'])[0])
        self.assertEqual(403, request('/auth/pairings/pending', token=self.device['deviceToken'])[0])

    def test_05_denied_pairing_never_creates_a_device(self):
        _, issued = request('/auth/pairings', {}, token=self.admin)
        _, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'rechazada'})
        request(f"/auth/pairings/{claimed['pairingId']}/deny", {}, token=self.admin)
        status, out = request(f"/auth/pairings/{claimed['pairingId']}/collect", {'pollSecret': claimed['pollSecret']})
        self.assertEqual(403, status); self.assertEqual('denied', out['status'])
        self.assertEqual('0', h.sql("SELECT count(*) FROM core.devices WHERE tenant='d4-pairing' AND name='rechazada'"))

    def test_06_revocation_cuts_access_immediately(self):
        status, _ = request(f"/auth/devices/{self.device['deviceId']}/revoke", {}, token=self.admin)
        self.assertEqual(200, status)
        self.assertEqual(401, request('/session', token=self.device['deviceToken'])[0])
        rows = request('/auth/devices', token=self.admin)[1]
        self.assertTrue(any(d['id'] == self.device['deviceId'] and d['revokedAt'] for d in rows))
        self.assertEqual(404, request(f"/auth/devices/{self.device['deviceId']}/revoke", {}, token=self.admin)[0])

    def test_07_only_hashes_at_rest(self):
        secret = self.device['deviceToken'].split('.')[2]
        stored = h.sql("SELECT string_agg(secret_hash, ',') FROM core.devices WHERE tenant='d4-pairing'")
        self.assertNotIn(secret, stored)
        codes = h.sql("SELECT string_agg(code_hash, ',') || ',' || string_agg(coalesce(poll_secret_hash,''), ',') "
                      "FROM core.pairings WHERE tenant='d4-pairing'")
        self.assertTrue(all(len(x) in (0, 64) for x in codes.split(',')))

    def test_08_role_must_fit_the_station_kind_and_no_new_device_is_ever_main(self):
        # Inicio y dispositivos (A1/A2): lo decide el SERVIDOR, no la pantalla. Fixtures: sala-1 (room), cold (kitchen), pase (pass).
        def approve(role, station):
            _, issued = request('/auth/pairings', {}, token=self.admin)
            _, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': f'matriz-{role}-{station}'})
            status, body = request(f"/auth/pairings/{claimed['pairingId']}/approve", {'role': role, 'station': station}, token=self.admin)
            return status, body.get('error')
        self.assertEqual((409, 'device_role_not_allowed'), approve('main', 'sala-1'))
        for role, station in (('kitchen', 'cold'), ('kitchen', 'pase'), ('service', 'sala-1')):
            self.assertEqual((200, None), approve(role, station), (role, station))
        for role, station in (('kitchen', 'sala-1'), ('service', 'cold'), ('service', 'pase')):
            self.assertEqual((409, 'station_kind_mismatch'), approve(role, station), (role, station))
        self.assertEqual((422, 'invalid_request'), approve('service', 'inventada'))
        self.assertEqual('0', h.sql("SELECT count(*) FROM core.devices WHERE tenant='d4-pairing' AND role='main'"))

    def test_09_online_is_a_live_channel_and_a_device_unpairs_itself(self):
        # A3: sin canal de tiempo real abierto no esta 'conectado ahora'. A4: /session nombra la estacion. A5: Desvincular en la
        # tablet revoca en el servidor (auditado a su nombre) y una persona con sesion no puede usarlo.
        _, issued = request('/auth/pairings', {}, token=self.admin)
        _, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'tablet-unpair'})
        request(f"/auth/pairings/{claimed['pairingId']}/approve", {'role': 'service', 'station': 'sala-1'}, token=self.admin)
        _, creds = request(f"/auth/pairings/{claimed['pairingId']}/collect", {'pollSecret': claimed['pollSecret']})
        row = next(d for d in request('/auth/devices', token=self.admin)[1] if d['id'] == creds['deviceId'])
        self.assertIs(row['online'], False)
        session = request('/session', token=creds['deviceToken'])[1]
        self.assertEqual(['sala-1', 'Sala 1'], [session['station'], session['stationName']])
        self.assertEqual(403, request('/auth/unpair', {}, token=self.admin)[0])
        status, out = request('/auth/unpair', {}, token=creds['deviceToken'])
        self.assertEqual((200, True), (status, out['unpaired']))
        self.assertEqual(401, request('/session', token=creds['deviceToken'])[0])
        self.assertEqual('device:tablet-unpair', h.sql("SELECT revoked_by FROM core.devices WHERE tenant='d4-pairing' AND name='tablet-unpair'"))
        self.assertEqual(401, request('/auth/unpair', {}, token=creds['deviceToken'])[0])

    # ---- B.2 (ADR-014): conexion sencilla por la red local ----
    def test_10_simple_connection_is_off_by_default_and_its_port_only_explains_itself(self):
        h.sql('UPDATE core.installation SET lan_http=false')
        request('/installation/network', token=self.admin)   # la lectura sincroniza el estado con la base
        for path, data in (('/app/', None), ('/health', None), ('/api/native/v1/session', None), ('/api/native/v1/auth/pairings/claim', {})):
            status, kind, text = lan(path, data)
            self.assertEqual((503, True), (status, kind.startswith('text/html')), path)
            self.assertIn('Conexión sencilla desactivada', text)
        status, network = request('/installation/network', token=self.admin)
        self.assertEqual(200, status)
        self.assertEqual({'enabled': False, 'port': int(LAN_PORT), 'listening': True}, network['lanHttp'])
        self.assertIsInstance(network['addresses'], list); self.assertIsInstance(network['networks'], list)
        self.assertEqual((False, True), (request('/diagnostics', token=self.admin)[1]['lanHttp']['enabled'], network['lanHttp']['listening']))

    def test_11_only_the_responsible_switches_it_and_it_is_audited(self):
        _, issued = request('/auth/pairings', {}, token=self.admin)
        _, claimed = request('/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'movil-sencillo'})
        request(f"/auth/pairings/{claimed['pairingId']}/approve", {'role': 'service', 'station': 'sala-1'}, token=self.admin)
        type(self).simple_device = request(f"/auth/pairings/{claimed['pairingId']}/collect", {'pollSecret': claimed['pollSecret']})[1]['deviceToken']
        command = '/api/native/v1/installation/commands/'
        self.assertEqual(403, lan(command + 'lan-http-set', {'enabled': True}, token=self.simple_device, key='b2-device', base=h.BASE)[0])
        self.assertEqual(403, lan('/api/native/v1/installation/network', token=self.simple_device, base=h.BASE)[0])
        self.assertEqual(401, lan(command + 'lan-http-set', {'enabled': True}, key='b2-anon', base=h.BASE)[0])
        self.assertEqual(403, lan(command + 'otra-cosa', {'enabled': True}, token=self.admin, key='b2-other', base=h.BASE)[0])
        self.assertIn(lan(command + 'lan-http-set', {}, token=self.admin, key='b2-empty', base=h.BASE)[0], (400, 422))
        self.assertEqual('f', h.sql('SELECT lan_http FROM core.installation'))
        status, _, text = lan(command + 'lan-http-set', {'enabled': True}, token=self.admin, key='b2-on', base=h.BASE)
        self.assertEqual(200, status, text)
        self.assertEqual({'enabled': True, 'port': int(LAN_PORT)}, json.loads(text)['lanHttp'])
        self.assertEqual('t', h.sql('SELECT lan_http FROM core.installation'))
        self.assertEqual('user:jefe', h.sql("SELECT actor FROM core.audit WHERE tenant='d4-pairing' AND action='installation.lan_http_enabled' ORDER BY occurred_at DESC LIMIT 1"))

    def test_12_on_it_serves_exactly_like_the_secure_address_at_once(self):
        status, kind, _ = lan('/app/')
        self.assertEqual((200, True), (status, kind.startswith('text/html')))
        self.assertEqual(200, lan('/health')[0])
        status, _, text = lan('/api/native/v1/session', token=self.simple_device)
        self.assertEqual((200, 'device:movil-sencillo'), (status, json.loads(text)['actor']))
        self.assertEqual(401, lan('/api/native/v1/session')[0])                                        # sigue exigiendo identidad
        self.assertEqual(403, lan('/api/native/v1/checkout/accounts', token=self.simple_device)[0])    # y los mismos permisos
        # Lo que hace el movil al leer el QR: reclamar el codigo por la conexion sencilla.
        _, issued = request('/auth/pairings', {}, token=self.admin)
        status, _, text = lan('/api/native/v1/auth/pairings/claim', {'code': issued['code'], 'deviceName': 'movil-qr'})
        self.assertEqual(200, status, text)

    def test_13_off_again_cuts_the_port_without_restarting_and_replays_change_nothing(self):
        command = '/api/native/v1/installation/commands/lan-http-set'
        status, _, text = lan(command, {'enabled': False}, token=self.admin, key='b2-off', base=h.BASE)
        self.assertEqual(200, status, text)
        self.assertEqual(503, lan('/api/native/v1/session', token=self.simple_device)[0])
        self.assertEqual(200, request('/session', token=self.simple_device)[0])   # el otro puerto no cambia
        self.assertEqual('user:jefe', h.sql("SELECT actor FROM core.audit WHERE tenant='d4-pairing' AND action='installation.lan_http_disabled' ORDER BY occurred_at DESC LIMIT 1"))
        self.assertEqual(200, lan(command, {'enabled': True}, token=self.admin, key='b2-on-2', base=h.BASE)[0])
        self.assertEqual(200, lan(command, {'enabled': False}, token=self.admin, key='b2-off', base=h.BASE)[0])   # repeticion: misma respuesta, sin efecto
        self.assertEqual('t', h.sql('SELECT lan_http FROM core.installation'))
        self.assertEqual(200, lan('/health')[0])
        self.assertEqual(200, lan(command, {'enabled': False}, token=self.admin, key='b2-off-2', base=h.BASE)[0])
        self.assertEqual(503, lan('/health')[0])

    def test_14_switching_off_cuts_a_live_realtime_channel_on_that_port(self):
        command = '/api/native/v1/installation/commands/lan-http-set'
        self.assertEqual(200, lan(command, {'enabled': True}, token=self.admin, key='b2-ws-on', base=h.BASE)[0])
        status, _, text = lan('/api/native/v1/auth/hub-token', {}, token=self.simple_device, key='b2-hub')
        self.assertEqual(200, status, text)
        ticket = json.loads(text)['hubToken']
        # El canal de la PWA: WebSocket directo (skipNegotiation) con el billete de un uso, por el puerto sencillo.
        sock = socket.create_connection(('127.0.0.1', int(LAN_PORT)), timeout=10)
        try:
            sock.sendall((f'GET /api/native/v1/events?access_token={ticket} HTTP/1.1\r\nHost: 127.0.0.1:{LAN_PORT}\r\n'
                          'Upgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\n'
                          f'Sec-WebSocket-Key: {base64.b64encode(os.urandom(16)).decode()}\r\n\r\n').encode())
            self.assertIn(b' 101 ', sock.recv(4096).split(b'\r\n', 1)[0])
            self.assertEqual(200, lan(command, {'enabled': False}, token=self.admin, key='b2-ws-off', base=h.BASE)[0])
            closed = False
            try:
                while True:
                    chunk = sock.recv(4096)
                    if not chunk: closed = True; break
            except (ConnectionResetError, ConnectionAbortedError): closed = True
            except socket.timeout: pass
            self.assertTrue(closed, 'the live channel must be cut when the simple connection is switched off')
        finally:
            sock.close()

if __name__ == '__main__': unittest.main()
