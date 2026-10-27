import { expect, test } from '@playwright/test'
import { asMain, checkDevice, checkScreen, command, deviceName, lane, loseResponses, MAIN, pairThroughTheUi, press, read, storedOrder, watch } from './support'

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
  // C3e: el numero de personas solo sale tras «Más…» (mas de 8); lo elegido se lee en el boton y en el resumen de encima de «Abrir».
  await expect(page.getByTestId('open-summary')).toHaveText('2 personas · Menú de ensayo')
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
  // E4e: con la mesa Sentados el servidor anuncia ya «Enviar siguiente pase» (empieza y envia en UNA orden), que es el boton grande;
  // «Empezar» sigue existiendo, en «Más opciones».
  await expect(page.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
  const beforeFirst = (await read(request, serviceId)).version
  await press(page.getByTestId('do-fire-next'))
  await expect(detail.getByTestId('detail-state')).toHaveText('En cocina', { timeout: 15_000 })
  expect((await read(request, serviceId)).version).toBe(beforeFirst + 1)
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
  // C3e: lo servido sale de la lista de pases y se recoge en la linea de servidos de arriba (pases.ts).
  await expect(detail.getByTestId('courses-served').getByTestId(`served-${first.id}`)).toContainText('Servido', { timeout: 15_000 })
  await expect(detail.getByTestId(`course-${first.id}`)).toHaveCount(0)
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
  // En TODA la ficha: el boton grande, «Más opciones» y tambien anotar o retirar una restriccion, que van en el bloque de arriba.
  await expect(detail.locator('button[data-testid^="do-"]')).not.toHaveCount(0)
  await expect(detail.locator('button[data-testid^="do-"]:not([disabled])')).toHaveCount(0)
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
  // «Bodega y bebidas» (C3b): un toque suma 1 a la ronda y «Enviar» es UNA orden. Catalogo SIN precios y respuesta sin importes. La
  // historia entera (buscar, respuesta perdida, el otro comandero, la foto) esta en drinks.spec.ts.
  await press(page.getByTestId('entry-bodega'))
  await expect(page.getByTestId('panel-bodega')).toBeVisible()
  await expect(page.getByTestId('chip-glass')).toHaveAttribute('aria-pressed', 'true', { timeout: 15_000 })   // la entrada: «Por copa»
  await expect(page.getByTestId('drink-wine')).toBeVisible()
  // Ningun importe («9,50»); un formato como «Botella 0.75 L» no es un importe.
  await expect(page.getByTestId('panel-bodega')).not.toContainText(/€|\b\d+[.,]\d{2}\b(?!\s*c?l\b)/i)
  await checkScreen(page, 'Bodega y bebidas')
  await page.getByTestId('drinks-search').fill('agua')
  await press(page.getByTestId('add-water-bottle'))                                          // una sola presentacion: «Añadir»
  await expect(page.getByTestId('round-summary')).toHaveText('Ronda · 1 sin enviar')
  await press(page.getByTestId('do-send-round'))
  // "pending" a cero ya era cierto ANTES del clic: no es senal de nada. La senal es el cargo en CAJA (nunca en el comandero).
  const waterCharges = async () => ((await (await asMain(request, `/checkout/services/${serviceId}`)).json()) as { data: { charges: Array<{ description: string }> } })
    .data.charges.filter(charge => /water|Agua/i.test(charge.description)).length
  await expect.poll(waterCharges, { timeout: 15_000 }).toBe(1)
  await expect(page.getByTestId('round-empty')).toContainText('Ronda enviada')
  await expect(page.getByTestId('pending')).toHaveCount(0)
  await expect(page.getByTestId('command-notice')).toHaveCount(0)                           // confirmada, no rechazada
  expect(await waterCharges()).toBe(1)                                                      // un solo cargo
  await checkScreen(page, 'after a round')

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
