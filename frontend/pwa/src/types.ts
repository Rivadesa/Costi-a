// Proyeccion OPERATIVA de /api/native/v1 (espejo de los DTO del servidor). Ningun campo economico:
// si el servidor anadiera uno, money-guard rechazaria la respuesta antes de llegar aqui.
export interface GuestRestriction { id: string; guestPosition: number | null; kind: 'Allergy' | 'Intolerance' | 'Preference'; substance: string; severity: 'Severe' | 'Moderate' | 'Mild' }
export interface PreparationReview { decision: 'Unaffected' | 'Adapt' | 'Remake'; note: string; at: string }
export interface Preparation {
  id: string; name: string; stationId: string; quantity: number; guestPosition: number | null; mandatory: boolean
  state: 'Pending' | 'Fired' | 'Preparing' | 'Ready'
  actions?: string[]; restrictions?: GuestRestriction[]; reviewPending?: boolean; review?: PreparationReview | null
}
export interface Course {
  id: string; name: string; state: 'Pending' | 'Fired' | 'Preparing' | 'Ready' | 'Served' | 'Skipped'
  firedAt: string | null; readyAt: string | null; servedAt: string | null; skipReason: string | null
  preparations: Preparation[]; actions?: string[]
}
export interface Dining {
  id: string; tableId: string; pax: number; state: 'Open' | 'InService' | 'Paused' | 'Completed' | 'Cancelled'
  courses: Course[]; actions?: string[]; restrictions?: GuestRestriction[]; restrictionsPendingAck?: boolean
}
export interface Occupancy { id: string; tableId: string; serviceId: string; state: 'Occupied' | 'Released'; releasedAt: string | null; actions?: string[] }
export interface BoardEntry { version: number; service: Dining; occupancy: Occupancy; occupancyVersion: number }
