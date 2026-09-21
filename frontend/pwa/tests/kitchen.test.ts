import { describe, expect, it } from 'vitest'
import { elapsedLabel, kitchenTickets, reviewNoteOk } from '../src/kitchen'
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

describe('kitchen labels and input format', () => {
  const now = Date.parse('2026-09-21T12:30:00Z')
  it('always writes the time since the course was fired, and never a negative one', () => {
    expect(elapsedLabel('2026-09-21T12:23:10Z', now)).toBe('enviado hace 6 min')
    expect(elapsedLabel('2026-09-21T12:29:40Z', now)).toBe('enviado hace menos de 1 min')
    expect(elapsedLabel('2026-09-21T12:45:00Z', now)).toBe('enviado hace menos de 1 min')   // reloj de la tablet atrasado
    expect(elapsedLabel(null, now)).toBe('hora de envio desconocida')
  })
  it('a review note is mandatory', () => {
    expect(reviewNoteOk('')).toBe(false); expect(reviewNoteOk('  a ')).toBe(false); expect(reviewNoteOk('sin nata')).toBe(true)
  })
})
