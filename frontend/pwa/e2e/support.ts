import { expect, test, type APIRequestContext, type Browser, type BrowserContext, type Locator, type Page } from '@playwright/test'
import { randomBytes } from 'node:crypto'
import { deflateSync } from 'node:zlib'

// C3a: lo comun de los specs E2E, que corren contra el MOTOR REAL en dos proyectos (escritorio y movil) con las MISMAS historias.
// El "PC principal" se simula por API con un token main; el navegador solo es el dispositivo. Todo JSON que llega al navegador pasa
// la guarda de dinero (ADR-007) y lo que se VE no lleva palabras de caja. Cada proyecto tiene su carril: sus mesas y sus nombres de
// dispositivo, porque los dos comparten el mismo motor (y la misma base) en una sola ejecucion.
export const API = '/api/native/v1'
export const MAIN = process.env.E2E_MAIN_TOKEN ?? ''
export const RUN = process.env.E2E_RUN ?? randomBytes(3).toString('hex')
const MONEY = /(price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff)/i
// Lo que el camarero VE: palabras enteras (board.spec desde D6.2). En cocina, incluso dentro de otra palabra (kitchen.spec, D6.4).
const MONEY_WORDS = /€|\bprecio\b|\bsaldo\b|\bpago\b|\bcuenta\b/i
const MONEY_INSIDE = /€|precio|saldo|pago|cuenta/i

export const post = (request: APIRequestContext, token: string, path: string, data: unknown) =>
  request.post(API + path, { data, headers: { Authorization: `Bearer ${token}`, 'Idempotency-Key': randomBytes(16).toString('hex') } })
export const asMain = (request: APIRequestContext, path: string, data?: unknown) =>
  data === undefined ? request.get(API + path, { headers: { Authorization: `Bearer ${MAIN}` } }) : post(request, MAIN, path, data)

export type ServiceRead = {
  version: number
  data: { state: string; courses: Array<{ id: string; state: string; preparations: Array<{ id: string; stationId: string; state: string; reviewPending?: boolean }> }> }
}
export const read = async (request: APIRequestContext, serviceId: string) => (await asMain(request, `/dining/services/${serviceId}`)).json() as Promise<ServiceRead>
// Una orden del PC principal (o de cocina) por API, con la version leida justo antes.
export async function command(request: APIRequestContext, serviceId: string, action: string, fields: Record<string, unknown> = {}): Promise<void> {
  const response = await asMain(request, `/dining/services/${serviceId}/commands/${action}`, { expectedVersion: (await read(request, serviceId)).version, ...fields })
  expect(response.ok(), `${action} -> ${response.status()}`).toBeTruthy()
}
// El PC principal abre una mesa. menuId: el alias de antes de E4a, como manda la PWA.
export async function openTable(request: APIRequestContext, tableId: string, pax: number, offerId = 'LAB-TASTING'): Promise<string> {
  const response = await asMain(request, '/dining/services', { tableId, pax, offerId, menuId: offerId })
  expect(response.ok(), `open ${tableId} -> ${response.status()}`).toBeTruthy()
  return (await response.json()).serviceId as string
}

export function moneyKeys(value: unknown, path = '$'): string[] {
  if (Array.isArray(value)) return value.flatMap((item, index) => moneyKeys(item, `${path}[${index}]`))
  if (value !== null && typeof value === 'object')
    return Object.entries(value).flatMap(([key, inner]) => [...(MONEY.test(key) ? [`${path}.${key}`] : []), ...moneyKeys(inner, `${path}.${key}`)])
  return []
}
// Todo JSON de /api/ que recibe la pagina se inspecciona (ninguna clave de dinero) y la consola delata cualquier violacion de la CSP.
// C3b: ademas, cada evento securitypolicyviolation de la pagina se escribe en la consola (las fotos van por blob:; nada mas debe entrar).
// Devuelve la promesa del guion de arranque: quien la espera tiene el escucha puesto antes de la primera navegacion.
export function watch(page: Page, leaks: string[], violations: string[]): Promise<void> {
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin cuerpo JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
  return page.addInitScript(() => {
    document.addEventListener('securitypolicyviolation', event =>
      console.error(`Refused to load (securitypolicyviolation) ${event.violatedDirective} ${event.blockedURI}`))
  }).then(() => undefined)
}
// Ni importes ni palabras de caja en lo que se ve en ESTA pantalla (el texto pintado de <main>, no el HTML).
async function noMoneyWords(page: Page, where: string, rule = MONEY_WORDS): Promise<void> {
  expect(await page.locator('main').innerText(), `money words on ${where}`).not.toMatch(rule)
}
// Cada pantalla que se visita: sin palabras de caja, sin desplazamiento lateral (tampoco en el movil de 375 px) y con objetivos
// tactiles de 48 px de alto y de ancho como minimo; en cocina, sus botones de accion y la nota de revision, de 64 px (manos ocupadas).
export async function checkScreen(page: Page, where: string, kitchen = false): Promise<void> {
  await noMoneyWords(page, where, kitchen ? MONEY_INSIDE : MONEY_WORDS)
  const [scroll, width] = await page.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth])
  expect(scroll, `horizontal scroll on ${where}`).toBeLessThanOrEqual(width)
  await targetsOf(page, where, 'main button, main summary, main input, main select', 48)
  if (kitchen) await targetsOf(page, where, 'main [data-testid^="do-"], main .review input', 64)
}
async function targetsOf(page: Page, where: string, selector: string, min: number): Promise<void> {
  // Lo que no se pinta (dentro de un <details> cerrado, por ejemplo) mide 0 x 0 y no es un objetivo.
  const small = await page.locator(selector).evaluateAll((elements, least) => elements.flatMap(element => {
    const box = element.getBoundingClientRect()
    if (box.width === 0 && box.height === 0) return []
    const name = element.getAttribute('data-testid') ?? element.getAttribute('name') ?? (element.textContent ?? '').trim().slice(0, 40)
    return Math.round(box.height) < least || Math.round(box.width) < least ? [`${element.tagName.toLowerCase()} ${name}: ${Math.round(box.width)} x ${Math.round(box.height)}`] : []
  }), min)
  expect(small, `touch targets under ${min} px on ${where}`).toEqual([])
}
// El proyecto es el aparato que dice ser: ancho de pantalla y tactil (el movil no puede volverse escritorio sin que se note).
export async function checkDevice(page: Page): Promise<void> {
  const use = test.info().project.use
  expect(await page.evaluate(() => ({ width: window.innerWidth, touch: navigator.maxTouchPoints > 0 })))
    .toEqual({ width: use.viewport?.width, touch: use.hasTouch === true })
}

// ---- Carriles: mesas y nombres propios de cada proyecto (M2 es de pwa_http.py; M1 queda libre) ----
export type Table = { id: string; name: string }
export type Lane = { board: Table; commands: Table; kitchen: Table; tag: string }
const table = (n: number): Table => ({ id: `M${n}`, name: `Mesa ${n}` })
const LANES: Record<string, Lane> = {
  desktop: { board: table(6), commands: table(7), kitchen: table(8), tag: 'pc' },
  mobile: { board: table(3), commands: table(4), kitchen: table(5), tag: 'movil' },
}
export function lane(): Lane {
  const found = LANES[test.info().project.name]
  if (!found) throw new Error(`El proyecto ${test.info().project.name} no tiene mesas propias (support.ts, LANES)`)
  return found
}
// Nombre de dispositivo unico por ejecucion Y por proyecto: los dos proyectos emparejan contra el mismo motor.
export const deviceName = (kind: string) => `e2e-${kind}-${lane().tag}-${RUN}`

// En el proyecto tactil se TOCA (eventos touch de verdad); en el de escritorio, clic de raton.
export const press = (target: Locator) => test.info().project.use.hasTouch === true ? target.tap() : target.click()

// Otro dispositivo = otro contexto (IndexedDB propio) con el MISMO aparato que el proyecto: pantalla, tactil y agente.
export function deviceContext(browser: Browser): Promise<BrowserContext> {
  const use = test.info().project.use
  return browser.newContext({ baseURL: use.baseURL, serviceWorkers: 'allow', viewport: use.viewport, deviceScaleFactor: use.deviceScaleFactor,
    isMobile: use.isMobile, hasTouch: use.hasTouch, userAgent: use.userAgent })
}

// C3c: cada rol tiene su aplicacion, en su carpeta y con su base IndexedDB (src/apps.ts): Costina Sala para la sala (y el `main`
// antiguo) y Costina Cocina para cocina.
export const appOf = (role: string): 'sala' | 'cocina' => role === 'kitchen' ? 'cocina' : 'sala'
export const databaseOf = (app: 'sala' | 'cocina') => `costina-${app}`
// El codigo de emparejamiento, como lo pide el PC desde C3c: el CORTO, que tambien se puede teclear (iPhone y iPad desde el icono).
export const issueCode = async (request: APIRequestContext): Promise<string> => (await (await asMain(request, '/auth/pairings', { typed: true })).json()).code as string

// La solicitud que espera aprobacion en el PC principal, por el nombre del dispositivo.
export async function pendingPairing(request: APIRequestContext, name: string): Promise<string> {
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === name)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  return pairingId
}

// Emparejar por la pantalla, como en el local: el QR abre la aplicacion del rol con el codigo en el fragmento; nombre, solicitud y
// aprobacion en el PC principal.
export async function pairThroughTheUi(page: Page, request: APIRequestContext, name: string, role: string, station: string): Promise<string> {
  const code = await issueCode(request)
  await page.goto(`/app/${appOf(role)}/#pair=${code}`)
  await expect(page.locator('input[name=code]')).toHaveValue(code)                 // el QR lleva el codigo en el fragmento
  await checkScreen(page, 'pairing')
  await page.fill('input[name=deviceName]', name)
  await press(page.getByTestId('do-pair'))
  await expect(page.getByTestId('pair-status')).toContainText('Esperando la aprobación')
  const pairingId = await pendingPairing(request, name)
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role, station })).ok()).toBeTruthy()
  await expect(page.getByTestId('home')).toBeVisible({ timeout: 15_000 })
  return deviceIdOf(request, name)
}
export const deviceIdOf = async (request: APIRequestContext, name: string): Promise<string> =>
  ((await (await asMain(request, '/auth/devices')).json()) as Array<{ id: string; name: string }>).find(d => d.name === name)!.id

// Un dispositivo emparejado SOLO por API (como si lo hubiera emparejado una version anterior): reclama, el PC aprueba y recoge el token.
export async function pairByApi(request: APIRequestContext, name: string, role: string, station: string): Promise<string> {
  const code = await issueCode(request)
  const key = () => ({ 'Idempotency-Key': randomBytes(16).toString('hex') })
  const claimed = await request.post(API + '/auth/pairings/claim', { data: { code, deviceName: name }, headers: key() })
  expect(claimed.ok(), `claim -> ${claimed.status()}`).toBeTruthy()
  const { pairingId, pollSecret } = await claimed.json() as { pairingId: string; pollSecret: string }
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role, station })).ok()).toBeTruthy()
  const collected = await request.post(API + `/auth/pairings/${pairingId}/collect`, { data: { pollSecret }, headers: key() })
  expect(collected.ok(), `collect -> ${collected.status()}`).toBeTruthy()
  return (await collected.json() as { deviceToken: string }).deviceToken
}

// Lo que hay DE VERDAD guardado en el dispositivo (IndexedDB de SU aplicacion), no lo que pinta la pantalla. Sin crear la base: si no
// existe, 'absent'. Revision C3c: cualquier otro fallo al abrirla o al leer es 'error', nunca «no hay nada».
export const stored = (page: Page, database: string, store: 'device' | 'pending', key: string) => page.evaluate(([database, store, key]) => new Promise<unknown>(resolve => {
  let absent = false
  const open = indexedDB.open(database)
  open.onupgradeneeded = () => { absent = true; open.transaction?.abort() }
  open.onsuccess = () => {
    if (!open.result.objectStoreNames.contains(store)) { open.result.close(); resolve('error'); return }
    const get = open.result.transaction(store).objectStore(store).get(key)
    get.onsuccess = () => { open.result.close(); resolve(get.result) }
    get.onerror = () => { open.result.close(); resolve('error') }
  }
  open.onerror = () => resolve(absent ? 'absent' : 'error')
}), [database, store, key] as const)
// null y '' quieren decir «la base existe y ese hueco esta vacio». Una base que no existe o que no se pudo leer sale como '<absent>' o
// '<error>': asi un toBeNull() o un toBe('') nunca pasan por un fallo o por mirar la base equivocada.
const unread = (value: unknown) => value === 'absent' || value === 'error' ? `<${value}>` : null
export const storedOrder = async (page: Page, database = databaseOf('sala')) => {
  const order = await stored(page, database, 'pending', 'uncertain') as { description?: unknown } | undefined
  return unread(order) ?? (order ? String(order.description) : null)
}
export const storedToken = async (page: Page, database: string) => {
  const credential = await stored(page, database, 'device', 'credential') as { token?: unknown } | undefined
  return unread(credential) ?? (credential ? String(credential.token ?? '') : '')
}

// RESPUESTA PERDIDA de verdad: el servidor aplica la orden y la respuesta nunca llega. `lost` se resuelve cuando eso YA ocurrio:
// la orden pendiente se ve ANTES del primer intento (por diseno), asi que sin esperar a `lost` el test podia retirar la ruta
// con el manejador aun en route.fetch(), y Playwright daba la ruta por gestionada ("Route is already handled").
export async function loseResponses(page: Page, pattern: string): Promise<{ lost: Promise<void> }> {
  let first: () => void = () => undefined
  const lost = new Promise<void>(resolve => { first = resolve })
  await page.route(pattern, async route => { try { await route.fetch(); await route.abort('connectionreset') } finally { first() } })
  return { lost }
}

// E4e (ADR-016): un pase con platos por revisar no sale. El PC principal (persona, sin estacion) decide los de ese pase para que pueda
// salir; lo usan las capturas que declaran alergias antes de enviar.
export async function reviewCourse(request: APIRequestContext, serviceId: string, index: number, note = 'no le afecta'): Promise<void> {
  const course = (await read(request, serviceId)).data.courses[index]
  for (const preparation of course.preparations.filter(p => p.reviewPending))
    await command(request, serviceId, 'review-preparation', { courseId: course.id, itemId: preparation.id, decision: 'unaffected', note })
}

// El PC principal deja una mesa libre: omite lo pendiente, termina y libera (como finish_and_release de pwa_http.py).
export async function release(request: APIRequestContext, serviceId: string): Promise<void> {
  for (const course of (await read(request, serviceId)).data.courses)
    if (course.state === 'Pending') await command(request, serviceId, 'skip', { courseId: course.id, reason: 'fin de la prueba' })
  if ((await read(request, serviceId)).data.state === 'Open') await command(request, serviceId, 'start')
  await command(request, serviceId, 'complete')
  const board = await (await asMain(request, '/dining/board')).json() as Array<{ service: { id: string }; occupancyVersion: number }>
  const entry = board.find(candidate => candidate.service.id === serviceId)!
  const released = await asMain(request, `/dining/occupancy/${serviceId}/release`, { expectedVersion: entry.occupancyVersion, reason: 'fin de la prueba' })
  expect(released.ok(), `release -> ${released.status()}`).toBeTruthy()
}

// ---- C3b: bodega y bebidas de prueba, por API como el PC principal (el laboratorio no trae ningun vino con «albar» ni fotos) ----
export const catalogCommand = (request: APIRequestContext, action: string, data: Record<string, unknown>) => asMain(request, `/erp/catalog/commands/${action}`, data)
export async function ensureCategory(request: APIRequestContext, id: string, name: string, parentId: string | null): Promise<void> {
  const created = await catalogCommand(request, 'category-create', { id, name, ...(parentId ? { parentId } : {}) })
  expect([200, 409], `category ${id} -> ${created.status()}`).toContain(created.status())
}
export type Drink = {
  id: string; name: string; categoryId: string; reference?: string
  presentations: Array<{ id: string; name: string; cents: number }>; glassCents?: number; image?: Buffer
}
// Idempotente: los dos proyectos (y las capturas) comparten el motor; lo que ya existe se deja como esta.
export async function ensureDrink(request: APIRequestContext, drink: Drink): Promise<void> {
  const sold = await (await asMain(request, '/catalog')).json() as Array<{ id: string; presentationId: string; imageSha?: string | null }>
  const own = sold.filter(row => row.id === drink.id)
  if (own.length >= drink.presentations.length + (drink.glassCents ? 1 : 0) && (!drink.image || own.some(row => row.imageSha))) return
  const created = await catalogCommand(request, 'product-create', { id: drink.id, name: drink.name, categoryId: drink.categoryId, taxId: 'iva-21', ...(drink.reference ? { reference: drink.reference } : {}) })
  expect([200, 409], `product ${drink.id} -> ${created.status()}`).toContain(created.status())
  for (const [index, presentation] of drink.presentations.entries()) {
    if (presentation.id !== 'unit') {
      const made = await catalogCommand(request, 'presentation-create', { productId: drink.id, id: presentation.id, name: presentation.name, sort: index + 1 })
      expect([200, 409], `presentation ${drink.id}/${presentation.id} -> ${made.status()}`).toContain(made.status())
    }
    expect((await catalogCommand(request, 'price-set', { tariffId: 'general', productId: drink.id, presentationId: presentation.id, priceCents: presentation.cents })).ok()).toBeTruthy()
  }
  if (drink.glassCents) expect((await catalogCommand(request, 'glass-sale-set', { items: [{ productId: drink.id, priceCents: drink.glassCents }] })).ok()).toBeTruthy()
  if (drink.image) expect((await catalogCommand(request, 'product-image-set', { productId: drink.id, contentType: 'image/png', data: drink.image.toString('base64') })).ok()).toBeTruthy()
}

// Un PNG de verdad (el navegador lo tiene que DECODIFICAR para pintarlo): RGB de 8 bits, sin filtro, comprimido con zlib.
const CRC = Array.from({ length: 256 }, (_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; return c >>> 0 })
const crc32 = (data: Buffer) => { let c = 0xFFFFFFFF; for (const byte of data) c = CRC[(c ^ byte) & 0xFF] ^ (c >>> 8); return (c ^ 0xFFFFFFFF) >>> 0 }
export function png(width: number, height: number, pixel: (x: number, y: number) => [number, number, number]): Buffer {
  const raw = Buffer.alloc((width * 3 + 1) * height)
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) raw.set(pixel(x, y), y * (width * 3 + 1) + 1 + x * 3)
  const chunk = (type: string, data: Buffer) => {
    const length = Buffer.alloc(4); length.writeUInt32BE(data.length)
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data])
    const check = Buffer.alloc(4); check.writeUInt32BE(crc32(body))
    return Buffer.concat([length, body, check])
  }
  const header = Buffer.alloc(13); header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 2
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), chunk('IHDR', header), chunk('IDAT', deflateSync(raw)), chunk('IEND', Buffer.alloc(0))])
}
// Una botella sencilla sobre blanco, como las fotos de producto de la tienda: capsula, cuello, hombros, cuerpo y etiqueta.
export function bottlePng(rgb: [number, number, number]): Buffer {
  const dark = rgb.map(value => Math.round(value * .55)) as [number, number, number]
  return png(96, 128, (x, y) => {
    const dx = Math.abs(x - 47.5)
    const half = y < 12 ? -1 : y < 44 ? 6 : y < 58 ? 6 + (y - 44) * 13 / 14 : y < 118 ? 19 : y < 124 ? 19 - (y - 118) * 2 : -1
    if (dx > half) return [255, 255, 255]
    if (y < 24) return dark
    if (y >= 74 && y < 102 && dx < 15) return [246, 241, 230]
    return rgb
  })
}
