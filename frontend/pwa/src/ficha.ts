import { EATING, KITCHEN, READY, tileSince, tileState } from './floor'
import { actionLabel, clockLabel, minutesLabel, plural } from './labels'
import type { BoardEntry, Course, Dining } from './types'

// C3a: ficha de una mesa en el comandero, sin DOM. Que botones salen, cual va en grande y con que texto, y que orden se envia. NINGUNA
// regla de negocio: una accion existe si y solo si el servidor la anuncia (affordances del servicio, de cada pase y de la ocupacion,
// ya filtradas por rol y estacion). Lo unico que se decide aqui es PRESENTACION, con el mismo orden que el Comedor del puesto
// (ServiceViewModel.PrimaryOrder/SecondaryOrder): el boton grande es la primera anunciada de servir, validar, enviar el siguiente pase,
// empezar, reanudar, terminar o liberar; el resto de las anunciadas va en «Más opciones».
export const PRIMARY_ORDER: readonly string[] = ['serve', 'ready', 'fire-next', 'start', 'resume', 'complete', 'release']
export const SECONDARY_ORDER: readonly string[] = ['serve', 'ready', 'fire-next', 'start', 'resume', 'pause', 'skip', 'complete', 'cancel-unstarted', 'release']
const COURSE_ACTIONS = new Set(['serve', 'ready', 'skip'])
// Las que el servidor solo acepta con motivo, y las que no tienen vuelta atras (van con borde de aviso, nunca en rojo: el rojo es de las alergias).
export const REASON_ACTIONS = new Set(['pause', 'skip', 'cancel-unstarted', 'release'])
const CAUTION_ACTIONS = new Set(['release', 'cancel-unstarted'])
// Plan, seccion 3: el boton que manda un pase dice «Enviar siguiente pase» (maqueta: «Enviar siguiente pase · Postre»). La orden
// pendiente y los avisos usan el texto completo del vocabulario («Enviar siguiente pase a cocina»), como el puesto.
const BUTTON: Readonly<Record<string, string>> = { 'fire-next': 'Enviar siguiente pase' }

export interface TableAction { action: string; course: Course | null; label: string; testId: string; needsReason: boolean; caution: boolean }

function item(action: string, course: Course | null, service: Dining): TableAction {
  const base = BUTTON[action] ?? actionLabel(action)
  const next = action === 'fire-next' ? service.courses.find(candidate => candidate.state === 'Pending') : undefined
  const label = course ? `${base} · ${course.name}` : next ? `${base} · ${next.name}` : base
  return { action, course, label, testId: course ? `do-${action}-${course.id}` : `do-${action}`, needsReason: REASON_ACTIONS.has(action), caution: CAUTION_ACTIONS.has(action) }
}

// Todas las acciones operativas ANUNCIADAS de una mesa, en el orden del Comedor. Servir, validar y omitir son de cada pase; liberar,
// de la ocupacion; las demas, del servicio. Lo que no esta en la lista (elegir, pedir, restricciones, consumos) tiene su sitio propio.
export function tableActions(entry: BoardEntry): TableAction[] {
  const service = entry.service
  const list: TableAction[] = []
  for (const action of SECONDARY_ORDER) {
    if (COURSE_ACTIONS.has(action)) { for (const course of service.courses) if (course.actions?.includes(action)) list.push(item(action, course, service)) }
    else if (action === 'release') { if (entry.occupancy.actions?.includes(action)) list.push(item(action, null, service)) }
    else if (service.actions?.includes(action)) list.push(item(action, null, service))
  }
  return list
}

export function primaryAction(actions: readonly TableAction[]): TableAction | null {
  for (const action of PRIMARY_ORDER) {
    const found = actions.find(candidate => candidate.action === action)
    if (found) return found
  }
  return null
}

// Sin accion principal anunciada, el boton grande queda apagado y dice que se espera (ServiceViewModel.WaitingText).
export function waitingText(service: Dining): string {
  if (service.restrictionsPendingAck === true) return 'Esperando la revisión de cocina'
  return tileState(service) === KITCHEN ? 'Esperando a cocina' : 'Sin acciones para esta mesa ahora'
}

// El rotulo del campo de motivo nombra SOLO lo anunciado que lo pide (no se ofrece con palabras lo que el servidor no permite).
const REASON_VERBS: Readonly<Record<string, string>> = { pause: 'pausar', skip: 'omitir', 'cancel-unstarted': 'cancelar', release: 'liberar', 'remove-restriction': 'retirar' }
export function reasonLabel(actions: readonly string[]): string {
  const verbs = [...new Set(actions.map(action => REASON_VERBS[action]).filter((verb): verb is string => verb !== undefined))]
  if (verbs.length === 0) return 'Motivo'
  const last = verbs.at(-1)!
  const list = verbs.length === 1 ? last : `${verbs.slice(0, -1).join(', ')} ${/^[oh]?o/i.test(last) ? 'u' : 'o'} ${last}`
  return `Motivo (para ${list})`
}

// Lo unico que se comprueba aqui es el FORMATO de lo tecleado: un motivo de al menos 3 caracteres.
export const reasonOk = (reason: string) => reason.trim().length >= 3
export const REASON_PROBLEM = 'Escribe el motivo (3 caracteres o más).'

export interface CommandRequest { path: string; body: Record<string, unknown>; description: string }

// La orden de una accion, con la version LEIDA de esa misma mesa (F01). La descripcion de la orden pendiente nombra la mesa por su
// nombre, como el puesto («Pausar servicio · Mesa 7»), nunca por su codigo. Liberar va a la ocupacion con SU version.
export function commandFor(entry: BoardEntry, action: TableAction, tableName: string, reason = ''): CommandRequest {
  const service = entry.service
  const description = `${actionLabel(action.action)}${action.course ? ` · ${action.course.name}` : ''} · ${tableName}`
  if (action.action === 'release')
    return { path: `/dining/occupancy/${encodeURIComponent(service.id)}/release`, body: { expectedVersion: entry.occupancyVersion, reason: reason.trim() }, description }
  const body: Record<string, unknown> = { expectedVersion: entry.version }
  if (action.course) body.courseId = action.course.id
  if (action.needsReason) body.reason = reason.trim()
  return { path: serviceCommand(service, action.action), body, description }
}
export const serviceCommand = (service: Dining, action: string) => `/dining/services/${encodeURIComponent(service.id)}/commands/${action}`

// Cabecera de la ficha (ServiceViewModel.DetailInfo): las personas y, si la lectura lo dice, desde cuando dura lo que se ve.
export function tableInfo(service: Dining, now: number): string {
  const people = plural(service.pax, 'persona', 'personas')
  const since = tileSince(service)
  if (since === null) return people
  const at = clockLabel(new Date(since).toISOString()), elapsed = minutesLabel(now - since)
  const state = tileState(service)
  return state === KITCHEN ? `${people} · en cocina desde las ${at} (${elapsed})`
    : state === READY ? `${people} · listo para servir desde las ${at} (${elapsed})`
    : state === EATING ? `${people} · último pase servido a las ${at} (${elapsed})`
    : people
}

// Entrada «Carta y menús»: lo que hay que hacer en ella si el servidor lo anuncia (elegir, pedir) o, si no, como va la mesa.
export function dishesHint(service: Dining, offer: string): string {
  const choose = service.courses.filter(course => course.actions?.includes('choose'))
  const order = service.courses.filter(course => course.actions?.includes('add-dish'))
  const done = service.courses.filter(course => course.state === 'Served' || course.state === 'Skipped').length
  const parts = [offer]
  if (choose.length > 0) parts.push(`elegir plato en ${choose.map(course => course.name).join(', ')}`)
  else if (order.length > 0) parts.push(`pedir en ${order.map(course => course.name).join(', ')}`)
  else if (service.courses.length > 0) parts.push(`${done} de ${plural(service.courses.length, 'pase hecho', 'pases hechos')}`)
  return parts.filter(part => part.length > 0).join(' · ')
}

export const coursesTitle = (service: Dining): string => service.courses.length === 0 ? 'Pases'
  : `Pases · ${service.courses.filter(course => course.state === 'Served' || course.state === 'Skipped').length} de ${service.courses.length} hechos`

export const PENDING_REVIEW_TEXT = 'SIN REVISAR POR COCINA: cada elaboración «Pendiente de revisión» necesita su decisión (no afecta, adaptar o rehacer) antes de validar, servir o enviar otro pase.'
