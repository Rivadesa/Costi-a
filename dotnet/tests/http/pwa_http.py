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
            self.assertEqual(sorted(i['id'] for i in items), ['water', 'wine-bottle', 'wine-glass'])
            self.assertEqual(set(items[0]), {'id', 'name', 'presentation'})
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
        # D6.3: tampoco lo que RESPONDE una orden de sala. Una clave como 'chargeId' basta para que la guarda de la PWA
        # rechace la respuesta y la orden quede sin confirmar: la referencia del consumo viaja como consumptionId.
        status, body = h.request('/dining/services/' + sid + '/commands/add-consumption',
            dict(expectedVersion=h.dining(sid)['version'], productId='water', quantity=1), role='service')
        self.assertEqual(status, 200, body)
        self.assertEqual(set(json.loads(body)), {'version', 'consumptionId', 'productId', 'quantity'})
        self.assertEqual(list(money_keys(json.loads(body))), [], 'add-consumption')

    def test_03_pwa_is_served_anonymously_on_the_same_origin_with_strict_headers(self):
        with urllib.request.urlopen(h.BASE + '/health', timeout=5) as r: self.assertTrue(json.loads(r.read())['pwa'], 'server was built without frontend/pwa/dist')
        status, headers, body = fetch('/app/')
        self.assertEqual(status, 200)
        self.assertIn(b'<div id="app">', body)
        csp = headers['Content-Security-Policy']
        for required in ("default-src 'none'", "script-src 'self'", "style-src 'self'", "connect-src 'self' wss://127.0.0.1:5088", "frame-ancestors 'none'", "base-uri 'none'"):
            self.assertIn(required, csp)
        for forbidden in ('unsafe-inline', 'unsafe-eval', 'http:', 'https:', '*'):
            self.assertNotIn(forbidden, csp)
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
        done = subprocess.run([npx, 'playwright', 'test'], cwd=PWA, env=env, text=True, capture_output=True)
        print(done.stdout[-6000:])
        self.assertEqual(done.returncode, 0, done.stdout[-3000:] + done.stderr[-2000:])

if __name__ == '__main__': unittest.main(verbosity=2)
