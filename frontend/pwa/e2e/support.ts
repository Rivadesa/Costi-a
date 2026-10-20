import { expect, test, type APIRequestContext, type Browser, type BrowserContext, type Locator, type Page } from '@playwright/test'
import { randomBytes } from 'node:crypto'

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
export function watch(page: Page, leaks: string[], violations: string[]): void {
  page.on('response', async response => {
    if (!response.url().includes('/api/')) return
    try { leaks.push(...moneyKeys(await response.json()).map(found => `${response.url()} ${found}`)) } catch { /* sin cuerpo JSON */ }
  })
  page.on('console', message => { if (/Content Security Policy|Refused to/i.test(message.text())) violations.push(message.text()) })
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

// Emparejar por la pantalla, como en el local: codigo en el fragmento (el QR), nombre, solicitud y aprobacion en el PC principal.
export async function pairThroughTheUi(page: Page, request: APIRequestContext, name: string, role: string, station: string): Promise<string> {
  const issued = await (await asMain(request, '/auth/pairings', {})).json()
  await page.goto(`/app/#pair=${issued.code}`)
  await expect(page.locator('input[name=code]')).toHaveValue(issued.code)          // el QR lleva el codigo en el fragmento
  await checkScreen(page, 'pairing')
  await page.fill('input[name=deviceName]', name)
  await press(page.getByTestId('do-pair'))
  await expect(page.getByTestId('pair-status')).toContainText('Esperando la aprobación')
  let pairingId = ''
  await expect.poll(async () => {
    const pending = await (await asMain(request, '/auth/pairings/pending')).json() as Array<{ pairingId: string; deviceName: string }>
    pairingId = pending.find(p => p.deviceName === name)?.pairingId ?? ''
    return pairingId
  }, { timeout: 15_000 }).not.toBe('')
  expect((await asMain(request, `/auth/pairings/${pairingId}/approve`, { role, station })).ok()).toBeTruthy()
  await expect(page.getByTestId('home')).toBeVisible({ timeout: 15_000 })
  const devices = await (await asMain(request, '/auth/devices')).json() as Array<{ id: string; name: string }>
  return devices.find(d => d.name === name)!.id
}
