import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  alsoText, AMBER_MINUTES, DARK_AMBER_MINUTES, dishes, dishNote, dishState, dishText, doneText, elapsedText, exceptionLine, lineRestrictions,
  listoPerDish, MAX_GROUP_PREPARATIONS, MAX_GROUP_REVIEWS, markLabel, missingText, nextText, passTickets, PRESEND_DECISIONS, presendReviews,
  progressText, readyBody, readySince, restBody, restOfGuest, reviewNoteOk, seesWholeTable, shouldChime, stationTickets, TicketWatch, timeTone,
  waitingForWaiter, wholeTable,
} from '../src/cocina'
import type { BoardEntry, Course, GuestRestriction, Preparation } from '../src/types'

// C3d (plan 2026-10-27; plan 2026-10-22, seccion 4.3): la logica SIN DOM de Costina Cocina. Todo es presentacion de la lectura
// autoritativa del tablero: ningun boton sale de aqui si el servidor no anuncia su accion (ya filtrada por rol y estacion).

// ---- La tabla COMUN con el PC (ClientChecks recorre la misma contra Costina.Client/DishGroups.cs) ----
type Row = { case: string; preparations: Preparation[]; dishes: Array<Record<string, unknown> & { exceptions: object[] }> }
const cases = JSON.parse(readFileSync(fileURLToPath(new URL('../../../dotnet/tests/fixtures/kitchen-cases.json', import.meta.url))).toString('utf8')) as { groups: Row[] }
// Como Only de ClientChecks: un apartado o un campo que esta prueba no conoce es un fallo (lo que solo hiciera un lado no se mira).
function only(row: object, fields: string[], where: string): void {
  expect(Object.keys(row).filter(field => !fields.includes(field)), `unknown field in the shared cases (${where})`).toEqual([])
}

describe('kitchen dishes: the case table shared with the main PC (DishGroups.cs)', () => {
  it('every section of the table is known here', () => { only(cases, ['about', 'groups'], 'sections') })
  it.each(cases.groups.map(row => [row.case, row] as const))('%s', (_, row) => {
    only(row, ['case', 'preparations', 'dishes'], 'groups')
    for (const dish of row.dishes) {
      only(dish, ['name', 'station', 'quantity', 'preparations', 'exceptions', 'state', 'ready'], 'dishes')
      for (const exception of dish.exceptions) only(exception, ['id', 'restrictions'], 'exceptions')
    }
    expect(dishes(row.preparations).map(dish => ({
      name: dish.name, station: dish.stationId, quantity: dish.quantity, preparations: dish.preparations.map(p => p.id),
      exceptions: dish.exceptions.map(p => ({ id: p.id, restrictions: lineRestrictions(p).map(r => r.id) })), state: dishState(dish), ready: dish.ready,
    }))).toEqual(row.dishes)
  })
})

// ---- Datos de prueba ----
const NUTS: GuestRestriction = { id: 'r-frutos', guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' }
const SHELLFISH: GuestRestriction = { id: 'r-marisco', guestPosition: 3, kind: 'Allergy', substance: 'marisco', severity: 'Severe' }
const GLUTEN: GuestRestriction = { id: 'r-gluten', guestPosition: null, kind: 'Allergy', substance: 'gluten', severity: 'Severe' }
const CORIANDER: GuestRestriction = { id: 'r-cilantro', guestPosition: 1, kind: 'Preference', substance: 'cilantro', severity: 'Mild' }
const AT = '2026-10-27T19:00:00Z'
const prep = (id: string, name: string, stationId: string, guestPosition: number | null, extra: Partial<Preparation> = {}): Preparation =>
  ({ id, name, stationId, quantity: 1, guestPosition, mandatory: true, state: 'Fired', actions: ['preparation-start', 'preparation-ready'], ...extra })
const course = (id: string, name: string, state: Course['state'], firedAt: string | null, preparations: Preparation[], extra: Partial<Course> = {}): Course =>
  ({ id, name, state, firedAt, readyAt: null, servedAt: null, skipReason: null, preparations, actions: ['preparations-start', 'preparations-ready'], ...extra })
const entry = (tableId: string, courses: Course[], state: BoardEntry['service']['state'] = 'InService', pax = 4): BoardEntry => ({
  version: 7, occupancyVersion: 1,
  service: { id: `s-${tableId}`, tableId, pax, state, courses },
  occupancy: { id: `o-${tableId}`, tableId, serviceId: `s-${tableId}`, state: 'Occupied', releasedAt: null },
})
const four = (name: string, stationId: string, extra: (guest: number) => Partial<Preparation> = () => ({})) =>
  [1, 2, 3, 4].map(guest => prep(`${name}-${guest}`, name, stationId, guest, extra(guest)))
const STATIONS: Record<string, string> = { hot: 'Cocina caliente', cold: 'Cocina fría', pase: 'Pase' }
const nameOf = (id: string) => STATIONS[id] ?? id
const key = (ticket: { entry: BoardEntry; course: Course }) => `${ticket.entry.service.tableId}/${ticket.course.id}`

// Cocina caliente a las 20:10: M1 (19:50), M9 (19:55, lo suyo ya listo) y M4 (20:00, la merluza con una alergia adaptada).
const BOARD: BoardEntry[] = [
  entry('M4', [
    course('aperitivo', 'Aperitivo', 'Served', '2026-10-27T19:30:00Z', [prep('pan-1', 'Pan', 'hot', 1, { state: 'Ready', actions: [] })], { actions: [] }),
    course('principal', 'Principal', 'Fired', '2026-10-27T20:00:00Z', [
      ...four('Merluza á galega', 'hot', guest => guest === 2
        ? { restrictions: [NUTS], review: { decision: 'Adapt', note: 'sin almendra', at: AT } } : {}),
      ...four('Ensalada', 'cold'),
    ]),
    course('postre', 'Postre', 'Pending', null, [prep('tarta-1', 'Tarta de Santiago', 'cold', 1, { state: 'Pending', actions: [] })], { actions: ['skip'] }),
  ]),
  entry('M1', [course('segundo', 'Segundo', 'Preparing', '2026-10-27T19:50:00Z', [
    prep('rodaballo-1', 'Rodaballo salvaje', 'hot', 1, { state: 'Preparing', actions: ['preparation-ready'] }), prep('rodaballo-2', 'Rodaballo salvaje', 'hot', 2)])], 'InService', 2),
  entry('M9', [course('aperitivo', 'Aperitivo', 'Preparing', '2026-10-27T19:55:00Z', [
    prep('zamb-1', 'Zamburiñas a la plancha', 'hot', 1, { state: 'Ready', actions: [] }), prep('zamb-2', 'Zamburiñas a la plancha', 'hot', 2, { state: 'Ready', actions: [] }),
    prep('ostra-1', 'Ostras de Arcade', 'cold', 1), prep('ostra-2', 'Ostras de Arcade', 'cold', 2)])], 'InService', 2),
  entry('M2', [course('primero', 'Primero', 'Pending', null, [prep('sopa-1', 'Sopa', 'hot', 1, { state: 'Pending', actions: [] })], { actions: ['skip'] })], 'Open'),
  entry('M6', [course('p1', 'Entrante', 'Ready', '2026-10-27T19:40:00Z', [prep('caldo-1', 'Caldo', 'hot', 1, { state: 'Ready', actions: [] })],
    { readyAt: '2026-10-27T20:07:00Z', actions: [] })]),
  entry('M3', [course('p1', 'Entrante', 'Fired', '2026-10-27T19:00:00Z', [prep('croqueta-1', 'Croquetas', 'hot', 1)])], 'Cancelled'),
  entry('M5', [course('p1', 'Entrante', 'Fired', '2026-10-27T19:10:00Z', [prep('tosta-1', 'Tosta', 'cold', 1)])]),
]

describe('a station sees only its own work', () => {
  const tickets = stationTickets(BOARD, 'hot')
  it('one ticket per table and course with its own work, oldest first; never unsent, validated, cancelled or other stations\' courses', () => {
    expect(tickets.map(key)).toEqual(['M1/segundo', 'M9/aperitivo', 'M4/principal'])
    expect(tickets.map(ticket => ticket.key)).toEqual(['s-M1:segundo', 's-M9:aperitivo', 's-M4:principal'])   // la clave de hoy
    expect(stationTickets(BOARD, 'cold').map(key)).toEqual(['M5/p1', 'M9/aperitivo', 'M4/principal'])
  })
  it('its own dishes only, grouped and summed; the other stations of the course are named in «También en»', () => {
    const m4 = tickets.find(ticket => ticket.entry.service.tableId === 'M4')!
    expect(m4.dishes.map(dishText)).toEqual(['Merluza á galega × 4'])
    expect(m4.dishes.flatMap(dish => dish.preparations).every(p => p.stationId === 'hot')).toBe(true)
    expect(m4.alsoIn).toEqual(['cold'])
    expect(alsoText(m4.alsoIn, nameOf)).toBe('También en: Cocina fría')
    expect(alsoText([], nameOf)).toBe('')
    expect(alsoText(['cold', 'pase', 'bar'], nameOf)).toBe('También en: Cocina fría, Pase y bar')   // sin nombre configurado, el codigo tal cual
  })
  it('the allergy goes apart under its dish, written and never dimmed, and the dish says how many are adapted', () => {
    const [merluza] = tickets.find(ticket => ticket.entry.service.tableId === 'M4')!.dishes
    expect(merluza.exceptions.map(exceptionLine).map(line => [line.text, line.alarming, line.pending]))
      .toEqual([['Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra', true, false]])
    expect(dishNote(merluza)).toBe('(1 adaptada)')
  })
  it('«Listo» is ONE preparations-ready with its own preparations that the server lets it mark, with the version read', () => {
    const m1 = tickets.find(ticket => ticket.entry.service.tableId === 'M1')!
    expect(m1.readyIds).toEqual(['rodaballo-1', 'rodaballo-2'])
    expect(readyBody(m1.entry, m1.course, m1.readyIds)).toEqual({ expectedVersion: 7, courseId: 'segundo', itemIds: ['rodaballo-1', 'rodaballo-2'] })
    // Nada se deduce: sin preparations-ready anunciado en el pase, o sin preparation-ready en una elaboracion, no va.
    const quiet = stationTickets([entry('M7', [course('p1', 'Entrante', 'Fired', AT, [prep('a', 'A', 'hot', 1)], { actions: [] })])], 'hot')
    expect(quiet[0].readyIds).toEqual([])
    const partly = stationTickets([entry('M7', [course('p1', 'Entrante', 'Fired', AT, [prep('a', 'A', 'hot', 1), prep('b', 'A', 'hot', 2, { actions: [] })])])], 'hot')
    expect(partly[0].readyIds).toEqual(['a'])
    // Como mucho lo que admite una orden de grupo (DiningOperations.MaxGroupPreparations); el resto, en el siguiente toque.
    const many = Array.from({ length: MAX_GROUP_PREPARATIONS + 3 }, (_, index) => prep(`p-${index}`, 'Pincho', 'hot', null))
    expect(stationTickets([entry('M7', [course('p1', 'Entrante', 'Fired', AT, many)])], 'hot')[0].readyIds).toHaveLength(MAX_GROUP_PREPARATIONS)
    expect(MAX_GROUP_PREPARATIONS).toBe(200)
  })
  it('each dish has its own «Listo» only with two or more dishes of its own', () => {
    expect(listoPerDish(tickets.find(ticket => ticket.entry.service.tableId === 'M4')!)).toBe(false)
    const two = stationTickets([entry('M11', [course('p1', 'Primeros', 'Fired', AT, [prep('c', 'Croquetas de lacón', 'hot', 1), prep('p', 'Pimientos de Padrón', 'hot', 2)])])], 'hot')
    expect(listoPerDish(two[0])).toBe(true)
  })
  it('«Hecho · esperando al pase»: what is all ready leaves the grid, without a time, until the pass validates it', () => {
    expect(tickets.filter(ticket => ticket.done).map(key)).toEqual(['M9/aperitivo'])
    const m9 = tickets.find(ticket => ticket.done)!
    expect(doneText(m9)).toBe('Zamburiñas a la plancha × 2')
    expect(m9.readyIds).toEqual([])
    // Cuando el pase valida, el pase queda Listo y sale tambien de la franja (M6 ya esta validado: no sale en ningun sitio).
    expect(tickets.map(key)).not.toContain('M6/p1')
  })
  it('a whole-table restriction is written ONCE, above the ticket, never on every line', () => {
    const board = [entry('M8', [course('p1', 'Principal', 'Fired', AT, four('Merluza', 'hot', guest => ({ restrictions: [GLUTEN],
      review: { decision: guest === 3 ? 'Adapt' : 'Unaffected', note: guest === 3 ? 'sin harina' : 'no lleva gluten', at: AT } })))])]
    const [ticket] = stationTickets(board, 'hot')
    expect(ticket.wholeTable).toEqual([GLUTEN])
    expect(ticket.dishes[0].exceptions.map(p => exceptionLine(p).text)).toEqual(['Comensal 3 · Adaptado: sin harina'])
    expect(exceptionLine(ticket.dishes[0].exceptions[0]).alarming).toBe(true)                 // adaptado por una alergia que alarma: rojo
    expect(wholeTable(board[0].service.courses[0].preparations)).toEqual([GLUTEN])
  })
  it('an allergy noted with the course already sent says «Por revisar en el pase»; a shared dish says whose allergy it is', () => {
    const pending = exceptionLine(prep('m-3', 'Merluza', 'hot', 3, { restrictions: [SHELLFISH], reviewPending: true }))
    expect([pending.text, pending.pending, pending.alarming]).toEqual(['Comensal 3 · ALERGIA marisco (grave) · Por revisar en el pase', true, true])
    expect(exceptionLine(prep('tabla', 'Tabla', 'cold', null, { restrictions: [SHELLFISH, GLUTEN], reviewPending: true })).text)
      .toBe('Toda la mesa · ALERGIA marisco (grave) del Comensal 3 · Por revisar en el pase')
    expect(exceptionLine(prep('o-1', 'Ostra', 'cold', 1, { restrictions: [CORIANDER], review: { decision: 'Unaffected', note: 'no lleva cilantro', at: AT } })))
      .toMatchObject({ text: 'Comensal 1 · Preferencia cilantro (leve) · No le afecta: no lleva cilantro', alarming: false, pending: false })
  })
  it('the dish note counts adapted, remade and pending lines', () => {
    const dish = dishes([
      prep('a', 'Vieira', 'hot', 1, { restrictions: [NUTS], review: { decision: 'Adapt', note: 'x', at: AT } }),
      prep('b', 'Vieira', 'hot', 2, { restrictions: [NUTS], review: { decision: 'Adapt', note: 'x', at: AT } }),
      prep('c', 'Vieira', 'hot', 3, { restrictions: [SHELLFISH], reviewPending: true }),
      prep('d', 'Vieira', 'hot', 4, { restrictions: [GLUTEN], review: { decision: 'Remake', note: 'sin rebozar', at: AT } })])[0]
    expect(dishNote(dish)).toBe('(2 adaptadas · 1 rehecha · 1 por revisar)')
    expect(dishNote(dishes([prep('e', 'Vieira', 'hot', 1)])[0])).toBe('')
    expect(dishText(dishes([prep('e', 'Vieira', 'hot', 1)])[0])).toBe('Vieira × 1')
  })
  it('a paused table is flagged, and a station with nothing of its own sees nothing', () => {
    expect(stationTickets([entry('M7', [course('p1', 'Entrante', 'Fired', AT, [prep('a', 'A', 'hot', 1)])], 'Paused')], 'hot')[0].paused).toBe(true)
    expect(stationTickets(BOARD, 'bar')).toEqual([])
  })
  it('ties keep today\'s order: by table, then by course', () => {
    const board = [entry('M2', [course('b', 'B', 'Fired', AT, [prep('x', 'X', 'hot', 1)]), course('a', 'A', 'Fired', AT, [prep('y', 'Y', 'hot', 1)])]),
      entry('M10', [course('a', 'A', 'Fired', AT, [prep('z', 'Z', 'hot', 1)])]), entry('M1', [course('a', 'A', 'Fired', null, [prep('w', 'W', 'hot', 1)])])]
    expect(stationTickets(board, 'hot').map(key)).toEqual(['M10/a', 'M2/a', 'M2/b', 'M1/a'])   // sin hora de envio, al final
  })
})

describe('the pass sees the whole table', () => {
  const NOW = Date.parse('2026-10-27T20:10:00Z')
  it('the pass, and a kitchen tablet without a station, see the whole table', () => {
    expect([seesWholeTable('pase'), seesWholeTable(null), seesWholeTable('hot')]).toEqual([true, true, false])
  })
  it('one ticket per table and course in the kitchen, oldest first, with every station\'s dishes', () => {
    const tickets = passTickets(BOARD)
    expect(tickets.map(key)).toEqual(['M5/p1', 'M1/segundo', 'M9/aperitivo', 'M4/principal'])
    const m4 = tickets.find(ticket => ticket.entry.service.tableId === 'M4')!
    expect(m4.dishes.map(dish => `${dishText(dish)} [${dish.stationId}] ${dishState(dish)}`)).toEqual(['Merluza á galega × 4 [hot] Por empezar', 'Ensalada × 4 [cold] Por empezar'])
  })
  it('the progress of each station by its full name, counting dishes: «Cocina caliente: lista · Cocina fría: 1 de 2 platos»', () => {
    const board = [entry('M9', [course('aperitivo', 'Aperitivo', 'Preparing', AT, [
      prep('z-1', 'Zamburiñas', 'hot', 1, { state: 'Ready', actions: [] }), prep('o-1', 'Ostras', 'cold', 1, { state: 'Ready', actions: [] }), prep('s-1', 'Salpicón', 'cold', 1)])])]
    const [ticket] = passTickets(board)
    expect(ticket.progress).toEqual([{ stationId: 'hot', ready: 1, total: 1, done: true }, { stationId: 'cold', ready: 1, total: 2, done: false }])
    expect(ticket.progress.map(row => progressText(row, nameOf)).join(' · ')).toBe('Cocina caliente: lista · Cocina fría: 1 de 2 platos')
    expect(progressText({ stationId: 'hot', ready: 0, total: 1, done: false }, nameOf)).toBe('Cocina caliente: 0 de 1 plato')
    expect(missingText(ticket, nameOf)).toBe('Falta Cocina fría')
    expect(ticket.canValidate).toBe(false)
  })
  it('while it cannot validate it says what is missing; the button exists only when the server announces ready', () => {
    const allergy = [entry('M3', [course('p1', 'Principal', 'Preparing', AT, [
      prep('a', 'A', 'hot', 1, { state: 'Ready', actions: ['review-preparation'], restrictions: [SHELLFISH], reviewPending: true }),
      prep('b', 'B', 'cold', 1)])])]
    expect(missingText(passTickets(allergy)[0], nameOf)).toBe('Falta Cocina fría y revisar la alergia')
    const two = [entry('M3', [course('p1', 'Principal', 'Preparing', AT, [
      prep('a', 'A', 'hot', 1, { state: 'Ready', restrictions: [SHELLFISH], reviewPending: true }),
      prep('c', 'C', 'hot', 2, { state: 'Ready', restrictions: [NUTS], reviewPending: true })])])]
    expect(missingText(passTickets(two)[0], nameOf)).toBe('Falta revisar las alergias')
    const ready = [entry('M3', [course('p1', 'Principal', 'Preparing', AT, [prep('a', 'A', 'hot', 1, { state: 'Ready', actions: [] })], { actions: ['ready'] })])]
    const [validating] = passTickets(ready)
    expect([validating.canValidate, missingText(validating, nameOf)]).toEqual([true, ''])
    // Lo que no es obligatorio no falta: el servidor valida sin ello.
    const optional = [entry('M3', [course('p1', 'Principal', 'Preparing', AT, [prep('a', 'A', 'hot', 1, { state: 'Ready' }), prep('b', 'B', 'cold', 1, { mandatory: false })])])]
    expect(missingText(passTickets(optional)[0], nameOf)).toBe('')
  })
  it('the sent work to review stays in its ticket (also once validated), with only what the server lets it review', () => {
    const board = [entry('M3', [course('p1', 'Principal', 'Ready', AT, [
      prep('a', 'A', 'hot', 1, { state: 'Ready', actions: ['review-preparation'], restrictions: [SHELLFISH], reviewPending: true }),
      prep('b', 'A', 'hot', 2, { state: 'Ready', actions: [], restrictions: [SHELLFISH], reviewPending: true })], { readyAt: AT, actions: [] })])]
    const [ticket] = passTickets(board)
    expect([key(ticket), ticket.pendingReviews, ticket.reviews.map(p => p.id)]).toEqual(['M3/p1', 2, ['a']])
    expect(waitingForWaiter(board)).toEqual([])                                             // validado pero por revisar: no se puede servir
  })
  it('«Siguiente: Postre (sin enviar)»: the first unsent course of the table, read only', () => {
    const m4 = passTickets(BOARD).find(ticket => ticket.entry.service.tableId === 'M4')!
    expect(m4.next?.id).toBe('postre')
    expect(nextText(m4.next)).toBe('Siguiente: Postre (sin enviar)')
    expect(passTickets(BOARD).find(ticket => ticket.entry.service.tableId === 'M1')!.next).toBeNull()
    expect(nextText(null)).toBe('')
  })
  it('«Más opciones»: mark a station as ready in ONE command with what the server announces; the pass\'s own work has its «Listo»', () => {
    const board = [entry('M9', [course('aperitivo', 'Aperitivo', 'Preparing', AT, [
      prep('z-1', 'Zamburiñas', 'hot', 1, { state: 'Ready', actions: [] }), prep('o-1', 'Ostras', 'cold', 1), prep('s-1', 'Salpicón', 'cold', 1, { actions: [] }),
      prep('pan-1', 'Pan', 'pase', 1), prep('t-1', 'Tosta', 'bar', 1)])])]
    const [ticket] = passTickets(board)
    expect(ticket.marks).toEqual([{ stationId: 'cold', itemIds: ['o-1'] }, { stationId: 'bar', itemIds: ['t-1'] }])
    expect(ticket.passWork).toEqual(['pan-1'])
    expect(ticket.marks.map(mark => markLabel(mark, nameOf))).toEqual(['Marcar Cocina fría como lista', 'Marcar bar como lista'])
    // Sin la orden de grupo anunciada en el pase (una estacion, un rol de sala), no hay nada que marcar.
    const quiet = passTickets([entry('M9', [{ ...board[0].service.courses[0], actions: [] }])])[0]
    expect([quiet.marks, quiet.passWork]).toEqual([[], []])
  })
  it('«Esperando al camarero»: validated and not served, oldest first, with the time since it was validated', () => {
    const board = [
      entry('M10', [course('p1', 'Segundos', 'Ready', AT, [prep('a', 'A', 'hot', 1, { state: 'Ready' })], { readyAt: '2026-10-27T20:08:00Z', actions: [] })]),
      entry('M2', [course('p1', 'Entrante', 'Ready', AT, [prep('b', 'B', 'hot', 1, { state: 'Ready' })], { readyAt: '2026-10-27T20:01:00Z', actions: [] }),
        course('p2', 'Segundo', 'Served', AT, [], { readyAt: '2026-10-27T19:00:00Z', actions: [] })]),
      entry('M4', [course('p1', 'Postre', 'Ready', AT, [prep('c', 'C', 'hot', 1, { state: 'Ready' })], { readyAt: null, actions: [] })]),
      entry('M7', [course('p1', 'Postre', 'Ready', AT, [prep('d', 'D', 'hot', 1, { state: 'Ready' })], { readyAt: AT, actions: [] })], 'Completed'),
    ]
    expect(waitingForWaiter(board).map(key)).toEqual(['M2/p1', 'M10/p1', 'M4/p1'])
    expect(passTickets(board)).toEqual([])                                                 // lo validado ya no es una comanda
    expect(readySince('2026-10-27T20:07:00Z', NOW)).toBe('listo hace 3 min')
    expect(readySince('2026-10-27T20:09:40Z', NOW)).toBe('listo hace menos de 1 min')
    expect(readySince('2026-10-27T20:15:00Z', NOW)).toBe('listo hace menos de 1 min')       // reloj de la tablet atrasado: nunca negativo
    expect(readySince('2026-10-27T18:55:00Z', NOW)).toBe('listo hace 1 h 15 min')
    expect(readySince(null, NOW)).toBe('')                                                 // sin hora del servidor, no se inventa
  })
})

describe('time: neutral, amber from 15 minutes, dark amber from 25; never red and always written', () => {
  const NOW = Date.parse('2026-10-27T20:30:00Z')
  const fired = (minutes: number, seconds = 0) => new Date(NOW - minutes * 60_000 - seconds * 1000).toISOString()
  it('the promoter\'s two thresholds', () => {
    expect([AMBER_MINUTES, DARK_AMBER_MINUTES]).toEqual([15, 25])
  })
  it.each([
    [0, 20, 'neutral', 'Lleva menos de 1 min'], [12, 0, 'neutral', 'Lleva 12 min'], [14, 59, 'neutral', 'Lleva 14 min'], [15, 0, 'amber', 'Lleva 15 min'],
    [24, 59, 'amber', 'Lleva 24 min'], [25, 0, 'dark-amber', 'Lleva 25 min'], [65, 0, 'dark-amber', 'Lleva 1 h 05 min'],
  ] as const)('%i min %i s: %s, «%s»', (minutes, seconds, tone, text) => {
    expect(timeTone(fired(minutes, seconds), NOW)).toBe(tone)
    expect(elapsedText(fired(minutes, seconds), NOW)).toBe(text)
  })
  it('a tablet clock behind the server never paints a negative time, and an unknown time is said, not invented', () => {
    expect([timeTone('2026-10-27T20:45:00Z', NOW), elapsedText('2026-10-27T20:45:00Z', NOW)]).toEqual(['neutral', 'Lleva menos de 1 min'])
    expect([timeTone(null, NOW), elapsedText(null, NOW)]).toEqual(['neutral', 'Hora de envío desconocida'])
    expect([timeTone('no es una hora', NOW), elapsedText('no es una hora', NOW)]).toEqual(['neutral', 'Hora de envío desconocida'])
  })
  it('no tone is red: red stays for allergies', () => {
    const tones = new Set([0, 10, 15, 20, 25, 90, 600].map(minutes => timeTone(fired(minutes), NOW)))
    expect([...tones].sort()).toEqual(['amber', 'dark-amber', 'neutral'])
  })
})

describe('a new ticket rings (after «Empezar turno»)', () => {
  it('only the keys new since the previous good read; the first read rings nothing', () => {
    const watch = new TicketWatch()
    expect(watch.read(['s-M1:p1', 's-M4:p2'])).toEqual([])                     // lo que ya estaba al encender no suena
    expect(watch.read(['s-M1:p1', 's-M4:p2'])).toEqual([])
    expect(watch.read(['s-M1:p1', 's-M4:p2', 's-M9:p1'])).toEqual(['s-M9:p1'])  // comanda nueva
    expect(watch.read(['s-M4:p2', 's-M9:p1'])).toEqual([])                      // una que se va no suena
    expect(watch.read(['s-M4:p2', 's-M9:p1', 's-M1:p1'])).toEqual(['s-M1:p1'])  // vuelve a la rejilla (un «Rehacer» del pase): suena
  })
  it('the keys come from the tickets in the grid: a station\'s ticket that goes back from «Hecho» rings again', () => {
    const watch = new TicketWatch()
    const grid = (board: BoardEntry[]) => stationTickets(board, 'hot').filter(ticket => !ticket.done).map(ticket => ticket.key)
    const done = [entry('M9', [course('p1', 'Aperitivo', 'Preparing', AT, [prep('z', 'Z', 'hot', 1, { state: 'Ready' }), prep('o', 'O', 'cold', 1)])])]
    const remade = [entry('M9', [course('p1', 'Aperitivo', 'Preparing', AT, [prep('z', 'Z', 'hot', 1, { state: 'Fired' }), prep('o', 'O', 'cold', 1)])])]
    expect(watch.read(grid(done))).toEqual([])
    expect(watch.read(grid(remade))).toEqual(['s-M9:p1'])
  })
  it('it sounds only with something new and the shift started (the browser lets it play only after a touch)', () => {
    expect([shouldChime(['k'], true), shouldChime(['k'], false), shouldChime([], true)]).toEqual([true, false, false])
  })
})

// E4e (ADR-016): «Por revisar antes de enviar», del pase. Se mueve tal cual de kitchen.ts: solo se lista lo que el servidor marca por
// revisar en pases SIN ENVIAR y le anuncia revisar a este dispositivo; «No le afecta a nada más» es UNA orden con lo que queda del comensal.
describe('reviews before sending (the pass)', () => {
  const pending = (id: string, guestPosition: number | null, station = 'hot', announced = true): Preparation =>
    prep(id, id, station, guestPosition, { state: 'Pending', reviewPending: true, restrictions: [SHELLFISH], actions: announced ? ['review-preparation'] : [] })
  const unsent = (id: string, preparations: Preparation[], actions = ['skip', 'review-preparations']): Course =>
    course(id, id, 'Pending', null, preparations, { actions })
  const board: BoardEntry[] = [
    entry('M10', [unsent('p1', [pending('ostra-2', 2, 'cold'), prep('ostra-1', 'ostra-1', 'cold', 1, { state: 'Pending', actions: [] })])], 'Open'),
    entry('M2', [
      course('p1', 'p1', 'Fired', AT, [prep('frio-2', 'frio-2', 'cold', 2, { reviewPending: true, actions: ['review-preparation'] })], { actions: ['review-preparations'] }),
      unsent('p2', [pending('principal-2', 2), pending('principal-1', 1), pending('compartir', null), pending('ajena', 3, 'hot', false)]),
      unsent('p3', [pending('postre-2', 2, 'cold')]),
    ]),
  ]
  it('lists only UNSENT courses, per table (M2 before M10) and guest (the whole table first), what the server announces', () => {
    const guests = presendReviews(board)
    expect(guests.map(guest => `${guest.entry.service.tableId}/${guest.guestPosition ?? 'all'}: ${guest.items.map(item => `${item.course.id}.${item.preparation.id}`).join(',')}`))
      .toEqual(['M2/all: p2.compartir', 'M2/1: p2.principal-1', 'M2/2: p2.principal-2,p3.postre-2', 'M10/2: p1.ostra-2'])
    expect(guests.flatMap(guest => guest.items.map(item => item.preparation.id))).not.toContain('frio-2')
    expect(guests.flatMap(guest => guest.items.map(item => item.preparation.id))).not.toContain('ajena')
    expect(guests[2].restrictions).toEqual([SHELLFISH])
  })
  it('a station is announced nothing, so it lists nothing', () => {
    const station = board.map(row => ({ ...row, service: { ...row.service, courses: row.service.courses.map(current =>
      ({ ...current, actions: [], preparations: current.preparations.map(p => ({ ...p, actions: [] })) })) } }))
    expect(presendReviews(station)).toEqual([])
  })
  it('«No le afecta a nada más» is ONE review-preparations with every pending dish of that guest, the read version and the note', () => {
    const guest = presendReviews(board).find(row => row.entry.service.tableId === 'M2' && row.guestPosition === 2)!
    expect(restBody(guest, '  no lleva marisco ')).toEqual({ expectedVersion: 7, items: [
      { courseId: 'p2', itemId: 'principal-2', decision: 'unaffected', note: 'no lleva marisco' },
      { courseId: 'p3', itemId: 'postre-2', decision: 'unaffected', note: 'no lleva marisco' }] })
  })
  it('the group carries only courses that announce review-preparations, and at most the server limit', () => {
    const quiet = [entry('M5', [unsent('p1', [pending('a', 2)], ['skip']), unsent('p2', [pending('b', 2)])])]
    expect(restOfGuest(presendReviews(quiet)[0]).map(item => item.preparation.id)).toEqual(['b'])
    const many = [entry('M6', [unsent('p1', Array.from({ length: MAX_GROUP_REVIEWS + 5 }, (_, index) => pending(`plato-${index}`, 1)))])]
    expect(restOfGuest(presendReviews(many)[0])).toHaveLength(MAX_GROUP_REVIEWS)
    expect(MAX_GROUP_REVIEWS).toBe(60)
  })
  it('before sending, only «No le afecta» and «Adaptado»: remaking is for what already left; a note is mandatory', () => {
    expect(PRESEND_DECISIONS.map(option => option.decision)).toEqual(['unaffected', 'adapt'])
    expect([reviewNoteOk(''), reviewNoteOk('  a '), reviewNoteOk('sin nata')]).toEqual([false, false, true])
  })
})

describe('cocina.ts itself', () => {
  const source = readFileSync(fileURLToPath(new URL('../src/cocina.ts', import.meta.url))).toString('utf8')
  const code = source.replace(/^\s*\/\/.*$/gm, '')
  const literals = [...code.matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])
  it('has no DOM: it runs in Node and is tested without a browser', () => {
    expect(code).not.toMatch(/\b(window|document|navigator|localStorage|indexedDB|AudioContext)\b/)
  })
  it('no checkout word in what it writes, not even inside another word (kitchen.spec rule)', () => {
    expect(literals.length).toBeGreaterThan(10)
    expect(literals.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
  })
  it('Spanish with accents', () => {
    const unaccented = ['estacion', 'envio', 'revision', 'tambien', 'preparacion', 'mas']
    expect(literals.flatMap(text => unaccented.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)))).toEqual([])
  })
  it('never «Sala avisada»: the server does not know whether the notice reached a phone', () => {
    expect(literals.filter(text => /avisad/i.test(text))).toEqual([])
  })
})
