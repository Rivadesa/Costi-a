import { expect, test, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { asMain, checkScreen, issueCode, MAIN, pairThroughTheUi, pendingPairing, RUN, watch } from './support'

// C3c: las capturas de docs/native/images/c3c/ (dos aplicaciones, el enrutador y los dos caminos de instalacion) salen del MOTOR REAL.
// Solo con C3C_CAPTURES=<carpeta> y nunca en la CI: playwright.config.ts corre entonces SOLO este spec. No usa mesas:
//   C3C_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3c)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
const DIR = resolve(process.env.C3C_CAPTURES ?? '.')
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'
const PHONE: BrowserContextOptions = { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
const TABLET: BrowserContextOptions = { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true }

async function device(browser: Browser, use: BrowserContextOptions, standalone?: boolean): Promise<Page> {
  const context = await browser.newContext({ ...use, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow', ...(standalone === undefined ? {} : { userAgent: IPHONE }) })
  if (standalone !== undefined) await context.addInitScript(value => { Object.defineProperty(Navigator.prototype, 'standalone', { configurable: true, get: () => value }) }, standalone)
  return context.newPage()
}
async function shot(page: Page, file: string, kitchen = false): Promise<void> {
  await checkScreen(page, file, kitchen)
  await page.mouse.move(1, 1)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of the two apps and both ways to install them, for the C3c doc', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []

  // El enrutador: sin nada guardado explica; con un QR de antes pregunta para que es el dispositivo.
  const phone = await device(browser, PHONE); await watch(phone, leaks, violations)
  await phone.goto('/app/')
  await expect(phone.getByTestId('router-none')).toBeVisible()
  await shot(phone, 'enrutador-375x812.png')
  await phone.goto(`/app/#pair=${await issueCode(request)}`)
  await expect(phone.getByTestId('router-choose')).toBeVisible()
  await shot(phone, 'enrutador-qr-375x812.png')

  // Android: el QR de «Conectar Costiña Sala» abre Costiña Sala con el codigo puesto.
  await phone.goto(`/app/sala/#pair=${await issueCode(request)}`)
  await phone.fill('input[name=deviceName]', `movil-ana-${RUN}`)
  await shot(phone, 'sala-emparejar-375x812.png')
  await pairThroughTheUi(phone, request, `movil-ana-${RUN}`, 'service', 'sala-1')
  await expect(phone.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await shot(phone, 'sala-375x812.png')

  // Un codigo aprobado para la otra aplicacion: nada guardado, baja en el servidor y que codigo pedir.
  const wrong = await device(browser, PHONE); await watch(wrong, leaks, violations)
  await wrong.goto(`/app/sala/#pair=${await issueCode(request)}`)
  await wrong.fill('input[name=deviceName]', `movil-equivocado-${RUN}`)
  await wrong.getByTestId('do-pair').tap()
  expect((await asMain(request, `/auth/pairings/${await pendingPairing(request, `movil-equivocado-${RUN}`)}/approve`, { role: 'kitchen', station: 'cold' })).ok()).toBeTruthy()
  await expect(wrong.getByTestId('pair-status')).toContainText('Este código era de Costiña Cocina', { timeout: 15_000 })
  await shot(wrong, 'sala-codigo-de-cocina-375x812.png')

  // iPhone: el QR abre Safari, que no empareja y explica como anadir el icono; desde el icono se teclea el codigo corto.
  const safari = await device(browser, PHONE, false); await watch(safari, leaks, violations)
  const code = await issueCode(request)
  await safari.goto(`/app/sala/#pair=${code}`)
  await expect(safari.getByTestId('add-to-home')).toBeVisible()
  await shot(safari, 'iphone-safari-375x812.png')
  const icon = await device(browser, PHONE, true); await watch(icon, leaks, violations)
  await icon.goto('/app/sala/')
  await icon.fill('input[name=code]', `${code.slice(0, 5)} ${code.slice(5)}`)
  await icon.fill('input[name=deviceName]', `iphone-de-bea-${RUN}`)
  await shot(icon, 'iphone-icono-375x812.png')

  // La tablet del pase: Costiña Cocina; y la misma tablet abriendo Costiña Sala por error.
  const tablet = await device(browser, TABLET); await watch(tablet, leaks, violations)
  await pairThroughTheUi(tablet, request, `tablet-del-pase-${RUN}`, 'kitchen', 'pase')
  await expect(tablet.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await shot(tablet, 'cocina-1180x820.png', true)
  await tablet.goto('/app/sala/')
  await expect(tablet.getByTestId('other-app')).toBeVisible()
  await shot(tablet, 'sala-con-credencial-de-cocina-1180x820.png')

  expect(leaks).toEqual([]); expect(violations).toEqual([])
})
