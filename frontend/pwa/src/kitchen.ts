import type { BoardEntry, Course, GuestRestriction, Preparation } from './types'

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
  if (!firedAt) return 'hora de envío desconocida'
  const minutes = Math.max(0, Math.floor((now - Date.parse(firedAt)) / 60_000))
  return minutes < 1 ? 'enviado hace menos de 1 min' : `enviado hace ${minutes} min`
}

// ---- E4e (ADR-016; plan 2026-10-26, seccion 10): «Por revisar antes de enviar», lo minimo del pase hasta C3d ----
// Cocina decide cada plato de un comensal con restriccion ANTES de que salga su pase, y ese pase espera. Aqui se listan, por mesa y
// comensal, los platos de los pases SIN ENVIAR que el servidor marca por revisar y en los que le ANUNCIA la revision a este
// dispositivo (solo al pase, D4.3a): nada se deduce aqui, una estacion no ve nada. Lo enviado sigue en su comanda, como siempre.
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

const announces = (actions: string[] | undefined, action: string) => actions?.includes(action) === true

export function presendReviews(entries: BoardEntry[]): PresendGuest[] {
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

export const REVIEW_DECISIONS = [
  { decision: 'unaffected', label: 'No afecta' },
  { decision: 'adapt', label: 'Adaptar' },
  { decision: 'remake', label: 'Rehacer' },
] as const
