"""D1 engineering acceptance: real HTTP, separate processes and PostgreSQL. No mock DB.
Only runs against the explicitly created *_d1_test CI database. Never deletes business data.
"""
import concurrent.futures
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time
import unittest
import urllib.error
import urllib.request

SERVER = Path(sys.argv[1]).resolve()
sys.argv = sys.argv[:1]
BASE = 'http://127.0.0.1:5088'
ENV = os.environ.copy()
ENV.update(COSTINA_LAB_MODE='true', COSTINA_PORT='5088', COSTINA_TENANT='d1-tenant',
           COSTINA_COMPANY='d1-company', COSTINA_LOCATION='d1-location')
# D4.3: las claves de rol quedan retiradas. Cada rol autentica como usuario lab-<rol> con
# sesion real; KEYS pasa a contener tokens de sesion y todas las suites siguen funcionando.
KEYS = {r: '' for r in ('main', 'service', 'kitchen')}
LAB_PASSWORD = 'lab-password-ensayo-123'

def _provision_identities(env):
    # MISMO entorno que el servidor en marcha: un reinicio con otro ambito (test_07) crea y
    # loguea los usuarios de ESE ambito. Errores ruidosos: aqui no se reintenta en silencio.
    for role in ('main', 'service', 'kitchen'):
        subprocess.run(['dotnet', str(SERVER), 'create-user', 'lab-' + role, role],
                       env=env, input=LAB_PASSWORD, text=True, capture_output=True)  # idempotente: si existe, falla y da igual
        req = urllib.request.Request(BASE + '/api/native/v1/auth/login',
            data=json.dumps({'username': 'lab-' + role, 'password': LAB_PASSWORD}).encode(),
            headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req, timeout=15) as r:
            KEYS[role] = json.loads(r.read())['token']
PROCESS = None
LOG = None
LAST_HEADERS = {}  # cabeceras de la ultima respuesta (D3.4: el servidor hace eco de Idempotency-Key)

def sql(query):
    return subprocess.check_output(['psql', '-At', '-v', 'ON_ERROR_STOP=1', '-c', query], env=ENV, text=True).strip()

def request(path, data=None, role='main', key=None, raw=None, headers=None):
    global LAST_HEADERS
    h = {'Authorization': 'Bearer ' + KEYS.get(role, ''), 'Content-Type': 'application/json'}
    if data is not None or raw is not None: h['Idempotency-Key'] = key or secrets.token_hex(16)
    h.update(headers or {})
    body = raw if raw is not None else json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(BASE + '/api/native/v1' + path, data=body, headers=h)
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            LAST_HEADERS = dict(r.headers); return r.status, r.read()
    except urllib.error.HTTPError as e:
        LAST_HEADERS = dict(e.headers); return e.code, e.read()

def ok(path, data=None, **kw):
    status, body = request(path, data, **kw)
    if status != 200: raise AssertionError((path, status, body.decode()))
    return json.loads(body)

def open_table(table='M1'):
    return ok('/services', {'tableId': table, 'pax': 2, 'menuId': 'LAB-TASTING'})['serviceId']

def dining(id): return ok('/services/' + id)
def account(id): return ok('/checkout/services/' + id)
def mutate(id, action, **fields):
    return ok('/services/' + id + '/commands/' + action,
              dict(expectedVersion=dining(id)['version'], **fields))
def charge(id, action, **fields):
    return ok('/checkout/services/' + id + '/commands/' + action,
              dict(expectedVersion=account(id)['version'], **fields))

def start_server(extra=None):
    global PROCESS, LOG
    env = ENV | (extra or {})
    LOG = open('d1-http-server.log', 'a', encoding='utf8')
    PROCESS = subprocess.Popen(['dotnet', str(SERVER)], env=env, stdout=LOG, stderr=subprocess.STDOUT)
    for _ in range(100):
        if PROCESS.poll() is not None: raise RuntimeError('Server exited; inspect d1-http-server.log')
        try:
            with urllib.request.urlopen(BASE + '/health', timeout=1) as r:
                if r.status == 200:
                    _provision_identities(env)
                    return
        except urllib.error.HTTPError: raise
        except (OSError, urllib.error.URLError): pass
        time.sleep(.1)
    raise TimeoutError('Server did not become ready')

def stop_server():
    global PROCESS, LOG
    if PROCESS and PROCESS.poll() is None:
        PROCESS.terminate()
        try: PROCESS.wait(timeout=5)
        except subprocess.TimeoutExpired: PROCESS.kill(); PROCESS.wait()
    if LOG: LOG.close()
    PROCESS = None

class NativeHttp(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not ENV.get('PGDATABASE', '').endswith('_d1_test'): raise RuntimeError('Use isolated CI test DB')
        subprocess.run(['dotnet', str(SERVER), 'init-lab'], env=ENV, check=True)
        start_server()

    @classmethod
    def tearDownClass(cls): stop_server()

    def test_01_prepayment_restart_kitchen_and_release_without_settling(self):
        id = open_table()
        mutate(id, 'start'); mutate(id, 'fire-next')
        initial = dining(id)
        charge(id, 'payment', paymentId='prepayment-1', method='card', amountCents=30000)
        self.assertEqual(dining(id), initial)
        stop_server(); start_server()
        self.assertEqual(dining(id), initial)
        self.assertEqual(account(id)['data']['paidCents'], 30000)
        status, _ = request('/services/'+id+'/commands/ready', {'expectedVersion':initial['version'], 'courseId':'p1'})
        self.assertEqual(status,409)
        for course in ('p1','p2'):
            if course=='p2': mutate(id,'fire-next')
            current = next(c for c in dining(id)['data']['courses'] if c['id']==course)
            for item in current['preparations']:
                mutate(id,'preparation-start',courseId=course,itemId=item['id'])
                mutate(id,'preparation-ready',courseId=course,itemId=item['id'])
            mutate(id,'ready',courseId=course); mutate(id,'serve',courseId=course)
        charge(id,'add-product',productId='water',quantity=1)
        self.assertEqual(account(id)['data']['balanceCents'],400)
        mutate(id,'complete')
        ok('/occupancy/'+id+'/release',{'expectedVersion':1,'reason':'salida de prueba'})
        self.assertEqual(account(id)['data']['state'],'Open')
        self.assertEqual(account(id)['data']['balanceCents'],400)
        second=open_table('M1')
        self.assertNotEqual(second,id)
        self.assertEqual(account(id)['data']['paidCents'],30000)
        self.assertEqual(dining(id)['data']['state'],'Completed')

    def test_02_persistent_idempotency_and_no_duplicate_audit(self):
        key=secrets.token_hex(16)
        data={'tableId':'M2','pax':2,'menuId':'LAB-TASTING'}
        first=request('/services',data,key=key)
        before=sql('SELECT count(*) FROM native_d1.audit')
        stop_server(); start_server()
        self.assertEqual(request('/services',data,key=key),first)
        self.assertEqual(sql('SELECT count(*) FROM native_d1.audit'),before)
        self.assertEqual(request('/services',data|{'pax':3},key=key)[0],409)
        self.assertEqual(sql('SELECT count(*) FROM native_d1.audit'),before)

    def test_03_concurrent_open_same_table_has_one_winner(self):
        before=int(sql('SELECT count(*) FROM native_d1.services'))
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results=list(pool.map(lambda _:request('/services',{'tableId':'M3','pax':2,'menuId':'LAB-TASTING'}),range(2)))
        self.assertEqual(sorted(s for s,_ in results),[200,409])
        self.assertEqual(int(sql('SELECT count(*) FROM native_d1.services')),before+1)
        self.assertEqual(sql("SELECT count(*) FROM native_d1.occupancies WHERE table_id='M3' AND state='Occupied'"),'1')

    def test_04_concurrent_same_key_replays_one_payment(self):
        id=open_table('M4'); key=secrets.token_hex(16)
        body={'expectedVersion':1,'paymentId':'concurrent-payment','method':'card','amountCents':1000}
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results=list(pool.map(lambda _:request('/checkout/services/'+id+'/commands/payment',body,key=key),range(2)))
        self.assertEqual(results[0],results[1]); self.assertEqual(results[0][0],200)
        self.assertEqual(len(account(id)['data']['payments']),1)

    def test_05_stale_version_and_failed_command_roll_back(self):
        id=open_table('M5'); mutate(id,'start')
        before=sql('SELECT count(*) FROM native_d1.outbox')
        self.assertEqual(request('/services/'+id+'/commands/fire-next',{'expectedVersion':1})[0],409)
        self.assertEqual(sql('SELECT count(*) FROM native_d1.outbox'),before)
        before=sql('SELECT count(*) FROM native_d1.commands')
        self.assertEqual(request('/services/'+id+'/commands/complete',{'expectedVersion':2})[0],409)
        self.assertEqual(sql('SELECT count(*) FROM native_d1.commands'),before)
        self.assertEqual(dining(id)['version'],2)

    def test_06_authorization_financial_boundaries_and_scope_headers(self):
        self.assertEqual(request('/board',role='unknown')[0],401)
        id=open_table('M6')
        for role in ('service','kitchen'):
            self.assertEqual(request('/checkout/services/'+id,role=role)[0],403)
            self.assertEqual(request('/checkout/services/'+id,role=role,headers={'X-Terminal-Mode':'main'})[0],403)
            board=ok('/board',role=role)
            text=json.dumps(board).lower()
            for field in ('pricecents','paidcents','balancecents','payments','charges','account'):
                self.assertNotIn(field,text)
        self.assertEqual(request('/services',{'tableId':'M7','pax':1,'menuId':'LAB-TASTING'},role='kitchen')[0],403)
        self.assertEqual(request('/services/'+id,headers={'X-Company-Id':'other'})[0],422)

    def test_07_other_scope_cannot_read_existing_services(self):
        id=ok('/board')[0]['service']['id']
        stop_server(); start_server({'COSTINA_COMPANY':'another-company'})
        try:
            self.assertEqual(ok('/board'),[])
            self.assertEqual(request('/services/'+id)[0],404)
            self.assertEqual(request('/checkout/services/'+id)[0],404)
        finally:
            stop_server(); start_server()

    def test_08_initialization_does_not_overwrite_catalog_or_services(self):
        sql("UPDATE native_d1.configuration SET payload=jsonb_set(payload,'{priceCents}','575') WHERE tenant='d1-tenant' AND kind='product' AND id='water'")
        before=sql('SELECT count(*) FROM native_d1.services')
        stop_server()
        subprocess.run(['dotnet',str(SERVER),'init-lab'],env=ENV,check=True)
        start_server()
        self.assertEqual(sql('SELECT count(*) FROM native_d1.services'),before)
        self.assertEqual(sql("SELECT payload->>'priceCents' FROM native_d1.configuration WHERE tenant='d1-tenant' AND kind='product' AND id='water'"),'575')

    def test_09_request_validation_and_price_tampering(self):
        self.assertEqual(request('/services',{'tableId':'M7','pax':-1,'menuId':'LAB-TASTING'})[0],422)
        self.assertEqual(request('/services',{'tableId':'M7','pax':1,'menuId':'LAB-TASTING','unitPriceCents':1})[0],422)
        self.assertEqual(request('/services',raw=b'not-json')[0],422)
        self.assertEqual(request('/services',{'tableId':'M7','pax':1,'menuId':'LAB-TASTING'},headers={'Idempotency-Key':''})[0],422)

    def test_10_failure_after_state_write_rolls_back_everything(self):
        id=open_table('M8'); mutate(id,'start')
        before=dining(id)
        audit=sql('SELECT count(*) FROM native_d1.audit')
        commands=sql('SELECT count(*) FROM native_d1.commands')
        sql("CREATE FUNCTION native_d1.reject_pause() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.type='service.paused' THEN RAISE EXCEPTION 'injected failure' USING ERRCODE='23514'; END IF; RETURN NEW; END $$")
        sql('CREATE TRIGGER fail_pause BEFORE INSERT ON native_d1.outbox FOR EACH ROW EXECUTE FUNCTION native_d1.reject_pause()')
        key=secrets.token_hex(16)
        payload={'expectedVersion':before['version'],'reason':'test rollback'}
        try:
            self.assertEqual(request('/services/'+id+'/commands/pause',payload,key=key)[0],503)
            self.assertEqual(dining(id),before)
            self.assertEqual(sql('SELECT count(*) FROM native_d1.audit'),audit)
            self.assertEqual(sql('SELECT count(*) FROM native_d1.commands'),commands)
        finally:
            sql('DROP TRIGGER fail_pause ON native_d1.outbox')
            sql('DROP FUNCTION native_d1.reject_pause()')
        self.assertEqual(ok('/services/'+id+'/commands/pause',payload,key=key)['data']['state'],'Paused')

    def test_11_outbox_and_audit_are_one_to_one(self):
        self.assertEqual(sql('SELECT count(*) FROM native_d1.audit'),sql('SELECT count(*) FROM native_d1.outbox'))
        # D2: el publicador del servidor marca published_at para SU ambito. La publicacion es
        # asincrona (at-least-once), asi que se espera el drenado en vez de exigir instantaneidad.
        for _ in range(100):
            if sql("SELECT count(*) FROM native_d1.outbox WHERE tenant='d1-tenant' AND published_at IS NULL")=='0': break
            time.sleep(0.1)
        self.assertEqual(sql("SELECT count(*) FROM native_d1.outbox WHERE tenant='d1-tenant' AND published_at IS NULL"),'0')

if __name__=='__main__':
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(NativeHttp))
    Path('d1-http-results.json').write_text(json.dumps({'run':os.environ.get('GITHUB_RUN_ID'),
        'commit':os.environ.get('GITHUB_SHA'),'tests':result.testsRun,'failures':len(result.failures),
        'errors':len(result.errors),'successful':result.wasSuccessful()},indent=2))
    stop_server()
    sys.exit(0 if result.wasSuccessful() else 1)
