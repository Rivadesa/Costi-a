import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { inflateSync } from 'node:zlib'
import { describe, expect, it } from 'vitest'

// D6.5: la PWA es instalable en Android e iOS. Lo que declara el manifiesto existe de verdad y mide lo que dice.
const file = (path: string) => readFileSync(fileURLToPath(new URL(`../${path}`, import.meta.url)))
const view = (data: Uint8Array) => new DataView(data.buffer, data.byteOffset, data.byteLength)
const pngSize = (data: Uint8Array) => {
  expect(Array.from(data.subarray(0, 8))).toEqual([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])
  return `${view(data).getUint32(16)}x${view(data).getUint32(20)}`
}
const manifest = JSON.parse(file('public/manifest.webmanifest').toString('utf8')) as {
  start_url: string; scope: string; display: string; icons: Array<{ src: string; sizes: string; type: string; purpose: string }>
}
const html = file('index.html').toString('utf8')

describe('installable PWA', () => {
  it('stays inside /app/ and opens standalone', () => {
    expect([manifest.start_url, manifest.scope, manifest.display]).toEqual(['/app/', '/app/', 'standalone'])
  })
  it('declares 192 and 512 PNG icons plus a maskable one, and every declared PNG has its real size', () => {
    const pngs = manifest.icons.filter(icon => icon.type === 'image/png')
    expect(pngs.map(icon => `${icon.sizes} ${icon.purpose}`).sort()).toEqual(['192x192 any', '512x512 any', '512x512 maskable'])
    for (const icon of pngs) {
      expect(icon.src.startsWith('/app/')).toBe(true)                       // mismo origen, bajo el alcance del shell
      expect(pngSize(file(`public/${icon.src.slice('/app/'.length)}`))).toBe(icon.sizes)
    }
  })
  it('a maskable icon is full-bleed (opaque corners); the regular one keeps transparent rounded corners', () => {
    const cornerAlpha = (name: string) => {                                  // alfa del pixel (0,0): fila 0 = [filtro, R, G, B, A, ...]
      const data: Uint8Array = file(`public/${name}`); const parts: number[] = []
      for (let at = 8; at < data.length;) {
        const length = view(data).getUint32(at), type = String.fromCharCode(...data.subarray(at + 4, at + 8))
        if (type === 'IDAT') parts.push(...data.subarray(at + 8, at + 8 + length))
        at += 12 + length
      }
      return inflateSync(Uint8Array.from(parts))[4]
    }
    expect(cornerAlpha('icon-maskable-512.png')).toBe(255)                   // Android recorta la mascara: sin esquinas transparentes
    expect(cornerAlpha('apple-touch-icon.png')).toBe(255)                    // iOS pinta negro lo transparente
    expect(cornerAlpha('icon-512.png')).toBe(0)
  })
  it('gives iOS its own 180 px PNG (it ignores manifest icons and SVG) and the home-screen metadata', () => {
    expect(pngSize(file('public/apple-touch-icon.png'))).toBe('180x180')
    expect(html).toContain('rel="apple-touch-icon" href="/app/apple-touch-icon.png"')
    for (const meta of ['apple-mobile-web-app-capable', 'mobile-web-app-capable', 'apple-mobile-web-app-title', 'theme-color', 'viewport-fit=cover'])
      expect(html).toContain(meta)
  })
  it('loads nothing from third parties and has no inline script or style (CSP of the engine)', () => {
    expect(html).not.toMatch(/https?:\/\//)
    expect(html).not.toMatch(/<style|style=|<script(?![^>]*\bsrc=)/)
  })
})
