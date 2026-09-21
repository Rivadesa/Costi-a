import { newKey, type Fetch } from './api'
import { assertNoMoney } from './money-guard'
import type { PendingCommand, PendingStore } from './pending'

// Ejecutor de comandos de dominio con ORDEN INCIERTA durable (D6.3, espejo de D3.3/D3.4):
//  1. La orden se guarda ANTES del primer intento. Si no se puede guardar, NO se envia.
//  2. Mientras exista una orden sin confirmar no se admite ninguna otra mutacion.
//  3. El reintento reenvia los MISMOS bytes con la MISMA clave: el servidor no duplica efectos.
//  4. La incertidumbre solo se cierra con (a) exito cuyo eco de Idempotency-Key coincide, o (b) rechazo
//     DEFINITIVO: 403/404/409/422 con un error JSON reconocible, que no sea de almacenamiento/concurrencia,
//     y con el eco de la clave. Cualquier otra cosa (corte, 5xx, 429, HTML de un proxy, eco ausente) la mantiene.
//  5. Un 401 NO la cierra: el primer intento pudo aplicarse antes de la revocacion. Queda guardada y se
//     concilia cuando el dispositivo vuelva a estar emparejado con el mismo nombre (mismo actor).
//  6. Una orden de OTRA instalacion o ilegible jamas se reenvia: se informa y solo admite conciliar o descartar.
const PREFIX = '/api/native/v1'
const INCONCLUSIVE = new Set(['storage_conflict', 'idempotency_conflict', 'operation_unconfirmed'])
const DEFINITIVE = new Set([403, 404, 409, 422])

export type Outcome =
  | { kind: 'confirmed'; response: unknown }
  | { kind: 'rejected'; status: number; code: string }
  | { kind: 'unconfirmed'; reason: 'network' | 'server' | 'unverifiable' }
  | { kind: 'unauthorized' }

export interface RunnerState {
  pending: PendingCommand | null
  blocked: { key: string | null; reason: 'unreadable' | 'foreign' } | null
  busy: boolean
  lastFailure: 'network' | 'server' | 'unverifiable' | null
  notice: string
}

export interface RunnerDeps {
  fetcher: Fetch
  store: PendingStore
  token: () => string
  identity: { installationId: string; actor: string }
  lock?: <T>(work: () => Promise<T>) => Promise<T>     // exclusion entre pestanas (Web Locks); sin ella, entre llamadas de esta
}

export const REJECTIONS: Record<string, string> = {
  version_conflict: 'Otro puesto cambio esta mesa a la vez. Se ha vuelto a leer: revisa y repite si sigue haciendo falta.',
  restrictions_unreviewed: 'Hay elaboraciones pendientes de revision por cocina tras un cambio de restricciones.',
  restrictions_unacknowledged: 'Cocina aun no ha revisado el cambio de restricciones.',
  wrong_station: 'Esta accion no corresponde a la estacion de este dispositivo.',
  account_closed: 'La cuenta de esta mesa esta cerrada: avisa en el PC principal.',
  forbidden: 'Este dispositivo no tiene permiso para esa accion.',
}

export class CommandRunner {
  // window.fetch exige invocarse SIN receptor: llamarlo como metodo de un objeto (deps.fetcher(...)) lanza
  // "Illegal invocation" en el navegador. Se guarda como funcion suelta.
  private readonly http: Fetch
  constructor(public readonly state: RunnerState, private readonly deps: RunnerDeps) {
    const fetcher = deps.fetcher
    this.http = (input, init) => fetcher(input, init)
  }

  get blocksMutations(): boolean { return this.state.busy || this.state.pending !== null || this.state.blocked !== null }

  // Al arrancar o volver a primer plano: lo que quedo de una sesion anterior manda sobre todo lo demas.
  async restore(): Promise<void> {
    const loaded = await this.deps.store.load()
    this.state.pending = null; this.state.blocked = null
    if (loaded.kind === 'unreadable') this.state.blocked = { key: loaded.key, reason: 'unreadable' }
    else if (loaded.kind === 'restored') {
      if (loaded.command.installationId !== this.deps.identity.installationId) this.state.blocked = { key: loaded.command.key, reason: 'foreign' }
      else {
        this.state.pending = loaded.command
        // Restaurada de otra sesion del navegador: en cuanto el servidor responda se reintenta sola (mismos bytes y clave).
        this.state.lastFailure ??= 'network'
      }
    }
  }

  async send(path: string, body: Record<string, unknown>, description: string): Promise<Outcome> {
    if (this.blocksMutations) throw new Error('Hay una orden sin resolver: no se admite otra.')
    const command: PendingCommand = { key: newKey(), path, body: JSON.stringify(body), description, createdAt: new Date().toISOString(), ...this.deps.identity }
    this.state.busy = true
    try {
      try { await this.deps.store.save(command) }
      catch { this.state.notice = 'No se pudo guardar la orden en este dispositivo, asi que NO se ha enviado. Libera espacio o revisa el navegador.'; throw new Error('pending store unavailable') }
      this.state.pending = command
      return await this.attempt(command)
    } finally { this.state.busy = false }
  }

  async retry(): Promise<Outcome | null> {
    const command = this.state.pending
    if (!command || this.state.busy) return null
    this.state.busy = true
    try { return await this.attempt(command) } finally { this.state.busy = false }
  }

  // Pregunta al servidor por la clave (solo el mismo actor). Si consta, la orden SE APLICO y se cierra.
  // Si no consta, la orden normal sigue pendiente (un reintento identico es seguro); una ilegible o ajena
  // queda habilitada para descarte explicito.
  async reconcile(): Promise<'applied' | 'not_found' | 'unknown'> {
    const key = this.state.pending?.key ?? this.state.blocked?.key
    if (!key || this.state.busy) return 'unknown'
    this.state.busy = true
    try {
      const response = await this.http(`${PREFIX}/commands/${encodeURIComponent(key)}`, this.init('GET'))
      if (!response.ok) return 'unknown'
      const found = (await response.json() as { found?: boolean }).found === true
      if (found) {
        await this.deps.store.clear(key); this.state.pending = null; this.state.blocked = null
        this.state.notice = 'El servidor confirma que esa orden SI se aplico. No la repitas.'
        return 'applied'
      }
      this.state.notice = 'Al servidor no le consta esa orden.'
      return 'not_found'
    } catch { return 'unknown' } finally { this.state.busy = false }
  }

  // Solo para lo que NO se puede reenviar (ilegible o de otra instalacion), y siempre por decision de la persona.
  async discardBlocked(): Promise<void> {
    if (!this.state.blocked) return
    await this.deps.store.discard(); this.state.blocked = null
  }

  private init(method: 'GET' | 'POST', command?: PendingCommand): RequestInit {
    const headers: Record<string, string> = { Accept: 'application/json', Authorization: `Bearer ${this.deps.token()}` }
    if (command) { headers['Content-Type'] = 'application/json'; headers['Idempotency-Key'] = command.key }
    return { method, headers, body: command?.body, cache: 'no-store', credentials: 'omit', redirect: 'error' }
  }

  private attempt(command: PendingCommand): Promise<Outcome> {
    const work = async (): Promise<Outcome> => {
      // Otra pestana pudo resolver o sustituir la orden mientras esperabamos el turno.
      const current = await this.deps.store.load()
      if (current.kind !== 'restored' || current.command.key !== command.key) { await this.restore(); return { kind: 'unconfirmed', reason: 'unverifiable' } }
      let response: Response
      try { response = await this.http(PREFIX + command.path, this.init('POST', command)) }
      catch { return this.keep('network') }
      const echoed = response.headers.get('Idempotency-Key') === command.key
      let payload: unknown = null
      try { payload = await response.json() } catch { /* sin JSON reconocible */ }
      if (response.status === 401) { this.state.lastFailure = null; return { kind: 'unauthorized' } }
      if (response.ok) {
        if (!echoed || payload === null) return this.keep('unverifiable')
        assertNoMoney(payload)
        await this.close(command.key)
        return { kind: 'confirmed', response: payload }
      }
      const code = payload !== null && typeof payload === 'object' && typeof (payload as { error?: unknown }).error === 'string' ? (payload as { error: string }).error : ''
      if (DEFINITIVE.has(response.status) && echoed && code !== '' && !INCONCLUSIVE.has(code)) {
        await this.close(command.key)
        this.state.notice = REJECTIONS[code] ?? `El servidor rechazo la orden (${code}). No se ha aplicado.`
        return { kind: 'rejected', status: response.status, code }
      }
      return this.keep(response.status >= 500 || response.status === 429 || INCONCLUSIVE.has(code) ? 'server' : 'unverifiable')
    }
    return this.deps.lock ? this.deps.lock(work) : work()
  }

  private keep(reason: 'network' | 'server' | 'unverifiable'): Outcome { this.state.lastFailure = reason; return { kind: 'unconfirmed', reason } }

  private async close(key: string): Promise<void> {
    await this.deps.store.clear(key)
    this.state.pending = null; this.state.lastFailure = null
  }
}

// Exclusion entre pestanas del mismo origen. Sin Web Locks (navegadores antiguos) se ejecuta sin ella:
// la comprobacion del almacen dentro de attempt() sigue evitando reenviar una orden ya sustituida.
export const tabLock = <T>(work: () => Promise<T>): Promise<T> =>
  typeof navigator !== 'undefined' && 'locks' in navigator ? navigator.locks.request('costina-command', () => work()) as Promise<T> : work()
