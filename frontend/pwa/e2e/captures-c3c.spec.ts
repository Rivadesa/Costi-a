import { expect, test, type APIRequestContext, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { asMain, bottlePng, checkScreen, command, ensureDrink, issueCode, MAIN, openTable, pairThroughTheUi, pendingPairing, read, RUN, watch, type Drink } from './support'

// C3c: las capturas de docs/native/images/c3c/ (dos aplicaciones, el enrutador, los dos caminos de instalacion, el aviso de «listo
// para servir» y la cabecera de Costina Cocina) salen del MOTOR REAL. Solo con C3C_CAPTURES=<carpeta> y nunca en la CI:
// playwright.config.ts corre entonces SOLO este spec. Usa las mesas M3, M4 y M5 y necesita una base recien creada (la de packaging_http);
// M1 queda libre para pwa_http.py:
//   C3C_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3c)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
const DIR = resolve(process.env.C3C_CAPTURES ?? '.')
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'
const PHONE: BrowserContextOptions = { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
const TABLET: BrowserContextOptions = { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true }
const IPAD: BrowserContextOptions = { viewport: { width: 820, height: 1180 }, deviceScaleFactor: 1, hasTouch: true }
// Tres vinos con copa y foto en «Vinos», como la bodega de la maqueta (el laboratorio no trae fotos).
const wine = (id: string, name: string, tint: [number, number, number]): Drink =>
  ({ id, name, categoryId: 'vinos', presentations: [{ id: 'botella-0-75-l', name: 'Botella 0.75 L', cents: 3000 }], glassCents: 600, image: bottlePng(tint) })
const WINES: Drink[] = [wine('c3c-pazo', 'Pazo de Señoráns Albariño 2023', [205, 175, 80]), wine('c3c-guimaro', 'Guímaro Mencía 2022', [122, 28, 42]),
  wine('c3c-guitian', 'Guitián Godello 2023', [205, 175, 80])]

// Cocina termina y valida el pase que esta en cocina (por API, como el PC principal).
async function kitchenValidates(request: APIRequestContext, serviceId: string): Promise<void> {
  const course = (await read(request, serviceId)).data.courses.find(current => current.state === 'Fired' || current.state === 'Preparing')!
  for (const preparation of course.preparations.filter(current => current.state !== 'Ready'))
    await command(request, serviceId, 'preparation-ready', { courseId: course.id, itemId: preparation.id })
  await command(request, serviceId, 'ready', { courseId: course.id })
}

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

  // El aviso de «listo para servir»: el camarero esta en «Bodega y bebidas» de la Mesa 5 (en cocina) y cocina valida la Mesa 3.
  for (const drink of WINES) await ensureDrink(request, drink)
  const m5 = await openTable(request, 'M5', 4)
  await command(request, m5, 'start'); await command(request, m5, 'fire-next')
  const m3 = await openTable(request, 'M3', 2)
  await command(request, m3, 'start'); await command(request, m3, 'fire-next')
  const m4 = await openTable(request, 'M4', 4)
  await command(request, m4, 'start')
  await command(request, m4, 'declare-restriction', { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' })
  await command(request, m4, 'fire-next')
  await expect(phone.getByTestId('table-M5').getByTestId('tile-state')).toHaveText('En cocina', { timeout: 15_000 })
  await phone.getByTestId('table-M5').tap()
  await phone.getByTestId('entry-bodega').tap()
  await expect(phone.getByTestId('chip-glass')).toHaveAttribute('aria-pressed', 'true', { timeout: 15_000 })
  await expect(phone.getByTestId('drink-c3c-pazo').getByTestId('photo')).toHaveAttribute('src', /^blob:/, { timeout: 15_000 })
  await phone.getByTestId('add-c3c-guimaro-copa').tap()
  await phone.getByTestId('add-c3c-pazo-copa').tap()
  await kitchenValidates(request, m3)
  await expect(phone.getByTestId('ready-banner')).toBeVisible({ timeout: 15_000 })
  await shot(phone, 'sala-aviso-en-bodega-375x812.png')
  // Otra mesa lista (con una alergia anotada antes de enviar: borde rojo sobre el verde) y vuelta al plano: dos mesas en verde y la
  // banda con la mas antigua arriba y «+1 más».
  await kitchenValidates(request, m4)
  await expect(phone.getByTestId('banner-more')).toBeVisible({ timeout: 15_000 })
  await phone.getByTestId('back').tap()
  await phone.getByTestId('back').tap()
  await phone.getByTestId('leave-discard').tap()
  await expect(phone.getByTestId('table-M4')).toHaveClass(/\bready\b/)
  await shot(phone, 'sala-aviso-mesas-en-verde-375x812.png')
  // La misma credencial de Sala abierta en Costiña Cocina: no monta nada y lo dice.
  await phone.goto('/app/cocina/')
  await expect(phone.getByTestId('other-app')).toBeVisible({ timeout: 15_000 })
  await shot(phone, 'cocina-con-credencial-de-sala-375x812.png')
  await phone.goto('/app/sala/')
  // La misma banda en una tableta en horizontal (o en el PC): en una fila, con las mesas en verde debajo.
  const salaTablet = await device(browser, TABLET); await watch(salaTablet, leaks, violations)
  await pairThroughTheUi(salaTablet, request, `tablet-sala-${RUN}`, 'service', 'sala-1')
  await expect(salaTablet.getByTestId('banner-more')).toBeVisible({ timeout: 15_000 })
  await shot(salaTablet, 'sala-aviso-1180x820.png')

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

  // Un iPad en vertical en Cocina caliente: la cabecera «Costiña Cocina · Cocina caliente», sin el parrafo de ayuda, y la comanda de la
  // Mesa 5, que sigue en cocina.
  const ipad = await device(browser, IPAD); await watch(ipad, leaks, violations)
  await pairThroughTheUi(ipad, request, `tablet-cocina-caliente-${RUN}`, 'kitchen', 'hot')
  await expect(ipad.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await expect(ipad.getByTestId('station-title')).toHaveText('Cocina caliente')
  await expect(ipad.getByTestId(`ticket-M5-${(await read(request, m5)).data.courses[0].id}`)).toBeVisible({ timeout: 15_000 })
  await shot(ipad, 'cocina-ipad-820x1180.png', true)

  expect(leaks).toEqual([]); expect(violations).toEqual([])
})
