import 'fake-indexeddb/auto'
import { describe, expect, it } from 'vitest'
import { CommandRunner, type RunnerState } from '../src/commands'
import { MoneyLeakError } from '../src/money-guard'
import { indexedDbPendingStore, parsePending, type PendingCommand, type PendingLoad, type PendingStore } from '../src/pending'

const IDENTITY = { installationId: 'inst-1', actor: 'device:tablet-sala-1' }
const newState = (): RunnerState => ({ pending: null, blocked: null, busy: false, lastFailure: null, notice: '' })

class MemoryStore implements PendingStore {
  value: unknown = undefined; failSave = false; log: string[] = []
  async load(): Promise<PendingLoad> { return parsePending(this.value) }
  async save(command: PendingCommand) { if (this.failSave) throw new Error('quota'); this.log.push('save'); this.value = structuredClone(command) }
  async clear(key: string) { const current = parsePending(this.value); if (current.kind === 'restored' && current.command.key === key) { this.log.push('clear'); this.value = undefined } }
  async discard() { this.log.push('discard'); this.value = undefined }
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
    await indexedDbPendingStore.save(command)
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'restored', command })
    await indexedDbPendingStore.clear('otra-clave')
    expect((await indexedDbPendingStore.load()).kind).toBe('restored')
    await indexedDbPendingStore.clear('k1')
    expect(await indexedDbPendingStore.load()).toEqual({ kind: 'absent' })
  })
})
