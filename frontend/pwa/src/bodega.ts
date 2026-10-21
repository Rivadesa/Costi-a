import { REJECTIONS, type Outcome } from './commands'
import { ADD, byGlass, familyChips, inFamily, lineText, type DrinkProduct } from './drinks'
import { guestLabel, plural, restrictionLabel } from './labels'
import { MAX_LINES, MAX_QUANTITY, roundLineText, type AddResult, type RoundLine, type Settlement } from './round'
import type { GuestRestriction, TableConsumption } from './types'

// C3b: lo que PINTA «Bodega y bebidas» del comandero, sin DOM (plan 2026-10-15, seccion 4.4; maqueta, pestanas «2 · Bodega y bebidas»
// y «2b · La ronda»). Es solo de la PWA: lo comun con el selector del puesto (busqueda, agrupacion y ronda) esta en search.ts, drinks.ts
// y round.ts, con su tabla de casos compartida. Aqui no hay ninguna regla de negocio: solo textos, accesos y el resultado de una orden.
// Sin palabras de caja: ni «precio», ni «cuenta», ni «pago», ni «saldo», ni «€» (tests/bodega.test.ts lo comprueba).

// ---- Accesos: «Por copa» (si el servidor marca alguna copa) y una ficha por familia. El primero es la vista de entrada. ----
export const GLASS_CHIP = 'glass'
export const OTHER_CHIP = 'other'
export interface DrinkChip { key: string; label: string; count: number; glass: boolean; family: string | null }

export function drinkChips(products: readonly DrinkProduct[]): DrinkChip[] {
  const glasses = byGlass(products).length
  // La clave es la familia (no su posicion): una relectura del catalogo con otra familia nueva no cambia lo elegido.
  const families = familyChips(products).map(chip => ({ key: chip.family === null ? OTHER_CHIP : `family:${chip.family}`, label: chip.label, count: chip.count, glass: false, family: chip.family }))
  return glasses > 0 ? [{ key: GLASS_CHIP, label: 'Por copa', count: glasses, glass: true, family: null }, ...families] : families
}

// La elegida, si sigue; si no (una familia que ya no tiene nada a la venta), la primera.
export const chosenChip = (chips: readonly DrinkChip[], key: string | null): DrinkChip | null => chips.find(chip => chip.key === key) ?? chips[0] ?? null

export function chipProducts(products: readonly DrinkProduct[], chip: DrinkChip | null, subfamily: string | null): DrinkProduct[] {
  if (chip === null) return []
  return chip.glass ? byGlass(products) : inFamily(products, chip.family, subfamily)
}

export const chipHeader = (chip: DrinkChip, count: number): string =>
  `${chip.glass ? plural(count, 'vino por copa', 'vinos por copa') : plural(count, 'producto', 'productos')} · de la A a la Z`

export const ONE_MORE_LETTER = 'Escribe una letra más para buscar.'
export const resultsCount = (count: number): string => plural(count, 'resultado', 'resultados')
export const RESULTS_WHERE = 'en toda la bodega y bebidas'
export const noResults = (query: string): string => `No hay nada con «${query.trim()}».`
export const NO_RESULTS_HINT = 'Busca por nombre, familia, región o referencia. La uva o la añada, solo si están en el nombre.'

// Las filas se pintan por tandas: una familia de 450 vinos no llena el movil de golpe.
export const PAGE = 60

// ---- Botones de una fila ----
// «Botella 0.75 L» se escribe en dos alturas («Botella» y, debajo, «0.75 L»), como en la maqueta; «Copa» y «Añadir», en una.
export function formatParts(label: string): [string, string] {
  if (label === ADD) return [label, '']
  const text = label.trim(), cut = text.indexOf(' ')
  return cut < 0 ? [text, ''] : [text.slice(0, cut), text.slice(cut + 1).trim()]
}
// Lo que dice el boton a un lector de pantalla: «Añadir 1 Copa de Albariño» o, con una sola presentacion, «Añadir 1 Agua con gas».
export const addLabel = (name: string, presentationId: string, presentation: string, single: boolean): string =>
  single ? `Añadir 1 ${lineText(name, presentationId, presentation)}` : `Añadir 1 ${presentation} de ${name}`

// Lo que contesta la ronda a un toque que no suma (los limites del servidor, o la ronda ya se esta enviando).
export function addProblem(result: AddResult): string {
  switch (result) {
    case 'full': return `La ronda ya tiene ${MAX_LINES} líneas: envíala y sigue en otra.`
    case 'max': return `Como mucho ${MAX_QUANTITY} en una misma línea de la ronda.`
    case 'sending': return 'La ronda se está enviando: espera a que termine.'
    default: return ''
  }
}

// ---- La ronda en la bandeja ----
export const EMPTY_ROUND = 'Ronda vacía. Copa o Botella suman 1 para toda la mesa.'
export const ROUND_SENT = 'Ronda enviada: ya sale en «Lo que lleva la mesa».'
export const ROUND_HINT = 'Sin enviar. Cambia la cantidad con − y +, o para quién en «Para». Si la página se recarga, la ronda se pierde.'
export const ROUND_CONFLICT = 'La mesa ha cambiado mientras tanto y se ha vuelto a leer: revisa la ronda y envíala de nuevo.'
export const roundNames = (lines: readonly RoundLine[]): string => lines.map(roundLineText).join(', ')
export const forLabel = (guestPosition: number | null): string => `Para: ${guestLabel(guestPosition)}`

// El ultimo rechazo, con la linea que el servidor dice (desde 0) si la hay y sigue en la ronda.
export function roundProblem(problem: { code: string; line: number | null } | null, lines: readonly RoundLine[]): string {
  if (problem === null) return ''
  if (problem.code === 'version_conflict') return ROUND_CONFLICT
  const text = REJECTIONS[problem.code] ?? `El servidor rechazó la ronda (${problem.code}). No se ha añadido nada.`
  const line = problem.line === null ? undefined : lines[problem.line]
  return line ? `Línea ${(problem.line ?? 0) + 1}, ${roundLineText(line)}: ${text}` : text
}

// ---- Hoja de una linea: cantidad y para quien ----
export interface GuestChoice { position: number | null; label: string; restricted: boolean; aria: string }
export function guestChoices(pax: number, restrictions: readonly GuestRestriction[]): GuestChoice[] {
  const choices: GuestChoice[] = [{ position: null, label: 'Toda la mesa', restricted: false, aria: 'Toda la mesa' }]
  for (let position = 1; position <= Math.max(0, pax); position++) {
    const own = restrictions.filter(restriction => restriction.guestPosition === position)
    choices.push({ position, label: String(position), restricted: own.length > 0, aria: [guestLabel(position), ...own.map(restrictionLabel)].join(', ') })
  }
  return choices
}
// Las restricciones de la mesa, escritas (siempre texto, nunca solo el punto rojo del comensal).
export const restrictionsNote = (restrictions: readonly GuestRestriction[]): string => restrictions.map(restrictionLabel).join(' · ')
export const doneDetail = (quantity: number, guestPosition: number | null): string =>
  `${quantity} · ${guestPosition === null ? 'toda la mesa' : `comensal ${guestPosition}`}`

// ---- Salir de la mesa con una ronda sin enviar ----
export const leaveText = (units: number): string => `La ronda tiene ${units} sin enviar. Si sales de la mesa, se pierde: nada de ella ha llegado al servidor.`

// ---- Lo que lleva la mesa (ADR-007, apartado C3b: no es la cuenta provisional) ----
export const HAD_TITLE = 'Lo que lleva la mesa'
export const HAD_SUB = 'Bebidas y extras ya enviados'
export const HAD_EMPTY = 'Aún no se ha enviado ninguna bebida ni ningún extra.'
export const HAD_FOOT = 'Los importes solo se ven en Caja.'
export const VOIDED = 'Anulado en Caja'
// «Copa · Comensal 3», «Botella 1 L · Toda la mesa» o solo «Toda la mesa» (la presentacion por defecto no se escribe).
export const hadDetail = (line: TableConsumption): string =>
  [line.presentation?.trim() ?? '', guestLabel(line.guestPosition)].filter(part => part.length > 0).join(' · ')

// ---- El resultado de la orden de la ronda, para Round.settle ----
// confirmed -> la ronda se vacia; rejected -> sigue entera, con el codigo y la linea; unconfirmed (o 401: la orden queda guardada,
// D6.3) -> lo enviado YA es la orden incierta, que se ve en su panel y se reintenta identica: la ronda se vacia para no mandarlo dos
// veces. Sin resultado (null) la orden no se envio... salvo que ESTA misma orden haya quedado guardada como la pendiente (un fallo
// despues de guardarla): entonces tambien es incierta.
export function settlementOf(outcome: Outcome | null, pending: { path: string; body: string } | null, sent: { path: string; body: string }): Settlement {
  if (outcome === null) return pending !== null && pending.path === sent.path && pending.body === sent.body ? { kind: 'unconfirmed' } : { kind: 'not_sent' }
  if (outcome.kind === 'confirmed') return { kind: 'confirmed' }
  if (outcome.kind === 'rejected') return { kind: 'rejected', code: outcome.code, line: outcome.line ?? null }
  return { kind: 'unconfirmed' }
}
