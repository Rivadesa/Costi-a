"""D5.1 packaging base: provisioned PostgreSQL roles, explicit lifecycle commands and file configuration.

Runs FIRST in the battery against the fresh *_d1_test CI database: it provisions the owner/runtime
roles once and exports their connections (GITHUB_ENV) so every later suite exercises the engine
with the runtime role, which has no DDL and cannot rewrite or delete the audit trail.
"""
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.error
import urllib.request

SERVER = Path(sys.argv[1]).resolve()
sys.argv = sys.argv[:1]
BASE = 'http://127.0.0.1:5088'
BOOTSTRAP = os.environ['COSTINA_DB']          # CI superuser: used only by provision
DATABASE = os.environ['PGDATABASE']
DATA = Path(os.environ.get('COSTINA_TEST_DATA') or tempfile.mkdtemp(prefix='costina-data-'))
PASSWORD = 'packaging-password-123'

def clean_env(**extra):
    env = {k: v for k, v in os.environ.items() if not k.startswith('COSTINA_')}
    env['COSTINA_DATA'] = str(DATA)
    env.update(extra)
    return env

def cli(*args, env, stdin=None):
    return subprocess.run(['dotnet', str(SERVER), *args], env=env, input=stdin, text=True, capture_output=True, timeout=120)

def parts(connection):
    return dict(p.split('=', 1) for p in connection.split(';') if '=' in p)

def psql_as(connection, query):
    c = parts(connection)
    env = os.environ | {'PGUSER': c['Username'], 'PGPASSWORD': c['Password'], 'PGDATABASE': c['Database'],
                        'PGHOST': c['Host'], 'PGPORT': c['Port']}
    return subprocess.run(['psql', '-At', '-v', 'ON_ERROR_STOP=1', '-c', query], env=env, text=True, capture_output=True)

def call(path, data=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if data is not None: headers['Idempotency-Key'] = secrets.token_hex(16)
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(BASE + path, data=json.dumps(data).encode() if data is not None else None, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e: return e.code, None

class Server:
    def __init__(self, env): self.env = env
    def __enter__(self):
        self.log = open('d1-http-server.log', 'a', encoding='utf8')
        self.process = subprocess.Popen(['dotnet', str(SERVER)], env=self.env, stdout=self.log, stderr=subprocess.STDOUT)
        for _ in range(150):
            if self.process.poll() is not None: raise RuntimeError('Server exited; inspect d1-http-server.log')
            try:
                with urllib.request.urlopen(BASE + '/health', timeout=1) as r:
                    if r.status == 200: return self
            except (OSError, urllib.error.URLError): pass
            time.sleep(.1)
        raise TimeoutError('Server did not become ready')
    def __exit__(self, *_):
        self.process.terminate()
        try: self.process.wait(timeout=5)
        except subprocess.TimeoutExpired: self.process.kill(); self.process.wait()
        self.log.close()

class Packaging(unittest.TestCase):
    """Ordered on purpose: the numbered steps are one installation story."""

    def config(self, name): return json.loads((DATA / 'config' / name).read_text(encoding='utf8'))

    def test_01_provision_creates_roles_and_private_configuration(self):
        if not DATABASE.endswith('_d1_test'): raise RuntimeError('Use isolated CI test DB')
        env = clean_env(COSTINA_DB_BOOTSTRAP=BOOTSTRAP, COSTINA_DB_NAME=DATABASE, COSTINA_TENANT='d5-tenant',
                        COSTINA_COMPANY='d5-company', COSTINA_LOCATION='d5-location')
        done = cli('provision', env=env)
        self.assertEqual(done.returncode, 0, done.stderr)
        server, owner = self.config('server.json'), self.config('owner.json')
        self.assertEqual(server['COSTINA_MODE'], 'installation')
        self.assertEqual(parts(server['COSTINA_DB'])['Username'], 'costina_runtime')
        self.assertEqual(parts(owner['COSTINA_DB_OWNER'])['Username'], 'costina_owner')
        self.assertNotIn('COSTINA_DB_OWNER', server)
        for secret in (parts(server['COSTINA_DB'])['Password'], parts(owner['COSTINA_DB_OWNER'])['Password']):
            self.assertNotIn(secret, done.stdout + done.stderr)
        self.assertEqual(oct((DATA / 'config' / 'owner.json').stat().st_mode & 0o777), '0o600')
        again = cli('provision', env=env)
        self.assertNotEqual(again.returncode, 0)
        self.assertIn('already provisioned', again.stdout + again.stderr)
        self.assertEqual(owner, self.config('owner.json'))

    def test_02_normal_start_never_creates_the_schema(self):
        started = cli(env=clean_env())
        self.assertNotEqual(started.returncode, 0)
        self.assertEqual(psql_as(BOOTSTRAP, "SELECT to_regclass('native_d1.schema_version') IS NULL").stdout.strip(), 't')

    def test_03_explicit_init_and_upgrade_from_file_configuration(self):
        env = clean_env()
        self.assertNotEqual(cli('upgrade', env=env).returncode, 0)      # nothing to upgrade yet
        refused = cli('init-lab', env=env)                               # fixtures never reach an installation
        self.assertNotEqual(refused.returncode, 0)
        first = cli('init', env=env)
        self.assertEqual(first.returncode, 0, first.stderr)
        users = "SELECT count(*) FROM native_d1.users"
        self.assertEqual(psql_as(BOOTSTRAP, users).stdout.strip(), '0')
        self.assertEqual(psql_as(BOOTSTRAP, "SELECT count(*) FROM native_d1.configuration").stdout.strip(), '0')
        second = cli('init', env=env)
        self.assertEqual(second.returncode, 0)
        self.assertIn('nothing was changed', second.stdout)
        upgraded = cli('upgrade', env=env)
        self.assertEqual(upgraded.returncode, 0, upgraded.stderr)
        self.assertEqual(psql_as(BOOTSTRAP, "SELECT nspowner::regrole::text FROM pg_namespace WHERE nspname='native_d1'").stdout.strip(), 'costina_owner')

    def test_04_runtime_role_has_no_ddl_and_cannot_touch_the_audit_trail(self):
        runtime = self.config('server.json')['COSTINA_DB']
        self.assertEqual(psql_as(runtime, 'SELECT count(*) FROM native_d1.audit').returncode, 0)
        for forbidden in ('CREATE TABLE native_d1.intruder (id int)', 'DROP TABLE native_d1.audit',
                          'DELETE FROM native_d1.audit', "UPDATE native_d1.audit SET actor='x'",
                          "UPDATE native_d1.commands SET response='{}'", 'DELETE FROM native_d1.commands',
                          'DELETE FROM native_d1.outbox', "UPDATE native_d1.outbox SET type='x'",
                          "UPDATE native_d1.users SET role='main'", 'DELETE FROM native_d1.sessions',
                          'TRUNCATE native_d1.services', 'CREATE SCHEMA other'):
            result = psql_as(runtime, forbidden)
            self.assertNotEqual(result.returncode, 0, forbidden)
            self.assertTrue('permission denied' in result.stderr or 'must be owner' in result.stderr, (forbidden, result.stderr))

    def test_05_installation_refuses_an_overprivileged_runtime_connection(self):
        owner = self.config('owner.json')['COSTINA_DB_OWNER']
        started = cli(env=clean_env(COSTINA_DB=owner))
        self.assertNotEqual(started.returncode, 0)
        self.assertIn('runtime role', started.stdout + started.stderr)

    def test_06_installation_serves_from_file_configuration_with_the_runtime_role(self):
        env = clean_env()
        created = cli('create-user', 'jefa', 'main', env=env, stdin=PASSWORD)
        self.assertEqual(created.returncode, 0, created.stderr)
        with Server(env):
            with urllib.request.urlopen(BASE + '/health', timeout=5) as r: health = json.loads(r.read())
            self.assertEqual(health['mode'], 'installation')
            status, login = call('/api/native/v1/auth/login', {'username': 'jefa', 'password': PASSWORD})
            self.assertEqual(status, 200)
            status, session = call('/api/native/v1/session', token=login['token'])
            self.assertEqual((status, session['actor'], session['tenantId']), (200, 'user:jefa', 'd5-tenant'))
            status, configuration = call('/api/native/v1/configuration', token=login['token'])
            self.assertEqual((status, configuration['tables']), (200, []))   # an installation starts without fixtures
            # D5.2: diagnostico solo main, sin datos de negocio; y log en fichero bajo la raiz de datos.
            for _ in range(20):
                status, diagnostics = call('/api/native/v1/diagnostics', token=login['token'])
                if diagnostics and diagnostics['publisher']['lastSuccessAt']: break
                time.sleep(.25)
            self.assertEqual(status, 200)
            self.assertEqual((diagnostics['mode'], diagnostics['runningAsService'], diagnostics['outbox']['pending']), ('installation', False, 0))
            self.assertTrue(diagnostics['publisher']['lastSuccessAt']); self.assertGreater(diagnostics['dataRoot']['freeBytes'], 0)
            self.assertEqual(call('/api/native/v1/diagnostics')[0], 401)
            self.assertEqual(call('/api/native/v1/auth/logout', {}, token=login['token'])[0], 200)
        logs = list((DATA / 'logs').glob('server-*.log'))
        self.assertEqual(len(logs), 1)
        text = logs[0].read_text(encoding='utf8', errors='replace')
        for secret in (PASSWORD, login['token'], 'Password='):
            self.assertNotIn(secret, text)

    def test_07_export_role_connections_for_the_rest_of_the_battery(self):
        target = os.environ.get('GITHUB_ENV')
        if not target: self.skipTest('Not running under GitHub Actions')
        for name in ('server.json', 'owner.json'):   # contrasenas efimeras de CI: aun asi, fuera de los logs
            for value in self.config(name).values():
                if 'Password=' in value: print('::add-mask::' + parts(value)['Password'])
        with open(target, 'a', encoding='utf8') as out:
            out.write('COSTINA_DB=' + self.config('server.json')['COSTINA_DB'] + '\n')
            out.write('COSTINA_DB_OWNER=' + self.config('owner.json')['COSTINA_DB_OWNER'] + '\n')

if __name__ == '__main__':
    unittest.main(verbosity=2, failfast=True)
