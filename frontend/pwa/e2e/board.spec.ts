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

const command = async (request: APIRequestContext, serviceId: string, action: string, fields: Record<string, unknown> = {}) => {
  const current = await (await asMain(request, `/dining/services/${serviceId}`)).json()
  const response = await asMain(request, `/dining/services/${serviceId}/commands/${action}`, { expectedVersion: current.version, ...fields })
  expect(response.ok(), `${action} -> ${response.status()}`).toBeTruthy()
}

test('the waiter board follows the kitchen and the main PC in real time, with money-free data and honest staleness', async ({ page, request, context }) => {
  test.setTimeout(180_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = [], sockets: string[] = []
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
  page.on('websocket', socket => sockets.push(socket.url()))

  await pair(page, request, `e2e-board-${RUN}`)
  await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  // El WebSocket lleva un BILLETE efimero, nunca el token del dispositivo.
  expect(sockets.length).toBeGreaterThan(0)
  expect(sockets.every(url => url.includes('access_token=') && !url.includes('dev.'))).toBeTruthy()

  // 1) Otro actor abre una mesa: aparece SOLA, sin recargar ni pulsar nada.
  const opened = await (await asMain(request, '/dining/services', { tableId: 'M6', pax: 3, menuId: 'LAB-TASTING' })).json()
  const tile = page.getByTestId('table-M6')
  await expect(tile).toBeVisible({ timeout: 10_000 })
  await expect(tile).toContainText('3 personas')
  await expect(tile).toContainText('Mesa abierta')

  // 2) El servicio avanza desde el PC principal: el tablero lo refleja.
  await command(request, opened.serviceId, 'start')
  await command(request, opened.serviceId, 'fire-next')
  await expect(tile).toContainText('Aperitivos: Enviado a cocina', { timeout: 10_000 })

  // 3) Alergia declarada con el pase ya enviado: texto + sustancia + severidad, y revision pendiente de cocina.
  await command(request, opened.serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  await expect(tile).toContainText('restriccion(es) declaradas', { timeout: 10_000 })
  await expect(tile).toContainText('PENDIENTES DE REVISION')
  await tile.click()
  const detail = page.getByTestId('detail')
  await expect(detail.getByTestId('restrictions')).toContainText('Comensal 1 · ALERGIA marisco (grave)')
  await expect(detail).toContainText('PENDIENTE DE REVISION POR COCINA')
  await expect(detail).toContainText('estacion cold')

  // 4) El detalle abierto tambien vive: cocina marca una elaboracion y se ve sin tocar nada.
  const read = await (await asMain(request, `/dining/services/${opened.serviceId}`)).json()
  const course = read.data.courses[0] as { id: string; preparations: Array<{ id: string; stationId: string }> }
  const hot = course.preparations.find(p => p.stationId === 'hot')!, cold = course.preparations.find(p => p.stationId === 'cold')!
  await expect(detail).not.toContainText('En preparacion')
  await command(request, opened.serviceId, 'preparation-start', { courseId: course.id, itemId: hot.id })
  await expect(detail).toContainText('En preparacion', { timeout: 10_000 })

  // 5) Sin red: lo dice, conserva lo ultimo leido y marca la antiguedad. Al volver, relee sola.
  await context.setOffline(true)
  await page.getByRole('button', { name: 'Actualizar' }).click()
  await expect(page.getByTestId('board-error')).toContainText('desactualizados')
  await expect(page.getByTestId('table-M6')).toHaveCount(0)                           // seguimos en el detalle: lo ultimo leido no desaparece
  await command(request, opened.serviceId, 'preparation-start', { courseId: course.id, itemId: cold.id })   // cambio ocurrido durante el corte (la API de test no pasa por el navegador)
  await context.setOffline(false)
  await expect(page.getByTestId('board-error')).toHaveCount(0, { timeout: 30_000 })
  await expect(detail.locator('li', { hasText: 'En preparacion' })).toHaveCount(2, { timeout: 30_000 })   // lo ocurrido durante el corte aparece solo
  await expect(page.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 60_000 })

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
  // Ni importes ni palabras de caja en lo que el camarero VE.
  expect(await page.locator('main').innerText()).not.toMatch(/€|\bprecio\b|\bsaldo\b|\bpago\b|\bcuenta\b/i)
})
