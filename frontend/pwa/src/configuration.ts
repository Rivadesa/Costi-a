import { onMounted, onUnmounted, reactive } from 'vue'
import { ApiError, call, type Fetch } from './api'
import { plural } from './labels'
import type { BoardEntry } from './types'

// C3a: configuracion operativa del comandero y de la cocina (GET /configuration): mesas ACTIVAS con su sala, ofertas vigentes ahora y
// estaciones. Sirve para escribir NOMBRES y no codigos (mesa, sala, estacion) y para pintar el plano por salas, tambien con las mesas
// libres. Se relee al montar, al abrir «Abrir mesa» y cada 5 minutos: un menu cambiado por la manana llega a un comandero abierto
// desde antes. Nada de esto habilita una accion: los botones siguen saliendo de lo que anuncia el servidor. Sin dinero (money-guard).
export interface TableChoice { id: string; name: string; capacity: number; zoneId?: string | null; zoneName?: string | null }
export interface StationChoice { id: string; name: string; kind: string; active?: boolean }
// E4a/E4b: oferta vigente con sus pases; dishes = platos de un menu con eleccion, items = lo pedible de un grupo de carta.
export interface OfferItem { productId: string; presentationId: string; name: string; presentation: string }
export interface OfferCourse { id: string; name: string; dishes: Array<{ id: string; name: string }>; items?: OfferItem[] }
export interface OfferChoice { id: string; name: string; kind: string; courses: OfferCourse[] }
// known: cada oferta leida en esta sesion, con su ultima definicion. /configuration solo lista las vigentes AHORA (un menu de comida
// deja de estarlo a las 17:00), pero una mesa abierta con ella sigue con sus platos: el servidor sigue anunciando 'choose' y 'add-dish'.
export interface Configuration { tables: TableChoice[]; offers: OfferChoice[]; stations: StationChoice[]; known?: OfferChoice[] }

interface RawConfiguration { tables?: TableChoice[]; menus?: Array<{ id: string; name: string }>; offers?: OfferChoice[]; stations?: StationChoice[] }

// Un servidor anterior a E4a solo manda 'menus' (se tratan como menus degustacion sin pases) y uno anterior a C2 no manda estaciones.
export function normalizeConfiguration(raw: RawConfiguration): Configuration {
  return {
    tables: raw.tables ?? [],
    offers: raw.offers ?? (raw.menus ?? []).map(menu => ({ ...menu, kind: 'tasting', courses: [] })),
    stations: raw.stations ?? [],
  }
}

// Nombres de mesas y estaciones (espejo de Costina.Client/Names): el nombre configurado o, si la configuracion no lo tiene (aun sin
// leer, o una mesa ya desactivada), el codigo tal cual: nunca se inventa un nombre.
export class Names {
  private readonly tables: Map<string, string>
  private readonly stations: Map<string, string>
  constructor(configuration?: Configuration | null) {
    this.tables = Names.index(configuration?.tables ?? [])
    this.stations = Names.index(configuration?.stations ?? [])
  }
  table(id: string | null | undefined): string { return Names.lookup(this.tables, id) }
  station(id: string | null | undefined): string { return Names.lookup(this.stations, id) }
  private static index(rows: ReadonlyArray<{ id: string; name: string }>): Map<string, string> {
    const index = new Map<string, string>()
    for (const row of rows) if (row.name.trim().length > 0 && !index.has(row.id)) index.set(row.id, row.name.trim())
    return index
  }
  private static lookup(index: Map<string, string>, id: string | null | undefined): string { return id === null || id === undefined ? '' : index.get(id) ?? id }
}

// Plano por salas (espejo de ServiceViewModel.RebuildFloor del puesto): una tarjeta por mesa activa de la configuracion, cruzada con el
// tablero, en el orden de la configuracion. Una mesa ocupada que la configuracion ya no lista sale igual, al final, en «Otras mesas
// ocupadas»: nunca se esconde una mesa con gente.
export const OTHER_ROOM = '~otras'
export interface FloorTile { key: string; tableId: string; name: string; capacity: number; roomId: string; roomName: string; entry: BoardEntry | null }
export interface FloorRoom { id: string; name: string; tiles: FloorTile[] }

export function floorRooms(configuration: Configuration, entries: readonly BoardEntry[]): FloorRoom[] {
  const names = new Names(configuration)
  const rooms = new Map<string, FloorRoom>()
  const add = (tile: FloorTile) => {
    const room = rooms.get(tile.roomId) ?? { id: tile.roomId, name: tile.roomName, tiles: [] }
    room.tiles.push(tile); rooms.set(tile.roomId, room)
  }
  const configured = new Set(configuration.tables.map(table => table.id))
  for (const table of configuration.tables) {
    const roomId = table.zoneId ?? '', roomName = table.zoneName?.trim() || 'Mesas'
    const own = entries.filter(entry => entry.service.tableId === table.id)
    if (own.length === 0) add({ key: `free:${table.id}`, tableId: table.id, name: names.table(table.id), capacity: table.capacity, roomId, roomName, entry: null })
    for (const entry of own) add({ key: entry.service.id, tableId: table.id, name: names.table(table.id), capacity: table.capacity, roomId, roomName, entry })
  }
  for (const entry of entries.filter(candidate => !configured.has(candidate.service.tableId)))
    add({ key: entry.service.id, tableId: entry.service.tableId, name: names.table(entry.service.tableId), capacity: 0, roomId: OTHER_ROOM, roomName: 'Otras mesas ocupadas', entry })
  return [...rooms.values()]
}

// "7 de 12 mesas ocupadas · 19 personas" y, por sala, "8 mesas · 6 ocupadas" (los mismos textos que el Comedor).
export function floorSummary(rooms: readonly FloorRoom[]): string {
  const tiles = rooms.flatMap(room => room.tiles)
  if (tiles.length === 0) return ''
  const occupied = tiles.filter(tile => tile.entry !== null)
  const people = occupied.reduce((total, tile) => total + (tile.entry?.service.pax ?? 0), 0)
  return `${occupied.length} de ${plural(tiles.length, 'mesa ocupada', 'mesas ocupadas')} · ${plural(people, 'persona', 'personas')}`
}
export const roomSummary = (room: FloorRoom): string =>
  `${plural(room.tiles.length, 'mesa', 'mesas')} · ${plural(room.tiles.filter(tile => tile.entry !== null).length, 'ocupada', 'ocupadas')}`

// La oferta con la que se abrio una mesa: la vigente o, si ya no lo es, la ultima definicion leida en esta sesion (sus platos siguen
// valiendo para esa mesa). «Abrir mesa» ofrece SOLO las vigentes (configuration.offers), nunca estas.
export const offerFor = (configuration: Configuration, offerId: string | null | undefined): OfferChoice | null =>
  offerId ? configuration.offers.find(offer => offer.id === offerId) ?? configuration.known?.find(offer => offer.id === offerId) ?? null : null
// Su nombre. Una oferta que este comandero nunca leyo no se nombra: no se pinta su codigo, los pases ya dicen que pasa.
export const offerName = (configuration: Configuration, offerId: string | null | undefined): string => offerFor(configuration, offerId)?.name ?? ''

// D6.1: nada sin leer se pinta como actual. El plano sale solo tras una lectura REAL del tablero (sin ella, cada mesa de la configuracion
// saldria «Libre» sin saberlo, y una mesa ocupada ofreceria «Abrir mesa») y tras la primera respuesta de la configuracion, buena o
// fallida (sin ella, una mesa ocupada saldria un instante con su codigo en «Otras mesas ocupadas»). 'unread': la lectura del tablero
// fallo y no hay ninguna anterior; el aviso del fallo ya esta en StatusPanel.
export type FloorView = 'reading' | 'unread' | 'ready'
export function floorView(board: { readAt: Date | null; error: string }, configuration: { readAt: Date | null; failed: boolean }): FloorView {
  if (board.readAt === null) return board.error ? 'unread' : 'reading'
  return configuration.readAt !== null || configuration.failed ? 'ready' : 'reading'
}
// «No hay mesas activas» solo cuando las dos lecturas fueron bien alguna vez y no hay ninguna mesa: sin configuracion no se sabe.
export const floorEmpty = (board: { readAt: Date | null }, configuration: { readAt: Date | null }, tiles: number): boolean =>
  board.readAt !== null && configuration.readAt !== null && tiles === 0

export const RELOAD_MS = 5 * 60_000

// Lectura de la configuracion para una pantalla. Un fallo conserva lo ultimo leido (y lo dice `failed`); un 401 avisa a la aplicacion,
// que relee la sesion. Lecturas solapadas se funden en una.
export function useConfiguration(props: { token: string }, onUnauthorized: () => void = () => undefined,
                                 deps: { fetcher?: Fetch; everyMs?: number } = {}) {
  const configuration = reactive<Configuration & { known: OfferChoice[]; readAt: Date | null; failed: boolean }>(
    { tables: [], offers: [], stations: [], known: [], readAt: null, failed: false })
  let current: Promise<void> | null = null
  let timer: ReturnType<typeof setInterval> | undefined

  async function read(): Promise<void> {
    try {
      // window.fetch se llama sin receptor (D6.3): se resuelve en cada lectura, no se guarda como metodo de un objeto.
      const raw = await call<RawConfiguration>(deps.fetcher ?? ((input, init) => fetch(input, init)), '/configuration', { token: props.token })
      const next = normalizeConfiguration(raw)
      // Las ofertas que dejan de ser vigentes se recuerdan (con su ultima definicion) para las mesas ya abiertas con ellas.
      const known = new Map(configuration.known.map(offer => [offer.id, offer]))
      for (const offer of next.offers) known.set(offer.id, offer)
      Object.assign(configuration, next, { known: [...known.values()], readAt: new Date(), failed: false })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) onUnauthorized()
      configuration.failed = true
    }
  }
  function reload(): Promise<void> {
    current ??= read().finally(() => { current = null })
    return current
  }
  onMounted(() => { void reload(); timer = setInterval(() => { void reload() }, deps.everyMs ?? RELOAD_MS) })
  onUnmounted(() => clearInterval(timer))
  return { configuration, reload }
}
