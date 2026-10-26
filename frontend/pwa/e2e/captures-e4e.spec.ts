import { expect, test, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { checkScreen, command, MAIN, openTable, pairThroughTheUi, RUN, watch } from './support'

// E4e: las capturas de docs/native/images/e4e/ (revisar las alergias ANTES de enviar: Costiña Sala esperando, el pase de Costiña Cocina
// con «Por revisar antes de enviar» y Sala con el plato adaptado) salen del MOTOR REAL. Solo con E4E_CAPTURES=<carpeta> y nunca en la
// CI: playwright.config.ts corre entonces SOLO este spec. Usa las mesas M3 y M4 y necesita una base recien creada (la de packaging_http);
// M1 queda libre para pwa_http.py:
//   E4E_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/e4e)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
const DIR = resolve(process.env.E4E_CAPTURES ?? '.')
const PHONE: BrowserContextOptions = { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
const TABLET: BrowserContextOptions = { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true }

async function device(browser: Browser, use: BrowserContextOptions): Promise<Page> {
  const context = await browser.newContext({ ...use, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow' })
  return context.newPage()
}
async function shot(page: Page, file: string, kitchen = false): Promise<void> {
  await checkScreen(page, file, kitchen)
  await page.mouse.move(1, 1)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of the kitchen review before sending, for the E4e doc', async ({ browser, request }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []

  // M3, en cocina (una comanda debajo de lo que el pase revisa); M4, recien abierta con dos alergias en la misma orden.
  const m3 = await openTable(request, 'M3', 2)
  await command(request, m3, 'fire-next')
  await openTable(request, 'M4', 4, 'LAB-TASTING', [
    { guestPosition: 3, kind: 'Allergy', substance: 'marisco', severity: 'Severe' },
    { guestPosition: 2, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' }])

  // Costiña Sala: la ficha de la Mesa 4 dice «Por revisar en cocina» y el boton grande espera.
  const phone = await device(browser, PHONE); await watch(phone, leaks, violations)
  await pairThroughTheUi(phone, request, `movil-ana-${RUN}`, 'service', 'sala-1')
  await expect(phone.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await phone.getByTestId('table-M4').tap()
  await expect(phone.getByTestId('do-wait')).toHaveText('Esperando la revisión de cocina', { timeout: 15_000 })
  await shot(phone, 'sala-esperando-revision-375x812.png')

  // Costiña Cocina, la tablet del pase: «Por revisar antes de enviar», por comensal, con la comanda de la Mesa 3 debajo.
  const tablet = await device(browser, TABLET); await watch(tablet, leaks, violations)
  await pairThroughTheUi(tablet, request, `tablet-del-pase-${RUN}`, 'kitchen', 'pase')
  await expect(tablet.getByTestId('presend-M4-3')).toBeVisible({ timeout: 15_000 })
  await tablet.getByTestId('presend-item-M4-p1-caliente-3').locator('input').fill('sin marisco: aceite y ajo')
  await tablet.evaluate(() => window.scrollTo(0, 0))                                      // escribir la nota baja la pagina: la captura, desde arriba
  await shot(tablet, 'cocina-por-revisar-antes-de-enviar-1180x820.png', true)
  // El pase decide: «Adaptado» un plato y «No le afecta a nada más» el resto de cada comensal (una orden por comensal).
  await tablet.getByTestId('do-presend-adapt-M4-p1-caliente-3').tap()
  await expect(tablet.getByTestId('presend-item-M4-p1-caliente-3')).toHaveCount(0, { timeout: 15_000 })
  for (const [guest, note] of [['3', 'no lleva marisco'], ['2', 'no lleva lactosa']] as const) {
    await tablet.getByTestId(`presend-rest-M4-${guest}`).locator('input').fill(note)
    await tablet.getByTestId(`do-presend-rest-M4-${guest}`).tap()
    await expect(tablet.getByTestId(`presend-M4-${guest}`)).toHaveCount(0, { timeout: 15_000 })
  }
  await expect(tablet.getByTestId('presend')).toHaveCount(0)

  // Costiña Sala: el plato adaptado, escrito, y «Enviar siguiente pase» ya en grande.
  await expect(phone.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
  await expect(phone.getByTestId('courses')).toContainText('Adaptado: sin marisco: aceite y ajo')
  await phone.getByTestId('courses').getByText('Adaptado: sin marisco: aceite y ajo').scrollIntoViewIfNeeded()
  await shot(phone, 'sala-adaptado-375x812.png')

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
