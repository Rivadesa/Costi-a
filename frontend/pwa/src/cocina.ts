import type { Outcome } from './commands'
import { dishGroups, restrictionAlarming } from './floor'
import { guestLabel, minutesLabel, plural, preparationLabel, restrictionKindLabel, reviewOutcome, severityLabel } from './labels'
import type { BoardEntry, Course, GuestRestriction, Preparation } from './types'

// C3d (plan 2026-10-27; plan 2026-10-22, seccion 4.3): Costina Cocina, comandas por mesa. Logica SIN DOM (vitest) que sustituye a
// kitchen.ts. Todo es PRESENTACION de la lectura autoritativa del tablero:
//  - una ESTACION ve solo su trabajo (cambia la regla de D6.4 «una estacion ve el pase entero»): una comanda por mesa y pase, con sus
//    platos agrupados, «Tambien en: <las demas estaciones>», un «Listo» y la franja «Hecho · esperando al pase»;
//  - el PASE (y una tablet de cocina sin estacion, que ve lo que el servidor le anuncie, como hasta ahora) ve la mesa entera: el progreso
//    de cada estacion, que falta, «Siguiente: … (sin enviar)», marcar por una estacion, «Esperando al camarero» y «Por revisar antes de
//    enviar» (E4e);
//  - los platos se agrupan con la regla COMUN con el PC (Costina.Client/DishGroups.cs; tabla dotnet/tests/fixtures/kitchen-cases.json);
//  - el tiempo, siempre escrito, en ambar desde 15 minutos y en ambar oscuro desde 25: nunca en rojo, que es de las alergias;
//  - las claves nuevas entre dos lecturas, para el sonido (que pone turno.ts tras «Empezar turno»);
//  - los atajos que rellenan la nota de una revision del pase, y el aviso de un version_conflict en la propia comanda.
// Nada habilita nada: un boton existe solo si el servidor anuncia su accion en ESA elaboracion o en ESE pase (ya filtrada por rol y
// estacion, D4.3a y E4e). Aqui solo se elige que elaboraciones lleva cada orden de grupo: las que anuncian la accion individual.

export const PASS_STATION = 'pase'
// Tope de preparations-ready (DiningOperations.MaxGroupPreparations): un «Listo» manda como mucho estas; si quedaran mas, el boton sigue.
export const MAX_GROUP_PREPARATIONS = 200

// El pase, y una tablet de cocina sin estacion (decision 4A de D6.4: espejo de lo que anuncia el servidor), ven la mesa entera.
export const seesWholeTable = (station: string | null | undefined): boolean => station === null || station === undefined || station === PASS_STATION

// Los estados del servidor se comparan sin distinguir mayusculas (como floor.ts y Same en Costina.Client).
const same = (value: string | null | undefined, code: string) => (value ?? '').toLowerCase() === code.toLowerCase()
const announces = (actions: readonly string[] | undefined, action: string) => actions?.includes(action) === true
const isReady = (preparation: Preparation) => same(preparation.state, 'Ready')
const inKitchen = (course: Course) => same(course.state, 'Fired') || same(course.state, 'Preparing')
const finished = (entry: BoardEntry) => same(entry.service.state, 'Completed') || same(entry.service.state, 'Cancelled')
const pendingReview = (preparation: Preparation) => preparation.reviewPending === true
const unique = (values: readonly string[]) => [...new Set(values)]

// «A», «A y B», «A, B y C».
function joined(parts: readonly string[]): string {
  return parts.length < 2 ? parts.join('') : `${parts.slice(0, -1).join(', ')} y ${parts.at(-1)}`
}

// ---- Platos agrupados (tabla comun con el PC: kitchen-cases.json) ----
export interface Dish {
  key: string                    // [nombre, estacion]: unico dentro de una comanda
  name: string
  stationId: string
  quantity: number               // suma de las cantidades de TODAS sus elaboraciones, excepciones incluidas
  preparations: Preparation[]
  exceptions: Preparation[]      // aparte, bajo el plato, nunca atenuadas
  ready: boolean                 // todas sus elaboraciones listas
}

// Las restricciones que se escriben en la linea del comensal: las de un comensal. Las de TODA LA MESA salen una sola vez arriba.
export const lineRestrictions = (preparation: Preparation): GuestRestriction[] =>
  (preparation.restrictions ?? []).filter(restriction => restriction.guestPosition !== null && restriction.guestPosition !== undefined)

// Va aparte: lo que esta por revisar, una decision que cambia el plato (todo lo que no es «No le afecta», tambien una decision que no se
// conoce) o una restriccion de un comensal concreto. Un «No le afecta» bajo una restriccion de toda la mesa no cambia el plato.
export const isException = (preparation: Preparation): boolean =>
  pendingReview(preparation) || (preparation.review != null && !same(preparation.review.decision, 'Unaffected')) || lineRestrictions(preparation).length > 0

// Mismo nombre en la misma estacion (comparacion exacta), en el orden de su primera aparicion (floor.ts, espejo de DishGroups.Of).
export function dishes(preparations: readonly Preparation[]): Dish[] {
  return dishGroups({ preparations: [...preparations] }).map(group => ({
    key: JSON.stringify([group[0].name, group[0].stationId]),
    name: group[0].name,
    stationId: group[0].stationId,
    quantity: group.reduce((total, preparation) => total + preparation.quantity, 0),
    preparations: group,
    exceptions: group.filter(isException),
    ready: group.every(isReady),
  }))
}

// Estado del plato con las palabras de siempre (espejo de DishGroups.State): el de todas si coinciden; si no, cuantas estan listas.
export function dishState(dish: Dish): string {
  const states = new Set(dish.preparations.map(preparation => preparation.state.toLowerCase()))
  return states.size === 1 ? preparationLabel(dish.preparations[0].state)
    : `${dish.preparations.filter(isReady).length} de ${plural(dish.preparations.length, 'lista', 'listas')}`
}

// «Merluza á galega × 4»: en cocina la cantidad siempre se escribe, tambien «× 1».
export const dishText = (dish: Dish): string => `${dish.name} × ${dish.quantity}`

// «(1 adaptada)», «(2 adaptadas · 1 por revisar)»: lo que cambia en el plato, contado por comensal. Nada si no cambia nada.
export function dishNote(dish: Dish): string {
  const count = (test: (preparation: Preparation) => boolean) => dish.preparations.filter(test).length
  const decided = (decision: string) => (preparation: Preparation) => !pendingReview(preparation) && same(preparation.review?.decision, decision)
  const adapted = count(decided('Adapt')), remade = count(decided('Remake')), pending = count(pendingReview)
  const parts = [
    ...(adapted > 0 ? [`${adapted} ${adapted === 1 ? 'adaptada' : 'adaptadas'}`] : []),
    ...(remade > 0 ? [`${remade} ${remade === 1 ? 'rehecha' : 'rehechas'}`] : []),
    ...(pending > 0 ? [`${pending} por revisar`] : []),
  ]
  return parts.length === 0 ? '' : `(${parts.join(' · ')})`
}

// ---- La linea de una excepcion: comensal, tipo, sustancia y gravedad, y la decision del pase ----
export const REVIEW_PENDING_AT_PASS = 'Por revisar en el pase'
export interface ExceptionLine {
  preparation: Preparation
  text: string        // «Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra»
  alarming: boolean   // en rojo: alguna restriccion que alarma (tambien la de toda la mesa que motivo la adaptacion)
  pending: boolean    // «Por revisar en el pase», en ambar
}
// La restriccion de OTRO comensal (un plato para compartir recibe la de cualquiera) dice de quien es.
function restrictionText(restriction: GuestRestriction, guest: number | null | undefined): string {
  const text = `${restrictionKindLabel(restriction.kind)} ${restriction.substance} (${severityLabel(restriction.severity)})`
  return (restriction.guestPosition ?? null) === (guest ?? null) ? text : `${text} del ${guestLabel(restriction.guestPosition)}`
}
export function exceptionLine(preparation: Preparation): ExceptionLine {
  const outcome = pendingReview(preparation) ? REVIEW_PENDING_AT_PASS : reviewOutcome(preparation)
  const parts = [guestLabel(preparation.guestPosition), ...lineRestrictions(preparation).map(restriction => restrictionText(restriction, preparation.guestPosition)), outcome]
  return {
    preparation,
    text: parts.filter(part => part.length > 0).join(' · '),
    alarming: (preparation.restrictions ?? []).some(restrictionAlarming),
    pending: pendingReview(preparation),
  }
}

// Las restricciones de toda la mesa, una sola vez (cabecera de la comanda), sin repetir y en el orden de su primera aparicion.
export function wholeTable(preparations: readonly Preparation[]): GuestRestriction[] {
  const found = new Map<string, GuestRestriction>()
  for (const preparation of preparations)
    for (const restriction of preparation.restrictions ?? [])
      if ((restriction.guestPosition === null || restriction.guestPosition === undefined) && !found.has(restriction.id)) found.set(restriction.id, restriction)
  return [...found.values()]
}

// ---- Ordenes de grupo: solo lo que el servidor anuncia ----
// Las elaboraciones que lleva un «Listo»: las que anuncian preparation-ready, y solo si el pase anuncia la orden de grupo.
function readyItems(course: Course, preparations: readonly Preparation[]): string[] {
  if (!announces(course.actions, 'preparations-ready')) return []
  return preparations.filter(preparation => announces(preparation.actions, 'preparation-ready')).map(preparation => preparation.id).slice(0, MAX_GROUP_PREPARATIONS)
}
export const dishReadyIds = (course: Course, dish: Dish): string[] => readyItems(course, dish.preparations)
// El cuerpo de preparations-ready, con la version LEIDA de esa mesa (F01).
export function readyBody(entry: BoardEntry, course: Course, itemIds: readonly string[]): { expectedVersion: number; courseId: string; itemIds: string[] } {
  return { expectedVersion: entry.version, courseId: course.id, itemIds: [...itemIds] }
}

// Lo mas antiguo primero (firedAt); sin hora de envio, al final; a igual hora, por mesa y pase. La clave y el orden son los de kitchen.ts.
const firedTime = (course: Course) => { const at = course.firedAt ? Date.parse(course.firedAt) : Number.NaN; return Number.isNaN(at) ? Number.POSITIVE_INFINITY : at }
function oldestFirst(a: { entry: BoardEntry; course: Course }, b: { entry: BoardEntry; course: Course }): number {
  return (firedTime(a.course) - firedTime(b.course)) || a.entry.service.tableId.localeCompare(b.entry.service.tableId) || a.course.id.localeCompare(b.course.id)
}
const ticketKey = (entry: BoardEntry, course: Course) => `${entry.service.id}:${course.id}`

// ---- Estacion: solo lo suyo ----
export interface StationTicket {
  key: string
  entry: BoardEntry
  course: Course
  paused: boolean
  dishes: Dish[]                    // solo su trabajo
  wholeTable: GuestRestriction[]    // una sola vez, arriba
  alsoIn: string[]                  // las demas estaciones del pase (ids; los nombra quien pinta), en el orden de aparicion
  readyIds: string[]                // el «Listo» grande: lo suyo que el servidor le deja marcar, en UNA orden
  done: boolean                     // todo lo suyo listo: «Hecho · esperando al pase»
}

export function stationTickets(entries: readonly BoardEntry[], station: string): StationTicket[] {
  const tickets: StationTicket[] = []
  for (const entry of entries) {
    if (finished(entry)) continue
    for (const course of entry.service.courses) {
      // Enviado y sin validar. Lo validado ya no es trabajo de la estacion: sale tambien de la franja «Hecho».
      if (!inKitchen(course)) continue
      const own = course.preparations.filter(preparation => preparation.stationId === station)
      if (own.length === 0) continue
      tickets.push({
        key: ticketKey(entry, course), entry, course, paused: same(entry.service.state, 'Paused'),
        dishes: dishes(own), wholeTable: wholeTable(own),
        alsoIn: unique(course.preparations.filter(preparation => preparation.stationId !== station).map(preparation => preparation.stationId)),
        readyIds: readyItems(course, own),
        done: own.every(isReady),
      })
    }
  }
  return tickets.sort(oldestFirst)
}

// Con dos platos propios o mas, cada plato lleva tambien su «Listo» (solo los suyos); con uno, basta el grande.
export const listoPerDish = (ticket: StationTicket): boolean => ticket.dishes.length > 1
// «También en: Cocina fría y Pase», con el nombre configurado de cada estacion.
export const alsoText = (stations: readonly string[], name: (stationId: string) => string): string =>
  stations.length === 0 ? '' : `También en: ${joined(stations.map(name))}`
// La franja «Hecho · esperando al pase»: lo que la estacion ya termino de esa comanda. Sin hora: el motor no la guarda.
export const DONE_BAND = 'Hecho · esperando al pase'
export const doneText = (ticket: StationTicket): string => ticket.dishes.map(dishText).join(', ')

// ---- Pase: la mesa entera ----
export interface StationProgress { stationId: string; ready: number; total: number; done: boolean }
export interface StationMark { stationId: string; itemIds: string[] }
export interface PassTicket {
  key: string
  entry: BoardEntry
  course: Course
  paused: boolean
  dishes: Dish[]                    // todas las estaciones
  wholeTable: GuestRestriction[]
  progress: StationProgress[]       // platos listos de cada estacion, en el orden de aparicion
  missingStations: string[]         // estaciones con algo obligatorio sin terminar
  pendingReviews: number            // elaboraciones de este pase por revisar
  reviews: Preparation[]            // lo enviado por revisar que el servidor deja revisar aqui
  canValidate: boolean              // el servidor anuncia ready: «Validar pase: listo para servir»
  marks: StationMark[]              // «Más opciones»: marcar por una estacion (no el pase), en UNA orden cada una
  next: Course | null               // el primer pase sin enviar de la mesa: «Siguiente: … (sin enviar)»
}

function progressOf(list: readonly Dish[]): StationProgress[] {
  const rows = new Map<string, StationProgress>()
  for (const dish of list) {
    const row = rows.get(dish.stationId) ?? { stationId: dish.stationId, ready: 0, total: 0, done: false }
    row.total++
    if (dish.ready) row.ready++
    rows.set(dish.stationId, row)
  }
  return [...rows.values()].map(row => ({ ...row, done: row.ready === row.total }))
}

export function passTickets(entries: readonly BoardEntry[]): PassTicket[] {
  const tickets: PassTicket[] = []
  for (const entry of entries) {
    if (finished(entry)) continue
    for (const course of entry.service.courses) {
      // En cocina, y lo validado que quedo por revisar (una alergia anotada despues de validar): no se puede servir hasta decidirlo.
      const reviewing = same(course.state, 'Ready') && course.preparations.some(pendingReview)
      if (!inKitchen(course) && !reviewing) continue
      if (course.preparations.length === 0) continue
      const list = dishes(course.preparations)
      const stations = unique(course.preparations.map(preparation => preparation.stationId))
      tickets.push({
        key: ticketKey(entry, course), entry, course, paused: same(entry.service.state, 'Paused'),
        dishes: list, wholeTable: wholeTable(course.preparations), progress: progressOf(list),
        missingStations: unique(course.preparations.filter(preparation => preparation.mandatory && !isReady(preparation)).map(preparation => preparation.stationId)),
        pendingReviews: course.preparations.filter(pendingReview).length,
        reviews: course.preparations.filter(preparation => pendingReview(preparation) && announces(preparation.actions, 'review-preparation')),
        canValidate: announces(course.actions, 'ready'),
        marks: stations.filter(station => station !== PASS_STATION)
          .map(station => ({ stationId: station, itemIds: readyItems(course, course.preparations.filter(preparation => preparation.stationId === station)) }))
          .filter(mark => mark.itemIds.length > 0),
        next: entry.service.courses.find(candidate => same(candidate.state, 'Pending')) ?? null,
      })
    }
  }
  return tickets.sort(oldestFirst)
}

// «Cocina caliente: lista», «Cocina fría: 1 de 2 platos». Sin hora por estacion: el motor no la guarda (diferido).
export const progressText = (row: StationProgress, name: (stationId: string) => string): string =>
  row.done ? `${name(row.stationId)}: lista` : `${name(row.stationId)}: ${row.ready} de ${plural(row.total, 'plato', 'platos')}`

// Mientras el servidor no anuncia ready, en lugar del boton: «Falta Cocina fría», «Falta revisar la alergia». Con el boton, nada.
export function missingText(ticket: PassTicket, name: (stationId: string) => string): string {
  if (ticket.canValidate) return ''
  const parts = ticket.missingStations.map(name)
  if (ticket.pendingReviews > 0) {
    const restrictions = new Set(ticket.course.preparations.filter(pendingReview).flatMap(preparation => (preparation.restrictions ?? []).map(restriction => restriction.id)))
    parts.push(restrictions.size > 1 ? 'revisar las alergias' : 'revisar la alergia')
  }
  return parts.length === 0 ? '' : `Falta ${joined(parts)}`
}

// «Marcar Cocina fría como lista» (respuesta 2 del 22-10: el pase marca por una estacion con la tablet estropeada).
export const markLabel = (mark: StationMark, name: (stationId: string) => string): string => `Marcar ${name(mark.stationId)} como lista`

// Lo que se emplata en el propio pase (platos de la estacion `pase`) lleva su «Listo» en SU plato: una orden preparations-ready con solo
// las elaboraciones de ese plato que el servidor deja marcar. Los platos de las estaciones no (para eso, «Marcar <estación> como lista»).
export const passDishIds = (ticket: PassTicket, dish: Dish): string[] => dish.stationId === PASS_STATION ? dishReadyIds(ticket.course, dish) : []

// La cabecera: «4 comandas»; en el pase, ademas, cuantos comensales hay por revisar antes de enviar («4 comandas · 1 por revisar»).
export const countText = (tickets: number, reviews: number | null): string =>
  reviews === null ? plural(tickets, 'comanda', 'comandas') : `${plural(tickets, 'comanda', 'comandas')} · ${reviews} por revisar`

// El plato en rojo si alguna de sus excepciones alarma; si no, en ambar (nunca atenuado).
export const dishAlarming = (dish: Dish): boolean => dish.exceptions.some(preparation => exceptionLine(preparation).alarming)

// «Siguiente: Postre (sin enviar)», solo de lectura: el ritmo lo marca el camarero.
export const nextText = (next: Course | null): string => next ? `Siguiente: ${next.name} (sin enviar)` : ''

// ---- «Esperando al camarero»: validado y sin servir ----
// Nunca «Sala avisada»: el servidor no sabe si el aviso llego a un movil (D2). Sale de la lectura, no del canal.
export interface WaitingCourse { key: string; entry: BoardEntry; course: Course }
const readyTime = (course: Course) => { const at = course.readyAt ? Date.parse(course.readyAt) : Number.NaN; return Number.isNaN(at) ? Number.POSITIVE_INFINITY : at }
// Orden ORDINAL a igual hora (como sala.ts): el mismo en cualquier tablet.
const ordinal = (left: string, right: string) => left < right ? -1 : left > right ? 1 : 0

export function waitingForWaiter(entries: readonly BoardEntry[]): WaitingCourse[] {
  const list: WaitingCourse[] = []
  for (const entry of entries) {
    if (finished(entry)) continue
    for (const course of entry.service.courses)
      if (same(course.state, 'Ready') && !course.preparations.some(pendingReview)) list.push({ key: ticketKey(entry, course), entry, course })
  }
  // Infinity - Infinity es NaN, que cuenta como empate: deciden la mesa y el pase.
  return list.sort((a, b) => (readyTime(a.course) - readyTime(b.course)) || ordinal(a.entry.service.tableId, b.entry.service.tableId) || ordinal(a.course.id, b.course.id))
}

// ---- El tiempo: siempre escrito; neutro, ambar desde 15 minutos y ambar oscuro desde 25 (respuesta 3 del 22-10). Nunca rojo. ----
export const AMBER_MINUTES = 15
export const DARK_AMBER_MINUTES = 25
export type TimeTone = 'neutral' | 'amber' | 'dark-amber'

// Minutos enteros desde una hora del servidor; nunca negativos (el reloj de la tablet puede ir por detras); null si no hay hora.
export function minutesSince(at: string | null | undefined, now: number): number | null {
  const time = at ? Date.parse(at) : Number.NaN
  return Number.isNaN(time) ? null : Math.max(0, Math.floor((now - time) / 60_000))
}
export function timeTone(firedAt: string | null | undefined, now: number): TimeTone {
  const minutes = minutesSince(firedAt, now)
  return minutes === null || minutes < AMBER_MINUTES ? 'neutral' : minutes < DARK_AMBER_MINUTES ? 'amber' : 'dark-amber'
}
// «Lleva 12 min» en los tres tramos (WCAG 1.4.1: nunca solo el color).
export function elapsedText(firedAt: string | null | undefined, now: number): string {
  const minutes = minutesSince(firedAt, now)
  if (minutes === null) return 'Hora de envío desconocida'
  return minutes < 1 ? 'Lleva menos de 1 min' : `Lleva ${minutesLabel(minutes * 60_000)}`
}
// «listo hace 3 min» desde readyAt; sin hora del servidor, nada (no se inventa).
export function readySince(readyAt: string | null | undefined, now: number): string {
  const minutes = minutesSince(readyAt, now)
  if (minutes === null) return ''
  return minutes < 1 ? 'listo hace menos de 1 min' : `listo hace ${minutesLabel(minutes * 60_000)}`
}

// ---- Comanda nueva, para el sonido (respuesta 4 del 22-10) ----
// Como ReadyWatch de sala.ts: tras CADA lectura correcta, las claves que no estaban en la anterior; la primera lectura no trae nada
// (lo que ya estaba al encender no suena). Una comanda que vuelve a la rejilla (un «Rehacer» del pase) es otra vez nueva. Sale de la
// RELECTURA, nunca del contenido del aviso del canal (D2).
export class TicketWatch {
  private previous: Set<string> | null = null
  read(keys: readonly string[]): string[] {
    const before = this.previous
    this.previous = new Set(keys)
    return before === null ? [] : unique(keys).filter(key => !before.has(key))
  }
}
// Suena solo con algo nuevo y despues de «Empezar turno»: el navegador no deja sonar hasta que se toca la pagina.
export const shouldChime = (fresh: readonly string[], shiftStarted: boolean): boolean => shiftStarted && fresh.length > 0
// Las claves que vigila cada pantalla: la rejilla. En una estacion, sus comandas por hacer (lo que baja a «Hecho» no suena; si vuelve
// con un «Rehacer», si); en el pase (o sin estacion), todas sus comandas.
export const ringKeys = (entries: readonly BoardEntry[], station: string | null | undefined): string[] =>
  seesWholeTable(station) ? passTickets(entries).map(ticket => ticket.key)
    : stationTickets(entries, station as string).filter(ticket => !ticket.done).map(ticket => ticket.key)

// ---- Dos pantallas a la vez en la misma mesa ----
// Un version_conflict (otra estacion toco la misma mesa a la vez) ya cierra la orden, sin aplicar nada, y relee (operations.ts). Aqui
// solo se dice en la propia comanda que se toque otra vez: nunca se reenvia sola, ni con otra clave.
export const CONFLICT_TEXT = 'Otra pantalla cambió esta mesa a la vez. Ya se ha vuelto a leer: toca otra vez si sigue haciendo falta.'
export const versionConflict = (outcome: Outcome | null | undefined): boolean => outcome?.kind === 'rejected' && outcome.code === 'version_conflict'

// ---- E4e (ADR-016): «Por revisar antes de enviar», del pase (movido tal cual de kitchen.ts) ----
// Cocina decide cada plato de un comensal con restriccion ANTES de que salga su pase, y ese pase espera. Aqui se listan, por mesa y
// comensal, los platos de los pases SIN ENVIAR que el servidor marca por revisar y en los que le ANUNCIA la revision a este
// dispositivo (solo al pase, D4.3a): nada se deduce aqui, una estacion no ve nada. Lo enviado sigue en su comanda.
export interface PresendItem { course: Course; preparation: Preparation }
export interface PresendGuest {
  key: string                        // servicio y comensal ('all' = toda la mesa)
  entry: BoardEntry
  guestPosition: number | null       // null = un plato para toda la mesa (recibe cualquier restriccion)
  restrictions: GuestRestriction[]   // las que llevan sus platos, sin repetir: siempre escritas
  items: PresendItem[]               // en el orden de los pases y de los platos
}
// Tope de review-preparations (DiningOperations.MaxGroupReviews): «No le afecta a nada más» manda como mucho estos en una orden; si
// quedaran mas, siguen en la lista y el boton vuelve a salir.
export const MAX_GROUP_REVIEWS = 60

export function presendReviews(entries: readonly BoardEntry[]): PresendGuest[] {
  const guests: PresendGuest[] = []
  for (const entry of entries) {
    const byGuest = new Map<string, PresendGuest>()
    for (const course of entry.service.courses) {
      if (course.state !== 'Pending') continue
      for (const preparation of course.preparations) {
        if (preparation.reviewPending !== true || !announces(preparation.actions, 'review-preparation')) continue
        const guestPosition = preparation.guestPosition ?? null
        const key = `${entry.service.id}:${guestPosition ?? 'all'}`
        let guest = byGuest.get(key)
        if (!guest) { guest = { key, entry, guestPosition, restrictions: [], items: [] }; byGuest.set(key, guest) }
        guest.items.push({ course, preparation })
        for (const restriction of preparation.restrictions ?? [])
          if (!guest.restrictions.some(known => known.id === restriction.id)) guest.restrictions.push(restriction)
      }
    }
    // Toda la mesa primero (lo compartido), despues cada comensal por su numero.
    guests.push(...[...byGuest.values()].sort((a, b) => (a.guestPosition ?? 0) - (b.guestPosition ?? 0)))
  }
  // Por mesa (su codigo, con los numeros en orden: M2 antes que M10) y, dentro, por comensal.
  return guests.sort((a, b) => a.entry.service.tableId.localeCompare(b.entry.service.tableId, 'es', { numeric: true }))
}

// «No le afecta a nada más»: lo que queda sin decidir de ese comensal, en UNA orden review-preparations, con la misma nota en cada plato.
// Solo los platos cuyo pase anuncia la orden de grupo (el servidor la deduce de la individual, ya filtrada por rol y estacion).
export function restOfGuest(guest: PresendGuest): PresendItem[] {
  return guest.items.filter(item => announces(item.course.actions, 'review-preparations')).slice(0, MAX_GROUP_REVIEWS)
}
export function restBody(guest: PresendGuest, note: string): { expectedVersion: number; items: Array<{ courseId: string; itemId: string; decision: string; note: string }> } {
  return { expectedVersion: guest.entry.version,
    items: restOfGuest(guest).map(item => ({ courseId: item.course.id, itemId: item.preparation.id, decision: 'unaffected', note: note.trim() })) }
}

// Las decisiones que se ofrecen ANTES de enviar: «Rehacer» es solo para lo que ya salio (nothing_to_remake).
export const PRESEND_DECISIONS = [
  { decision: 'unaffected', label: 'No le afecta' },
  { decision: 'adapt', label: 'Adaptado' },
] as const

// Formato de lo tecleado (lo unico local): la nota de una revision es obligatoria.
export const reviewNoteOk = (note: string) => note.trim().length >= 2

// Atajos del pase que RELLENAN la nota a la vista (plan 2026-10-22, seccion 4.3): la nota sigue siendo obligatoria y se puede corregir.
// «Cambiado por …» deja la nota abierta para escribir por que (open: la pantalla pone el cursor al final).
export const NOTE_SHORTCUTS = [
  { label: 'No lo lleva', text: 'No lo lleva', open: false },
  { label: 'Sin gluten', text: 'Sin gluten', open: false },
  { label: 'Cambiado por …', text: 'Cambiado por ', open: true },
] as const
export type NoteShortcut = typeof NOTE_SHORTCUTS[number]
export const applyShortcut = (shortcut: NoteShortcut): string => shortcut.text
// Una nota que es solo el principio de un atajo abierto («Cambiado por», o con sus puntos) aun no dice nada: no hay decision.
const unfinished = (note: string) => NOTE_SHORTCUTS.some(shortcut => shortcut.open
  && [shortcut.text, shortcut.label].some(start => note.trim().replace(/\s+/g, ' ').toLowerCase() === start.trim().toLowerCase()))
export const noteReady = (note: string): boolean => reviewNoteOk(note) && !unfinished(note)

// Lo ENVIADO por revisar (D3.5): tambien «Rehacer», que devuelve el plato a la estacion y retira la validacion. Con las palabras de
// Sala y del PC («No le afecta», «Adaptado: …»).
export const REVIEW_DECISIONS = [
  { decision: 'unaffected', label: 'No le afecta' },
  { decision: 'adapt', label: 'Adaptado' },
  { decision: 'remake', label: 'Rehacer' },
] as const
