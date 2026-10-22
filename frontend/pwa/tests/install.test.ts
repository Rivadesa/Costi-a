import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { inflateSync } from 'node:zlib'
import { describe, expect, it } from 'vitest'
import { iconSvg } from '../src/app-icons'
import { APP_IDS, APPS, type AppId } from '../src/apps'

// D6.5 y C3c: DOS aplicaciones instalables en Android e iOS, Costina Sala (/app/sala/) y Costina Cocina (/app/cocina/), cada una con
// su manifiesto, su id y su alcance, su nombre, su color, sus iconos y su service worker; y /app/, que solo enruta y no se instala.
// Lo que declara cada manifiesto existe de verdad y mide lo que dice.
const file = (path: string) => readFileSync(fileURLToPath(new URL(`../${path}`, import.meta.url)))
const text = (path: string) => file(path).toString('utf8')
const view = (data: Uint8Array) => new DataView(data.buffer, data.byteOffset, data.byteLength)
const pngSize = (data: Uint8Array) => {
  expect(Array.from(data.subarray(0, 8))).toEqual([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])
  return `${view(data).getUint32(16)}x${view(data).getUint32(20)}`
}
// Pixel RGBA (x, y) de un PNG RGBA de 8 bits sin filtro por fila, como los escribe scripts/make-icons.mjs.
function pixel(name: string, x: number, y: number): number[] {
  const data: Uint8Array = file(name); const parts: number[] = []
  for (let at = 8; at < data.length;) {
    const length = view(data).getUint32(at), type = String.fromCharCode(...data.subarray(at + 4, at + 8))
    if (type === 'IDAT') parts.push(...data.subarray(at + 8, at + 8 + length))
    at += 12 + length
  }
  const width = view(data).getUint32(16), rows = inflateSync(Uint8Array.from(parts)), start = y * (width * 4 + 1) + 1 + x * 4
  return Array.from(rows.subarray(start, start + 4))
}
const rgb = (hex: string) => [1, 3, 5].map(at => parseInt(hex.slice(at, at + 2), 16))
type Manifest = {
  id: string; name: string; short_name: string; start_url: string; scope: string; display: string; lang: string; theme_color: string; background_color: string
  icons: Array<{ src: string; sizes: string; type: string; purpose: string }>
}
const manifestOf = (app: AppId) => JSON.parse(text(`public/${app}/manifest.webmanifest`)) as Manifest
const metaOf = (html: string, name: string) => new RegExp(`<meta name="${name}" content="([^"]*)"`).exec(html)?.[1]
const SHORT: Record<AppId, string> = { sala: 'Costiña Sala', cocina: 'Cocina' }   // «Costina Cocina» (14) lo cortan muchos lanzadores

describe.each(APP_IDS)('installable app %s', app => {
  const manifest = manifestOf(app), html = text(`${app}/index.html`), path = APPS[app].path
  it('has its own id, scope and start inside its folder, and opens standalone', () => {
    expect([manifest.id, manifest.start_url, manifest.scope, manifest.display, manifest.lang]).toEqual([path, path, path, 'standalone', 'es'])
  })
  it('has its name, a short name launchers do not cut, its colour only in theme_color and the light background of always', () => {
    expect([manifest.name, manifest.short_name]).toEqual([APPS[app].name, SHORT[app]])
    expect(manifest.short_name.length).toBeLessThanOrEqual(12)
    expect([manifest.theme_color, manifest.background_color]).toEqual([APPS[app].color, '#F3F4F6'])
  })
  it('declares 192 and 512 PNG icons plus a maskable one, all its own, each with its real size', () => {
    const pngs = manifest.icons.filter(icon => icon.type === 'image/png')
    expect(pngs.map(icon => `${icon.sizes} ${icon.purpose}`).sort()).toEqual(['192x192 any', '512x512 any', '512x512 maskable'])
    for (const icon of manifest.icons) expect(icon.src.startsWith(path), icon.src).toBe(true)        // nada de la otra aplicacion ni del enrutador
    for (const icon of pngs) expect(pngSize(file(`public/${icon.src.slice('/app/'.length)}`))).toBe(icon.sizes)
  })
  it('a maskable icon is full-bleed (opaque corners); the regular one keeps transparent rounded corners', () => {
    expect(pixel(`public/${app}/icon-maskable-512.png`, 0, 0)[3]).toBe(255)                         // Android recorta la mascara
    expect(pixel(`public/${app}/apple-touch-icon.png`, 0, 0)[3]).toBe(255)                          // iOS pinta negro lo transparente
    expect(pixel(`public/${app}/icon-512.png`, 0, 0)[3]).toBe(0)
  })
  it('the PNGs are painted in the colour of the app, with the white drawing in the middle', () => {
    expect(pixel(`public/${app}/icon-512.png`, 256, 470)).toEqual([...rgb(APPS[app].color), 255])    // fondo, bajo el dibujo y el brillo
    expect(pixel(`public/${app}/icon-512.png`, 256, 300)).toEqual([255, 255, 255, 255])              // la campana o el gorro
    expect(pixel(`public/${app}/apple-touch-icon.png`, 90, 175)).toEqual([...rgb(APPS[app].color), 255])
  })
  it('the SVG icon is the one drawing of the header (app-icons.ts), with its name', () => {
    expect(text(`public/${app}/icon.svg`).replace(/\r\n/g, '\n')).toBe(iconSvg(app, APPS[app].name))   // en Windows, git lo saca con CRLF
  })
  it('gives iOS its own 180 px PNG (it ignores manifest icons and SVG) and the home-screen metadata, with the full name', () => {
    expect(pngSize(file(`public/${app}/apple-touch-icon.png`))).toBe('180x180')
    expect(html).toContain(`rel="apple-touch-icon" href="${path}apple-touch-icon.png"`)
    expect(html).toContain(`rel="manifest" href="${path}manifest.webmanifest"`)
    expect(html).toContain(`<title>${APPS[app].name}</title>`)
    expect([metaOf(html, 'apple-mobile-web-app-title'), metaOf(html, 'application-name')]).toEqual([APPS[app].name, APPS[app].name])
    expect([metaOf(html, 'apple-mobile-web-app-capable'), metaOf(html, 'mobile-web-app-capable'), metaOf(html, 'theme-color')]).toEqual(['yes', 'yes', APPS[app].color])
    expect(html).toContain('viewport-fit=cover')
    expect(html).toContain(`<script type="module" src="/src/main-${app}.ts"></script>`)
  })
  it('loads nothing from third parties and has no inline script or style (CSP of the engine)', () => {
    expect(html).not.toMatch(/https?:\/\//)
    expect(html).not.toMatch(/<style|style=|<script(?![^>]*\bsrc=)/)
  })
  it('chooses ITS database before mounting anything, and registers its own worker', () => {
    const entry = text(`src/main-${app}.ts`)
    expect(entry).toContain(`selectDatabase(APPS.${app}.database)`)
    expect(entry.indexOf('selectDatabase(')).toBeLessThan(entry.indexOf('.mount('))
    expect(entry).toContain(`startWorkers('${app}')`)
  })
})

describe('the /app/ router is not an app', () => {
  const html = text('index.html')
  it('has no manifest and none of the iOS home-screen metadata, so it cannot be installed', () => {
    expect(html).not.toMatch(/rel="manifest"|apple-mobile-web-app|mobile-web-app-capable|apple-touch-icon/)
    expect(existsSync(fileURLToPath(new URL('../public/manifest.webmanifest', import.meta.url)))).toBe(false)
    expect(html).toContain('<title>Costiña</title>')
    expect(html).toContain('<script type="module" src="/src/main.ts"></script>')
    expect(html).not.toMatch(/https?:\/\/|<style|style=|<script(?![^>]*\bsrc=)/)
  })
  it('one build, three pages', () => {
    const config = text('vite.config.ts')
    for (const page of ["'./index.html'", "'./sala/index.html'", "'./cocina/index.html'"]) expect(config).toContain(page)
    expect(config).toContain("base: '/app/'")
  })
})

// ---- Los service workers, ejecutados con un navegador de mentira: su `self`, sus caches y la red ----
type Stored = Map<string, Response>
interface FakeEvent { waitUntil(promise: Promise<unknown>): void; respondWith(promise: Promise<Response>): void; request: Request }
function worker(source: string, scriptUrl: string, network: (url: string) => Promise<Response>) {
  const origin = new URL(scriptUrl).origin
  const listeners: Record<string, (event: FakeEvent) => void> = {}
  const store = new Map<string, Stored>()
  const key = (request: Request | string) => new URL(typeof request === 'string' ? request : request.url, origin).href
  const fetcher = (request: Request | string) => network(key(request))
  let unregistered = false
  const caches = {
    keys: async () => [...store.keys()],
    delete: async (name: string) => store.delete(name),
    match: async (request: Request | string) => { for (const cache of store.values()) { const hit = cache.get(key(request)); if (hit) return hit.clone() } return undefined },
    open: async (name: string) => {
      if (!store.has(name)) store.set(name, new Map())
      const cache = store.get(name)!
      const add = async (request: Request | string) => { const response = await fetcher(request); if (!response.ok) throw new TypeError('add'); cache.set(key(request), response) }
      return {
        add, addAll: async (requests: string[]) => { for (const request of requests) await add(request) },
        put: async (request: Request | string, response: Response) => { cache.set(key(request), response) },
        match: async (request: Request | string) => cache.get(key(request))?.clone(),
      }
    },
  }
  const self = {
    location: new URL(scriptUrl), addEventListener: (type: string, handler: (event: FakeEvent) => void) => { listeners[type] = handler },
    skipWaiting: async () => undefined, clients: { claim: async () => undefined },
    registration: { unregister: async () => { unregistered = true; return true } },
  }
  new Function('self', 'caches', 'fetch', source)(self, caches, fetcher)
  async function fire(type: string, request?: Request): Promise<Response | null> {
    const waits: Array<Promise<unknown>> = []; let answer: Promise<Response> | null = null
    listeners[type]?.({ waitUntil: promise => { waits.push(promise) }, respondWith: promise => { answer = promise }, request: request! })
    await Promise.all(waits)
    return answer
  }
  const paths = (name: string) => [...(store.get(name)?.keys() ?? [])].map(url => new URL(url).pathname).sort()
  return { listeners, store, fire, paths, unregistered: () => unregistered, origin }
}
const ORIGIN = 'https://equipo:5443'
const page = (app: string) => `<html><head><script type="module" src="/app/assets/${app}-1.js"></script><link rel="modulepreload" href="/app/assets/shared-2.js"><link rel="stylesheet" href="/app/assets/style-3.css"></head></html>`
const site = (online = true) => async (url: string) => {
  if (!online) throw new TypeError('sin red')
  const path = new URL(url).pathname
  return new Response(path.endsWith('/') ? page(path.split('/')[2]) : `contenido de ${path}`, { status: 200 })
}

describe('one service worker per app (C3c)', () => {
  const source = text('public/sala/sw.js')
  it('Sala and Cocina ship the SAME worker; each one learns its folder from its own address', () => {
    expect(text('public/cocina/sw.js')).toBe(source)
    expect(source).not.toMatch(/['"`]\/api/)
    expect(source).not.toMatch(/addEventListener\(['"](sync|periodicsync)|\.sync\b|SyncManager/)   // sin Background Sync
  })
  it('installs its page, manifest, icons and the hashed files its page asks for, and nothing of anybody else', async () => {
    const sala = worker(source, `${ORIGIN}/app/sala/sw.js`, site())
    await sala.fire('install')
    expect([...sala.store.keys()]).toEqual(['costina-sala-v1'])
    expect(sala.paths('costina-sala-v1')).toEqual(['/app/assets/sala-1.js', '/app/assets/shared-2.js', '/app/assets/style-3.css', '/app/sala/',
      '/app/sala/apple-touch-icon.png', '/app/sala/icon-192.png', '/app/sala/icon.svg', '/app/sala/manifest.webmanifest'])
    const cocina = worker(source, `${ORIGIN}/app/cocina/sw.js`, site())
    await cocina.fire('install')
    expect([...cocina.store.keys()]).toEqual(['costina-cocina-v1'])
    expect(cocina.paths('costina-cocina-v1').filter(path => !path.startsWith('/app/cocina/') && !path.startsWith('/app/assets/'))).toEqual([])
  })
  it('on activation deletes ONLY the old caches of its own prefix: never the other app, never anything else', async () => {
    const sala = worker(source, `${ORIGIN}/app/sala/sw.js`, site())
    for (const name of ['costina-sala-v0', 'costina-sala-v1', 'costina-cocina-v1', 'costina-cocina-v0', 'costina-shell-v1', 'otra-cosa']) await (await caches(sala)).open(name)
    await sala.fire('activate')
    expect([...sala.store.keys()].sort()).toEqual(['costina-cocina-v0', 'costina-cocina-v1', 'costina-sala-v1', 'costina-shell-v1', 'otra-cosa'])
  })
  it('never answers for /api/, /health, the router, the other app, another origin or a POST', async () => {
    const sala = worker(source, `${ORIGIN}/app/sala/sw.js`, site())
    for (const url of ['/api/native/v1/session', '/api/native/v1/dining/board', '/health', '/app/', '/app/index.html', '/app/cocina/', '/app/cocina/sw.js', '/app/icon.svg'])
      expect(await sala.fire('fetch', new Request(ORIGIN + url)), url).toBeNull()
    expect(await sala.fire('fetch', new Request('https://otro:5443/app/sala/'))).toBeNull()
    expect(await sala.fire('fetch', new Request(`${ORIGIN}/app/sala/`, { method: 'POST', body: '{}' }))).toBeNull()
    expect(await sala.fire('fetch', new Request(`${ORIGIN}/app/sala/`))).not.toBeNull()
    expect(await sala.fire('fetch', new Request(`${ORIGIN}/app/assets/shared-2.js`))).not.toBeNull()
  })
  it('without network it answers from ITS cache only (never a hit in the other app\'s cache) and falls back to its page', async () => {
    let online = true
    const sala = worker(source, `${ORIGIN}/app/sala/sw.js`, url => site(online)(url))
    await sala.fire('install')
    await (await (await caches(sala)).open('costina-cocina-v1')).put(`${ORIGIN}/app/sala/extra`, new Response('de cocina'))
    online = false
    const fallback = await (await sala.fire('fetch', new Request(`${ORIGIN}/app/sala/extra`)))!
    expect(await fallback.text()).toBe(page('sala'))
    expect(await (await sala.fire('fetch', new Request(`${ORIGIN}/app/assets/sala-1.js`)))!.text()).toBe('contenido de /app/assets/sala-1.js')
  })
})

describe('the old /app/ worker retires', () => {
  const source = text('public/sw.js')
  it('intercepts nothing, deletes only the old shell cache and unregisters itself', async () => {
    const old = worker(source, `${ORIGIN}/app/sw.js`, site())
    for (const name of ['costina-shell-v1', 'costina-sala-v1', 'costina-cocina-v1']) await (await caches(old)).open(name)
    expect(Object.keys(old.listeners).sort()).toEqual(['activate', 'install'])
    await old.fire('install'); await old.fire('activate')
    expect([...old.store.keys()].sort()).toEqual(['costina-cocina-v1', 'costina-sala-v1'])
    expect(old.unregistered()).toBe(true)
    expect(source).not.toMatch(/['"`]\/api/)
  })
})

// Las caches del navegador de mentira de un worker (las mismas que ve su `self`).
async function caches(running: ReturnType<typeof worker>) {
  return { open: async (name: string) => {
    if (!running.store.has(name)) running.store.set(name, new Map())
    const cache = running.store.get(name)!
    return { put: async (url: string, response: Response) => { cache.set(new URL(url, running.origin).href, response) } }
  } }
}
