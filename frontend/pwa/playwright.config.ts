import { defineConfig, devices } from '@playwright/test'

// E2E contra el MOTOR REAL (con PostgreSQL), que arranca la suite dotnet/tests/http/pwa_http.py y nos pasa
// su direccion y un token main solo para hacer de "PC principal" por API. Aqui no se levanta ningun servidor.
// C3a: dos proyectos con las MISMAS historias: el escritorio y el comandero de bolsillo (375 x 812, tactil). Los dos son Chromium,
// el unico navegador que instala la CI; cada uno con sus mesas y sus nombres de dispositivo (e2e/support.ts, carriles).
// Con C3A_CAPTURES=<carpeta> (nunca en la CI) solo corre e2e/captures.spec.ts, que guarda alli las capturas de la documentacion; con
// C3B_CAPTURES=<carpeta>, solo e2e/captures-c3b.spec.ts (Bodega y bebidas); con C3C_CAPTURES=<carpeta>, solo e2e/captures-c3c.spec.ts (las
// dos aplicaciones y como se instalan).
const MOBILE = {
  ...devices['Pixel 7'],
  viewport: { width: 375, height: 812 }, screen: { width: 375, height: 812 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true,
}
const captures = (process.env.C3A_CAPTURES ?? '') !== ''
const capturesC3b = (process.env.C3B_CAPTURES ?? '') !== ''
const capturesC3c = (process.env.C3C_CAPTURES ?? '') !== ''
const CAPTURE_SPECS = /captures(-c3b|-c3c)?\.spec\.ts$/

export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  retries: 0,
  workers: 1,
  reporter: [['list']],
  use: { baseURL: process.env.E2E_BASE ?? 'http://127.0.0.1:5088', serviceWorkers: 'allow', trace: 'off' },
  projects: captures
    ? [{ name: 'captures', testMatch: /captures\.spec\.ts$/, use: { ...devices['Desktop Chrome'], hasTouch: true } }]
    : capturesC3b
      ? [{ name: 'captures-c3b', testMatch: /captures-c3b\.spec\.ts$/, use: { ...devices['Desktop Chrome'], hasTouch: true } }]
      : capturesC3c
        ? [{ name: 'captures-c3c', testMatch: /captures-c3c\.spec\.ts$/, use: { ...devices['Desktop Chrome'], hasTouch: true } }]
        : [
            { name: 'desktop', testIgnore: CAPTURE_SPECS, use: { ...devices['Desktop Chrome'] } },
            { name: 'mobile', testIgnore: CAPTURE_SPECS, use: MOBILE },
          ],
})
