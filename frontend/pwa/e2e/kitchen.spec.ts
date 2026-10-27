import { expect, test, type Page } from '@playwright/test'
import { checkDevice, checkScreen, command, databaseOf, deviceContext, deviceName, lane, MAIN, openTable, pairThroughTheUi, post, press, read, storedToken, watch } from './support'

// D6.4: pantalla de cocina por estacion contra el MOTOR REAL. C3a: con el aparato de cada proyecto (escritorio o movil tactil) y la
// mesa de cocina de su carril (e2e/support.ts).
// C3c: la credencial de una tablet de cocina vive en la base de Costina Cocina.
// C3d (paso 3 del plan 2026-10-27): ajustada lo justo a la pantalla nueva (la estacion ve solo lo suyo, «Listo» es UNA orden y el pase
// ve el progreso de cada estacion, marca por una estacion y deja lo validado en «Esperando al camarero»). El paso 4 la reescribe para
// el proyecto `tableta`.
const deviceToken = (page: Page) => storedToken(page, databaseOf('cocina'))

test('a station marks only its own work, the pass reviews and validates, and everything is enabled by the server alone', async ({ browser, request }) => {
  test.setTimeout(300_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const TABLE = lane().kitchen.id, TABLE_NAME = lane().kitchen.name
  const leaks: string[] = [], violations: string[] = []
  const version = async () => (await read(request, serviceId)).version

  // 0) E4e (ADR-016), REVISAR ANTES DE ENVIAR. La mesa se abre con una intolerancia del comensal 2 en la MISMA orden: elaboraciones frias
  //    (cold) y calientes (hot), una POR COMENSAL, y las del comensal 2, de todo el menu, por revisar. Sala lo ve («Por revisar en cocina») y
  //    su boton grande espera; el pase decide antes de enviar: «Adaptado» un plato (una orden) y «No le afecta a nada más» el resto (UNA
  //    orden); Sala ve «Adaptado: …» y envia el primer pase desde Sentados (una orden).
  const serviceId = await openTable(request, TABLE, 2, 'LAB-TASTING', [{ guestPosition: 2, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' }])
  const salaContext = await deviceContext(browser); const salaPage = await salaContext.newPage(); watch(salaPage, leaks, violations)
  await pairThroughTheUi(salaPage, request, deviceName('sala-pase'), 'service', 'sala-1')
  await press(salaPage.getByTestId(`table-${TABLE}`))
  const salaCard = salaPage.getByTestId('detail')
  await expect(salaCard.getByTestId('detail-state')).toHaveText('Sentados', { timeout: 15_000 })
  await expect(salaCard.getByTestId('courses')).toContainText('Por revisar en cocina')
  await expect(salaCard.getByTestId('courses')).toContainText('Intolerancia lactosa (leve)')
  await expect(salaPage.getByTestId('do-wait')).toHaveText('Esperando la revisión de cocina')
  await expect(salaPage.getByTestId('do-wait')).toBeDisabled()
  await expect(salaPage.getByTestId('do-fire-next')).toHaveCount(0)                        // el servidor no anuncia enviar lo que espera
  await checkScreen(salaPage, 'waiting for the kitchen review')

  const passContext = await deviceContext(browser); const passPage = await passContext.newPage(); watch(passPage, leaks, violations)
  await pairThroughTheUi(passPage, request, deviceName('pase'), 'kitchen', 'pase')
  await expect(passPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(passPage)
  const guest = passPage.getByTestId(`presend-${TABLE}-2`)
  await expect(guest).toBeVisible({ timeout: 15_000 })
  await expect(guest).toContainText(`${TABLE_NAME} · Comensal 2`)
  await expect(guest).toContainText('Intolerancia lactosa (leve)')
  await expect(guest.locator('[data-testid^="presend-item-"]')).toHaveCount(3)              // los tres platos del comensal 2, de los dos pases
  await expect(passPage.getByTestId(`presend-${TABLE}-1`)).toHaveCount(0)
  await checkScreen(passPage, 'pass before sending', true)
  const adapted = passPage.getByTestId(`presend-item-${TABLE}-p1-caliente-2`)
  for (const button of await adapted.locator('[data-testid^="do-"]').all()) await expect(button).toBeDisabled()   // sin nota no hay decision
  await adapted.locator('input').fill('aceite de oliva en vez de mantequilla')
  let before = await version()
  await press(passPage.getByTestId(`do-presend-adapt-${TABLE}-p1-caliente-2`))
  await expect(adapted).toHaveCount(0, { timeout: 15_000 })
  expect(await version()).toBe(before + 1)
  const rest = passPage.getByTestId(`presend-rest-${TABLE}-2`)
  await expect(rest).toContainText('No le afecta a nada más · 2 platos')
  await expect(passPage.getByTestId(`do-presend-rest-${TABLE}-2`)).toBeDisabled()
  await press(rest.getByRole('button', { name: 'No lo lleva' }))                             // C3d: el atajo rellena la nota a la vista
  await expect(rest.locator('input')).toHaveValue('No lo lleva')
  before = await version()
  await press(passPage.getByTestId(`do-presend-rest-${TABLE}-2`))
  await expect(guest).toHaveCount(0, { timeout: 15_000 })
  const reviewed = await read(request, serviceId)
  expect(reviewed.version).toBe(before + 1)                                                  // UNA orden para los dos platos
  expect(reviewed.data.courses.flatMap(current => current.preparations).filter(preparation => preparation.reviewPending)).toEqual([])
  await expect(salaCard.getByTestId('courses')).toContainText('Adaptado: aceite de oliva en vez de mantequilla', { timeout: 15_000 })
  await expect(salaPage.getByTestId('do-fire-next')).toBeVisible()
  before = await version()
  await press(salaPage.getByTestId('do-fire-next'))
  await expect(salaCard.getByTestId('detail-state')).toHaveText('En cocina', { timeout: 15_000 })
  expect(await version()).toBe(before + 1)                                                   // empieza y envia en UNA orden
  await checkScreen(salaPage, 'adapted before sending')
  await salaContext.close()

  const course = (await read(request, serviceId)).data.courses[0]
  const hot = course.preparations.find(p => p.stationId === 'hot')!
  const ticketId = `${TABLE}-${course.id}`

  // 1) ESTACION FRIA (C3d, ADR-017): solo lo suyo, agrupado («Preparación fría × 2»); lo de la caliente no se pinta, se nombra
  //    («También en: Cocina caliente»). Ni validar ni revisar. «Empezar turno» deja sonar.
  const coldContext = await deviceContext(browser); const coldPage = await coldContext.newPage(); watch(coldPage, leaks, violations)
  await pairThroughTheUi(coldPage, request, deviceName('cold'), 'kitchen', 'cold')
  await expect(coldPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(coldPage)
  await expect(coldPage.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  await press(coldPage.getByTestId('do-shift-start'))
  await expect(coldPage.getByTestId('shift-on')).toContainText('Turno empezado')
  const coldTicket = coldPage.getByTestId(`ticket-${ticketId}`)
  await expect(coldTicket).toBeVisible({ timeout: 15_000 })
  await expect(coldTicket.locator('h3')).toContainText(TABLE_NAME)                                         // el NOMBRE de la mesa, no su codigo
  await expect(coldTicket).toContainText(/Lleva/)
  await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('Preparación fría')
  await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('× 2')
  await expect(coldTicket.getByTestId(`dish-${ticketId}-0`)).toContainText('Comensal 2 · Intolerancia lactosa (leve) · No le afecta: No lo lleva')
  await expect(coldTicket).not.toContainText('Preparación caliente')
  await expect(coldPage.getByTestId(`also-${ticketId}`)).toHaveText('También en: Cocina caliente')
  await expect(coldTicket.locator('[data-testid^="do-listo-"]')).toHaveCount(1)                            // un solo plato: solo el «Listo» grande
  await expect(coldPage.getByTestId(`do-ready-${ticketId}`)).toHaveCount(0)
  await checkScreen(coldPage, 'cold station', true)

  // 2) «Listo»: UNA orden preparations-ready con lo suyo (las dos elaboraciones frias); la version sube en uno y la comanda baja a
  //    «Hecho · esperando al pase».
  before = await version()
  await press(coldPage.getByTestId(`do-listo-${ticketId}`))
  const coldDone = coldPage.getByTestId(`done-${ticketId}`)
  await expect(coldDone).toBeVisible({ timeout: 15_000 })
  await expect(coldTicket).toHaveCount(0)
  await expect(coldPage.getByTestId('pending')).toHaveCount(0)
  expect(await version()).toBe(before + 1)
  const marked = (await read(request, serviceId)).data.courses[0].preparations
  expect(marked.filter(p => p.stationId === 'cold').map(p => p.state)).toEqual(['Ready', 'Ready'])
  expect(marked.filter(p => p.stationId === 'hot').map(p => p.state)).toEqual(['Fired', 'Fired'])

  // 3) La frontera es el SERVIDOR, no la pantalla: con el token de este dispositivo, marcar lo de otra estacion es un rechazo wrong_station.
  const token = await deviceToken(coldPage)
  expect(token.startsWith('dev.')).toBeTruthy()
  const foreign = await post(request, token, `/dining/services/${serviceId}/commands/preparation-start`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id, itemId: hot.id })
  expect(foreign.status()).toBe(409); expect((await foreign.json()).error).toBe('wrong_station')
  const validate = await post(request, token, `/dining/services/${serviceId}/commands/ready`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id })
  expect(validate.status()).toBe(409); expect((await validate.json()).error).toBe('wrong_station')

  // 4) Sala declara una alergia grave con el pase enviado: la estacion lo ve ESCRITO (tipo, sustancia, severidad), tambien en la franja
  //    «Hecho», y sin botones de revision.
  await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  await expect(coldDone).toContainText('Por revisar en el pase', { timeout: 15_000 })
  await expect(coldDone).toContainText('ALERGIA marisco (grave)')
  await expect(coldPage.locator('[data-testid^="review-"]')).toHaveCount(0)                               // en NINGUNA mesa: el servidor no se lo anuncia a una estacion

  // 5) PASE (el dispositivo del paso 0): la mesa entera, con el progreso de cada estacion y lo que falta; revisa con nota OBLIGATORIA.
  //    E4e: puede marcar por una estacion con la tablet estropeada, desde «Más opciones» (lo ya listo de la fria, no).
  const passTicket = passPage.getByTestId(`ticket-${ticketId}`)
  await expect(passTicket).toBeVisible({ timeout: 15_000 })
  await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina fría: lista')
  await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina caliente: 0 de 1 plato')
  await expect(passPage.getByTestId(`missing-${ticketId}`)).toHaveText('Falta Cocina caliente y revisar la alergia')
  await expect(passPage.getByTestId(`do-ready-${ticketId}`)).toHaveCount(0)                    // con revisiones pendientes el servidor no anuncia validar
  const more = passPage.getByTestId(`more-${ticketId}`), mark = passPage.getByTestId(`do-mark-${ticketId}-hot`)
  await press(more.locator('summary'))
  await expect(mark).toHaveText('Marcar Cocina caliente como lista')
  await expect(passPage.getByTestId(`do-mark-${ticketId}-cold`)).toHaveCount(0)
  await checkScreen(passPage, 'pass', true)
  const reviews = passTicket.locator('[data-testid^="review-"]')
  const pendingReviews = (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.reviewPending).length
  expect(pendingReviews).toBeGreaterThan(0)
  await expect(reviews).toHaveCount(pendingReviews, { timeout: 15_000 })
  for (const button of await reviews.first().locator('[data-testid^="do-"]').all()) await expect(button).toBeDisabled()   // sin nota no hay decision
  for (let left = pendingReviews; left > 0; left--) {
    await reviews.first().locator('input').fill('sin marisco, salsa aparte')
    await press(reviews.first().locator('[data-testid^="do-review-adapt-"]'))
    await expect(reviews).toHaveCount(left - 1, { timeout: 15_000 })                                       // relectura tras confirmar: version fresca
  }
  await expect(passTicket).toContainText('Adaptado: sin marisco, salsa aparte')
  await expect(coldDone).toContainText('Adaptado: sin marisco, salsa aparte', { timeout: 15_000 })          // la estacion lo ve sola

  // 6) El pase marca lo de la caliente (UNA orden). Solo cuando TODO esta listo el servidor anuncia VALIDAR, y solo al pase.
  const ready = passPage.getByTestId(`do-ready-${ticketId}`)
  await expect(ready).toHaveCount(0)
  if (!await mark.isVisible()) await press(more.locator('summary'))
  before = await version()
  await press(mark)
  await expect(ready).toBeVisible({ timeout: 15_000 })
  expect(await version()).toBe(before + 1)
  await expect(passPage.getByTestId(`progress-${ticketId}`)).toContainText('Cocina caliente: lista')
  await expect(coldPage.getByTestId(`do-ready-${ticketId}`)).toHaveCount(0)
  await press(ready)
  const waiting = passPage.getByTestId(`waiting-${ticketId}`)
  await expect(waiting).toContainText(/listo hace/, { timeout: 15_000 })                                    // «Esperando al camarero»
  await expect(passTicket).toHaveCount(0)
  await expect(passPage.getByTestId('pending')).toHaveCount(0)
  expect((await read(request, serviceId)).data.courses[0].state).toBe('Ready')
  await expect(coldDone).toHaveCount(0, { timeout: 15_000 })                                                // un pase listo ya no es trabajo de la estacion

  // 7) Sala sirve: desaparece tambien de «Esperando al camarero».
  await command(request, serviceId, 'serve', { courseId: course.id })
  await expect(waiting).toHaveCount(0, { timeout: 15_000 })

  for (const [page, where] of [[coldPage, 'cold station'], [passPage, 'pass']] as const) await checkScreen(page, `${where} at the end`, true)
  expect(leaks, 'a kitchen device received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
  await coldContext.close(); await passContext.close()
})
