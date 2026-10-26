import { expect, test, type Page } from '@playwright/test'
import { checkDevice, checkScreen, command, databaseOf, deviceContext, deviceName, lane, MAIN, openTable, pairThroughTheUi, post, press, read, storedToken, watch } from './support'

// D6.4: pantalla de cocina por estacion contra el MOTOR REAL. C3a: con el aparato de cada proyecto (escritorio o movil tactil) y la
// mesa de cocina de su carril (e2e/support.ts).
// C3c: la credencial de una tablet de cocina vive en la base de Costina Cocina.
const deviceToken = (page: Page) => storedToken(page, databaseOf('cocina'))

test('a station marks only its own work, the pass reviews and validates, and everything is enabled by the server alone', async ({ browser, request }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const TABLE = lane().kitchen.id, TABLE_NAME = lane().kitchen.name
  const leaks: string[] = [], violations: string[] = []

  // Sala (por API) abre la mesa, inicia y envia el primer pase: elaboraciones frias (cold) y calientes (hot), una POR COMENSAL.
  const serviceId = await openTable(request, TABLE, 2)
  await command(request, serviceId, 'start'); await command(request, serviceId, 'fire-next')
  const course = (await read(request, serviceId)).data.courses[0]
  const cold = course.preparations.find(p => p.stationId === 'cold')!, hot = course.preparations.find(p => p.stationId === 'hot')!
  const id = (action: string, preparationId: string) => `do-${action}-${TABLE}-${course.id}-${preparationId}`

  // 1) ESTACION FRIA: ve el pase entero, lo ajeno atenuado y SIN botones; ni validar ni revisar.
  const coldContext = await deviceContext(browser); const coldPage = await coldContext.newPage(); watch(coldPage, leaks, violations)
  await pairThroughTheUi(coldPage, request, deviceName('cold'), 'kitchen', 'cold')
  await expect(coldPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(coldPage)
  await expect(coldPage.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  const coldTicket = coldPage.getByTestId(`ticket-${TABLE}-${course.id}`)
  await expect(coldTicket).toBeVisible({ timeout: 15_000 })
  await expect(coldTicket.locator('h3')).toContainText(TABLE_NAME)                                         // el NOMBRE de la mesa, no su codigo
  await expect(coldTicket).toContainText(/enviado hace/)
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${hot.id}`)).toContainText('Cocina caliente (otra estación)')
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`)).not.toContainText('(otra estación)')
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${hot.id}`).locator('button')).toHaveCount(0)
  await expect(coldPage.getByTestId(`do-ready-${TABLE}-${course.id}`)).toHaveCount(0)
  await checkScreen(coldPage, 'cold station', true)

  // 2) Marca SU elaboracion: empezar -> lista. El servidor lo refleja.
  await press(coldPage.getByTestId(id('preparation-start', cold.id)))
  // El motor anuncia "empezar" y "marcar lista" A LA VEZ en una elaboracion enviada: la senal de que la primera orden
  // se aplico y se releyo es el ESTADO escrito, no que el segundo boton exista (ya existia).
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`).locator('.prep-title')).toContainText('· En preparación', { timeout: 15_000 })
  await expect(coldPage.getByTestId(id('preparation-start', cold.id))).toHaveCount(0)
  await press(coldPage.getByTestId(id('preparation-ready', cold.id)))
  // La senal es el ESTADO escrito y que el servidor ya no anuncie la accion (no el texto del boton).
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`).locator('.prep-title')).toContainText('· Lista', { timeout: 15_000 })
  await expect(coldPage.getByTestId(id('preparation-ready', cold.id))).toHaveCount(0)
  await expect(coldPage.getByTestId('pending')).toHaveCount(0)
  expect((await read(request, serviceId)).data.courses[0].preparations.find(p => p.id === cold.id)!.state).toBe('Ready')

  // 3) La frontera es el SERVIDOR, no la pantalla: con el token de este dispositivo, marcar lo de otra estacion es un rechazo wrong_station.
  const token = await deviceToken(coldPage)
  expect(token.startsWith('dev.')).toBeTruthy()
  const foreign = await post(request, token, `/dining/services/${serviceId}/commands/preparation-start`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id, itemId: hot.id })
  expect(foreign.status()).toBe(409); expect((await foreign.json()).error).toBe('wrong_station')
  const validate = await post(request, token, `/dining/services/${serviceId}/commands/ready`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id })
  expect(validate.status()).toBe(409); expect((await validate.json()).error).toBe('wrong_station')

  // 4) Sala declara una alergia grave con el pase enviado: la estacion lo ve ESCRITO (tipo, sustancia, severidad), sin botones de revision.
  await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  await expect(coldTicket).toContainText(/PENDIENTES? DE REVISIÓN/, { timeout: 15_000 })
  await expect(coldTicket).toContainText('ALERGIA marisco (grave)')
  await expect(coldPage.locator('[data-testid^="review-"]')).toHaveCount(0)                               // en NINGUNA mesa: el servidor no se lo anuncia a una estacion

  // 5) PASE (otro dispositivo): todo es suyo para mirar; revisa con nota OBLIGATORIA. E4e (ADR-016): tambien puede marcar por una estacion
  //    con la tablet estropeada, asi que el servidor le anuncia lo que falta de CUALQUIER estacion (lo ya listo de la fria, no).
  const passContext = await deviceContext(browser); const passPage = await passContext.newPage(); watch(passPage, leaks, violations)
  await pairThroughTheUi(passPage, request, deviceName('pase'), 'kitchen', 'pase')
  await expect(passPage.getByTestId('kitchen')).toBeVisible(); await checkDevice(passPage)
  const passTicket = passPage.getByTestId(`ticket-${TABLE}-${course.id}`)
  await expect(passTicket).toBeVisible({ timeout: 15_000 })
  await expect(passTicket).not.toContainText('(otra estación)')
  await expect(passPage.getByTestId(id('preparation-ready', hot.id))).toBeVisible()
  await expect(passPage.getByTestId(id('preparation-ready', cold.id))).toHaveCount(0)
  await expect(passPage.getByTestId(`do-ready-${TABLE}-${course.id}`)).toHaveCount(0)            // con revisiones pendientes el servidor no anuncia validar
  await checkScreen(passPage, 'pass', true)
  const reviews = passTicket.locator('[data-testid^="review-"]')
  const pendingReviews = (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.reviewPending).length
  expect(pendingReviews).toBeGreaterThan(0)
  await expect(reviews).toHaveCount(pendingReviews, { timeout: 15_000 })
  for (const button of await reviews.first().locator('button').all()) await expect(button).toBeDisabled()   // sin nota no hay decision
  for (let left = pendingReviews; left > 0; left--) {
    await reviews.first().locator('input').fill('sin marisco, salsa aparte')
    await press(reviews.first().locator('[data-testid^="do-review-adapt-"]'))
    await expect(reviews).toHaveCount(left - 1, { timeout: 15_000 })                                       // relectura tras confirmar: version fresca
  }
  await expect(passTicket).toContainText('Revisada: adaptada — sin marisco, salsa aparte')
  await expect(coldTicket).toContainText('Revisada: adaptada', { timeout: 15_000 })                         // la estacion lo ve sola

  // 6) El resto de la cocina termina (por API): las elaboraciones son POR COMENSAL, asi que quedan varias de cada estacion.
  //    Solo cuando TODAS estan listas el servidor anuncia VALIDAR, y solo al pase.
  const ready = passPage.getByTestId(`do-ready-${TABLE}-${course.id}`)
  await expect(ready).toHaveCount(0)
  await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: hot.id })
  for (const preparation of (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.state !== 'Ready'))
    await command(request, serviceId, 'preparation-ready', { courseId: course.id, itemId: preparation.id })
  await expect(ready).toBeVisible({ timeout: 15_000 })
  await expect(coldPage.getByTestId(`do-ready-${TABLE}-${course.id}`)).toHaveCount(0)
  await press(ready)
  await expect(passTicket.locator('h3')).toContainText('Listo para servir', { timeout: 15_000 })          // el encabezado, no el texto del boton
  await expect(ready).toHaveCount(0)
  await expect(passPage.getByTestId('pending')).toHaveCount(0)
  expect((await read(request, serviceId)).data.courses[0].state).toBe('Ready')
  await expect(coldTicket).toHaveCount(0, { timeout: 15_000 })                                             // un pase listo ya no es trabajo de la estacion

  // 7) Sala sirve: desaparece tambien del pase.
  await command(request, serviceId, 'serve', { courseId: course.id })
  await expect(passTicket).toHaveCount(0, { timeout: 15_000 })

  for (const [page, where] of [[coldPage, 'cold station'], [passPage, 'pass']] as const) await checkScreen(page, `${where} at the end`, true)
  expect(leaks, 'a kitchen device received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
  await coldContext.close(); await passContext.close()
})
