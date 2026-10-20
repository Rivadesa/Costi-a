import { clockLabel, minutesLabel, plural, restrictionKindLabel, severityLabel, tableStateLabel } from './labels'
import type { Course, Dining, GuestRestriction } from './types'

// C3a: plano de mesas del comandero. Copia SIN DOM de Costina.Client/FloorPlan.cs (C2, puesto principal): las dos
// implementaciones recorren la MISMA tabla de casos (dotnet/tests/fixtures/floorplan-cases.json, en vitest y en ClientChecks),
// asi que no pueden separarse. Todo es PRESENTACION calculada de lo leido (mesas de /configuration cruzadas con el tablero):
// el estado de la tarjeta, su tiempo, el avance de los pases y el aviso de restricciones. Nada habilita ni decide una accion:
// los botones siguen saliendo solo de lo que anuncia el servidor (affordances). Un valor desconocido se muestra tal cual.

// Codigos de estado de la tarjeta (tableStateLabel los rotula). Un estado del servidor que no se conoce viaja crudo.
export const FREE = 'free', SEATED = 'seated', KITCHEN = 'kitchen', READY = 'ready', EATING = 'eating',
  PAUSED = 'paused', FINISHED = 'finished', CANCELLED = 'cancelled'
export const TILE_CODES: readonly string[] = [FREE, SEATED, KITCHEN, READY, EATING, PAUSED, FINISHED, CANCELLED]
const KNOWN_COURSE_STATES = ['Pending', 'Fired', 'Preparing', 'Ready', 'Served', 'Skipped']

// Los estados del servidor se comparan sin distinguir mayusculas (como Same en FloorPlan.cs).
const same = (value: string | null | undefined, code: string) => (value ?? '').toLowerCase() === code.toLowerCase()
const inKitchen = (course: Course) => same(course.state, 'Fired') || same(course.state, 'Preparing')

// Texto del estado de una tarjeta. Solo se rotulan los codigos de la tarjeta (comparacion EXACTA): un estado del servidor que
// no se conoce sale tal cual aunque se parezca a uno de ellos (un pase "Cancelled" no es una mesa cancelada, ni un servicio
// "Ready" una mesa lista para servir).
export const isTileCode = (state: string) => TILE_CODES.includes(state)
export const tileLabel = (state: string) => isTileCode(state) ? tableStateLabel(state) : state

// Estado de una mesa: libre sin servicio en el tablero; despues manda el estado del servicio y, en servicio, el de sus pases
// (listo para servir antes que en cocina; comiendo cuando ya se sirvio algo y no hay nada en cocina; sentados si aun no salio nada).
export function tileState(service: Dining | null | undefined): string {
  if (!service) return FREE
  if (same(service.state, 'Completed')) return FINISHED
  if (same(service.state, 'Cancelled')) return CANCELLED
  if (same(service.state, 'Paused')) return PAUSED
  if (same(service.state, 'Open')) return SEATED
  if (!same(service.state, 'InService')) return service.state
  const odd = service.courses.find(course => !KNOWN_COURSE_STATES.some(known => same(course.state, known)))
  if (odd) return odd.state
  if (service.courses.some(course => same(course.state, 'Ready'))) return READY
  if (service.courses.some(inKitchen)) return KITCHEN
  return service.courses.some(course => same(course.state, 'Served')) ? EATING : SEATED
}

const times = (values: Array<string | null | undefined>) => values.filter((value): value is string => !!value).map(value => Date.parse(value))

// Desde cuando dura lo que muestra la tarjeta, si la lectura lo dice: en cocina desde que se envio el pase, listo desde que cocina
// lo valido, comiendo desde el ultimo pase servido. Sentados, en pausa o terminado: el servidor no da la hora, no se inventa.
export function tileSince(service: Dining | null | undefined): number | null {
  const state = tileState(service)
  const courses = service?.courses ?? []
  const found = state === KITCHEN ? times(courses.filter(inKitchen).map(course => course.firedAt))
    : state === READY ? times(courses.filter(course => same(course.state, 'Ready')).map(course => course.readyAt))
    : state === EATING ? times(courses.map(course => course.servedAt))
    : []
  if (found.length === 0) return null
  return state === EATING ? Math.max(...found) : Math.min(...found)
}

// Los minutos del estado que se ve ("12 min"); sin hora del servidor, nada.
export function tileElapsed(service: Dining | null | undefined, now: number): string {
  const since = tileSince(service)
  return since === null ? '' : minutesLabel(now - since)
}

// Avance de los pases: cada uno con su tono (hecho, listo, en cocina, pendiente) y cuantos estan servidos u omitidos.
export function courseStep(course: Course): string {
  if (same(course.state, 'Served') || same(course.state, 'Skipped')) return 'done'
  if (same(course.state, 'Ready')) return 'ready'
  if (inKitchen(course)) return 'kitchen'
  return same(course.state, 'Pending') ? 'pending' : 'other'
}
export const courseSteps = (service: Dining | null | undefined): string[] => service ? service.courses.map(courseStep) : []
export const coursesDone = (service: Dining | null | undefined): number => courseSteps(service).filter(step => step === 'done').length
// El avance en corto de la tarjeta, "2 de 5"; sin pases, nada.
export function tileProgress(service: Dining | null | undefined): string {
  const steps = courseSteps(service)
  return steps.length === 0 ? '' : `${coursesDone(service)} de ${steps.length}`
}

// Aviso de restricciones de una mesa (siempre con texto): la palabra nombra lo mas alarmante y "· por revisar" se anade mientras
// cocina no haya revisado un cambio. alertIsAlarming dice si va en rojo (alergia, grave, desconocido o por revisar).
//  - "Alergia" si hay alguna alergia (con su gravedad tal cual si no se conoce: "Alergia (Critical)");
//  - un tipo que no se conoce, TAL CUAL y con su gravedad ("Anaphylaxis (Critical)"), aunque haya tambien una alergia;
//  - sin nada de eso, una intolerancia o preferencia grave o de gravedad desconocida con su tipo y su gravedad ("Intolerancia (grave)");
//  - "Restricción" solo para las moderadas o leves (y para un cambio sin revisar sin restricciones a la vista).
// Asi el rojo y el ambar nunca llevan la misma palabra y un valor desconocido nunca se rebaja a una palabra generica.
export const PENDING_REVIEW = 'por revisar'
const knownKind = (kind: string) => same(kind, 'Allergy') || same(kind, 'Intolerance') || same(kind, 'Preference')
const knownSeverity = (severity: string) => same(severity, 'Severe') || same(severity, 'Moderate') || same(severity, 'Mild')
const kindAndSeverity = (restriction: GuestRestriction) => `${restrictionKindLabel(restriction.kind)} (${severityLabel(restriction.severity)})`

// Una restriccion pide la maxima atencion salvo que sea, CON SEGURIDAD, una intolerancia o preferencia moderada o leve: una alergia,
// una gravedad grave o cualquier valor que no se conoce cuentan como alarma (nunca se rebajan).
export const restrictionAlarming = (restriction: GuestRestriction): boolean =>
  !(same(restriction.kind, 'Intolerance') || same(restriction.kind, 'Preference')) || !(same(restriction.severity, 'Moderate') || same(restriction.severity, 'Mild'))

export function alertWord(service: Dining | null | undefined): string {
  if (!service) return ''
  const list = service.restrictions ?? []
  const words: string[] = []
  const allergies = list.filter(restriction => same(restriction.kind, 'Allergy'))
  if (allergies.length > 0) {
    const odd = allergies.find(restriction => !knownSeverity(restriction.severity))
    words.push(odd ? `Alergia (${severityLabel(odd.severity)})` : 'Alergia')
  }
  words.push(...list.filter(restriction => !knownKind(restriction.kind)).map(kindAndSeverity))
  if (words.length === 0) {
    const alarming = list.filter(restrictionAlarming)
    // Primero las de gravedad desconocida, sin cambiar el orden de las demas (OrderBy estable del puesto).
    words.push(...[...alarming.filter(r => !knownSeverity(r.severity)), ...alarming.filter(r => knownSeverity(r.severity))].map(kindAndSeverity))
  }
  if (words.length === 0 && (list.length > 0 || service.restrictionsPendingAck === true)) words.push('Restricción')
  return [...new Set(words)].join(', ')
}

export function alert(service: Dining | null | undefined): string {
  if (!service) return ''
  const word = alertWord(service)
  return service.restrictionsPendingAck === true ? `${word} · ${PENDING_REVIEW}` : word
}

export const alertIsAlarming = (service: Dining | null | undefined): boolean =>
  !!service && (service.restrictionsPendingAck === true || (service.restrictions ?? []).some(restrictionAlarming))

// Linea de detalle de un pase en la ficha: cuando se sirvio, desde cuando esta en cocina y cuanto lleva hecho, quien falta por
// elegir... Sin el numero de personas no se sabe cuantos faltan por elegir.
export function courseDetail(course: Course, pax: number): string {
  const preparations = course.preparations
  if (same(course.state, 'Served')) return course.servedAt ? `Servido a las ${clockLabel(course.servedAt)}` : ''
  if (same(course.state, 'Skipped')) return course.skipReason ? `Motivo: ${course.skipReason}` : ''
  if (same(course.state, 'Ready')) return course.readyAt ? `Listo desde las ${clockLabel(course.readyAt)}` : ''
  if (inKitchen(course)) {
    const since = course.firedAt ? `En cocina desde las ${clockLabel(course.firedAt)}` : 'En cocina'
    return preparations.length === 0 ? since
      : `${since} · ${preparations.filter(p => same(p.state, 'Ready')).length} de ${plural(preparations.length, 'elaboración lista', 'elaboraciones listas')}`
  }
  if (!same(course.state, 'Pending')) return ''
  if (course.optional === true) return preparations.length === 0 ? 'Se piden platos de la carta' : plural(preparations.length, 'plato pedido', 'platos pedidos')
  if (course.choiceRequired === true) {
    const chosen = new Set(preparations.filter(p => p.guestPosition !== null && p.guestPosition !== undefined).map(p => p.guestPosition)).size
    return `Han elegido ${chosen} de ${plural(pax, 'persona', 'personas')}`
  }
  return plural(preparations.length, 'elaboración', 'elaboraciones')
}
