"""Integration test against the packaged HTTP server, not Laravel's in-process test kernel."""
import http.client
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

BASE = 'http://127.0.0.1:8000/api/v1'
STATE = Path('/tmp/hospitality-local-test-state.json')
TABLE = '01DEMO00000000000000000010'
MENU = '01DEMO00000000000000000030'
PRODUCT = '01DEMO00000000000000000071'


def request(path, token=None, data=None, key=None):
    headers = {'Accept': 'application/json', 'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = f'Bearer {token}'
    if key:
        headers['Idempotency-Key'] = key
    req = urllib.request.Request(BASE + path, headers=headers,
                                 data=None if data is None else json.dumps(data).encode())
    with urllib.request.urlopen(req, timeout=10) as response:
        return json.load(response)


def wait_ready():
    for _ in range(60):
        try:
            request('/meta')
            return
        except (OSError, http.client.HTTPException):
            time.sleep(2)
    raise RuntimeError('Packaged HTTP API did not become ready')


def main(mode):
    wait_ready()
    token = request('/auth/login', data={'email': 'demo@hospitality.local', 'password': 'demo1234'})['access_token']
    config = request('/configuration', token)['data']
    assert len(config['tables']) == 8
    assert config['sale_items']
    if mode == 'create':
        service_id = request('/services', token, {'table_id': TABLE, 'pax': 2, 'menu_id': MENU}, 'restart-open-001')['service_id']
        result = request(f'/checkout/services/{service_id}/consumptions', token,
                         {'product_id': PRODUCT, 'quantity': 2}, 'restart-consumption-001')
        assert result['unit_price_cents'] == 400
        assert result['subtotal_cents'] == 30800
        STATE.write_text(json.dumps({'service_id': service_id}), encoding='utf-8')
    elif mode == 'verify':
        service_id = json.loads(STATE.read_text(encoding='utf-8'))['service_id']
        assert next(t for t in config['tables'] if t['id'] == TABLE)['name'] == 'Mesa persistente'
        request(f'/checkout/services/{service_id}/consumptions', token,
                {'product_id': PRODUCT, 'quantity': 2}, 'restart-consumption-001')
    else:
        raise ValueError('Expected create or verify')

    service = request(f'/services/{service_id}', token)
    assert not {'consumptions', 'payments', 'subtotal_cents', 'paid_cents', 'balance_cents'}.intersection(service)
    assert 'unit_price_cents' not in (service.get('menu') or {})
    account = request(f'/checkout/services/{service_id}', token)
    assert account['subtotal_cents'] == 30800
    assert len(account['consumptions']) == 1
    assert account['consumptions'][0]['product_id'] == PRODUCT
    assert account['consumptions'][0]['quantity'] == 2
    print(f'Packaged HTTP / persistence / financial boundary: {mode} passed')


if __name__ == '__main__':
    main(sys.argv[1])
