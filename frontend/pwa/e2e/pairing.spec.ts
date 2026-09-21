import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { randomBytes } from 'node:crypto'

const API = '/api/native/v1'
const MAIN = process.env.E2E_MAIN_TOKEN ?? ''
const RUN = process.env.E2E_RUN ?? randomBytes(3).toString('hex')
const MONEY = /(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)/i

// El "PC principal" se simula por API con un token main; el navegador solo es el dispositivo.
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

// Todo JSON que llega al navegador se inspecciona: la PWA no debe RECIBIR datos economicos (ADR-007).
function watchResponses(page: Page, leaks: string[], violations: string[]): void {
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin cuerpo JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
}

async function pairThroughTheUi(page: Page, request: APIRequestContext, deviceName: string, role: string, station: string): Promise<string> {
  const issued = await (await asMain(request, '/auth/pairings', {})).json()
  await page.goto(`/app/#pair=${issued.code}`)
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)          // el QR llevara el codigo en el fragmento
  await page.fill('input[name=deviceName]', deviceName)
  await page.click('button[type=submit]')
  await expect(page.getByTestId('pair-status')).toContainText('Esperando la aprobacion')
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === deviceName)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role, station })).ok()).toBeTruthy()
  await expect(page.getByTestId('home')).toBeVisible({ timeout: 15_000 })
  const devices = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; name: string }>
  return devices.find(d => d.name === deviceName)!.id
}

test('a device pairs, survives reload, opens offline from the shell cache and honours revocation', async ({ page, request, context }) => {
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []
  watchResponses(page, leaks, violations)
  const deviceName = `e2e-tablet-${RUN}`

  const document = await page.goto('/app/')
  expect(document!.headers()['content-security-policy']).toContain("script-src 'self'")
  await expect(page.getByTestId('pair')).toBeVisible()                              // sin credencial no hay nada que ver

  const deviceId = await pairThroughTheUi(page, request, deviceName, 'service', 'sala-1')
  await expect(page.getByTestId('actor')).toHaveText(`device:${deviceName}`)
  await expect(page.locator('h2')).toContainText('Sala')
  await expect(page.locator('h2')).toContainText('sala-1')
  expect(page.url()).not.toContain('pair=')                                         // el codigo usado no queda a la vista

  await page.reload()                                                               // la credencial vive en IndexedDB
  await expect(page.getByTestId('actor')).toHaveText(`device:${deviceName}`)

  // El service worker cachea SOLO el shell: sin red la aplicacion abre, pero no inventa datos.
  await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined))
  await page.reload()
  await context.setOffline(true)
  await page.reload()
  await expect(page.getByTestId('offline')).toBeVisible()
  await expect(page.getByTestId('home')).toHaveCount(0)
  const cached = await page.evaluate(async () => (await Promise.all((await caches.keys()).map(async name => (await (await caches.open(name)).keys()).map(r => new URL(r.url).pathname)))).flat())
  expect(cached.length).toBeGreaterThan(0)
  expect(cached.filter(path => !path.startsWith('/app/'))).toEqual([])              // nada de /api en cache, nunca
  await context.setOffline(false)
  await page.getByRole('button', { name: 'Reintentar' }).click()
  await expect(page.getByTestId('home')).toBeVisible()

  // Revocado en el PC principal: el dispositivo lo descubre, borra su credencial y vuelve a emparejar.
  expect((await asMain(request, `/auth/devices/${deviceId}/revoke`, {})).ok()).toBeTruthy()
  await page.getByRole('button', { name: 'Actualizar' }).click()
  await expect(page.getByTestId('pair')).toBeVisible()
  await expect(page.getByTestId('notice')).toContainText('revocado')
  await page.reload()
  await expect(page.getByTestId('pair')).toBeVisible()                              // la credencial se borro de verdad

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})

test('a kitchen device shows its station and a denied request says so', async ({ page, request }) => {
  const leaks: string[] = [], violations: string[] = []
  watchResponses(page, leaks, violations)
  await pairThroughTheUi(page, request, `e2e-kds-${RUN}`, 'kitchen', 'cold')
  await expect(page.locator('h2')).toContainText('Cocina')
  await expect(page.locator('h2')).toContainText('cold')
  await page.getByRole('button', { name: 'Desvincular este dispositivo' }).click()
  await expect(page.getByTestId('pair')).toBeVisible()

  const issued = await (await asMain(request, '/auth/pairings', {})).json()
  // PWA ya abierta y se escanea el QR: solo cambia el fragmento, sin recarga. Un fragmento hostil se ignora.
  await page.evaluate(code => { location.hash = `#pair=${code}` }, issued.code)
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)
  await page.evaluate(() => { location.hash = '#pair=<img src=x onerror=alert(1)>' })
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)
  await page.fill('input[name=deviceName]', `e2e-denied-${RUN}`)
  await page.click('button[type=submit]')
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === `e2e-denied-${RUN}`)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  await asMain(request, `/auth/pairings/${pairingId}/deny`, {})
  await expect(page.getByTestId('pair-status')).toContainText('denegada', { timeout: 15_000 })
  await page.fill('input[name=code]', 'codigo-inventado-0000000000')
  await page.click('button[type=submit]')
  await expect(page.getByTestId('pair-status')).toContainText('no valido')
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})
