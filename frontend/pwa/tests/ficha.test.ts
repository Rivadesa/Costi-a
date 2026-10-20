import { describe, expect, it } from 'vitest'
import { commandFor, coursesTitle, dishesHint, PENDING_REVIEW_TEXT, primaryAction, REASON_PROBLEM, reasonLabel, reasonOk, tableActions, tableInfo, waitingText } from '../src/ficha'
import { clockLabel } from '../src/labels'
import type { BoardEntry, Course, Dining } from '../src/types'

// C3a: la ficha de mesa del comandero. Los botones salen SOLO de lo que anuncia el servidor; cual va en grande sigue el orden del
// Comedor del puesto (servir, validar, enviar el siguiente pase, empezar, reanudar, terminar, liberar) y cada orden lleva la version
// leida de ESA mesa (F01) y su nombre, nunca su codigo.
const course = (id: string, name: string, state: Course['state'], actions: string[] = [], extra: Partial<Course> = {}): Course =>
  ({ id, name, state, firedAt: null, readyAt: null, servedAt: null, skipReason: null, preparations: [], actions, ...extra })
const dining = (actions: string[], courses: Course[], extra: Partial<Dining> = {}): Dining =>
  ({ id: 'svc/1', tableId: 'M7', pax: 4, state: 'InService', offerId: 'DIA', courses, actions, ...extra })
const entry = (service: Dining, occupancy: string[] = []): BoardEntry =>
  ({ version: 12, service, occupancy: { id: 'o1', tableId: service.tableId, serviceId: service.id, state: 'Occupied', releasedAt: null, actions: occupancy }, occupancyVersion: 5 })
const ids = (row: BoardEntry) => tableActions(row).map(action => action.testId)

describe('announced actions, in the order of the main PC', () => {
  it('nothing announced, nothing shown: no button is deduced by the client', () => {
    const row = entry(dining([], [course('c1', 'Entrantes', 'Ready'), course('c2', 'Principales', 'Pending')]))
    expect(tableActions(row)).toEqual([])
    expect(primaryAction(tableActions(row))).toBeNull()
  })
  it('the big button is the first announced of serve, ready, fire-next, start, resume, complete, release', () => {
    const all = entry(dining(['start', 'fire-next', 'pause', 'resume', 'complete', 'cancel-unstarted'],
      [course('c1', 'Entrantes', 'Ready', ['serve', 'ready', 'skip']), course('c2', 'Principales', 'Pending', ['skip'])]), ['release'])
    expect(ids(all)).toEqual(['do-serve-c1', 'do-ready-c1', 'do-fire-next', 'do-start', 'do-resume', 'do-pause', 'do-skip-c1', 'do-skip-c2', 'do-complete', 'do-cancel-unstarted', 'do-release'])
    const order: string[] = []
    let row = all
    for (;;) {
      const primary = primaryAction(tableActions(row))
      if (!primary) break
      order.push(primary.action)
      // Sin esa accion anunciada, manda la siguiente de la lista.
      row = entry({ ...row.service, actions: row.service.actions?.filter(action => action !== primary.action),
        courses: row.service.courses.map(current => ({ ...current, actions: current.actions?.filter(action => action !== primary.action) })) },
        row.occupancy.actions?.filter(action => action !== primary.action))
    }
    expect(order).toEqual(['serve', 'ready', 'fire-next', 'start', 'resume', 'complete', 'release'])
  })
  it('pause, skip and cancelling never go in the big button: they stay in «Más opciones»', () => {
    const row = entry(dining(['pause', 'cancel-unstarted'], [course('c1', 'Entrantes', 'Pending', ['skip'])]))
    expect(primaryAction(tableActions(row))).toBeNull()
    expect(tableActions(row).map(action => [action.action, action.needsReason, action.caution])).toEqual([['pause', true, false], ['skip', true, false], ['cancel-unstarted', true, true]])
  })
  it('serve on two courses: the first one in the big button, the other one in «Más opciones»', () => {
    const actions = tableActions(entry(dining([], [course('c1', 'Entrantes', 'Ready', ['serve']), course('c2', 'Principales', 'Ready', ['serve'])])))
    const primary = primaryAction(actions)
    expect(primary?.testId).toBe('do-serve-c1')
    expect(actions.filter(action => action !== primary).map(action => action.testId)).toEqual(['do-serve-c2'])
  })
  it('release is an action of the occupancy, not of the service', () => {
    expect(ids(entry(dining(['release'], [])))).toEqual([])
    expect(ids(entry(dining([], []), ['release']))).toEqual(['do-release'])
  })
})

describe('words of the buttons', () => {
  it('«Enviar siguiente pase» names the next course, like the approved mockup', () => {
    const row = entry(dining(['fire-next'], [course('c1', 'Entrantes', 'Served'), course('c2', 'Postre', 'Pending'), course('c3', 'Café', 'Pending')]))
    expect(primaryAction(tableActions(row))?.label).toBe('Enviar siguiente pase · Postre')
  })
  it('a course action names its course; the others use the words of the main PC', () => {
    const row = entry(dining(['start', 'pause'], [course('c1', 'Entrantes', 'Ready', ['serve', 'skip'])]), ['release'])
    expect(tableActions(row).map(action => action.label)).toEqual(['Marcar servido · Entrantes', 'Iniciar servicio', 'Pausar servicio', 'Omitir pase · Entrantes', 'Liberar mesa'])
  })
  it('without a big action the button says what it waits for', () => {
    expect(waitingText(dining([], [course('c1', 'Entrantes', 'Fired')], { restrictionsPendingAck: true }))).toBe('Esperando la revisión de cocina')
    expect(waitingText(dining([], [course('c1', 'Entrantes', 'Preparing')]))).toBe('Esperando a cocina')
    expect(waitingText(dining([], [course('c1', 'Entrantes', 'Pending')], { state: 'Paused' }))).toBe('Sin acciones para esta mesa ahora')
  })
})

describe('the command of a button (F01)', () => {
  it('carries the version READ for this table, the course and the table NAME in its description', () => {
    const row = entry(dining(['fire-next', 'pause'], [course('c1', 'Entrantes', 'Ready', ['serve'])]))
    const [serve, fire, pause] = tableActions(row)
    expect(commandFor(row, serve, 'Mesa 7')).toEqual({ path: '/dining/services/svc%2F1/commands/serve', body: { expectedVersion: 12, courseId: 'c1' }, description: 'Marcar servido · Entrantes · Mesa 7' })
    expect(commandFor(row, fire, 'Mesa 7')).toEqual({ path: '/dining/services/svc%2F1/commands/fire-next', body: { expectedVersion: 12 }, description: 'Enviar siguiente pase a cocina · Mesa 7' })
    expect(commandFor(row, pause, 'Mesa 7', '  salen un momento ')).toEqual({ path: '/dining/services/svc%2F1/commands/pause', body: { expectedVersion: 12, reason: 'salen un momento' }, description: 'Pausar servicio · Mesa 7' })
  })
  it('release goes to the occupancy, with ITS version and the reason', () => {
    const row = entry(dining([], []), ['release'])
    expect(commandFor(row, tableActions(row)[0], 'Mesa 7', 'se van')).toEqual({ path: '/dining/occupancy/svc%2F1/release', body: { expectedVersion: 5, reason: 'se van' }, description: 'Liberar mesa · Mesa 7' })
  })
  it('the reason field names only what is announced and needs it (a waiter never reads «liberar» if it cannot release)', () => {
    expect(reasonLabel(['fire-next', 'pause', 'skip'])).toBe('Motivo (para pausar u omitir)')
    expect(reasonLabel(['release'])).toBe('Motivo (para liberar)')
    expect(reasonLabel(['pause', 'skip', 'cancel-unstarted', 'release', 'remove-restriction'])).toBe('Motivo (para pausar, omitir, cancelar, liberar o retirar)')
    expect(reasonLabel(['start', 'serve'])).toBe('Motivo')
  })
  it('the only local check is the format of the reason', () => {
    expect(reasonOk('  ab ')).toBe(false); expect(reasonOk('abc')).toBe(true)
    expect(REASON_PROBLEM).toContain('3')
  })
})

describe('header and short courses', () => {
  const at = (minutes: number) => new Date(Date.parse('2026-10-20T12:00:00Z') + minutes * 60_000).toISOString()
  const now = Date.parse(at(25))
  it('people and, when the read says so, since when (never invented)', () => {
    expect(tableInfo(dining([], [course('c1', 'Entrantes', 'Fired', [], { firedAt: at(0) })]), now)).toBe(`4 personas · en cocina desde las ${clockLabel(at(0))} (25 min)`)
    expect(tableInfo(dining([], [course('c1', 'Entrantes', 'Ready', [], { firedAt: at(0), readyAt: at(20) })]), now)).toBe(`4 personas · listo para servir desde las ${clockLabel(at(20))} (5 min)`)
    expect(tableInfo(dining([], [course('c1', 'Entrantes', 'Served', [], { servedAt: at(10) }), course('c2', 'Postre', 'Pending')]), now)).toBe(`4 personas · último pase servido a las ${clockLabel(at(10))} (15 min)`)
    expect(tableInfo(dining([], [course('c1', 'Entrantes', 'Pending')], { state: 'Open', pax: 1 }), now)).toBe('1 persona')
  })
  it('the «Carta y menús» entry says what there is to do in it, or how the table goes', () => {
    expect(dishesHint(dining([], [course('c1', 'Primeros', 'Pending', ['choose'])]), 'Menú del día')).toBe('Menú del día · elegir plato en Primeros')
    expect(dishesHint(dining([], [course('c1', 'Entrantes', 'Pending', ['add-dish'])]), '')).toBe('pedir en Entrantes')
    expect(dishesHint(dining([], [course('c1', 'Entrantes', 'Served'), course('c2', 'Postre', 'Pending'), course('c3', 'Café', 'Skipped')]), 'Menú del día')).toBe('Menú del día · 2 de 3 pases hechos')
    expect(coursesTitle(dining([], [course('c1', 'Entrantes', 'Served'), course('c2', 'Postre', 'Pending')]))).toBe('Pases · 1 de 2 hechos')
  })
  it('no checkout word in any text of the table card, not even inside another word', () => {
    const texts = [PENDING_REVIEW_TEXT, REASON_PROBLEM, waitingText(dining([], [], { restrictionsPendingAck: true })), waitingText(dining([], [course('c', 'x', 'Fired')])), waitingText(dining([], [])),
      ...tableActions(entry(dining(['start', 'fire-next', 'pause', 'resume', 'complete', 'cancel-unstarted'], [course('c1', 'x', 'Ready', ['serve', 'ready', 'skip'])]), ['release'])).map(action => action.label)]
    expect(texts.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
  })
})
