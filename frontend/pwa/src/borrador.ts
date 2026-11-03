import { KINDS, MAX_DRAFTS, SEVERITIES, type RestrictionDraft } from './abrir'
import { currentDatabase, run, STORES } from './db'
import { clockLabel, plural } from './labels'
import { MAX_LINES, MAX_QUANTITY, roundLineText, type RoundLine } from './round'

// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01; decision 8b del promotor, 02-11): la RONDA sin enviar y la MESA A MEDIO
// ABRIR (personas, menu y alergias escritas) se guardan en ESTE movil hasta enviarlas, abrir la mesa o descartarlas. Antes eran
// borradores solo en memoria (round.ts y abrir.ts, que siguen sin tocar nada que persista): el «Atrás» del sistema, una recarga o cerrar
// la aplicacion los perdian sin preguntar, y una mesa reabierta sin acordarse de la alergia salia sin ella.
//  - Viven en la base IndexedDB de Costina Sala (db.ts), en el almacen `pending`, con claves propias (`draft:round:<servicio>`,
//    `draft:open:<mesa>`): el hueco UNICO de la orden incierta (`uncertain`, pending.ts) no se toca, y la adopcion (adoption.ts) solo lee
//    ese hueco. Sin cambio de version de la base.
//  - NUNCA son una orden ni una cola: nada de aqui se envia solo. Al volver a esa mesa se PREGUNTA (RecoverSheet.vue) y solo con la
//    respuesta se recupera (round.restore, OpenTable) o se borra.
//  - Fallo cerrado al leer: un registro que no es exactamente un borrador (otra version, otra mesa, una cantidad fuera de rango, un tipo
//    desconocido) cuenta como ausente. Lo que se guarda son copias planas, nunca lo reactivo.
//  - Se borran al enviar la ronda (confirmada o sin confirmar: ya es la orden), al abrir la mesa (BoardView, con la orden confirmada o sin
//    confirmar), con «Salir sin enviar» o «Salir sin abrir», y al quedar la ronda o la hoja vacias.
export interface RoundDraft { kind: 'round'; serviceId: string; lines: RoundLine[]; savedAt: string }
export interface OpenDraft { kind: 'open'; tableId: string; pax: number | null; offerId: string; rows: RestrictionDraft[]; savedAt: string }
export interface OpenDraftInput { pax: number | null; offerId: string; rows: RestrictionDraft[] }

const roundKey = (serviceId: string) => `draft:round:${serviceId}`
const openKey = (tableId: string) => `draft:open:${tableId}`
const isText = (value: unknown, max: number): value is string => typeof value === 'string' && value.length <= max
const isId = (value: unknown): value is string => typeof value === 'string' && value.length > 0 && value.length <= 128
const isGuest = (value: unknown): value is number => Number.isInteger(value) && (value as number) >= 1
const PAX_MAX = 60   // el tope de guestChoices (abrir.ts): mas no es una mesa

export function parseRoundDraft(stored: unknown, serviceId: string): RoundDraft | null {
  if (stored === null || typeof stored !== 'object') return null
  const candidate = stored as Partial<RoundDraft>
  if (candidate.kind !== 'round' || candidate.serviceId !== serviceId || !Array.isArray(candidate.lines)) return null
  if (candidate.lines.length === 0 || candidate.lines.length > MAX_LINES) return null
  const lines: RoundLine[] = []
  for (const line of candidate.lines as Array<Partial<RoundLine> | null>) {
    if (line === null || typeof line !== 'object') return null
    if (!isId(line.productId) || !isId(line.presentationId) || !isText(line.name, 256) || !isText(line.presentation, 64)) return null
    if (!Number.isInteger(line.quantity) || (line.quantity as number) < 1 || (line.quantity as number) > MAX_QUANTITY) return null
    const guestPosition = line.guestPosition ?? null
    if (guestPosition !== null && !isGuest(guestPosition)) return null
    lines.push({ productId: line.productId, presentationId: line.presentationId, name: line.name, presentation: line.presentation, quantity: line.quantity as number, guestPosition })
  }
  return { kind: 'round', serviceId, lines, savedAt: typeof candidate.savedAt === 'string' ? candidate.savedAt : '' }
}

const KIND_CODES = KINDS.map(option => option.code as string), SEVERITY_CODES = SEVERITIES.map(option => option.code as string)
export function parseOpenDraft(stored: unknown, tableId: string): OpenDraft | null {
  if (stored === null || typeof stored !== 'object') return null
  const candidate = stored as Partial<OpenDraft>
  if (candidate.kind !== 'open' || candidate.tableId !== tableId || !Array.isArray(candidate.rows) || candidate.rows.length > MAX_DRAFTS) return null
  const pax = candidate.pax ?? null
  if (pax !== null && !(Number.isInteger(pax) && pax >= 1 && pax <= PAX_MAX)) return null
  const offerId = candidate.offerId ?? ''
  if (!isText(offerId, 128)) return null
  const rows: RestrictionDraft[] = []
  for (const row of candidate.rows as Array<Partial<RestrictionDraft> | null>) {
    if (row === null || typeof row !== 'object') return null
    const guest = row.guest ?? null
    if (guest !== null && guest !== 'all' && !isGuest(guest)) return null
    if (!KIND_CODES.includes(row.kind as string) || !SEVERITY_CODES.includes(row.severity as string) || !isText(row.substance, 200)) return null
    rows.push({ guest, kind: row.kind as RestrictionDraft['kind'], substance: row.substance, severity: row.severity as RestrictionDraft['severity'] })
  }
  if (pax === null && offerId === '' && rows.length === 0) return null   // nada a medio abrir
  return { kind: 'open', tableId, pax, offerId, rows, savedAt: typeof candidate.savedAt === 'string' ? candidate.savedAt : '' }
}

// ---- El almacen (copias planas; la promesa se resuelve con la transaccion completada, db.ts) ----
const plainLine = (line: RoundLine): RoundLine =>
  ({ productId: line.productId, presentationId: line.presentationId, name: line.name, presentation: line.presentation, quantity: line.quantity, guestPosition: line.guestPosition })
const plainRow = (row: RestrictionDraft): RestrictionDraft => ({ guest: row.guest, kind: row.kind, substance: row.substance, severity: row.severity })

export const loadRoundDraft = async (serviceId: string, database: string = currentDatabase()): Promise<RoundDraft | null> =>
  parseRoundDraft(await run<unknown>(STORES.pending, 'readonly', store => store.get(roundKey(serviceId)), database), serviceId)
export const saveRoundDraft = (serviceId: string, lines: readonly RoundLine[], now: Date = new Date(), database: string = currentDatabase()): Promise<void> =>
  run(STORES.pending, 'readwrite', store => store.put({ kind: 'round', serviceId, lines: lines.map(plainLine), savedAt: now.toISOString() } satisfies RoundDraft, roundKey(serviceId)), database)
    .then(() => undefined)
export const clearRoundDraft = (serviceId: string, database: string = currentDatabase()): Promise<void> =>
  run(STORES.pending, 'readwrite', store => store.delete(roundKey(serviceId)), database).then(() => undefined)

export const loadOpenDraft = async (tableId: string, database: string = currentDatabase()): Promise<OpenDraft | null> =>
  parseOpenDraft(await run<unknown>(STORES.pending, 'readonly', store => store.get(openKey(tableId)), database), tableId)
export const saveOpenDraft = (tableId: string, input: OpenDraftInput, now: Date = new Date(), database: string = currentDatabase()): Promise<void> =>
  run(STORES.pending, 'readwrite', store => store.put({ kind: 'open', tableId, pax: input.pax, offerId: input.offerId, rows: input.rows.map(plainRow), savedAt: now.toISOString() } satisfies OpenDraft,
    openKey(tableId)), database).then(() => undefined)
export const clearOpenDraft = (tableId: string, database: string = currentDatabase()): Promise<void> =>
  run(STORES.pending, 'readwrite', store => store.delete(openKey(tableId)), database).then(() => undefined)

// ---- Las palabras de la pregunta (RecoverSheet.vue): nunca se recupera sin que la persona lo diga ----
export const RECOVER_ROUND_TITLE = '¿Recuperas la ronda sin enviar?'
export const RECOVER_ROUND_YES = 'Recuperar la ronda'
export const RECOVER_ROUND_NO = 'Descartarla'
export const RECOVER_OPEN_TITLE = '¿Sigues abriendo esta mesa?'
export const RECOVER_OPEN_YES = 'Seguir abriendo'
export const RECOVER_OPEN_NO = 'Empezar de cero'
// «guardada en este móvil a las 21:14»; sin hora legible, solo «guardada en este móvil».
function savedText(savedAt: string, feminine: boolean): string {
  const time = Date.parse(savedAt)
  const word = feminine ? 'guardada' : 'guardados'
  return Number.isNaN(time) ? `${word} en este móvil` : `${word} en este móvil a las ${clockLabel(new Date(time).toISOString())}`
}
const units = (draft: RoundDraft) => draft.lines.reduce((total, line) => total + line.quantity, 0)
export function recoverRoundText(draft: RoundDraft): string {
  return `Tenías ${units(draft)} sin enviar en esta mesa, ${savedText(draft.savedAt, false)}. Nada de ello ha llegado al servidor.`
}
export const recoverRoundLines = (draft: RoundDraft): Array<{ text: string; alarming: boolean }> =>
  draft.lines.map(line => ({ text: `${line.quantity} × ${roundLineText(line)}`, alarming: false }))
export function recoverOpenText(draft: OpenDraft, offerName: string): string {
  const parts = [draft.pax !== null ? plural(draft.pax, 'persona', 'personas') : '', offerName,
    draft.rows.length > 0 ? plural(draft.rows.length, 'alergia o intolerancia', 'alergias o intolerancias') : ''].filter(part => part.length > 0)
  return `Tenías esta mesa a medio abrir, ${savedText(draft.savedAt, true)}${parts.length > 0 ? `: ${parts.join(' · ')}` : ''}. No se ha abierto: nada ha llegado al servidor.`
}
