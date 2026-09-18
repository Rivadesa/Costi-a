"""D4.1: usuarios, sesiones y tokens sobre HTTP real. Ambito propio (patron de suites)."""
import json
import subprocess
import unittest
import urllib.error
import urllib.request
import native_http as h

PASSWORD = 'clave-de-ensayo-larga-1'

def create_user(username, role, password=PASSWORD):
    return subprocess.run(['dotnet', str(h.SERVER), 'create-user', username, role],
                          env=h.ENV, input=password, text=True, capture_output=True)

def login(username, password):
    req = urllib.request.Request(h.BASE + '/api/native/v1/auth/login',
        data=json.dumps({'username': username, 'password': password}).encode(),
        headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e: return e.code, json.loads(e.read())

def bearer(path, token, data=None):
    body = json.dumps(data).encode() if data is not None else None
    headers = {'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}
    if body is not None: headers['Idempotency-Key'] = 'id-' + token[:12]
    req = urllib.request.Request(h.BASE + '/api/native/v1' + path, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e: return e.code, json.loads(e.read())

class IdentityChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        h.ENV['COSTINA_TENANT'] = 'd4-identity'
        h.NativeHttp.setUpClass()
        for username, role in [('maitre', 'main'), ('camarero', 'service'), ('cocinero', 'kitchen')]:
            result = create_user(username, role)
            assert result.returncode == 0, result.stderr
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_login_token_authenticates_with_real_audit_actor(self):
        status, data = login('maitre', PASSWORD)
        self.assertEqual(200, status)
        self.assertEqual(['main', 'maitre'], [data['role'], data['username']])
        status, session = bearer('/session', data['token'])
        self.assertEqual(200, status)
        self.assertEqual('user:maitre', session['actor'])
        # D4.3: ya no hay claves de laboratorio; la infraestructura de suites autentica como
        # usuarios lab-<rol> con sesion real.
        self.assertEqual('user:lab-main', h.ok('/session')['actor'])

    def test_02_failed_login_is_generic_and_never_issues_tokens(self):
        for username, password in [('maitre', 'contrasena-incorrecta-x'), ('nadie', PASSWORD)]:
            status, data = login(username, password)
            self.assertEqual(401, status)
            self.assertEqual({'error': 'invalid_credentials'}, data)

    def test_03_weak_passwords_and_bad_roles_are_rejected_at_creation(self):
        self.assertNotEqual(0, create_user('debil', 'main', password='corta').returncode)
        self.assertNotEqual(0, create_user('raro', 'gerente').returncode)

    def test_04_token_role_is_enforced_by_server(self):
        _, sala = login('camarero', PASSWORD)
        status, _ = bearer('/checkout/accounts', sala['token'])
        self.assertEqual(403, status)
        _, cocina = login('cocinero', PASSWORD)
        status, _ = bearer('/session', cocina['token'])
        self.assertEqual(200, status)

    def test_05_sliding_renewal_extends_but_absolute_cap_wins(self):
        _, data = login('maitre', PASSWORD)
        before = h.sql("SELECT max(expires_at) FROM native_d1.sessions WHERE tenant='d4-identity'")
        h.sql("UPDATE native_d1.sessions SET expires_at = expires_at - interval '10 minutes' WHERE tenant='d4-identity'")
        self.assertEqual(200, bearer('/session', data['token'])[0])
        after = h.sql("SELECT max(expires_at) FROM native_d1.sessions WHERE tenant='d4-identity'")
        self.assertGreaterEqual(after, before)
        h.sql("UPDATE native_d1.sessions SET absolute_expires_at = now() - interval '1 second' WHERE tenant='d4-identity'")
        self.assertEqual(401, bearer('/session', data['token'])[0])
        h.sql("UPDATE native_d1.sessions SET absolute_expires_at = now() + interval '1 hour' WHERE tenant='d4-identity'")

    def test_06_expiry_logout_and_deactivation_revoke_access(self):
        _, data = login('maitre', PASSWORD)
        h.sql("UPDATE native_d1.sessions SET expires_at = now() - interval '1 second' WHERE tenant='d4-identity'")
        self.assertEqual(401, bearer('/session', data['token'])[0])
        _, fresh = login('maitre', PASSWORD)
        status, out = bearer('/auth/logout', fresh['token'], data={})
        self.assertEqual(200, status); self.assertTrue(out['loggedOut'])
        self.assertEqual(401, bearer('/session', fresh['token'])[0])
        _, again = login('maitre', PASSWORD)
        h.sql("UPDATE native_d1.users SET active=false WHERE tenant='d4-identity' AND username='maitre'")
        self.assertEqual(401, bearer('/session', again['token'])[0])
        h.sql("UPDATE native_d1.users SET active=true WHERE tenant='d4-identity' AND username='maitre'")

    def test_07_only_hashes_at_rest(self):
        _, data = login('cocinero', PASSWORD)
        stored = h.sql("SELECT string_agg(token_hash, ',') FROM native_d1.sessions WHERE tenant='d4-identity'")
        self.assertNotIn(data['token'], stored)
        self.assertTrue(all(len(x) == 64 for x in stored.split(',')))
        hashes = h.sql("SELECT string_agg(password_hash, ',') FROM native_d1.users WHERE tenant='d4-identity'")
        self.assertNotIn(PASSWORD, hashes)
        self.assertTrue(all(x.startswith('pbkdf2-sha256.') for x in hashes.split(',')))

    def test_075_repeated_failures_are_throttled_per_origin_and_user(self):
        # D5.3: 10 fallos en 5 minutos bloquean ESE usuario desde ESE origen (429); los demas siguen entrando.
        for _ in range(10): self.assertEqual(401, login('fantasma', 'contrasena-equivocada-1')[0])
        self.assertEqual(429, login('fantasma', 'contrasena-equivocada-1')[0])
        self.assertEqual(200, login('maitre', PASSWORD)[0])

    def test_08_no_secrets_in_server_log(self):
        _, data = login('maitre', PASSWORD)
        log = open('d1-http-server.log', encoding='utf8', errors='replace').read()
        self.assertNotIn(PASSWORD, log)
        self.assertNotIn(h.LAB_PASSWORD, log)
        self.assertNotIn(data['token'], log)

if __name__ == '__main__': unittest.main()
