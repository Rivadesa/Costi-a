import type { AppId } from './apps'

// C3c: los iconos de Costina Sala y Costina Cocina, la UNICA fuente de su dibujo (maqueta docs/plans/maquetas/2026-10-22-sala-y-cocina.html).
// Misma familia: marco redondeado, dibujo blanco y la tilde de Costina (la onda). Sala: un plato con campana sobre el verde de acento.
// Cocina: un gorro de cocinero sobre azul pizarra (ni rojo, que es de las alergias, ni ambar, que avisa). Coordenadas en 96 x 96.
// De aqui salen el SVG de la cabecera (views/AppIcon.vue), el icon.svg de cada manifiesto y los PNG que pinta scripts/make-icons.mjs
// (que importa este fichero tal cual: por eso no importa nada que no sean tipos). Trazos propios, sin imagenes externas.
export type Shape =
  | { kind: 'rect'; x: number; y: number; w: number; h: number; radii: readonly [number, number, number, number]; fill: string }   // esquinas: arriba izq., arriba der., abajo der., abajo izq.
  | { kind: 'circle'; cx: number; cy: number; r: number; fill: string }
  | { kind: 'dome'; cx: number; cy: number; r: number; fill: string }                                       // medio disco de arriba
  | { kind: 'wave'; x: number; y: number; amp: number; stroke: string; width: number }                     // la tilde, 19 de ancho

export interface IconArt { background: string; shapes: readonly Shape[] }

const WHITE = '#FFFFFF'
export const ICON_ART: Readonly<Record<AppId, IconArt>> = {
  sala: {
    background: '#0E7A5F',
    shapes: [
      { kind: 'wave', x: 38.5, y: 27.5, amp: 3.8, stroke: WHITE, width: 4.4 },
      { kind: 'dome', cx: 48, cy: 64, r: 28, fill: WHITE },
      { kind: 'rect', x: 14, y: 67.5, w: 68, h: 7, radii: [3.5, 3.5, 3.5, 3.5], fill: WHITE },
    ],
  },
  cocina: {
    background: '#2F4A6D',
    shapes: [
      { kind: 'circle', cx: 33, cy: 45, r: 13, fill: WHITE },
      { kind: 'circle', cx: 63, cy: 45, r: 13, fill: WHITE },
      { kind: 'circle', cx: 48, cy: 36, r: 16, fill: WHITE },
      { kind: 'rect', x: 27, y: 42, w: 42, h: 15, radii: [0, 0, 0, 0], fill: WHITE },
      { kind: 'rect', x: 29, y: 61, w: 38, h: 14, radii: [0, 0, 4, 4], fill: WHITE },
      { kind: 'wave', x: 38.5, y: 68, amp: 3.6, stroke: '#2F4A6D', width: 3.8 },
    ],
  },
}

// Marco y brillo: el cuadrado de 96 con esquinas de 22, y arriba un velo blanco al 7 % que acaba en una curva suave.
export const FRAME_RADIUS = 22
// El velo va del lado derecho (96, 36) al izquierdo (0, 42).
export const GLOSS = { opacity: 0.07, from: [96, 36], c1: [66, 26], c2: [30, 26], to: [0, 42] } as const
export const WAVE = { half: 9.5, c1: 3.2, c2: 6.3 } as const   // cada media onda: 9,5 de ancho con sus puntos de control

const n = (value: number) => String(Math.round(value * 100) / 100)
function rectPath(shape: Extract<Shape, { kind: 'rect' }>): string {
  const { x, y, w, h, radii: [tl, tr, br, bl] } = shape
  const arc = (r: number, dx: number, dy: number) => r > 0 ? `a${n(r)} ${n(r)} 0 0 1 ${n(dx)} ${n(dy)}` : ''
  return `M${n(x + tl)} ${n(y)}h${n(w - tl - tr)}${arc(tr, tr, tr)}v${n(h - tr - br)}${arc(br, -br, br)}h${n(-(w - br - bl))}${arc(bl, -bl, -bl)}v${n(-(h - bl - tl))}${arc(tl, tl, -tl)}z`
}
export function shapeSvg(shape: Shape): string {
  switch (shape.kind) {
    case 'rect': return `<path d="${rectPath(shape)}" fill="${shape.fill}"/>`
    case 'circle': return `<circle cx="${n(shape.cx)}" cy="${n(shape.cy)}" r="${n(shape.r)}" fill="${shape.fill}"/>`
    case 'dome': return `<path d="M${n(shape.cx - shape.r)} ${n(shape.cy)}a${n(shape.r)} ${n(shape.r)} 0 0 1 ${n(2 * shape.r)} 0z" fill="${shape.fill}"/>`
    case 'wave': return `<path d="M${n(shape.x)} ${n(shape.y)}c${n(WAVE.c1)} ${n(-shape.amp)} ${n(WAVE.c2)} ${n(-shape.amp)} ${n(WAVE.half)} 0s${n(WAVE.c2)} ${n(shape.amp)} ${n(WAVE.half)} 0" fill="none" stroke="${shape.stroke}" stroke-width="${n(shape.width)}" stroke-linecap="round"/>`
  }
}
// El dibujo entero (sin la etiqueta <svg>): marco, brillo y figuras.
export function iconBody(app: AppId): string {
  const art = ICON_ART[app], r = FRAME_RADIUS, fy = GLOSS.from[1], [ax, ay] = GLOSS.c1, [bx, by] = GLOSS.c2, [tx, ty] = GLOSS.to
  return `<rect width="96" height="96" rx="${r}" fill="${art.background}"/>`
    + `<path d="M0 ${r}A${r} ${r} 0 0 1 ${r} 0h${96 - 2 * r}a${r} ${r} 0 0 1 ${r} ${r}V${fy}C${ax} ${ay} ${bx} ${by} ${tx} ${ty}z" fill="#FFFFFF" fill-opacity="${GLOSS.opacity}"/>`
    + art.shapes.map(shapeSvg).join('')
}
// El fichero icon.svg de cada aplicacion (lo escribe scripts/make-icons.mjs). El nombre llega de fuera: este fichero no importa nada.
export const iconSvg = (app: AppId, label: string): string =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" role="img" aria-label="${label}">${iconBody(app)}</svg>\n`
