"""Additional real HTTP regression tests; uses only the isolated existing D1 CI fixture."""
import json
import secrets
import unittest
import urllib.request
import native_http as h

def free_table():
    occupied={b['service']['tableId'] for b in h.ok('/board')}
    return next('M'+str(i) for i in range(1,9) if 'M'+str(i) not in occupied)

def finish_and_release(sid):
    for course in h.dining(sid)['data']['courses']:
        h.mutate(sid,'skip',courseId=course['id'],reason='read-model test')
    h.mutate(sid,'start');h.mutate(sid,'complete')
    entry=next(b for b in h.ok('/board') if b['service']['id']==sid)
    h.ok('/occupancy/'+sid+'/release',{'expectedVersion':entry['occupancyVersion'],'reason':'read-model test'})

class DesktopReadChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls): h.NativeHttp.setUpClass()
    @classmethod
    def tearDownClass(cls): h.stop_server()
    def test_session_role_is_from_server(self):
        for role in ('main','service','kitchen'):
            data=h.ok('/session',role=role)
            self.assertEqual(role,data['role'])
            self.assertNotIn('key',str(data).lower())
    def test_session_identifies_installation_and_server_version(self):
        # D3.4 (F03/F08): identidad estable de la instalacion (misma para todos los roles y tras reiniciar)
        # y version del motor derivada del csproj, no de una cadena fija.
        ids=set()
        for role in ('main','service','kitchen'):
            data=h.ok('/session',role=role)
            self.assertRegex(data['installationId'],r'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
            ids.add(data['installationId'])
            self.assertTrue(data['serverVersion'].startswith('0.7.0-d3.4'),data['serverVersion'])
        self.assertEqual(1,len(ids))
        h.stop_server();h.start_server()
        self.assertEqual(ids.pop(),h.ok('/session')['installationId'])
        health=json.loads(urllib.request.urlopen(h.BASE+'/health',timeout=5).read())
        self.assertEqual(h.ok('/session')['serverVersion'],health['version'])
    def test_command_lookup_echoes_key_and_stays_per_actor(self):
        # D3.4 (F02/F04): toda respuesta a un comando lleva su Idempotency-Key; la consulta de una clave
        # devuelve la respuesta guardada solo al mismo actor, y nunca ejecuta nada.
        key=secrets.token_hex(16)
        status,body=h.request('/services',{'tableId':free_table(),'pax':2,'menuId':'LAB-TASTING'},key=key)
        self.assertEqual(200,status); self.assertEqual(key,h.LAST_HEADERS.get('Idempotency-Key'))
        sid=json.loads(body)['serviceId']
        found=h.ok('/commands/'+key)
        self.assertTrue(found['found']); self.assertEqual(key,found['key']); self.assertEqual(sid,found['response']['serviceId'])
        self.assertEqual({'key':key,'found':False},h.ok('/commands/'+key,role='service'))
        self.assertFalse(h.ok('/commands/'+secrets.token_hex(16))['found'])
        self.assertEqual(422,h.request('/commands/'+'k'*129)[0])
        self.assertEqual(401,h.request('/commands/'+key,role='unknown')[0])
        rejected=h.request('/services/'+sid+'/commands/fire-next',{'expectedVersion':99},key=key+'x')
        self.assertEqual(409,rejected[0]); self.assertEqual(key+'x',h.LAST_HEADERS.get('Idempotency-Key'))
        self.assertFalse(h.ok('/commands/'+key+'x')['found'])   # un rechazo no deja rastro de comando aplicado
        finish_and_release(sid)
    def test_configuration_has_no_financial_fields(self):
        for role in ('main','service','kitchen'):
            cfg=h.ok('/configuration',role=role)
            self.assertEqual(8,len(cfg['tables']))
            for menu in cfg['menus']: self.assertEqual({'id','name'},set(menu))
    def test_catalog_only_for_main_even_with_case_or_profile(self):
        for role in ('service','kitchen'):
            for path in ('/checkout/catalog','/CHECKOUT/CATALOG','/checkout/accounts'):
                self.assertEqual(403,h.request(path,role=role,headers={'X-Terminal-Mode':'main'})[0])
        products=h.ok('/checkout/catalog')
        self.assertTrue(any(p['id']=='water' for p in products))
        self.assertTrue(all('priceCents' in p for p in products))
    def test_released_service_account_still_listed(self):
        sid=h.open_table(free_table())
        finish_and_release(sid)
        self.assertIn(sid,[a['serviceId'] for a in h.ok('/checkout/accounts')])
    def test_anonymous_read_is_denied(self):
        for path in ('/configuration','/session','/checkout/catalog','/checkout/accounts'):
            self.assertEqual(401,h.request(path,role='unknown')[0])

if __name__=='__main__': unittest.main()
