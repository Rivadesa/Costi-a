import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { floorRooms, floorSummary, OTHER_ROOM, type Configuration, type FloorRoom } from '../src/configuration'
import { ALL_TITLE, FREE_TITLE, freeLabel, freeSections, MINE_EMPTY, MINE_TITLE, mineHint, mineRoomLabel, mineSections, mineSummary, type MesasSection } from '../src/mesas'
import type { Who } from '../src/sala'
import type { BoardEntry, Course, Dining } from '../src/types'

// C3e (plan 2026-10-27, paso 2): «Mis mesas» de Costina Sala, sin DOM. Ordena lo que ya da floorRooms (que no cambia): las mias por la
// sala de cada mesa, con las listas para servir arriba DENTRO de su sala (el mismo tileState que pinta la tarjeta en verde) y despues
// por el orden de las mesas; la mesa que la configuracion ya no lista, al final de las mias; y todas las libres al final, por sala.
const ANA: Who = { actor: 'device:Móvil Ana', station: 'sala-1' }
type Opener = Pick<Dining, 'openedBy' | 'openedStation'>
const OWN: Opener = { openedBy: 'device:Móvil Ana', openedStation: 'sala-1' }
const SAME_STATION: Opener = { openedBy: 'device:Móvil Luis', openedStation: 'sala-1' }
const OTHER_STATION: Opener = { openedBy: 'device:Móvil Eva', openedStation: 'sala-2' }
const PERSON: Opener = { openedBy: 'user:responsable' }   // en el PC con la contrasena del responsable: sin estacion
const BEFORE_E4E: Opener = {}                               // abierta antes de E4e: el servidor no dice quien

const course = (id: string, state: Course['state']): Course =>
  ({ id, name: id, state, firedAt: null, readyAt: null, servedAt: null, skipReason: null, preparations: [] })
const entry = (id: string, tableId: string, pax: number, state: Dining['state'], courses: Array<Course['state']>, opener: Opener): BoardEntry => ({
  version: 3, service: { id, tableId, pax, state, courses: courses.map((each, index) => course(`c${index}`, each)), actions: [], ...opener },
  occupancy: { id: `o-${id}`, tableId, serviceId: id, state: 'Occupied', releasedAt: null, actions: [] }, occupancyVersion: 1,
})
const table = (id: string, name: string, zoneId: string, zoneName: string) => ({ id, name, capacity: 4, zoneId, zoneName })
const configuration: Configuration = {
  tables: [
    table('M1', 'Mesa 1', 'sala1', 'Sala 1'), table('M2', 'Mesa 2', 'sala1', 'Sala 1'), table('M3', 'Mesa 3', 'sala1', 'Sala 1'),
    table('M4', 'Mesa 4', 'sala1', 'Sala 1'), table('M5', 'Mesa 5', 'sala1', 'Sala 1'), table('M6', 'Mesa 6', 'sala1', 'Sala 1'),
    table('M7', 'Mesa 7', 'sala1', 'Sala 1'), table('T1', 'Terraza 1', 'terraza', 'Terraza'), table('T2', 'Terraza 2', 'terraza', 'Terraza'),
  ],
  offers: [], stations: [],
}
// El tablero en un orden cualquiera: el de las secciones sale de la configuracion, no de la lectura.
const board: BoardEntry[] = [
  entry('s9', 'X9', 1, 'InService', ['Served', 'Pending'], OWN),           // ya no esta en la configuracion; mia, comiendo
  entry('s7', 'M7', 2, 'Paused', ['Ready'], OWN),                          // en pausa: no se pinta en verde, va por su orden
  entry('s1', 'M1', 2, 'InService', ['Served', 'Preparing'], OWN),         // en cocina
  entry('s2', 'M2', 4, 'InService', ['Ready'], OTHER_STATION),             // lista, pero de otra estacion: solo en «Todas»
  entry('s6', 'M6', 5, 'InService', ['Ready', 'Pending'], PERSON),         // lista; abierta por una persona en el PC
  entry('s3', 'M3', 3, 'InService', ['Served', 'Ready'], SAME_STATION),    // lista; otro movil de Sala 1
  entry('s5', 'M5', 2, 'Open', ['Pending'], BEFORE_E4E),                   // sentados; sin dato de quien la abrio
  entry('s8', 'T1', 4, 'Open', ['Pending'], SAME_STATION),
  entry('s10', 'X8', 6, 'InService', ['Pending'], OTHER_STATION),          // ya no esta en la configuracion y no es mia
]
const shape = (sections: readonly MesasSection[]) => sections.map(section => [section.id, section.name, section.tiles.map(tile => tile.tableId)])

describe('«Mis mesas»: the occupied ones of this device and its station, by the room of each table', () => {
  const rooms = floorRooms(configuration, board)
  it('ready to serve first WITHIN each room, then the rest by table order; a table no longer configured goes last of mine', () => {
    expect(shape(mineSections(rooms, ANA))).toEqual([
      ['sala1', 'Sala 1', ['M3', 'M6', 'M1', 'M5', 'M7']],
      ['terraza', 'Terraza', ['T1']],
      [OTHER_ROOM, 'Otras mesas ocupadas', ['X9']],
    ])
  })
  it('then ALL the free tables, grouped by the room of each table (there is no «my room» without joining station and room)', () => {
    expect(shape(freeSections(rooms))).toEqual([['sala1', 'Sala 1', ['M4']], ['terraza', 'Terraza', ['T2']]])
  })
  it('the tiles are the same ones floorRooms gives («Todas»): mine, free or of another station, nothing lost and nothing twice', () => {
    const shown = [...mineSections(rooms, ANA), ...freeSections(rooms)].flatMap(section => section.tiles)
    const all = rooms.flatMap(room => room.tiles)
    expect(shown.every(tile => all.includes(tile))).toBe(true)
    expect(new Set(shown).size).toBe(shown.length)
    expect(all.filter(tile => !shown.includes(tile)).map(tile => tile.tableId)).toEqual(['M2', 'X8'])
    expect(floorSummary(rooms)).toBe('9 de 11 mesas ocupadas · 29 personas')            // «Todas» conserva las palabras del Comedor
  })
  it('the ready ones go first by the SAME tileState as the green tile: a paused table with a Ready pass keeps its place', () => {
    const paused = floorRooms(configuration, [entry('a', 'M1', 2, 'Open', ['Pending'], OWN), entry('b', 'M2', 2, 'Paused', ['Ready'], OWN), entry('c', 'M3', 2, 'InService', ['Ready'], OWN)])
    expect(shape(mineSections(paused, ANA))).toEqual([['sala1', 'Sala 1', ['M3', 'M1', 'M2']]])
  })
  it('a phone without a station: only what it opened itself and what nobody knows who opened', () => {
    expect(shape(mineSections(rooms, { actor: 'device:Móvil Ana', station: null }))).toEqual([
      ['sala1', 'Sala 1', ['M6', 'M1', 'M5', 'M7']],
      [OTHER_ROOM, 'Otras mesas ocupadas', ['X9']],
    ])
  })
  it('none of mine: no sections and no summary (the screen says so), and the free tables are still all there', () => {
    const others = floorRooms(configuration, [entry('s2', 'M2', 4, 'InService', ['Ready'], OTHER_STATION)])
    expect(mineSections(others, ANA)).toEqual([])
    expect(mineSummary([])).toBe('')
    expect(freeSections(others).flatMap(section => section.tiles).map(tile => tile.tableId)).toEqual(['M1', 'M3', 'M4', 'M5', 'M6', 'M7', 'T1', 'T2'])
  })
  it('the order of the rooms is the one given, and «Otras mesas ocupadas» is always the last of mine', () => {
    const reordered: FloorRoom[] = [rooms.at(-1)!, ...rooms.slice(0, -1).reverse()]
    expect(shape(mineSections(reordered, ANA)).map(([id]) => id)).toEqual(['terraza', 'sala1', OTHER_ROOM])
  })
})

describe('the words of «Mis mesas» (the mock-up, maq:690-697)', () => {
  const rooms = floorRooms(configuration, board)
  it('«7 abiertas · 19 personas», «Sala 1 · 5 abiertas» per room and «Mesas libres · 2 libres»', () => {
    const mine = mineSections(rooms, ANA), free = freeSections(rooms)
    expect(mineSummary(mine)).toBe('7 abiertas · 19 personas')
    expect(mine.map(mineRoomLabel)).toEqual(['Sala 1 · 5 abiertas', 'Terraza · 1 abierta', 'Otras mesas ocupadas · 1 abierta'])
    expect(freeLabel(free)).toBe('Mesas libres · 2 libres')
  })
  it('singular and plural', () => {
    const one = floorRooms({ ...configuration, tables: configuration.tables.slice(0, 2) }, [entry('s1', 'M1', 1, 'Open', ['Pending'], OWN)])
    expect(mineSummary(mineSections(one, ANA))).toBe('1 abierta · 1 persona')
    expect(freeLabel(freeSections(one))).toBe('Mesas libres · 1 libre')
    expect(freeLabel([])).toBe('Mesas libres · 0 libres')
  })
  it('the help names the station by its NAME; without it, «tu estación» (never the code)', () => {
    expect(mineHint('Sala 1')).toBe('Las que abriste tú y las abiertas desde los otros móviles de Sala 1. Arriba, las listas para servir; las libres, al final.')
    for (const none of [null, undefined, '', '   '])
      expect(mineHint(none)).toBe('Las que abriste tú y las abiertas desde los otros móviles de tu estación. Arriba, las listas para servir; las libres, al final.')
    expect(mineHint('  Terraza ')).toContain('de Terraza.')
  })
  it('the titles and the empty case', () => {
    expect([MINE_TITLE, ALL_TITLE, FREE_TITLE]).toEqual(['Mis mesas', 'Todas las mesas', 'Mesas libres'])
    expect(MINE_EMPTY).toBe('Ahora no tienes ninguna mesa abierta. Las de las otras estaciones están en «Todas».')
  })
})

describe('mesas.ts itself', () => {
  const source = readFileSync(fileURLToPath(new URL('../src/mesas.ts', import.meta.url))).toString('utf8')
  const code = source.replace(/^\s*\/\/.*$/gm, '')
  const literals = [...code.matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])
  it('has no DOM and no storage: it runs in Node and only orders what floorRooms gives', () => {
    expect(code).not.toMatch(/\b(window|document|navigator|localStorage|indexedDB)\b/)
    expect([...code.matchAll(/from '([^']+)'/g)].map(match => match[1]).sort()).toEqual(['./configuration', './floor', './labels', './sala'])
  })
  it('no checkout word anywhere, not even in a comment, an identifier or inside another word', () => {
    expect(literals.length).toBeGreaterThan(5)
    expect(source).not.toMatch(/€|precio|saldo|pago|cuenta|importe|cents|amount/i)
  })
  it('Spanish with tildes', () => {
    const plain = ['estacion', 'estan', 'movil', 'moviles', 'mas']
    expect(literals.flatMap(text => plain.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)))).toEqual([])
    expect(source.charCodeAt(0)).not.toBe(0xfeff)
  })
})
