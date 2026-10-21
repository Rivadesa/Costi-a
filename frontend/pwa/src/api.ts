import { assertNoMoney } from './money-guard'

// Cliente de /api/native/v1 en el MISMO origen. Sin reglas de negocio: lo permitido lo dicen las
// affordances del servidor. Todo POST lleva Idempotency-Key. Nunca se registran tokens ni cuerpos.
const PREFIX = '/api/native/v1'

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly code: string) {
    super(`HTTP ${status}: ${code}`)
  }
}
export class NetworkError extends Error {}

export interface Session {
  role: 'main' | 'service' | 'kitchen'
  actor: string
  station: string | null
  stationName?: string | null   // A4: nombre legible de la estacion (un servidor anterior no lo envia)
  tenantId: string
  companyId: string
  locationId: string
  installationId: string
  serverVersion: string
  demo: boolean
  actions: string[]
  modules: string[]   // ADR-012: modulos activos; las vistas se montan solo para ellos
}
// E3: un vendible = producto + presentacion (presentationId, categoria opcionales: un servidor anterior no los envia). Nunca dinero.
// E3.2/E3.3 (los usa C3b): huella de la foto, seccion ('cocina' | 'bodega'), familia, ruta de categorias sin la seccion, referencia
// y si es la copa (lo decide el servidor). Todos opcionales.
export interface CatalogItem {
  id: string; presentationId?: string; name: string; presentation: string; categoryId?: string | null; categoryName?: string | null
  imageSha?: string | null; section?: string | null; family?: string | null; categoryPath?: string[] | null; reference?: string | null; glass?: boolean
}
export const catalogKey = (item: CatalogItem): string => item.presentationId ? `${item.id}/${item.presentationId}` : item.id

export type Fetch = typeof fetch

export function newKey(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('')
}

export async function call<T>(fetcher: Fetch, path: string, options: { token?: string; body?: unknown; key?: string } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.token) headers.Authorization = `Bearer ${options.token}`
  let init: RequestInit = { method: 'GET', headers, cache: 'no-store', credentials: 'omit', redirect: 'error' }
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    headers['Idempotency-Key'] = options.key ?? newKey()
    init = { ...init, method: 'POST', body: JSON.stringify(options.body) }
  }
  let response: Response
  try { response = await fetcher(PREFIX + path, init) }
  catch { throw new NetworkError('Servidor sin respuesta.') }
  let payload: unknown = null
  try { payload = await response.json() } catch { /* cuerpo vacio o no JSON: se decide por el estado */ }
  if (!response.ok) {
    const code = payload !== null && typeof payload === 'object' && 'error' in payload ? String((payload as { error: unknown }).error) : 'unrecognised_response'
    throw new ApiError(response.status, code)
  }
  assertNoMoney(payload)
  return payload as T
}

export const getSession = (fetcher: Fetch, token: string) => call<Session>(fetcher, '/session', { token })
export const getCatalog = (fetcher: Fetch, token: string) => call<CatalogItem[]>(fetcher, '/catalog', { token })

// C3b: lectura CONDICIONAL del catalogo operativo (E3.3: GET /catalog con ETag). Con la lista anterior y su ETag se manda
// If-None-Match A MANO: todo va con cache 'no-store' (la cache HTTP del navegador no interviene, y asi el 304 llega aqui tal cual).
// Un 304 no trae cuerpo: se devuelve la lectura ANTERIOR, la misma. Cada 200 es una lista nueva y pasa ENTERA por la guarda de
// dinero antes de usarse (ADR-007); si la trae, se rechaza y quien llama conserva la anterior. Un 304 sin lista anterior, o un cuerpo
// que no es una lista, es una respuesta no reconocida.
export interface CatalogRead { items: CatalogItem[]; etag: string | null }
export async function readCatalog(fetcher: Fetch, token: string, previous: CatalogRead | null = null): Promise<CatalogRead> {
  const headers: Record<string, string> = { Accept: 'application/json', Authorization: `Bearer ${token}` }
  if (previous?.etag) headers['If-None-Match'] = previous.etag
  let response: Response
  try { response = await fetcher(PREFIX + '/catalog', { method: 'GET', headers, cache: 'no-store', credentials: 'omit', redirect: 'error' }) }
  catch { throw new NetworkError('Servidor sin respuesta.') }
  if (response.status === 304) {
    if (previous?.etag) return previous
    throw new ApiError(304, 'unrecognised_response')
  }
  let payload: unknown = null
  try { payload = await response.json() } catch { /* cuerpo vacio o no JSON: se decide por el estado */ }
  if (!response.ok) {
    const code = payload !== null && typeof payload === 'object' && 'error' in payload ? String((payload as { error: unknown }).error) : 'unrecognised_response'
    throw new ApiError(response.status, code)
  }
  assertNoMoney(payload)
  if (!Array.isArray(payload)) throw new ApiError(response.status, 'unrecognised_response')
  return { items: payload as CatalogItem[], etag: response.headers.get('ETag') }
}
