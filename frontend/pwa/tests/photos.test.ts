import { describe, expect, it } from 'vitest'
import type { Fetch } from '../src/api'
import { initials, MAX_PHOTO_BYTES, MAX_PHOTOS, PhotoCache } from '../src/photos'

// C3b: las fotos de «Bodega y bebidas» (photos.ts). Se piden con el token, una vez por huella, y se guardan en memoria con un tope de
// cuantas y de cuanto ocupan; al pasarse, la que lleva mas tiempo sin verse se descarta y su URL se libera. Nada que no sea una foto.
type Reply = { status: number; type?: string; bytes?: number } | 'network'
function setup(replies: Record<string, Reply[]>, limits: { maxCount?: number; maxBytes?: number } = {}) {
  const asked: Array<{ url: string; auth: string; cache: string | undefined }> = []
  const created: string[] = [], revoked: string[] = []
  let gate: Promise<void> = Promise.resolve()
  const fetcher: Fetch = async (url, init) => {
    asked.push({ url: String(url), auth: (init?.headers as Record<string, string>).Authorization, cache: init?.cache })
    await gate
    const product = decodeURIComponent(String(url).split('/catalog/images/')[1])
    const reply = replies[product]?.shift() ?? { status: 404 }
    if (reply === 'network') throw new TypeError('failed to fetch')
    const bytes = new Uint8Array(reply.bytes ?? 100)
    return new Response(reply.status === 200 ? bytes : '{"error":"not_found"}', { status: reply.status, headers: { 'Content-Type': reply.type ?? 'image/png' } })
  }
  const cache = new PhotoCache({
    fetcher, token: () => 'dev.t.s', createUrl: () => { const url = `blob:test/${created.length + 1}`; created.push(url); return url }, revokeUrl: url => { revoked.push(url) }, ...limits,
  })
  return { cache, asked, created, revoked, hold: () => { let open: () => void = () => undefined; gate = new Promise(resolve => { open = resolve }); return () => open() } }
}
const ok = (bytes = 100, type = 'image/png'): Reply => ({ status: 200, type, bytes })

describe('photos of the drinks: asked with the token, once per fingerprint, kept in memory with limits', () => {
  it('asks the image of the product with the token, never cached by the browser, and keeps it by its fingerprint', async () => {
    const { cache, asked, created } = setup({ albarino: [ok()] })
    expect(await cache.load('albarino', 'sha-a')).toBe('blob:test/1')
    expect(asked).toEqual([{ url: '/api/native/v1/catalog/images/albarino', auth: 'Bearer dev.t.s', cache: 'no-store' }])
    expect(await cache.load('albarino', 'sha-a')).toBe('blob:test/1')        // ya en memoria: ni otra peticion ni otra URL
    expect(cache.cached('sha-a')).toBe('blob:test/1')
    expect(asked).toHaveLength(1); expect(created).toHaveLength(1)
  })
  it('several rows asking for the same fingerprint at once make ONE request', async () => {
    const { cache, asked, hold } = setup({ albarino: [ok()] })
    const release = hold()
    const all = Promise.all([cache.load('albarino', 'sha-a'), cache.load('albarino', 'sha-a'), cache.load('albarino', 'sha-a')])
    release()
    expect(await all).toEqual(['blob:test/1', 'blob:test/1', 'blob:test/1'])
    expect(asked).toHaveLength(1)
  })
  it('without a fingerprint there is nothing to ask', async () => {
    const { cache, asked } = setup({})
    expect(await cache.load('agua', null)).toBeNull(); expect(await cache.load('agua', '')).toBeNull()
    expect(asked).toEqual([])
  })
  it('only JPEG, PNG or WebP: anything else, a 404 or an empty body gives the initials and is not asked again', async () => {
    const { cache, asked, created } = setup({ a: [ok(100, 'text/html')], b: [{ status: 404 }], c: [ok(0)], d: [ok(100, 'image/webp; charset=binary')] })
    expect(await cache.load('a', 'sha-a')).toBeNull()
    expect(await cache.load('b', 'sha-b')).toBeNull()
    expect(await cache.load('c', 'sha-c')).toBeNull()
    expect(await cache.load('d', 'sha-d')).toBe('blob:test/1')
    for (const [product, sha] of [['a', 'sha-a'], ['b', 'sha-b'], ['c', 'sha-c']]) expect(await cache.load(product, sha)).toBeNull()
    expect(asked).toHaveLength(4); expect(created).toEqual(['blob:test/1'])
  })
  it('a network cut, a 401 or a 5xx are passing: asked again the next time the row is seen', async () => {
    const { cache, asked } = setup({ a: ['network', { status: 401 }, { status: 503 }, ok()] })
    expect(await cache.load('a', 'sha-a')).toBeNull()
    expect(await cache.load('a', 'sha-a')).toBeNull()
    expect(await cache.load('a', 'sha-a')).toBeNull()
    expect(await cache.load('a', 'sha-a')).toBe('blob:test/1')
    expect(asked).toHaveLength(4)
  })
  it('past the count limit, the one unseen for longest goes and its URL is released (never the one that just arrived)', async () => {
    const { cache, revoked } = setup({ a: [ok()], b: [ok()], c: [ok()] }, { maxCount: 2 })
    await cache.load('a', 'sha-a'); await cache.load('b', 'sha-b')
    expect(cache.cached('sha-a')).toBe('blob:test/1')                          // vista otra vez: pasa a ser la mas reciente
    await cache.load('c', 'sha-c')
    expect(revoked).toEqual(['blob:test/2'])                                   // se va la de «b», no la de «a»
    expect([cache.cached('sha-a'), cache.cached('sha-b'), cache.cached('sha-c')]).toEqual(['blob:test/1', null, 'blob:test/3'])
    expect(cache.size).toBe(2)
  })
  it('past the size limit too; a photo bigger than the whole limit is not kept', async () => {
    const { cache, revoked } = setup({ a: [ok(600)], b: [ok(600)], big: [ok(1200)] }, { maxBytes: 1000 })
    await cache.load('a', 'sha-a'); await cache.load('b', 'sha-b')
    expect(revoked).toEqual(['blob:test/1']); expect(cache.bytes).toBe(600)
    expect(await cache.load('big', 'sha-big')).toBeNull()
    expect(cache.size).toBe(1)
  })
  it('the limits are about 150 photos or 20 MB', () => {
    expect([MAX_PHOTOS, MAX_PHOTO_BYTES]).toEqual([150, 20 * 1024 * 1024])
  })
  it('leaving the handheld releases every URL, and what was still on its way is dropped when it arrives', async () => {
    const { cache, created, revoked, hold } = setup({ a: [ok()], b: [ok()] })
    await cache.load('a', 'sha-a')
    const release = hold()
    const late = cache.load('b', 'sha-b')
    cache.clear()
    release()
    expect(await late).toBeNull()
    expect(revoked).toEqual(['blob:test/1']); expect(created).toHaveLength(1)
    expect(cache.size).toBe(0); expect(cache.bytes).toBe(0)
  })
})

describe('the placeholder without a photo', () => {
  it.each([
    ['Albariño Pazo de Señoráns 2023', 'AP'], ['Agua mineral', 'AM'], ['Vino de ensayo', 'VE'], ['La Bota nº 132 Vermut Rojo', 'LB'],
    ['Ñora', 'Ñ'], ['1906 Reserva Especial', 'RE'], ['Lalama 2019', 'L'], ['1906', '1'], ['  ', '?'], ['Estrella Galicia', 'EG'], ['Café con leche', 'CL'],
  ])('«%s» -> %s', (name, expected) => { expect(initials(name)).toBe(expected) })
})
