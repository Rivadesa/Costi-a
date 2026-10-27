import type { PendingLoad } from './pending'
import type { BoardEntry, Course, Dining, GuestRestriction, Preparation } from './types'

// Textos en pantalla. Regla de AGENTS.md: una alerta de alergia NUNCA depende solo del color; aqui
// todo aviso lleva palabra, sustancia y severidad escritas. Sin reglas de negocio: solo se rotula.
//
// C3a: el MISMO vocabulario que el puesto principal (Costina.Client/Labels.cs), con tildes. La tabla de casos comun
// dotnet/tests/fixtures/vocabulary-cases.json la recorren vitest y ClientChecks: las dos copias no pueden separarse.
// Reglas, como en el puesto:
//  - un valor DESCONOCIDO se muestra tal cual: nunca se rebaja a una etiqueta mas suave ni se inventa;
//  - ni mayusculas ni guiones cuentan: las lecturas llegan en PascalCase (InService) y /configuration manda los tipos de
//    estacion en minuscula (kitchen); las dos formas dan el mismo texto;
//  - el comandero NUNCA rotula vocabulario de caja (pagos, cobros, cuentas): esas acciones no estan aqui y saldrian en crudo.

// Clave de busqueda de una familia (espejo de Labels.Vocabulary.Key): solo letras y cifras, en minuscula.
const key = (value: string) => Array.from(value).filter(char => /[\p{L}\p{Nd}]/u.test(char)).join('').toLowerCase()

// Tabla de una familia: codigo -> texto. Lo que no esta en la tabla vuelve sin tocar.
function vocabulary(rows: Readonly<Record<string, string>>): (value: string | null | undefined) => string {
  const labels = new Map(Object.entries(rows).map(([code, label]) => [key(code), label]))
  return value => value === null || value === undefined ? '' : labels.get(key(value)) ?? value
}

// Las tablas, exportadas para que la prueba compruebe que no hay ni una palabra fuera de la tabla de casos comun.
export const VOCABULARY = {
  dining: { Open: 'Sentados', InService: 'En servicio', Paused: 'En pausa', Completed: 'Terminado', Cancelled: 'Cancelado' },
  course: { Pending: 'Sin enviar', Fired: 'En cocina', Preparing: 'En preparación', Ready: 'Listo para servir', Served: 'Servido', Skipped: 'Omitido' },
  preparation: { Pending: 'Sin enviar', Fired: 'Por empezar', Preparing: 'En preparación', Ready: 'Lista' },
  // C2: estado de una mesa en el plano (codigos de floor.ts). Solo presentacion: nunca habilita nada.
  tableState: {
    free: 'Libre', seated: 'Sentados', kitchen: 'En cocina', ready: 'Listo para servir', eating: 'Comiendo', paused: 'En pausa',
    finished: 'Terminado, por liberar', cancelled: 'Cancelado, por liberar',
  },
  // E4d: tasting es el «Menú degustación» del promotor; set-menu, el «Menú con elección» (cada persona elige).
  offerKind: { tasting: 'Menú degustación', 'set-menu': 'Menú con elección', 'a-la-carte': 'Carta' },
  stationKind: { kitchen: 'Cocina', pass: 'Pase', bar: 'Barra', room: 'Sala' },
  // D3.2: la alergia en mayusculas para que se vea sin depender del color.
  restrictionKind: { Allergy: 'ALERGIA', Intolerance: 'Intolerancia', Preference: 'Preferencia' },
  severity: { Severe: 'grave', Moderate: 'moderada', Mild: 'leve' },
  review: { Unaffected: 'no afecta', Adapt: 'adaptada', Remake: 'rehecha' },
  // Acciones que anuncia el servidor, solo las OPERATIVAS (las de caja no existen en el comandero).
  action: {
    open: 'Abrir mesa', start: 'Iniciar servicio', 'fire-next': 'Enviar siguiente pase a cocina', serve: 'Marcar servido',
    pause: 'Pausar servicio', resume: 'Reanudar servicio', complete: 'Terminar servicio', 'cancel-unstarted': 'Cancelar mesa sin empezar',
    skip: 'Omitir pase', ready: 'Validar pase listo', choose: 'Elegir plato', 'add-dish': 'Pedir plato',
    'preparation-start': 'Empezar elaboración', 'preparation-ready': 'Marcar elaboración lista', 'review-preparation': 'Revisar elaboración',
    'declare-restriction': 'Declarar restricción', 'remove-restriction': 'Retirar restricción', release: 'Liberar mesa',
    'add-consumption': 'Añadir consumo',
    // E4e: las ordenes de grupo (un plato agrupado, o el menu de un comensal, en UNA orden).
    'preparations-start': 'Empezar plato', 'preparations-ready': 'Marcar plato listo', 'review-preparations': 'Revisar platos',
  },
} as const satisfies Record<string, Record<string, string>>

export const diningLabel = vocabulary(VOCABULARY.dining)
export const courseLabel = vocabulary(VOCABULARY.course)
export const preparationLabel = vocabulary(VOCABULARY.preparation)
export const tableStateLabel = vocabulary(VOCABULARY.tableState)
export const offerKindLabel = vocabulary(VOCABULARY.offerKind)
export const stationKindLabel = vocabulary(VOCABULARY.stationKind)
export const restrictionKindLabel = vocabulary(VOCABULARY.restrictionKind)
export const severityLabel = vocabulary(VOCABULARY.severity)
export const reviewDecisionLabel = vocabulary(VOCABULARY.review)
export const actionLabel = vocabulary(VOCABULARY.action)

const same = (value: string | null | undefined, code: string) => (value ?? '').toLowerCase() === code.toLowerCase()

// Estado de un pase con su matiz (espejo de Labels.Course(CourseDto, pax)): el motivo de la omision, la eleccion pendiente de un
// menu con eleccion y el grupo de carta vacio. Sin el numero de personas solo se sabe que falta elegir cuando nadie ha elegido.
export function courseStateLabel(course: Course, pax?: number | null): string {
  if (same(course.state, 'Skipped') && course.skipReason) return `${courseLabel(course.state)}: ${course.skipReason}`
  if (same(course.state, 'Pending')) {
    if (course.optional === true && course.preparations.length === 0) return 'Sin platos pedidos'
    const chosen = new Set(course.preparations.filter(p => p.guestPosition !== null && p.guestPosition !== undefined).map(p => p.guestPosition)).size
    if (course.choiceRequired === true && (pax !== null && pax !== undefined ? chosen < pax : chosen === 0)) return 'Falta elegir plato'
  }
  return courseLabel(course.state)
}

// «Toda la mesa» o «Comensal n», como en el puesto (plan, sección 3: nada de «Mesa» ni de «Para compartir»).
export const guestLabel = (position: number | null | undefined) => position === null || position === undefined ? 'Toda la mesa' : `Comensal ${position}`

// Hora LOCAL del dispositivo (el servidor sella en UTC), como Labels.Clock del puesto.
export function clockLabel(at: string): string {
  const date = new Date(at)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

// Minutos transcurridos ("12 min", "1 h 05 min"), truncados y nunca negativos si el reloj del dispositivo va por detras del servidor.
export function minutesLabel(elapsedMs: number): string {
  const minutes = Math.max(0, Math.trunc(elapsedMs / 60_000))
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')} min`
}

export const plural = (count: number, one: string, many: string) => count === 1 ? `1 ${one}` : `${count} ${many}`

export function restrictionLabel(restriction: GuestRestriction): string {
  return `${guestLabel(restriction.guestPosition)} · ${restrictionKindLabel(restriction.kind)} ${restriction.substance} (${severityLabel(restriction.severity)})`
}

// E4e (plan 2026-10-15, seccion 3): lo que la SALA lee de la decision de cocina sobre el plato de un comensal (espejo de
// Labels.ReviewOutcome del puesto; tabla comun vocabulary-cases.json, «reviews»): «Por revisar en cocina» mientras cocina no decide y,
// despues, la decision delante del texto de cocina («Adaptado: tosta de maíz»). El nombre del plato no cambia. Una decision que no se
// conoce sale tal cual. C3d: Costina Cocina dice lo mismo («Adaptado: …»), y «Por revisar en el pase» mientras el pase no decide
// (cocina.ts, exceptionLine); el rotulo antiguo de la cocina («PENDIENTE DE REVISIÓN POR COCINA», «Revisada: …») se retira.
export const REVIEW_PENDING_TEXT = 'Por revisar en cocina'
const REVIEW_OUTCOMES = vocabulary({ Unaffected: 'No le afecta', Adapt: 'Adaptado', Remake: 'Rehecho' })
export function reviewOutcome(preparation: Preparation): string {
  if (preparation.reviewPending) return REVIEW_PENDING_TEXT
  return preparation.review ? `${REVIEW_OUTCOMES(preparation.review.decision)}: ${preparation.review.note}` : ''
}

// El pase "en curso" de una mesa: el primero que no esta servido ni omitido.
export const currentCourse = (service: Dining): Course | null => service.courses.find(course => course.state !== 'Served' && course.state !== 'Skipped') ?? null

export function boardSummary(entry: BoardEntry): string {
  const course = currentCourse(entry.service)
  return course ? `${course.name}: ${courseLabel(course.state)}` : 'Todos los pases servidos'
}

export const pendingReviews = (service: Dining): number => service.courses.reduce((total, course) => total + course.preparations.filter(p => p.reviewPending).length, 0)

export function ageLabel(seconds: number | null): string {
  if (seconds === null) return 'sin lectura'
  return seconds < 60 ? `hace ${seconds} s` : `hace ${Math.floor(seconds / 60)} min ${seconds % 60} s`
}
// C3a: la misma antiguedad en corto, junto al estado del enlace en un movil (sigue SIEMPRE a la vista, D6.2).
export const ageShort = (seconds: number | null): string => seconds === null ? 'Sin lectura' : `Leído ${ageLabel(seconds)}`

// D6.7: el nombre de dispositivo de un actor 'device:<nombre>'. Cualquier otro actor se muestra tal cual (nunca se suaviza).
export const deviceNameOf = (actor: string): string => actor.startsWith('device:') ? actor.slice('device:'.length) : actor

// Rotulo de una orden guardada que NO se puede reenviar. Un motivo desconocido se muestra tal cual.
const BLOCKED: Record<string, string> = { foreign: 'ORDEN DE OTRA INSTALACIÓN', unreadable: 'ORDEN ANTERIOR ILEGIBLE' }
export function blockedTitle(blocked: { reason: string; actor?: string }): string {
  if (blocked.reason === 'actor') return `ORDEN HECHA CON OTRO NOMBRE DE DISPOSITIVO («${deviceNameOf(blocked.actor ?? '')}»)`
  return BLOCKED[blocked.reason] ?? blocked.reason
}

// D6.7: una orden hecha con OTRO nombre de dispositivo. El servidor guarda la idempotencia por nombre (actor): reenviarla con
// el nombre actual podria aplicarla dos veces y preguntar por ella diria "no consta" aunque se aplicara. Se dice como comprobarla.
export function otherActorAdvice(actor: string): string {
  const name = `«${deviceNameOf(actor)}»`
  return `Se hizo cuando este dispositivo se llamaba ${name}, y ahora está emparejado con otro nombre. Con el nombre actual no se puede reenviar ni preguntar por ella: el servidor la trataría como una orden nueva. Para saber si se aplicó, compruébalo en el PC principal, o desvincula este dispositivo (Opciones) y vuelve a emparejarlo con el nombre ${name}. Después, si hace falta, descártala.`
}

// D6.7: al emparejar, el nombre con que se hizo la orden guardada (si es de un dispositivo): con ESE nombre se puede comprobar.
export function pendingDeviceName(load: PendingLoad | null): { name: string; description: string } | null {
  if (load?.kind !== 'restored' || !load.command.actor.startsWith('device:')) return null
  return { name: deviceNameOf(load.command.actor), description: load.command.description }
}

// D6.7 (revision): antes de emparejar no se sabe con que servidor (instalacion) se va a emparejar. El mismo nombre solo
// sirve con el MISMO servidor: con uno instalado de nuevo la orden queda bloqueada como de otra instalacion, sea cual sea
// el nombre. Por eso el consejo es condicional y nunca promete que con ese nombre baste.
export function pairPendingAdvice(name: string): string {
  return `Se hizo con el nombre «${name}». Si vuelves a emparejarlo con el mismo servidor, usa ese mismo nombre para poder comprobar si se aplicó. Con otro nombre, o si el servidor se ha instalado de nuevo, quedará bloqueada: compruébala en el PC principal y después descártala.`
}

// D6.7: aviso (no bloqueo) antes de desvincular con una orden guardada. Desvincular no la borra.
export function unpairWarning(load: PendingLoad | null): string {
  if (load?.kind === 'restored')
    return `Atención: hay una orden sin confirmar guardada en este dispositivo («${load.command.description}»). Desvincular no la borra, pero después solo se podrá comprobar volviendo a emparejarlo con este mismo servidor y el mismo nombre («${deviceNameOf(load.command.actor)}») o en el PC principal. Mejor resuélvela antes.`
  if (load?.kind === 'unreadable') return 'Atención: hay una orden anterior ilegible guardada en este dispositivo. Desvincular no la borra.'
  return ''
}
