import { OTHER_ROOM, type FloorRoom, type FloorTile } from './configuration'
import { READY, tileState } from './floor'
import { plural } from './labels'
import { isMine, type Who } from './sala'

// C3e (plan 2026-10-27, paso 2; plan 2026-10-22, 4.4): «Mis mesas» de Costina Sala, sin DOM (vitest). Solo ORDENA lo que ya da
// floorRooms (configuration.ts), que no cambia: las mismas tarjetas, sin copiar ni rehacer ninguna. Todo es presentacion de la lectura
// autoritativa del tablero: nada habilita nada (los botones siguen saliendo de lo que anuncia el servidor) y no es atribucion a una
// persona (ADR-018).
//  - mineSections: las ocupadas mias (sala.ts, isMine), agrupadas por la sala de CADA MESA, en el orden de la configuracion. Dentro de
//    cada sala, primero las listas para servir (el MISMO tileState que pinta la tarjeta en verde, floor.ts) y despues las demas, por el
//    orden de las mesas (orden estable). Una mesa ocupada que la configuracion ya no lista («Otras mesas ocupadas») va al final de las
//    mias, si es mia: nunca se esconde una mesa con gente.
//  - freeSections: despues, TODAS las libres, agrupadas por la sala de cada mesa. No hay «mi sala» ni «otras salas»: falta unir estacion
//    y sala, que es esquema (plan 2026-10-22, 4.4 y seccion 6).
//  - Una sala sin mesas que ensenar no sale (la maqueta, maq:693-701).
//  - «Todas» es floorRooms tal cual, con floorSummary: aqui no se toca.
export interface MesasSection { id: string; name: string; tiles: FloorTile[] }

const ready = (tile: FloorTile) => tileState(tile.entry?.service) === READY

export function mineSections(rooms: readonly FloorRoom[], who: Who): MesasSection[] {
  const sections: MesasSection[] = []
  for (const room of rooms) {
    const own = room.tiles.filter(tile => tile.entry !== null && isMine(tile.entry.service, who))
    // sort es estable: a igual rango manda el orden de las mesas que da floorRooms.
    const tiles = [...own].sort((a, b) => Number(ready(b)) - Number(ready(a)))
    if (tiles.length > 0) sections.push({ id: room.id, name: room.name, tiles })
  }
  // «Otras mesas ocupadas» siempre al final de las mias, aunque llegue en otro orden.
  return [...sections.filter(section => section.id !== OTHER_ROOM), ...sections.filter(section => section.id === OTHER_ROOM)]
}

export function freeSections(rooms: readonly FloorRoom[]): MesasSection[] {
  const sections: MesasSection[] = []
  for (const room of rooms) {
    const tiles = room.tiles.filter(tile => tile.entry === null)
    if (tiles.length > 0) sections.push({ id: room.id, name: room.name, tiles })
  }
  return sections
}

// ---- Palabras de la maqueta (maq:677,690-697): «abiertas», como la maqueta; «Todas» conserva las del Comedor (floorSummary) ----
export const MINE_TITLE = 'Mis mesas'
export const ALL_TITLE = 'Todas las mesas'
export const FREE_TITLE = 'Mesas libres'
export const MINE_EMPTY = 'Ahora no tienes ninguna mesa abierta. Las de las otras estaciones están en «Todas».'

const opened = (count: number) => plural(count, 'abierta', 'abiertas')
const tilesOf = (sections: readonly MesasSection[]) => sections.flatMap(section => section.tiles)

// «7 abiertas · 19 personas». Sin mesas mias, nada: la pantalla lo dice con MINE_EMPTY (como floorSummary sin mesas).
export function mineSummary(sections: readonly MesasSection[]): string {
  const tiles = tilesOf(sections)
  if (tiles.length === 0) return ''
  const people = tiles.reduce((total, tile) => total + (tile.entry?.service.pax ?? 0), 0)
  return `${opened(tiles.length)} · ${plural(people, 'persona', 'personas')}`
}
// «Sala 1 · 6 abiertas», en cada seccion de las mias.
export const mineRoomLabel = (section: MesasSection): string => `${section.name} · ${opened(section.tiles.length)}`
// «Mesas libres · 3 libres».
export const freeLabel = (sections: readonly MesasSection[]): string => `${FREE_TITLE} · ${plural(tilesOf(sections).length, 'libre', 'libres')}`

// La ayuda (maq:691), con el NOMBRE de la estacion de /session; sin nombre, la frase sin nombrarla, nunca el codigo.
export function mineHint(stationName: string | null | undefined): string {
  const name = stationName?.trim() || 'tu estación'
  return `Las que abriste tú y las abiertas desde los otros móviles de ${name}. Arriba, las listas para servir; las libres, al final.`
}
