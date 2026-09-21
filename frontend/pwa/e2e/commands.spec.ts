import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { randomBytes } from 'node:crypto'

const API = '/api/native/v1'
const MAIN = process.env.E2E_MAIN_TOKEN ?? ''
const RUN = process.env.E2E_RUN ?? randomBytes(3).toString('hex')
const MONEY = /(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)/i

const asMain = (request: APIRequestContext, path: string, data?: unknown) =>
  data === undefined
    ? request.get(API + path, { headers: { Authorization: `Bearer ${MAIN}` } })
    : request.post(API + path, { data, headers: { Authorization: `Bearer ${MAIN}`, 'Idempotency-Key': randomBytes(16).toString('hex') } })

function moneyKeys(value: unknown, path = '$'): string[] {
  if (Array.isArray(value)) return value.flatMap((item, index) => moneyKeys(item, `${path}[${index}]`))
  if (value !== null && typeof value === 'object')
    return Object.entries(value).flatMap(([key, inner]) => [...(MONEY.test(key) ? [`${path}.${key}`] : []), ...moneyKeys(inner, `${path}.${key}`)])
  return []
}

async function pair(page: Page, request: APIRequestContext, deviceName: string): Promise<void> {
  const issued = await (await asMain(request, '/auth/pairings', {})).json()
  await page.goto(`/app/#pair=${issued.code}`)
  await page.fill('input[name=deviceName]', deviceName)
  await page.click('button[type=submit]')
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === deviceName)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role: 'service', station: 'sala-1' })).ok()).toBeTruthy()
  await expect(page.getByTestId('board')).toBeVisible({ timeout: 15_000 })
}

const read = async (request: APIRequestContext, serviceId: string) => (await asMain(request, `/services/${serviceId}`)).json() as Promise<{ version: number; data: { state: string; courses: Array<{ id: string; state: string; preparations: Array<{ id: string }> }> } }>
const asKitchen = async (request: APIRequestContext, serviceId: string, action: string, fields: Record<string, unknown>) => {
  const current = await read(request, serviceId)
  const response = await asMain(request, `/services/${serviceId}/commands/${action}`, { expectedVersion: current.version, ...fields })
  expect(response.ok(), `${action} -> ${response.status()}`).toBeTruthy()
}
// Lo que hay DE VERDAD guardado en el dispositivo (IndexedDB), no lo que pinta la pantalla.
const storedOrder = (page: Page) => page.evaluate(() => new Promise<string | null>(resolve => {
  const open = indexedDB.open('costina')
  open.onsuccess = () => { const get = open.result.transaction('pending').objectStore('pending').get('uncertain'); get.onsuccess = () => { open.result.close(); resolve(get.result ? String(get.result.description) : null) } }
  open.onerror = () => resolve('error')
}))
// Cuantas veces quedo AUDITADA una accion sobre este servicio: la prueba de "un solo efecto".
const audited = async (request: APIRequestContext, serviceId: string) => (await read(request, serviceId)).version

test('a waiter runs a table from the tablet; lost responses and network cuts never duplicate or lose a command', async ({ page, request, context }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
  await pair(page, request, `e2e-waiter-${RUN}`)

  // 1) Abrir mesa, iniciar y enviar el primer pase DESDE LA TABLET. Los botones existen porque el servidor los anuncia.
  await page.locator('[data-testid=open-form] summary').click()
  await page.selectOption('select[name=table]', 'M7')
  await page.fill('input[name=pax]', '2')
  await page.getByTestId('do-open').click()
  const tile = page.getByTestId('table-M7')
  await expect(tile).toBeVisible({ timeout: 15_000 })
  await expect(page.getByTestId('pending')).toHaveCount(0)
  const board = await (await asMain(request, '/board')).json() as Array<{ service: { id: string; tableId: string } }>
  const serviceId = board.find(entry => entry.service.tableId === 'M7')!.service.id
  await tile.click()
  await page.getByTestId('do-start').click()
  await expect(page.getByTestId('do-fire-next')).toBeVisible({ timeout: 15_000 })
  await page.getByTestId('do-fire-next').click()
  await expect(page.getByTestId('detail')).toContainText('Enviado a cocina', { timeout: 15_000 })
  // Un dispositivo de SALA no ve acciones de cocina ni de cierre: el servidor no se las anuncia.
  await expect(page.getByTestId('actions')).not.toContainText(/Validar|Terminar|Liberar|Elaboracion lista/i)

  // 2) Cocina termina y valida el pase (por API): aparece "Servir". Se sirve desde la tablet.
  let current = await read(request, serviceId)
  for (const preparation of current.data.courses[0].preparations) {
    await asKitchen(request, serviceId, 'preparation-start', { courseId: current.data.courses[0].id, itemId: preparation.id })
    await asKitchen(request, serviceId, 'preparation-ready', { courseId: current.data.courses[0].id, itemId: preparation.id })
  }
  await asKitchen(request, serviceId, 'ready', { courseId: current.data.courses[0].id })
  const serve = page.getByTestId(`do-serve-${current.data.courses[0].id}`)
  await expect(serve).toBeVisible({ timeout: 15_000 })
  await serve.click()
  await expect(page.getByTestId('detail')).toContainText('Servido', { timeout: 15_000 })

  // 3) RESPUESTA PERDIDA de verdad: el servidor APLICA la pausa pero la respuesta nunca llega a la tablet.
  await page.locator('[data-testid=actions] details', { hasText: 'Motivo' }).locator('summary').click()
  await page.fill('input[name=reason]', 'los comensales salen un momento')
  const before = await audited(request, serviceId)
  await page.route('**/commands/pause', async route => { await route.fetch(); await route.abort('connectionreset') })
  await page.getByTestId('do-pause').click()
  const pending = page.getByTestId('pending')
  await expect(pending).toBeVisible({ timeout: 15_000 })
  await expect(pending).toContainText('ORDEN SIN CONFIRMAR')
  await expect(pending).toContainText('Pausar servicio · M7')
  expect((await read(request, serviceId)).data.state).toBe('Paused')                       // el servidor SI la aplico
  for (const button of await page.locator('[data-testid=actions] button').all()) await expect(button).toBeDisabled()   // nada nuevo mientras tanto
  expect(await storedOrder(page)).toBe('Pausar servicio · M7')                              // persistida en el dispositivo
  await page.unroute('**/commands/pause')

  // 4) Cerrar y reabrir el navegador (recarga): la orden sigue ahi, inequivoca, y se resuelve con la MISMA clave.
  await page.reload()
  await expect(page.getByTestId('board')).toBeVisible({ timeout: 30_000 })
  await expect.poll(() => storedOrder(page), { timeout: 30_000 }).toBeNull()                 // reintento automatico con la misma clave: cerrada
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(request, serviceId)).toBe(before + 1)                                // UN solo efecto: ni perdida ni duplicada
  await expect(page.getByTestId('table-M7')).toContainText('EN PAUSA', { timeout: 15_000 })

  // 5) Otra respuesta perdida, resuelta PREGUNTANDO al servidor en vez de reintentar.
  await page.getByTestId('table-M7').click()
  const beforeResume = await audited(request, serviceId)
  await page.route('**/commands/resume', async route => { await route.fetch(); await route.abort('connectionreset') })
  await page.getByTestId('do-resume').click()
  await expect(page.getByTestId('pending')).toBeVisible({ timeout: 15_000 })
  await page.getByTestId('reconcile').click()                                               // la ruta sigue cortada: solo la consulta por clave llega
  await expect(page.getByTestId('command-notice')).toContainText('SI se aplico', { timeout: 15_000 })
  await page.unroute('**/commands/resume')
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(request, serviceId)).toBe(beforeResume + 1)

  // 6) RED CORTADA: la orden queda en el dispositivo, se ve tambien al reabrir SIN red, y sale sola al volver.
  const beforeFire = await audited(request, serviceId)
  await context.setOffline(true)
  await page.getByTestId('do-fire-next').click()
  await expect(page.getByTestId('pending')).toContainText('Enviar siguiente pase a cocina · M7', { timeout: 15_000 })
  expect(await audited(request, serviceId)).toBe(beforeFire)                                // no llego: nada aplicado
  await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined))
  await page.reload()                                                                       // "cierre del navegador" sin red: abre desde el shell
  await expect(page.getByTestId('offline-pending')).toContainText('Enviar siguiente pase a cocina · M7', { timeout: 15_000 })
  await context.setOffline(false)
  await expect(page.getByTestId('board')).toBeVisible({ timeout: 30_000 })
  await expect.poll(() => storedOrder(page), { timeout: 30_000 }).toBeNull()
  await expect(page.getByTestId('pending')).toHaveCount(0)
  expect(await audited(request, serviceId)).toBe(beforeFire + 1)                            // exactamente una vez
  expect((await read(request, serviceId)).data.courses[1].state).toBe('Fired')

  // 7) Consumo a mayores desde sala: catalogo SIN precios y respuesta sin importes.
  await expect(page.getByTestId('table-M7')).toContainText('Enviado a cocina', { timeout: 15_000 })   // tablero al dia tras el reintento: version fresca
  await page.getByTestId('table-M7').click()
  await page.locator('[data-testid=actions] details', { hasText: 'consumo' }).locator('summary').click()
  await expect(page.locator('select[name=product]')).not.toContainText(/€|\d+[.,]\d{2}/)
  await page.selectOption('select[name=product]', 'water')
  await page.getByTestId('do-add-consumption').click()
  // "pending" a cero ya era cierto ANTES del clic: no es senal de nada. La senal es el cargo en CAJA (nunca en la tablet).
  const waterCharges = async () => ((await (await asMain(request, `/checkout/services/${serviceId}`)).json()) as { data: { charges: Array<{ description: string }> } })
    .data.charges.filter(charge => /water|Agua/i.test(charge.description)).length
  await expect.poll(waterCharges, { timeout: 15_000 }).toBe(1)
  await expect(page.getByTestId('pending')).toHaveCount(0)
  await expect(page.getByTestId('command-notice')).toHaveCount(0)                            // confirmada, no rechazada
  expect(await waterCharges()).toBe(1)                                                       // un solo cargo

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})
