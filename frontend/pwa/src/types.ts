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
  choiceRequired?: boolean   // E4a: pase de menu cerrado (cada comensal elige antes de disparar)
  optional?: boolean         // E4b: grupo de carta libre (los platos se piden; vacio se omite)
}
export interface Dining {
  id: string; tableId: string; pax: number; state: 'Open' | 'InService' | 'Paused' | 'Completed' | 'Cancelled'
  offerId?: string | null   // E4a: oferta con la que se abrio la mesa
  // E4e/C3e (plan 2026-10-27, paso 2): quien abrio la mesa (el actor de la orden open: device:<nombre> o user:<nombre>) y desde que
  // estacion (el codigo; ninguna para una persona). El servidor los omite si no los sabe (abierta antes de E4e). Solo para «Mis mesas»
  // (sala.ts, isMine): presentacion, nunca permiso. Aditivo: money-guard no los toca.
  openedBy?: string | null; openedStation?: string | null
  courses: Course[]; actions?: string[]; restrictions?: GuestRestriction[]; restrictionsPendingAck?: boolean
}
export interface Occupancy { id: string; tableId: string; serviceId: string; state: 'Occupied' | 'Released'; releasedAt: string | null; actions?: string[] }
// C3b: «Lo que lleva la mesa»: bebidas y extras ya enviados (tambien los de Caja), SIN importes ni totales (ADR-007, apartado C3b).
// presentation es null en la presentacion por defecto (no se escribe); guestPosition null = toda la mesa; at null en una linea
// anterior a C3b; voided = anulado en Caja. productId/presentationId null en una linea muy antigua, sin producto.
export interface TableConsumption {
  id: string; productId: string | null; presentationId: string | null; name: string; presentation: string | null
  quantity: number; guestPosition: number | null; at: string | null; voided: boolean
}
// C3b: consumptions es opcional: un servidor anterior no lo envia.
export interface BoardEntry { version: number; service: Dining; occupancy: Occupancy; occupancyVersion: number; consumptions?: TableConsumption[] }
