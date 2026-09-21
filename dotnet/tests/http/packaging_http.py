"""D5.1 packaging base: provisioned PostgreSQL roles, explicit lifecycle commands and file configuration.

Runs FIRST in the battery against the fresh *_d1_test CI database: it provisions the owner/runtime
roles once and exports their connections (GITHUB_ENV) so every later suite exercises the engine
with the runtime role, which has no DDL and cannot rewrite or delete the audit trail.
"""
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
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
    if os.environ.get('COSTINA_PG_BIN'): env['COSTINA_PG_BIN'] = os.environ['COSTINA_PG_BIN']   # pg_dump del mismo major que el servidor
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

def call(path, data=None, token=None, base=None):
    headers = {'Content-Type': 'application/json'}
    if data is not None: headers['Idempotency-Key'] = secrets.token_hex(16)
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request((base or BASE) + path, data=json.dumps(data).encode() if data is not None else None, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=15) as r: return r.status, json.loads(r.read())
    except urllib.error.HTTPError as e: return e.code, None

class Server:
    def __init__(self, env, base=None): self.env = env; self.base = base or BASE
    def __enter__(self):
        self.log = open('d1-http-server.log', 'a', encoding='utf8')
        self.process = subprocess.Popen(['dotnet', str(SERVER)], env=self.env, stdout=self.log, stderr=subprocess.STDOUT)
        for _ in range(150):
            if self.process.poll() is not None: raise RuntimeError('Server exited; inspect d1-http-server.log')
            try:
                with urllib.request.urlopen(self.base + '/health', timeout=1) as r:
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

    def assert_private(self, path):
        # Unix: 0600. En Windows no hay bits de modo (la proteccion es la ACL, que ejercita el job windows-service con un
        # servicio real): aqui solo se exige que el fichero exista, para poder correr la suite en un PC de desarrollo.
        self.assertTrue(path.is_file(), path)
        if os.name != 'nt': self.assertEqual(oct(path.stat().st_mode & 0o777), '0o600', path)

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
        self.assert_private(DATA / 'config' / 'owner.json')
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
            # D5.4: sin ninguna copia previa, el propio motor hace la primera de forma desatendida.
            for _ in range(80):
                status, diagnostics = call('/api/native/v1/diagnostics', token=login['token'])
                if diagnostics['backup']['lastSuccessAt'] or diagnostics['backup']['lastError']: break
                time.sleep(.5)
            self.assertIsNone(diagnostics['backup']['lastError'])
            self.assertTrue(diagnostics['backup']['automatic'] and diagnostics['backup']['lastSuccessAt'])
            self.assertTrue((DATA / 'backups' / diagnostics['backup']['lastFile']).exists())
            self.assertFalse(diagnostics['backup']['secondDestinationConfigured'])
            self.assertEqual(call('/api/native/v1/auth/logout', {}, token=login['token'])[0], 200)
        logs = list((DATA / 'logs').glob('server-*.log'))
        self.assertEqual(len(logs), 1)
        text = logs[0].read_text(encoding='utf8', errors='replace')
        for secret in (PASSWORD, login['token'], 'Password='):
            self.assertNotIn(secret, text)

    def test_06a_demo_fixtures_are_explicit_flagged_and_only_for_an_empty_configuration(self):
        """D5.6: an installation gets fictitious fixtures only on request, and says so everywhere."""
        env = clean_env()
        loaded = cli('load-demo', env=env)
        self.assertEqual(loaded.returncode, 0, loaded.stderr)
        again = cli('load-demo', env=env)
        self.assertNotEqual(again.returncode, 0)
        self.assertIn('empty configuration', again.stdout + again.stderr)
        with Server(env):
            with urllib.request.urlopen(BASE + '/health', timeout=5) as r: self.assertTrue(json.loads(r.read())['demo'])
            _, login = call('/api/native/v1/auth/login', {'username': 'jefa', 'password': PASSWORD})
            status, session = call('/api/native/v1/session', token=login['token'])
            self.assertTrue(session['demo'])
            status, configuration = call('/api/native/v1/configuration', token=login['token'])
            self.assertEqual(len(configuration['tables']), 8)
            # The engine still runs with the runtime role: a real service through the installed product.
            status, opened = call('/api/native/v1/services', {'tableId': 'M1', 'pax': 2, 'menuId': 'LAB-TASTING'}, token=login['token'])
            self.assertEqual(status, 200)

    def test_06b_lan_https_with_a_name_constrained_local_ca(self):
        """D5.3: an independent TLS stack (OpenSSL) validates the chain with NO exceptions."""
        if os.name == 'nt':
            # En Windows provision-tls deja tls\ solo para SYSTEM y Administradores (D5.3): un usuario corriente ya no puede ni
            # leer ca.key, que es lo correcto. Esta historia corre en Linux (aqui) y, como servicio real, en el job windows-service.
            import ctypes
            if not ctypes.windll.shell32.IsUserAnAdmin(): self.skipTest('TLS provisioning locks tls/ to administrators on Windows')
        env = clean_env(COSTINA_TLS_NAMES='localhost,127.0.0.1')
        tls = DATA / 'tls'
        self.assertNotEqual(cli('renew-tls', env=env).returncode, 0)          # nothing to renew from yet
        made = cli('provision-tls', env=env)
        self.assertEqual(made.returncode, 0, made.stderr)
        self.assertIn('fingerprint', made.stdout)
        for private in ('ca.key', 'server.pfx'):
            self.assert_private(tls / private)
        again = cli('provision-tls', env=env)                                  # a trusted CA is never replaced silently
        self.assertNotEqual(again.returncode, 0)
        self.assertIn('renew-tls', again.stdout + again.stderr)
        for public in ('8.8.8.8', 'bad name'):                                # only private addresses and real names
            self.assertNotEqual(cli('renew-tls', env=clean_env(COSTINA_TLS_NAMES='localhost,' + public)).returncode, 0)

        trusted = ssl.create_default_context(cafile=str(tls / 'ca.crt'))
        def health(host, context=trusted):
            with urllib.request.urlopen(f'https://{host}:5443/health', context=context, timeout=10) as r:
                return json.loads(r.read()), r.headers
        with Server(clean_env()):
            for host in ('localhost', '127.0.0.1'):                            # DNS SAN and IP SAN, hostname checked
                body, headers = health(host)
                self.assertEqual(body['mode'], 'installation')
                self.assertIn('max-age', headers.get('Strict-Transport-Security', ''))
            with self.assertRaises(urllib.error.URLError):                     # nobody trusts it without the local root
                health('localhost', ssl.create_default_context())
            with urllib.request.urlopen('https://localhost:5443/ca.crt', context=trusted, timeout=10) as r:
                self.assertEqual(r.read(), (tls / 'ca.crt').read_bytes())
            with urllib.request.urlopen(BASE + '/health', timeout=5) as r:      # loopback HTTP stays for the local client
                self.assertEqual(r.status, 200)
            probe = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            try: probe.connect(('10.255.255.255', 1)); lan = probe.getsockname()[0]
            except OSError: lan = None
            finally: probe.close()
            if lan and not lan.startswith('127.'):
                with self.assertRaises(OSError): socket.create_connection((lan, 5088), timeout=3)   # never plain HTTP on the LAN
                socket.create_connection((lan, 5443), timeout=3).close()                            # HTTPS does listen there
            status, login = call('/api/native/v1/auth/login', {'username': 'jefa', 'password': PASSWORD})
            status, diagnostics = call('/api/native/v1/diagnostics', token=login['token'])
            self.assertTrue(diagnostics['tls']['enabled']); self.assertEqual(diagnostics['tls']['port'], 5443)
            self.assertIn(diagnostics['tls']['caFingerprintSha256'], made.stdout)
            # D5.6: 'status' resume la instalacion sin credenciales ni secretos.
            report = cli('status', env=clean_env())
            self.assertEqual(report.returncode, 0, report.stdout + report.stderr)
            for expected in ('installation', 'DEMO', diagnostics['tls']['caFingerprintSha256'], 'Last backup', 'NOT CONFIGURED'):
                self.assertIn(expected, report.stdout)
            self.assertNotIn('Password=', report.stdout)
        stopped = cli('status', env=clean_env())
        self.assertEqual(stopped.returncode, 1); self.assertIn('NOT ANSWERING', stopped.stdout)

        # The CA is name constrained: even with its key it cannot vouch for a foreign name.
        work = Path(tempfile.mkdtemp(prefix='costina-nc-'))
        def issue(name):
            run = lambda *a: subprocess.run(['openssl', *a], capture_output=True, text=True)
            self.assertEqual(run('req', '-new', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(work / (name + '.key')),
                                 '-out', str(work / (name + '.csr')), '-subj', '/CN=' + name).returncode, 0)
            (work / (name + '.ext')).write_text('subjectAltName=DNS:' + name + '\n')
            signed = run('x509', '-req', '-in', str(work / (name + '.csr')), '-CA', str(tls / 'ca.crt'), '-CAkey', str(tls / 'ca.key'),
                         '-CAserial', str(work / 'ca.srl'), '-CAcreateserial', '-days', '30', '-extfile', str(work / (name + '.ext')),
                         '-out', str(work / (name + '.crt')))
            self.assertEqual(signed.returncode, 0, signed.stderr)
            return run('verify', '-CAfile', str(tls / 'ca.crt'), str(work / (name + '.crt')))
        self.assertEqual(issue('localhost').returncode, 0)                     # control: a permitted name verifies
        foreign = issue('evil.example')
        self.assertNotEqual(foreign.returncode, 0)
        self.assertIn('permitted subtree violation', foreign.stdout + foreign.stderr)

        # renew-tls reissues the server certificate; the CA devices trust stays the same.
        ca_before, server_before = (tls / 'ca.crt').read_text(), (tls / 'server.crt').read_text()
        renewed = cli('renew-tls', env=env)
        self.assertEqual(renewed.returncode, 0, renewed.stderr)
        self.assertEqual(ca_before, (tls / 'ca.crt').read_text())
        self.assertNotEqual(server_before, (tls / 'server.crt').read_text())
        with Server(clean_env()):
            self.assertEqual(health('localhost')[0]['mode'], 'installation')

    def test_06c_backup_restores_the_same_records_on_a_clean_installation(self):
        """D5.4: a REAL restore, verified by content, on another data root and another empty database."""
        env = clean_env()
        with Server(env):                                                     # a few more rows than a lone user
            _, login = call('/api/native/v1/auth/login', {'username': 'jefa', 'password': PASSWORD})
            self.assertEqual(call('/api/native/v1/auth/pairings', {}, token=login['token'])[0], 200)
        copies = Path(tempfile.mkdtemp(prefix='costina-second-disk-'))
        made = cli('backup', env=clean_env(COSTINA_BACKUP_COPY=str(copies)))
        self.assertEqual(made.returncode, 0, made.stderr)
        self.assertNotIn(parts(self.config('server.json')['COSTINA_DB'])['Password'], made.stdout + made.stderr)
        newest = sorted((DATA / 'backups').glob('costina-*.backup'))[-1]
        manifest = json.loads(Path(str(newest) + '.json').read_text(encoding='utf8'))
        self.assertEqual((manifest['tenantId'], manifest['tables']['users']['rows'], manifest['tables']['pairings']['rows']), ('d5-tenant', 1, 1))
        self.assertEqual((manifest['tables']['services']['rows'], manifest['tables']['occupancies']['rows'], manifest['tables']['configuration']['rows']), (1, 1, 12))   # D5.6: un servicio real viaja en la copia
        self.assert_private(newest)
        self.assertEqual((copies / newest.name).read_bytes(), newest.read_bytes())          # second destination really holds it

        # "Clean machine": another data root and another EMPTY database. Roles are cluster-wide on this single
        # CI cluster, so its configuration is scaffolded instead of re-running provision (which would re-key them).
        target = 'costina_restore_d1_test'
        clean = Path(tempfile.mkdtemp(prefix='costina-clean-')); (clean / 'config').mkdir()
        self.assertEqual(psql_as(BOOTSTRAP, f'CREATE DATABASE {target} OWNER costina_owner').returncode, 0)
        self.assertEqual(psql_as(BOOTSTRAP, f'GRANT CONNECT ON DATABASE {target} TO costina_runtime').returncode, 0)
        for name in ('server.json', 'owner.json'):
            config = {k: v.replace('Database=' + DATABASE, 'Database=' + target) for k, v in self.config(name).items()}
            if name == 'server.json': config['COSTINA_PORT'] = '5094'
            (clean / 'config' / name).write_text(json.dumps(config), encoding='utf8')
        fresh = clean_env(COSTINA_DATA=str(clean))
        elsewhere = BOOTSTRAP.replace('Database=' + DATABASE, 'Database=' + target)

        damaged = clean / 'damaged.backup'
        damaged.write_bytes(newest.read_bytes() + b'x'); Path(str(damaged) + '.json').write_text(json.dumps(manifest), encoding='utf8')
        refused = cli('restore', str(damaged), env=fresh)
        self.assertNotEqual(refused.returncode, 0); self.assertIn('checksum', refused.stdout + refused.stderr)
        foreign = cli('restore', str(newest), env=clean_env(COSTINA_DATA=str(clean), COSTINA_TENANT='otro-tenant'))
        self.assertNotEqual(foreign.returncode, 0); self.assertIn('scope', foreign.stdout + foreign.stderr)
        self.assertEqual(psql_as(elsewhere, "SELECT to_regclass('native_d1.users') IS NULL").stdout.strip(), 't')   # nothing touched

        restored = cli('restore', str(newest), env=fresh)
        self.assertEqual(restored.returncode, 0, restored.stdout + restored.stderr)
        self.assertIn('Restored and verified', restored.stdout)
        # Independent comparison, table by table and by content, between the source and the restored database.
        tables = psql_as(BOOTSTRAP, "SELECT string_agg(tablename, ',' ORDER BY tablename) FROM pg_tables WHERE schemaname='native_d1'").stdout.strip().split(',')
        self.assertGreaterEqual(len(tables), 12)
        for table in tables:
            digest = f"SELECT count(*) || ':' || coalesce(md5(string_agg(h, '' ORDER BY h)), '') FROM (SELECT md5(t::text) AS h FROM native_d1.{table} t) s"
            self.assertEqual(psql_as(BOOTSTRAP, digest).stdout.strip(), psql_as(elsewhere, digest).stdout.strip(), table)
        self.assertNotEqual(cli('restore', str(newest), env=fresh).returncode, 0)             # never overwrites an installation

        # The restored installation WORKS: same user and password, runtime role still least-privileged.
        runtime = json.loads((clean / 'config' / 'server.json').read_text(encoding='utf8'))['COSTINA_DB']
        self.assertIn('permission denied', psql_as(runtime, 'DELETE FROM native_d1.audit').stderr)
        with Server(fresh, base='http://127.0.0.1:5094'):
            status, again = call('/api/native/v1/auth/login', {'username': 'jefa', 'password': PASSWORD}, base='http://127.0.0.1:5094')
            self.assertEqual(status, 200)
            status, session = call('/api/native/v1/session', token=again['token'], base='http://127.0.0.1:5094')
            self.assertEqual(session['installationId'], manifest['installationId'])

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
