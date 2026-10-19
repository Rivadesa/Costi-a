import 'fake-indexeddb/auto'
import { IDBFactory } from 'fake-indexeddb'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CommandRunner, type RunnerState } from '../src/commands'
import { run as transaction, STORES } from '../src/db'
import { blockedTitle, deviceNameOf, otherActorAdvice, pairPendingAdvice, pendingDeviceName, unpairWarning } from '../src/labels'
import { MoneyLeakError } from '../src/money-guard'
import { indexedDbPendingStore, parsePending, type PendingCommand, type PendingLoad, type PendingStore } from '../src/pending'

const IDENTITY = { installationId: 'inst-1', actor: 'device:tablet-sala-1' }
const newState = (): RunnerState => ({ pending: null, blocked: null, busy: false, lastFailure: null, notice: '' })

// Mismo contrato que el almacen real (D6.7): la reserva solo escribe con el hueco libre y devuelve lo que habia;
// clear y discard solo borran la orden con ESA clave.
const storedKey = (load: PendingLoad) => load.kind === 'restored' ? load.command.key : load.kind === 'unreadable' ? load.key : undefined
class MemoryStore implements PendingStore {
  value: unknown = undefined; failSave = false; log: string[] = []
  async load(): Promise<PendingLoad> { return parsePending(this.value) }
  async reserve(command: PendingCommand): Promise<PendingLoad> {
    if (this.failSave) throw new Error('quota')
    const current = parsePending(this.value)
    if (current.kind === 'absent') { this.log.push('save'); this.value = structuredClone(command) }
    return current
  }
  async clear(key: string) { if (storedKey(parsePending(this.value)) === key) { this.log.push('clear'); this.value = undefined } }
  async discard(key: string | null) { if (storedKey(parsePending(this.value)) === key) { this.log.push('discard'); this.value = undefined } }
}

type Reply = { status: number; body?: unknown; echo?: boolean | string; html?: boolean } | 'network'
function server(replies: Reply[]) {
  const seen: Array<{ url: string; key: string | undefined; body: string | undefined; method: string }> = []
  const fetcher = async (url: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const headers = init!.headers as Record<string, string>
    seen.push({ url: String(url), key: headers['Idempotency-Key'], body: init!.body as string | undefined, method: init!.method! })
    const reply = replies.shift() ?? { status: 500 }
    if (reply === 'network') throw new TypeError('failed to fetch')
    const echo = reply.echo === false ? undefined : typeof reply.echo === 'string' ? reply.echo : headers['Idempotency-Key']
    const asked = decodeURIComponent(String(url).split('/commands/')[1] ?? '')       // GET /commands/{key}: el motor devuelve la clave consultada
    const json = JSON.stringify(reply.body ?? {}).replace('"$key"', JSON.stringify(init!.method === 'GET' ? asked : ''))
    return new Response(reply.html ? '<html>proxy</html>' : json, { status: reply.status, headers: echo ? { 'Idempotency-Key': echo } : {} })
  }
  return { fetcher, seen }
}
const runner = (store: PendingStore, replies: Reply[], state = newState()) => {
  const { fetcher, seen } = server(replies)
  return { run: new CommandRunner(state, { fetcher, store, token: () => 'dev.a.b', identity: IDENTITY }), seen, state }
}
const SERVE = ['/dining/services/s1/commands/serve', { expectedVersion: 4, courseId: 'p1' }, 'Servir Aperitivos · M1'] as const

describe('durable uncertain command', () => {
  it('is persisted BEFORE the first attempt and cleared only on a confirmed success', async () => {
    const store = new MemoryStore(); let savedWhenSent = false
    const { fetcher } = server([{ status: 200, body: { version: 5 } }])
    const run = new CommandRunner(newState(), { store, token: () => 't', identity: IDENTITY, fetcher: async (u, i) => { savedWhenSent = parsePending(store.value).kind === 'restored'; return fetcher(u, i) } })
    const outcome = await run.send(...SERVE)
    expect(savedWhenSent).toBe(true)
    expect(outcome).toEqual({ kind: 'confirmed', response: { version: 5 } })
    expect(store.log).toEqual(['save', 'clear']); expect(run.state.pending).toBeNull()
  })
  it('calls fetch WITHOUT a receiver, as window.fetch requires (a method call throws Illegal invocation in a browser)', async () => {
    const { fetcher } = server([{ status: 200, body: { version: 5 } }])
    const strict = function (this: unknown, input: RequestInfo | URL, init?: RequestInit) {
      if (this !== undefined) throw new TypeError('Illegal invocation')
      return fetcher(input, init)
    } as typeof fetch
    const run = new CommandRunner(newState(), { fetcher: strict, store: new MemoryStore(), token: () => 't', identity: IDENTITY })
    expect((await run.send(...SERVE)).kind).toBe('confirmed')
  })
  it('never sends what it could not persist', async () => {
    const store = new MemoryStore(); store.failSave = true
    const { run, seen, state } = runner(store, [{ status: 200 }])
    await expect(run.send(...SERVE)).rejects.toThrow()
    expect(seen).toHaveLength(0); expect(state.pending).toBeNull(); expect(state.notice).toContain('NO se ha enviado')
  })
  it('a lost response keeps it pending, blocks any other mutation, and the retry resends identical bytes and key', async () => {
    const store = new MemoryStore()
    const { run, seen, state } = runner(store, ['network', { status: 200, body: { version: 5 } }])
    expect(await run.send(...SERVE)).toEqual({ kind: 'unconfirmed', reason: 'network' })
    expect(state.pending?.description).toBe('Servir Aperitivos · M1'); expect(run.blocksMutations).toBe(true)
    await expect(run.send('/dining/services/s1/commands/pause', { expectedVersion: 4 }, 'otra')).rejects.toThrow('sin resolver')
    expect((await run.retry())?.kind).toBe('confirmed')
    expect(seen).toHaveLength(2)
    expect(seen[1].key).toBe(seen[0].key); expect(seen[1].body).toBe(seen[0].body); expect(seen[1].body).toBe('{"expectedVersion":4,"courseId":"p1"}')
    expect(state.pending).toBeNull()
  })
  it('survives closing the browser: a new runner restores it and only then retries the same key', async () => {
    const store = new MemoryStore()
    const first = runner(store, ['network']); await first.run.send(...SERVE)
    const key = first.seen[0].key
    const second = runner(store, [{ status: 200, body: {} }])                // "otra sesion del navegador"
    await second.run.restore()
    expect(second.state.pending?.key).toBe(key); expect(second.run.blocksMutations).toBe(true)
    expect(second.state.lastFailure).toBe('network')                        // se reintentara sola en cuanto el servidor responda
    await second.run.retry()
    expect(second.seen[0].key).toBe(key); expect(parsePending(store.value).kind).toBe('absent')
  })
  it('closes the uncertainty on a DEFINITIVE rejection only: recognisable error, echoed key, not a storage conflict', async () => {
    const definitive = runner(new MemoryStore(), [{ status: 409, body: { error: 'version_conflict' } }, { status: 200, body: { key: '$key', found: false } }])
    expect(await definitive.run.send(...SERVE)).toEqual({ kind: 'rejected', status: 409, code: 'version_conflict' })
    expect(definitive.state.pending).toBeNull(); expect(definitive.state.notice).toContain('Otro puesto')
    expect(definitive.seen.map(request => request.method)).toEqual(['POST', 'GET'])      // pregunta por la clave ANTES de darla por no aplicada
    expect(definitive.seen[1].url).toContain('/commands/' + definitive.seen[0].key)
    for (const reply of [
      { status: 409, body: { error: 'storage_conflict' } }, { status: 409, body: { error: 'idempotency_conflict' } },
      { status: 409, body: { error: 'version_conflict' }, echo: false }, { status: 409, body: { error: 'version_conflict' }, echo: 'otra-clave' },
      { status: 403, html: true }, { status: 404, body: {} }, { status: 500, body: { error: 'x' } }, { status: 429, body: { error: 'too_many' } },
      { status: 200, body: { version: 5 }, echo: false }, { status: 200, html: true },
    ] as Reply[]) {
      const kept = runner(new MemoryStore(), [reply])
      expect((await kept.run.send(...SERVE)).kind, JSON.stringify(reply)).toBe('unconfirmed')
      expect(kept.state.pending, JSON.stringify(reply)).not.toBeNull()
    }
  })
  it('a rejection speaks for THIS attempt only: if the server has the key, an earlier attempt WAS applied and it closes as applied', async () => {
    // Respuesta perdida -> el puesto se re-empareja con otro rol -> el reintento recibe 403 con eco... de una orden que SI se aplico.
    const store = new MemoryStore()
    const { run, state, seen } = runner(store, ['network', { status: 403, body: { error: 'forbidden' } }, { status: 200, body: { key: '$key', found: true, response: { version: 5 } } }])
    await run.send(...SERVE)
    expect(await run.retry()).toEqual({ kind: 'confirmed', response: { version: 5 } })
    expect(state.pending).toBeNull(); expect(state.notice).toContain('SI se aplico'); expect(store.log).toEqual(['save', 'clear'])
    expect(seen.map(request => request.method)).toEqual(['POST', 'POST', 'GET'])
  })
  it('a rejection it cannot verify against the server record closes nothing', async () => {
    for (const lookup of ['network', { status: 500, body: {} }, { status: 401, body: { error: 'unauthorized' } }, { status: 200, html: true },
      { status: 200, body: { found: false } }, { status: 200, body: { key: 'otra-clave', found: false } }] as Reply[]) {
      const { run, state } = runner(new MemoryStore(), [{ status: 409, body: { error: 'version_conflict' } }, lookup])
      expect(await run.send(...SERVE), JSON.stringify(lookup)).toEqual({ kind: 'unconfirmed', reason: 'unverifiable' })
      expect(state.pending, JSON.stringify(lookup)).not.toBeNull()
    }
  })
  it('an applied-earlier response that carries money is closed and still refused', async () => {
    const { run, state } = runner(new MemoryStore(), [{ status: 403, body: { error: 'forbidden' } }, { status: 200, body: { key: '$key', found: true, response: { balanceCents: 1 } } }])
    await expect(run.send(...SERVE)).rejects.toBeInstanceOf(MoneyLeakError)
    expect(state.pending).toBeNull()
  })
  it('a 401 does not close it: the first attempt may have been applied before the revocation', async () => {
    const { run, state } = runner(new MemoryStore(), [{ status: 401, body: { error: 'unauthorized' } }])
    expect(await run.send(...SERVE)).toEqual({ kind: 'unauthorized' })
    expect(state.pending).not.toBeNull()
  })
  it('reconciles by key: found closes it as APPLIED; not found leaves the identical retry available', async () => {
    const store = new MemoryStore()
    const lost = runner(store, ['network', { status: 200, body: { key: '$key', found: false } }, { status: 200, body: { key: '$key', found: true, response: {} } }])
    await lost.run.send(...SERVE)
    expect(await lost.run.reconcile()).toBe('not_found'); expect(lost.state.pending).not.toBeNull()
    expect(await lost.run.reconcile()).toBe('applied'); expect(lost.state.pending).toBeNull(); expect(lost.state.notice).toContain('SI se aplico')
    expect(lost.seen[1].url).toContain('/commands/' + lost.seen[0].key); expect(lost.seen[1].method).toBe('GET')
  })
  it('never resends a command from ANOTHER installation or an unreadable one; they block until an explicit decision', async () => {
    const foreign = new MemoryStore(); foreign.value = { key: 'k-foreign', path: '/dining/services/x/commands/serve', body: '{}', description: 'vieja', createdAt: '', installationId: 'inst-OTRA', actor: IDENTITY.actor }
    const a = runner(foreign, [{ status: 200 }]); await a.run.restore()
    expect(a.state.blocked).toEqual({ key: 'k-foreign', reason: 'foreign' }); expect(a.run.blocksMutations).toBe(true)
    expect(await a.run.retry()).toBeNull(); expect(a.seen).toHaveLength(0)
    const broken = new MemoryStore(); broken.value = { key: 'k-broken', path: 'sin-barra', body: '{no json' }
    const b = runner(broken, []); await b.run.restore()
    expect(b.state.blocked).toEqual({ key: 'k-broken', reason: 'unreadable' })
    await expect(b.run.send(...SERVE)).rejects.toThrow()
    await b.run.discardBlocked(); expect(b.run.blocksMutations).toBe(false); expect(broken.log).toEqual(['discard'])
  })
  it('refuses a confirmed response that carries money, but the command IS applied: it is closed, never retried', async () => {
    const store = new MemoryStore()
    const { run, seen, state } = runner(store, [{ status: 200, body: { balanceCents: 100 } }])
    await expect(run.send(...SERVE)).rejects.toBeInstanceOf(MoneyLeakError)
    expect(store.log).toEqual(['save', 'clear']); expect(state.pending).toBeNull(); expect(run.blocksMutations).toBe(false)
    expect(await run.retry()).toBeNull(); expect(seen).toHaveLength(1)
  })
  it('a money-looking key is enough: "chargeId" is refused (regression: the waiter route answers consumptionId)', async () => {
    const { run } = runner(new MemoryStore(), [{ status: 200, body: { version: 5, chargeId: 'abc', productId: 'water', quantity: 1 } }])
    await expect(run.send(...SERVE)).rejects.toBeInstanceOf(MoneyLeakError)
    const clean = runner(new MemoryStore(), [{ status: 200, body: { version: 5, consumptionId: 'abc', productId: 'water', quantity: 1 } }])
    expect((await clean.run.send(...SERVE)).kind).toBe('confirmed')
  })
  it('without the echoed key a money-carrying success closes nothing', async () => {
    const store = new MemoryStore()
    const { run, state } = runner(store, [{ status: 200, body: { balanceCents: 100 }, echo: false }])
    expect(await run.send(...SERVE)).toEqual({ kind: 'unconfirmed', reason: 'unverifiable' })
    expect(state.pending).not.toBeNull(); expect(store.log).toEqual(['save'])
  })
})

describe('IndexedDB pending store', () => {
  const command: PendingCommand = { key: 'k1', path: '/dining/services/s1/commands/serve', body: '{"expectedVersion":4}', description: 'Servir', createdAt: '2026-09-21T10:00:00Z', ...IDENTITY }
  it('round-trips, and clear(key) only removes the command carrying that key', async () => {
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
    expect(await indexedDbPendingStore.reserve(command)).toEqual({ kind: 'absent' })
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'restored', command })
    await indexedDbPendingStore.clear('otra-clave')
    expect((await indexedDbPendingStore.load()).kind).toBe('restored')
    await indexedDbPendingStore.clear('k1')
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
  })
})

// D6.7 (revision externa del 19-10): la orden incierta, a salvo de OTRO nombre de dispositivo y de OTRA pestana.
// Todo sobre el almacen REAL (IndexedDB de fake-indexeddb, uno nuevo por prueba) y SIN cerrojo entre pestanas: la conexion
// sencilla de B.2 es http, sin contexto seguro, y alli no hay Web Locks.
describe('D6.7: the uncertain command is safe from another device name and from another tab (no Web Locks)', () => {
  beforeEach(() => { vi.stubGlobal('indexedDB', new IDBFactory()) })
  afterEach(() => { vi.unstubAllGlobals() })

  const order = (key: string, over: Partial<PendingCommand> = {}): PendingCommand => ({
    key, path: '/dining/services/s1/commands/add-consumption', body: '{"expectedVersion":4,"productId":"water","quantity":1}',
    description: 'Añadir Agua · M1', createdAt: '2026-10-19T10:00:00Z', ...IDENTITY, ...over,
  })
  // Lo que otra pestana (u otra sesion del navegador) deja en el hueco, escrito sin pasar por el almacen.
  const put = (value: unknown) => transaction(STORES.pending, 'readwrite', store => store.put(value, 'uncertain'))
  const storedKey = async () => { const load = await indexedDbPendingStore.load(); return load.kind === 'restored' ? load.command.key : load.kind }
  const tab = (replies: Reply[]) => runner(indexedDbPendingStore, replies)
  const PAUSE = ['/dining/services/s2/commands/pause', { expectedVersion: 1, reason: 'salen' }, 'Pausar servicio · M2'] as const

  it('an order made under ANOTHER device name is blocked: never retried, never sent, never asked about; only an explicit discard removes it', async () => {
    await put(order('k-old', { actor: 'device:tablet-vieja' }))          // respuesta perdida y re-emparejado con otro nombre
    const { run, seen, state } = tab([{ status: 200, body: { version: 5 } }, { status: 200, body: { key: '$key', found: false } }])
    await run.restore()
    expect(state.blocked).toEqual({ key: 'k-old', reason: 'actor', actor: 'device:tablet-vieja', description: 'Añadir Agua · M1' })
    expect(state.pending).toBeNull(); expect(state.lastFailure).toBeNull(); expect(run.blocksMutations).toBe(true)
    expect(await run.retry()).toBeNull()
    await expect(run.send(...PAUSE)).rejects.toThrow('sin resolver')
    expect(await run.reconcile()).toBe('unknown')                        // con este nombre el servidor diria "no consta": no se le pregunta
    expect(state.notice).toContain('«tablet-vieja»'); expect(state.notice).not.toMatch(/no le consta/i)
    expect(seen).toHaveLength(0)                                         // ni POST ni GET
    expect(await storedKey()).toBe('k-old')                              // nada se borra solo
    await run.discardBlocked()
    expect(state.blocked).toBeNull(); expect(run.blocksMutations).toBe(false)
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
  })

  it('attempt() re-reads the store and never POSTs an order whose saved actor or installation is not this runner\'s', async () => {
    for (const swapped of [{ actor: 'device:otra-tablet' }, { installationId: 'inst-OTRA' }]) {
      vi.stubGlobal('indexedDB', new IDBFactory())
      await put(order('k-mine'))
      const { run, seen, state } = tab([{ status: 200, body: { version: 5 } }])
      await run.restore()
      expect(state.pending?.key, JSON.stringify(swapped)).toBe('k-mine')
      await put(order('k-mine', swapped))                                 // misma clave, otra identidad, entre la lectura y el intento
      expect(await run.retry(), JSON.stringify(swapped)).toEqual({ kind: 'unconfirmed', reason: 'unverifiable' })
      expect(seen, JSON.stringify(swapped)).toHaveLength(0)
      expect(state.pending, JSON.stringify(swapped)).toBeNull(); expect(state.blocked?.key, JSON.stringify(swapped)).toBe('k-mine')
    }
  })

  it('control: the SAME device name re-paired with another role still restores, retries and closes as applied (D6.6 path)', async () => {
    await put(order('k-old'))
    const { run, seen, state } = tab([{ status: 403, body: { error: 'forbidden' } }, { status: 200, body: { key: '$key', found: true, response: { version: 5 } } }])
    await run.restore()
    expect(state.blocked).toBeNull(); expect(state.pending?.key).toBe('k-old'); expect(state.lastFailure).toBe('network')
    expect(await run.retry()).toEqual({ kind: 'confirmed', response: { version: 5 } })
    expect(seen.map(request => request.method)).toEqual(['POST', 'GET']); expect(seen[0].key).toBe('k-old')
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
  })

  it('a STALE tab cannot replace an uncertain order: it sends nothing and adopts the stored one', async () => {
    const a = tab(['network']), b = tab([{ status: 200, body: { version: 5 } }])
    await a.run.restore(); await b.run.restore()                         // las dos leen el hueco vacio
    await a.run.send(...SERVE)                                           // A: respuesta perdida, queda pendiente
    const keyA = a.seen[0].key
    expect(b.run.blocksMutations).toBe(false)                            // B no lo sabe: no ha vuelto a leer
    await expect(b.run.send(...PAUSE)).rejects.toThrow('sin resolver')
    expect(b.seen).toHaveLength(0)
    expect(await storedKey()).toBe(keyA)
    expect(b.state.pending?.key).toBe(keyA); expect(b.state.notice).toContain('otra pestaña')
    expect((await a.run.retry())?.kind).toBe('unconfirmed')              // A sigue con SU orden: reintenta la misma clave
    expect(a.seen.map(request => request.key)).toEqual([keyA, keyA])
  })

  it('two tabs sending at the same moment: exactly one order is saved and exactly one POST goes out', async () => {
    const a = tab(['network']), b = tab(['network'])
    await a.run.restore(); await b.run.restore()
    const results = await Promise.allSettled([a.run.send(...SERVE), b.run.send(...PAUSE)])
    const posts = [...a.seen, ...b.seen]
    expect(posts).toHaveLength(1)
    const refused = results.filter(result => result.status === 'rejected')
    expect(refused).toHaveLength(1); expect(String((refused[0] as PromiseRejectedResult).reason)).toContain('sin resolver')
    expect(await storedKey()).toBe(posts[0].key)
    expect([a.state.pending?.key, b.state.pending?.key]).toEqual([posts[0].key, posts[0].key])   // la que perdio ve la ganadora
  })

  it('clear(key) never deletes an order another tab saved between its read and its delete', async () => {
    const b = tab(['network']); await b.run.restore()                    // B: pestana atrasada, leyo el hueco vacio
    await put(order('k-a'))                                              // A guardo la suya y ahora la cierra
    const get = IDBObjectStore.prototype.get
    let other: Promise<unknown> | null = null
    IDBObjectStore.prototype.get = function (this: IDBObjectStore, query: IDBValidKey | IDBKeyRange) {
      other ??= b.run.send(...PAUSE).catch(error => error)               // B guarda la suya justo mientras A cierra la suya
      return get.call(this, query)
    }
    try { await indexedDbPendingStore.clear('k-a'); await other } finally { IDBObjectStore.prototype.get = get }
    expect(b.seen).toHaveLength(1)
    expect(await storedKey()).toBe(b.seen[0].key)                        // la orden de B sigue guardada
  })

  it('discard removes only the blocked order, never one another tab saved afterwards', async () => {
    await put({ key: 'k-broken', path: 'sin-barra', body: '{no json' })
    const { run, state } = tab([])
    await run.restore()
    expect(state.blocked).toEqual({ key: 'k-broken', reason: 'unreadable' })
    await put(order('k-new'))                                            // mientras, otra pestana la descarto y guardo una nueva
    await run.discardBlocked()
    expect(await storedKey()).toBe('k-new')
    expect(state.blocked).toBeNull(); expect(state.pending?.key).toBe('k-new')   // y esta pestana ve la que hay ahora
    await indexedDbPendingStore.discard('otra-clave'); await indexedDbPendingStore.discard(null)
    expect(await storedKey()).toBe('k-new')
    await indexedDbPendingStore.discard('k-new')
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
  })

  it('the reserve never overwrites: an occupied slot is returned as it is, readable or not', async () => {
    expect(await indexedDbPendingStore.reserve(order('k-1'))).toEqual({ kind: 'absent' })
    expect(await indexedDbPendingStore.reserve(order('k-2'))).toEqual({ kind: 'restored', command: order('k-1') })
    await put({ key: 'k-broken' })
    expect(await indexedDbPendingStore.reserve(order('k-3'))).toEqual({ kind: 'unreadable', key: 'k-broken' })
    expect(await storedKey()).toBe('unreadable')
  })

  it('labels: the other-name order names the original device, never says "no le consta", and pairing proposes that name', () => {
    expect(deviceNameOf('device:tablet-vieja')).toBe('tablet-vieja'); expect(deviceNameOf('user:ana')).toBe('user:ana')
    expect(blockedTitle({ reason: 'actor', actor: 'device:tablet-vieja' })).toBe('ORDEN HECHA CON OTRO NOMBRE DE DISPOSITIVO («tablet-vieja»)')
    expect(blockedTitle({ reason: 'foreign' })).toBe('ORDEN DE OTRA INSTALACIÓN'); expect(blockedTitle({ reason: 'raro' })).toBe('raro')
    expect(otherActorAdvice('device:tablet-vieja')).toContain('«tablet-vieja»'); expect(otherActorAdvice('device:x')).not.toMatch(/no le consta|es seguro/i)
    expect(pendingDeviceName({ kind: 'restored', command: order('k', { actor: 'device:tablet-vieja' }) })).toEqual({ name: 'tablet-vieja', description: 'Añadir Agua · M1' })
    expect(pendingDeviceName({ kind: 'absent' })).toBeNull(); expect(pendingDeviceName(null)).toBeNull()
    expect(unpairWarning({ kind: 'restored', command: order('k') })).toContain('«tablet-sala-1»'); expect(unpairWarning({ kind: 'absent' })).toBe('')
  })

  // Revision D6.7: el aviso de "otra pestaña" solo cuando la orden encontrada es de verdad de este dispositivo y nombre.
  it('a send that finds the slot taken by an order it can NEVER resend does not blame another tab: the red panel gives the reason', async () => {
    const cases: Array<[unknown, string]> = [
      [order('k-x', { actor: 'device:tablet-vieja' }), 'actor'], [order('k-x', { installationId: 'inst-OTRA' }), 'foreign'], [{ key: 'k-x' }, 'unreadable'],
    ]
    for (const [stored, reason] of cases) {
      vi.stubGlobal('indexedDB', new IDBFactory())
      const b = tab([{ status: 200, body: { version: 5 } }]); await b.run.restore()     // B leyo el hueco vacio
      await put(stored)                                                                  // fallo al montar, o dos credenciales a la vez
      await expect(b.run.send(...PAUSE), reason).rejects.toThrow('sin resolver')
      expect(b.seen, reason).toHaveLength(0)
      expect(b.state.blocked?.reason, reason).toBe(reason); expect(b.state.pending, reason).toBeNull()
      expect(b.state.notice, reason).not.toContain('otra pestaña'); expect(b.state.notice, reason).toContain('no se puede reenviar')
      expect(await storedKey(), reason).toBe(reason === 'unreadable' ? 'unreadable' : 'k-x')
    }
    // Y si la orden que ocupaba el hueco ya se resolvio al releer, tampoco: se dice que esta no salio y que se repita.
    const gone: PendingStore = { load: async () => ({ kind: 'absent' }), reserve: async () => ({ kind: 'restored', command: order('k-x') }), clear: async () => {}, discard: async () => {} }
    const c = runner(gone, [{ status: 200, body: { version: 5 } }])
    await expect(c.run.send(...PAUSE)).rejects.toThrow('sin resolver')
    expect(c.seen).toHaveLength(0); expect(c.state.pending).toBeNull(); expect(c.state.blocked).toBeNull()
    expect(c.state.notice).not.toContain('otra pestaña'); expect(c.state.notice).toContain('no se ha enviado')
  })

  // Revision D6.7: al emparejar aun no se sabe con que servidor; el mismo nombre solo sirve con el MISMO servidor.
  it('pairing advice is conditional: the same name only helps with the same server; a reinstalled server blocks it as foreign whatever the name', async () => {
    const saved = order('k-old', { actor: 'device:tablet-vieja' })
    expect(pendingDeviceName({ kind: 'restored', command: { ...saved, installationId: 'inst-OTRA' } })?.name).toBe('tablet-vieja')   // no puede saber el servidor
    const advice = pairPendingAdvice('tablet-vieja')
    expect(advice).toContain('«tablet-vieja»'); expect(advice).toContain('mismo servidor'); expect(advice).toContain('instalado de nuevo')
    expect(advice).not.toMatch(/^Se hizo con el nombre «tablet-vieja»: empareja/)
    await put(order('k-old', { installationId: 'inst-ANTERIOR' }))                      // mismo nombre, servidor instalado de nuevo
    const { run, state } = tab([]); await run.restore()
    expect(state.blocked).toEqual({ key: 'k-old', reason: 'foreign' }); expect(state.pending).toBeNull()
    expect(unpairWarning({ kind: 'restored', command: saved })).toContain('mismo servidor')
  })
})
