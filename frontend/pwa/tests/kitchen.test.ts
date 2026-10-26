import { describe, expect, it } from 'vitest'
import { elapsedLabel, kitchenTickets, MAX_GROUP_REVIEWS, PRESEND_DECISIONS, presendReviews, restBody, restOfGuest, reviewNoteOk } from '../src/kitchen'
import type { BoardEntry, Course, Preparation } from '../src/types'

const prep = (id: string, stationId: string, extra: Partial<Preparation> = {}): Preparation =>
  ({ id, name: id, stationId, quantity: 1, guestPosition: null, mandatory: true, state: 'Fired', ...extra })
const course = (id: string, state: Course['state'], firedAt: string | null, preparations: Preparation[]): Course =>
  ({ id, name: id, state, firedAt, readyAt: null, servedAt: null, skipReason: null, preparations })
const entry = (tableId: string, courses: Course[], state: BoardEntry['service']['state'] = 'InService'): BoardEntry => ({
  version: 3, occupancyVersion: 1,
  service: { id: `s-${tableId}`, tableId, pax: 2, state, courses },
  occupancy: { id: `o-${tableId}`, tableId, serviceId: `s-${tableId}`, state: 'Occupied', releasedAt: null },
})

const BOARD: BoardEntry[] = [
  entry('M2', [
    course('p1', 'Served', '2026-09-21T12:00:00Z', [prep('frio', 'cold'), prep('caliente', 'hot')]),
    course('p2', 'Preparing', '2026-09-21T12:20:00Z', [prep('principal', 'hot', { state: 'Preparing' })]),
    course('p3', 'Pending', null, [prep('postre', 'cold', { state: 'Pending' })]),
  ]),
  entry('M1', [course('p1', 'Fired', '2026-09-21T12:10:00Z', [prep('frio', 'cold'), prep('caliente', 'hot', { reviewPending: true })])]),
  entry('M3', [course('p1', 'Ready', '2026-09-21T12:05:00Z', [prep('frio', 'cold', { state: 'Ready' }), prep('caliente', 'hot', { state: 'Ready' })])], 'Paused'),
  entry('M4', [course('p1', 'Fired', '2026-09-21T11:00:00Z', [prep('frio', 'cold')])], 'Cancelled'),
]
const names = (station: string | null) => kitchenTickets(BOARD, station).map(ticket => `${ticket.entry.service.tableId}/${ticket.course.id}`)

describe('kitchen tickets', () => {
  it('a station sees fired or preparing courses that contain its work, oldest first, never pending, served or cancelled ones', () => {
    expect(names('cold')).toEqual(['M1/p1'])
    expect(names('hot')).toEqual(['M1/p1', 'M2/p2'])
  })
  it('shows the whole course to a station, with the work of other stations marked as not its own', () => {
    const [ticket] = kitchenTickets(BOARD, 'cold')
    expect(ticket.lines.map(line => [line.preparation.id, line.own])).toEqual([['frio', true], ['caliente', false]])
  })
  it('a course with nothing for this station is not painted', () => {
    expect(names('cold')).not.toContain('M2/p2')
  })
  it('the pass also sees READY courses until they are served, everything as its own, and counts pending reviews', () => {
    expect(names('pase')).toEqual(['M3/p1', 'M1/p1', 'M2/p2'])
    const tickets = kitchenTickets(BOARD, 'pase')
    expect(tickets.every(ticket => ticket.lines.every(line => line.own))).toBe(true)
    expect(tickets.find(ticket => ticket.entry.service.tableId === 'M1')!.pendingReviews).toBe(1)
    expect(tickets.find(ticket => ticket.entry.service.tableId === 'M3')!.paused).toBe(true)
  })
  it('a kitchen device without a station mirrors whatever the server announces: it sees everything', () => {
    expect(names(null)).toEqual(names('pase'))
  })
  it('never derives permission: affordances travel untouched and a line of another station keeps whatever the server said', () => {
    const board = [entry('M5', [course('p1', 'Fired', '2026-09-21T12:00:00Z', [prep('frio', 'cold', { actions: ['preparation-start'] }), prep('caliente', 'hot', { actions: [] })])])]
    const [ticket] = kitchenTickets(board, 'cold')
    expect(ticket.lines[0].preparation.actions).toEqual(['preparation-start'])
    expect(ticket.lines[1].preparation.actions).toEqual([])
  })
})

// E4e (ADR-016): «Por revisar antes de enviar», lo minimo del pase hasta C3d. Solo se lista lo que el servidor marca por revisar en
// pases SIN ENVIAR y le anuncia revisar a este dispositivo; «No le afecta a nada más» es UNA orden con lo que queda del comensal.
describe('reviews before sending (the pass)', () => {
  const shellfish = { id: 'r-marisco', guestPosition: 2, kind: 'Allergy' as const, substance: 'marisco', severity: 'Severe' as const }
  const pending = (id: string, guestPosition: number | null, station = 'hot', announced = true): Preparation =>
    prep(id, station, { state: 'Pending', guestPosition, reviewPending: true, restrictions: [shellfish], actions: announced ? ['review-preparation'] : [] })
  const unsent = (id: string, preparations: Preparation[], actions = ['skip', 'review-preparations']): Course =>
    ({ ...course(id, 'Pending', null, preparations), actions })
  const board: BoardEntry[] = [
    entry('M10', [unsent('p1', [pending('ostra-2', 2, 'cold'), prep('ostra-1', 'cold', { state: 'Pending', guestPosition: 1 })])], 'Open'),
    entry('M2', [
      { ...course('p1', 'Fired', '2026-09-21T12:00:00Z', [prep('frio-2', 'cold', { guestPosition: 2, reviewPending: true, actions: ['review-preparation'] })]), actions: ['review-preparations'] },
      unsent('p2', [pending('principal-2', 2), pending('principal-1', 1), pending('compartir', null), pending('ajena', 3, 'hot', false)]),
      unsent('p3', [pending('postre-2', 2, 'cold')]),
    ]),
  ]
  it('lists only UNSENT courses, per table (M2 before M10) and guest (the whole table first), what the server announces', () => {
    const guests = presendReviews(board)
    expect(guests.map(guest => `${guest.entry.service.tableId}/${guest.guestPosition ?? 'all'}: ${guest.items.map(item => `${item.course.id}.${item.preparation.id}`).join(',')}`))
      .toEqual(['M2/all: p2.compartir', 'M2/1: p2.principal-1', 'M2/2: p2.principal-2,p3.postre-2', 'M10/2: p1.ostra-2'])
    // Lo enviado (frio-2, en cocina) sigue en su comanda; lo no anunciado (otra estacion, sala) no sale.
    expect(guests.flatMap(guest => guest.items.map(item => item.preparation.id))).not.toContain('frio-2')
    expect(guests.flatMap(guest => guest.items.map(item => item.preparation.id))).not.toContain('ajena')
    expect(guests[2].restrictions).toEqual([shellfish])                                    // escrita una vez por comensal
  })
  it('a station is announced nothing, so it lists nothing', () => {
    const station = board.map(row => ({ ...row, service: { ...row.service, courses: row.service.courses.map(current =>
      ({ ...current, actions: [], preparations: current.preparations.map(p => ({ ...p, actions: [] })) })) } }))
    expect(presendReviews(station)).toEqual([])
  })
  it('«No le afecta a nada más» is ONE review-preparations with every pending dish of that guest, the read version and the note', () => {
    const guest = presendReviews(board).find(row => row.entry.service.tableId === 'M2' && row.guestPosition === 2)!
    expect(restBody(guest, '  no lleva marisco ')).toEqual({ expectedVersion: 3, items: [
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
  it('before sending, only «No le afecta» and «Adaptado»: remaking is for what already left', () => {
    expect(PRESEND_DECISIONS.map(option => option.decision)).toEqual(['unaffected', 'adapt'])
  })
})

describe('kitchen labels and input format', () => {
  const now = Date.parse('2026-09-21T12:30:00Z')
  it('always writes the time since the course was fired, and never a negative one', () => {
    expect(elapsedLabel('2026-09-21T12:23:10Z', now)).toBe('enviado hace 6 min')
    expect(elapsedLabel('2026-09-21T12:29:40Z', now)).toBe('enviado hace menos de 1 min')
    expect(elapsedLabel('2026-09-21T12:45:00Z', now)).toBe('enviado hace menos de 1 min')   // reloj de la tablet atrasado
    expect(elapsedLabel(null, now)).toBe('hora de envío desconocida')
  })
  it('a review note is mandatory', () => {
    expect(reviewNoteOk('')).toBe(false); expect(reviewNoteOk('  a ')).toBe(false); expect(reviewNoteOk('sin nata')).toBe(true)
  })
})
