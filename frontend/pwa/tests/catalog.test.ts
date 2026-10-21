import { afterEach, describe, expect, it, vi } from 'vitest'
import { createRenderer, defineComponent } from 'vue'
import type { Fetch } from '../src/api'
import { CATALOG_MS, reconnections, useCatalog } from '../src/catalog'

// C3b: el catalogo de «Bodega y bebidas» al dia (catalog.ts): se lee al montar y se revalida con ETag al abrir Bebidas (refresh), cada 5
// minutos y tras reconectar. Un 304 conserva la MISMA lista; cada cuerpo nuevo pasa la guarda de dinero; un fallo conserva lo leido.
const renderer = createRenderer<object, object>({
  patchProp: () => undefined, insert: () => undefined, remove: () => undefined, createElement: () => ({}), createText: () => ({}),
  createComment: () => ({}), setText: () => undefined, setElementText: () => undefined, parentNode: () => null, nextSibling: () => null,
})
function mount(fetcher: Fetch, enabled = () => true, onUnauthorized = () => undefined) {
  let used: ReturnType<typeof useCatalog> | null = null
  const app = renderer.createApp(defineComponent({ setup() { used = useCatalog({ token: 'dev.t.s' }, enabled, onUnauthorized, { fetcher }); return () => null } }))
  app.mount({})
  return { used: used!, unmount: () => app.unmount() }
}
const ROWS = [{ id: 'wine', presentationId: 'glass', name: 'Vino', presentation: 'Copa', section: 'bodega', glass: true }]

// Un motor con ETag: 304 si la huella que manda el cliente es la vigente.
function engine() {
  const seen: Array<string | null> = []
  let etag = '"v1"', body: unknown = ROWS, status = 200
  const fetcher: Fetch = async (url, init) => {
    expect(String(url)).toBe('/api/native/v1/catalog')
    const asked = (init?.headers as Record<string, string>)['If-None-Match'] ?? null
    seen.push(asked)
    if (status !== 200) return new Response('{"error":"x"}', { status })
    if (asked === etag) return new Response(null, { status: 304, headers: { ETag: etag } })
    return new Response(JSON.stringify(body), { status: 200, headers: { ETag: etag } })
  }
  return { fetcher, seen, set: (next: { etag?: string; body?: unknown; status?: number }) => { etag = next.etag ?? etag; body = next.body ?? body; status = next.status ?? status } }
}

describe('useCatalog: the drinks catalogue kept current with ETag', () => {
  afterEach(() => { vi.useRealTimers() })

  it('reads on mount, revalidates when Bebidas opens and every 5 minutes; a 304 keeps the SAME list; stops when unmounted', async () => {
    vi.useFakeTimers()
    const server = engine()
    const { used, unmount } = mount(server.fetcher)
    await vi.advanceTimersByTimeAsync(0)
    expect(server.seen).toEqual([null])
    const first = used.read.value
    expect(first?.items.map(item => item.id)).toEqual(['wine']); expect(used.status.readAt).not.toBeNull()
    await used.refresh()                                                     // abrir Bebidas
    expect(server.seen).toEqual([null, '"v1"'])
    expect(used.read.value).toBe(first)                                      // 304: la misma lista, nada que rehacer
    server.set({ etag: '"v2"', body: [...ROWS, { id: 'water', presentationId: 'bottle', name: 'Agua', presentation: 'Botella', section: 'bodega' }] })
    await vi.advanceTimersByTimeAsync(CATALOG_MS - 1000)
    expect(server.seen).toHaveLength(2)
    await vi.advanceTimersByTimeAsync(1000)
    expect(server.seen).toEqual([null, '"v1"', '"v1"'])
    expect(used.read.value?.items.map(item => item.id)).toEqual(['wine', 'water']); expect(used.read.value?.etag).toBe('"v2"')
    unmount()
    await vi.advanceTimersByTimeAsync(CATALOG_MS * 3)
    expect(server.seen).toHaveLength(3)
  })

  it('overlapping revalidations are one read', async () => {
    const server = engine()
    const { used, unmount } = mount(server.fetcher)
    try {
      await Promise.all([used.refresh(), used.refresh(), used.refresh()])
      expect(server.seen).toHaveLength(1)
    } finally { unmount() }
  })

  it('without the round announced by the server (add-consumptions) nothing is read', async () => {
    const server = engine()
    const { used, unmount } = mount(server.fetcher, () => false)
    try {
      await used.refresh()
      expect(server.seen).toEqual([]); expect(used.read.value).toBeNull()
    } finally { unmount() }
  })

  it('a failed read keeps what was read and says so; a 401 tells the app; a body with money is refused and the old list stays', async () => {
    const server = engine()
    let unauthorized = 0
    const { used, unmount } = mount(server.fetcher, () => true, () => { unauthorized++ })
    try {
      await used.refresh()
      const kept = used.read.value
      server.set({ status: 503 }); await used.refresh()
      expect(used.status.failed).toBe(true); expect(used.read.value).toBe(kept); expect(unauthorized).toBe(0)
      server.set({ status: 401 }); await used.refresh()
      expect(unauthorized).toBe(1)
      server.set({ status: 200, etag: '"v3"', body: [{ ...ROWS[0], priceCents: 950 }] }); await used.refresh()
      expect(used.status.failed).toBe(true); expect(used.read.value).toBe(kept)
      server.set({ body: ROWS }); await used.refresh()
      expect(used.status.failed).toBe(false); expect(used.read.value?.etag).toBe('"v3"')
    } finally { unmount() }
  })
})

describe('after reconnecting', () => {
  it('says «again» each time the live link comes back, never on the first connection', () => {
    const again = reconnections()
    expect(['connecting', 'live', 'reconnecting', 'live', 'polling', 'connecting', 'live'].map(again)).toEqual([false, false, false, true, false, false, true])
  })
})
