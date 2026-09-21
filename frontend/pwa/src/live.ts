import { ApiError, NetworkError } from './api'
import { MoneyLeakError } from './money-guard'
import type { BoardEntry } from './types'

// Tablero "en vivo" (contrato D2). El canal solo AVISA: los avisos son finos, pueden llegar duplicados
// y nunca traen estado. Cada aviso, y OBLIGATORIAMENTE cada reconexion, dispara la misma relectura
// autoritativa por HTTP que el boton Actualizar. Si el canal cae, la pantalla degrada a sondeo y lo
// dice; la antiguedad de la ultima lectura esta siempre a la vista. Sin DOM: testeable.
export type Link = 'connecting' | 'live' | 'reconnecting' | 'polling'

export interface HubLike {
  start(): Promise<void>
  stop(): Promise<void>
  onEvent(handler: () => void): void
  onReconnecting(handler: () => void): void
  onReconnected(handler: () => void): void
  onClosed(handler: () => void): void
}

export interface LiveState { entries: BoardEntry[]; readAt: Date | null; link: Link; error: string }

export interface LiveDeps {
  load: () => Promise<BoardEntry[]>
  connect: () => HubLike
  onUnauthorized: () => void
  pollMs?: number        // sondeo cuando NO hay tiempo real
  safetyMs?: number      // relectura de seguridad aun con tiempo real (at-least-once no es "siempre")
  reconnectMs?: number   // reintento del canal tras agotarse la reconexion automatica
  startTimeoutMs?: number // un WebSocket que ni conecta ni falla (proxy, Wi-Fi cautiva) no deja la pantalla en "Conectando"
  timers?: { set: (handler: () => void, ms: number) => unknown; clear: (id: unknown) => void }
}

export class LiveBoard {
  private hub: HubLike | null = null
  private loading = false
  private queued = false
  private stopped = false
  private pollTimer: unknown
  private reconnectTimer: unknown
  private lastRead = 0
  private readonly timers: NonNullable<LiveDeps['timers']>

  constructor(private readonly state: LiveState, private readonly deps: LiveDeps) {
    this.timers = deps.timers ?? { set: (handler, ms) => setInterval(handler, ms), clear: id => clearInterval(id as number) }
  }

  async start(): Promise<void> {
    await this.refresh()
    this.pollTimer = this.timers.set(() => { void this.tick() }, 1000)
    await this.connect()
  }

  async stop(): Promise<void> {
    this.stopped = true
    this.timers.clear(this.pollTimer); this.timers.clear(this.reconnectTimer)
    const hub = this.hub; this.hub = null
    if (hub) await hub.stop().catch(() => undefined)
  }

  // Coalescencia: los avisos que llegan durante una lectura no lanzan otra en paralelo; se relee una vez al terminar.
  async refresh(): Promise<void> {
    if (this.stopped) return
    if (this.loading) { this.queued = true; return }
    this.loading = true
    try {
      do {
        this.queued = false
        try {
          this.state.entries = await this.deps.load()
          this.state.readAt = new Date(); this.lastRead = Date.now(); this.state.error = ''
        } catch (error) {
          if (error instanceof ApiError && error.status === 401) { this.deps.onUnauthorized(); return }
          this.state.error = error instanceof NetworkError ? 'Servidor sin respuesta: los datos en pantalla pueden estar desactualizados.'
            : error instanceof MoneyLeakError ? error.message
            : 'No se pudo leer el estado del servidor.'
        }
      } while (this.queued && !this.stopped)
    } finally { this.loading = false }
  }

  private async tick(): Promise<void> {
    const every = this.state.link === 'live' ? (this.deps.safetyMs ?? 60_000) : (this.deps.pollMs ?? 15_000)
    if (Date.now() - this.lastRead >= every) { this.lastRead = Date.now(); await this.refresh() }
  }

  private async connect(): Promise<void> {
    if (this.stopped) return
    this.state.link = 'connecting'
    const hub = this.deps.connect()
    hub.onEvent(() => { void this.refresh() })
    hub.onReconnecting(() => { this.state.link = 'reconnecting' })
    hub.onReconnected(() => { this.state.link = 'live'; void this.refresh() })       // relectura OBLIGATORIA tras reconectar
    hub.onClosed(() => { if (this.hub === hub) { this.hub = null; this.degrade() } })
    try {
      let expire: ReturnType<typeof setTimeout> | undefined
      const timeout = new Promise<never>((_, reject) => { expire = setTimeout(() => reject(new Error('hub start timeout')), this.deps.startTimeoutMs ?? 10_000) })
      try { await Promise.race([hub.start(), timeout]) }
      catch (error) { void hub.stop().catch(() => undefined); throw error }
      finally { clearTimeout(expire) }
      if (this.stopped) { await hub.stop().catch(() => undefined); return }
      this.hub = hub; this.state.link = 'live'
      void this.refresh()                                                            // lo ocurrido mientras se conectaba
    } catch { this.degrade() }
  }

  private degrade(): void {
    if (this.stopped) return
    this.state.link = 'polling'
    this.timers.clear(this.reconnectTimer)
    const id = this.timers.set(() => { this.timers.clear(id); void this.connect() }, this.deps.reconnectMs ?? 30_000)
    this.reconnectTimer = id
  }
}

export const LINK_TEXT: Record<Link, string> = {
  connecting: 'Conectando el tiempo real…',
  live: 'Tiempo real activo',
  reconnecting: 'RECONECTANDO — los cambios pueden tardar en verse',
  polling: 'SIN TIEMPO REAL — se relee cada 15 s',
}
