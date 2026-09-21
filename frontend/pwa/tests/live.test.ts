import { describe, expect, it } from 'vitest'
import { ApiError, NetworkError } from '../src/api'
import { ageLabel, boardSummary, pendingReviews, restrictionLabel, reviewLabel } from '../src/labels'
import { LiveBoard, type HubLike, type LiveState } from '../src/live'
import type { BoardEntry, Dining, Preparation } from '../src/types'

const preparation = (over: Partial<Preparation> = {}): Preparation => ({ id: 'frio', name: 'Preparacion fria', stationId: 'cold', quantity: 1, guestPosition: 1, mandatory: true, state: 'Fired', ...over })
const dining = (over: Partial<Dining> = {}): Dining => ({ id: 's1', tableId: 'M1', pax: 2, state: 'InService', courses: [{ id: 'p1', name: 'Aperitivos', state: 'Fired', firedAt: null, readyAt: null, servedAt: null, skipReason: null, preparations: [preparation()] }], ...over })
const entry = (service = dining()): BoardEntry => ({ version: 3, service, occupancy: { id: 'o1', tableId: service.tableId, serviceId: service.id, state: 'Occupied', releasedAt: null }, occupancyVersion: 1 })

class FakeHub implements HubLike {
  handlers: Record<string, () => void> = {}
  started = 0; stopped = 0; failStart = false
  async start() { this.started++; if (this.failStart) throw new Error('no websocket') }
  async stop() { this.stopped++ }
  onEvent(h: () => void) { this.handlers.event = h }
  onReconnecting(h: () => void) { this.handlers.reconnecting = h }
  onReconnected(h: () => void) { this.handlers.reconnected = h }
  onClosed(h: () => void) { this.handlers.closed = h }
}
const newState = (): LiveState => ({ entries: [], readAt: null, link: 'connecting', error: '' })
const manualTimers = () => {
  const pending = new Map<number, () => void>(); let next = 1
  return { timers: { set: (h: () => void) => { pending.set(next, h); return next++ }, clear: (id: unknown) => { pending.delete(id as number) } }, pending }
}
const flush = () => new Promise<void>(resolve => setTimeout(resolve, 0))

describe('labels never rely on colour', () => {
  it('spells out kind, substance and severity, per guest or whole table', () => {
    expect(restrictionLabel({ id: 'r', guestPosition: 2, kind: 'Allergy', substance: 'marisco', severity: 'Severe' })).toBe('Comensal 2 · ALERGIA marisco (grave)')
    expect(restrictionLabel({ id: 'r', guestPosition: null, kind: 'Preference', substance: 'sin picante', severity: 'Mild' })).toBe('Mesa · Preferencia sin picante (leve)')
  })
  it('says when kitchen still has to review, and what it decided', () => {
    expect(reviewLabel(preparation({ reviewPending: true }))).toBe('PENDIENTE DE REVISION POR COCINA')
    expect(reviewLabel(preparation({ review: { decision: 'Remake', note: 'sin frutos secos', at: '' } }))).toBe('Revisada: rehecha — sin frutos secos')
    expect(reviewLabel(preparation())).toBe('')
    expect(pendingReviews(dining({ courses: [{ ...dining().courses[0], preparations: [preparation({ reviewPending: true }), preparation({ id: 'b' })] }] }))).toBe(1)
  })
  it('summarises the current course and formats data age', () => {
    expect(boardSummary(entry())).toBe('Aperitivos: Enviado a cocina')
    expect(boardSummary(entry(dining({ courses: [{ ...dining().courses[0], state: 'Served' }] })))).toBe('Todos los pases servidos')
    expect([ageLabel(null), ageLabel(7), ageLabel(125)]).toEqual(['sin lectura', 'hace 7 s', 'hace 2 min 5 s'])
  })
})

describe('live board (D2 contract: the channel only notifies)', () => {
  it('reads on start, goes live, and re-reads on every notice and ALWAYS after a reconnection', async () => {
    const state = newState(), hub = new FakeHub(), { timers } = manualTimers(); let loads = 0
    const live = new LiveBoard(state, { load: async () => { loads++; return [entry()] }, connect: () => hub, onUnauthorized: () => { throw new Error('unexpected') }, timers })
    await live.start(); await flush()
    expect(state.link).toBe('live'); expect(state.entries).toHaveLength(1); expect(state.readAt).not.toBeNull()
    const afterStart = loads
    hub.handlers.event(); await flush()
    expect(loads).toBe(afterStart + 1)
    hub.handlers.reconnecting(); expect(state.link).toBe('reconnecting')
    hub.handlers.reconnected(); await flush()
    expect(state.link).toBe('live'); expect(loads).toBe(afterStart + 2)
    await live.stop(); expect(hub.stopped).toBe(1)
  })
  it('coalesces a burst of notices into one extra authoritative read', async () => {
    const state = newState(), hub = new FakeHub(), { timers } = manualTimers(); let loads = 0; let release: () => void = () => undefined
    const live = new LiveBoard(state, { load: () => { loads++; return new Promise(resolve => { release = () => resolve([entry()]) }) }, connect: () => hub, onUnauthorized: () => undefined, timers })
    const starting = live.start(); await flush(); release(); await starting; await flush(); release(); await flush()
    const base = loads
    hub.handlers.event(); hub.handlers.event(); hub.handlers.event(); hub.handlers.event(); await flush()
    expect(loads).toBe(base + 1)                 // una en vuelo
    release(); await flush()
    expect(loads).toBe(base + 2)                 // y UNA sola mas por todo el resto de la rafaga
    release(); await flush()
    expect(loads).toBe(base + 2)
  })
  it('degrades to polling when the channel cannot start or closes, says so, and retries the channel', async () => {
    const state = newState(), first = new FakeHub(), second = new FakeHub(), { timers, pending } = manualTimers(); first.failStart = true
    const hubs = [first, second]
    const live = new LiveBoard(state, { load: async () => [entry()], connect: () => hubs.shift()!, onUnauthorized: () => undefined, timers })
    await live.start(); await flush()
    expect(state.link).toBe('polling')
    const retry = [...pending.values()].at(-1)!; retry(); await flush()
    expect(state.link).toBe('live'); expect(second.started).toBe(1)
    second.handlers.closed(); expect(state.link).toBe('polling')
  })
  it('a channel that neither connects nor fails does not leave the screen on "connecting"', async () => {
    const state = newState(), hub = new FakeHub(), { timers } = manualTimers()
    hub.start = () => new Promise<void>(() => undefined)               // se queda colgado para siempre
    const live = new LiveBoard(state, { load: async () => [entry()], connect: () => hub, onUnauthorized: () => undefined, timers, startTimeoutMs: 20 })
    await live.start()
    expect(state.link).toBe('polling'); expect(hub.stopped).toBe(1); expect(state.entries).toHaveLength(1)
  })
  it('keeps the last data but flags the error when the server stops answering; a 401 hands control back', async () => {
    const state = newState(), hub = new FakeHub(), { timers } = manualTimers(); let mode: 'ok' | 'down' | 'revoked' = 'ok'; let unauthorized = 0
    const live = new LiveBoard(state, { timers, connect: () => hub, onUnauthorized: () => { unauthorized++ },
      load: async () => { if (mode === 'down') throw new NetworkError('x'); if (mode === 'revoked') throw new ApiError(401, 'unauthorized'); return [entry()] } })
    await live.start(); await flush()
    const readAt = state.readAt
    mode = 'down'; await live.refresh()
    expect(state.entries).toHaveLength(1); expect(state.readAt).toBe(readAt); expect(state.error).toContain('desactualizados')
    mode = 'ok'; await live.refresh(); expect(state.error).toBe('')
    mode = 'revoked'; await live.refresh(); expect(unauthorized).toBe(1)
  })
})
