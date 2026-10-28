import { expect, test, type APIRequestContext, type Browser, type BrowserContext, type BrowserContextOptions, type Page } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { mkdirSync } from 'node:fs'
import { join, resolve } from 'node:path'
import {
  asMain, checkScreen, command, createRoomStation, deactivateStation, finishAndRelease, MAIN, pairByApi, pairThroughTheUi, post, reviewCourse, revokeDevice,
  RUN, watch, type Restriction,
} from './support'

// C3e (plan 2026-10-27, paso 4 y «Pruebas»): las capturas de docs/native/images/c3e/ (Costina Sala en el movil de 375 x 812: «Mis mesas»,
// las libres, «Todas» con sus pestanas de sala, abrir con una alergia (desde arriba y con la fila de la alergia), la ficha con los platos
// agrupados y la linea adaptada, «Servido · <pase>» y la ficha despues, la banda en «Mis mesas» y sobre la ficha de otra mesa, y la hoja
// de alergias; y en vertical, 820 x 1180, «Mis mesas» y la ficha) salen del MOTOR REAL, con la escena de la maqueta aprobada
// (2026-10-22, pestanas 1, 2, 5 y 6). Solo con C3E_CAPTURES=<carpeta> y nunca en la CI: playwright.config.ts corre entonces SOLO este
// spec. Usa las mesas M3 a M8 y necesita una base recien creada (la de packaging_http); M1 y M2 quedan para pwa_http.py (M2 la abre su
// test_02 con el token principal, una persona sin estacion: tambien sale en «Mis mesas», como una mesa abierta en el PC por el
// responsable, ADR-018 y decision 1 del plan):
//   C3E_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3e)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http
// (con el motor ya compilado con el build actual de la PWA: npm run build y la bateria completa, o dotnet build del servidor, antes).
// Solo para las capturas:
//  - Los platos de la maqueta (el laboratorio solo trae «Preparación fría» y «Preparación caliente»): dos ofertas creadas por API, como el
//    PC principal, y la primera pasa a ser el menu del dia (offers-reorder, el orden de Menus y cartas).
//  - Una segunda estacion de sala, «Terraza», con su movil (el de Uxia): abre la Mesa 7, que sale en «Todas» y no en «Mis mesas» del
//    movil de Ana. Las demas las abren otro movil de «Sala 1» (el de Brais) o Ana desde la hoja: son de Ana (ADR-018).
//  - Dos salas: /configuration se reescribe EN EL NAVEGADOR (M6 a M8 en una «Terraza», como e2e/board.spec.ts); el motor no cambia. En
//    la misma reescritura, el menu de ensayo del laboratorio (LAB-TASTING, el de la Mesa 2 de pwa_http.py) se llama «Menú da tempada»,
//    como los de la casa (revision de C3e: «Menú de ensayo» no es de ningun restaurante).
//  - Las horas de los pases: el motor guarda la hora real y aqui todo pasa en un minuto. Para que las tarjetas no digan todas «0 min», se
//    ATRASAN firedAt, readyAt y servedAt en la instantanea de la base con psql (como captures-c3d.spec.ts). El motor no cambia.
// Al terminar, tambien si algo fallo a medias: se cierran los contextos, se terminan y liberan las mesas que abrio ESTA ejecucion (nunca
// otra que estuviera abierta en M3 a M8), se desactiva la estacion nueva y se revocan los dispositivos que emparejo (revision de C3e).
const DIR = resolve(process.env.C3E_CAPTURES ?? '.')
const PHONE: BrowserContextOptions = { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
const VERTICAL: BrowserContextOptions = { viewport: { width: 820, height: 1180 }, deviceScaleFactor: 1, hasTouch: true }
const TABLES = ['M3', 'M4', 'M5', 'M6', 'M7', 'M8']
const TERRACE = ['M6', 'M7', 'M8']
const NAMES = { ana: 'Móvil de Ana', brais: 'Móvil de Brais', uxia: 'Móvil de Uxía', vertical: 'Tableta de sala' }
const LAB_OFFER = 'LAB-TASTING', LAB_OFFER_NAME = 'Menú da tempada'

type Dish = [id: string, name: string, station: string]
type OfferCourse = [id: string, name: string, dishes: Dish[]]
// Idempotente: una segunda ejecucion sobre la misma base deja lo que ya existe (como captures-c3d.spec.ts).
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

type Prep = { id: string; name: string; state: string; reviewPending?: boolean; guestPosition?: number | null }
type CourseRead = { id: string; name: string; state: string; preparations: Prep[] }
const coursesOf = async (request: APIRequestContext, serviceId: string) =>
  ((await (await asMain(request, `/dining/services/${serviceId}`)).json()) as { data: { courses: CourseRead[] } }).data.courses
// Un movil de sala abre una mesa por API, con SU token: la mesa guarda quien la abrio y desde que estacion (E4e).
async function openAs(request: APIRequestContext, token: string, tableId: string, pax: number, offerId: string, restrictions?: Restriction[]): Promise<string> {
  const response = await post(request, token, '/dining/services', { tableId, pax, offerId, menuId: offerId, ...(restrictions ? { restrictions } : {}) })
  expect(response.ok(), `open ${tableId} -> ${response.status()}`).toBeTruthy()
  return (await response.json()).serviceId as string
}
// Lo que una estacion (o el PC) da por listo: UNA orden de grupo con todas las elaboraciones del pase que aun no lo estan.
async function stationsReady(request: APIRequestContext, serviceId: string, index: number): Promise<void> {
  const course = (await coursesOf(request, serviceId))[index]
  const itemIds = course.preparations.filter(p => p.state !== 'Ready').map(p => p.id)
  if (itemIds.length > 0) await command(request, serviceId, 'preparations-ready', { courseId: course.id, itemIds })
}
// El pase valida: listo para servir.
async function validated(request: APIRequestContext, serviceId: string, index: number): Promise<void> {
  await stationsReady(request, serviceId, index)
  await command(request, serviceId, 'ready', { courseId: (await coursesOf(request, serviceId))[index].id })
}
// Un pase entero, de enviar a servir (para llegar al siguiente).
async function wholeCourse(request: APIRequestContext, serviceId: string, index: number): Promise<void> {
  await command(request, serviceId, 'fire-next')
  await validated(request, serviceId, index)
  await command(request, serviceId, 'serve', { courseId: (await coursesOf(request, serviceId))[index].id })
}
// Solo en las capturas: atrasa las horas de cada pase (enviado, listo y servido) minutes[n] minutos en la instantanea de la base. Las tres
// se mueven juntas, asi que su orden (listo despues de enviado, servido despues de listo) no cambia.
function backdate(serviceId: string, minutes: number[]): void {
  const by = `COALESCE((ARRAY[${minutes.join(', ')}]::int[])[n::int], 0)`
  const shift = (key: string) => `CASE WHEN c->>'${key}' IS NULL THEN '{}'::jsonb `
    + `ELSE jsonb_build_object('${key}', to_jsonb((c->>'${key}')::timestamptz - make_interval(mins => ${by}))) END`
  const sql = `UPDATE dining.services SET payload = jsonb_set(payload, '{courses}', (SELECT jsonb_agg(c || ${shift('firedAt')} || ${shift('readyAt')} `
    + `|| ${shift('servedAt')} ORDER BY n) FROM jsonb_array_elements(payload->'courses') WITH ORDINALITY AS t(c, n))) WHERE id = '${serviceId}'`
  expect(execFileSync('psql', ['-At', '-v', 'ON_ERROR_STOP=1', '-c', sql], { encoding: 'utf-8' }).trim()).toBe('UPDATE 1')
}
// La mesa abierta ahora en esa mesa, segun el tablero del PC principal (o null).
async function openOn(request: APIRequestContext, tableId: string): Promise<string | null> {
  const board = await (await asMain(request, '/dining/board')).json() as Array<{ service: { id: string; tableId: string } }>
  return board.find(entry => entry.service.tableId === tableId)?.service.id ?? null
}

// Un aparato = un contexto propio (su IndexedDB), con las dos salas y el nombre del menu de ensayo reescritos EN EL NAVEGADOR antes de la
// primera lectura.
async function device(browser: Browser, use: BrowserContextOptions, contexts: BrowserContext[], leaks: string[], violations: string[]): Promise<Page> {
  const context = await browser.newContext({ ...use, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow' })
  contexts.push(context)
  const page = await context.newPage()
  await watch(page, leaks, violations)
  await page.route('**/api/native/v1/configuration', async route => {
    const response = await route.fetch()
    type Named = { id: string; name: string }
    const body = await response.json() as { tables: Array<{ id: string; zoneId?: string | null; zoneName?: string | null }>; offers?: Named[]; menus?: Named[] }
    for (const row of body.tables) if (TERRACE.includes(row.id)) { row.zoneId = 'terraza'; row.zoneName = 'Terraza' }
    for (const row of [...(body.offers ?? []), ...(body.menus ?? [])]) if (row.id === LAB_OFFER) row.name = LAB_OFFER_NAME
    await route.fulfill({ response, json: body })
  })
  return page
}
// Lo que importa a la vista: lo justo para que el fondo de `selector` quede por encima de la barra fija de abajo (si la hay), y si ya se ve,
// la pantalla desde arriba.
async function reveal(page: Page, selector: string): Promise<void> {
  await page.evaluate(found => {
    const box = document.querySelector(found)?.getBoundingClientRect()
    const bar = document.querySelector('.ficha .bar')?.getBoundingClientRect().height ?? 0
    if (!box) throw new Error(`no se ve ${found}`)
    window.scrollTo(0, Math.max(0, box.bottom + window.scrollY - (window.innerHeight - bar - 12)))
  }, selector)
}
// Con el raton y sin foco al hacer la foto: un toque emulado o el foco dejan pintado su resaltado en la captura.
async function shot(page: Page, file: string): Promise<void> {
  await checkScreen(page, file)
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
  await page.mouse.move(1, 1)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.screenshot({ path: join(DIR, file), animations: 'disabled', caret: 'hide' })
}

test('captures of Costina Sala, «Mis mesas», opening with an allergy, the table card and the ready banner, for the C3e doc', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  mkdirSync(DIR, { recursive: true })
  const leaks: string[] = [], violations: string[] = []
  const contexts: BrowserContext[] = []
  const station = `c3e-terraza-${RUN}`
  let stationCreated = false
  let passed = false
  // Revision de C3e: lo que limpia el finally es SOLO lo de esta ejecucion. Las mesas que abrio (por su id) y los dispositivos que no
  // estaban antes de empezar; nunca otra mesa abierta en M3 a M8 ni otro dispositivo con el mismo nombre.
  const opened: string[] = []
  let devicesBefore: Set<string> | null = null
  const deviceIds = async () => new Set(((await (await asMain(request, '/auth/devices')).json()) as Array<{ id: string }>).map(found => found.id))
  const own = async (...args: Parameters<typeof openAs>) => { const id = await openAs(...args); opened.push(id); return id }
  try {
    devicesBefore = await deviceIds()
    // ---- El PC principal: los menus de la maqueta, la estacion «Terraza» y dos moviles emparejados por API ----
    await offer(request, 'C3E-CASA', 'Menú da casa', 'tasting', [
      ['entrante', 'Entrante', [['empanada', 'Empanada de zamburiñas', 'cold'], ['pimientos', 'Pimientos de Padrón', 'hot']]],
      ['primero', 'Primero', [['pulpo', 'Pulpo á feira', 'hot']]],
      ['principal', 'Principal', [['merluza', 'Merluza á galega', 'hot']]],
      ['postre', 'Postre', [['tarta', 'Tarta de Santiago', 'cold']]],
    ])
    await offer(request, 'C3E-MAR', 'Menú do mar', 'tasting', [
      ['aperitivo', 'Aperitivo', [['zamburinas', 'Zamburiñas á prancha', 'hot'], ['ostras', 'Ostras de Arcade', 'cold']]],
      ['segundo', 'Segundo', [['rodaballo', 'Rodaballo salvaxe', 'hot']]],
    ])
    const reordered = await asMain(request, '/erp/offers/commands/offers-reorder', { offerIds: ['C3E-CASA', 'C3E-MAR'] })
    expect(reordered.ok(), `offers-reorder -> ${reordered.status()}`).toBeTruthy()
    await createRoomStation(request, station, 'Terraza')
    stationCreated = true
    const brais = await pairByApi(request, NAMES.brais, 'service', 'sala-1')
    const uxia = await pairByApi(request, NAMES.uxia, 'service', station)

    // ---- Las mesas ----
    // Mesa 3, 4 personas, abierta por Brais (Sala 1): el entrante servido y el primero listo en el pase desde hace 4 minutos (verde).
    const m3 = await own(request, brais, 'M3', 4, 'C3E-CASA')
    await wholeCourse(request, m3, 0)
    await command(request, m3, 'fire-next')
    await validated(request, m3, 1)
    backdate(m3, [22, 4])
    // Mesa 4, 4 personas, abierta por Brais con dos restricciones (la de la maqueta, pestana 2). El pase las revisa ANTES de enviar: la
    // merluza y la tarta del comensal 2, adaptadas; lo demas no le afecta. Entrante y primero servidos: el siguiente es «Principal».
    const allergies: Restriction[] = [
      { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' },
      { guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' }]
    const m4 = await own(request, brais, 'M4', 4, 'C3E-CASA', allergies)
    const adapted: Record<string, string> = { 'principal/2': 'sin almendra', 'postre/2': 'cambiada por filloas' }
    for (const course of await coursesOf(request, m4))
      for (const preparation of course.preparations.filter(p => p.reviewPending)) {
        const note = adapted[`${course.id}/${preparation.guestPosition}`]
        await command(request, m4, 'review-preparation', { courseId: course.id, itemId: preparation.id, decision: note ? 'adapt' : 'unaffected',
          note: note ?? (preparation.guestPosition === 3 ? 'no lleva lactosa' : 'no lleva frutos secos') })
      }
    await wholeCourse(request, m4, 0)
    await wholeCourse(request, m4, 1)
    backdate(m4, [31, 12])
    // Mesa 5, 2 personas, abierta por Brais con una alergia del comensal 1 (gluten, grave), como la mesa de las pestanas 5 y 6 de la
    // maqueta: en verde sigue con su borde rojo, y la ficha con «Servido» lleva su franja. El pase la revisa antes de enviar: las
    // zamburinas, adaptadas; lo demas no le afecta. El aperitivo en cocina desde hace 9 minutos. Luego, la banda.
    const m5 = await own(request, brais, 'M5', 2, 'C3E-MAR', [{ guestPosition: 1, kind: 'Allergy', substance: 'gluten', severity: 'Severe' }])
    let adaptedM5 = 0
    for (const course of await coursesOf(request, m5))
      for (const preparation of course.preparations.filter(p => p.reviewPending)) {
        const adapt = preparation.name === 'Zamburiñas á prancha'
        if (adapt) adaptedM5++
        await command(request, m5, 'review-preparation', { courseId: course.id, itemId: preparation.id, decision: adapt ? 'adapt' : 'unaffected',
          note: adapt ? 'sin pan rallado' : 'no lleva gluten' })
      }
    expect(adaptedM5, 'the guest 1 scallops are adapted').toBe(1)
    await command(request, m5, 'fire-next')
    backdate(m5, [9])
    // Mesa 7 (Terraza), 6 personas, abierta por Uxia desde la estacion «Terraza»: no es de Ana.
    const m7 = await own(request, uxia, 'M7', 6, 'C3E-MAR')
    await command(request, m7, 'fire-next')
    backdate(m7, [6])

    // ---- El movil de Ana, en «Sala 1» ----
    const ana = await device(browser, PHONE, contexts, leaks, violations)
    await pairThroughTheUi(ana, request, NAMES.ana, 'service', 'sala-1')
    await expect(ana.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    // La primera lectura ya trae la Mesa 3 lista: sale en la banda (sin vibrar). Ana la da por vista.
    await expect(ana.getByTestId('banner-title')).toHaveText('Mesa 3 · Primero', { timeout: 15_000 })
    await ana.getByTestId('banner-seen').click()
    await expect(ana.getByTestId('ready-banner')).toHaveCount(0)
    await expect(ana.getByTestId('board-scope-mine')).toHaveAttribute('aria-selected', 'true')
    const mineSala = ana.getByTestId('mine-room-sala')
    await expect(mineSala.getByTestId('table-M3').getByTestId('tile-state')).toHaveText('Listo para servir')
    await expect(ana.getByTestId('table-M7')).toHaveCount(0)                               // la de la Terraza: no es suya
    await expect(ana.getByTestId('free-room-terraza').getByTestId('table-M6')).toBeVisible()

    // «Abrir mesa» desde las libres: la Mesa 6, 3 personas, el menu del dia ya elegido y una alergia del comensal 2.
    await ana.getByTestId('free-room-terraza').getByTestId('table-M6').click()
    await expect(ana.getByTestId('open-form')).toContainText('Abrir Mesa 6')
    await expect(ana.getByTestId('offer-C3E-CASA')).toHaveAttribute('aria-checked', 'true')
    await expect(ana.getByTestId('offer-C3E-CASA')).toContainText('Menú del día')
    await ana.getByTestId('pax-3').click()
    await ana.getByTestId('open-add-restriction').click()
    await ana.getByTestId('open-guest-1-2').click()
    await ana.fill('input[name=open-substance-1]', 'marisco')
    await expect(ana.getByTestId('open-kind-1-Allergy')).toHaveAttribute('aria-checked', 'true')
    await expect(ana.getByTestId('open-severity-1-Severe')).toHaveAttribute('aria-checked', 'true')
    await expect(ana.getByTestId('open-summary')).toHaveText('3 personas · Menú da casa · 1 alergia o intolerancia')
    await expect(ana.getByTestId('open-problem')).toHaveCount(0)
    await expect(ana.getByTestId('do-open')).toBeEnabled()
    // Desde arriba, como abrir.png (maqueta 2026-10-15): el titulo, «¿Cuántas personas?» con el 3 elegido, el menu propuesto y el
    // principio de las alergias; la barra de abajo ya resume la alergia. Despues, la fila de la alergia entera (revision de C3e).
    await ana.evaluate(() => window.scrollTo(0, 0))
    await expect(ana.getByTestId('pax-3')).toBeInViewport()
    await shot(ana, 'sala-abrir-mesa-375x812.png')
    await reveal(ana, '[data-testid="open-row-1"]')
    await shot(ana, 'sala-abrir-mesa-alergia-375x812.png')
    await ana.getByTestId('do-open').click()
    await expect(ana.getByTestId('detail-state')).toHaveText('Sentados', { timeout: 15_000 })
    await expect(ana.getByTestId('restrictions')).toContainText('Comensal 2 · ALERGIA marisco (grave)')
    const m6 = await openOn(request, 'M6')
    expect(m6, 'M6 is open').not.toBeNull()
    opened.push(m6!)
    await ana.getByTestId('back').click()

    // «Mis mesas»: las de Sala 1 (la lista arriba, en verde) y la de la Terraza que acaba de abrir Ana; al final, las libres.
    await expect(ana.getByTestId('mine-room-terraza').getByTestId('table-M6').getByTestId('tile-alert')).toHaveText('Alergia · por revisar', { timeout: 15_000 })
    await expect(ana.getByTestId('mine-summary')).toHaveText('5 abiertas · 15 personas')
    await ana.evaluate(() => window.scrollTo(0, 0))
    await shot(ana, 'sala-mis-mesas-375x812.png')
    // Al bajar: la Terraza de Ana y las libres, con un margen debajo de la ultima.
    await ana.getByTestId('mine-free').evaluate(free => window.scrollTo(0, free.getBoundingClientRect().bottom + window.scrollY - window.innerHeight + 16))
    await shot(ana, 'sala-mis-mesas-libres-375x812.png')

    // «Todas», en la Terraza: la Mesa 7 de Uxia esta aqui y no en «Mis mesas».
    await ana.getByTestId('board-scope-all').click()
    await ana.getByTestId('room-tab-terraza').click()
    await expect(ana.getByTestId('room-tab-terraza')).toHaveAttribute('aria-pressed', 'true')
    await expect(ana.getByTestId('table-M7').getByTestId('tile-state')).toHaveText('En cocina')
    await expect(ana.getByTestId('floor-summary')).toHaveText('6 de 8 mesas ocupadas · 21 personas')
    await ana.evaluate(() => window.scrollTo(0, 0))
    await shot(ana, 'sala-todas-375x812.png')

    // La banda: cocina valida el aperitivo de la Mesa 5 (de Ana: la abrio Brais desde Sala 1).
    await ana.getByTestId('board-scope-mine').click()
    await validated(request, m5, 0)
    await expect(ana.getByTestId('banner-title')).toHaveText('Mesa 5 · Aperitivo', { timeout: 15_000 })
    await expect(ana.getByTestId('banner-serve')).toHaveText('Servido · Aperitivo')
    await expect(mineSala.getByTestId('table-M5').getByTestId('tile-state')).toHaveText('Listo para servir')
    await expect(mineSala.getByTestId('table-M5').getByTestId('tile-alert')).toBeVisible()          // en verde, con su borde rojo
    await ana.evaluate(() => window.scrollTo(0, 0))
    await shot(ana, 'sala-aviso-375x812.png')
    // En otra pantalla (maqueta, pestana 5, el movil de la izquierda): la banda de la Mesa 5 sale tambien sobre la ficha de la Mesa 4, sin
    // tapar volver ni el boton grande de abajo.
    await ana.getByTestId('table-M4').click()
    await expect(ana.getByTestId('do-fire-next')).toHaveText('Enviar siguiente pase · Principal', { timeout: 15_000 })
    await expect(ana.getByTestId('banner-title')).toHaveText('Mesa 5 · Aperitivo')
    await ana.evaluate(() => window.scrollTo(0, 0))
    await shot(ana, 'sala-aviso-otra-mesa-375x812.png')

    // «Ver mesa»: la ficha de la Mesa 5. El boton grande es «Servido · Aperitivo», con desde cuando esta listo en el pase. Se toca: UNA orden.
    await ana.getByTestId('banner-view').click()
    const serve = ana.getByTestId('do-serve-aperitivo')
    await expect(serve).toHaveText('Servido · Aperitivo', { timeout: 15_000 })
    await expect(ana.getByTestId('bar-note')).toContainText('Listo en el pase desde las ')
    await expect(ana.getByTestId('restrictions')).toContainText('Comensal 1 · ALERGIA gluten (grave)')
    await reveal(ana, '[data-testid="course-aperitivo"]')
    await shot(ana, 'sala-mesa-servido-375x812.png')
    await serve.click()
    // Despues de «Servido» (maqueta, pestana 6, el movil de la derecha): el aperitivo en la linea de servidos y el boton grande pasa a
    // «Enviar siguiente pase · Segundo».
    await expect(ana.getByTestId('courses-served').getByTestId('served-aperitivo')).toBeVisible({ timeout: 15_000 })
    await expect(ana.getByTestId('do-fire-next')).toHaveText('Enviar siguiente pase · Segundo', { timeout: 15_000 })
    await expect(ana.getByTestId('bar-note')).toContainText('Último pase servido a las ')
    await expect(ana.getByTestId('ready-banner')).toHaveCount(0)
    await reveal(ana, '[data-testid="course-segundo"]')
    await shot(ana, 'sala-mesa-servido-despues-375x812.png')
    await ana.getByTestId('back').click()

    // La ficha de la Mesa 4: los platos agrupados, la merluza adaptada en rojo y «Enviar siguiente pase · Principal».
    await ana.getByTestId('table-M4').click()
    await expect(ana.getByTestId('do-fire-next')).toHaveText('Enviar siguiente pase · Principal', { timeout: 15_000 })
    const adaptedLine = ana.getByTestId('course-principal').locator('[data-testid^="prep-"]').filter({ hasText: 'Adaptado: sin almendra' })
    await expect(adaptedLine).toContainText('Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra')
    await expect(adaptedLine).toHaveClass(/\balarm\b/)
    // El plato entero a la vista, con sus dos lineas (la adaptada y la del comensal 3), por encima de la barra del boton grande.
    await reveal(ana, '[data-testid="dish-principal-0"]')
    await shot(ana, 'sala-mesa-375x812.png')
    // La hoja de alergias, desde la franja roja: cada restriccion escrita entera, con sus platos.
    await ana.evaluate(() => window.scrollTo(0, 0))
    await ana.getByTestId('restrictions-open').click()
    await expect(ana.getByTestId('allergy-sheet')).toContainText('frutos secos')
    await shot(ana, 'sala-alergias-375x812.png')
    await ana.getByTestId('allergy-close').click()

    // ---- Costina Sala en vertical (820 x 1180), otro aparato de Sala 1 ----
    const tablet = await device(browser, VERTICAL, contexts, leaks, violations)
    await pairThroughTheUi(tablet, request, NAMES.vertical, 'service', 'sala-1')
    await expect(tablet.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    await expect(tablet.getByTestId('banner-title')).toHaveText('Mesa 3 · Primero', { timeout: 15_000 })
    await tablet.getByTestId('banner-seen').click()
    await expect(tablet.getByTestId('ready-banner')).toHaveCount(0)
    await expect(tablet.getByTestId('mine-room-terraza').getByTestId('table-M6')).toBeVisible()
    await tablet.evaluate(() => window.scrollTo(0, 0))
    await shot(tablet, 'sala-mis-mesas-820x1180.png')
    await tablet.getByTestId('table-M4').click()
    await expect(tablet.getByTestId('do-fire-next')).toHaveText('Enviar siguiente pase · Principal', { timeout: 15_000 })
    await reveal(tablet, '[data-testid="dish-principal-0"]')
    await shot(tablet, 'sala-mesa-820x1180.png')

    expect(leaks, 'the PWA received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
    passed = true
  } finally {
    // Tambien si algo fallo a medias: cada paso se intenta aunque falle el anterior; si la prueba ya fallo, un fallo aqui no tapa el de verdad.
    const failures: unknown[] = []
    const attempt = async (step: () => Promise<unknown>) => { try { await step() } catch (error) { failures.push(error) } }
    for (const context of contexts) await attempt(() => context.close())
    // Solo las mesas de esta ejecucion: las que abrio (opened) y, si fallo antes de apuntarla, la Mesa 6 que abre Ana desde la hoja. Lo
    // que el pase no reviso (la alergia de la Mesa 6) se decide antes: un pase omitido no puede quedar por revisar.
    for (const tableId of TABLES) await attempt(async () => {
      const open = await openOn(request, tableId)
      if (!open) return
      const openedBy = ((await (await asMain(request, `/dining/services/${open}`)).json()) as { data: { openedBy?: string | null } }).data.openedBy
      if (!opened.includes(open) && !(tableId === 'M6' && openedBy === `device:${NAMES.ana}`)) return
      for (const [index, course] of (await coursesOf(request, open)).entries())
        if (course.preparations.some(p => p.reviewPending)) await reviewCourse(request, open, index, 'fin de la prueba')
      await finishAndRelease(request, open)
    })
    if (stationCreated) await attempt(() => deactivateStation(request, station))
    // Solo los dispositivos emparejados en esta ejecucion (los que no estaban al empezar), por su nombre.
    if (devicesBefore) await attempt(async () => {
      const devices = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; name: string; revokedAt?: string | null }>
      for (const found of devices.filter(candidate => Object.values(NAMES).includes(candidate.name) && !candidate.revokedAt && !devicesBefore!.has(candidate.id)))
        await revokeDevice(request, found.id)
    })
    if (passed && failures.length > 0) throw failures[0]
  }
})
