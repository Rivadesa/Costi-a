import 'fake-indexeddb/auto'
import { describe, expect, it } from 'vitest'
import { call, NetworkError, readCatalog, type Fetch } from '../src/api'
import { clearCredential, loadCredential, saveCredential } from '../src/credentials'
import { assertNoMoney, MoneyLeakError } from '../src/money-guard'
import { codeFromLocation, pairDevice, unpairDevice } from '../src/pairing'
import { ApiError } from '../src/api'
import { NOT_READY_PREFIX, outcomeOf, UNPAIRED_NOTICE } from '../src/startup'

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const noWait = () => Promise.resolve()

describe('money guard (ADR-007)', () => {
  it('accepts operational payloads', () => {
    assertNoMoney({ role: 'service', station: 'sala-1', actions: ['open'], courses: [{ preparations: [{ name: 'Frio', stationId: 'cold' }] }] })
    assertNoMoney([{ id: 'water', name: 'Agua', presentation: 'Botella' }])
  })
  it.each(['priceCents', 'unitPriceCents', 'balanceCents', 'totalCents', 'paidCents', 'payments', 'refunds', 'charges', 'creditCents', 'amount'])(
    'rejects any payload carrying %s, at any depth', key => {
      expect(() => assertNoMoney({ data: [{ nested: { [key]: 1 } }] })).toThrow(MoneyLeakError)
    })
})

describe('api client', () => {
  it('sends bearer, no cookies, and an Idempotency-Key on every POST', async () => {
    let seen: { url: string; init: RequestInit } | undefined
    const fetcher: Fetch = async (url, init) => { seen = { url: String(url), init: init! }; return json(200, { ok: true }) }
    await call(fetcher, '/auth/hub-token', { token: 'dev.a.b', body: {} })
    const headers = seen!.init.headers as Record<string, string>
    expect(seen!.url).toBe('/api/native/v1/auth/hub-token')
    expect(headers.Authorization).toBe('Bearer dev.a.b')
    expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f]{32}$/)
    expect(seen!.init.credentials).toBe('omit'); expect(seen!.init.cache).toBe('no-store'); expect(seen!.init.redirect).toBe('error')
  })
  it('a GET carries no Idempotency-Key', async () => {
    let headers: Record<string, string> = {}
    await call(async (_url, init) => { headers = init!.headers as Record<string, string>; return json(200, {}) }, '/session', { token: 'dev.a.b' })
    expect(headers['Idempotency-Key']).toBeUndefined()
  })
  it('maps rejections to ApiError with the server code and transport failures to NetworkError', async () => {
    await expect(call(async () => json(401, { error: 'unauthorized' }), '/session')).rejects.toMatchObject({ status: 401, code: 'unauthorized' })
    await expect(call(async () => new Response('<html>', { status: 403 }), '/session')).rejects.toMatchObject({ status: 403, code: 'unrecognised_response' })
    await expect(call(async () => { throw new TypeError('offline') }, '/session')).rejects.toBeInstanceOf(NetworkError)
  })
  it('refuses a successful response that carries money', async () => {
    await expect(call(async () => json(200, [{ id: 'water', priceCents: 400 }]), '/catalog')).rejects.toBeInstanceOf(MoneyLeakError)
    await expect(call(async () => json(200, [{ id: 'water', presentationId: 'bottle', tariffId: 'general' }]), '/catalog')).rejects.toBeInstanceOf(MoneyLeakError)   // E3: la tarifa es vocabulario de caja
  })
})

// C3b: el catalogo se revalida con su ETag (al abrir Bebidas, cada 5 minutos y al reconectar) sin bajar otra vez 2.000 filas.
describe('conditional catalogue read (C3b)', () => {
  const water = { id: 'water', presentationId: 'bottle', name: 'Agua mineral', presentation: 'Botella', section: 'bodega' }
  const tagged = (status: number, body: unknown, etag: string | null) => {
    const response = new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
    if (etag) response.headers.set('ETag', etag)
    return response
  }
  const seen: Array<{ url: string; init: RequestInit }> = []
  const answering = (response: () => Response): Fetch => async (url, init) => { seen.push({ url: String(url), init: init! }); return response() }

  it('a first read asks without If-None-Match, keeps the ETag and the transport rules of every read', async () => {
    seen.length = 0
    const read = await readCatalog(answering(() => tagged(200, [water], '"abc"')), 'dev.a.b')
    expect(read).toEqual({ items: [water], etag: '"abc"' })
    const headers = seen[0].init.headers as Record<string, string>
    expect(seen[0].url).toBe('/api/native/v1/catalog'); expect(headers.Authorization).toBe('Bearer dev.a.b'); expect(headers['If-None-Match']).toBeUndefined()
    expect(seen[0].init.method).toBe('GET'); expect(seen[0].init.cache).toBe('no-store'); expect(seen[0].init.credentials).toBe('omit'); expect(seen[0].init.redirect).toBe('error')
  })
  it('revalidates with the ETag by hand; a 304 without body keeps the previous list, the same one', async () => {
    seen.length = 0
    const previous = { items: [water], etag: '"abc"' }
    const read = await readCatalog(answering(() => new Response(null, { status: 304 })), 'dev.a.b', previous)
    expect((seen[0].init.headers as Record<string, string>)['If-None-Match']).toBe('"abc"')
    expect(read).toBe(previous)
  })
  it('a changed catalogue replaces the list and its ETag', async () => {
    const soda = { ...water, id: 'soda', name: 'Gaseosa' }
    const read = await readCatalog(answering(() => tagged(200, [water, soda], '"def"')), 'dev.a.b', { items: [water], etag: '"abc"' })
    expect(read).toEqual({ items: [water, soda], etag: '"def"' })
  })
  it('every new body goes through the money guard, also on a revalidation', async () => {
    await expect(readCatalog(answering(() => tagged(200, [{ ...water, priceCents: 400 }], '"x"')), 'dev.a.b')).rejects.toBeInstanceOf(MoneyLeakError)
    await expect(readCatalog(answering(() => tagged(200, [{ ...water, tariffId: 'general' }], '"x"')), 'dev.a.b', { items: [water], etag: '"abc"' })).rejects.toBeInstanceOf(MoneyLeakError)
  })
  it('without an ETag nothing is revalidated next time', async () => {
    seen.length = 0
    const first = await readCatalog(answering(() => tagged(200, [water], null)), 'dev.a.b')
    expect(first.etag).toBeNull()
    await readCatalog(answering(() => tagged(200, [water], null)), 'dev.a.b', first)
    expect((seen[1].init.headers as Record<string, string>)['If-None-Match']).toBeUndefined()
  })
  it('a 304 with nothing to keep, a body that is not a list, a rejection or no answer are errors', async () => {
    await expect(readCatalog(answering(() => new Response(null, { status: 304 })), 'dev.a.b')).rejects.toMatchObject({ status: 304, code: 'unrecognised_response' })
    await expect(readCatalog(answering(() => tagged(200, { items: [water] }, '"x"')), 'dev.a.b')).rejects.toMatchObject({ status: 200, code: 'unrecognised_response' })
    await expect(readCatalog(answering(() => tagged(401, { error: 'unauthorized' }, null)), 'dev.a.b', { items: [water], etag: '"abc"' })).rejects.toMatchObject({ status: 401, code: 'unauthorized' })
    await expect(readCatalog(async () => { throw new TypeError('offline') }, 'dev.a.b')).rejects.toBeInstanceOf(NetworkError)
  })
})

describe('device credential store', () => {
  it('round-trips, survives reopening and clears', async () => {
    expect(await loadCredential()).toBeNull()
    await saveCredential({ token: 'dev.abc.secret', deviceName: 'tablet-sala-1', pairedAt: '2026-09-21T10:00:00Z' })
    expect(await loadCredential()).toEqual({ token: 'dev.abc.secret', deviceName: 'tablet-sala-1', pairedAt: '2026-09-21T10:00:00Z' })
    await clearCredential()
    expect(await loadCredential()).toBeNull()
  })
  it('fails closed on anything that is not a device credential', async () => {
    await saveCredential({ token: 'user-session-token', deviceName: 'x', pairedAt: '' })
    expect(await loadCredential()).toBeNull()
    await clearCredential()
  })
})

describe('pairing', () => {
  const flow = (collects: Array<() => Response | never>): Fetch => {
    let index = 0
    return async url => String(url).endsWith('/claim') ? json(200, { pairingId: 'p1', pollSecret: 's1' }) : collects[Math.min(index++, collects.length - 1)]()
  }
  it('waits while pending, tolerates a network cut and returns the token once approved', async () => {
    let waiting = false
    const outcome = await pairDevice(' code-123 ', ' tablet ', {
      wait: noWait, onWaiting: () => { waiting = true },
      fetcher: flow([() => json(200, { status: 'pending' }), () => { throw new TypeError('wifi') }, () => json(200, { status: 'approved', deviceToken: 'dev.d1.s', role: 'service', station: 'sala-1' })]),
    })
    expect(waiting).toBe(true)
    expect(outcome).toEqual({ kind: 'approved', token: 'dev.d1.s', role: 'service', station: 'sala-1' })
  })
  it('reports denied, expired, invalid and throttled without leaking detail', async () => {
    expect(await pairDevice('c', 'tablet', { wait: noWait, fetcher: flow([() => json(403, { status: 'denied' })]) })).toEqual({ kind: 'denied' })
    expect(await pairDevice('c', 'tablet', { wait: noWait, fetcher: flow([() => json(404, { error: 'unknown_pairing' })]) })).toEqual({ kind: 'expired' })
    expect(await pairDevice('c', 'tablet', { wait: noWait, attempts: 3, fetcher: flow([() => json(200, { status: 'pending' })]) })).toEqual({ kind: 'expired' })
    expect(await pairDevice('c', 'tablet', { wait: noWait, fetcher: async () => json(401, { error: 'invalid_code' }) })).toEqual({ kind: 'invalid_code' })
    expect(await pairDevice('c', 'tablet', { wait: noWait, fetcher: async () => json(429, { error: 'too_many_attempts' }) })).toEqual({ kind: 'throttled' })
  })
  it('reads the code from the URL fragment only', () => {
    expect(codeFromLocation('#pair=AbC_def-1234567890')).toBe('AbC_def-1234567890')
    expect(codeFromLocation('#x=1&pair=AbCdEf123456')).toBe('AbCdEf123456')
    expect(codeFromLocation('#pair=<script>')).toBe('')
    expect(codeFromLocation('')).toBe('')
  })
})

describe('persistent storage request (D6.6)', () => {
  it('asks the browser once it matters and reports honestly what it got', async () => {
    const { requestPersistence } = await import('../src/storage')
    expect(await requestPersistence(undefined)).toBe('unsupported')
    expect(await requestPersistence({})).toBe('unsupported')
    expect(await requestPersistence({ persisted: async () => true, persist: async () => { throw new Error('must not ask again') } })).toBe('persisted')
    expect(await requestPersistence({ persisted: async () => false, persist: async () => true })).toBe('persisted')
    expect(await requestPersistence({ persisted: async () => false, persist: async () => false })).toBe('best-effort')
    expect(await requestPersistence({ persist: async () => { throw new Error('SecurityError') } })).toBe('best-effort')
  })
  it('keeps the receiver: navigator.storage.persist() called detached throws Illegal invocation in a browser', async () => {
    const { requestPersistence } = await import('../src/storage')
    const strict = { granted: true, async persisted(this: { granted: boolean }) { return false }, async persist(this: { granted: boolean }) { return this.granted } }
    expect(await requestPersistence(strict)).toBe('persisted')
  })
  it('over the simple connection (plain http) says so and asks nothing (B.2)', async () => {
    const { requestPersistence, PERSISTENCE_HINT } = await import('../src/storage')
    expect(await requestPersistence({ persist: async () => { throw new Error('must not ask over http') } }, false)).toBe('plain-http')
    expect(PERSISTENCE_HINT['plain-http']).toMatch(/sin cifrar/)
  })
})

describe('startup (corte A, F8)', () => {
  it('a 401 is the only "unpaired"; a network cut keeps the last state; anything else is "server not ready" and retried', () => {
    expect(outcomeOf(new ApiError(401, 'unauthorized'), true)).toEqual({ state: 'unpaired', notice: UNPAIRED_NOTICE })
    expect(outcomeOf(new NetworkError('x'), true)).toEqual({ state: 'ready', notice: '' })
    expect(outcomeOf(new NetworkError('x'), false)).toEqual({ state: 'offline', notice: '' })
    const notReady = outcomeOf(new ApiError(503, 'storage_unavailable'), false)
    expect(notReady.state).toBe('offline'); expect(notReady.notice).toContain(NOT_READY_PREFIX); expect(notReady.notice).toContain('HTTP 503')
    expect(outcomeOf(new ApiError(403, 'forbidden'), false).state).toBe('offline')              // nunca se borra la credencial por un 403
    expect(outcomeOf(new Error('IndexedDB bloqueada por otra pestana'), false).notice).toContain('IndexedDB bloqueada')
    expect(outcomeOf(new MoneyLeakError('$.x'), true).state).toBe('offline')
  })
})

describe('unpair (corte A, F9)', () => {
  it('revokes on the server first, treats 401 as already gone and refuses to forget the credential offline', async () => {
    let posted = ''
    expect(await unpairDevice(async (url, init) => { posted = `${init!.method} ${url}`; return json(200, { unpaired: true }) }, 'dev.a.b')).toBe('unpaired')
    expect(posted).toBe('POST /api/native/v1/auth/unpair')
    expect(await unpairDevice(async () => json(401, { error: 'unauthorized' }), 'dev.a.b')).toBe('already')
    expect(await unpairDevice(async () => { throw new TypeError('offline') }, 'dev.a.b')).toBe('offline')
  })
})
