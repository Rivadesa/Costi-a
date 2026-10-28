import { dishes, dishNote, dishState, dishText, exceptionLine, type Dish, type ExceptionLine } from './cocina'
import type { OfferItem } from './configuration'
import { fireNextCourse, serviceCommand, type CommandRequest } from './ficha'
import { courseDetail, courseStep, restrictionAlarming } from './floor'
import { clockLabel, courseLabel, courseStateLabel, guestLabel, restrictionLabel, REVIEW_PENDING_TEXT, reviewOutcome } from './labels'
import type { BoardEntry, Course, Dining, GuestRestriction, Preparation } from './types'

// C3e (plan 2026-10-27, paso 2; maqueta 2026-10-22, pestanas «2 · Sala · Mesa» y «6 · Sala · servido»): los pases de la ficha de
// Costina Sala, SIN DOM (vitest). Todo es PRESENTACION de la lectura autoritativa del tablero:
//  - lo servido y lo omitido, recogido en UNA linea («✓ Aperitivos 20:41», «Postre omitido»), en el orden de la mesa;
//  - cada pase sin terminar con sus platos AGRUPADOS por la regla COMUN de cocina.ts (dishes, isException, lineRestrictions), la de
//    Costina Cocina y el PC (Costina.Client/DishGroups.cs), fijada por la tabla dotnet/tests/fixtures/kitchen-cases.json: romper un
//    caso rompe cocina.test, pases.test y ClientChecks. Sustituye la regla propia de Sala (TableCard.vue, C3a/E4e), que contaba como
//    excepcion un «No le afecta» bajo una restriccion de toda la mesa, la repetia en cada linea y no agrupaba nada
//    (docs/native/C3d-costina-cocina.md:324);
//  - las excepciones aparte, bajo su plato y NUNCA atenuadas (exceptionLine), en rojo solo si alarman. Mientras cocina no decide,
//    «Por revisar en cocina», la palabra del camarero (plan 2026-10-22, seccion 3), con el segundo parametro de exceptionLine. Las
//    restricciones de TODA LA MESA no se repiten en cada linea: ya estan en la franja roja de arriba;
//  - la nota de cada pase y la marca «Siguiente» en el que enviaria fire-next (fireNextCourse, la regla del boton grande);
//  - elegir y pedir: UNA orden choose o add-dish por toque, con la version LEIDA (F01), hasta E4g (plan 2026-10-15, 678-679);
//  - la lista de la hoja de alergias: cada restriccion con los platos a los que afecta y su estado (plan 2026-10-15, 680); en un pase
//    servido, el de servido y nunca una decision (lo servido no se revisa por una restriccion anotada despues).
// Nada habilita nada: un boton existe solo si el servidor anuncia su accion (ya filtrada por rol y estacion, D3.1 y E4e).

const announces = (actions: readonly string[] | undefined, action: string) => actions?.includes(action) === true
const pendingReview = (preparation: Preparation) => preparation.reviewPending === true
// Los estados del servidor se comparan sin distinguir mayusculas (como floor.ts y cocina.ts).
const same = (value: string | null | undefined, code: string) => (value ?? '').toLowerCase() === code.toLowerCase()

// «A», «A y B», «A, B y C».
function joined(parts: readonly string[]): string {
  return parts.length < 2 ? parts.join('') : `${parts.slice(0, -1).join(', ')} y ${parts.at(-1)}`
}

// ---- Lo servido y lo omitido, en una linea ----
export interface DoneCourse {
  course: Course
  skipped: boolean
  name: string
  time: string        // la hora en que se sirvio; sin la hora del servidor (o si se omitio), nada
  text: string        // «✓ Aperitivos 20:41» o «Postre omitido»
}
function doneCourse(course: Course): DoneCourse {
  if (same(course.state, 'Skipped')) return { course, skipped: true, name: course.name, time: '', text: `${course.name} omitido` }
  const time = course.servedAt ? clockLabel(course.servedAt) : ''
  return { course, skipped: false, name: course.name, time, text: `✓ ${[course.name, time].filter(part => part.length > 0).join(' ')}` }
}

// ---- Cada pase sin terminar, con sus platos agrupados ----
export const NEXT_MARK = 'Siguiente'
export const REVIEWED_TEXT = 'Revisado por cocina'

export interface TimelineDish {
  dish: Dish
  text: string                  // «Merluza á galega × 4» (dishText: la cantidad siempre escrita, como en cocina)
  note: string                  // «(1 adaptada)», «(1 por revisar)»; nada si no cambia nada
  state: string                 // «Sin enviar», «Por empezar», «2 de 4 listas»
  station: string               // la estacion por su nombre: el mismo plato en dos estaciones son dos platos
  exceptions: ExceptionLine[]   // aparte, bajo el plato, nunca atenuadas; «Por revisar en cocina» mientras cocina no decide
  alarming: boolean             // alguna excepcion alarma: el plato tambien en rojo
}
export interface TimelineCourse {
  course: Course
  position: number              // su numero en la mesa (1, 2, 3…), tambien contando los ya hechos
  step: string                  // courseStep: pending, kitchen, ready u other (un estado que no se conoce)
  state: string                 // «Sin enviar», «Falta elegir plato», «En cocina», «Listo para servir»
  next: boolean                 // «Siguiente»: el que enviaria fire-next, solo si el servidor lo anuncia
  ready: boolean                // listo para servir: en verde
  note: string                  // «Revisado por cocina», «Por revisar en cocina», «En el pase desde las 21:19»
  dishes: TimelineDish[]
}
export interface Timeline { done: DoneCourse[]; courses: TimelineCourse[] }

function timelineDish(dish: Dish, station: (stationId: string) => string): TimelineDish {
  const exceptions = dish.exceptions.map(preparation => exceptionLine(preparation, REVIEW_PENDING_TEXT))
  return { dish, text: dishText(dish), note: dishNote(dish), state: dishState(dish), station: station(dish.stationId), exceptions,
    alarming: exceptions.some(line => line.alarming) }
}

// La nota de un pase, de la lectura y sin inventar ninguna hora:
//  - sin enviar: «Por revisar en cocina» si cocina aun tiene algo que decidir, o «Revisado por cocina» si ya lo decidio todo; un menu
//    con eleccion o un grupo de carta dicen ademas quien ha elegido o que hay pedido (courseDetail, como hoy);
//  - en cocina: desde cuando y cuantos platos estan listos (courseDetail); «Revisado por cocina» ya no se dice (nada sale sin revisar,
//    E4e), pero una alergia anotada despues de enviar si: «Por revisar en cocina»;
//  - listo: «En el pase desde las 21:19» (readyAt), y «Por revisar en cocina» si una alergia llego despues de validar.
export function courseNote(course: Course, pax: number): string {
  const step = courseStep(course)
  const pending = course.preparations.some(pendingReview)
  const reviewed = !pending && course.preparations.some(preparation => preparation.review != null)
  const detail = step === 'ready' ? (course.readyAt ? `En el pase desde las ${clockLabel(course.readyAt)}` : '')
    : step === 'kitchen' ? courseDetail(course, pax)
    : step === 'pending' && (course.choiceRequired === true || course.optional === true) ? courseDetail(course, pax)
    : ''
  const review = pending ? REVIEW_PENDING_TEXT : reviewed && step === 'pending' ? REVIEWED_TEXT : ''
  return [detail, review].filter(part => part.length > 0).join(' · ')
}

// Los pases de la ficha: los hechos en una linea y, debajo, cada uno sin terminar (tambien uno en un estado que no se conoce: nunca se
// esconde un pase). station nombra cada estacion con su nombre configurado (Names.station).
export function timeline(service: Dining, station: (stationId: string) => string): Timeline {
  const next = announces(service.actions, 'fire-next') ? fireNextCourse(service) : null
  const done: DoneCourse[] = []
  const courses: TimelineCourse[] = []
  service.courses.forEach((course, index) => {
    const step = courseStep(course)
    if (step === 'done') { done.push(doneCourse(course)); return }
    courses.push({
      course, position: index + 1, step, state: courseStateLabel(course, service.pax), next: next === course, ready: step === 'ready',
      note: courseNote(course, service.pax), dishes: dishes(course.preparations).map(dish => timelineDish(dish, station)),
    })
  })
  return { done, courses }
}

// ---- Elegir y pedir: una orden por toque (hasta E4g), con la version LEIDA de esa mesa (F01) ----
// Las descripciones son las de hoy (DishPanel.vue): la orden pendiente nombra la mesa por su nombre, nunca por su codigo.
export function chooseRequest(entry: BoardEntry, course: Course, guestPosition: number, dish: { id: string; name: string }, tableName: string): CommandRequest | null {
  if (!announces(course.actions, 'choose')) return null
  return { path: serviceCommand(entry.service, 'choose'), body: { expectedVersion: entry.version, courseId: course.id, guestPosition, dishId: dish.id },
    description: `Elegir ${dish.name} para el comensal ${guestPosition} · ${tableName}` }
}

// La cantidad de un plato de la carta, de 1 a 20, como el servidor (DiningOperations.AddDish). Es lo unico que se comprueba aqui.
export const MAX_DISH_QUANTITY = 20
export function addDishRequest(entry: BoardEntry, course: Course, item: Pick<OfferItem, 'productId' | 'presentationId' | 'name'>, quantity: number,
  guestPosition: number | null, tableName: string): CommandRequest | null {
  if (!announces(course.actions, 'add-dish') || !Number.isInteger(quantity) || quantity < 1 || quantity > MAX_DISH_QUANTITY) return null
  return { path: serviceCommand(entry.service, 'add-dish'),
    body: { expectedVersion: entry.version, courseId: course.id, productId: item.productId, presentationId: item.presentationId, quantity, guestPosition },
    description: `Pedir ${quantity} × ${item.name} en ${course.name} · ${tableName}` }
}

// ---- La hoja de alergias (desde la franja roja) ----
export interface AllergyDish {
  course: Course
  preparations: Preparation[]   // las de ese plato con ese mismo estado (en un pase servido, todas las del plato)
  text: string                  // «Aperitivos · Ostra: Adaptado: sin almendra», «Aperitivos · Ostra × 4: Servido a las 20:41»
  pending: boolean              // «Por revisar en cocina», en ambar
}
export interface AllergyItem {
  restriction: GuestRestriction
  text: string                  // escrita entera: «Comensal 2 · ALERGIA frutos secos (grave)»
  alarming: boolean             // en rojo solo si alarma
  dishes: AllergyDish[]         // en el orden de los pases y de los platos
}

// «Comensal 3», «Comensales 1, 3 y 4»; un plato para compartir, «Toda la mesa».
function guestsText(preparations: readonly Preparation[]): string {
  const positions = [...new Set(preparations.map(preparation => preparation.guestPosition ?? null))]
  const numbers = positions.filter((position): position is number => position !== null).sort((a, b) => a - b)
  const parts = [
    ...(positions.includes(null) ? [guestLabel(null)] : []),
    ...(numbers.length === 1 ? [guestLabel(numbers[0])] : numbers.length > 1 ? [`Comensales ${joined(numbers.map(String))}`] : []),
  ]
  return parts.join(', ')
}

// Los platos de un pase a los que afecta una restriccion (el servidor ya pone en cada elaboracion las que le aplican), agrupados con la
// regla comun y, dentro de cada plato, por su decision: «Merluza á galega × 4: Por revisar en cocina». Si una decision es solo de
// algunos comensales del plato, se dice de cuales («Ostra · Comensal 2: Adaptado: sin almendra»): nunca se pierde de quien es.
// C3e (revision del paso 2): cada linea lleva SIEMPRE su estado, y en un pase SERVIDO es el de servido, nunca una decision:
//  - el servidor pone en cada elaboracion todas las restricciones que le aplican, tambien en lo servido (AllowedActions.cs,
//    ViewWithActions), pero lo servido no se marca para revisar (CourseExecution.FlagForReview, «if (Terminal) return 0»). Si el
//    comensal avisa despues de los aperitivos, sus platos llevan la restriccion nueva sin que cocina los mirara por ella, y la decision
//    que tengan puede ser de otra anterior: las restricciones no llevan hora, asi que aqui no se sabe. Se escribe lo que es cierto
//    siempre, «Aperitivos · Ostra × 4: Servido a las 20:41» (sin la hora del servidor, «Servido»), y el plato no se esconde: el
//    camarero sabe que ya se sirvio. Coste aceptado: un pase revisado por ESTA restriccion y servido despues tampoco dice su decision;
//  - sin decision ni revision pendiente fuera de lo servido (una mesa anterior a E4e), el estado del plato (dishState): «Por empezar».
function servedState(course: Course): string {
  return course.servedAt ? `${courseLabel(course.state)} a las ${clockLabel(course.servedAt)}` : courseLabel(course.state)
}
function affected(course: Course, restriction: GuestRestriction): AllergyDish[] {
  const touched = course.preparations.filter(preparation => (preparation.restrictions ?? []).some(candidate => candidate.id === restriction.id))
  const served = same(course.state, 'Served')
  return dishes(touched).flatMap(dish => {
    const byState = new Map<string, Preparation[]>()
    for (const preparation of dish.preparations) {
      const state = served ? servedState(course) : reviewOutcome(preparation)
      const group = byState.get(state)
      if (group) group.push(preparation); else byState.set(state, [preparation])
    }
    return [...byState.entries()].map(([outcome, preparations]) => {
      const whole = preparations.length === dish.preparations.length
      const quantity = preparations.reduce((total, preparation) => total + preparation.quantity, 0)
      const title = whole ? (quantity > 1 ? `${dish.name} × ${quantity}` : dish.name) : `${dish.name} · ${guestsText(preparations)}`
      const state = outcome || dishState({ ...dish, preparations })
      return { course, preparations, text: `${course.name} · ${title}: ${state}`, pending: !served && preparations.some(pendingReview) }
    })
  })
}

// Cada restriccion de la mesa (la lista autoritativa del servicio), las de TODA LA MESA primero y despues cada comensal por su numero,
// con los platos a los que afecta y su estado. Un pase omitido no sale de cocina: sus platos no se listan. Una restriccion sin platos
// sale igual: nunca se esconde una alergia.
export function allergyList(service: Dining): AllergyItem[] {
  const rank = (restriction: GuestRestriction) => restriction.guestPosition === null || restriction.guestPosition === undefined ? 0 : 1
  return [...(service.restrictions ?? [])]
    .sort((a, b) => (rank(a) - rank(b)) || ((a.guestPosition ?? 0) - (b.guestPosition ?? 0)))
    .map(restriction => ({
      restriction, text: restrictionLabel(restriction), alarming: restrictionAlarming(restriction),
      dishes: service.courses.filter(course => !same(course.state, 'Skipped')).flatMap(course => affected(course, restriction)),
    }))
}
