import { expect, test, type APIRequestContext, type BrowserContext } from '@playwright/test'
import {
  API, asMain, checkDevice, checkScreen, createRoomStation, databaseOf, deactivateStation, deviceContext, deviceName, finishAndRelease, kitchenValidates,
  MAIN, pairByApi, pairThroughTheUi, post, press, read, recordVibrations, revokeDevice, RUN, salaTable, storedToken, vibrations, watch,
} from './support'

// C3e (plan 2026-10-27, paso 4; plan 2026-10-22, seccion 4.4): «Mis mesas» de Costina Sala contra el MOTOR REAL, en el movil y en
// `sala-vertical` (820 x 1180). Dos moviles de sala en dos estaciones: A en «Sala 1» y B en una segunda estacion de sala creada para la
// prueba. A abre M5 desde «Mis mesas» > «Mesas libres» con una alergia, en UNA orden; la mesa es suya (la abrio el y desde su estacion)
// y no de B, que solo la ve en «Todas». El pase la revisa, A envia el pase, cocina lo valida y la banda y la vibracion salen SOLO en A
// (B la ve en verde en «Todas», con la pagina a la vista y activada, y no vibra). «Servido» desde el boton grande de la ficha es UNA orden.
// «Mis mesas» es presentacion por dispositivo y estacion, nunca permiso (ADR-018): lo que se prueba es el filtro del cliente (sala.ts,
// isMine y mineEntries), no el servidor, que anuncia serve a cualquier movil de sala (Dining/Affordances.cs).
// Usa M5 (support.ts, salaTable) y la deja libre al terminar, tambien si algo falla a medias, con la estacion nueva desactivada y B
// revocado: M1, M5 y M8 quedan libres para pwa_http 06-08.
type Prep = { id: string; state: string; reviewPending?: boolean }
type Course = { id: string; name: string; state: string; preparations: Prep[] }
type Service = { version: number; data: { state: string; openedBy?: string; openedStation?: string; courses: Course[] } }
const service = async (request: APIRequestContext, serviceId: string) => (await asMain(request, `/dining/services/${serviceId}`)).json() as Promise<Service>
// La mesa abierta en esa mesa ahora mismo, segun el tablero del PC principal (o null).
async function openOn(request: APIRequestContext, tableId: string): Promise<string | null> {
  const board = await (await asMain(request, '/dining/board')).json() as Array<{ service: { id: string; tableId: string } }>
  return board.find(entry => entry.service.tableId === tableId)?.service.id ?? null
}
// El dispositivo sin revocar con ese nombre (o null, si no llego a emparejarse).
async function deviceNamed(request: APIRequestContext, name: string): Promise<string | null> {
  const devices = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; name: string; revokedAt?: string | null }>
  return devices.find(device => device.name === name && !device.revokedAt)?.id ?? null
}

test('Sala: «Mis mesas» per device and station - A opens M5 with an allergy from the free tables in ONE command, only A gets the banner and the vibration, «Servido» is ONE command', async ({ page, request, browser }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const table = salaTable()
  const station = deviceName('sala2'), stationName = `Sala 2 ${RUN}`
  expect(station.length, 'a station code has 32 characters at most').toBeLessThanOrEqual(32)
  const nameA = deviceName('mesas-a'), nameB = deviceName('mesas-b')
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  await recordVibrations(page)
  // Cuantas ordenes manda A: abrir, enviar el pase y servir, cada una UNA vez.
  const sent = { open: 0, fire: 0, serve: 0 }
  page.on('request', outgoing => {
    if (outgoing.method() !== 'POST') return
    const path = new URL(outgoing.url()).pathname
    if (path.endsWith('/dining/services')) sent.open++
    else if (path.endsWith('/commands/fire-next')) sent.fire++
    else if (path.endsWith('/commands/serve')) sent.serve++
  })
  const zone = ((await (await asMain(request, '/configuration')).json()) as { tables: Array<{ id: string; zoneId?: string | null }> })
    .tables.find(row => row.id === table.id)?.zoneId ?? ''
  let contextB: BrowserContext | null = null
  let stationCreated = false
  let serviceId: string | null = null
  let passed = false
  try {
    // 1) Una segunda estacion de sala, como la crea el PC principal: con un codigo de esta ejecucion.
    await createRoomStation(request, station, stationName)
    stationCreated = true

    // 2) A en «Sala 1», por la pantalla. B, otro movil (otro contexto), en la estacion nueva. Los dos cuentan la vibracion. El pase, por API.
    await pairThroughTheUi(page, request, nameA, 'service', 'sala-1')
    await expect(page.getByTestId('board')).toBeVisible()
    await checkDevice(page)
    await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    contextB = await deviceContext(browser)
    const pageB = await contextB.newPage()
    await watch(pageB, leaks, violations)
    await recordVibrations(pageB)
    await pairThroughTheUi(pageB, request, nameB, 'service', station)
    await expect(pageB.getByTestId('board')).toBeVisible()
    await checkDevice(pageB)
    await expect(pageB.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    const pass = await pairByApi(request, deviceName('mesas-pase'), 'kitchen', 'pase')

    // Las dos empiezan en «Mis mesas», con la ayuda de SU estacion (el nombre, nunca el codigo) y M5 entre las libres.
    const hint = (name: string) => `Las que abriste tú y las abiertas desde los otros móviles de ${name}. Arriba, las listas para servir; las libres, al final.`
    for (const [device, name] of [[page, 'Sala 1'], [pageB, stationName]] as const) {
      await expect(device.getByTestId('board-scope-mine')).toHaveAttribute('aria-selected', 'true')
      await expect(device.getByTestId('mine-hint')).toHaveText(hint(name))
      await expect(device.getByTestId('mine-free').getByTestId(`free-room-${zone || 'none'}`).getByTestId(`table-${table.id}`).getByTestId('tile-state'))
        .toHaveText('Libre', { timeout: 15_000 })
    }
    await checkScreen(page, 'Mis mesas (A)')
    await checkScreen(pageB, 'Mis mesas (B)')

    // 3) A abre M5 desde «Mesas libres» en tres toques: la mesa, las personas y «Abrir Mesa 5», con el menu del dia ya elegido. La alergia
    //    va aparte (anotar, quien y a que), con Alergia y Grave, las de por defecto. Todo viaja en UNA orden open (E4e).
    await press(page.getByTestId('mine-free').getByTestId(`table-${table.id}`))
    const sheet = page.getByTestId('open-form')
    await expect(sheet).toContainText(`Abrir ${table.name}`)
    await expect(page.getByTestId('offer-LAB-TASTING')).toHaveAttribute('aria-checked', 'true')
    await expect(page.getByTestId('offer-LAB-TASTING')).toContainText('Menú del día')
    await press(page.getByTestId('pax-3'))
    await expect(page.getByTestId('pax-3')).toHaveAttribute('aria-checked', 'true')
    await press(page.getByTestId('open-add-restriction'))
    await expect(page.getByTestId('open-row-1')).toBeVisible()
    await press(page.getByTestId('open-guest-1-2'))
    await page.fill('input[name=open-substance-1]', 'frutos secos')
    await expect(page.getByTestId('open-guest-1-2')).toHaveAttribute('aria-checked', 'true')
    await expect(page.getByTestId('open-kind-1-Allergy')).toHaveAttribute('aria-checked', 'true')
    await expect(page.getByTestId('open-severity-1-Severe')).toHaveAttribute('aria-checked', 'true')
    //    C3e.1 (usabilidad 28-10, H06): encima de «Abrir», la alergia escrita entera y en rojo, como la dira la ficha, en vez de «1 alergia
    //    o intolerancia» al final del resumen.
    await expect(page.getByTestId('open-summary')).toHaveText('3 personas · Menú de ensayo')
    await expect(page.getByTestId('open-line')).toHaveText(['Comensal 2 · ALERGIA frutos secos (grave)'])
    await expect(page.getByTestId('open-line')).toHaveClass(/\balarm\b/)
    await expect(page.getByTestId('open-problem')).toHaveCount(0)
    await expect(page.getByTestId('do-open')).toHaveText(`Abrir ${table.name}`)
    await expect(page.getByTestId('do-open')).toBeEnabled()
    await checkScreen(page, 'open sheet with an allergy')
    //    Revision final de C3e.1 (H06): «Salir sin abrir», de verdad. Con la alergia escrita, «Mesas» pregunta; «Salir sin abrir» vuelve al
    //    plano sin mandar nada y la alergia se pierde: al tocar otra vez la misma mesa libre, la hoja empieza sin filas. Se escribe de nuevo.
    const leaveSheet = page.getByTestId('open-leave-sheet')
    const freeTile = page.getByTestId('mine-free').getByTestId(`table-${table.id}`)
    await press(sheet.getByTestId('back'))
    await expect(leaveSheet).toContainText('¿Salir sin abrir la mesa?')
    await expect(leaveSheet.getByTestId('open-leave-discard')).toHaveText('Salir sin abrir')
    await press(leaveSheet.getByTestId('open-leave-discard'))
    await expect(leaveSheet).toHaveCount(0)
    await expect(sheet).toHaveCount(0)
    await expect(freeTile.getByTestId('tile-state')).toHaveText('Libre')
    expect(sent.open).toBe(0)
    await press(freeTile)
    await expect(sheet).toContainText(`Abrir ${table.name}`)
    await expect(page.getByTestId('open-row-1')).toHaveCount(0)
    await expect(page.getByTestId('open-line')).toHaveCount(0)
    await expect(page.getByTestId('open-add-restriction')).toBeVisible()
    await press(page.getByTestId('pax-3'))
    await press(page.getByTestId('open-add-restriction'))
    await expect(page.getByTestId('open-row-1')).toBeVisible()
    await press(page.getByTestId('open-guest-1-2'))
    await page.fill('input[name=open-substance-1]', 'frutos secos')
    await expect(page.getByTestId('open-summary')).toHaveText('3 personas · Menú de ensayo')
    await expect(page.getByTestId('open-line')).toHaveText(['Comensal 2 · ALERGIA frutos secos (grave)'])
    //    C3e.1 (H06): «Mesas» con una alergia escrita ya no cierra la hoja sin aviso: pregunta, con la alergia entera, y «Seguir abriendo»
    //    (la opcion segura, con el foco) deja la hoja como estaba. No sale ninguna orden.
    await press(sheet.getByTestId('back'))
    await expect(leaveSheet).toContainText('¿Salir sin abrir la mesa?')
    await expect(leaveSheet.getByTestId('open-leave-text')).toHaveText('Se perderá 1 alergia: habrá que escribirla otra vez.')
    await expect(leaveSheet).toContainText('Comensal 2 · ALERGIA frutos secos (grave)')
    await expect(leaveSheet.getByTestId('open-leave-stay')).toBeFocused()
    await checkScreen(page, 'leaving the open sheet with an allergy')
    await press(leaveSheet.getByTestId('open-leave-stay'))
    await expect(leaveSheet).toHaveCount(0)
    await expect(sheet).toBeVisible()
    await expect(page.locator('input[name=open-substance-1]')).toHaveValue('frutos secos')
    await expect(page.getByTestId('open-guest-1-2')).toHaveAttribute('aria-checked', 'true')
    expect(sent.open).toBe(0)
    await press(page.getByTestId('do-open'))
    // C3e.1 (usabilidad 28-10, H20): confirmada, el boton dice primero lo hecho, apagado; la accion siguiente sale DONE_MS despues. Revision
    // de C3e.1: «Mesa abierta · <nombre>», con el nombre detras (el configurado puede ser masculino: «Reservado 2 abierta» no concuerda).
    await expect(page.locator('[data-testid="do-done"]:disabled')).toHaveText(`Mesa abierta · ${table.name} ✓`, { timeout: 15_000 })
    // La ficha se abre sola, Sentados y con la alergia escrita; el boton grande espera a cocina y cada plato del comensal 2 tambien.
    const detail = page.getByTestId('detail')
    await expect(detail.getByTestId('detail-state')).toHaveText('Sentados', { timeout: 15_000 })
    // Revision de C3e.1 (H20): el «✓» sigue en el boton grande de la FICHA tras abrirla (no solo en la hoja, durante la relectura).
    await expect(detail.getByTestId('do-done')).toHaveText(`Mesa abierta · ${table.name} ✓`)
    await expect(page.getByTestId('pending')).toHaveCount(0)
    await expect(detail.getByTestId('restrictions')).toContainText('Comensal 2 · ALERGIA frutos secos (grave)')
    await expect(page.getByTestId('do-wait')).toBeDisabled({ timeout: 15_000 })
    await expect(page.getByTestId('do-wait')).toHaveText('Esperando la revisión de cocina')
    serviceId = await openOn(request, table.id)
    expect(serviceId, `${table.id} is open`).not.toBeNull()
    const opened = await service(request, serviceId!)
    const waiting = opened.data.courses.flatMap(course => course.preparations.filter(p => p.reviewPending))
    expect(waiting.length, 'the guest 2 dishes wait for the kitchen').toBeGreaterThan(0)
    for (const preparation of waiting)
      await expect(detail.getByTestId(`prep-${preparation.id}`)).toContainText('Comensal 2 · ALERGIA frutos secos (grave) · Por revisar en cocina')
    await checkScreen(page, 'table card waiting for the kitchen')
    // Por API: la abrio el comandero A (el actor que da /session a SU credencial) desde «Sala 1», en UNA sola orden (version 1). No el
    // token principal, que es una persona sin estacion y haria la mesa «mia» para todos los moviles.
    const tokenA = await storedToken(page, databaseOf('sala'))
    const sessionA = await (await request.get(API + '/session', { headers: { Authorization: `Bearer ${tokenA}` } })).json() as { actor: string; station: string | null }
    expect(sessionA).toMatchObject({ actor: `device:${nameA}`, station: 'sala-1' })
    expect([opened.data.openedBy, opened.data.openedStation]).toEqual([sessionA.actor, 'sala-1'])
    expect(opened.version).toBe(1)
    expect(sent.open).toBe(1)

    //    C3e.1 (usabilidad 28-10, H03): la hoja de alergias espera el resultado de su orden. Si el servidor la RECHAZA (aqui, un choque con
    //    otra pantalla simulado en el navegador: la orden no llega al motor; la consulta por clave si, y al motor no le consta), la hoja se
    //    queda con la fila escrita y dice en rojo que NO se ha anotado. La mesa no cambia; se cierra sin anotar nada.
    await press(detail.getByTestId('restrictions-open'))
    const allergySheet = page.getByTestId('allergy-sheet')
    await expect(allergySheet.getByTestId('declare')).toBeVisible()
    await press(allergySheet.getByTestId('declare-guest-1'))
    await allergySheet.locator('input[name=substance]').fill('sésamo')
    const declareRoute = '**/commands/declare-restriction'
    await page.route(declareRoute, route => route.fulfill({
      status: 409, headers: { 'Idempotency-Key': route.request().headers()['idempotency-key'] ?? '' }, json: { error: 'version_conflict' },
    }))
    await press(allergySheet.getByTestId('do-declare-restriction'))
    await expect(allergySheet.getByTestId('declare-rejected'))
      .toHaveText('La alergia NO se ha anotado: otro puesto cambió la mesa a la vez y ya se ha vuelto a leer. Toca otra vez.', { timeout: 15_000 })
    await expect(allergySheet.getByTestId('declare-rejected')).toHaveClass(/\balarm\b/)                 // una alergia grave: en rojo
    await expect(allergySheet.locator('input[name=substance]')).toHaveValue('sésamo')
    await expect(allergySheet.getByTestId('declare-guest-1')).toHaveAttribute('aria-checked', 'true')
    await expect(allergySheet.getByTestId('do-declare-restriction')).toHaveText('Anotar alergia o intolerancia')
    await expect(page.getByTestId('pending')).toHaveCount(0)
    await checkScreen(page, 'allergy sheet after a rejected allergy')
    await page.unroute(declareRoute)
    expect((await service(request, serviceId!)).version).toBe(1)
    await press(allergySheet.getByTestId('allergy-close'))
    await expect(allergySheet).toHaveCount(0)
    await expect(detail.getByTestId('restrictions')).not.toContainText('sésamo')

    // 4) En B, M5 ya no esta entre las libres y no es suya: no sale en «Mis mesas». En «Todas», si, Sentados. B pasa a «Todas» con un
    //    toque de verdad, como el camarero, antes de que cocina valide. Ojo (revision de C3e): la activacion de la pagina que pide buzz
    //    (sala.ts) no depende de este toque. En Chromium cada page.evaluate de Playwright corre como gesto del usuario
    //    (Runtime.callFunctionOn con userGesture), asi que B ya esta activada desde su primer checkDevice. El paso 7 comprueba las dos
    //    condiciones de buzz (a la vista y activada) justo antes del 0, vengan de donde vengan.
    await expect(pageB.getByTestId('mine-hint')).toBeVisible()
    await expect(pageB.getByTestId(`table-${table.id}`)).toHaveCount(0, { timeout: 15_000 })
    await checkScreen(pageB, 'Mis mesas without the table of another station (B)')
    await press(pageB.getByTestId('board-scope-all'))
    await expect(pageB.getByTestId('board-scope-all')).toHaveAttribute('aria-selected', 'true')
    const tileB = pageB.getByTestId(`table-${table.id}`)
    await expect(tileB.getByTestId('tile-state')).toHaveText('Sentados', { timeout: 15_000 })
    await checkScreen(pageB, 'Todas (B)')

    // 5) El pase revisa con SU token: el primer plato del comensal 2 en el primer pase, «Adaptado: sin almendra»; los demas, «No le
    //    afecta». En A, la linea adaptada en rojo y el boton grande «Enviar siguiente pase · Aperitivos» (el primer pase del menu de
    //    ensayo del laboratorio, LabConfiguration.cs). Se toca: UNA orden y la version sube en uno.
    const [first] = opened.data.courses
    expect(first.name).toBe('Aperitivos')
    const adapted = first.preparations.find(p => p.reviewPending)!
    for (const course of opened.data.courses) for (const preparation of course.preparations.filter(p => p.reviewPending)) {
      const adapt = preparation.id === adapted.id
      const reviewed = await post(request, pass, `/dining/services/${serviceId}/commands/review-preparation`, { expectedVersion: (await read(request, serviceId!)).version,
        courseId: course.id, itemId: preparation.id, decision: adapt ? 'adapt' : 'unaffected', note: adapt ? 'sin almendra' : 'no lleva frutos secos' })
      expect(reviewed.ok(), `review -> ${reviewed.status()}`).toBeTruthy()
    }
    const adaptedLine = detail.getByTestId(`prep-${adapted.id}`)
    await expect(adaptedLine).toContainText('Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra', { timeout: 15_000 })
    await expect(adaptedLine).toHaveClass(/\balarm\b/)
    const fire = page.getByTestId('do-fire-next')
    await expect(fire).toHaveText(`Enviar siguiente pase · ${first.name}`, { timeout: 15_000 })
    await expect(page.getByTestId('bar-note')).toHaveText('Cada estación lo recibe en su tableta al momento.')
    await checkScreen(page, 'table card reviewed by the kitchen')
    const beforeFire = (await read(request, serviceId!)).version
    await press(fire)
    await expect(page.getByTestId('do-done')).toHaveText(`Pase enviado · ${first.name} ✓`, { timeout: 15_000 })   // C3e.1 (H20)
    await expect(detail.getByTestId('detail-state')).toHaveText('En cocina', { timeout: 15_000 })
    expect((await read(request, serviceId!)).version).toBe(beforeFire + 1)
    expect(sent.fire).toBe(1)
    // A vuelve a «Mis mesas»: con la ficha de M5 a la vista, su boton grande ya avisa y la banda no repite el aviso de ESA mesa
    // (BoardView.vue, revision de C3e).
    await press(detail.getByTestId('back'))
    const mine = page.getByTestId(`mine-room-${zone || 'none'}`)
    const tileA = mine.getByTestId(`table-${table.id}`)
    await expect(tileA.getByTestId('tile-state')).toHaveText('En cocina', { timeout: 15_000 })
    await checkScreen(page, 'Mis mesas with the table in the kitchen')

    // 6) Cocina valida: las estaciones por API como el PC y la validacion con el token del pase.
    await kitchenValidates(request, serviceId!, first.id, pass)

    // 7) La banda sale SOLO en A: «Mesa 5 · Aperitivos», una vibracion y M5 en verde en «Mis mesas».
    const banner = page.getByTestId('ready-banner')
    await expect(banner).toBeVisible({ timeout: 15_000 })
    await expect(banner.getByTestId('banner-title')).toHaveText(`${table.name} · ${first.name}`)
    await expect(page.getByTestId('banner-serve')).toHaveText(`Servido · ${first.name}`)
    //    C3e.1 (usabilidad 28-10, H19): bajo el titulo, las mismas lineas que la ficha (cada plato del comensal 2 en Aperitivos): a quien
    //    va el plato adaptado y por que, en rojo. Todas a la vista, y la banda sigue dejando sitio debajo (375 x 812 y 820 x 1180).
    const adaptedInBanner = banner.getByTestId('banner-line').filter({ hasText: 'Adaptado: sin almendra' })
    await expect(adaptedInBanner).toContainText('Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra')
    await expect(adaptedInBanner).toHaveClass(/\balarm\b/)
    await expect(banner.getByTestId('banner-line').filter({ hasText: 'No le afecta: no lleva frutos secos' }))
      .toHaveCount(first.preparations.filter(p => p.reviewPending).length - 1)
    const bannerBox = await banner.boundingBox()
    expect(bannerBox && bannerBox.height < page.viewportSize()!.height * 0.6, `the banner with its allergy lines leaves room below it (${bannerBox?.height} px)`).toBeTruthy()
    await expect.poll(() => vibrations(page)).toBe(1)
    await expect(tileA).toHaveClass(/\bready\b/)
    await expect(tileA.getByTestId('tile-state')).toHaveText('Listo para servir')
    await checkScreen(page, 'Mis mesas with the banner')
    //    B relee PRIMERO: en «Todas», M5 en verde. La banda y la vibracion salen de esa MISMA lectura (BoardView.vue, el watch de readAt),
    //    asi que con el verde ya pintado lo negativo se puede afirmar; sin esa espera pasaria sin probar nada. Antes, las dos condiciones de
    //    buzz (sala.ts): la pagina a la vista y activada. Sin ellas, el 0 de vibraciones saldria igual con el filtro roto. La activacion la
    //    da ya cualquier page.evaluate (paso 4), asi que no prueba el toque: solo que buzz no se cortaria por eso.
    await expect(tileB).toHaveClass(/\bready\b/, { timeout: 15_000 })
    await expect(tileB.getByTestId('tile-state')).toHaveText('Listo para servir')
    expect(await pageB.evaluate(() => ({ visible: document.visibilityState, active: navigator.userActivation.hasBeenActive })),
      'B is visible and activated: buzz would vibrate for a table of its own').toEqual({ visible: 'visible', active: true })
    //    Ninguna banda con «Mesa 5» y ninguna vibracion, en UNA afirmacion: con el filtro de la banda roto (mineEntries), el fallo ensena
    //    las dos cosas a la vez.
    expect({ banners: await pageB.getByTestId('ready-banner').filter({ hasText: table.name }).count(), vibrations: await vibrations(pageB) },
      'B gets neither the banner nor the vibration of a table of another station').toEqual({ banners: 0, vibrations: 0 })
    await checkScreen(pageB, 'Todas with the table of another station in green (B)')
    //    En A, «Ver mesa» abre la ficha: el boton grande es «Servido · Aperitivos», con desde cuando esta listo en el pase.
    await press(page.getByTestId('banner-view'))
    const serve = page.getByTestId(`do-serve-${first.id}`)
    await expect(serve).toHaveText(`Servido · ${first.name}`, { timeout: 15_000 })
    await expect(page.getByTestId('bar-note')).toContainText('Listo en el pase desde las ')
    await checkScreen(page, 'table card ready to serve')

    // 8) «Servido» desde el boton grande: UNA orden, la version sube en uno, el pase sale servido y la banda no vuelve. Al volver, M5 sigue
    //    en «Mis mesas» de A, en su sala.
    const beforeServe = (await read(request, serviceId!)).version
    //    C3e.1 (usabilidad 28-10, H20): «Servido · Aperitivos ✓», apagado, en el mismo sitio. Un segundo toque «por si no fue» justo
    //    DESPUES de la relectura (antes de C3e.1, el boton ya era «Enviar siguiente pase») cae en el resultado: ni otro servir ni un envio.
    const spot = await serve.boundingBox()
    let answered = false
    const reread = page.waitForResponse(response => {
      const path = new URL(response.url()).pathname
      if (response.request().method() === 'POST' && path.endsWith('/commands/serve')) answered = true
      return answered && path.endsWith('/dining/board')
    })
    await press(serve)
    await reread
    const result = page.getByTestId('do-done')
    await expect(result).toHaveText(`Servido · ${first.name} ✓`)
    const [x, y] = [spot!.x + spot!.width / 2, spot!.y + spot!.height / 2]
    await (test.info().project.use.hasTouch === true ? page.touchscreen.tap(x, y) : page.mouse.click(x, y))
    await expect(result).toHaveCount(0, { timeout: 15_000 })
    await expect.poll(async () => (await service(request, serviceId!)).data.courses[0].state, { timeout: 15_000 }).toBe('Served')
    expect((await read(request, serviceId!)).version).toBe(beforeServe + 1)
    expect(sent.serve).toBe(1)
    expect(sent.fire).toBe(1)
    await expect(detail.getByTestId('courses-served').getByTestId(`served-${first.id}`)).toContainText(first.name, { timeout: 15_000 })
    await expect(detail.getByTestId(`course-${first.id}`)).toHaveCount(0)
    await expect(page.getByTestId('pending')).toHaveCount(0)
    await checkScreen(page, 'table card served')
    await press(detail.getByTestId('back'))
    await expect(page.getByTestId('board-scope-mine')).toHaveAttribute('aria-selected', 'true')
    await expect(tileA.getByTestId('tile-state')).toHaveText('Comiendo', { timeout: 15_000 })
    await expect(banner).toHaveCount(0)
    await checkScreen(page, 'Mis mesas after serving')
    expect(await vibrations(page)).toBe(1)
    expect(await vibrations(pageB)).toBe(0)

    // 9) Ningun dato de dinero en lo que recibieron los dos moviles y ninguna violacion de la CSP.
    expect(leaks, 'the PWA received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
    passed = true
  } finally {
    // 10) Tambien si algo fallo a medias: se cierra B, M5 queda libre, la estacion nueva se desactiva y B se revoca. Cada paso se intenta
    //     aunque falle el anterior; si la prueba ya fallo, un fallo aqui no tapa el de verdad.
    const failures: unknown[] = []
    const attempt = async (step: () => Promise<unknown>) => { try { await step() } catch (error) { failures.push(error) } }
    await attempt(async () => { await contextB?.close() })
    //     Solo la mesa que abrio ESTA ejecucion (la de A: su nombre lleva la ejecucion), aunque no se llegara a apuntar su id: nunca la de
    //     otro (revision de C3e).
    await attempt(async () => {
      const open = serviceId ?? await openOn(request, table.id)
      if (open && (await service(request, open)).data.openedBy === `device:${nameA}`) await finishAndRelease(request, open)
    })
    if (stationCreated) await attempt(() => deactivateStation(request, station))
    await attempt(async () => { const id = await deviceNamed(request, nameB); if (id) await revokeDevice(request, id) })
    if (passed && failures.length > 0) throw failures[0]
  }
})
