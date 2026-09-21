import type { BoardEntry, Course, Preparation } from './types'

// Pantalla de cocina (D6.4). SOLO presentacion: que pases se pintan, en que orden y que lineas van atenuadas.
// Nada de esto autoriza nada: un boton existe si y solo si el servidor anuncia la accion en esa elaboracion
// o en ese pase (affordances ya filtradas por rol y estacion, D4.3a). `own` sirve para atenuar, no para permitir.
export const PASS_STATION = 'pase'

export interface KitchenLine { preparation: Preparation; own: boolean }
export interface KitchenTicket {
  key: string
  entry: BoardEntry
  course: Course
  lines: KitchenLine[]
  paused: boolean
  pendingReviews: number   // elaboraciones de ESTE pase a la espera de decision del pase (D3.5)
}

// El pase (y un dispositivo de cocina sin estacion: decision 4A, espejo puro de lo que anuncia el servidor) ve todo.
const seesEverything = (station: string | null) => station === null || station === PASS_STATION

export function kitchenTickets(entries: BoardEntry[], station: string | null): KitchenTicket[] {
  const all = seesEverything(station)
  const tickets: KitchenTicket[] = []
  for (const entry of entries) {
    const service = entry.service
    if (service.state === 'Completed' || service.state === 'Cancelled') continue
    for (const course of service.courses) {
      // Enviados o en preparacion. Los LISTOS solo interesan a quien valida y entrega (pase) hasta que se sirven.
      const active = course.state === 'Fired' || course.state === 'Preparing' || (all && course.state === 'Ready')
      if (!active) continue
      const lines = course.preparations.map(preparation => ({ preparation, own: all || preparation.stationId === station }))
      // Un pase sin nada de esta estacion es ruido en su pantalla: no se pinta.
      if (!lines.some(line => line.own)) continue
      tickets.push({
        key: `${service.id}:${course.id}`, entry, course, lines, paused: service.state === 'Paused',
        pendingReviews: course.preparations.filter(preparation => preparation.reviewPending === true).length,
      })
    }
  }
  // Lo mas antiguo primero. Un pase sin hora de envio (no deberia existir si esta activo) va al final, estable por mesa.
  return tickets.sort((a, b) => {
    const left = a.course.firedAt ? Date.parse(a.course.firedAt) : Number.POSITIVE_INFINITY
    const right = b.course.firedAt ? Date.parse(b.course.firedAt) : Number.POSITIVE_INFINITY
    return left - right || a.entry.service.tableId.localeCompare(b.entry.service.tableId) || a.course.id.localeCompare(b.course.id)
  })
}

// Tiempo desde el envio, siempre escrito. El reloj de la tablet puede ir desajustado respecto al servidor:
// nunca se pinta un tiempo negativo.
export function elapsedLabel(firedAt: string | null, now: number): string {
  if (!firedAt) return 'hora de envio desconocida'
  const minutes = Math.max(0, Math.floor((now - Date.parse(firedAt)) / 60_000))
  return minutes < 1 ? 'enviado hace menos de 1 min' : `enviado hace ${minutes} min`
}

// Formato de lo tecleado (lo unico local): la nota de una revision es obligatoria.
export const reviewNoteOk = (note: string) => note.trim().length >= 2

export const REVIEW_DECISIONS = [
  { decision: 'unaffected', label: 'No afecta' },
  { decision: 'adapt', label: 'Adaptar' },
  { decision: 'remake', label: 'Rehacer' },
] as const
