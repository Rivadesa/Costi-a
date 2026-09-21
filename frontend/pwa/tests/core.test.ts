import 'fake-indexeddb/auto'
import { describe, expect, it } from 'vitest'
import { call, NetworkError, type Fetch } from '../src/api'
import { clearCredential, loadCredential, saveCredential } from '../src/credentials'
import { assertNoMoney, MoneyLeakError } from '../src/money-guard'
import { codeFromLocation, pairDevice } from '../src/pairing'

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
