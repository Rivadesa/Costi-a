import { newKey, type Fetch } from './api'
import { otherActorAdvice } from './labels'
import { assertNoMoney } from './money-guard'
import type { PendingCommand, PendingStore } from './pending'

// Ejecutor de comandos de dominio con ORDEN INCIERTA durable (D6.3, espejo de D3.3/D3.4):
//  1. La orden se guarda ANTES del primer intento, RESERVANDO el unico hueco del dispositivo (D6.7: comprobar y escribir
//     en la misma transaccion). Si no se puede guardar, o el hueco ya tiene otra orden (de otra pestana), NO se envia.
//  2. Mientras exista una orden sin confirmar no se admite ninguna otra mutacion.
//  3. El reintento reenvia los MISMOS bytes con la MISMA clave: el servidor no duplica efectos.
//  4. La incertidumbre solo se cierra con (a) exito cuyo eco de Idempotency-Key coincide, o (b) rechazo
//     DEFINITIVO: 403/404/409/422 con un error JSON reconocible, que no sea de almacenamiento/concurrencia,
//     y con el eco de la clave. Cualquier otra cosa (corte, 5xx, 429, HTML de un proxy, eco ausente) la mantiene.
//  5. Un 401 NO la cierra: el primer intento pudo aplicarse antes de la revocacion. Queda guardada y se
//     concilia cuando el dispositivo vuelva a estar emparejado con el mismo nombre (mismo actor).
//  6. Una orden de OTRA instalacion, de OTRO actor o ilegible jamas se reenvia: se informa y solo admite conciliar o
//     descartar. (D6.7) La de otro actor (dispositivo re-emparejado con otro nombre) ni siquiera se concilia: el servidor
//     guarda la idempotencia POR ACTOR, asi que con esta credencial seria una orden NUEVA (un consumo, dos cargos) y la
//     consulta por clave diria "no consta" aunque se aplicara. Solo descarte explicito tras comprobarla.
//  7. (D6.6) Un rechazo definitivo habla de ESTE intento, no de los anteriores: el servidor comprueba el permiso ANTES
//     de mirar si la clave ya se ejecuto, asi que un reintento puede recibir 403 de una orden que SI se aplico (p. ej.
//     puesto re-emparejado con otro rol). Antes de cerrar como "no aplicada" se pregunta por la clave: si consta, se
//     cierra como APLICADA; si no se puede preguntar, la orden se conserva.
const PREFIX = '/api/native/v1'
const INCONCLUSIVE = new Set(['storage_conflict', 'idempotency_conflict', 'operation_unconfirmed'])
const DEFINITIVE = new Set([403, 404, 409, 422])

export type Outcome =
  | { kind: 'confirmed'; response: unknown }
  | { kind: 'rejected'; status: number; code: string }
  | { kind: 'unconfirmed'; reason: 'network' | 'server' | 'unverifiable' }
  | { kind: 'unauthorized' }

// Orden guardada que NO se puede reenviar. 'actor' (D6.7) dice con que actor se hizo y que era, para explicar como comprobarla.
export type BlockedCommand =
  | { key: string | null; reason: 'unreadable' | 'foreign' }
  | { key: string; reason: 'actor'; actor: string; description: string }

export interface RunnerState {
  pending: PendingCommand | null
  blocked: BlockedCommand | null
  busy: boolean
  lastFailure: 'network' | 'server' | 'unverifiable' | null
  notice: string
}

export interface RunnerDeps {
  fetcher: Fetch
  store: PendingStore
  token: () => string
  identity: { installationId: string; actor: string }
  lock?: <T>(work: () => Promise<T>) => Promise<T>     // cerrojo de los INTENTOS entre pestanas (Web Locks); el hueco lo protege la reserva atomica del almacen
}

const UNRESOLVED = 'Hay una orden sin resolver: no se admite otra.'

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
    if (loaded.kind === 'restored') {
      const command = loaded.command
      if (command.installationId !== this.deps.identity.installationId) this.state.blocked = { key: command.key, reason: 'foreign' }
      // D6.7: hecha con OTRO nombre de dispositivo. Nunca pendiente ni "fallo de red": ni se reintenta sola ni con el boton.
      else if (command.actor !== this.deps.identity.actor) this.state.blocked = { key: command.key, reason: 'actor', actor: command.actor, description: command.description }
      else {
        this.state.pending = command
        // Restaurada de otra sesion del navegador: en cuanto el servidor responda se reintenta sola (mismos bytes y clave).
        this.state.lastFailure ??= 'network'
        return
      }
    } else if (loaded.kind === 'unreadable') this.state.blocked = { key: loaded.key, reason: 'unreadable' }
    this.state.lastFailure = null          // sin orden propia pendiente no hay nada que reintentar
  }

  async send(path: string, body: Record<string, unknown>, description: string): Promise<Outcome> {
    if (this.blocksMutations) throw new Error(UNRESOLVED)
    const command: PendingCommand = { key: newKey(), path, body: JSON.stringify(body), description, createdAt: new Date().toISOString(), ...this.deps.identity }
    this.state.busy = true
    try {
      let occupied: boolean
      try { occupied = (await this.deps.store.reserve(command)).kind !== 'absent' }
      catch { this.state.notice = 'No se pudo guardar la orden en este dispositivo, asi que NO se ha enviado. Libera espacio o revisa el navegador.'; throw new Error('pending store unavailable') }
      if (occupied) {
        // D6.7: otra pestana o ventana guardo una orden que esta aun no habia leido. Esta NO sale y se adopta (o bloquea) aquella.
        await this.restore()
        this.state.notice = 'Hay otra orden sin resolver en este dispositivo (de otra pestaña o ventana). Esta no se ha enviado: resuelve antes aquella.'
        throw new Error(UNRESOLVED)
      }
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
  // Si no consta, la orden normal sigue pendiente (un reintento identico es seguro); una ilegible o de otra
  // instalacion queda habilitada para descarte explicito. La de otro actor no se pregunta (D6.7): se explica.
  async reconcile(): Promise<'applied' | 'not_found' | 'unknown'> {
    const blocked = this.state.blocked
    if (blocked?.reason === 'actor') { this.state.notice = otherActorAdvice(blocked.actor); return 'unknown' }
    const key = this.state.pending?.key ?? blocked?.key
    if (!key || this.state.busy) return 'unknown'
    this.state.busy = true
    try {
      const lookup = await this.lookup(key)
      if (lookup.kind === 'applied') {
        await this.deps.store.clear(key); this.state.pending = null; this.state.blocked = null
        this.state.notice = 'El servidor confirma que esa orden SI se aplico. No la repitas.'
        return 'applied'
      }
      if (lookup.kind === 'absent') this.state.notice = 'Al servidor no le consta esa orden.'
      return lookup.kind === 'absent' ? 'not_found' : 'unknown'
    } finally { this.state.busy = false }
  }

  // GET /commands/{key}: solo el mismo actor; nunca ejecuta nada. 'unknown' = no se pudo saber (red, 5xx, 401, cuerpo raro).
  private async lookup(key: string): Promise<{ kind: 'applied'; response: unknown } | { kind: 'absent' } | { kind: 'unknown' }> {
    try {
      const response = await this.http(`${PREFIX}/commands/${encodeURIComponent(key)}`, this.init('GET'))
      if (!response.ok) return { kind: 'unknown' }
      const body = await response.json() as { key?: unknown; found?: unknown; response?: unknown }
      if (body === null || typeof body !== 'object' || body.key !== key || typeof body.found !== 'boolean') return { kind: 'unknown' }
      return body.found ? { kind: 'applied', response: body.response ?? null } : { kind: 'absent' }
    } catch { return { kind: 'unknown' } }
  }

  // Solo para lo que NO se puede reenviar (ilegible, de otra instalacion o de otro actor), y siempre por decision de la
  // persona. Borra solo ESA orden (D6.7: nunca la que otra pestana pudiera haber guardado despues) y relee el hueco.
  async discardBlocked(): Promise<void> {
    const blocked = this.state.blocked
    if (!blocked || this.state.busy) return
    this.state.busy = true
    try { await this.deps.store.discard(blocked.key) } finally { this.state.busy = false }
    await this.restore()
  }

  private init(method: 'GET' | 'POST', command?: PendingCommand): RequestInit {
    const headers: Record<string, string> = { Accept: 'application/json', Authorization: `Bearer ${this.deps.token()}` }
    if (command) { headers['Content-Type'] = 'application/json'; headers['Idempotency-Key'] = command.key }
    return { method, headers, body: command?.body, cache: 'no-store', credentials: 'omit', redirect: 'error' }
  }

  private attempt(command: PendingCommand): Promise<Outcome> {
    const work = async (): Promise<Outcome> => {
      // Otra pestana pudo resolver la orden mientras esperabamos el turno. Y solo sale la orden de ESTA instalacion y de
      // ESTE actor (D6.7): con otro, el servidor la ejecutaria como nueva. Si algo no cuadra, se relee y no se envia nada.
      const current = await this.deps.store.load()
      const { installationId, actor } = this.deps.identity
      if (current.kind !== 'restored' || current.command.key !== command.key || current.command.installationId !== installationId || current.command.actor !== actor) {
        await this.restore(); return { kind: 'unconfirmed', reason: 'unverifiable' }
      }
      let response: Response
      try { response = await this.http(PREFIX + command.path, this.init('POST', command)) }
      catch { return this.keep('network') }
      const echoed = response.headers.get('Idempotency-Key') === command.key
      let payload: unknown = null
      try { payload = await response.json() } catch { /* sin JSON reconocible */ }
      if (response.status === 401) { this.state.lastFailure = null; return { kind: 'unauthorized' } }
      if (response.ok) {
        if (!echoed || payload === null) return this.keep('unverifiable')
        // Exito con eco: la orden CONSTA aplicada y se cierra ya. Si ademas la respuesta trae una clave economica se
        // rechaza entera (ADR-007) DESPUES de cerrar: dejarla pendiente la reintentaria sin fin contra la misma respuesta.
        await this.close(command.key)
        assertNoMoney(payload)
        return { kind: 'confirmed', response: payload }
      }
      const code = payload !== null && typeof payload === 'object' && typeof (payload as { error?: unknown }).error === 'string' ? (payload as { error: string }).error : ''
      if (DEFINITIVE.has(response.status) && echoed && code !== '' && !INCONCLUSIVE.has(code)) {
        // El rechazo es de ESTE intento. Solo el registro del servidor dice si uno ANTERIOR llego a aplicarse.
        const earlier = await this.lookup(command.key)
        if (earlier.kind === 'unknown') return this.keep('unverifiable')
        await this.close(command.key)
        if (earlier.kind === 'applied') {
          this.state.notice = 'El servidor confirma que esa orden SI se aplico en un intento anterior. No la repitas.'
          assertNoMoney(earlier.response)
          return { kind: 'confirmed', response: earlier.response }
        }
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

// Cerrojo de los INTENTOS entre pestanas del mismo origen: solo evita lanzar a la vez el mismo intento desde dos pestanas
// (seria inocuo: misma clave y mismo actor, el servidor no duplica). NO protege el hueco: eso lo hace la reserva atomica
// del almacen (reserve/clear/discard en una sola transaccion de IndexedDB, D6.7). Sin Web Locks (navegador antiguo, o http
// sin contexto seguro, como la conexion sencilla de B.2) se ejecuta sin cerrojo y el hueco sigue protegido.
export const tabLock = <T>(work: () => Promise<T>): Promise<T> =>
  typeof navigator !== 'undefined' && 'locks' in navigator ? navigator.locks.request('costina-command', () => work()) as Promise<T> : work()
