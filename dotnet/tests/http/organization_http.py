"""E2 (Hito 6): organizacion editable del nucleo - salas, mesas y estaciones - por HTTP real contra PostgreSQL.
Solo el puesto principal edita; nunca se borra, se desactiva; una mesa con ocupacion viva no se desactiva
(el nucleo lo pregunta al modulo por contrato); GET /configuration solo lista mesas activas; los puestos
se aprueban con una estacion activa de la lista.
"""
import secrets
import unittest

import native_http as h

ok, request = h.ok, h.request

def command(action, role='main', **body):
    return request('/organization/commands/' + action, body, role=role)

def organization():
    return ok('/organization')

def tables(zone_id):
    return {t['id']: t for z in organization()['zones'] if z['id'] == zone_id for t in z['tables']}

class Organization(unittest.TestCase):
    @classmethod
    def setUpClass(cls): h.start_server()
    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_demo_organization_is_relational_and_only_main_reads_it(self):
        org = organization()
        self.assertEqual([z['id'] for z in org['zones']], ['sala'])
        self.assertEqual([t['id'] for t in org['zones'][0]['tables']], ['M%d' % i for i in range(1, 9)])
        self.assertEqual({s['id']: s['kind'] for s in org['stations']}, {'pase': 'Pass', 'cold': 'Kitchen', 'hot': 'Kitchen', 'sala-1': 'Room'})
        for role in ('service', 'kitchen'):
            self.assertEqual(request('/organization', role=role)[0], 403, role)
            self.assertEqual(command('zone-create', role=role, id='x', name='x')[0], 403, role)
        # La configuracion operativa (todos los roles) lleva la sala de cada mesa y solo mesas activas.
        first = ok('/configuration', role='service')['tables'][0]
        self.assertEqual((first['id'], first['zoneId'], first['zoneName'], first['capacity']), ('M1', 'sala', 'Sala', 12))

    def test_02_zones_and_tables_are_created_edited_and_never_deleted(self):
        status, body = command('zone-create', id='terraza', name='Terraza', sort=5)
        self.assertEqual(status, 200, body)
        self.assertEqual(command('zone-create', id='terraza', name='Otra')[0], 409)            # codigo estable, unico
        for bad in (dict(id='con espacio', name='x'), dict(id='ñ', name='x'), dict(id='t2', name=''), dict(id='t2', name='x' * 61), dict(id='t2', name='x', sort=-1)):
            self.assertIn(command('zone-create', **bad)[0], (409, 422), bad)
        status, body = command('table-create', id='T1', name='Terraza 1', capacity=4, zoneId='terraza', sort=0)
        self.assertEqual(status, 200, body)
        for bad in (dict(id='T2', name='x', capacity=0, zoneId='terraza'), dict(id='T2', name='x', capacity=61, zoneId='terraza'),
                    dict(id='T2', name='x', capacity=2, zoneId='no-existe'), dict(id='T2', name='x', capacity=2)):
            self.assertIn(command('table-create', **bad)[0], (404, 409, 422), bad)
        status, body = command('table-update', id='T1', name='Terraza uno', capacity=6)
        self.assertEqual((status, h.json.loads(body)['name'], h.json.loads(body)['capacity']), (200, 'Terraza uno', 6))
        self.assertEqual(command('zone-update', id='terraza', name='La terraza')[0], 200)
        # Desactivar: la mesa desaparece de /configuration pero sigue en la organizacion (marcada); reactivar la devuelve.
        self.assertEqual(command('table-deactivate', id='T1')[0], 200)
        self.assertNotIn('T1', [t['id'] for t in ok('/configuration')['tables']])
        self.assertFalse(tables('terraza')['T1']['active'])
        self.assertEqual(request('/dining/services', {'tableId': 'T1', 'pax': 2, 'menuId': 'LAB-TASTING'})[0], 409)   # table_inactive
        self.assertEqual(command('table-reactivate', id='T1')[0], 200)
        self.assertIn('T1', [t['id'] for t in ok('/configuration')['tables']])
        # Una sala con mesas activas no se desactiva; sin ellas si, y entonces sus mesas no salen en /configuration.
        self.assertEqual(command('zone-deactivate', id='terraza')[0], 409)
        self.assertEqual(command('table-deactivate', id='T1')[0], 200)
        self.assertEqual(command('zone-deactivate', id='terraza')[0], 200)
        self.assertEqual(command('table-reactivate', id='T1')[0], 409)                         # zone_inactive
        self.assertEqual(command('zone-reactivate', id='terraza')[0], 200)
        self.assertEqual(command('table-reactivate', id='T1')[0], 200)
        self.assertEqual(command('table-update', id='M1', zoneId='terraza')[0], 200)           # mover de sala
        self.assertEqual(command('table-update', id='M1', zoneId='sala')[0], 200)
        self.assertEqual(command('zone-update', id='no-existe', name='x')[0], 404)
        self.assertEqual(command('nada', id='x')[0], 403)                                     # accion no anunciada

    def test_03_a_table_in_use_by_a_module_cannot_be_deactivated(self):
        occupied = {r['service']['tableId'] for r in ok('/dining/board')}                     # otras suites dejan mesas abiertas
        table = next(t['id'] for t in ok('/configuration')['tables'] if t['id'] not in occupied)
        sid = h.open_table(table)
        status, body = command('table-deactivate', id=table)
        self.assertEqual((status, h.json.loads(body)['error']), (409, 'table_in_use'))
        self.release(sid)
        self.assertEqual(command('table-deactivate', id=table)[0], 200)
        self.assertEqual(command('table-reactivate', id=table)[0], 200)

    def release(self, sid):
        h.mutate(sid, 'cancel-unstarted', reason='prueba')
        occupancy = h.ok('/dining/board')
        version = next(r['occupancyVersion'] for r in occupancy if r['service']['id'] == sid)
        ok('/dining/occupancy/' + sid + '/release', {'expectedVersion': version, 'reason': 'prueba'})

    def test_04_stations_drive_device_approval(self):
        self.assertEqual(command('station-create', id='barra', name='Barra', kind='bar', sort=9)[0], 200)
        self.assertEqual(command('station-create', id='barra', name='Otra', kind='bar')[0], 409)
        self.assertEqual(command('station-create', id='x2', name='x', kind='nada')[0], 422)
        self.assertEqual(command('station-update', id='barra', name='Barra alta')[0], 200)
        self.assertEqual(command('station-deactivate', id='pase')[0], 409)                     # reservada: la de pase existe siempre
        self.assertEqual(command('station-update', id='pase', kind='kitchen')[0], 409)
        self.assertEqual(command('station-deactivate', id='barra')[0], 200)
        # Aprobar un puesto exige una estacion ACTIVA de la lista.
        pairing = ok('/auth/pairings', {})
        status, claimed = request('/auth/pairings/claim', {'code': pairing['code'], 'deviceName': 'tablet-barra'}, role='')
        self.assertEqual(status, 200)
        pid = h.json.loads(claimed)['pairingId']
        for station in ('barra', 'inventada', ''):
            self.assertEqual(request('/auth/pairings/' + pid + '/approve', {'role': 'service', 'station': station})[0], 422, station)
        self.assertEqual(command('station-reactivate', id='barra')[0], 200)
        self.assertEqual(request('/auth/pairings/' + pid + '/approve', {'role': 'service', 'station': 'barra'})[0], 200)

    def test_05_commands_are_idempotent_and_audited(self):
        key = secrets.token_hex(8)
        first = request('/organization/commands/zone-create', {'id': 'jardin', 'name': 'Jardín'}, key=key)
        again = request('/organization/commands/zone-create', {'id': 'jardin', 'name': 'Jardín'}, key=key)
        self.assertEqual((first[0], again[0], first[1]), (200, 200, again[1]))                 # misma respuesta guardada, sin 409
        self.assertEqual(h.sql("SELECT count(*) FROM core.audit WHERE tenant='d1-tenant' AND action='organization.zone_created' AND aggregate_id='jardin'"), '1')
        self.assertEqual(h.sql("SELECT count(*) FROM core.zones WHERE tenant='d1-tenant' AND id='jardin'"), '1')

if __name__ == '__main__':
    unittest.main(verbosity=2, failfast=True)
