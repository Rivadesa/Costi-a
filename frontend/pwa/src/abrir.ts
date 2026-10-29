import type { CommandRequest } from './ficha'
import { restrictionAlarming } from './floor'
import { guestLabel, plural, restrictionKindLabel, restrictionLabel, severityLabel } from './labels'
import { proposedOffer } from './offers'
import type { GuestRestriction } from './types'

// C3e (plan 2026-10-27, paso 2, «Abrir mesa»): la hoja de abrir de Costina Sala SIN DOM (vitest). Modelo: la pestana «0 · Abrir mesa»
// de la maqueta del 15-10 (maq15:789-795), con las personas de 1 a 8 y «Más…» (plan22:738). Tres toques: la mesa, las personas y
// «Abrir Mesa 5»; los de una alergia van aparte (anotar, quien y a que).
// Todo es presentacion y FORMATO: la hoja existe solo si el servidor anuncia open (D3.1), y el servidor decide la capacidad (422,
// DiningOperations.cs:39), la oferta vigente (offer_unavailable), el comensal (invalid_guest) y las duplicadas (duplicate_restriction).
// Aqui solo se adelantan esos rechazos, con los textos del PC. Sin dinero (ADR-007) y sin guardar nada: las filas de alergias son
// borradores EN MEMORIA, como la ronda de Bodega y como el PC (ServiceViewModel.cs:510).

// ---- ¿Cuantas personas? ----
// Botones de 1 a 8 sin pasar de la capacidad; «Más…» (el paso a paso y el numero) solo si la mesa admite mas de 8 o no se sabe su
// capacidad. El limite es la capacidad o, si no se sabe (0: la configuracion no tiene la mesa), el de hoy, 40 (OpenTable.vue:31,36).
// Empieza en 2, como hoy, salvo en una mesa de 1.
export const PAX_BUTTONS = 8
export const PAX_UNKNOWN_LIMIT = 40
const PAX_START = 2
export interface PaxChoices { counts: number[]; more: boolean }

const knownCapacity = (capacity: number | null | undefined): capacity is number => typeof capacity === 'number' && Number.isInteger(capacity) && capacity > 0
export const paxLimit = (capacity: number | null | undefined): number => knownCapacity(capacity) ? capacity : PAX_UNKNOWN_LIMIT
export const initialPax = (capacity: number | null | undefined): number => Math.min(PAX_START, paxLimit(capacity))

export function paxChoices(capacity: number | null | undefined): PaxChoices {
  const limit = paxLimit(capacity)
  return { counts: Array.from({ length: Math.min(PAX_BUTTONS, limit) }, (_, index) => index + 1), more: limit > PAX_BUTTONS }
}

// Fuera de limite, la capacidad escrita («Esta mesa admite hasta 12 personas.»); sin capacidad conocida, o con menos de 1, el texto de
// hoy con su limite. Devuelve '' si vale. El 422 del servidor sigue mandando: esto solo evita una orden que no puede salir.
export function paxProblem(pax: number, capacity: number | null | undefined): string {
  const limit = paxLimit(capacity)
  if (Number.isInteger(pax) && pax >= 1 && pax <= limit) return ''
  return knownCapacity(capacity) && Number.isInteger(pax) && pax > limit
    ? `Esta mesa admite hasta ${plural(limit, 'persona', 'personas')}.`
    : `Elige cuántas personas (1 a ${limit}).`
}

// ---- ¿Que van a comer? ----
// La propuesta es la primera oferta vigente (E4d, offers.ts: el «Menú del día»), ya elegida; las demas, detras de «Otro menú…»; los
// menus con eleccion (set-menu), solo en «Más opciones» de la hoja (plan15:671, maq15:792). Cada grupo, en el orden de /configuration.
// Si la primera es un menu con eleccion se propone igual, como el PC (ProposeDaily, ServiceViewModel.cs:641): el orden es el de Menus y
// cartas, y la propuesta no se repite en «Más opciones». En el laboratorio se propone LAB-TASTING («Menú de ensayo») y LAB-DAILY, que es
// con eleccion, va en «Más opciones» (e2e/commands.spec.ts:24).
export interface OfferGroups<T> { proposed: T | null; others: T[]; withChoice: T[] }

// Como labels.ts: ni mayusculas ni guiones importan («set-menu» de /configuration o «SetMenu»).
const withChoice = (kind: string) => kind.replace(/[^\p{L}\p{Nd}]/gu, '').toLowerCase() === 'setmenu'

export function offerGroups<T extends { id: string; kind: string }>(offers: readonly T[]): OfferGroups<T> {
  const proposed = proposedOffer(offers)
  const rest = offers.filter(offer => offer !== proposed)
  return { proposed, others: rest.filter(offer => !withChoice(offer.kind)), withChoice: rest.filter(offer => withChoice(offer.kind)) }
}

// ---- ¿Alergias o intolerancias? ----
// KINDS y SEVERITIES salen de TableCard.vue:131-132 a este modulo (C3e): los usan la hoja de abrir y la de alergias. Los codigos son
// los nombres de los enum del servidor (RestrictionKind y RestrictionSeverity, Restrictions.cs:8-9), que los lee sin mirar mayusculas.
export const KINDS = [{ code: 'Allergy', label: 'Alergia' }, { code: 'Intolerance', label: 'Intolerancia' }, { code: 'Preference', label: 'Preferencia' }] as const satisfies
  ReadonlyArray<{ code: GuestRestriction['kind']; label: string }>
export const SEVERITIES = [{ code: 'Severe', label: 'Grave' }, { code: 'Moderate', label: 'Moderada' }, { code: 'Mild', label: 'Leve' }] as const satisfies
  ReadonlyArray<{ code: GuestRestriction['severity']; label: string }>

// C3e.1 (usabilidad 28-10, H03): como se nombra una restriccion al decir que NO se ha anotado o retirado: «La alergia», «La
// intolerancia», «La preferencia» (las tres en femenino, como los motivos de commands.ts, notApplied). Un tipo que no se conoce no se
// rebaja a una palabra mas suave: «La restricción».
export function restrictionSubject(kind: string): string {
  const label = KINDS.find(option => option.code.toLowerCase() === kind.toLowerCase())?.label
  return label ? `La ${label.toLowerCase()}` : 'La restricción'
}

// Una fila de la hoja, como AllergyDraft del PC (ServiceViewModel.cs:94-100): quien (null = sin elegir; «Toda la mesa» es una opcion
// mas, 'all', nunca la de por defecto), tipo (Alergia por defecto), a que y gravedad (Grave por defecto). Como mucho 30 filas, como
// CanAddAllergy del PC.
export type DraftGuest = number | 'all' | null
export interface RestrictionDraft { guest: DraftGuest; kind: GuestRestriction['kind']; substance: string; severity: GuestRestriction['severity'] }
export const newDraft = (): RestrictionDraft => ({ guest: null, kind: 'Allergy', substance: '', severity: 'Severe' })
export const MAX_DRAFTS = 30

// Los comensales que se pueden elegir, como GuestsFor del PC: 1..personas y «Toda la mesa» al final. Hasta que las personas son un
// numero valido (1 a 60, el mismo tope), solo «Toda la mesa».
export interface GuestChoice { guest: number | 'all'; label: string }
export function guestChoices(pax: number): GuestChoice[] {
  const people = Number.isInteger(pax) && pax >= 1 && pax <= 60 ? pax : 0
  const whole: GuestChoice = { guest: 'all', label: guestLabel(null) }
  return [...Array.from({ length: people }, (_, index): GuestChoice => ({ guest: index + 1, label: guestLabel(index + 1) })), whole]
}

// Una fila vacia del todo (sin quien y sin a que) no se mira, como en el PC (ServiceViewModel.cs:499): el tipo o la gravedad solos no
// la llenan. Las demas viajan en la orden.
const filled = (draft: RestrictionDraft) => draft.guest !== null || draft.substance.trim().length > 0
// Igual que OrdinalIgnoreCase del servidor (Restrictions.cs:44): mayuscula letra a letra, sin las que crecen («ß» no pasa a «SS»).
const fold = (text: string) => Array.from(text.trim(), char => { const upper = char.toUpperCase(); return upper.length === 1 ? upper : char }).join('')

export const GUEST_PROBLEM = 'Elige para quién es cada alergia o intolerancia: un comensal o toda la mesa.'
export const SUBSTANCE_PROBLEM = 'Escribe a qué es cada alergia o intolerancia (2 caracteres o más).'
export const DUPLICATE_PROBLEM = 'Esa alergia o intolerancia ya está anotada para ese comensal.'
export const guestRangeProblem = (pax: number) => `El comensal de una alergia tiene que estar entre 1 y ${pax}.`

// Solo el FORMATO, con los textos del PC (ServiceViewModel.cs:502-505) y en su orden, fila a fila; despues, la duplicada que el
// servidor rechazaria (duplicate_restriction: mismo comensal, mismo tipo y la misma sustancia sin mirar mayusculas ni los espacios de
// los lados). El rango del comensal, solo con personas validas: si no, habla antes paxProblem. Devuelve el primer problema, o ''.
export function draftProblem(drafts: readonly RestrictionDraft[], pax: number): string {
  const rows = drafts.filter(filled)
  const people = Number.isInteger(pax) && pax >= 1 ? pax : null
  for (const row of rows) {
    if (row.guest === null) return GUEST_PROBLEM
    if (row.substance.trim().length < 2) return SUBSTANCE_PROBLEM
    if (row.guest !== 'all' && people !== null && (!Number.isInteger(row.guest) || row.guest < 1 || row.guest > people)) return guestRangeProblem(people)
  }
  const seen = new Set<string>()
  for (const row of rows) {
    const key = `${row.guest}|${row.kind.toLowerCase()}|${fold(row.substance)}`
    if (seen.has(key)) return DUPLICATE_PROBLEM
    seen.add(key)
  }
  return ''
}

// ---- La orden ----
const restrictionsText = (count: number) => plural(count, 'alergia o intolerancia', 'alergias o intolerancias')

// La descripcion de la orden pendiente: «Abrir Mesa 5 para 3 personas con 1 alergia o intolerancia» (sin filas, la de hoy).
export function openDescription(tableName: string, pax: number, drafts: readonly RestrictionDraft[]): string {
  const count = drafts.filter(filled).length
  return `Abrir ${tableName} para ${plural(pax, 'persona', 'personas')}${count > 0 ? ` con ${restrictionsText(count)}` : ''}`
}

// La linea encima de «Abrir»: «3 personas · Menú de ensayo», solo con lo que hay. C3e.1 (usabilidad 28-10, H06): sin «1 alergia o
// intolerancia» al final; debajo va cada alergia escrita entera (openLines).
export function openSummary(pax: number, offer: { name: string } | null): string {
  return [Number.isInteger(pax) && pax >= 1 ? plural(pax, 'persona', 'personas') : '', offer?.name ?? ''].filter(part => part.length > 0).join(' · ')
}

// ---- C3e.1 (usabilidad 28-10, H06): lo escrito, entero, encima de «Abrir» y antes de salir ----
// Antes, encima de «Abrir» solo ponia «1 alergia o intolerancia», sin quien ni que, y salir de la hoja («Mesas», o «Ver mesa» en la
// banda) la cerraba sin aviso: habia que reescribirlas, y al reescribir es facil olvidar una. La mesa no se abria, asi que nada salia sin
// su alergia; lo que se perdia era lo escrito.
// Cada fila escrita (con quien o con a que; una vacia del todo no se mira) va ENTERA, con el formato de la ficha (labels.ts,
// restrictionLabel): «Comensal 2 · ALERGIA frutos secos (grave)», «Toda la mesa · Intolerancia lactosa (leve)». Lo que aun falta se
// escribe como su pregunta («¿Quién?», «¿A qué?»), nunca se rellena: una fila sin quien NO es «Toda la mesa». Rojo si alarma con la regla
// de la ficha (floor.ts, restrictionAlarming), ambar si no, como su fila en la hoja.
export interface OpenLine { text: string; alarming: boolean }
export const WHO_MISSING = '¿Quién?'
export const WHAT_MISSING = '¿A qué?'
const asRestriction = (draft: RestrictionDraft): GuestRestriction =>
  ({ id: '', guestPosition: draft.guest === 'all' ? null : draft.guest, kind: draft.kind, substance: draft.substance.trim(), severity: draft.severity })
export function draftLabel(draft: RestrictionDraft): string {
  if (draft.guest !== null && draft.substance.trim().length > 0) return restrictionLabel(asRestriction(draft))
  const who = draft.guest === null ? WHO_MISSING : guestLabel(draft.guest === 'all' ? null : draft.guest)
  return `${who} · ${restrictionKindLabel(draft.kind)} ${draft.substance.trim() || WHAT_MISSING} (${severityLabel(draft.severity)})`
}
export const draftLines = (drafts: readonly RestrictionDraft[]): OpenLine[] =>
  drafts.filter(filled).map(draft => ({ text: draftLabel(draft), alarming: restrictionAlarming(asRestriction(draft)) }))

// Encima de «Abrir», hasta OPEN_LINES filas enteras. Con mas, UNA linea que no esconde que las hay, roja si alguna alarma: las filas
// siguen escritas arriba, en la hoja, y la barra de abajo es fija (con 30 lineas taparia la pantalla).
export const OPEN_LINES = 3
export function openLines(drafts: readonly RestrictionDraft[]): OpenLine[] {
  const lines = draftLines(drafts)
  if (lines.length <= OPEN_LINES) return lines
  return [{ text: `${restrictionsText(lines.length)} anotadas: revísalas arriba antes de abrir.`, alarming: lines.some(line => line.alarming) }]
}

// Salir con alguna fila escrita pregunta antes, como la ronda (TableCard.vue, «¿Salir sin enviar la ronda?»), con las filas enteras
// debajo. «alergia» si todas lo son; si no, «alergia o intolerancia», como la hoja. '' sin nada escrito: entonces no se pregunta.
export const LEAVE_TITLE = '¿Salir sin abrir la mesa?'
export const LEAVE_STAY = 'Seguir abriendo'
export const LEAVE_DISCARD = 'Salir sin abrir'
export function lostText(drafts: readonly RestrictionDraft[]): string {
  const rows = drafts.filter(filled)
  if (rows.length === 0) return ''
  const noun = rows.every(row => row.kind === 'Allergy') ? plural(rows.length, 'alergia', 'alergias') : restrictionsText(rows.length)
  return rows.length === 1 ? `Se perderá ${noun}: habrá que escribirla otra vez.` : `Se perderán ${noun}: habrá que escribirlas otra vez.`
}

// UNA orden POST /dining/services con todo (E4e, ADR-016; D6.3: una sola orden incierta por gesto): { tableId, pax, offerId, menuId,
// restrictions }, los campos de OpenRequest y RestrictionRequest (DiningOperations.cs:10,12; el servidor rechaza un campo desconocido).
// Sin filas, el cuerpo de hoy (BoardView.vue:114), que tambien entiende un servidor anterior a E4e; menuId, alias para uno anterior a
// E4a. «Toda la mesa» viaja como guestPosition null y la sustancia sin los espacios de los lados. Con un problema de formato no hay
// orden: una fila sin quien NUNCA sale como «Toda la mesa» (la vista apaga «Abrir» antes; esto es la defensa).
export function openRequest(table: { id: string; name: string }, pax: number, offer: { id: string }, drafts: readonly RestrictionDraft[]): CommandRequest {
  const problem = draftProblem(drafts, pax)
  if (problem) throw new Error(problem)
  const body: Record<string, unknown> = { tableId: table.id, pax, offerId: offer.id, menuId: offer.id }
  const restrictions = drafts.filter(filled).map(row => ({
    guestPosition: row.guest === 'all' ? null : row.guest, kind: row.kind, substance: row.substance.trim(), severity: row.severity,
  }))
  if (restrictions.length > 0) body.restrictions = restrictions
  return { path: '/dining/services', body, description: openDescription(table.name, pax, drafts) }
}
