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
