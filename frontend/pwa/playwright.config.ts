import { defineConfig, devices } from '@playwright/test'

// E2E contra el MOTOR REAL (con PostgreSQL), que arranca la suite dotnet/tests/http/pwa_http.py y nos pasa
// su direccion y un token main solo para hacer de "PC principal" por API. Aqui no se levanta ningun servidor.
export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  use: { baseURL: process.env.E2E_BASE ?? 'http://127.0.0.1:5088', serviceWorkers: 'allow', trace: 'off' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
