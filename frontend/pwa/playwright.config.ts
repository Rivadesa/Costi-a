import { defineConfig, devices } from '@playwright/test'

// E2E contra el MOTOR REAL (con PostgreSQL), que arranca la suite dotnet/tests/http/pwa_http.py y nos pasa
// su direccion y un token main solo para hacer de "PC principal" por API. Aqui no se levanta ningun servidor.
// C3a: dos proyectos con las MISMAS historias: el escritorio y el comandero de bolsillo (375 x 812, tactil). Los dos son Chromium,
// el unico navegador que instala la CI; cada uno con sus mesas y sus nombres de dispositivo (e2e/support.ts, carriles).
// C3d (plan 2026-10-27, seccion 3): la cocina es una tablet fija en horizontal. El proyecto `tableta` (1180 x 820, tactil) corre SOLO
// las historias de cocina, que salen del escritorio y del movil: una vez por ejecucion en lugar de dos, para no alargar la CI.
// Con C3A_CAPTURES=<carpeta> (nunca en la CI) solo corre e2e/captures.spec.ts, que guarda alli las capturas de la documentacion; con
// C3B_CAPTURES=<carpeta>, solo e2e/captures-c3b.spec.ts (Bodega y bebidas); con C3C_CAPTURES=<carpeta>, solo e2e/captures-c3c.spec.ts (las
// dos aplicaciones y como se instalan); con E4E_CAPTURES=<carpeta>, solo e2e/captures-e4e.spec.ts (revisar las alergias antes de enviar);
// con C3D_CAPTURES=<carpeta>, solo e2e/captures-c3d.spec.ts (Costina Cocina: estacion, franja «Hecho» y pase).
const MOBILE = {
  ...devices['Pixel 7'],
  viewport: { width: 375, height: 812 }, screen: { width: 375, height: 812 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true,
}
// La tablet fija de una estacion o del pase, en horizontal: la de la maqueta aprobada (pestanas 3 y 4).
const TABLET = {
  ...devices['Desktop Chrome'],
  viewport: { width: 1180, height: 820 }, screen: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true,
}
const captures = (process.env.C3A_CAPTURES ?? '') !== ''
const capturesC3b = (process.env.C3B_CAPTURES ?? '') !== ''
const capturesC3c = (process.env.C3C_CAPTURES ?? '') !== ''
const capturesE4e = (process.env.E4E_CAPTURES ?? '') !== ''
const capturesC3d = (process.env.C3D_CAPTURES ?? '') !== ''
const CAPTURE_SPECS = /captures(-c3b|-c3c|-e4e|-c3d)?\.spec\.ts$/
const KITCHEN_SPECS = /kitchen\.spec\.ts$/

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
        : capturesE4e
          ? [{ name: 'captures-e4e', testMatch: /captures-e4e\.spec\.ts$/, use: { ...devices['Desktop Chrome'], hasTouch: true } }]
          : capturesC3d
            ? [{ name: 'captures-c3d', testMatch: /captures-c3d\.spec\.ts$/, use: TABLET }]
            : [
                { name: 'desktop', testIgnore: [CAPTURE_SPECS, KITCHEN_SPECS], use: { ...devices['Desktop Chrome'] } },
                { name: 'mobile', testIgnore: [CAPTURE_SPECS, KITCHEN_SPECS], use: MOBILE },
                { name: 'tableta', testMatch: KITCHEN_SPECS, use: TABLET },
              ],
})
