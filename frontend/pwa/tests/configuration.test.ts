import { afterEach, describe, expect, it, vi } from 'vitest'
import { createRenderer, defineComponent } from 'vue'
import type { Fetch } from '../src/api'
import {
  floorEmpty, floorRooms, floorSummary, floorView, Names, normalizeConfiguration, offerFor, offerName, OTHER_ROOM, RELOAD_MS, roomSummary, useConfiguration,
  type Configuration,
} from '../src/configuration'
import type { BoardEntry, Dining } from '../src/types'

// C3a: la configuracion del comandero (mesas con su sala, ofertas, estaciones): nombres en vez de codigos, plano por salas con las
// mesas libres, y relectura cada 5 minutos y a demanda (al abrir «Abrir mesa»).
const configuration: Configuration = {
  tables: [
    { id: 'M1', name: 'Mesa 1', capacity: 4, zoneId: 'sala', zoneName: 'Sala principal' },
    { id: 'T1', name: 'Terraza 1', capacity: 2, zoneId: 'terraza', zoneName: 'Terraza' },
    { id: 'M2', name: ' Mesa 2 ', capacity: 6, zoneId: 'sala', zoneName: 'Sala principal' },
  ],
  offers: [{ id: 'DIA', name: 'Menú del día', kind: 'tasting', courses: [] }],
  stations: [{ id: 'cold', name: 'Cocina fría', kind: 'kitchen' }, { id: 'hot', name: '  ', kind: 'kitchen' }],
}
const service = (id: string, tableId: string, pax: number, offerId: string | null = 'DIA'): Dining =>
  ({ id, tableId, pax, state: 'Open', offerId, courses: [], actions: [] })
const entry = (dining: Dining): BoardEntry => ({ version: 3, service: dining, occupancy: { id: dining.id, tableId: dining.tableId, serviceId: dining.id, state: 'Occupied', releasedAt: null, actions: [] }, occupancyVersion: 1 })

describe('names, never codes', () => {
  it('tables and stations by their configured name; an unknown one stays raw, never invented', () => {
    const names = new Names(configuration)
    expect(names.table('M2')).toBe('Mesa 2')
    expect(names.station('cold')).toBe('Cocina fría')
    expect(names.station('hot')).toBe('hot')                     // un nombre vacio no borra la estacion: sale su codigo
    expect(names.table('M9')).toBe('M9')
    expect(new Names(null).table('M1')).toBe('M1')               // antes de leer la configuracion
    expect(names.table(null)).toBe('')
  })
  it('the offer of a table is named only if this handheld read it (its code is never painted)', () => {
    expect(offerName(configuration, 'DIA')).toBe('Menú del día')
    expect(offerName(configuration, 'CENA')).toBe('')
    expect(offerName(configuration, null)).toBe('')
  })
  it('an older server: menus as tasting offers without courses, and no stations', () => {
    expect(normalizeConfiguration({ tables: [], menus: [{ id: 'm', name: 'Menú' }] })).toEqual({ tables: [], offers: [{ id: 'm', name: 'Menú', kind: 'tasting', courses: [] }], stations: [] })
  })
})

describe('floor plan by room', () => {
  it('one tile per active table, free ones too, grouped by room in the configuration order', () => {
    const rooms = floorRooms(configuration, [entry(service('s1', 'M2', 3))])
    expect(rooms.map(room => [room.id, room.name, room.tiles.map(tile => [tile.tableId, tile.name, tile.entry?.service.id ?? null])])).toEqual([
      ['sala', 'Sala principal', [['M1', 'Mesa 1', null], ['M2', 'Mesa 2', 's1']]],
      ['terraza', 'Terraza', [['T1', 'Terraza 1', null]]],
    ])
    expect(floorSummary(rooms)).toBe('1 de 3 mesas ocupadas · 3 personas')
    expect(rooms.map(roomSummary)).toEqual(['2 mesas · 1 ocupada', '1 mesa · 0 ocupadas'])
  })
  it('an occupied table that the configuration no longer lists is never hidden: it goes last, in «Otras mesas ocupadas»', () => {
    const rooms = floorRooms(configuration, [entry(service('s9', 'M9', 2)), entry(service('s1', 'M1', 2))])
    expect(rooms.at(-1)).toMatchObject({ id: OTHER_ROOM, name: 'Otras mesas ocupadas' })
    expect(rooms.at(-1)!.tiles.map(tile => [tile.tableId, tile.name, tile.entry?.service.id])).toEqual([['M9', 'M9', 's9']])
    expect(floorSummary(rooms)).toBe('2 de 4 mesas ocupadas · 4 personas')
  })
  it('without configuration (not read yet, or failed) the occupied tables are still there', () => {
    const rooms = floorRooms({ tables: [], offers: [], stations: [] }, [entry(service('s1', 'M1', 2))])
    expect(rooms.flatMap(room => room.tiles).map(tile => tile.tableId)).toEqual(['M1'])
    expect(floorRooms({ tables: [], offers: [], stations: [] }, [])).toEqual([])
    expect(floorSummary([])).toBe('')
  })
  it('a table without room (older server) goes under «Mesas»', () => {
    expect(floorRooms({ tables: [{ id: 'M1', name: 'Mesa 1', capacity: 2 }], offers: [], stations: [] }, []).map(room => room.name)).toEqual(['Mesas'])
  })
})

describe('the floor is painted only from a real read of the board (D6.1: nothing unread is shown as current)', () => {
  const read = new Date(), configured = { readAt: read, failed: false }
  it('with the configuration read but the board not yet: no tiles (every table would say «Libre» without knowing it)', () => {
    expect(floorRooms(configuration, []).flatMap(room => room.tiles).every(tile => tile.entry === null)).toBe(true)   // lo que se pintaria
    expect(floorView({ readAt: null, error: '' }, configured)).toBe('reading')
    expect(floorEmpty({ readAt: null }, configured, 0)).toBe(false)
  })
  it('a failed first board read is said, not replaced by free tables', () => {
    expect(floorView({ readAt: null, error: 'No se pudo leer el estado del servidor.' }, configured)).toBe('unread')
  })
  it('with the board read, it waits for the first answer of the configuration, good or failed (no code painted meanwhile)', () => {
    expect(floorView({ readAt: read, error: '' }, { readAt: null, failed: false })).toBe('reading')
    expect(floorView({ readAt: read, error: '' }, { readAt: null, failed: true })).toBe('ready')
    expect(floorView({ readAt: read, error: '' }, configured)).toBe('ready')
    expect(floorView({ readAt: read, error: 'Servidor sin respuesta.' }, configured)).toBe('ready')   // lo ultimo leido se queda (D6.2)
  })
  it('«No hay mesas activas» only when both reads went well and there is no table', () => {
    expect(floorEmpty({ readAt: read }, configured, 0)).toBe(true)
    expect(floorEmpty({ readAt: read }, { readAt: null }, 0)).toBe(false)                 // configuracion caida: no se sabe
    expect(floorEmpty({ readAt: read }, configured, 3)).toBe(false)
  })
})

describe('an open table keeps the offer it was opened with, even when it is no longer current', () => {
  const lunch = { id: 'COMIDA', name: 'Menú de mediodía', kind: 'set-menu', courses: [{ id: 'c1', name: 'Primero', dishes: [{ id: 'd1', name: 'Pulpo' }] }] }
  it('the current offer first, else the last definition read in this session; never one that was never read', () => {
    const after17 = { ...configuration, known: [lunch, ...configuration.offers] }
    expect(offerFor(after17, 'COMIDA')?.courses[0].dishes.map(dish => dish.name)).toEqual(['Pulpo'])
    expect(offerName(after17, 'COMIDA')).toBe('Menú de mediodía')
    expect(offerFor(configuration, 'COMIDA')).toBeNull()
    expect(offerFor(after17, null)).toBeNull()
  })
})

const renderer = createRenderer<object, object>({
  patchProp: () => undefined, insert: () => undefined, remove: () => undefined, createElement: () => ({}), createText: () => ({}),
  createComment: () => ({}), setText: () => undefined, setElementText: () => undefined, parentNode: () => null, nextSibling: () => null,
})

describe('useConfiguration: re-read on demand and every 5 minutes', () => {
  afterEach(() => { vi.useRealTimers() })
  function mount(fetcher: Fetch, onUnauthorized = () => undefined) {
    let used: ReturnType<typeof useConfiguration> | null = null
    const app = renderer.createApp(defineComponent({ setup() { used = useConfiguration({ token: 'dev.t.s' }, onUnauthorized, { fetcher }); return () => null } }))
    app.mount({})
    return { used: used!, unmount: () => app.unmount() }
  }

  it('reads on mount, again every 5 minutes and when asked (the open-table sheet), and stops when unmounted', async () => {
    vi.useFakeTimers()
    let reads = 0
    let tables = [{ id: 'M1', name: 'Mesa 1', capacity: 2 }]
    const fetcher: Fetch = async url => { if (String(url).endsWith('/configuration')) reads++; return new Response(JSON.stringify({ tables, offers: [], stations: [] }), { status: 200 }) }
    const { used, unmount } = mount(fetcher)
    await vi.advanceTimersByTimeAsync(0)
    expect(reads).toBe(1); expect(used.configuration.tables.map(table => table.name)).toEqual(['Mesa 1'])
    tables = [...tables, { id: 'M2', name: 'Mesa 2', capacity: 4 }]                // una mesa creada en el PC principal
    await vi.advanceTimersByTimeAsync(RELOAD_MS - 1000)
    expect(reads).toBe(1)
    await vi.advanceTimersByTimeAsync(1000)
    expect(reads).toBe(2); expect(used.configuration.tables.map(table => table.name)).toEqual(['Mesa 1', 'Mesa 2'])
    await used.reload()
    expect(reads).toBe(3)
    unmount()
    await vi.advanceTimersByTimeAsync(RELOAD_MS * 3)
    expect(reads).toBe(3)
  })

  it('a failed read keeps what was read and says so; a 401 tells the app', async () => {
    let status = 200
    let unauthorized = 0
    const fetcher: Fetch = async () => new Response(status === 200 ? JSON.stringify({ tables: [{ id: 'M1', name: 'Mesa 1', capacity: 2 }] }) : '{"error":"x"}', { status })
    const { used, unmount } = mount(fetcher, () => { unauthorized++ })
    try {
      await used.reload()
      expect(used.configuration.failed).toBe(false)
      status = 503; await used.reload()
      expect(used.configuration.failed).toBe(true); expect(used.configuration.tables).toHaveLength(1); expect(unauthorized).toBe(0)
      status = 401; await used.reload()
      expect(unauthorized).toBe(1)
      status = 200; await used.reload()
      expect(used.configuration.failed).toBe(false)
    } finally { unmount() }
  })

  it('an offer that stops being current (a lunch menu at 17:00) is remembered for the tables opened with it, never offered again', async () => {
    let offers = [{ id: 'COMIDA', name: 'Menú de mediodía', kind: 'set-menu', courses: [{ id: 'c1', name: 'Primero', dishes: [{ id: 'd1', name: 'Pulpo' }] }] },
      { id: 'CARTA', name: 'Carta', kind: 'a-la-carte', courses: [] }]
    const fetcher: Fetch = async () => new Response(JSON.stringify({ tables: [], offers, stations: [] }), { status: 200 })
    const { used, unmount } = mount(fetcher)
    try {
      await used.reload()
      offers = offers.filter(offer => offer.id !== 'COMIDA')
      await used.reload()
      expect(used.configuration.offers.map(offer => offer.id)).toEqual(['CARTA'])          // «Abrir mesa» solo ofrece lo vigente
      expect(offerFor(used.configuration, 'COMIDA')?.courses[0].dishes.map(dish => dish.name)).toEqual(['Pulpo'])
      expect(offerName(used.configuration, 'COMIDA')).toBe('Menú de mediodía')
    } finally { unmount() }
  })

  it('refuses a configuration with money in it, like every other read (ADR-007)', async () => {
    const fetcher: Fetch = async () => new Response(JSON.stringify({ tables: [], offers: [{ id: 'o', name: 'Menú', kind: 'tasting', courses: [], priceCents: 6500 }] }), { status: 200 })
    const { used, unmount } = mount(fetcher)
    try {
      await used.reload()
      expect(used.configuration.failed).toBe(true); expect(used.configuration.offers).toEqual([])
    } finally { unmount() }
  })
})
