import type { PendingLoad } from './pending'
import type { BoardEntry, Course, Dining, GuestRestriction, Preparation } from './types'

// Textos en pantalla. Regla de AGENTS.md: una alerta de alergia NUNCA depende solo del color; aqui
// todo aviso lleva palabra, sustancia y severidad escritas. Sin reglas de negocio: solo se rotula.
const DINING: Record<Dining['state'], string> = { Open: 'Mesa abierta', InService: 'En servicio', Paused: 'EN PAUSA', Completed: 'Terminado', Cancelled: 'Cancelado' }
const COURSE: Record<Course['state'], string> = { Pending: 'Sin enviar', Fired: 'Enviado a cocina', Preparing: 'En preparacion', Ready: 'LISTO PARA SERVIR', Served: 'Servido', Skipped: 'Omitido' }
const PREPARATION: Record<Preparation['state'], string> = { Pending: 'Sin enviar', Fired: 'Enviada', Preparing: 'En preparacion', Ready: 'Lista' }
const KIND: Record<GuestRestriction['kind'], string> = { Allergy: 'ALERGIA', Intolerance: 'Intolerancia', Preference: 'Preferencia' }
const SEVERITY: Record<GuestRestriction['severity'], string> = { Severe: 'grave', Moderate: 'moderada', Mild: 'leve' }
const REVIEW: Record<string, string> = { Unaffected: 'no afecta', Adapt: 'adaptada', Remake: 'rehecha' }

export const diningLabel = (state: Dining['state']) => DINING[state] ?? state
export const courseLabel = (state: Course['state']) => COURSE[state] ?? state
export const preparationLabel = (state: Preparation['state']) => PREPARATION[state] ?? state

export function restrictionLabel(restriction: GuestRestriction): string {
  const who = restriction.guestPosition === null ? 'Mesa' : `Comensal ${restriction.guestPosition}`
  return `${who} · ${KIND[restriction.kind] ?? restriction.kind} ${restriction.substance} (${SEVERITY[restriction.severity] ?? restriction.severity})`
}

export function reviewLabel(preparation: Preparation): string {
  if (preparation.reviewPending) return 'PENDIENTE DE REVISION POR COCINA'
  return preparation.review ? `Revisada: ${REVIEW[preparation.review.decision] ?? preparation.review.decision} — ${preparation.review.note}` : ''
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
