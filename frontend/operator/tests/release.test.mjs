import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { localTestSettings, normalizeRealApiBase } from '../src/utils/connectionSettings.js';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
test('frontend, Tauri and Cargo carry the same explicit release version', () => {
  const version = JSON.parse(read('../package.json')).version;
  assert.match(version, /^\d+\.\d+\.\d+$/);
  assert.equal(JSON.parse(read('../src-tauri/tauri.conf.json')).version, version);
  assert.equal(read('../src-tauri/Cargo.toml').match(/^version = "([^"]+)"/m)[1], version);
});
test('real test server selection removes standalone demo scope without mutation', () => {
  const first = localTestSettings();
  assert.deepEqual(first, { apiBase: 'http://127.0.0.1:8000/api/v1', companyId: '', locationId: '', terminalMode: 'main' });
  first.companyId = 'demo-company';
  assert.equal(localTestSettings().companyId, '');
});
for (const value of ['demo://local', 'file:///etc/passwd', 'javascript:alert(1)', 'http://user:secret@localhost/api/v1', 'https://host/api?token=secret', 'https://host/api#fragment', '']) {
  test(`reject invalid or credential-bearing real API address: ${value}`, () => assert.throws(() => normalizeRealApiBase(value)));
}
test('normalizes a real local API while preserving an explicit port/path', () => {
  assert.equal(normalizeRealApiBase(' http://127.0.0.1:8000/api/v1/ '), 'http://127.0.0.1:8000/api/v1');
  assert.equal(normalizeRealApiBase('https://restaurant.example/api/v1'), 'https://restaurant.example/api/v1');
});
