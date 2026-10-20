import { expect, test } from '@playwright/test'
import { asMain, checkScreen, deviceName, MAIN, pairThroughTheUi, press, watch } from './support'

test('a device pairs, survives reload, opens offline from the shell cache and honours revocation', async ({ page, request, context }) => {
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []
  watch(page, leaks, violations)
  const name = deviceName('tablet')

  const document = await page.goto('/app/')
  expect(document!.headers()['content-security-policy']).toContain("script-src 'self'")
  await expect(page.getByTestId('pair')).toBeVisible()                              // sin credencial no hay nada que ver

  const deviceId = await pairThroughTheUi(page, request, name, 'service', 'sala-1')
  await expect(page.getByTestId('actor')).toHaveText(name)
  await expect(page.getByTestId('role')).toContainText('Comandero')
  await expect(page.getByTestId('role')).toContainText('Sala 1')                     // A4: el NOMBRE de la estacion, no su codigo
  expect(page.url()).not.toContain('pair=')                                         // el codigo usado no queda a la vista
  await checkScreen(page, 'home')

  await page.reload()                                                               // la credencial vive en IndexedDB
  await expect(page.getByTestId('actor')).toHaveText(name)

  // El service worker cachea SOLO el shell: sin red la aplicacion abre, pero no inventa datos.
  await page.evaluate(() => navigator.serviceWorker.ready.then(() => undefined))
  await page.reload()
  await context.setOffline(true)
  await page.reload()
  await expect(page.getByTestId('offline')).toBeVisible()
  await expect(page.getByTestId('home')).toHaveCount(0)
  await checkScreen(page, 'offline')
  const cached = await page.evaluate(async () => (await Promise.all((await caches.keys()).map(async name => (await (await caches.open(name)).keys()).map(r => new URL(r.url).pathname)))).flat())
  expect(cached.length).toBeGreaterThan(0)
  expect(cached.filter(path => !path.startsWith('/app/'))).toEqual([])              // nada de /api en cache, nunca
  // Al volver la red la aplicacion se recupera SOLA (evento 'online'): nadie tiene que pulsar nada.
  await context.setOffline(false)
  await expect(page.getByTestId('home')).toBeVisible({ timeout: 20_000 })

  // Revocado en el PC principal: el dispositivo lo descubre, borra su credencial y vuelve a emparejar.
  expect((await asMain(request, `/auth/devices/${deviceId}/revoke`, {})).ok()).toBeTruthy()
  // Nadie pulsa nada: la revocacion corta el tiempo real y la siguiente lectura devuelve 401; la PWA lo descubre SOLA.
  await expect(page.getByTestId('pair')).toBeVisible({ timeout: 45_000 })
  await expect(page.getByTestId('notice')).toContainText('ya no está emparejado')
  await checkScreen(page, 'revoked')
  await page.reload()
  await expect(page.getByTestId('pair')).toBeVisible()                              // la credencial se borro de verdad

  expect(leaks, 'the PWA received financial data').toEqual([])
  expect(violations, 'CSP violations in the console').toEqual([])
})

test('a kitchen device shows its station and a denied request says so', async ({ page, request }) => {
  const leaks: string[] = [], violations: string[] = []
  watch(page, leaks, violations)
  const kdsId = await pairThroughTheUi(page, request, deviceName('kds'), 'kitchen', 'cold')
  await expect(page.getByTestId('role')).toContainText('Pantalla de cocina')
  await expect(page.getByTestId('role')).toContainText('Cocina fría')
  await checkScreen(page, 'kitchen home', true)
  // A10 (F9): desvincular va en Opciones, pide confirmacion y revoca EN EL SERVIDOR antes de olvidar la credencial.
  await press(page.getByTestId('options').locator('summary'))
  await press(page.getByTestId('unpair'))
  await press(page.getByTestId('unpair-confirm'))
  await expect(page.getByTestId('pair')).toBeVisible()
  const afterUnpair = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; revokedAt: string | null; online: boolean }>
  expect(afterUnpair.find(d => d.id === kdsId)).toMatchObject({ online: false })
  expect(afterUnpair.find(d => d.id === kdsId)!.revokedAt).not.toBeNull()

  const issued = await (await asMain(request, '/auth/pairings', {})).json()
  // PWA ya abierta y se escanea el QR: solo cambia el fragmento, sin recarga. Un fragmento hostil se ignora.
  await page.evaluate(code => { location.hash = `#pair=${code}` }, issued.code)
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)
  await page.evaluate(() => { location.hash = '#pair=<img src=x onerror=alert(1)>' })
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)
  const denied = deviceName('denied')
  await page.fill('input[name=deviceName]', denied)
  await press(page.getByTestId('do-pair'))
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === denied)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  await asMain(request, `/auth/pairings/${pairingId}/deny`, {})
  await expect(page.getByTestId('pair-status')).toContainText('denegada', { timeout: 15_000 })
  await page.fill('input[name=code]', 'codigo-inventado-0000000000')
  await press(page.getByTestId('do-pair'))
  await expect(page.getByTestId('pair-status')).toContainText('no válido')
  await checkScreen(page, 'pairing after a denial')
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})

test('a server that is not ready when the app opens never freezes it on Cargando (F8)', async ({ page, request }) => {
  const leaks: string[] = [], violations: string[] = []
  watch(page, leaks, violations)
  await pairThroughTheUi(page, request, deviceName('503'), 'service', 'sala-1')
  // Siguiente apertura con el servidor "no listo" (p. ej. 503 con PostgreSQL caido): se dice, no se borra la credencial y se reintenta solo.
  let failures = 0
  await page.route('**/api/native/v1/session', route => { failures++; return route.fulfill({ status: 503, contentType: 'application/json', body: '{"error":"storage_unavailable"}' }) })
  await page.reload()
  await expect(page.getByTestId('offline')).toBeVisible()
  await expect(page.getByTestId('notice')).toContainText('Servidor no listo')
  await checkScreen(page, 'server not ready')
  expect(failures).toBeGreaterThan(0)
  await page.unroute('**/api/native/v1/session')
  await expect(page.getByTestId('home')).toBeVisible({ timeout: 25_000 })            // el reintento periodico lo recupera sin tocar nada
  await expect(page.getByTestId('notice')).toHaveCount(0)
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})
