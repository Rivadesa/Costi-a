import { expect, test, type BrowserContext, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { checkScreen, deviceIdOf, finishAndRelease, MAIN, openTable, pairThroughTheUi, revokeDevice, RUN, watch } from './support'

// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01; decision 8b): las capturas de docs/native/images/f0.2/ (Costina Sala en el
// movil de 375 x 812: la pregunta al volver a una mesa a medio abrir y la pregunta al volver a una mesa con una ronda sin enviar, las dos
// tras recargar la pagina) salen del MOTOR REAL. Solo con F02_CAPTURES=<carpeta> y nunca en la CI: playwright.config.ts corre entonces SOLO
// este spec. Usa M5 (la ronda, abierta por API) y M6 (la hoja de abrir, que no llega a abrirse) y necesita una base recien creada (la de
// packaging_http); M1 y M2 quedan para pwa_http.py:
//   F02_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/f0.2)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA). Al terminar, tambien si algo fallo a medias: se cierra el contexto, se
// termina y libera M5 y se revoca el movil que emparejo esta ejecucion.
const DIR = resolve(process.env.F02_CAPTURES ?? '.')
const PHONE: BrowserContextOptions = { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }

async function shot(page: Page, file: string): Promise<void> {
  await checkScreen(page, file)
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
  await page.mouse.move(1, 1)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of Costina Sala, the saved open sheet and the saved round, for the F0.2 doc', async ({ browser, request }) => {
  test.setTimeout(180_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []
  const name = `Movil de prueba F0.2 ${RUN}`
  let context: BrowserContext | null = null
  const m5 = await openTable(request, 'M5', 4)
  try {
    context = await browser.newContext({ ...PHONE, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow' })
    const page = await context.newPage(); await watch(page, leaks, violations)
    await pairThroughTheUi(page, request, name, 'service', 'sala-1')
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })

    // ---- La mesa a medio abrir: M6, 3 personas y una alergia escrita; una recarga; al tocar M6 otra vez, la pregunta ----
    const m6 = page.getByTestId('mine-free').getByTestId('table-M6')
    await expect(m6.getByTestId('tile-state')).toHaveText('Libre', { timeout: 15_000 })
    await m6.tap()
    await expect(page.getByTestId('open-form')).toContainText('Abrir Mesa 6')
    await page.getByTestId('pax-3').tap()
    await page.getByTestId('open-add-restriction').tap()
    await page.getByTestId('open-guest-1-2').tap()
    await page.fill('input[name=open-substance-1]', 'frutos secos')
    await expect(page.getByTestId('open-line')).toHaveText(['Comensal 2 · ALERGIA frutos secos (grave)'])
    await page.reload()
    await expect(page.getByTestId('board')).toBeVisible({ timeout: 20_000 })
    await page.getByTestId('mine-free').getByTestId('table-M6').tap()
    const openRecover = page.getByTestId('open-recover-sheet')
    await expect(openRecover).toContainText('¿Sigues abriendo esta mesa?')
    await expect(openRecover).toContainText('Comensal 2 · ALERGIA frutos secos (grave)')
    await shot(page, 'sala-mesa-a-medio-abrir-375x812.png')
    await openRecover.getByTestId('open-recover-no').tap()                                    // «Empezar de cero»: M6 no se abre
    await expect(openRecover).toHaveCount(0)
    await page.getByTestId('back').tap()
    await expect(page.getByTestId('open-form')).toHaveCount(0)

    // ---- La ronda sin enviar: M5, dos aguas en Bodega; una recarga; al tocar M5 otra vez, la pregunta ----
    const tile = page.getByTestId('table-M5')
    await expect(tile.getByTestId('tile-state')).toHaveText('Sentados', { timeout: 15_000 })
    await tile.tap()
    await page.getByTestId('entry-bodega').tap()
    await expect(page.getByTestId('panel-bodega')).toBeVisible()
    await page.getByTestId('drinks-search').fill('agua')
    await page.getByTestId('add-water-bottle').tap()
    await page.getByTestId('add-water-bottle').tap()
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 2 sin enviar')
    await page.reload()
    await expect(page.getByTestId('board')).toBeVisible({ timeout: 20_000 })
    await page.getByTestId('table-M5').tap()
    const roundRecover = page.getByTestId('round-recover-sheet')
    await expect(roundRecover).toContainText('¿Recuperas la ronda sin enviar?')
    await expect(roundRecover).toContainText('Tenías 2 sin enviar en esta mesa')
    await shot(page, 'sala-ronda-guardada-375x812.png')
    await roundRecover.getByTestId('round-recover-no').tap()                                  // «Descartarla»: nada sale al servidor
    await expect(roundRecover).toHaveCount(0)

    expect(leaks, 'the handheld received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
  } finally {
    await context?.close()
    await finishAndRelease(request, m5).catch(() => undefined)
    const device = await deviceIdOf(request, name).catch(() => null)
    if (device) await revokeDevice(request, device).catch(() => undefined)
  }
})
