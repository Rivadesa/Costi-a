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
  tenantId: string
  companyId: string
  locationId: string
  installationId: string
  serverVersion: string
  demo: boolean
  actions: string[]
}
export interface CatalogItem { id: string; name: string; presentation: string }

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
