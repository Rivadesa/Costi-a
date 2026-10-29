import { devices, expect, test, type APIRequestContext, type BrowserContext, type Locator, type Page } from '@playwright/test'
import {
  checkDevice, checkScreen, command, databaseOf, deviceContext, deviceName, kitchenTable, MAIN, openTable, pairThroughTheUi, post, press, read,
  release, reviewCourse, storedToken, watch,
} from './support'

// D6.4 y C3d (ADR-017; plan 2026-10-27, seccion 3; plan 2026-10-22, seccion 4.3): Costina Cocina contra el MOTOR REAL, SOLO en el
// proyecto `tableta` (1180 x 820, tactil): la cocina es una tablet fija. El camarero lleva su movil (Costina Sala, 375 x 812).
// La mesa es M8 (support.ts, kitchenTable): la de cocina que el escritorio ya no usa. Se abre al empezar y se deja LIBRE al terminar,
// tambien si la historia falla a medias, como drinks.spec con M1. Asi no hace falta crear mesas por API: la siembra del laboratorio no
// las anade a una base ya sembrada y habria que revisar las suites que cuentan mesas; y una segunda ejecucion sobre la misma base la
// encuentra libre. Un solo proyecto la usa, una vez por ejecucion.
//  0) E4e: el pase revisa ANTES de enviar, con los atajos de la nota; Sala espera, ve «Adaptado: …» y envia el pase.
//  1) La estacion fria empieza el turno, suena con la comanda nueva (y solo con ella) y ve SOLO lo suyo: «También en: Cocina caliente».
//  2) Su «Listo» es UNA orden preparations-ready con lo suyo. Si otra pantalla cambia la mesa a la vez (version_conflict), relee, lo
//     dice en la comanda y espera otro toque: nunca se reenvia sola. Con el toque, OTRA orden con la version releida, que sube en uno,
//     y la comanda baja a «Hecho · esperando al pase».
//  3) La frontera es el SERVIDOR: con el token del propio dispositivo, lo ajeno es wrong_station, tambien en la orden de grupo.
//  4) Una alergia con el pase ya enviado: la estacion la ve escrita en «Hecho», sin botones de revision.
//  5) El pase ve la mesa entera: el progreso de cada estacion («Cocina fría: lista»), que falta y «Siguiente»; revisa con nota, y
//     «Rehacer» pregunta antes de mandar nada (C3e.1).
//  6) «Marcar Cocina caliente como lista» desde «Más opciones» es UNA orden; entonces, y solo entonces, valida; «Esperando al camarero»
//     hasta que se sirve.
// Ninguna palabra de caja en lo que se ve (en cocina, ni dentro de otra palabra) ni ninguna clave de dinero en lo que llega.
const PHONE = { ...devices['Pixel 7'], viewport: { width: 375, height: 812 }, screen: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
const deviceToken = (page: Page) => storedToken(page, databaseOf('cocina'))

// Las notas que suenan en la pagina (Web Audio, sintetizado): cada tono son DOS (turno.ts, CHIME). Se pone antes de cargarla.
const countNotes = (page: Page) => page.addInitScript(() => {
  const scope = window as unknown as { costinaNotes: number }
  scope.costinaNotes = 0
  const create = AudioContext.prototype.createOscillator
  AudioContext.prototype.createOscillator = function (this: AudioContext) { scope.costinaNotes++; return create.call(this) }
})
const notes = (page: Page) => page.evaluate(() => (window as unknown as { costinaNotes: number }).costinaNotes)

// C3e.1 (usabilidad 28-10): la separacion entre dos objetivos vecinos, en horizontal o en vertical (al menos 8 px), y una decision que se
// lee en UNA linea, sin partirse ni salirse de su boton (H53: «No le afecta» se partia en dos lineas en la comanda del pase).
async function gapBetween(one: Locator, two: Locator): Promise<number> {
  const [a, b] = [await one.boundingBox(), await two.boundingBox()]
  if (!a || !b) throw new Error('a target is not painted')
  return Math.max(b.x - (a.x + a.width), a.x - (b.x + b.width), b.y - (a.y + a.height), a.y - (b.y + b.height))
}
const oneLine = (button: Locator) => button.evaluate(element => {
  const range = document.createRange(); range.selectNodeContents(element)
  const lines = new Set([...range.getClientRects()].filter(rect => rect.width > 0).map(rect => Math.round(rect.top)))
  return lines.size === 1 && element.scrollWidth <= element.clientWidth
})

// Deja la mesa libre como el PC principal, tambien a medias: lo enviado se revisa, se marca, se valida y se sirve; lo demas, release().
async function freeTable(request: APIRequestContext, serviceId: string): Promise<void> {
  for (const [index, course] of (await read(request, serviceId)).data.courses.entries()) {
    if (!['Fired', 'Preparing', 'Ready'].includes(course.state)) continue
    await reviewCourse(request, serviceId, index)
    const left = (await read(request, serviceId)).data.courses[index].preparations.filter(p => p.state !== 'Ready').map(p => p.id)
    if (left.length > 0) await command(request, serviceId, 'preparations-ready', { courseId: course.id, itemIds: left })
    if ((await read(request, serviceId)).data.courses[index].state !== 'Ready') await command(request, serviceId, 'ready', { courseId: course.id })
    await command(request, serviceId, 'serve', { courseId: course.id })
  }
  await release(request, serviceId)
}

test('a station sees and marks only its own work in ONE command, the pass reviews, marks for a station and validates, all enabled by the server alone', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const TABLE = kitchenTable()
  const leaks: string[] = [], violations: string[] = []
  const contexts: BrowserContext[] = []
  const version = async () => (await read(request, serviceId)).version

  // La mesa se abre con una intolerancia del comensal 2 en la MISMA orden: una elaboracion fria (cold) y una caliente (hot) por comensal
  // en el primer pase, y las del comensal 2, de todo el menu, por revisar.
  const serviceId = await openTable(request, TABLE.id, 2, 'LAB-TASTING', [{ guestPosition: 2, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' }])
  let told = false
  try {
    // 0) SALA (el movil del camarero): «Por revisar en cocina» y el boton grande espera; el servidor no le anuncia enviar.
    const salaContext = await browser.newContext({ ...PHONE, baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow' }); contexts.push(salaContext)
    const salaPage = await salaContext.newPage(); await watch(salaPage, leaks, violations)
    await pairThroughTheUi(salaPage, request, deviceName('sala'), 'service', 'sala-1')
    await press(salaPage.getByTestId(`table-${TABLE.id}`))
    const salaCard = salaPage.getByTestId('detail')
    await expect(salaCard.getByTestId('detail-state')).toHaveText('Sentados', { timeout: 15_000 })
    await expect(salaCard.getByTestId('courses')).toContainText('Por revisar en cocina')
    await expect(salaCard.getByTestId('courses')).toContainText('Intolerancia lactosa (leve)')
    await expect(salaPage.getByTestId('do-wait')).toHaveText('Esperando la revisión de cocina')
    await expect(salaPage.getByTestId('do-wait')).toBeDisabled()
    await expect(salaPage.getByTestId('do-fire-next')).toHaveCount(0)
    await checkScreen(salaPage, 'waiting for the kitchen review')

    //    EL PASE (su tablet): «Por revisar antes de enviar», por comensal, con sus platos de los dos pases.
    const passContext = await deviceContext(browser); contexts.push(passContext)
    const passPage = await passContext.newPage(); await watch(passPage, leaks, violations)
    await pairThroughTheUi(passPage, request, deviceName('pase'), 'kitchen', 'pase')
    await expect(passPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(passPage)
    const guest = passPage.getByTestId(`presend-${TABLE.id}-2`)
    await expect(guest).toBeVisible({ timeout: 15_000 })
    await expect(guest).toContainText(`${TABLE.name} · Comensal 2`)
    await expect(guest).toContainText('Intolerancia lactosa (leve)')
    await expect(guest.locator('[data-testid^="presend-item-"]')).toHaveCount(3)
    await expect(passPage.getByTestId(`presend-${TABLE.id}-1`)).toHaveCount(0)
    await checkScreen(passPage, 'pass before sending', true)
    //    C3e.1 (usabilidad 28-10, H56): «No le afecta a nada más» va ARRIBA, justo bajo la alergia del comensal y antes de sus platos.
    const rest = passPage.getByTestId(`presend-rest-${TABLE.id}-2`)
    const [allergyBox, restBox, firstBox] = [await guest.locator('.k-al').first().boundingBox(), await rest.boundingBox(),
      await guest.locator('[data-testid^="presend-item-"]').first().boundingBox()]
    expect(restBox!.y).toBeGreaterThanOrEqual(allergyBox!.y + allergyBox!.height)
    expect(firstBox!.y).toBeGreaterThanOrEqual(restBox!.y + restBox!.height)
    //    «Cambiado por …» rellena la nota a la vista y deja el cursor al final; solo eso aun no es una decision.
    const adapted = passPage.getByTestId(`presend-item-${TABLE.id}-p1-caliente-2`)
    const adapt = passPage.getByTestId(`do-presend-adapt-${TABLE.id}-p1-caliente-2`)
    //    Sin nota no hay decision: ni «No le afecta» ni «Adaptado» (los atajos, en cambio, se tocan siempre).
    const decisions = adapted.locator('[data-testid^="do-presend-"]')
    await expect(decisions).toHaveCount(2)
    for (const button of await decisions.all()) await expect(button).toBeDisabled()
    expect(await oneLine(passPage.getByTestId(`do-presend-unaffected-${TABLE.id}-p1-caliente-2`))).toBe(true)          // C3e.1 (H53)
    //    C3e.1 (H48): una intolerancia a la lactosa no ofrece «Sin gluten», ni en el plato ni en lo que queda; los atajos, a 8 px o mas.
    await expect(adapted.getByRole('button', { name: 'Sin gluten' })).toHaveCount(0)
    await expect(rest.getByRole('button', { name: 'Sin gluten' })).toHaveCount(0)
    expect(await gapBetween(adapted.getByRole('button', { name: 'No lo lleva' }), adapted.getByRole('button', { name: 'Cambiado por …' }))).toBeGreaterThanOrEqual(8)
    await press(adapted.getByRole('button', { name: 'Cambiado por …' }))
    await expect(adapted.locator('input')).toHaveValue('Cambiado por ')
    await expect(adapted.locator('input')).toBeFocused()
    for (const button of await decisions.all()) await expect(button).toBeDisabled()
    await passPage.keyboard.type('aceite de oliva')
    await expect(adapted.locator('input')).toHaveValue('Cambiado por aceite de oliva')
    let before = await version()
    await press(adapt)
    await expect(adapted).toHaveCount(0, { timeout: 15_000 })
    expect(await version()).toBe(before + 1)
    //    «No le afecta a nada más», con el atajo «No lo lleva»: los dos platos que quedan, en UNA orden. Revision de C3e.1 (H56): encima
    //    del boton, ENTEROS, los platos que decide (los mismos que quedan en la lista, por su nombre), y el boton los cuenta.
    //    C3e.2 (decision 1b del promotor, 29-10): en DOS pasos, como «Rehacer». El primer toque no manda nada: en su sitio sale la pregunta,
    //    con la sustancia del comensal 2 («lactosa»), «Cancelar» y «Sí, a ninguno»; «Cancelar» vuelve atras sin orden y sin borrar la
    //    nota. «Sí, a ninguno» es UNA review-preparations.
    const left = (await guest.locator('[data-testid^="presend-item-"] .rv-d b').allTextContents()).map(text => text.replace(/\s+/g, ' ').trim())
    expect(left).toHaveLength(2)
    const askRest = passPage.getByTestId(`rest-ask-${TABLE.id}-2`), restStep = passPage.getByTestId(`rest-step-${TABLE.id}-2`)
    const cancelRest = passPage.getByTestId(`rest-cancel-${TABLE.id}-2`), confirmRest = passPage.getByTestId(`do-presend-rest-${TABLE.id}-2`)
    await expect(passPage.getByTestId(`presend-rest-dishes-${TABLE.id}-2`)).toHaveText(left.join(' · '))
    await expect(askRest).toHaveText('No le afecta a ninguno de estos 2 platos')
    await expect(askRest).toBeDisabled()
    await expect(confirmRest).toHaveCount(0)                                                   // la orden solo sale de la pregunta
    const groupPosts: string[] = []
    passPage.on('request', sent => {
      if (sent.method() === 'POST' && sent.url().endsWith('/commands/review-preparations')) groupPosts.push(sent.headers()['idempotency-key'] ?? '')
    })
    await press(rest.getByRole('button', { name: 'No lo lleva' }))
    await expect(rest.locator('input')).toHaveValue('No lo lleva')
    await expect(askRest).toBeEnabled()
    await askRest.scrollIntoViewIfNeeded()                                                     // medido donde se toca: el toque ya no desplaza
    const restAskBox = await askRest.boundingBox()
    before = await version()
    await press(askRest)
    await expect(restStep).toContainText('¿Ninguno de estos 2 platos lleva lactosa?')
    await expect(askRest).toHaveCount(0)
    await expect(cancelRest).toHaveText('Cancelar')
    await expect(cancelRest).toBeFocused()
    await expect(confirmRest).toHaveText('Sí, a ninguno')
    await expect(confirmRest).toBeEnabled()
    //    La pregunta ocupa el sitio del primer toque: el segundo toque de un doble toque cae en el texto, no en un boton.
    const restQuestionBox = await restStep.locator('.k-rest-q').boundingBox()
    expect(Math.abs(restQuestionBox!.y - restAskBox!.y)).toBeLessThanOrEqual(1)
    expect(restQuestionBox!.height).toBeGreaterThanOrEqual(restAskBox!.height - 1)
    //    Revision de C3e.2: el primer paso (rest-ask-…) ya no empieza por «do-» y checkScreen no lo mide (KITCHEN_TARGETS): se mide aqui.
    expect(restAskBox!.height).toBeGreaterThanOrEqual(64)
    expect((await cancelRest.boundingBox())!.height).toBeGreaterThanOrEqual(64)
    expect((await confirmRest.boundingBox())!.height).toBeGreaterThanOrEqual(64)
    expect(await gapBetween(cancelRest, confirmRest)).toBeGreaterThanOrEqual(8)
    await checkScreen(passPage, 'pass asking whether nothing else is affected', true)
    await press(cancelRest)
    await expect(restStep).toHaveCount(0)
    await expect(askRest).toBeEnabled()
    await expect(askRest).toBeFocused()
    await expect(rest.locator('input')).toHaveValue('No lo lleva')                             // «Cancelar» no borra la nota
    expect(groupPosts).toHaveLength(0)                                                         // ni el primer toque ni «Cancelar» mandan nada
    expect(await version()).toBe(before)
    //    Revision de C3e.2: con la pregunta abierta y la nota vaciada, «Sí, a ninguno» se apaga (la orden exige nota) y «Cancelar» lleva
    //    el foco a la nota: el primer paso vuelve apagado y no lo recibiria (caeria en <body>).
    await press(askRest)
    await rest.locator('input').fill('')
    await expect(confirmRest).toBeDisabled()
    await press(cancelRest)
    await expect(askRest).toBeDisabled()
    await expect(rest.locator('input')).toBeFocused()
    await press(rest.getByRole('button', { name: 'No lo lleva' }))
    await expect(rest.locator('input')).toHaveValue('No lo lleva')
    expect(groupPosts).toHaveLength(0)
    await press(askRest)
    await press(confirmRest)
    await expect(guest).toHaveCount(0, { timeout: 15_000 })
    expect(groupPosts).toHaveLength(1)
    const reviewed = await read(request, serviceId)
    expect(reviewed.version).toBe(before + 1)
    expect(reviewed.data.courses.flatMap(current => current.preparations).filter(preparation => preparation.reviewPending)).toEqual([])
    await expect(salaCard.getByTestId('courses')).toContainText('Adaptado: Cambiado por aceite de oliva', { timeout: 15_000 })
    await expect(salaPage.getByTestId('do-fire-next')).toBeVisible()

    // 1) LA ESTACION FRIA empieza el turno ANTES de que llegue la comanda: el toque suena una vez (dos notas) y nada mas.
    const coldContext = await deviceContext(browser); contexts.push(coldContext)
    const coldPage = await coldContext.newPage(); await watch(coldPage, leaks, violations); await countNotes(coldPage)
    await pairThroughTheUi(coldPage, request, deviceName('fria'), 'kitchen', 'cold')
    await expect(coldPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(coldPage)
    await expect(coldPage.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
    await expect(coldPage.getByTestId('shift')).toContainText('Tócalo al empezar')
    expect(await notes(coldPage)).toBe(0)                                                       // sin el toque, nada suena
    await checkScreen(coldPage, 'start of shift', true)
    await press(coldPage.getByTestId('do-shift-start'))
    await expect(coldPage.getByTestId('shift-on')).toContainText('Turno empezado')
    await expect.poll(() => notes(coldPage)).toBe(2)

    //    Sala envia el primer pase (empieza y envia en UNA orden) y la comanda llega a la estacion: suena.
    before = await version()
    await press(salaPage.getByTestId('do-fire-next'))
    await expect(salaCard.getByTestId('detail-state')).toHaveText('En cocina', { timeout: 15_000 })
    expect(await version()).toBe(before + 1)
    await checkScreen(salaPage, 'adapted and sent')

    const course = (await read(request, serviceId)).data.courses[0]
    const cold = course.preparations.filter(p => p.stationId === 'cold'), hot = course.preparations.filter(p => p.stationId === 'hot')
    const ticketId = `${TABLE.id}-${course.id}`
    const coldTicket = coldPage.getByTestId(`ticket-${ticketId}`)
    await expect(coldTicket).toBeVisible({ timeout: 15_000 })
    await expect.poll(() => notes(coldPage)).toBe(4)
    //    Solo lo suyo, agrupado («Preparación fría × 2») y con la excepcion del comensal 2 escrita debajo; lo de la caliente no se pinta,
    //    se nombra. El NOMBRE de la mesa, no su codigo. Ni validar, ni revisar, ni marcar por otra estacion.
    await expect(coldTicket.locator('h3')).toHaveText(`${TABLE.name} · Aperitivos`)
    await expect(coldTicket.getByTestId('elapsed')).toHaveText(/^Lleva /)
    await expect(coldTicket.locator('[data-testid^="dish-"]')).toHaveCount(1)
    await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('Preparación fría')
    await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('× 2')
    await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('Comensal 2 · Intolerancia lactosa (leve) · No le afecta: No lo lleva')
    await expect(coldTicket).not.toContainText('Preparación caliente')
    await expect(coldPage.getByTestId(`also-${ticketId}`)).toHaveText('También en: Cocina caliente')
    await expect(coldTicket.locator('[data-testid^="do-listo-"]')).toHaveCount(1)              // un solo plato: solo el «Listo» grande
    await expect(coldPage.locator(`[data-testid="do-ready-${ticketId}"], [data-testid^="do-mark-"], [data-testid^="review-"], [data-testid^="progress-"], [data-testid="presend"]`)).toHaveCount(0)
    await checkScreen(coldPage, 'cold station', true)

    // 2) VERSION_CONFLICT: la caliente empieza su plato justo cuando la fria toca «Listo». Nada se aplica, la comanda lo dice y NO se
    //    reenvia sola; el siguiente toque es OTRA orden (otra clave) con la version releida, que sube en uno.
    const readyPosts: Array<{ key: string; body: { expectedVersion: number; courseId: string; itemIds: string[] } }> = []
    coldPage.on('request', sent => {
      if (sent.method() === 'POST' && sent.url().endsWith('/commands/preparations-ready')) readyPosts.push({ key: sent.headers()['idempotency-key'] ?? '', body: sent.postDataJSON() })
    })
    await coldPage.route('**/commands/preparations-ready', async route => {
      await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: hot[0].id }); await route.continue()
    }, { times: 1 })
    before = await version()
    await press(coldPage.getByTestId(`do-listo-${ticketId}`))
    await expect(coldPage.getByTestId(`conflict-${ticketId}`)).toContainText('toca otra vez', { timeout: 15_000 })
    await expect(coldTicket).toBeVisible()
    await expect(coldPage.getByTestId('pending')).toHaveCount(0)                               // un rechazo no es una orden incierta
    expect((await read(request, serviceId)).data.courses[0].preparations.filter(p => p.stationId === 'cold').map(p => p.state)).toEqual(['Fired', 'Fired'])
    expect(await version()).toBe(before + 1)                                                   // solo la de la caliente
    await checkScreen(coldPage, 'version conflict', true)
    expect(readyPosts).toHaveLength(1)                                                         // nada se reenvia sin otro toque
    before = await version()
    await press(coldPage.getByTestId(`do-listo-${ticketId}`))
    const coldDone = coldPage.getByTestId(`done-${ticketId}`)
    await expect(coldDone).toBeVisible({ timeout: 15_000 })
    await expect(coldTicket).toHaveCount(0)
    await expect(coldPage.getByTestId(`conflict-${ticketId}`)).toHaveCount(0)
    await expect(coldPage.getByTestId('pending')).toHaveCount(0)
    expect(await version()).toBe(before + 1)                                                   // UNA orden para las dos elaboraciones
    expect(readyPosts).toHaveLength(2)
    expect(readyPosts[1].key).not.toBe(readyPosts[0].key)
    expect(readyPosts[1].body.expectedVersion).toBe(before)
    for (const sent of readyPosts) expect([...sent.body.itemIds].sort()).toEqual(cold.map(p => p.id).sort())
    const marked = (await read(request, serviceId)).data.courses[0].preparations
    expect(marked.filter(p => p.stationId === 'cold').map(p => p.state)).toEqual(['Ready', 'Ready'])
    expect(marked.filter(p => p.stationId === 'hot').map(p => p.state)).toEqual(['Preparing', 'Fired'])
    await expect(coldDone).toContainText(`${TABLE.name} · Aperitivos`)
    expect(await notes(coldPage)).toBe(4)                                                       // lo que baja a «Hecho» no suena

    // 3) La frontera es el SERVIDOR, no la pantalla: con el token de este dispositivo, lo de otra estacion es un rechazo wrong_station,
    //    tambien en la orden de grupo, y validar es solo del pase.
    const token = await deviceToken(coldPage)
    expect(token.startsWith('dev.')).toBeTruthy()
    const path = `/dining/services/${serviceId}/commands/`
    for (const [action, fields] of [['preparation-start', { itemId: hot[1].id }], ['preparations-ready', { itemIds: hot.map(p => p.id) }], ['ready', {}]] as const) {
      const refused = await post(request, token, path + action, { expectedVersion: await version(), courseId: course.id, ...fields })
      expect([action, refused.status(), (await refused.json()).error]).toEqual([action, 409, 'wrong_station'])
    }

    // 4) Sala declara una alergia grave con el pase enviado: la estacion la ve ESCRITA (tipo, sustancia, gravedad), tambien en la franja
    //    «Hecho», y sin botones de revision. C3e.1 (usabilidad 28-10, H34): en «Hecho» el plato ya esta Listo, asi que la linea NO dice
    //    «no lo emplates hasta que decida» (eso es de la rejilla, de lo que aun hay que hacer).
    await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
    await expect(coldDone).toContainText('Comensal 1 · ALERGIA marisco (grave) · Por revisar en el pase', { timeout: 15_000 })
    await expect(coldDone).not.toContainText('no lo emplates')
    await expect(coldPage.locator('[data-testid^="review-"]')).toHaveCount(0)                  // en NINGUNA mesa: el servidor no se lo anuncia

    // 5) EL PASE: la mesa entera, con el progreso de cada estacion por su nombre, que falta y lo que viene (sin enviar). Lo enviado por
    //    revisar sigue en su comanda; lo del pase sin enviar, arriba.
    const passTicket = passPage.getByTestId(`ticket-${ticketId}`)
    await expect(passTicket).toBeVisible({ timeout: 15_000 })
    await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina fría: lista')
    await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina caliente: 0 de 1 plato')
    await expect(passTicket).toContainText('Preparación caliente')
    await expect(passPage.getByTestId(`missing-${ticketId}`)).toHaveText('Falta Cocina caliente y revisar la alergia')
    await expect(passPage.getByTestId(`next-${ticketId}`)).toHaveText('Siguiente: Segundo pase (sin enviar)')
    await expect(passPage.getByTestId(`do-ready-${ticketId}`)).toHaveCount(0)                  // con revisiones pendientes el servidor no anuncia validar
    await expect(passPage.getByTestId(`presend-${TABLE.id}-1`).locator('[data-testid^="presend-item-"]')).toHaveCount(1)
    const more = passPage.getByTestId(`more-${ticketId}`), mark = passPage.getByTestId(`do-mark-${ticketId}-hot`)
    await press(more.locator('summary'))
    await expect(mark).toHaveText('Marcar Cocina caliente como lista')
    await expect(passPage.getByTestId(`do-mark-${ticketId}-cold`)).toHaveCount(0)              // lo ya listo de la fria, no
    await checkScreen(passPage, 'pass', true)
    const reviews = passTicket.locator('[data-testid^="review-"]')
    const pendingReviews = (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.reviewPending).length
    expect(pendingReviews).toBeGreaterThan(0)
    await expect(reviews).toHaveCount(pendingReviews, { timeout: 15_000 })
    for (const button of await reviews.first().locator('[data-testid^="do-"]').all()) await expect(button).toBeDisabled()   // sin nota no hay decision
    //    C3e.1 (usabilidad 28-10): «Rehacer», aparte y tambien apagado sin nota (H53); «No le afecta» en una linea (H53); y ante una
    //    alergia al marisco, sin «Sin gluten» (H48).
    await expect(reviews.first().locator('[data-testid^="do-review-"]')).toHaveCount(2)
    await expect(reviews.first().locator('[data-testid^="remake-"]')).toBeDisabled()
    expect(await gapBetween(reviews.first().locator('[data-testid^="do-review-adapt-"]'), reviews.first().locator('[data-testid^="remake-"]'))).toBeGreaterThanOrEqual(8)
    expect(await oneLine(reviews.first().locator('[data-testid^="do-review-unaffected-"]'))).toBe(true)
    await expect(reviews.first().getByRole('button', { name: 'Sin gluten' })).toHaveCount(0)
    await expect(reviews.first().getByRole('button', { name: 'No lo lleva' })).toHaveCount(1)
    //    Revision C3d: un version_conflict en una revision (la caliente empieza un plato a la vez) NO borra la nota escrita: la comanda
    //    pide tocar otra vez y UN toque, sin volver a escribirla, la envia como otra orden (otra clave) con la version releida.
    const reviewPosts: string[] = []
    passPage.on('request', sent => {
      if (sent.method() === 'POST' && sent.url().endsWith('/commands/review-preparation')) reviewPosts.push(sent.headers()['idempotency-key'] ?? '')
    })
    await passPage.route('**/commands/review-preparation', async route => {
      await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: hot[1].id }); await route.continue()
    }, { times: 1 })
    const firstNote = reviews.first().locator('input'), firstAdapt = reviews.first().locator('[data-testid^="do-review-adapt-"]')
    await firstNote.fill('sin marisco, salsa aparte')
    before = await version()
    await press(firstAdapt)
    await expect(passPage.getByTestId(`conflict-${ticketId}`)).toContainText('toca otra vez', { timeout: 15_000 })
    await expect(reviews).toHaveCount(pendingReviews)
    await expect(firstNote).toHaveValue('sin marisco, salsa aparte')                             // la nota sigue ahi
    await expect(firstAdapt).toBeEnabled()
    expect(await version()).toBe(before + 1)                                                   // solo la de la caliente
    expect(reviewPosts).toHaveLength(1)                                                        // nada se reenvia sin otro toque
    await press(firstAdapt)
    await expect(reviews).toHaveCount(pendingReviews - 1, { timeout: 15_000 })
    expect(reviewPosts).toHaveLength(2)
    expect(reviewPosts[1]).not.toBe(reviewPosts[0])
    await expect(passPage.getByTestId(`conflict-${ticketId}`)).toHaveCount(0)
    //    C3e.1 (usabilidad 28-10, H53): «Rehacer» en DOS pasos, con el plato caliente del comensal 1 (la fria, la primera de la comanda,
    //    ya esta adaptada). El primer toque no manda nada: en su sitio sale la pregunta, con «Cancelar» y «Sí, rehacer»; «Cancelar» vuelve
    //    atras sin orden y sin borrar la nota. «Sí, rehacer» es UNA review-preparation: el plato vuelve a «Por empezar» en su estacion.
    const hotPending = (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.reviewPending && p.stationId === 'hot')
    expect(hotPending).toHaveLength(1)
    const remakeKey = `${ticketId}-${hotPending[0].id}`
    const remakeReview = passPage.getByTestId(`review-${remakeKey}`), askRemake = passPage.getByTestId(`remake-${remakeKey}`)
    const remakeStep = passPage.getByTestId(`remake-step-${remakeKey}`), cancelRemake = passPage.getByTestId(`remake-cancel-${remakeKey}`)
    const confirmRemake = passPage.getByTestId(`do-review-remake-${remakeKey}`)
    await expect(askRemake).toHaveText('Rehacer')
    await expect(confirmRemake).toHaveCount(0)                                                 // la orden solo sale de la pregunta
    await remakeReview.locator('input').fill('sin marisco, de nuevo')
    await expect(askRemake).toBeEnabled()
    await askRemake.scrollIntoViewIfNeeded()                                                   // medido donde se toca: el toque ya no desplaza
    const askBox = await askRemake.boundingBox()
    before = await version()
    await press(askRemake)
    await expect(remakeStep).toContainText('¿Rehacer Preparación caliente? Vuelve a la estación.')
    await expect(askRemake).toHaveCount(0)
    await expect(cancelRemake).toHaveText('Cancelar')
    await expect(confirmRemake).toHaveText('Sí, rehacer')
    //    La pregunta ocupa el sitio del primer toque: el segundo toque de un doble toque cae en el texto, no en un boton.
    const questionBox = await remakeStep.locator('.k-remake-q').boundingBox()
    expect(Math.abs(questionBox!.y - askBox!.y)).toBeLessThanOrEqual(1)
    expect(questionBox!.height).toBeGreaterThanOrEqual(askBox!.height - 1)
    expect(askBox!.height).toBeGreaterThanOrEqual(64)                                          // revision de C3e.2: remake-… tampoco es «do-»
    expect((await cancelRemake.boundingBox())!.height).toBeGreaterThanOrEqual(64)
    expect(await gapBetween(cancelRemake, confirmRemake)).toBeGreaterThanOrEqual(8)
    await checkScreen(passPage, 'pass asking to remake', true)
    await press(cancelRemake)
    await expect(remakeStep).toHaveCount(0)
    await expect(askRemake).toBeEnabled()
    await expect(remakeReview.locator('input')).toHaveValue('sin marisco, de nuevo')           // «Cancelar» no borra la nota
    expect(reviewPosts).toHaveLength(2)                                                        // ni el primer toque ni «Cancelar» mandan nada
    expect(await version()).toBe(before)
    await press(askRemake)
    await press(confirmRemake)
    await expect(remakeReview).toHaveCount(0, { timeout: 15_000 })
    expect(reviewPosts).toHaveLength(3)
    expect(await version()).toBe(before + 1)
    expect((await read(request, serviceId)).data.courses[0].preparations.find(p => p.id === hotPending[0].id)?.state).toBe('Fired')
    await expect(passTicket).toContainText('Rehecho: sin marisco, de nuevo')
    for (let left = pendingReviews - 2; left > 0; left--) {
      await reviews.first().locator('input').fill('sin marisco, salsa aparte')
      await press(reviews.first().locator('[data-testid^="do-review-adapt-"]'))
      await expect(reviews).toHaveCount(left - 1, { timeout: 15_000 })                         // relectura tras confirmar: version fresca
    }
    await expect(passTicket).toContainText('Adaptado: sin marisco, salsa aparte')
    await expect(coldDone).toContainText('Adaptado: sin marisco, salsa aparte', { timeout: 15_000 })   // la estacion lo ve sola

    // 6) El pase marca lo de la caliente desde «Más opciones» (UNA orden). Solo cuando TODO esta listo el servidor anuncia VALIDAR, y
    //    solo al pase. Validar es la llamada al camarero: «Esperando al camarero» hasta que se sirve.
    const ready = passPage.getByTestId(`do-ready-${ticketId}`)
    await expect(passPage.getByTestId(`missing-${ticketId}`)).toHaveText('Falta Cocina caliente')
    if (!await mark.isVisible()) await press(more.locator('summary'))
    before = await version()
    await press(mark)
    await expect(ready).toHaveText('Validar pase: listo para servir', { timeout: 15_000 })
    expect(await version()).toBe(before + 1)
    expect((await read(request, serviceId)).data.courses[0].preparations.filter(p => p.stationId === 'hot').map(p => p.state)).toEqual(['Ready', 'Ready'])
    await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina caliente: lista')
    await expect(passPage.getByTestId(`missing-${ticketId}`)).toHaveCount(0)
    await expect(coldPage.getByTestId(`do-ready-${ticketId}`)).toHaveCount(0)
    await checkScreen(passPage, 'pass ready to validate', true)
    before = await version()
    await press(ready)
    const waiting = passPage.getByTestId(`waiting-${ticketId}`)
    await expect(waiting).toContainText(`${TABLE.name} · Aperitivos`, { timeout: 15_000 })
    await expect(waiting).toContainText(/listo hace/)
    await expect(passTicket).toHaveCount(0)
    await expect(passPage.getByTestId('pending')).toHaveCount(0)
    expect(await version()).toBe(before + 1)
    expect((await read(request, serviceId)).data.courses[0].state).toBe('Ready')
    await expect(coldDone).toHaveCount(0, { timeout: 15_000 })                                  // un pase validado ya no es trabajo de la estacion
    await checkScreen(passPage, 'waiting for the waiter', true)

    //    Sala sirve: desaparece tambien de «Esperando al camarero».
    await command(request, serviceId, 'serve', { courseId: course.id })
    await expect(waiting).toHaveCount(0, { timeout: 15_000 })

    for (const [page, where] of [[coldPage, 'cold station'], [passPage, 'pass']] as const) await checkScreen(page, `${where} at the end`, true)
    expect(await notes(coldPage)).toBe(4)                                                       // una sola comanda nueva: un solo tono
    expect(leaks, 'a kitchen or room device received financial data').toEqual([])
    expect(violations, 'CSP violations in the console').toEqual([])
    told = true
  } finally {
    for (const context of contexts) await context.close()
    // La mesa queda libre; si la historia fallo a medias, sin tapar su error.
    if (told) await freeTable(request, serviceId)
    else await freeTable(request, serviceId).catch(() => undefined)
  }
})
