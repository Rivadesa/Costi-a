// Genera los iconos PNG de la PWA a partir de la MISMA geometria que public/icon.svg (D6.5). Sin dependencias:
// rasterizador propio con supermuestreo y PNG con node:zlib, para que el resultado sea reproducible byte a byte.
// Uso (Node 22.2+):  node scripts/make-icons.mjs      Los PNG se versionan; el build no ejecuta este script.
import { crc32, deflateSync } from 'node:zlib'
import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const BACKGROUND = [0x1f, 0x2a, 0x37], FOREGROUND = [0xf3, 0xf5, 0xf8]
const CENTER = 256, SAMPLES = 4

// Geometria en el espacio 512x512 del SVG.
const inRoundedSquare = (x, y, radius) => {
  const dx = Math.max(radius - x, x - (512 - radius), 0), dy = Math.max(radius - y, y - (512 - radius), 0)
  return x >= 0 && x <= 512 && y >= 0 && y <= 512 && dx * dx + dy * dy <= radius * radius
}
const inRing = (x, y, radius, width) => Math.abs(Math.hypot(x - CENTER, y - CENTER) - radius) <= width / 2
const inCapsule = (x, y, y0, y1, width) => Math.hypot(x - CENTER, y - Math.min(Math.max(y, y0), y1)) <= width / 2
const inMark = (x, y) => inRing(x, y, 150, 28) || inRing(x, y, 86, 16) || inCapsule(x, y, 66, 126, 20) || inCapsule(x, y, 386, 446, 20)

// rounded: esquinas del SVG y fondo transparente fuera. Si no: fondo a sangre (maskable y iOS, que recortan ellos).
// scale: reduce la marca hacia el centro para que quepa en la zona segura de un icono maskable (circulo del 80 %).
function render(size, { rounded, scale }) {
  const pixels = Buffer.alloc(size * size * 4)
  for (let py = 0; py < size; py++) for (let px = 0; px < size; px++) {
    let inside = 0, mark = 0
    for (let sy = 0; sy < SAMPLES; sy++) for (let sx = 0; sx < SAMPLES; sx++) {
      const x = (px + (sx + 0.5) / SAMPLES) * 512 / size, y = (py + (sy + 0.5) / SAMPLES) * 512 / size
      if (rounded && !inRoundedSquare(x, y, 96)) continue
      inside++
      if (inMark(CENTER + (x - CENTER) / scale, CENTER + (y - CENTER) / scale)) mark++
    }
    const offset = (py * size + px) * 4, total = SAMPLES * SAMPLES
    if (inside === 0) continue
    for (let channel = 0; channel < 3; channel++)
      pixels[offset + channel] = Math.round((FOREGROUND[channel] * mark + BACKGROUND[channel] * (inside - mark)) / inside)
    pixels[offset + 3] = Math.round(255 * inside / total)
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
for (const [name, size, options] of ICONS) {
  const file = fileURLToPath(new URL(`../public/${name}`, import.meta.url))
  writeFileSync(file, png(size, render(size, options)))
  console.log(name, size)
}
