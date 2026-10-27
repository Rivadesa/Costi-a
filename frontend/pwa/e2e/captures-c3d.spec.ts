import { expect, test, type APIRequestContext, type Browser, type Page } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { asMain, checkScreen, command, MAIN, openTable, pairThroughTheUi, RUN, watch, type Restriction } from './support'

// C3d: las capturas de docs/native/images/c3d/ (Costina Cocina en la tablet de 1180 x 820: «Empezar turno», la estacion, la franja
// «Hecho · esperando al pase», el pase, «Por revisar antes de enviar» y el pase desplazado, con una revision en ambar y la columna
// lateral a la vista, con «Esperando al camarero») salen
// del MOTOR REAL, con la escena de la maqueta aprobada (2026-10-22, pestanas 3 y 4). Solo con C3D_CAPTURES=<carpeta> y nunca en la CI: playwright.config.ts corre
// entonces SOLO este spec. Usa las mesas M3 a M8 y necesita una base recien creada (la de packaging_http); M1 y M2 quedan libres para
// pwa_http.py:
//   C3D_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3d)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
// Solo para las capturas:
//  - Los platos de la maqueta (el laboratorio solo trae «Preparación fría» y «Preparación caliente»): tres ofertas creadas por API, como
//    el PC principal.
//  - El tiempo de cada comanda: el motor guarda la hora real del envio y aqui todo se envia en un minuto. Para que se vean los tres
//    tonos (neutro, ambar desde 15 y ambar oscuro desde 25), se ATRASAN firedAt y readyAt en la instantanea de la base con psql (el
//    entorno de local-tests.sh). El motor no cambia.
//  - Las tablets del local hablan por http sencillo (B.2), sin Wake Lock; 127.0.0.1 cuenta como seguro, asi que la tablet de la captura
//    dice que no lo es para ensenar el texto que veran: «déjala siempre encendida en los ajustes de la tablet».
const DIR = resolve(process.env.C3D_CAPTURES ?? '.')
const TABLET = { viewport: { width: 1180, height: 820 }, deviceScaleFactor: 1, hasTouch: true }

type Dish = [id: string, name: string, station: string]
type OfferCourse = [id: string, name: string, dishes: Dish[]]
// Idempotente: una segunda ejecucion sobre la misma base deja lo que ya existe.
async function offer(request: APIRequestContext, id: string, name: string, kind: string, courses: OfferCourse[]): Promise<void> {
  const accepted = async (action: string, body: Record<string, unknown>) => {
    const response = await asMain(request, `/erp/offers/commands/${action}`, body)
    expect([200, 409], `${action} ${String(body.id)} -> ${response.status()}`).toContain(response.status())
  }
  await accepted('offer-create', { id, name, kind, priceCents: 3000 })
  for (const [courseId, courseName, dishes] of courses) {
    await accepted('course-create', { offerId: id, id: courseId, name: courseName })
    for (const [dishId, dishName, stationId] of dishes) await accepted('dish-create', { offerId: id, courseId, id: dishId, name: dishName, stationId })
  }
}

type Prep = { id: string; name: string; stationId: string; state: string; reviewPending?: boolean; guestPosition?: number | null }
type CourseRead = { id: string; state: string; preparations: Prep[] }
const courseOf = async (request: APIRequestContext, serviceId: string, index: number) =>
  ((await (await asMain(request, `/dining/services/${serviceId}`)).json()) as { data: { courses: CourseRead[] } }).data.courses[index]
// Cada comensal elige su plato (menu cerrado), en orden.
async function choose(request: APIRequestContext, serviceId: string, courseId: string, dishes: string[]): Promise<void> {
  for (const [index, dishId] of dishes.entries()) await command(request, serviceId, 'choose', { courseId, guestPosition: index + 1, dishId })
}
// Lo que una estacion (o el PC) da por listo: UNA orden de grupo con las elaboraciones de esos platos (todas, sin nombres).
async function ready(request: APIRequestContext, serviceId: string, index: number, names?: string[]): Promise<void> {
  const course = await courseOf(request, serviceId, index)
  const itemIds = course.preparations.filter(p => p.state !== 'Ready' && (!names || names.includes(p.name))).map(p => p.id)
  if (itemIds.length > 0) await command(request, serviceId, 'preparations-ready', { courseId: course.id, itemIds })
}
// Un pase entero, de enviar a servir (para llegar al segundo).
async function wholeCourse(request: APIRequestContext, serviceId: string, index: number): Promise<void> {
  await command(request, serviceId, 'fire-next')
  await ready(request, serviceId, index)
  await command(request, serviceId, 'ready', { courseId: (await courseOf(request, serviceId, index)).id })
  await command(request, serviceId, 'serve', { courseId: (await courseOf(request, serviceId, index)).id })
}
// Solo en las capturas: atrasa las horas del pase en cocina (ver la cabecera).
function backdate(serviceId: string, firedMinutes: number, readyMinutes = 0): void {
  const shift = (key: string, minutes: number) => `CASE WHEN c->>'${key}' IS NULL THEN '{}'::jsonb `
    + `ELSE jsonb_build_object('${key}', to_jsonb((c->>'${key}')::timestamptz - make_interval(mins => ${minutes}))) END`
  const sql = `UPDATE dining.services SET payload = jsonb_set(payload, '{courses}', (SELECT jsonb_agg(CASE WHEN c->>'state' IN ('Fired', 'Preparing', 'Ready') `
    + `THEN c || ${shift('firedAt', firedMinutes)} || ${shift('readyAt', readyMinutes)} ELSE c END ORDER BY n) `
    + `FROM jsonb_array_elements(payload->'courses') WITH ORDINALITY AS t(c, n))) WHERE id = '${serviceId}'`
  expect(execFileSync('psql', ['-At', '-v', 'ON_ERROR_STOP=1', '-c', sql], { encoding: 'utf-8' }).trim()).toBe('UPDATE 1')
}

async function tablet(browser: Browser): Promise<Page> {
  const context = await browser.newContext({ ...TABLET, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow' })
  await context.addInitScript(() => { Object.defineProperty(window, 'isSecureContext', { configurable: true, get: () => false }) })
  return context.newPage()
}
async function shot(page: Page, file: string): Promise<void> {
  await checkScreen(page, file, true)
  await page.mouse.move(1, 1)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of Costina Cocina, station and pass, for the C3d doc', async ({ browser, request }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []

  // Los platos de la maqueta: un menu del dia (cada comensal elige), una degustacion y un menu de mar con pan emplatado en el pase.
  await offer(request, 'C3D-DIA', 'Menú do día', 'set-menu', [
    ['primeros', 'Primeros', [['croquetas', 'Croquetas de lacón', 'hot'], ['pimientos', 'Pimientos de Padrón', 'hot'], ['pulpo', 'Pulpo á feira', 'hot'],
      ['ensalada', 'Ensalada de tomate do país', 'cold']]],
    ['segundos', 'Segundos', [['raxo', 'Raxo con patatas', 'hot'], ['chuleton', 'Chuletón de vaca gallega', 'hot'], ['salpicon', 'Salpicón de marisco', 'cold']]],
  ])
  await offer(request, 'C3D-DEGUSTA', 'Degustación', 'tasting', [
    ['principal', 'Principal', [['merluza', 'Merluza á galega', 'hot']]],
    ['postre', 'Postre', [['tarta', 'Tarta de Santiago', 'cold']]],
  ])
  await offer(request, 'C3D-MAR', 'Menú do mar', 'tasting', [
    ['aperitivo', 'Aperitivo', [['zamburinas', 'Zamburiñas a la plancha', 'hot'], ['ostras', 'Ostras de Arcade', 'cold'], ['salpicon', 'Salpicón de marisco', 'cold'],
      ['pan', 'Pan de Cea', 'pase']]],
    ['segundo', 'Segundo', [['rodaballo', 'Rodaballo salvaje', 'hot']]],
  ])

  // Mesa 6, 6 personas, en «Segundos» desde hace 16 minutos (ambar): raxo, chuleton y, en la fria, salpicon.
  const m6 = await openTable(request, 'M6', 6, 'C3D-DIA')
  await choose(request, m6, 'primeros', ['croquetas', 'croquetas', 'pulpo', 'pulpo', 'ensalada', 'ensalada'])
  await wholeCourse(request, m6, 0)
  await choose(request, m6, 'segundos', ['raxo', 'raxo', 'chuleton', 'salpicon', 'salpicon', 'salpicon'])
  await command(request, m6, 'fire-next')
  backdate(m6, 16)
  // Mesa 5, 2 personas, en «Segundo» desde hace 27 (ambar oscuro con letra blanca): la mas antigua, la primera.
  const m5 = await openTable(request, 'M5', 2, 'C3D-MAR')
  await wholeCourse(request, m5, 0)
  await command(request, m5, 'fire-next')
  backdate(m5, 27)
  // Mesa 7, 2 personas, «Aperitivo» desde hace 9: la caliente ya termino (franja «Hecho»), la fria va por la mitad y el pan es del pase.
  const m7 = await openTable(request, 'M7', 2, 'C3D-MAR')
  await command(request, m7, 'fire-next')
  await ready(request, m7, 0, ['Zamburiñas a la plancha', 'Ostras de Arcade'])
  backdate(m7, 9)
  // Mesa 4, 4 personas, degustacion con una alergia grave del comensal 2 decidida ANTES de enviar: la merluza adaptada y el postre, que no
  // le afecta. En cocina desde hace 6.
  const allergy: Restriction = { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' }
  const m4 = await openTable(request, 'M4', 4, 'C3D-DEGUSTA', [allergy])
  for (const [index, decision, note] of [[0, 'adapt', 'sin almendra'], [1, 'unaffected', 'No lo lleva']] as const) {
    const course = await courseOf(request, m4, index)
    for (const preparation of course.preparations.filter(p => p.reviewPending))
      await command(request, m4, 'review-preparation', { courseId: course.id, itemId: preparation.id, decision, note })
  }
  await command(request, m4, 'fire-next')
  backdate(m4, 6)
  // Mesa 3, 4 personas, «Primeros» desde hace 2: las croquetas ya estan.
  const m3 = await openTable(request, 'M3', 4, 'C3D-DIA')
  await choose(request, m3, 'primeros', ['croquetas', 'croquetas', 'pimientos', 'pulpo'])
  await command(request, m3, 'fire-next')
  await ready(request, m3, 0, ['Croquetas de lacón'])
  backdate(m3, 2)
  // Mesa 8, 4 personas, recien abierta con una alergia grave del comensal 1: el pase la revisa antes de enviar.
  await openTable(request, 'M8', 4, 'C3D-DEGUSTA', [{ guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' }])

  // ---- La tablet de Cocina caliente: «Empezar turno», la rejilla y la franja «Hecho» ----
  const hot = await tablet(browser); await watch(hot, leaks, violations)
  await pairThroughTheUi(hot, request, `tablet-cocina-caliente-${RUN}`, 'kitchen', 'hot')
  await expect(hot.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await expect(hot.locator('.k-grid > li')).toHaveCount(4, { timeout: 15_000 })
  await expect(hot.getByTestId('kitchen-count')).toHaveText('4 comandas')
  await expect(hot.getByTestId('shift')).toContainText('déjala siempre encendida en los ajustes de la tablet')
  await shot(hot, 'cocina-empezar-turno-1180x820.png')
  await hot.getByTestId('do-shift-start').tap()
  await expect(hot.getByTestId('shift-on')).toContainText('Turno empezado')
  await shot(hot, 'cocina-estacion-1180x820.png')
  // «Listo» en la Mesa 4 (UNA orden): baja a la franja «Hecho», con su alergia escrita, junto a la Mesa 7.
  await hot.getByTestId('do-listo-M4-principal').tap()
  const done = hot.getByTestId('done-M4-principal')
  await expect(done).toContainText('Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra', { timeout: 15_000 })
  await expect(hot.locator('.k-grid > li')).toHaveCount(3)
  await expect(hot.getByTestId('done-band')).toContainText('Zamburiñas a la plancha × 2')
  await hot.getByTestId('done-band').evaluate(band => band.scrollIntoView({ block: 'end' }))
  await shot(hot, 'cocina-estacion-hecho-1180x820.png')

  // ---- La tablet del pase ----
  // La Mesa 3 termina, se valida y espera al camarero desde hace 2 minutos.
  await ready(request, m3, 0)
  await command(request, m3, 'ready', { courseId: (await courseOf(request, m3, 0)).id })
  backdate(m3, 7, 2)
  const pass = await tablet(browser); await watch(pass, leaks, violations)
  await pairThroughTheUi(pass, request, `tablet-del-pase-${RUN}`, 'kitchen', 'pase')
  await expect(pass.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await expect(pass.locator('.pcards > li')).toHaveCount(4, { timeout: 15_000 })
  await expect(pass.getByTestId('presend-M8-1')).toBeVisible()
  await expect(pass.getByTestId('do-ready-M4-principal')).toHaveText('Validar pase: listo para servir')
  await expect(pass.getByTestId('waiting-M3-primeros')).toContainText('listo hace 2 min')
  await pass.getByTestId('do-shift-start').tap()
  // Con el raton y sin foco al hacer la foto: un toque emulado deja pintado el resaltado del toque en la captura.
  await pass.getByTestId('more-M5-segundo').locator('summary').click()
  await expect(pass.getByTestId('do-mark-M5-segundo-hot')).toHaveText('Marcar Cocina caliente como lista')
  await pass.evaluate(() => { (document.activeElement as HTMLElement | null)?.blur(); window.scrollTo(0, 0) })
  await shot(pass, 'cocina-pase-1180x820.png')
  // «Por revisar antes de enviar»: el atajo «Cambiado por …» deja la nota a la vista para terminarla.
  const guest = pass.getByTestId('presend-M8-1')
  await guest.locator('[data-testid^="presend-item-"]').first().getByRole('button', { name: 'Cambiado por …' }).click()
  await pass.keyboard.type('merluza al vapor')
  await expect(guest.locator('[data-testid^="presend-item-"]').first().locator('input')).toHaveValue('Cambiado por merluza al vapor')
  await pass.evaluate(() => {
    (document.activeElement as HTMLElement | null)?.blur()
    const aside = document.querySelector('.pside')
    if (aside) window.scrollTo(0, aside.getBoundingClientRect().top + window.scrollY - 16)
  })
  await shot(pass, 'cocina-pase-por-revisar-1180x820.png')
  // Revision de C3d: el pase desplazado hasta la Mesa 4, con una intolerancia leve anotada con el pase ya enviado. Su revision va en ambar
  // (el rojo es solo de la alergia que alarma) y la columna lateral sigue a la vista, con «Esperando al camarero» al pie. (Antes era una
  // captura de la pagina entera: con la columna fija, una captura de pagina entera la pinta a medias.)
  await command(request, m4, 'declare-restriction', { guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' })
  await expect(pass.locator('[data-testid^="review-M4-principal-"]')).toHaveCount(1, { timeout: 15_000 })
  await expect(pass.getByTestId('do-ready-M4-principal')).toHaveCount(0)
  await pass.evaluate(() => {
    (document.activeElement as HTMLElement | null)?.blur()
    const box = document.querySelector('[data-testid^="review-M4-principal-"]')?.getBoundingClientRect()
    if (box) window.scrollTo(0, box.bottom + window.scrollY - window.innerHeight + 24)
  })
  await shot(pass, 'cocina-pase-desplazado-1180x820.png')

  expect(leaks, 'a kitchen device received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
