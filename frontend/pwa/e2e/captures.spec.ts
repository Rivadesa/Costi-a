import { expect, test, type APIRequestContext, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { checkScreen, command, MAIN, openTable, pairThroughTheUi, read, reviewCourse, watch } from './support'

// C3a: las capturas de docs/native/images/c3a/ salen del MOTOR REAL, no de una maqueta. Solo con C3A_CAPTURES=<carpeta> y nunca en
// la CI: playwright.config.ts corre entonces SOLO este spec. Prepara M1 y M3..M8 a su gusto (M2 la deja abierta pwa_http.py), asi que
// necesita una base recien creada, la que deja packaging_http:
//   C3A_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3a)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
const DIR = resolve(process.env.C3A_CAPTURES ?? '.')

type Size = { tag: string; device: string; use: BrowserContextOptions }
// Movil 375 x 812, iPad en vertical 820 x 1180 y en horizontal 1180 x 820. Todos tactiles: se toca, sin restos de :hover del raton.
const SIZES: Size[] = [
  { tag: '375x812', device: 'movil-sala-1', use: { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  { tag: '820x1180', device: 'tablet-sala-1', use: { viewport: { width: 820, height: 1180 }, deviceScaleFactor: 1, hasTouch: true } },
  { tag: '1180x820', device: 'tablet-sala-2', use: { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true } },
]
const KITCHEN: BrowserContextOptions = { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true }

// La cocina termina un pase por API: cada elaboracion empieza y queda lista, y el pase valida.
async function finishCourse(request: APIRequestContext, serviceId: string, index: number): Promise<string> {
  const course = (await read(request, serviceId)).data.courses[index]
  for (const preparation of course.preparations) {
    await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: preparation.id })
    await command(request, serviceId, 'preparation-ready', { courseId: course.id, itemId: preparation.id })
  }
  await command(request, serviceId, 'ready', { courseId: course.id })
  return course.id
}

// Un comedor con un poco de todo, como el Comedor del puesto en C2.
async function stage(request: APIRequestContext): Promise<void> {
  // M1: sentados con el menu con eleccion por comensal, aun sin elegir.
  await openTable(request, 'M1', 4, 'LAB-DAILY')
  // M3: en cocina, y una alergia declarada con el pase ya enviado: por revisar.
  const m3 = await openTable(request, 'M3', 4)
  await command(request, m3, 'start'); await command(request, m3, 'fire-next')
  const fired = (await read(request, m3)).data.courses[0]
  await command(request, m3, 'preparation-start', { courseId: fired.id, itemId: fired.preparations.find(p => p.stationId === 'hot')!.id })
  await command(request, m3, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  // M4: listo para servir.
  const m4 = await openTable(request, 'M4', 2)
  await command(request, m4, 'start'); await command(request, m4, 'fire-next')
  await finishCourse(request, m4, 0)
  // M5, la ficha de las capturas, como en la maqueta: dos restricciones, el primer pase servido y el siguiente por enviar.
  const m5 = await openTable(request, 'M5', 4)
  await command(request, m5, 'declare-restriction', { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' })
  await command(request, m5, 'declare-restriction', { guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' })
  await reviewCourse(request, m5, 0); await reviewCourse(request, m5, 1)               // E4e: ningun pase sale sin la decision de cocina
  await command(request, m5, 'start'); await command(request, m5, 'fire-next')
  await command(request, m5, 'serve', { courseId: await finishCourse(request, m5, 0) })
  // M6: en pausa, con su pase en cocina.
  const m6 = await openTable(request, 'M6', 2)
  await command(request, m6, 'start'); await command(request, m6, 'fire-next')
  await command(request, m6, 'pause', { reason: 'los comensales salen un momento' })
  // M8: terminado, por liberar (los dos pases omitidos). M7 queda libre.
  const m8 = await openTable(request, 'M8', 2)
  await command(request, m8, 'start')
  for (const course of (await read(request, m8)).data.courses) await command(request, m8, 'skip', { courseId: course.id, reason: 'solo toman algo' })
  await command(request, m8, 'complete')
}

// Un aparato instalado en la pantalla de inicio tiene almacenamiento persistente (D6.6): sin el aviso de un navegador sin instalar.
// Chromium no lo expone como permiso de Playwright; se concede por CDP al contexto de ESTA pagina.
async function installed(browser: Browser, page: Page, origin: string): Promise<void> {
  const { targetInfo } = await (await page.context().newCDPSession(page)).send('Target.getTargetInfo')
  await (await browser.newBrowserCDPSession()).send('Browser.grantPermissions', { origin, browserContextId: targetInfo.browserContextId, permissions: ['durableStorage'] })
  await page.goto('/app/')
  expect(await page.evaluate(() => navigator.storage.persist())).toBe(true)
}

async function shot(page: Page, file: string, kitchen = false): Promise<void> {
  await checkScreen(page, file, kitchen)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of the new handheld and kitchen screens for the C3a doc', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []
  const baseURL = test.info().project.use.baseURL
  const origin = new URL(baseURL ?? '').origin
  await stage(request)

  for (const size of SIZES) {
    const context = await browser.newContext({ ...size.use, baseURL, serviceWorkers: 'allow' })
    const page = await context.newPage(); watch(page, leaks, violations)
    await installed(browser, page, origin)
    await pairThroughTheUi(page, request, size.device, 'service', 'sala-1')
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    for (const [table, state] of [['M1', 'Sentados'], ['M3', 'En cocina'], ['M4', 'Listo para servir'], ['M5', 'Comiendo'], ['M6', 'En pausa'], ['M7', 'Libre'], ['M8', 'Terminado, por liberar']])
      await expect(page.getByTestId(`table-${table}`).getByTestId('tile-state')).toHaveText(state, { timeout: 15_000 })
    await shot(page, `mesas-${size.tag}.png`)

    await page.getByTestId('table-M5').tap()
    await expect(page.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
    await shot(page, `ficha-${size.tag}.png`)

    if (size.tag === '375x812') {
      await page.getByTestId('back').tap()
      await page.getByTestId('table-M7').tap()
      await expect(page.getByTestId('do-open')).toBeVisible()
      await shot(page, `abrir-${size.tag}.png`)
    }
    await context.close()
  }

  for (const [station, device] of [['pase', 'pantalla-pase'], ['cold', 'pantalla-fria']]) {
    const context = await browser.newContext({ ...KITCHEN, baseURL, serviceWorkers: 'allow' })
    const page = await context.newPage(); watch(page, leaks, violations)
    await installed(browser, page, origin)
    await pairThroughTheUi(page, request, device, 'kitchen', station)
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    await expect(page.locator('[data-testid^="ticket-M3-"]')).toBeVisible({ timeout: 15_000 })
    await shot(page, `cocina-${station}-1180x820.png`, true)
    await context.close()
  }

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
