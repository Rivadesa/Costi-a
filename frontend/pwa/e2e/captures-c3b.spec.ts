import { expect, test, type APIRequestContext, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { asMain, bottlePng, catalogCommand, checkScreen, command, ensureCategory, ensureDrink, MAIN, openTable, pairThroughTheUi, read, watch, type Drink } from './support'

// C3b: las capturas de docs/native/images/c3b/ salen del MOTOR REAL, no de la maqueta. Solo con C3B_CAPTURES=<carpeta> y nunca en la
// CI: playwright.config.ts corre entonces SOLO este spec. Monta una bodega parecida a la de la maqueta (familias bajo «Bodega y
// bebidas», copas, botellas y fotos dibujadas aqui) y la Mesa 4 con lo que lleva; necesita una base recien creada, la de packaging_http:
//   C3B_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3b)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
const DIR = resolve(process.env.C3B_CAPTURES ?? '.')

type Size = { tag: string; device: string; use: BrowserContextOptions }
// Movil 375 x 812 e iPad en vertical 820 x 1180, tactiles: se toca, sin restos de :hover del raton.
const SIZES: Size[] = [
  { tag: '375x812', device: 'movil-sala-1', use: { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  { tag: '820x1180', device: 'tablet-sala-1', use: { viewport: { width: 820, height: 1180 }, deviceScaleFactor: 1, hasTouch: true } },
]

const WINE: Record<string, [number, number, number]> = { blanco: [205, 175, 80], tinto: [122, 28, 42], rosado: [226, 132, 146], espumoso: [104, 132, 84], vermut: [150, 52, 30], cerveza: [150, 92, 32], agua: [84, 146, 196] }
const bottle = (id: string, name: string, categoryId: string, tint: [number, number, number] | null, glass: boolean, presentations = [{ id: 'botella-0-75-l', name: 'Botella 0.75 L', cents: 3000 }]): Drink =>
  ({ id, name, categoryId, presentations, ...(glass ? { glassCents: 600 } : {}), ...(tint ? { image: bottlePng(tint) } : {}) })
const DRINKS: Drink[] = [
  bottle('pazo-senorans', 'Pazo de Señoráns Albariño 2023', 'blancos-rias-baixas', WINE.blanco, true),
  bottle('do-ferreiro', 'Do Ferreiro Albariño 2022', 'blancos-rias-baixas', WINE.blanco, false),
  bottle('guitian', 'Guitián Godello 2023', 'blancos-valdeorras', WINE.blanco, true),
  bottle('guimaro', 'Guímaro Mencía 2022', 'tintos-ribeira-sacra', WINE.tinto, true),
  bottle('lalama', 'Lalama 2019', 'tintos-ribeira-sacra', null, true),
  bottle('ardanza', 'Viña Ardanza Reserva 2017', 'tintos-rioja', WINE.tinto, false),
  bottle('domaine-ile', 'Domaine de l’Ile', 'rosados-francia', WINE.rosado, true),
  bottle('gramona', 'Gramona Imperial Brut 2018', 'espumosos-corpinnat', WINE.espumoso, true),
  bottle('bota-132', 'La Bota nº 132 Vermut Rojo', 'vermut', WINE.vermut, true),
  bottle('estrella', 'Estrella Galicia', 'cervezas', WINE.cerveza, false, [{ id: 'cana', name: 'Caña', cents: 250 }, { id: 'tercio', name: 'Tercio', cents: 300 }]),
  bottle('estrella-1906', 'Estrella Galicia 1906 Reserva Especial', 'cervezas', WINE.cerveza, false, [{ id: 'tercio', name: 'Tercio', cents: 350 }]),
  bottle('cabreiroa', 'Agua Cabreiroá', 'refrescos', WINE.agua, false, [{ id: 'botella-0-5-l', name: 'Botella 0.5 L', cents: 200 }, { id: 'botella-1-l', name: 'Botella 1 L', cents: 350 }]),
  bottle('mondariz', 'Agua con gas Mondariz', 'refrescos', null, false, [{ id: 'unit', name: 'Unidad', cents: 250 }]),
  bottle('cafe-solo', 'Café solo', 'cafes', null, false, [{ id: 'unit', name: 'Unidad', cents: 150 }]),
  bottle('cafe-leche', 'Café con leche', 'cafes', null, false, [{ id: 'unit', name: 'Unidad', cents: 180 }]),
]

async function stage(request: APIRequestContext): Promise<string> {
  // Familias bajo la seccion «Bodega y bebidas» (E3.3), con sus subfamilias (la region en los vinos).
  for (const [id, name, parent] of [['tintos', 'Tintos', '_bodega'], ['tintos-ribeira-sacra', 'Ribeira Sacra', 'tintos'], ['tintos-rioja', 'Rioja', 'tintos'],
    ['blancos', 'Blancos', '_bodega'], ['blancos-rias-baixas', 'Rías Baixas', 'blancos'], ['blancos-valdeorras', 'Valdeorras', 'blancos'],
    ['rosados', 'Rosados', '_bodega'], ['rosados-francia', 'Francia', 'rosados'], ['espumosos', 'Espumosos', '_bodega'], ['espumosos-corpinnat', 'Corpinnat', 'espumosos'],
    ['vermut', 'Vermut', '_bodega'], ['cervezas', 'Cervezas', '_bodega'], ['refrescos', 'Refrescos y aguas', '_bodega'], ['cafes', 'Cafés e infusiones', '_bodega']])
    await ensureCategory(request, id, name, parent)
  for (const drink of DRINKS) await ensureDrink(request, drink)
  // El agua y el vino del laboratorio, a sus familias (las pruebas que vienen despues los siguen usando con su nombre).
  expect((await catalogCommand(request, 'products-move', { productIds: ['water'], categoryId: 'refrescos' })).ok()).toBeTruthy()
  expect((await catalogCommand(request, 'products-move', { productIds: ['wine'], categoryId: 'tintos-ribeira-sacra' })).ok()).toBeTruthy()

  // Mesa 4, como en la maqueta: 4 personas, dos restricciones, el primer pase servido y lo que lleva la mesa (con algo anulado en Caja).
  const m4 = await openTable(request, 'M4', 4)
  await command(request, m4, 'declare-restriction', { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' })
  await command(request, m4, 'declare-restriction', { guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' })
  await command(request, m4, 'start'); await command(request, m4, 'fire-next')
  const course = (await read(request, m4)).data.courses[0]
  for (const preparation of course.preparations) {
    await command(request, m4, 'preparation-start', { courseId: course.id, itemId: preparation.id })
    await command(request, m4, 'preparation-ready', { courseId: course.id, itemId: preparation.id })
  }
  await command(request, m4, 'ready', { courseId: course.id })
  await command(request, m4, 'serve', { courseId: course.id })
  const round = await asMain(request, `/dining/services/${m4}/commands/add-consumptions`, { expectedVersion: (await read(request, m4)).version, lines: [
    { productId: 'pazo-senorans', presentationId: 'botella-0-75-l', quantity: 1 }, { productId: 'cabreiroa', presentationId: 'botella-1-l', quantity: 2 },
    { productId: 'mondariz', quantity: 1 }, { productId: 'guimaro', presentationId: 'copa', quantity: 1, guestPosition: 3 }] })
  expect(round.ok(), `round -> ${round.status()}`).toBeTruthy()
  const voided = (await round.json() as { consumptionIds: string[] }).consumptionIds[2]
  const account = await (await asMain(request, `/checkout/services/${m4}`)).json() as { version: number }
  expect((await asMain(request, `/checkout/services/${m4}/commands/void-charge`, { expectedVersion: account.version, chargeId: voided, reason: 'era sin gas' })).ok()).toBeTruthy()
  return m4
}

// Un aparato instalado en la pantalla de inicio tiene almacenamiento persistente (D6.6): sin el aviso de un navegador sin instalar.
async function installed(browser: Browser, page: Page, origin: string): Promise<void> {
  const { targetInfo } = await (await page.context().newCDPSession(page)).send('Target.getTargetInfo')
  await (await browser.newBrowserCDPSession()).send('Browser.grantPermissions', { origin, browserContextId: targetInfo.browserContextId, permissions: ['durableStorage'] })
  await page.goto('/app/')
  expect(await page.evaluate(() => navigator.storage.persist())).toBe(true)
}

// Las fotos de las filas a la vista ya pintadas: el numero de fotos deja de crecer y todas estan decodificadas.
async function photosSettled(page: Page): Promise<void> {
  let last = -1
  await expect.poll(async () => {
    const [count, done] = await page.locator('[data-testid=photo]').evaluateAll(images => [images.length, images.every(image => (image as HTMLImageElement).complete && (image as HTMLImageElement).naturalWidth > 0)])
    const stable = count === last && done
    last = count as number
    return stable
  }, { timeout: 15_000, intervals: [500, 500, 500, 1000] }).toBe(true)
}

async function shot(page: Page, file: string): Promise<void> {
  await checkScreen(page, file)
  await page.mouse.move(1, 1)                                                     // sin restos de :hover del ultimo toque
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of «Bodega y bebidas» and «Lo que lleva la mesa» for the C3b doc', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []
  const baseURL = test.info().project.use.baseURL
  const origin = new URL(baseURL ?? '').origin
  await stage(request)

  for (const size of SIZES) {
    const context = await browser.newContext({ ...size.use, baseURL, serviceWorkers: 'allow' })
    const page = await context.newPage(); await watch(page, leaks, violations)
    await installed(browser, page, origin)
    await pairThroughTheUi(page, request, size.device, 'service', 'sala-1')
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })

    // Lo que lleva la mesa, en la ficha (en el movil, desplazada hasta la lista).
    await page.getByTestId('table-M4').tap()
    const had = page.getByTestId('had')
    await expect(had.locator('li')).toHaveCount(4, { timeout: 15_000 })
    if (size.tag === '375x812') await had.evaluate(element => element.scrollIntoView({ block: 'center' }))
    await shot(page, `lo-que-lleva-${size.tag}.png`)

    // Bebidas: la entrada («Por copa», con «En esta mesa») y una ronda de dos sin enviar.
    await page.evaluate(() => window.scrollTo({ top: 0 }))
    await page.getByTestId('entry-bodega').tap()
    await expect(page.getByTestId('chip-glass')).toHaveAttribute('aria-pressed', 'true', { timeout: 15_000 })
    await page.getByTestId('add-guimaro-copa').tap()
    await page.getByTestId('add-pazo-senorans-copa').tap()
    await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 2 sin enviar')
    await page.evaluate(() => window.scrollTo({ top: 0 }))                          // tocar la fila la habia traido a la vista
    await photosSettled(page)
    await shot(page, `bebidas-${size.tag}.png`)

    // Una busqueda, sin tilde: «albar».
    await page.getByTestId('drinks-search').fill('albar')
    await expect(page.getByTestId('results-count')).toBeVisible()
    await photosSettled(page)
    await shot(page, `busqueda-${size.tag}.png`)

    // La ronda abierta y la hoja de una linea: cuantas y para quien (los comensales con restriccion, con su punto y escrita).
    await page.getByTestId('search-clear').tap()
    await page.getByTestId('round-toggle').tap()
    await page.getByTestId('round-for-0').tap()
    await page.getByTestId('sheet-plus').tap()
    await expect(page.getByTestId('sheet-qty')).toHaveText('2')
    await photosSettled(page)
    await shot(page, `ronda-${size.tag}.png`)

    // De vuelta en la ficha, la ronda sin enviar sigue abajo, sobre el boton grande.
    await page.getByTestId('sheet-close').tap()
    await page.getByTestId('round-toggle').tap()
    await page.getByTestId('back').tap()
    await expect(page.getByTestId('do-fire-next')).toBeVisible()
    await page.evaluate(() => window.scrollTo({ top: 0 }))
    await shot(page, `ficha-ronda-${size.tag}.png`)
    await context.close()
  }

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
