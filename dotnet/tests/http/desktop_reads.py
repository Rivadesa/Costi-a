"""Additional real HTTP regression tests; uses only the isolated existing D1 CI fixture."""
import unittest
import native_http as h

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
        occupied={b['service']['tableId'] for b in h.ok('/board')}
        table=next('M'+str(i) for i in range(1,9) if 'M'+str(i) not in occupied)
        sid=h.open_table(table)
        for course in h.dining(sid)['data']['courses']:
            h.mutate(sid,'skip',courseId=course['id'],reason='read-model test')
        h.mutate(sid,'start');h.mutate(sid,'complete')
        entry=next(b for b in h.ok('/board') if b['service']['id']==sid)
        h.ok('/occupancy/'+sid+'/release',{'expectedVersion':entry['occupancyVersion'],'reason':'read-model test'})
        self.assertIn(sid,[a['serviceId'] for a in h.ok('/checkout/accounts')])
    def test_anonymous_read_is_denied(self):
        for path in ('/configuration','/session','/checkout/catalog','/checkout/accounts'):
            self.assertEqual(401,h.request(path,role='unknown')[0])

if __name__=='__main__': unittest.main()
