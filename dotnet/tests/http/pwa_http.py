"""D6.1 (#28): the engine hosts the operational PWA on the same origin, and the operational catalogue has no money.

Own scope. Requires the PWA build (frontend/pwa/dist) to exist BEFORE the server was built, as CI does.
With COSTINA_E2E=1 it also drives a real browser (Playwright/Chromium) through pairing against this server.
"""
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import unittest
import urllib.error
import urllib.request

import native_http as h

h.ENV['COSTINA_TENANT'] = 'd6-pwa'
MONEY = re.compile(r'(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)', re.I)
PWA = Path(__file__).resolve().parents[3] / 'frontend' / 'pwa'

def fetch(path, headers=None):
    req = urllib.request.Request(h.BASE + path, headers=headers or {})
    opener = urllib.request.build_opener(NoRedirect)
    try:
        with opener.open(req, timeout=15) as r: return r.status, dict(r.headers), r.read()
    except urllib.error.HTTPError as e: return e.code, dict(e.headers), e.read()

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None

def free_table():
    occupied = {b['service']['tableId'] for b in h.ok('/dining/board')}
    return next('M' + str(i) for i in range(1, 9) if 'M' + str(i) not in occupied)

def open_table(table, offer='LAB-TASTING'):
    return h.ok('/dining/services', {'tableId': table, 'pax': 2, 'offerId': offer})['serviceId']

def finish_and_release(sid):
    """Deja la mesa libre otra vez: el resto de las pruebas de este ambito (y las E2E) eligen sus mesas por carril."""
    for course in h.dining(sid)['data']['courses']:
        if course['state'] == 'Pending': h.mutate(sid, 'skip', courseId=course['id'], reason='pwa_http')
    h.mutate(sid, 'start'); h.mutate(sid, 'complete')
    entry = next(b for b in h.ok('/dining/board') if b['service']['id'] == sid)
    h.ok('/dining/occupancy/' + sid + '/release', {'expectedVersion': entry['occupancyVersion'], 'reason': 'pwa_http'})

def money_keys(value, path='$'):
    if isinstance(value, list):
        for i, item in enumerate(value): yield from money_keys(item, f'{path}[{i}]')
    elif isinstance(value, dict):
        for key, inner in value.items():
            if MONEY.search(key): yield f'{path}.{key}'
            yield from money_keys(inner, f'{path}.{key}')

class Pwa(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        subprocess.run(['dotnet', str(h.SERVER), 'init-lab'], env=h.ENV, check=True)
        h.start_server()

    @classmethod
    def tearDownClass(cls): h.stop_server()

    def test_01_operational_catalogue_has_no_money_and_is_not_for_kitchen(self):
        for role in ('main', 'service'):
            status, body = h.request('/catalog', role=role)
            self.assertEqual(status, 200, role)
            items = json.loads(body)
            # E3: un vendible por presentacion (water/bottle, wine/glass, wine/bottle); categoria sin dinero; nunca 'tariff'.
            self.assertEqual(sorted((i['id'], i['presentationId']) for i in items), [('croquetas', 'unit'), ('lubina', 'unit'), ('tarta', 'unit'), ('water', 'bottle'), ('wine', 'bottle'), ('wine', 'glass')])   # E4b: platos de la carta de ensayo
            self.assertEqual(set(items[0]), {'id', 'presentationId', 'name', 'presentation', 'categoryId', 'categoryName', 'imageSha', 'section', 'family', 'categoryPath', 'reference', 'glass'})   # E3.2: huella de la foto; E3.3: seccion, familia, ruta, referencia y copa; nunca dinero
            self.assertEqual(list(money_keys(items)), [])
        self.assertEqual(h.request('/catalog', role='kitchen')[0], 403)
        self.assertEqual(fetch('/api/native/v1/catalog')[0], 401)
        # The financial catalogue stays where it was: main only, with prices.
        self.assertEqual(h.request('/checkout/catalog', role='service')[0], 403)

    def test_02_what_a_waiter_device_can_read_never_carries_money(self):
        sid = h.open_table('M2')
        for path in ('/session', '/configuration', '/dining/board', '/dining/services/' + sid, '/catalog'):
            status, body = h.request(path, role='service')
            self.assertEqual(status, 200, path)
            self.assertEqual(list(money_keys(json.loads(body))), [], path)
            if path == '/configuration': self.assertTrue(json.loads(body)['stations'], path)   # C2: las estaciones tambien pasan la guarda
        # D6.3: tampoco lo que RESPONDE una orden de sala. Una clave como 'chargeId' basta para que la guarda de la PWA
        # rechace la respuesta y la orden quede sin confirmar: la referencia del consumo viaja como consumptionId.
        status, body = h.request('/dining/services/' + sid + '/commands/add-consumption',
            dict(expectedVersion=h.dining(sid)['version'], productId='water', quantity=1), role='service')
        self.assertEqual(status, 200, body)
        self.assertEqual(set(json.loads(body)), {'version', 'consumptionId', 'productId', 'presentationId', 'quantity'})
        self.assertEqual(list(money_keys(json.loads(body))), [], 'add-consumption')

    def test_03_pwa_is_served_anonymously_on_the_same_origin_with_strict_headers(self):
        with urllib.request.urlopen(h.BASE + '/health', timeout=5) as r: self.assertTrue(json.loads(r.read())['pwa'], 'server was built without frontend/pwa/dist')
        status, headers, body = fetch('/app/')
        self.assertEqual(status, 200)
        self.assertIn(b'<div id="app">', body)
        csp = headers['Content-Security-Policy']
        for required in ("default-src 'none'", "script-src 'self'", "style-src 'self'", "connect-src 'self' wss://127.0.0.1:" + h.PORT, "frame-ancestors 'none'", "base-uri 'none'"):
            self.assertIn(required, csp)
        for forbidden in ('unsafe-inline', 'unsafe-eval', 'http:', 'https:', '*', 'data:'):
            self.assertNotIn(forbidden, csp)
        # C3b: las fotos de los articulos se piden con el token y se pintan desde la memoria (URL blob:). Solo en img-src.
        self.assertIn("img-src 'self' blob:;", csp)
        self.assertEqual(1, csp.count('blob:'), csp)
        self.assertEqual(headers['X-Content-Type-Options'], 'nosniff')
        self.assertEqual(headers['Referrer-Policy'], 'no-referrer')
        self.assertEqual(headers['Cache-Control'], 'no-cache')
        self.assertNotIn('Set-Cookie', headers)
        self.assertNotIn(b'<script>', body)                                    # no inline scripts to allow
        asset = re.search(rb'/app/assets/[A-Za-z0-9_.-]+\.js', body).group(0).decode()
        status, headers, _ = fetch(asset)
        self.assertEqual((status, headers['Cache-Control']), (200, 'public, max-age=31536000, immutable'))
        self.assertIn('javascript', headers['Content-Type'])
        status, headers, worker = fetch('/app/sw.js')
        self.assertEqual((status, headers['Cache-Control']), (200, 'no-cache'))
        self.assertNotIn(b"'/api", worker)                                     # the service worker never caches data
        self.assertEqual(fetch('/app/manifest.webmanifest')[1]['Content-Type'], 'application/manifest+json')
        # D6.5: instalable en Android e iOS. Cada icono declarado existe, es un PNG real del tamano que dice y lo sirve el motor.
        manifest = json.loads(fetch('/app/manifest.webmanifest')[2])
        declared = {(i['sizes'], i['purpose']) for i in manifest['icons'] if i['type'] == 'image/png'}
        self.assertEqual(declared, {('192x192', 'any'), ('512x512', 'any'), ('512x512', 'maskable')})
        for icon in [i for i in manifest['icons'] if i['type'] == 'image/png'] + [dict(src='/app/apple-touch-icon.png', sizes='180x180')]:
            status, headers, data = fetch(icon['src'])
            self.assertEqual((status, headers['Content-Type'], headers['Cache-Control']), (200, 'image/png', 'no-cache'), icon['src'])
            self.assertEqual(data[:8], b'\x89PNG\r\n\x1a\n', icon['src'])
            width, height = int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')
            self.assertEqual(f'{width}x{height}', icon['sizes'], icon['src'])
        self.assertIn(b'rel="apple-touch-icon" href="/app/apple-touch-icon.png"', body)

    def test_04_only_build_files_are_public(self):
        status, headers, _ = fetch('/app')
        self.assertEqual((status, headers['Location']), (302, '/app/'))
        for path in ('/app/../appsettings.json', '/app/%2e%2e/Costina.Server.dll', '/app/nope.js', '/app/assets/', '/wwwroot/app/index.html'):
            self.assertIn(fetch(path)[0], (401, 404), path)
        status, headers, _ = fetch('/api/native/v1/session')                    # data still needs identity and is never cacheable
        self.assertEqual((status, headers['Cache-Control']), (401, 'no-store'))

    @unittest.skipUnless(os.environ.get('COSTINA_E2E') == '1', 'browser run only where Playwright is installed (CI)')
    def test_05_real_browser_pairs_survives_reload_works_offline_shell_and_honours_revocation(self):
        env = os.environ | {'E2E_BASE': h.BASE, 'E2E_MAIN_TOKEN': h.KEYS['main'], 'E2E_RUN': secrets.token_hex(3)}
        npx = 'npx.cmd' if os.name == 'nt' else 'npx'
        # UTF-8 explicito: en Windows el codec por defecto (cp1252) no decodifica la salida de Playwright y la prueba moria antes de leerla.
        done = subprocess.run([npx, 'playwright', 'test'], cwd=PWA, env=env, text=True, capture_output=True, encoding='utf-8', errors='replace')
        print(done.stdout[-6000:])
        self.assertEqual(done.returncode, 0, done.stdout[-3000:] + done.stderr[-2000:])

    def test_06_what_the_table_has_reaches_every_device_without_money(self):
        # C3b: «Lo que lleva la mesa» en el tablero: bebidas y extras ya enviados (tambien los de Caja), con nombre, formato,
        # cantidad, comensal, hora y anulado; sin importes, sin el menu y sin los platos. Pasa la guarda de dinero de la PWA.
        sid = open_table(free_table())
        status, body = h.request('/dining/services/' + sid + '/commands/add-consumptions',
            {'expectedVersion': h.dining(sid)['version'], 'lines': [{'productId': 'water', 'quantity': 1}, {'productId': 'wine', 'presentationId': 'glass', 'quantity': 2, 'guestPosition': 2}]}, role='service')
        self.assertEqual(200, status, body)
        self.assertEqual(list(money_keys(json.loads(body))), [], 'add-consumptions')
        water, wine = json.loads(body)['consumptionIds']
        checkout = h.ok('/checkout/services/' + sid + '/commands/add-product', {'expectedVersion': h.account(sid)['version'], 'productId': 'water', 'quantity': 3})
        cashier = checkout['data']['charges'][-1]['id']
        h.ok('/checkout/services/' + sid + '/commands/void-charge', {'expectedVersion': h.account(sid)['version'], 'chargeId': water, 'reason': 'equivocado'})
        for role in ('service', 'kitchen', 'main'):
            status, body = h.request('/dining/board', role=role)
            self.assertEqual(200, status, role)
            self.assertEqual(list(money_keys(json.loads(body))), [], role)
            entry = next(b for b in json.loads(body) if b['service']['id'] == sid)
            had = entry['consumptions']
            self.assertEqual([water, wine, cashier], [c['id'] for c in had], role)            # sin la linea del menu, en orden
            for c in had: self.assertEqual({'id', 'productId', 'presentationId', 'name', 'presentation', 'quantity', 'guestPosition', 'at', 'voided'}, set(c), role)
            self.assertEqual(('water', 'bottle', 'Agua mineral', 'Botella', 1, None, True), tuple(had[0][k] for k in ('productId', 'presentationId', 'name', 'presentation', 'quantity', 'guestPosition', 'voided')))
            self.assertEqual(('wine', 'glass', 'Vino de ensayo', 'Copa', 2, 2, False), tuple(had[1][k] for k in ('productId', 'presentationId', 'name', 'presentation', 'quantity', 'guestPosition', 'voided')))
            self.assertEqual((3, False), (had[2]['quantity'], had[2]['voided']))
            self.assertTrue(all(c['at'] for c in had))
        finish_and_release(sid)

    def test_07_lines_written_before_c3b_are_classified_by_their_product(self):
        # C3b: una linea guardada antes del corte no tiene origen: fuera el producto-menu y los platos que tienen su elaboracion;
        # un plato apuntado por error como consumo SI se ve, para que alguien lo note. Se simula quitando los campos nuevos.
        sid = open_table(free_table(), offer='LAB-CARTA')
        h.mutate(sid, 'add-dish', courseId='entrantes', productId='croquetas', presentationId='unit', quantity=2)
        for product in ('croquetas', 'water'):
            h.ok('/dining/services/' + sid + '/commands/add-consumption', {'expectedVersion': h.dining(sid)['version'], 'productId': product, 'quantity': 1}, role='service')
        tasting = open_table(free_table())
        h.ok('/dining/services/' + tasting + '/commands/add-consumption', {'expectedVersion': h.dining(tasting)['version'], 'productId': 'water', 'quantity': 1}, role='service')
        for service in (sid, tasting):
            h.sql("UPDATE core.accounts SET payload=jsonb_set(payload,'{charges}',(SELECT jsonb_agg(c-'origin'-'at'-'guestPosition') FROM jsonb_array_elements(payload->'charges') c)) "
                  "WHERE tenant='d6-pwa' AND service_id='" + service + "'")
        self.assertEqual('0', h.sql("SELECT count(*) FROM core.accounts a, jsonb_array_elements(a.payload->'charges') c WHERE a.tenant='d6-pwa' AND a.service_id IN ('" + sid + "','" + tasting + "') AND c ? 'origin'"))
        board = {b['service']['id']: b for b in h.ok('/dining/board', role='service')}
        self.assertEqual([('croquetas', 'Croquetas de la casa', None, None), ('water', 'Agua mineral', 'Botella', None)],
                         [(c['productId'], c['name'], c['presentation'], c['at']) for c in board[sid]['consumptions']])
        self.assertEqual(['water'], [c['productId'] for c in board[tasting]['consumptions']])
        for service in (sid, tasting): finish_and_release(service)

if __name__ == '__main__': unittest.main(verbosity=2)
