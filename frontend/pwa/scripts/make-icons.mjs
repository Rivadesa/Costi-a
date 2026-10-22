// Genera los iconos de Costina Sala y Costina Cocina (C3c) a partir de su UNICO dibujo, src/app-icons.ts: los PNG que piden Android
// (192, 512 y maskable de 512) e iOS (apple-touch-icon de 180, que ignora los iconos del manifiesto y los SVG, D6.5) y el icon.svg de
// cada manifiesto. Sin dependencias: rasterizador propio con supermuestreo y PNG con node:zlib, reproducible byte a byte.
// Uso (Node 23.6+, que lee TypeScript sin compilar):  node scripts/make-icons.mjs
// Los ficheros se versionan en public/sala/ y public/cocina/; el build no ejecuta este script.
import { crc32, deflateSync } from 'node:zlib'
import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { FRAME_RADIUS, GLOSS, ICON_ART, iconSvg, WAVE } from '../src/app-icons.ts'

const NAMES = { sala: 'Costiña Sala', cocina: 'Costiña Cocina' }
const SAMPLES = 4, CENTER = 48, WHITE = [255, 255, 255]
const rgb = hex => [1, 3, 5].map(at => parseInt(hex.slice(at, at + 2), 16))

// ---- Geometria en el espacio 96 x 96 del SVG ----
function inRect(x, y, { x: left, y: top, w, h, radii: [tl, tr, br, bl] }) {
  if (x < left || x > left + w || y < top || y > top + h) return false
  const corner = (r, cx, cy, inX, inY) => !(r > 0 && inX && inY) || Math.hypot(x - cx, y - cy) <= r
  return corner(tl, left + tl, top + tl, x < left + tl, y < top + tl) && corner(tr, left + w - tr, top + tr, x > left + w - tr, y < top + tr)
    && corner(br, left + w - br, top + h - br, x > left + w - br, y > top + h - br) && corner(bl, left + bl, top + h - bl, x < left + bl, y > top + h - bl)
}
const inFrame = (x, y) => inRect(x, y, { x: 0, y: 0, w: 96, h: 96, radii: [FRAME_RADIUS, FRAME_RADIUS, FRAME_RADIUS, FRAME_RADIUS] })

const cubic = (p0, p1, p2, p3, steps) => Array.from({ length: steps + 1 }, (_, i) => {
  const t = i / steps, u = 1 - t
  return [0, 1].map(k => u * u * u * p0[k] + 3 * u * u * t * p1[k] + 3 * u * t * t * p2[k] + t * t * t * p3[k])
})
// El borde inferior del brillo, de derecha a izquierda: y de la curva para cada x (la x decrece de forma monotona).
const glossCurve = cubic(GLOSS.from, GLOSS.c1, GLOSS.c2, GLOSS.to, 256)
const glossMemo = new Map()
const glossBottom = x => { if (!glossMemo.has(x)) glossMemo.set(x, glossY(x)); return glossMemo.get(x) }
function glossY(x) {
  for (let i = 1; i < glossCurve.length; i++) {
    const [x0, y0] = glossCurve[i - 1], [x1, y1] = glossCurve[i]
    if (x <= x0 && x >= x1) return x0 === x1 ? y0 : y0 + (y1 - y0) * (x0 - x) / (x0 - x1)
  }
  return x > GLOSS.from[0] ? GLOSS.from[1] : GLOSS.to[1]
}
// La tilde: dos medias ondas cubicas (c ... s ...), como polilinea; dentro si la distancia a ella es como mucho medio trazo.
function wavePoints({ x, y, amp }) {
  const { half, c1, c2 } = WAVE
  const first = cubic([x, y], [x + c1, y - amp], [x + c2, y - amp], [x + half, y], 48)
  const second = cubic([x + half, y], [x + half + (half - c2), y + amp], [x + half + c2, y + amp], [x + 2 * half, y], 48)
  return [...first, ...second.slice(1)]
}
function nearPolyline(px, py, points, reach) {
  for (let i = 1; i < points.length; i++) {
    const [ax, ay] = points[i - 1], [bx, by] = points[i], dx = bx - ax, dy = by - ay
    const t = Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    if (Math.hypot(px - ax - t * dx, py - ay - t * dy) <= reach) return true
  }
  return false
}
function inside(shape, x, y) {
  switch (shape.kind) {
    case 'rect': return inRect(x, y, shape)
    case 'circle': return Math.hypot(x - shape.cx, y - shape.cy) <= shape.r
    case 'dome': return y <= shape.cy && Math.hypot(x - shape.cx, y - shape.cy) <= shape.r
    case 'wave': return Math.abs(y - shape.y) <= shape.amp + shape.width && nearPolyline(x, y, shape.points, shape.width / 2)
  }
  throw new Error(`figura desconocida: ${shape.kind}`)
}

// rounded: el marco con sus esquinas y fondo transparente fuera. Si no: fondo a sangre (maskable e iOS, que recortan ellos).
// scale: acerca el dibujo al centro para que quepa en la zona segura de un icono maskable (circulo del 80 %).
function render(art, size, { rounded, scale }) {
  const background = rgb(art.background)
  const shapes = art.shapes.map(shape => ({ ...shape, color: rgb(shape.kind === 'wave' ? shape.stroke : shape.fill),
    ...(shape.kind === 'wave' ? { points: wavePoints(shape) } : {}) }))
  const pixels = Buffer.alloc(size * size * 4), total = SAMPLES * SAMPLES
  for (let py = 0; py < size; py++) for (let px = 0; px < size; px++) {
    let covered = 0
    const sum = [0, 0, 0]
    for (let sy = 0; sy < SAMPLES; sy++) for (let sx = 0; sx < SAMPLES; sx++) {
      const x = (px + (sx + 0.5) / SAMPLES) * 96 / size, y = (py + (sy + 0.5) / SAMPLES) * 96 / size
      if (rounded && !inFrame(x, y)) continue
      covered++
      let color = background
      if (y <= glossBottom(x)) color = color.map((value, k) => value * (1 - GLOSS.opacity) + WHITE[k] * GLOSS.opacity)
      const u = CENTER + (x - CENTER) / scale, v = CENTER + (y - CENTER) / scale
      for (const shape of shapes) if (inside(shape, u, v)) color = shape.color
      for (let k = 0; k < 3; k++) sum[k] += color[k]
    }
    if (covered === 0) continue
    const offset = (py * size + px) * 4
    for (let k = 0; k < 3; k++) pixels[offset + k] = Math.round(sum[k] / covered)
    pixels[offset + 3] = Math.round(255 * covered / total)
  }
  return pixels
}

function png(size, pixels) {
  const chunk = (type, data) => {
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]), out = Buffer.alloc(body.length + 8)
    out.writeUInt32BE(data.length, 0); body.copy(out, 4); out.writeUInt32BE(crc32(body) >>> 0, body.length + 4)
    return out
  }
  const header = Buffer.alloc(13)
  header.writeUInt32BE(size, 0); header.writeUInt32BE(size, 4); header[8] = 8; header[9] = 6   // 8 bits, RGBA
  const rows = Buffer.alloc(size * (size * 4 + 1))
  for (let y = 0; y < size; y++) pixels.copy(rows, y * (size * 4 + 1) + 1, y * size * 4, (y + 1) * size * 4)   // filtro 0 por fila
  return Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk('IHDR', header), chunk('IDAT', deflateSync(rows, { level: 9 })), chunk('IEND', Buffer.alloc(0))])
}

const ICONS = [
  ['icon-192.png', 192, { rounded: true, scale: 1 }],
  ['icon-512.png', 512, { rounded: true, scale: 1 }],
  ['icon-maskable-512.png', 512, { rounded: false, scale: 0.8 }],
  ['apple-touch-icon.png', 180, { rounded: false, scale: 0.9 }],
]
const out = (app, name) => fileURLToPath(new URL(`../public/${app}/${name}`, import.meta.url))
for (const app of Object.keys(ICON_ART)) {
  for (const [name, size, options] of ICONS) {
    writeFileSync(out(app, name), png(size, render(ICON_ART[app], size, options)))
    console.log(`${app}/${name}`, size)
  }
  writeFileSync(out(app, 'icon.svg'), iconSvg(app, NAMES[app]))
  console.log(`${app}/icon.svg`)
}
