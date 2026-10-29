import 'fake-indexeddb/auto'
import { IDBFactory } from 'fake-indexeddb'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createRenderer, defineComponent } from 'vue'
import type { Session } from '../src/api'
import { run as transaction, STORES } from '../src/db'
import { DONE_MS, useOperations } from '../src/operations'
import { indexedDbPendingStore, type PendingCommand } from '../src/pending'

// D6.7, de punta a punta: el useOperations REAL (LiveBoard, CommandRunner, IndexedDB y tabLock) montado con un renderizador
// vacio en Node. Sin tiempo real (el tablero degrada a sondeo) y SIN Web Locks, como en la conexion sencilla de B.2 (http).
vi.mock('../src/realtime', () => ({
  connectHub: () => ({
    start: () => Promise.reject(new Error('sin tiempo real en la prueba')), stop: async () => undefined,
    onEvent: () => undefined, onReconnecting: () => undefined, onReconnected: () => undefined, onClosed: () => undefined,
  }),
}))

const renderer = createRenderer<object, object>({
  patchProp: () => undefined, insert: () => undefined, remove: () => undefined, createElement: () => ({}), createText: () => ({}),
  createComment: () => ({}), setText: () => undefined, setElementText: () => undefined, parentNode: () => null, nextSibling: () => null,
})
const session = (actor: string): Session => ({
  role: 'service', actor, station: 'sala-1', stationName: 'Sala 1', tenantId: 't', companyId: 'c', locationId: 'l',
  installationId: 'inst-1', serverVersion: '0.40.1-d6.7', demo: false, actions: ['add-consumption'], modules: ['dining'],
})
const order = (actor: string): PendingCommand => ({
  key: 'k-old', path: '/dining/services/s1/commands/add-consumption', body: '{"expectedVersion":4,"productId":"water","quantity":1}',
  description: 'Añadir Agua · M1', createdAt: '2026-10-19T10:00:00Z', installationId: 'inst-1', actor,
})

let requests: Array<{ method: string; url: string; key?: string }> = []
let listeners: Record<string, () => void> = {}
const posts = () => requests.filter(request => request.method === 'POST')
const reads = () => requests.filter(request => request.url.endsWith('/dining/board')).length
const settle = () => new Promise<void>(resolve => setTimeout(resolve, 150))   // margen para un reintento que NO debe llegar

async function fakeFetch(url: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const headers = (init?.headers ?? {}) as Record<string, string>
  requests.push({ method: init?.method ?? 'GET', url: String(url), key: headers['Idempotency-Key'] })
  const echo: Record<string, string> = headers['Idempotency-Key'] ? { 'Idempotency-Key': headers['Idempotency-Key'] } : {}
  if (String(url).endsWith('/dining/board')) return new Response('[]', { status: 200 })
  if (init?.method === 'POST') return new Response('{"version":5,"consumptionId":"c1","productId":"water","quantity":1}', { status: 200, headers: echo })
  const asked = decodeURIComponent(String(url).split('/commands/')[1] ?? '')
  return new Response(JSON.stringify({ key: asked, found: false }), { status: 200 })
}

function mount(actor: string) {
  let operations: ReturnType<typeof useOperations> | null = null
  const app = renderer.createApp(defineComponent({
    setup() { operations = useOperations({ token: 'dev.tablet.s', session: session(actor) }, () => undefined); return () => null },
  }))
  app.mount({})
  return { operations: operations!, unmount: () => app.unmount() }
}

describe('D6.7 end to end through useOperations (no realtime, no Web Locks)', () => {
  beforeEach(() => {
    requests = []; listeners = {}
    const on = (name: string, handler: () => void) => { listeners[name] = handler }
    vi.stubGlobal('indexedDB', new IDBFactory())
    vi.stubGlobal('fetch', fakeFetch)
    vi.stubGlobal('navigator', {})                                          // sin navigator.locks
    // setTimeout y clearTimeout se buscan al llamar: con los temporizadores falsos de vitest (C3e.1, H20) son los falsos.
    vi.stubGlobal('window', { setInterval, clearInterval, addEventListener: on, removeEventListener: () => undefined,
      setTimeout: (handler: () => void, ms: number) => globalThis.setTimeout(handler, ms), clearTimeout: (id?: number) => globalThis.clearTimeout(id) })
    vi.stubGlobal('document', { visibilityState: 'visible', addEventListener: on, removeEventListener: () => undefined })
  })
  afterEach(() => { vi.unstubAllGlobals() })

  it('re-paired under ANOTHER name: the stored order is never resent (board read, button, foreground, remount) until explicitly discarded', async () => {
    await transaction(STORES.pending, 'readwrite', store => store.put(order('device:tablet-vieja'), 'uncertain'))
    const first = mount('device:tablet-nueva')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      await settle()
      expect(first.operations.runnerState.blocked).toMatchObject({ reason: 'actor', actor: 'device:tablet-vieja' })
      expect(first.operations.runnerState.pending).toBeNull()
      await first.operations.retry()
      await first.operations.run('/dining/services/s1/commands/pause', { expectedVersion: 5, reason: 'salen' }, 'Pausar servicio · M1')
      await first.operations.reconcile()
      expect(first.operations.runnerState.notice).toContain('«tablet-vieja»'); expect(first.operations.runnerState.notice).not.toMatch(/no le consta/i)
      listeners.visibilitychange?.()                                         // vuelve a primer plano: relee el hueco y el tablero
      await settle()
      expect(posts()).toHaveLength(0)
      expect(requests.filter(request => request.url.includes('/api/native/v1/commands/'))).toHaveLength(0)   // ni pregunta por la clave
    } finally { first.unmount() }

    const readsBefore = reads()
    const second = mount('device:tablet-nueva')                             // otra sesion del navegador, mismo nombre nuevo
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(readsBefore))
      await settle()
      expect(posts()).toHaveLength(0)
      expect(second.operations.runnerState.blocked?.reason).toBe('actor')
      await second.operations.discard()                                     // solo la decision explicita la quita
      expect(second.operations.runnerState.blocked).toBeNull(); expect(second.operations.locked.value).toBe(false)
      expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
      expect(posts()).toHaveLength(0)
    } finally { second.unmount() }
  })

  it('control: re-paired under the SAME name, the stored order is retried once by itself after the first board read', async () => {
    await transaction(STORES.pending, 'readwrite', store => store.put(order('device:tablet-nueva'), 'uncertain'))
    const mounted = mount('device:tablet-nueva')
    try {
      await vi.waitFor(() => expect(posts()).toHaveLength(1))
      expect(posts()[0].key).toBe('k-old')
      await vi.waitFor(async () => expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' }))
      await settle()
      expect(posts()).toHaveLength(1)
    } finally { mounted.unmount() }
  })

  // C3b: una orden APLICADA cuya respuesta trae una clave de dinero no se pinta (ADR-007), pero tampoco se da por «no enviada»: vuelve
  // como confirmada, sin respuesta. Si volviera null, la ronda seguiria en la bandeja y el camarero la enviaria otra vez.
  it('an applied command whose answer carries money comes back confirmed without its answer, never as not sent', async () => {
    vi.stubGlobal('fetch', async (url: RequestInfo | URL, init?: RequestInit) => {
      const headers = (init?.headers ?? {}) as Record<string, string>
      requests.push({ method: init?.method ?? 'GET', url: String(url), key: headers['Idempotency-Key'] })
      if (String(url).endsWith('/dining/board')) return new Response('[]', { status: 200 })
      if (init?.method === 'POST') return new Response('{"version":5,"consumptionIds":["c1"],"totalCents":950}', { status: 200, headers: { 'Idempotency-Key': headers['Idempotency-Key'] } })
      return new Response('{}', { status: 200 })
    })
    const mounted = mount('device:tablet')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      const outcome = await mounted.operations.run('/dining/services/s1/commands/add-consumptions',
        { expectedVersion: 4, lines: [{ productId: 'wine', presentationId: 'glass', quantity: 2, guestPosition: null }] }, 'Ronda para Mesa 1: 2 × Vino · Copa')
      expect(outcome).toEqual({ kind: 'confirmed', response: null })
      expect(mounted.operations.runnerState.pending).toBeNull()
      expect(mounted.operations.runnerState.notice).toContain('La orden SÍ se aplicó')
      expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
      expect(posts()).toHaveLength(1)
    } finally { mounted.unmount() }
  })
})

// C3e.1 (usabilidad 28-10, H20): tras una orden CONFIRMADA del boton grande, su resultado se ve apagado durante DONE_MS desde que termina la
// relectura, y todo queda bloqueado hasta entonces: un doble toque, o un segundo «por si no fue», no cae en la accion siguiente.
describe('C3e.1 (H20): the result of a confirmed big-button order, for DONE_MS, before the next action', () => {
  const SERVE = '/dining/services/s1/commands/serve'
  const RESULT = { text: 'Servido · Aperitivos ✓', place: 'servicio:s1' }
  beforeEach(() => {
    requests = []; listeners = {}
    const on = (name: string, handler: () => void) => { listeners[name] = handler }
    vi.stubGlobal('indexedDB', new IDBFactory())
    vi.stubGlobal('fetch', fakeFetch)
    vi.stubGlobal('navigator', {})
    vi.stubGlobal('window', { setInterval, clearInterval, addEventListener: on, removeEventListener: () => undefined,
      setTimeout: (handler: () => void, ms: number) => globalThis.setTimeout(handler, ms), clearTimeout: (id?: number) => globalThis.clearTimeout(id) })
    vi.stubGlobal('document', { visibilityState: 'visible', addEventListener: on, removeEventListener: () => undefined })
  })
  afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })
  // Solo setTimeout y clearTimeout: IndexedDB (setImmediate) y la lectura siguen con los de verdad.
  const fake = () => vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })

  it('confirmed: the result shows at once, the read after it is done, and only DONE_MS later the lock goes', async () => {
    const mounted = mount('device:tablet')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      fake()
      const readsBefore = reads()
      const outcome = await mounted.operations.run(SERVE, { expectedVersion: 4, courseId: 'c1' }, 'Marcar servido · Aperitivos · Mesa 5', RESULT)
      expect(outcome?.kind).toBe('confirmed')
      expect(reads()).toBe(readsBefore + 1)                                      // la relectura ya termino...
      expect(mounted.operations.done.value).toEqual(RESULT)                      // ...y el boton sigue con su resultado
      expect(mounted.operations.locked.value).toBe(true)
      vi.advanceTimersByTime(DONE_MS - 1)
      expect([mounted.operations.done.value, mounted.operations.locked.value]).toEqual([RESULT, true])
      vi.advanceTimersByTime(1)
      expect([mounted.operations.done.value, mounted.operations.locked.value]).toEqual([null, false])   // ahora, la accion siguiente
      expect(posts()).toHaveLength(1)
    } finally { mounted.unmount() }
  })
  it('without a result asked (a dish, a round, the kitchen), nothing changes: no result and no extra wait', async () => {
    const mounted = mount('device:tablet')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      fake()
      expect((await mounted.operations.run(SERVE, { expectedVersion: 4, courseId: 'c1' }, 'Marcar servido · Aperitivos · Mesa 5'))?.kind).toBe('confirmed')
      expect([mounted.operations.done.value, mounted.operations.locked.value]).toEqual([null, false])
    } finally { mounted.unmount() }
  })
  it('rejected by the server: no «✓», the notice says why and the buttons come back as before', async () => {
    vi.stubGlobal('fetch', async (url: RequestInfo | URL, init?: RequestInit) => {
      const headers = (init?.headers ?? {}) as Record<string, string>
      requests.push({ method: init?.method ?? 'GET', url: String(url), key: headers['Idempotency-Key'] })
      if (String(url).endsWith('/dining/board')) return new Response('[]', { status: 200 })
      if (init?.method === 'POST') return new Response('{"error":"version_conflict"}', { status: 409, headers: { 'Idempotency-Key': headers['Idempotency-Key'] } })
      return new Response(JSON.stringify({ key: decodeURIComponent(String(url).split('/commands/')[1] ?? ''), found: false }), { status: 200 })
    })
    const mounted = mount('device:tablet')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      fake()
      expect((await mounted.operations.run(SERVE, { expectedVersion: 4, courseId: 'c1' }, 'Marcar servido · Aperitivos · Mesa 5', RESULT))?.kind).toBe('rejected')
      expect([mounted.operations.done.value, mounted.operations.locked.value]).toEqual([null, false])
      expect(mounted.operations.runnerState.notice).not.toBe('')
    } finally { mounted.unmount() }
  })
  it('unconfirmed (the network fails): no «✓»; the pending order locks, as before', async () => {
    vi.stubGlobal('fetch', async (url: RequestInfo | URL, init?: RequestInit) => {
      requests.push({ method: init?.method ?? 'GET', url: String(url) })
      if (String(url).endsWith('/dining/board')) return new Response('[]', { status: 200 })
      throw new TypeError('Failed to fetch')
    })
    const mounted = mount('device:tablet')
    try {
      await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
      fake()
      expect((await mounted.operations.run(SERVE, { expectedVersion: 4, courseId: 'c1' }, 'Marcar servido · Aperitivos · Mesa 5', RESULT))?.kind).toBe('unconfirmed')
      expect(mounted.operations.done.value).toBeNull()
      expect(mounted.operations.runnerState.pending).not.toBeNull()
      expect(mounted.operations.locked.value).toBe(true)                          // por la orden pendiente, no por un resultado
      vi.advanceTimersByTime(DONE_MS)
      expect(mounted.operations.done.value).toBeNull()
    } finally { mounted.unmount() }
  })
  it('the screen goes away while the result shows: its timer is cleared (nothing fires after unmounting)', async () => {
    const mounted = mount('device:tablet')
    await vi.waitFor(() => expect(reads()).toBeGreaterThan(0))
    fake()
    await mounted.operations.run(SERVE, { expectedVersion: 4, courseId: 'c1' }, 'Marcar servido · Aperitivos · Mesa 5', RESULT)
    expect(vi.getTimerCount()).toBe(1)
    mounted.unmount()
    expect(vi.getTimerCount()).toBe(0)
  })
})
