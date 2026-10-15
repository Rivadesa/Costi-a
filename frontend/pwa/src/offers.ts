// E4d: el menu del dia es la primera oferta vigente que da /configuration (el servidor las ordena como en Menus y cartas): se propone
// SIEMPRE al abrir mesa, aunque haya varias, sin DOM para poder probarlo. Sin ofertas, ninguna.
export interface ProposableOffer { id: string }

export function proposedOffer<T extends ProposableOffer>(offers: readonly T[]): T | null {
  return offers.length > 0 ? offers[0] : null
}

// E4d: etiqueta del tipo junto al nombre en el desplegable de abrir mesa (vocabulario del plan: "con eleccion", "carta"; un menu
// degustacion no lleva coletilla).
export function offerKindSuffix(kind: string): string {
  return kind === 'set-menu' ? ' (con elección)' : kind === 'a-la-carte' ? ' (carta)' : ''
}
