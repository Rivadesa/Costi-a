import { expect, test, type Browser, type Page } from '@playwright/test'
import {
  asMain, checkScreen, databaseOf, deviceIdOf, deviceName, issueCode, MAIN, pairByApi, pendingPairing, press, stored, storedOrder, storedToken, watch,
} from './support'

// C3c: dos aplicaciones, Costina Sala y Costina Cocina, y /app/ como enrutador. Los DOS caminos de instalacion, contra el MOTOR REAL:
// Android (el QR abre la aplicacion que toca y se empareja alli) e iPhone/iPad (el QR abre Safari, que no empareja y explica como
// anadir el icono; desde el icono se teclea el codigo corto). Ademas: un codigo de la otra aplicacion no deja nada guardado ni
// huerfano, un dispositivo emparejado con la version anterior llega a su aplicacion y adopta lo suyo, y el service worker viejo se
// retira. Ninguna prueba usa mesas: no tocan los carriles.
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'

// Un iPhone simulado en Chromium: agente de iOS y `navigator.standalone`, que solo existe en iOS (false en Safari, true en el icono).
async function iphone(browser: Browser, standalone: boolean): Promise<Page> {
  const context = await browser.newContext({ baseURL: test.info().project.use.baseURL, serviceWorkers: 'allow', userAgent: IPHONE,
    viewport: { width: 390, height: 844 }, deviceScaleFactor: 3, isMobile: true, hasTouch: true })
  await context.addInitScript(value => { Object.defineProperty(Navigator.prototype, 'standalone', { configurable: true, get: () => value }) }, standalone)
  return context.newPage()
}

test('the router asks whether the device is for the room or the kitchen and hands the QR code to the chosen app', async ({ page, request }) => {
  expect(MAIN, 'E2E_MAIN_TOKEN is required').not.toBe('')
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  const code = await issueCode(request)
  await page.goto(`/app/#pair=${code}`)                                              // un QR o un enlace de antes de C3c
  await expect(page.getByTestId('router-choose')).toContainText('¿Este dispositivo es para la sala o para la cocina?')
  await checkScreen(page, 'router choose')
  await press(page.getByTestId('route-cocina'))
  await expect(page).toHaveURL(new RegExp(`/app/cocina/#pair=${code}$`))
  await expect(page.getByTestId('app-name')).toHaveText('Costiña Cocina')
  await expect(page.locator('input[name=code]')).toHaveValue(code)
  await page.goBack()                                                                 // el codigo no se queda en el historial del enrutador
  expect(page.url()).not.toContain('/app/#pair=')
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})

test('a code approved for the other app saves nothing, leaves no orphan device on the server and says which code to ask for', async ({ page, request }) => {
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  const code = await issueCode(request)
  await page.goto(`/app/sala/#pair=${code}`)
  const name = deviceName('wrong-app')
  await page.fill('input[name=deviceName]', name)
  await press(page.getByTestId('do-pair'))
  expect((await asMain(request, `/auth/pairings/${await pendingPairing(request, name)}/approve`, { role: 'kitchen', station: 'pase' })).ok()).toBeTruthy()
  await expect(page.getByTestId('pair-status')).toHaveText('Este código era de Costiña Cocina: pide en el PC uno de Costiña Sala.', { timeout: 15_000 })
  await expect(page.getByTestId('pair')).toBeVisible()
  await expect(page.locator('input[name=code]')).toHaveValue('')
  await checkScreen(page, 'pairing with the code of the other app')
  expect(await storedToken(page, databaseOf('sala'))).toBe('')                        // nada guardado
  const id = await deviceIdOf(request, name)
  const devices = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; revokedAt: string | null }>
  expect(devices.find(device => device.id === id)!.revokedAt).not.toBeNull()          // dado de baja en el servidor: nada huerfano
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})

test('iPhone and iPad: Safari does not pair and explains how to add the icon; the icon pairs with the short code typed in two groups', async ({ browser, request }) => {
  const leaks: string[] = [], violations: string[] = []
  // 1) El QR abre Safari: ni campo de codigo ni «Solicitar emparejamiento», y el codigo sale de la direccion.
  const safari = await iphone(browser, false)
  await watch(safari, leaks, violations)
  const code = await issueCode(request)
  await safari.goto(`/app/sala/#pair=${code}`)
  await expect(safari.getByTestId('add-to-home')).toContainText('Añádela a la pantalla de inicio')
  await expect(safari.getByTestId('add-to-home')).toContainText('Compartir › Añadir a pantalla de inicio')
  await expect(safari.getByTestId('add-to-home')).toContainText('Abre el icono Costiña Sala y teclea el código corto que enseña el PC.')
  await expect(safari.getByTestId('do-pair')).toHaveCount(0)
  await expect(safari.locator('input[name=code]')).toHaveCount(0)
  await expect.poll(() => new URL(safari.url()).hash).toBe('')
  await checkScreen(safari, 'safari on iPhone')
  await safari.context().close()

  // 2) Desde el icono (otro almacen, como en iOS): se teclea el codigo que ensena el PC, en dos grupos y en minusculas.
  const icon = await iphone(browser, true)
  await watch(icon, leaks, violations)
  await icon.goto('/app/sala/')
  await expect(icon.getByTestId('pair')).toBeVisible()
  await checkScreen(icon, 'icon on iPhone')
  const name = deviceName('iphone')
  await icon.fill('input[name=code]', `${code.slice(0, 5)} ${code.slice(5)}`.toLowerCase())
  await icon.fill('input[name=deviceName]', name)
  await icon.getByTestId('do-pair').tap()
  expect((await asMain(request, `/auth/pairings/${await pendingPairing(request, name)}/approve`, { role: 'service', station: 'sala-1' })).ok()).toBeTruthy()
  await expect(icon.getByTestId('home')).toBeVisible({ timeout: 15_000 })
  await expect(icon.getByTestId('actor')).toHaveText(name)
  await expect(icon.getByTestId('role')).toHaveText('Sala 1')
  await checkScreen(icon, 'paired from the icon')
  await icon.context().close()
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})

test('a device paired before C3c reaches its app through /app/, which adopts its credential and its pending order', async ({ page, request }) => {
  const leaks: string[] = [], violations: string[] = []
  await watch(page, leaks, violations)
  const name = deviceName('c3b')
  const token = await pairByApi(request, name, 'service', 'sala-1')
  const installationId = (await (await asMain(request, '/session')).json() as { installationId: string }).installationId
  const order = { key: `c3c-${name}`, path: '/dining/services/no-existe/commands/pause', body: '{"expectedVersion":1,"reason":"prueba"}',
    description: 'Pausar servicio · Mesa de antes', createdAt: new Date().toISOString(), installationId, actor: `device:${name}` }
  await page.goto('/app/')
  await expect(page.getByTestId('router-none')).toBeVisible()
  // Lo que dejo C3b en este navegador: la base `costina` con la credencial y una orden sin confirmar.
  await page.evaluate(({ token, name, order }) => new Promise<void>((resolve, reject) => {
    const open = indexedDB.open('costina', 2)
    open.onupgradeneeded = () => { open.result.createObjectStore('device'); open.result.createObjectStore('pending') }
    open.onsuccess = () => {
      const transaction = open.result.transaction(['device', 'pending'], 'readwrite')
      transaction.objectStore('device').put({ token, deviceName: name, pairedAt: '2026-10-21T10:00:00Z' }, 'credential')
      transaction.objectStore('pending').put(order, 'uncertain')
      transaction.oncomplete = () => { open.result.close(); resolve() }
      transaction.onerror = () => reject(transaction.error)
    }
  }), { token, name, order })

  // La orden adoptada es una orden PROPIA (misma instalacion y mismo nombre): se reintenta sola con su MISMA clave (D6.3/D6.7). Aqui va
  // a un servicio que no existe, asi que el servidor la rechaza de forma definitiva y queda cerrada; lo que se prueba es que llego.
  const retried: string[] = []
  page.on('request', sent => { if (sent.method() === 'POST' && sent.url().endsWith('/commands/pause')) retried.push(sent.headers()['idempotency-key'] ?? '') })
  await page.goto('/app/')                                                            // el acceso directo de antes
  await expect(page).toHaveURL(/\/app\/sala\/$/)
  await expect(page.getByTestId('actor')).toHaveText(name)
  await expect.poll(() => retried, { timeout: 20_000 }).toContain(order.key)
  await expect.poll(() => storedOrder(page, databaseOf('sala')), { timeout: 20_000 }).toBeNull()
  await checkScreen(page, 'adopted')
  expect(await storedToken(page, databaseOf('sala'))).toBe(token)
  expect(await storedOrder(page, 'costina')).toBeNull()                               // salio de la vieja por su clave: nadie mas la reenvia
  expect(await storedToken(page, 'costina')).toBe(token)                              // la base vieja no se borra
  expect(await stored(page, databaseOf('cocina'), 'device', 'credential')).toBe('absent')   // ni se crea la de la otra
  expect(leaks).toEqual([]); expect(violations).toEqual([])
})

test('the old service worker of /app/ retires: it deletes only its own cache and unregisters itself', async ({ page }) => {
  await page.goto('/app/')
  await page.evaluate(async () => {
    await (await caches.open('costina-shell-v1')).put('/app/viejo', new Response('de antes'))
    await (await caches.open('costina-sala-v1')).put('/app/sala/', new Response('de Sala'))
    await navigator.serviceWorker.register('/app/sw.js', { scope: '/app/' })          // lo que dejo instalado C3b
  })
  await expect.poll(() => page.evaluate(async () => (await navigator.serviceWorker.getRegistrations()).map(r => new URL(r.scope).pathname)), { timeout: 15_000 }).toEqual([])
  await expect.poll(() => page.evaluate(async () => (await caches.keys()).sort())).toEqual(['costina-sala-v1'])
})
