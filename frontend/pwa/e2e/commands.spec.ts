import { expect, test, type Page } from '@playwright/test'
import { asMain, checkDevice, checkScreen, command, deviceName, lane, MAIN, pairThroughTheUi, press, read, watch } from './support'

// Lo que hay DE VERDAD guardado en el dispositivo (IndexedDB), no lo que pinta la pantalla.
const storedOrder = (page: Page) => page.evaluate(() => new Promise<string | null>(resolve => {
  const open = indexedDB.open('costina')
  open.onsuccess = () => { const get = open.result.transaction('pending').objectStore('pending').get('uncertain'); get.onsuccess = () => { open.result.close(); resolve(get.result ? String(get.result.description) : null) } }
  open.onerror = () => resolve('error')
}))
// RESPUESTA PERDIDA de verdad: el servidor aplica la orden y la respuesta nunca llega. `lost` se resuelve cuando eso YA ocurrio:
// la orden pendiente se ve ANTES del primer intento (por diseno), asi que sin esperar a `lost` el test podia retirar la ruta
// con el manejador aun en route.fetch(), y Playwright daba la ruta por gestionada ("Route is already handled").
async function loseResponses(page: Page, pattern: string): Promise<{ lost: Promise<void> }> {
  let first: () => void = () => undefined
  const lost = new Promise<void>(resolve => { first = resolve })
  await page.route(pattern, async route => { try { await route.fetch(); await route.abort('connectionreset') } finally { first() } })
  return { lost }
}

test('a waiter runs a table from the handheld; lost responses and network cuts never duplicate or lose a command', async ({ page, request, context }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const { commands: table } = lane()
  const leaks: string[] = [], violations: string[] = []
  watch(page, leaks, violations)
  // Cuantas veces quedo AUDITADA una accion sobre este servicio: la prueba de "un solo efecto".
  const audited = async (serviceId: string) => (await read(request, serviceId)).version
  await pairThroughTheUi(page, request, deviceName('waiter'), 'service', 'sala-1')
  await expect(page.getByTestId('board')).toBeVisible()
  await checkDevice(page)

  // 1) Abrir mesa, iniciar y enviar el primer pase DESDE EL COMANDERO. Los botones existen porque el servidor los anuncia.
  // C3a: la mesa libre del plano abre la hoja «Abrir mesa» (personas y menu propuesto); al confirmarse, la ficha de esa mesa.
  const tile = page.getByTestId(`table-${table.id}`)
  await expect(tile.getByTestId('tile-state')).toHaveText('Libre', { timeout: 15_000 })
  await checkScreen(page, 'floor')
  await press(tile)
  const sheet = page.getByTestId('open-form')
  await expect(sheet).toContainText(`Abrir ${table.name}`)
  await expect(page.getByTestId('offer-LAB-TASTING')).toContainText('Menú del día')        // E4d: la primera oferta vigente, propuesta
  await press(page.getByTestId('pax-3'))
  await press(page.getByTestId('pax-2'))
  await expect(page.getByTestId('pax-2')).toHaveAttribute('aria-checked', 'true')
  await expect(sheet.locator('input[name=pax]')).toHaveValue('2')
  await press(page.getByTestId('offer-LAB-TASTING'))
  await checkScreen(page, 'open sheet')
  await press(page.getByTestId('do-open'))
  const detail = page.getByTestId('detail')
  await expect(detail.getByTestId('detail-state')).toHaveText('Sentados', { timeout: 15_000 })
  await expect(detail.getByTestId('detail-info')).toContainText('2 personas')
  await expect(page.getByTestId('pending')).toHaveCount(0)
  const board = await (await asMain(request, '/dining/board')).json() as Array<{ service: { id: string; tableId: string } }>
  const serviceId = board.find(entry => entry.service.tableId === table.id)!.service.id
  await checkScreen(page, 'table card')
  await press(page.getByTestId('do-start'))
  await expect(page.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
  await press(page.getByTestId('do-fire-next'))
  await expect(detail.getByTestId('detail-state')).toHaveText('En cocina', { timeout: 15_000 })
  await expect(page.getByTestId('do-wait')).toBeDisabled()                                 // nada que hacer: el boton grande dice que se espera
  // Un dispositivo de SALA no ve acciones de cocina ni de cierre: el servidor no se las anuncia.
  await expect(page.getByTestId('actions')).not.toContainText(/Validar|Terminar|Liberar|elaboraci[oó]n lista/i)

  // 2) Cocina termina y valida el pase (por API): aparece "Servir". Se sirve desde el comandero.
  const current = await read(request, serviceId)
  const first = current.data.courses[0]
  for (const preparation of first.preparations) {
    await command(request, serviceId, 'preparation-start', { courseId: first.id, itemId: preparation.id })
    await command(request, serviceId, 'preparation-ready', { courseId: first.id, itemId: preparation.id })
  }
  await command(request, serviceId, 'ready', { courseId: first.id })
  const serve = page.getByTestId(`do-serve-${first.id}`)
  await expect(serve).toBeVisible({ timeout: 15_000 })
  await expect(detail.getByTestId('detail-state')).toHaveText('Listo para servir')
  await press(serve)
  await expect(detail.getByTestId(`course-${first.id}`)).toContainText('Servido', { timeout: 15_000 })
  await expect(detail.getByTestId('detail-state')).toHaveText('Comiendo')

  // 3) RESPUESTA PERDIDA de verdad: el servidor APLICA la pausa pero la respuesta nunca llega al comandero.
  await press(page.getByTestId('more-options').locator('summary'))
  await page.fill('input[name=reason]', 'los comensales salen un momento')
  await checkScreen(page, 'more options')
  const before = await audited(serviceId)
  const pauseLost = await loseResponses(page, '**/commands/pause')
  await press(page.getByTestId('do-pause'))
  await pauseLost.lost
  const pending = page.getByTestId('pending')
  await expect(pending).toBeVisible({ timeout: 15_000 })
  await expect(pending).toContainText('ORDEN SIN CONFIRMAR')
  await expect(pending).toContainText(`Pausar servicio · ${table.name}`)
  expect((await read(request, serviceId)).data.state).toBe('Paused')                      // el servidor SI la aplico
  // Nada nuevo mientras tanto. El aviso de tiempo real puede releer y CAMBIAR los botones a mitad: se afirma que ninguno esta habilitado, no uno a uno.
  await expect(page.locator('[data-testid=actions] button:not([disabled])')).toHaveCount(0)
  expect(await storedOrder(page)).toBe(`Pausar servicio · ${table.name}`)                 // persistida en el dispositivo
  await checkScreen(page, 'uncertain order')
  await page.unrouteAll({ behavior: 'wait' })

  // 4) Cerrar y reabrir el navegador (recarga): la orden sigue ahi, inequivoca, y se resuelve con la MISMA clave.
  await page.reload()
  await expect(page.getByTestId('board')).toBeVisible({ timeout: 30_000 })
  await expect.poll(() => storedOrder(page), { timeout: 30_000 }).toBeNull()                // reintento automatico con la misma clave: cerrada
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(serviceId)).toBe(before + 1)                                        // UN solo efecto: ni perdida ni duplicada
  await expect(tile.getByTestId('tile-state')).toHaveText('En pausa', { timeout: 15_000 })

  // 5) Otra respuesta perdida, resuelta PREGUNTANDO al servidor en vez de reintentar.
  await press(tile)
  await expect(page.getByTestId('do-resume')).toBeVisible({ timeout: 15_000 })           // reanudar: el boton grande de una mesa en pausa
  const beforeResume = await audited(serviceId)
  const resumeLost = await loseResponses(page, '**/commands/resume')
  await press(page.getByTestId('do-resume'))
  await resumeLost.lost
  await expect(page.getByTestId('pending')).toBeVisible({ timeout: 15_000 })
  await press(page.getByTestId('reconcile'))                                               // la ruta sigue cortada: solo la consulta por clave llega
  await expect(page.getByTestId('command-notice')).toContainText('SÍ se aplicó', { timeout: 15_000 })
  await page.unrouteAll({ behavior: 'wait' })
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(serviceId)).toBe(beforeResume + 1)

  // 6) RED CORTADA: la orden queda en el dispositivo, se ve tambien al reabrir SIN red, y sale sola al volver.
  await expect(page.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
  const beforeFire = await audited(serviceId)
  await context.setOffline(true)
  await press(page.getByTestId('do-fire-next'))
  await expect(page.getByTestId('pending')).toContainText(`Enviar siguiente pase a cocina · ${table.name}`, { timeout: 15_000 })
  expect(await audited(serviceId)).toBe(beforeFire)                                        // no llego: nada aplicado
  await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined))
  await page.reload()                                                                      // "cierre del navegador" sin red: abre desde el shell
  await expect(page.getByTestId('offline-pending')).toContainText(`Enviar siguiente pase a cocina · ${table.name}`, { timeout: 15_000 })
  await checkScreen(page, 'offline')
  await context.setOffline(false)
  await expect(page.getByTestId('board')).toBeVisible({ timeout: 30_000 })
  await expect.poll(() => storedOrder(page), { timeout: 30_000 }).toBeNull()
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(serviceId)).toBe(beforeFire + 1)                                    // exactamente una vez
  expect((await read(request, serviceId)).data.courses[1].state).toBe('Fired')

  // 7) Las dos entradas grandes de la ficha. «Carta y menús»: lo de hoy; un menu sin eleccion no tiene nada que pedir.
  await expect(tile.getByTestId('tile-state')).toHaveText('En cocina', { timeout: 15_000 })   // plano al dia tras el reintento: version fresca
  await press(tile)
  await press(page.getByTestId('entry-carta'))
  await expect(page.getByTestId('panel-carta')).toBeVisible()
  await checkScreen(page, 'Carta y menus')
  await press(page.getByTestId('back'))
  // «Bodega y bebidas»: consumo a mayores desde sala, catalogo SIN precios y respuesta sin importes.
  await press(page.getByTestId('entry-bodega'))
  await expect(page.getByTestId('panel-bodega')).toBeVisible()
  await expect(page.locator('select[name=product]')).not.toContainText(/€|\d+[.,]\d{2}/)
  await checkScreen(page, 'Bodega y bebidas')
  await page.selectOption('select[name=product]', 'water/bottle')   // E3: producto/presentacion
  await press(page.getByTestId('do-add-consumption'))
  // "pending" a cero ya era cierto ANTES del clic: no es senal de nada. La senal es el cargo en CAJA (nunca en el comandero).
  const waterCharges = async () => ((await (await asMain(request, `/checkout/services/${serviceId}`)).json()) as { data: { charges: Array<{ description: string }> } })
    .data.charges.filter(charge => /water|Agua/i.test(charge.description)).length
  await expect.poll(waterCharges, { timeout: 15_000 }).toBe(1)
  await expect(page.getByTestId('pending')).toHaveCount(0)
  await expect(page.getByTestId('command-notice')).toHaveCount(0)                           // confirmada, no rechazada
  expect(await waterCharges()).toBe(1)                                                      // un solo cargo
  await checkScreen(page, 'after a consumption')

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
