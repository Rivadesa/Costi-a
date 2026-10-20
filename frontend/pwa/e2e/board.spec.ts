import { expect, test } from '@playwright/test'
import { checkDevice, checkScreen, command, deviceName, lane, MAIN, openTable, pairThroughTheUi, press, read, watch } from './support'

test('the waiter board follows the kitchen and the main PC in real time, with money-free data and honest staleness', async ({ page, request, context }) => {
  test.setTimeout(180_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const { board: table } = lane()
  const leaks: string[] = [], violations: string[] = [], sockets: string[] = []
  watch(page, leaks, violations)
  page.on('websocket', socket => sockets.push(socket.url()))

  await pairThroughTheUi(page, request, deviceName('board'), 'service', 'sala-1')
  await expect(page.getByTestId('board')).toBeVisible()
  await checkDevice(page)
  await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  // El WebSocket lleva un BILLETE efimero, nunca el token del dispositivo.
  expect(sockets.length).toBeGreaterThan(0)
  expect(sockets.every(url => url.includes('access_token=') && !url.includes('dev.'))).toBeTruthy()
  // C3a: el plano tiene TODAS las mesas activas, tambien las libres, con su estado escrito.
  const tile = page.getByTestId(`table-${table.id}`)
  await expect(tile.getByTestId('tile-state')).toHaveText('Libre', { timeout: 10_000 })
  await checkScreen(page, 'floor')

  // 1) Otro actor abre la mesa: cambia SOLA, sin recargar ni pulsar nada.
  const serviceId = await openTable(request, table.id, 3)
  await expect(tile.getByTestId('tile-state')).toHaveText('Sentados', { timeout: 10_000 })
  await expect(tile).toContainText('3 personas')

  // 2) El servicio avanza desde el PC principal: el plano lo refleja.
  await command(request, serviceId, 'start')
  await command(request, serviceId, 'fire-next')
  await expect(tile.getByTestId('tile-state')).toHaveText('En cocina', { timeout: 10_000 })
  await expect(tile).toContainText('0 de 2')                                           // avance de los pases

  // 3) Alergia declarada con el pase ya enviado: texto + sustancia + severidad, y revision pendiente de cocina.
  await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  await expect(tile.getByTestId('tile-alert')).toHaveText('Alergia · por revisar', { timeout: 10_000 })   // siempre escrito, nunca solo color
  await press(tile)
  const detail = page.getByTestId('detail')
  await expect(detail.getByTestId('detail-state')).toHaveText('En cocina')
  await expect(detail.getByTestId('restrictions')).toContainText('Comensal 1 · ALERGIA marisco (grave)')
  await expect(detail.getByTestId('restrictions-pending')).toContainText('SIN REVISAR POR COCINA')
  await expect(detail.getByTestId('courses')).toContainText('PENDIENTE DE REVISIÓN POR COCINA')   // en cada elaboracion afectada
  await expect(detail.getByTestId('courses')).toContainText('Cocina fría')                 // el NOMBRE de la estacion, no su codigo
  await expect(detail).not.toContainText(new RegExp(`cold|${table.id}`))
  await checkScreen(page, 'table card')

  // 4) La ficha abierta tambien vive: cocina marca una elaboracion y se ve sin tocar nada.
  const course = (await read(request, serviceId)).data.courses[0]
  const hot = course.preparations.find(p => p.stationId === 'hot')!, cold = course.preparations.find(p => p.stationId === 'cold')!
  await expect(detail.getByTestId(`prep-${hot.id}`)).not.toContainText('En preparación')
  await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: hot.id })
  await expect(detail.getByTestId(`prep-${hot.id}`)).toContainText('En preparación', { timeout: 10_000 })

  // 5) Sin red: lo dice, conserva lo ultimo leido y marca la antiguedad. Al volver, relee sola.
  await context.setOffline(true)
  await press(page.getByTestId('refresh'))
  await expect(page.getByTestId('board-error')).toContainText('desactualizados')
  await expect(detail).toBeVisible()                                                     // seguimos en la ficha: lo ultimo leido no desaparece
  await command(request, serviceId, 'preparation-start', { courseId: course.id, itemId: cold.id })   // cambio ocurrido durante el corte (la API de test no pasa por el navegador)
  await context.setOffline(false)
  await expect(page.getByTestId('board-error')).toHaveCount(0, { timeout: 30_000 })
  await expect(detail.getByTestId(`prep-${cold.id}`)).toContainText('En preparación', { timeout: 30_000 })   // lo ocurrido durante el corte aparece solo
  await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 60_000 })

  // 6) Volver al plano: la mesa sigue con su estado y su aviso.
  await press(detail.getByTestId('back'))
  await expect(tile.getByTestId('tile-state')).toHaveText('En cocina')
  await checkScreen(page, 'floor after the card')

  // 7) Con dos salas, la elegida sigue elegida al volver de una ficha (como SelectedZone en el Comedor). El laboratorio tiene una sola
  // sala: /configuration se reescribe EN EL NAVEGADOR (la mesa del carril pasa a una «Terraza»); el motor y la base no cambian.
  const configurationRoute = '**/api/native/v1/configuration'
  await page.route(configurationRoute, async route => {
    const response = await route.fetch()
    const body = await response.json() as { tables: Array<{ id: string; zoneId?: string | null; zoneName?: string | null }> }
    for (const row of body.tables) if (row.id === table.id) { row.zoneId = 'e2e-terraza'; row.zoneName = 'Terraza' }
    await route.fulfill({ response, json: body })
  })
  await press(page.getByTestId('refresh'))                                               // «Actualizar» relee tambien la configuracion
  const terrace = page.getByTestId('room-tab-e2e-terraza')
  await press(terrace)
  await expect(terrace).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByTestId('table-M1')).toHaveCount(0)                               // solo la Terraza
  await checkScreen(page, 'floor with two rooms')
  await press(tile)
  await expect(detail.getByTestId('detail-state')).toHaveText('En cocina')
  await press(detail.getByTestId('back'))
  await expect(page.getByTestId('room-tab-e2e-terraza')).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByTestId('table-M1')).toHaveCount(0)
  await expect(tile).toBeVisible()
  await page.unroute(configurationRoute)

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
