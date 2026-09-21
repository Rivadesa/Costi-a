import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { randomBytes } from 'node:crypto'

// D6.4: pantalla de cocina por estacion contra el MOTOR REAL. Mesa propia de este spec: M8.
const API = '/api/native/v1'
const MAIN = process.env.E2E_MAIN_TOKEN ?? ''
const RUN = process.env.E2E_RUN ?? randomBytes(3).toString('hex')
const MONEY = /(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)/i
const TABLE = 'M8'
// Dos dispositivos = dos contextos (IndexedDB propio). Las opciones se fijan aqui: no dependen de heredar las del proyecto.
const CONTEXT = { baseURL: process.env.E2E_BASE ?? 'http://127.0.0.1:5088', serviceWorkers: 'allow' as const }

const post = (request: APIRequestContext, token: string, path: string, data: unknown) =>
  request.post(API + path, { data, headers: { Authorization: `Bearer ${token}`, 'Idempotency-Key': randomBytes(16).toString('hex') } })
const asMain = (request: APIRequestContext, path: string, data?: unknown) =>
  data === undefined ? request.get(API + path, { headers: { Authorization: `Bearer ${MAIN}` } }) : post(request, MAIN, path, data)

function moneyKeys(value: unknown, path = '$'): string[] {
  if (Array.isArray(value)) return value.flatMap((item, index) => moneyKeys(item, `${path}[${index}]`))
  if (value !== null && typeof value === 'object')
    return Object.entries(value).flatMap(([key, inner]) => [...(MONEY.test(key) ? [`${path}.${key}`] : []), ...moneyKeys(inner, `${path}.${key}`)])
  return []
}
function watch(page: Page, leaks: string[], violations: string[]): void {
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
}

async function pairKitchen(page: Page, request: APIRequestContext, deviceName: string, station: string): Promise<void> {
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
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role: 'kitchen', station })).ok()).toBeTruthy()
  await expect(page.getByTestId('kitchen')).toBeVisible({ timeout: 15_000 })
}

type Service = { version: number; data: { courses: Array<{ id: string; state: string; preparations: Array<{ id: string; stationId: string; state: string; reviewPending?: boolean }> }> } }
const read = async (request: APIRequestContext, serviceId: string) => (await asMain(request, `/services/${serviceId}`)).json() as Promise<Service>
const command = async (request: APIRequestContext, serviceId: string, action: string, fields: Record<string, unknown> = {}) => {
  const response = await asMain(request, `/services/${serviceId}/commands/${action}`, { expectedVersion: (await read(request, serviceId)).version, ...fields })
  expect(response.ok(), `${action} -> ${response.status()}`).toBeTruthy()
}
const deviceToken = (page: Page) => page.evaluate(() => new Promise<string>(resolve => {
  const open = indexedDB.open('costina')
  open.onsuccess = () => { const get = open.result.transaction('device').objectStore('device').get('credential'); get.onsuccess = () => { open.result.close(); resolve(String(get.result?.token ?? '')) } }
}))

test('a station marks only its own work, the pass reviews and validates, and everything is enabled by the server alone', async ({ browser, request }) => {
  test.setTimeout(240_000)
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []

  // Sala (por API) abre M8, inicia y envia el primer pase: elaboraciones frias (cold) y calientes (hot), una POR COMENSAL.
  const opened = await (await asMain(request, '/services', { tableId: TABLE, pax: 2, menuId: 'LAB-TASTING' })).json()
  const serviceId: string = opened.serviceId
  await command(request, serviceId, 'start'); await command(request, serviceId, 'fire-next')
  const course = (await read(request, serviceId)).data.courses[0]
  const cold = course.preparations.find(p => p.stationId === 'cold')!, hot = course.preparations.find(p => p.stationId === 'hot')!
  const id = (action: string, preparationId: string) => `do-${action}-${TABLE}-${course.id}-${preparationId}`

  // 1) ESTACION FRIA: ve el pase entero, lo ajeno atenuado y SIN botones; ni validar ni revisar.
  const coldContext = await browser.newContext(CONTEXT); const coldPage = await coldContext.newPage(); watch(coldPage, leaks, violations)
  await pairKitchen(coldPage, request, `e2e-cold-${RUN}`, 'cold')
  await expect(coldPage.getByTestId('link')).toHaveText('Tiempo real activo', { timeout: 20_000 })
  const coldTicket = coldPage.getByTestId(`ticket-${TABLE}-${course.id}`)
  await expect(coldTicket).toBeVisible({ timeout: 15_000 })
  await expect(coldTicket).toContainText(/enviado hace/)
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${hot.id}`)).toContainText('(otra estacion)')
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`)).not.toContainText('(otra estacion)')
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${hot.id}`).locator('button')).toHaveCount(0)
  await expect(coldPage.getByTestId(`do-ready-${TABLE}-${course.id}`)).toHaveCount(0)

  // 2) Marca SU elaboracion: empezar -> lista. El servidor lo refleja.
  await coldPage.getByTestId(id('preparation-start', cold.id)).click()
  // El motor anuncia "empezar" y "marcar lista" A LA VEZ en una elaboracion enviada: la senal de que la primera orden
  // se aplico y se releyo es el ESTADO escrito, no que el segundo boton exista (ya existia).
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`).locator('.prep-title')).toContainText('· En preparacion', { timeout: 15_000 })
  await expect(coldPage.getByTestId(id('preparation-start', cold.id))).toHaveCount(0)
  await coldPage.getByTestId(id('preparation-ready', cold.id)).click()
  // La senal es el ESTADO escrito y que el servidor ya no anuncie la accion (no el texto del boton).
  await expect(coldPage.getByTestId(`prep-${TABLE}-${course.id}-${cold.id}`).locator('.prep-title')).toContainText('· Lista', { timeout: 15_000 })
  await expect(coldPage.getByTestId(id('preparation-ready', cold.id))).toHaveCount(0)
  await expect(coldPage.getByTestId('pending')).toHaveCount(0)
  expect((await read(request, serviceId)).data.courses[0].preparations.find(p => p.id === cold.id)!.state).toBe('Ready')

  // 3) La frontera es el SERVIDOR, no la pantalla: con el token de este dispositivo, marcar lo de otra estacion es un rechazo wrong_station.
  const token = await deviceToken(coldPage)
  expect(token.startsWith('dev.')).toBeTruthy()
  const foreign = await post(request, token, `/services/${serviceId}/commands/preparation-start`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id, itemId: hot.id })
  expect(foreign.status()).toBe(409); expect((await foreign.json()).error).toBe('wrong_station')
  const validate = await post(request, token, `/services/${serviceId}/commands/ready`, { expectedVersion: (await read(request, serviceId)).version, courseId: course.id })
  expect(validate.status()).toBe(409); expect((await validate.json()).error).toBe('wrong_station')

  // 4) Sala declara una alergia grave con el pase enviado: la estacion lo ve ESCRITO (tipo, sustancia, severidad), sin botones de revision.
  await command(request, serviceId, 'declare-restriction', { guestPosition: 1, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })
  await expect(coldTicket).toContainText('PENDIENTES DE REVISION', { timeout: 15_000 })
  await expect(coldTicket).toContainText('ALERGIA marisco (grave)')
  await expect(coldPage.locator('[data-testid^="review-"]')).toHaveCount(0)                               // en NINGUNA mesa: el servidor no se lo anuncia a una estacion

  // 5) PASE (otro dispositivo): todo es suyo para mirar, nada de marcar elaboraciones; revisa con nota OBLIGATORIA.
  const passContext = await browser.newContext(CONTEXT); const passPage = await passContext.newPage(); watch(passPage, leaks, violations)
  await pairKitchen(passPage, request, `e2e-pase-${RUN}`, 'pase')
  const passTicket = passPage.getByTestId(`ticket-${TABLE}-${course.id}`)
  await expect(passTicket).toBeVisible({ timeout: 15_000 })
  await expect(passTicket).not.toContainText('(otra estacion)')
  await expect(passPage.locator(`[data-testid^="do-preparation-"][data-testid*="-${TABLE}-"]`)).toHaveCount(0)
  await expect(passPage.getByTestId(`do-ready-${TABLE}-${course.id}`)).toHaveCount(0)            // con revisiones pendientes el servidor no anuncia validar
  const reviews = passTicket.locator('[data-testid^="review-"]')
  const pendingReviews = (await read(request, serviceId)).data.courses[0].preparations.filter(p => p.reviewPending).length
  expect(pendingReviews).toBeGreaterThan(0)
  await expect(reviews).toHaveCount(pendingReviews, { timeout: 15_000 })
  for (const button of await reviews.first().locator('button').all()) await expect(button).toBeDisabled()   // sin nota no hay decision
  for (let left = pendingReviews; left > 0; left--) {
    await reviews.first().locator('input').fill('sin marisco, salsa aparte')
    await reviews.first().locator('[data-testid^="do-review-adapt-"]').click()
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
  await ready.click()
  await expect(passTicket.locator('h3')).toContainText('LISTO PARA SERVIR', { timeout: 15_000 })          // el encabezado, no el texto del boton
  await expect(ready).toHaveCount(0)
  await expect(passPage.getByTestId('pending')).toHaveCount(0)
  expect((await read(request, serviceId)).data.courses[0].state).toBe('Ready')
  await expect(coldTicket).toHaveCount(0, { timeout: 15_000 })                                             // un pase listo ya no es trabajo de la estacion

  // 7) Sala sirve: desaparece tambien del pase.
  await command(request, serviceId, 'serve', { courseId: course.id })
  await expect(passTicket).toHaveCount(0, { timeout: 15_000 })

  for (const page of [coldPage, passPage]) await expect(page.locator('main')).not.toContainText(/€|precio|saldo|pago|cuenta/i)
  expect(leaks, 'a kitchen device received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
  await coldContext.close(); await passContext.close()
})
