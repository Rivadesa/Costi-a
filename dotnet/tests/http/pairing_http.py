"""D4.2: emparejamiento de dispositivos y revocacion sobre HTTP real. Ambito propio."""
import json
import subprocess
import unittest
import urllib.error
import urllib.request
import native_http as h

PASSWORD = 'clave-de-ensayo-larga-1'

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
        h.NativeHttp.setUpClass()
        result = subprocess.run(['dotnet', str(h.SERVER), 'create-user', 'jefe', 'main'],
                                env=h.ENV, input=PASSWORD, text=True, capture_output=True)
        assert result.returncode == 0, result.stderr
        cls.admin = request('/auth/login', {'username': 'jefe', 'password': PASSWORD})[1]['token']
    @classmethod
    def tearDownClass(cls): h.stop_server()

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

if __name__ == '__main__': unittest.main()
