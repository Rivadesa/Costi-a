import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { lineRestrictions } from '../src/cocina'
import { clockLabel, restrictionKindLabel, severityLabel } from '../src/labels'
import { addDishRequest, allergyList, chooseRequest, courseNote, exceptionText, lineTone, MAX_DISH_QUANTITY, NEXT_MARK, passLines, REVIEWED_TEXT, timeline } from '../src/pases'
import type { BoardEntry, Course, Dining, GuestRestriction, Preparation } from '../src/types'

// C3e (plan 2026-10-27, paso 2): los pases de la ficha de Costina Sala, sin DOM. Todo es presentacion de la lectura autoritativa del
// tablero: los platos se agrupan y se apartan con la regla COMUN de cocina.ts (la misma que Costina Cocina y el PC), las excepciones
// salen bajo su plato y nunca atenuadas, y ningun boton ni orden sale de aqui si el servidor no anuncia su accion.

// ---- La tabla COMUN con Costina Cocina y el PC (cocina.test.ts y ClientChecks recorren la misma): romper un caso rompe las tres ----
type Row = { case: string; preparations: Preparation[]; dishes: Array<Record<string, unknown> & { exceptions: object[] }> }
const cases = JSON.parse(readFileSync(fileURLToPath(new URL('../../../dotnet/tests/fixtures/kitchen-cases.json', import.meta.url))).toString('utf8')) as { groups: Row[] }
// Como Only de ClientChecks y cocina.test.ts: un apartado o un campo que esta prueba no conoce es un fallo.
function only(row: object, fields: string[], where: string): void {
  expect(Object.keys(row).filter(field => !fields.includes(field)), `unknown field in the shared cases (${where})`).toEqual([])
}
const restrictionText = (restriction: GuestRestriction) => `${restrictionKindLabel(restriction.kind)} ${restriction.substance} (${severityLabel(restriction.severity)})`

describe('the dishes of the table card: the case table shared with Costina Cocina and the main PC (DishGroups.cs)', () => {
  it('every section of the table is known here', () => { only(cases, ['about', 'groups'], 'sections') })
  it.each(cases.groups.map(row => [row.case, row] as const))('%s', (_, row) => {
    only(row, ['case', 'preparations', 'dishes'], 'groups')
    for (const dish of row.dishes) {
      only(dish, ['name', 'station', 'quantity', 'preparations', 'exceptions', 'state', 'ready'], 'dishes')
      for (const exception of dish.exceptions) only(exception, ['id', 'restrictions'], 'exceptions')
    }
    // El pase sin terminar de la ficha, con las elaboraciones tal como llegan.
    const service: Dining = { id: 's', tableId: 'M1', pax: 4, state: 'InService', courses: [
      { id: 'p1', name: 'Principal', state: 'Fired', firedAt: null, readyAt: null, servedAt: null, skipReason: null, preparations: row.preparations }] }
    const [course] = timeline(service, id => id).courses
    expect(course.dishes.map(line => ({
      name: line.dish.name, station: line.dish.stationId, quantity: line.dish.quantity, preparations: line.dish.preparations.map(p => p.id),
      exceptions: line.dish.exceptions.map(p => ({ id: p.id, restrictions: lineRestrictions(p).map(r => r.id) })), state: line.state, ready: line.dish.ready,
    }))).toEqual(row.dishes)
    // Cada excepcion tiene su linea escrita, con la palabra del camarero; las de toda la mesa nunca se repiten en ella.
    for (const line of course.dishes) {
      expect(line.exceptions.map(exception => exception.preparation)).toEqual(line.dish.exceptions)
      for (const exception of line.exceptions) {
        const whole = (exception.preparation.restrictions ?? []).filter(r => r.guestPosition === null)
        expect(whole.filter(r => !lineRestrictions(exception.preparation).some(own => restrictionText(own) === restrictionText(r)))
          .filter(r => exception.text.includes(restrictionText(r)))).toEqual([])
        expect(exception.text).not.toContain('Por revisar en el pase')
        if (exception.preparation.reviewPending) expect(exception.text.endsWith('Por revisar en cocina')).toBe(true)
      }
    }
  })
})

// ---- Datos de prueba (la Mesa 4 de la maqueta) ----
const NUTS: GuestRestriction = { id: 'r-frutos', guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' }
const LACTOSE: GuestRestriction = { id: 'r-lactosa', guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' }
const GLUTEN: GuestRestriction = { id: 'r-gluten', guestPosition: null, kind: 'Allergy', substance: 'gluten', severity: 'Severe' }
const AT = '2026-10-27T19:00:00Z'
const at = (hour: string) => `2026-10-27T${hour}:00Z`
const prep = (id: string, name: string, stationId: string, guestPosition: number | null, extra: Partial<Preparation> = {}): Preparation =>
  ({ id, name, stationId, quantity: 1, guestPosition, mandatory: true, state: 'Pending', actions: [], ...extra })
const course = (id: string, name: string, state: Course['state'], preparations: Preparation[], extra: Partial<Course> = {}): Course =>
  ({ id, name, state, firedAt: null, readyAt: null, servedAt: null, skipReason: null, preparations, actions: [], ...extra })
const four = (id: string, name: string, stationId: string, extra: (guest: number) => Partial<Preparation> = () => ({})) =>
  [1, 2, 3, 4].map(guest => prep(`${id}-${guest}`, name, stationId, guest, extra(guest)))
const dining = (courses: Course[], actions: string[] = [], extra: Partial<Dining> = {}): Dining =>
  ({ id: 'svc/4', tableId: 'M4', pax: 4, state: 'InService', offerId: 'DIA', courses, actions, restrictions: [NUTS, LACTOSE], ...extra })
const entry = (service: Dining): BoardEntry =>
  ({ version: 21, service, occupancy: { id: 'o4', tableId: 'M4', serviceId: service.id, state: 'Occupied', releasedAt: null }, occupancyVersion: 3 })
const STATIONS: Record<string, string> = { hot: 'Cocina caliente', cold: 'Cocina fría', pase: 'Pase' }
const nameOf = (id: string) => STATIONS[id] ?? id

// Guest 2 con la alergia a frutos secos (adaptada por cocina) y guest 3 con la intolerancia leve a la lactosa (no le afecta).
const reviewed = (guest: number): Partial<Preparation> => guest === 2
  ? { restrictions: [NUTS], review: { decision: 'Adapt', note: 'sin almendra', at: AT } }
  : guest === 3 ? { restrictions: [LACTOSE], review: { decision: 'Unaffected', note: 'no lleva lactosa', at: AT } } : {}
const MESA4 = dining([
  course('aperitivos', 'Aperitivos', 'Served', four('ostra', 'Ostra', 'cold', guest => ({ state: 'Ready', ...reviewed(guest) })), { servedAt: at('20:41') }),
  course('primero', 'Primero', 'Skipped', four('caldo', 'Caldo', 'hot'), { skipReason: 'no lo quieren' }),
  course('principal', 'Principal', 'Pending', [...four('merluza', 'Merluza á galega', 'hot', reviewed), ...four('ensalada', 'Ensalada', 'cold')]),
  course('postre', 'Postre', 'Pending', four('tarta', 'Tarta de Santiago', 'cold', guest => guest === 2 ? { restrictions: [NUTS], reviewPending: true } : {})),
], ['fire-next', 'pause', 'declare-restriction'])

describe('served and skipped courses: gathered in one line', () => {
  it('«✓ Aperitivos 20:41» and «Postre omitido», in the order of the table; they are not repeated below', () => {
    const { done, courses } = timeline(MESA4, nameOf)
    expect(done.map(item => item.text)).toEqual([`✓ Aperitivos ${clockLabel(at('20:41'))}`, 'Primero omitido'])
    expect(done.map(item => [item.course.id, item.skipped, item.name, item.time])).toEqual([['aperitivos', false, 'Aperitivos', clockLabel(at('20:41'))], ['primero', true, 'Primero', '']])
    expect(courses.map(item => item.course.id)).toEqual(['principal', 'postre'])
  })
  it('a served course without the server time says nothing more (never invented)', () => {
    expect(timeline(dining([course('c1', 'Aperitivos', 'Served', [])]), nameOf).done[0].text).toBe('✓ Aperitivos')
  })
})

describe('each unfinished course with its dishes grouped by the common rule', () => {
  const { courses } = timeline(MESA4, nameOf)
  const principal = courses[0]
  it('«Merluza á galega × 4 (1 adaptada)», its state and its station by name', () => {
    expect(principal.dishes.map(line => [line.text, line.note, line.state, line.station]))
      .toEqual([['Merluza á galega × 4', '(1 adaptada)', 'Sin enviar', 'Cocina caliente'], ['Ensalada × 4', '', 'Sin enviar', 'Cocina fría']])
    expect(principal.position).toBe(3)
    expect([principal.step, principal.state]).toEqual(['pending', 'Sin enviar'])
  })
  it('the exceptions go apart under their dish, written and never dimmed; red only when they alarm', () => {
    expect(principal.dishes[0].exceptions.map(line => [line.text, line.alarming, line.pending])).toEqual([
      ['Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra', true, false],
      ['Comensal 3 · Intolerancia lactosa (leve) · No le afecta: no lleva lactosa', false, false],
    ])
    expect(principal.dishes.map(line => line.alarming)).toEqual([true, false])
    expect(principal.dishes[1].exceptions).toEqual([])
  })
  it('while the kitchen has not decided, the waiter reads «Por revisar en cocina» (never «Por revisar en el pase»)', () => {
    const postre = courses[1]
    expect(postre.dishes[0].exceptions.map(line => [line.text, line.alarming, line.pending]))
      .toEqual([['Comensal 2 · ALERGIA frutos secos (grave) · Por revisar en cocina', true, true]])
    expect(postre.dishes[0].note).toBe('(1 por revisar)')
  })
  it('a whole-table restriction is not repeated on every line (it is in the red strip above); an «unaffected» under it is no exception', () => {
    const service = dining([course('principal', 'Principal', 'Pending', four('merluza', 'Merluza', 'hot', guest => ({ restrictions: [GLUTEN],
      review: { decision: guest === 3 ? 'Adapt' : 'Unaffected', note: guest === 3 ? 'sin harina' : 'no lleva gluten', at: AT } })))],
    ['fire-next'], { restrictions: [GLUTEN] })
    const [merluza] = timeline(service, nameOf).courses[0].dishes
    expect(merluza.exceptions.map(line => line.text)).toEqual(['Comensal 3 · Adaptado: sin harina'])
    expect(merluza.exceptions[0].alarming).toBe(true)          // adaptado por una alergia que alarma: rojo
    expect([merluza.text, merluza.note]).toEqual(['Merluza × 4', '(1 adaptada)'])
  })
  it('a course in the kitchen and a ready one also show their dishes grouped, with their state', () => {
    const service = dining([
      course('c1', 'Aperitivos', 'Ready', four('ostra', 'Ostra', 'cold', () => ({ state: 'Ready' })), { firedAt: at('21:05'), readyAt: at('21:19') }),
      course('c2', 'Principal', 'Preparing', [...four('m', 'Merluza', 'hot', guest => ({ state: guest < 3 ? 'Ready' : 'Preparing' })), prep('p', 'Pan', 'pase', null, { state: 'Fired' })],
        { firedAt: at('21:14') }),
    ])
    const [ready, kitchen] = timeline(service, nameOf).courses
    expect([ready.step, ready.state, ready.ready, ready.dishes.map(line => [line.text, line.state])]).toEqual(['ready', 'Listo para servir', true, [['Ostra × 4', 'Lista']]])
    expect([kitchen.step, kitchen.state, kitchen.ready]).toEqual(['kitchen', 'En preparación', false])
    expect(kitchen.dishes.map(line => [line.text, line.state, line.station])).toEqual([['Merluza × 4', '2 de 4 listas', 'Cocina caliente'], ['Pan × 1', 'Por empezar', 'Pase']])
  })
  it('a course without dishes has none, and its state says what is missing', () => {
    const service = dining([course('c1', 'Postres', 'Pending', [], { optional: true, actions: ['add-dish', 'skip'] })])
    const [postres] = timeline(service, nameOf).courses
    expect([postres.dishes, postres.state]).toEqual([[], 'Sin platos pedidos'])
  })
})

describe('the note of each course and the «Siguiente» mark', () => {
  it('«Revisado por cocina» or «Por revisar en cocina» before sending; «En el pase desde las 21:19» when ready', () => {
    const [principal, postre] = timeline(MESA4, nameOf).courses
    expect(principal.note).toBe(REVIEWED_TEXT)
    expect(REVIEWED_TEXT).toBe('Revisado por cocina')
    expect(postre.note).toBe('Por revisar en cocina')
    const ready = course('c1', 'Principal', 'Ready', [prep('m', 'Merluza', 'hot', 1, { state: 'Ready', restrictions: [NUTS], review: { decision: 'Adapt', note: 'x', at: AT } })],
      { readyAt: at('21:19') })
    expect(courseNote(ready, 4)).toBe(`En el pase desde las ${clockLabel(at('21:19'))}`)
    // Una alergia anotada despues de validar: el pase sigue listo pero por revisar, y se dice.
    expect(courseNote({ ...ready, preparations: [{ ...ready.preparations[0], reviewPending: true }] }, 4))
      .toBe(`En el pase desde las ${clockLabel(at('21:19'))} · Por revisar en cocina`)
    expect(courseNote({ ...ready, readyAt: null }, 4)).toBe('')
  })
  it('nothing to review, no note; in the kitchen, since when and how many dishes are ready; a choice says who has chosen', () => {
    expect(courseNote(course('c1', 'Principal', 'Pending', four('m', 'Merluza', 'hot')), 4)).toBe('')
    const fired = course('c1', 'Principal', 'Fired', [prep('m', 'Merluza', 'hot', 1, { state: 'Fired', restrictions: [NUTS], review: { decision: 'Adapt', note: 'x', at: AT } })],
      { firedAt: at('21:14') })
    expect(courseNote(fired, 4)).toBe(`En cocina desde las ${clockLabel(at('21:14'))} · 0 de 1 plato listo`)
    expect(courseNote({ ...fired, preparations: [{ ...fired.preparations[0], reviewPending: true }] }, 4))
      .toBe(`En cocina desde las ${clockLabel(at('21:14'))} · 0 de 1 plato listo · Por revisar en cocina`)
    expect(courseNote(course('c1', 'Primero', 'Pending', [prep('a', 'Vieira', 'hot', 1)], { choiceRequired: true }), 4)).toBe('Han elegido 1 de 4 personas')
  })
  it('«Siguiente» is the course that fire-next would send (the one the big button names), only when the server announces it', () => {
    const { courses } = timeline(MESA4, nameOf)
    expect(courses.map(item => item.next)).toEqual([true, false])
    expect(NEXT_MARK).toBe('Siguiente')
    // Sin fire-next anunciado (un pase en cocina o listo, o la revision de cocina pendiente), ninguna marca.
    expect(timeline({ ...MESA4, actions: ['pause'] }, nameOf).courses.map(item => item.next)).toEqual([false, false])
  })
})

describe('choose and order: one command per tap, with the version read (F01), until E4g', () => {
  const choice = course('primero', 'Primero', 'Pending', [], { choiceRequired: true, actions: ['choose', 'skip'] })
  const carta = course('postres', 'Postres', 'Pending', [], { optional: true, actions: ['add-dish', 'skip'] })
  const row = entry(dining([choice, carta]))
  const TARTA = { productId: 'tarta', presentationId: 'racion', name: 'Tarta de Santiago', presentation: 'Ración' }
  it('choose: ONE command for that guest and dish', () => {
    expect(chooseRequest(row, choice, 2, { id: 'vieira', name: 'Vieira' }, 'Mesa 4')).toEqual({
      path: '/dining/services/svc%2F4/commands/choose', body: { expectedVersion: 21, courseId: 'primero', guestPosition: 2, dishId: 'vieira' },
      description: 'Elegir Vieira para el comensal 2 · Mesa 4' })
  })
  it('order: ONE command with the quantity and for whom (a guest or the whole table)', () => {
    expect(addDishRequest(row, carta, TARTA, 2, null, 'Mesa 4')).toEqual({
      path: '/dining/services/svc%2F4/commands/add-dish',
      body: { expectedVersion: 21, courseId: 'postres', productId: 'tarta', presentationId: 'racion', quantity: 2, guestPosition: null },
      description: 'Pedir 2 × Tarta de Santiago en Postres · Mesa 4' })
    expect(addDishRequest(row, carta, TARTA, 1, 3, 'Mesa 4')?.body).toMatchObject({ quantity: 1, guestPosition: 3 })
  })
  it('nothing announced, no command: nothing is deduced here', () => {
    expect(chooseRequest(row, carta, 2, { id: 'vieira', name: 'Vieira' }, 'Mesa 4')).toBeNull()
    expect(addDishRequest(row, choice, TARTA, 1, null, 'Mesa 4')).toBeNull()
  })
  it('the only local check is the format of the quantity: 1 to 20, like the server', () => {
    expect(MAX_DISH_QUANTITY).toBe(20)
    expect([0, 21, 1.5, Number.NaN].map(quantity => addDishRequest(row, carta, TARTA, quantity, null, 'Mesa 4'))).toEqual([null, null, null, null])
    expect(addDishRequest(row, carta, TARTA, 20, null, 'Mesa 4')).not.toBeNull()
  })
})

describe('the allergy sheet: each restriction with the dishes it affects and their state', () => {
  // Gluten de toda la mesa y frutos secos del comensal 2 (el servidor los da en el orden en que se anotaron). Aperitivos, listo en el
  // pase y aun sin servir: lo que cocina decidio vale para estas restricciones (al anotarlas se marca para revisar todo lo no servido).
  const both = (guest: number): GuestRestriction[] => guest === 2 ? [GLUTEN, NUTS] : [GLUTEN]
  const service = dining([
    course('aperitivos', 'Aperitivos', 'Ready', four('ostra', 'Ostra', 'cold', guest => ({ state: 'Ready', restrictions: both(guest),
      review: guest === 2 ? { decision: 'Adapt', note: 'sin almendra', at: AT } : { decision: 'Unaffected', note: 'no lleva gluten', at: AT } })),
      { readyAt: at('20:35') }),
    course('primero', 'Primero', 'Skipped', four('caldo', 'Caldo', 'hot', guest => ({ restrictions: both(guest), review: { decision: 'Unaffected', note: 'no', at: AT } }))),
    course('segundo', 'Segundo pase', 'Pending', four('merluza', 'Merluza á galega', 'hot', guest => ({ restrictions: both(guest), reviewPending: true }))),
    course('postre', 'Postre', 'Pending', [prep('pan', 'Pan', 'pase', null, { restrictions: [GLUTEN], review: { decision: 'Remake', note: 'pan sin gluten', at: AT } })]),
  ], [], { restrictions: [NUTS, LACTOSE, GLUTEN] })
  const list = allergyList(service)
  it('the whole-table ones first, then each guest by number; each one written in full, red only when it alarms', () => {
    expect(list.map(item => [item.text, item.alarming])).toEqual([
      ['Toda la mesa · ALERGIA gluten (grave)', true],
      ['Comensal 2 · ALERGIA frutos secos (grave)', true],
      ['Comensal 3 · Intolerancia lactosa (leve)', false],
    ])
  })
  it('«Aperitivos · Ostra: Adaptado: sin almendra», «Segundo pase · Merluza á galega: Por revisar en cocina»', () => {
    expect(list[1].dishes.map(dish => [dish.text, dish.pending])).toEqual([
      ['Aperitivos · Ostra: Adaptado: sin almendra', false],
      ['Segundo pase · Merluza á galega: Por revisar en cocina', true],
    ])
  })
  it('a whole-table one gathers the same decision of a dish; when it is only some guests, it says which ones', () => {
    expect(list[0].dishes.map(dish => dish.text)).toEqual([
      'Aperitivos · Ostra · Comensales 1, 3 y 4: No le afecta: no lleva gluten',
      'Aperitivos · Ostra · Comensal 2: Adaptado: sin almendra',
      'Segundo pase · Merluza á galega × 4: Por revisar en cocina',
      'Postre · Pan: Rehecho: pan sin gluten',
    ])
  })
  it('a skipped course does not go out: its dishes are not listed; a restriction without dishes still is', () => {
    expect(list.flatMap(item => item.dishes.map(dish => dish.text)).filter(text => text.startsWith('Primero'))).toEqual([])
    expect(list[2].dishes).toEqual([])
    expect(allergyList(dining([], [], { restrictions: undefined }))).toEqual([])
  })
  // El servidor pone en cada elaboracion TODAS las restricciones que le aplican, tambien en lo servido (AllowedActions.cs,
  // ViewWithActions), pero no marca para revisar lo servido (CourseExecution.FlagForReview). Caso de la sala: el comensal avisa del
  // gluten despues de los aperitivos, que ya llevaban la adaptacion de frutos secos del comensal 2.
  it('a course served before the restriction was noted: its served state, never a decision (nobody reviewed it for this one)', () => {
    const later = dining([
      course('aperitivos', 'Aperitivos', 'Served', four('ostra', 'Ostra', 'cold', guest => ({ state: 'Ready', restrictions: guest === 2 ? [NUTS, GLUTEN] : [GLUTEN],
        ...(guest === 2 ? { review: { decision: 'Adapt' as const, note: 'sin almendra', at: AT } } : {}) })), { servedAt: at('20:41') }),
      course('principal', 'Principal', 'Pending', four('merluza', 'Merluza á galega', 'hot', guest => ({ restrictions: guest === 2 ? [NUTS, GLUTEN] : [GLUTEN], reviewPending: true }))),
    ], [], { restrictions: [NUTS, GLUTEN] })
    const [gluten, nuts] = allergyList(later)
    expect(gluten.dishes.map(dish => [dish.text, dish.pending])).toEqual([
      [`Aperitivos · Ostra × 4: Servido a las ${clockLabel(at('20:41'))}`, false],
      ['Principal · Merluza á galega × 4: Por revisar en cocina', true],
    ])
    expect(nuts.dishes.map(dish => dish.text)).toEqual([`Aperitivos · Ostra: Servido a las ${clockLabel(at('20:41'))}`, 'Principal · Merluza á galega: Por revisar en cocina'])
    // Sin la hora del servidor, solo «Servido»: no se inventa.
    expect(allergyList({ ...later, courses: [{ ...later.courses[0], servedAt: null }] })[0].dishes.map(dish => dish.text)).toEqual(['Aperitivos · Ostra × 4: Servido'])
  })
  it('a dish with neither a decision nor a review pending (a table opened before E4e) still says its state: never a line without one', () => {
    const legacy = dining([course('principal', 'Principal', 'Fired', [prep('pulpo', 'Pulpo á feira', 'hot', 3, { state: 'Fired', restrictions: [LACTOSE] })])],
      [], { restrictions: [LACTOSE] })
    expect(allergyList(legacy)[0].dishes.map(dish => [dish.text, dish.pending])).toEqual([['Principal · Pulpo á feira: Por empezar', false]])
  })
})

// C3e.1 (usabilidad 28-10, H19): no siempre sirve quien abrio la mesa. La banda «listo para servir» escribe, bajo su titulo, las lineas
// del pase listo con la MISMA regla, el mismo texto y el mismo tono que la ficha, todas; delante, las restricciones de toda la mesa.
describe('C3e.1 (H19): the lines of ONE course for the «listo para servir» banner', () => {
  const READY = { readyAt: at('21:19'), actions: ['serve'] }
  it('the very lines the table card writes under each dish of that course, all of them and with the same tone', () => {
    const service = dining([course('principal', 'Principal', 'Ready', [...four('merluza', 'Merluza á galega', 'hot', guest => ({ state: 'Ready', ...reviewed(guest) })),
      prep('pan', 'Pan', 'pase', null, { state: 'Ready', restrictions: [NUTS], review: { decision: 'Remake', note: 'sin nueces', at: AT } })], READY)])
    const lines = passLines(service.courses[0])
    expect(lines.map(line => [line.text, line.tone])).toEqual([
      ['Merluza á galega · Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra', 'alarm'],
      ['Merluza á galega · Comensal 3 · Intolerancia lactosa (leve) · No le afecta: no lleva lactosa', 'calm'],
      ['Pan · Toda la mesa · ALERGIA frutos secos (grave) del Comensal 2 · Rehecho: sin nueces', 'alarm'],
    ])
    // Lo mismo que pinta la ficha (CoursesTimeline, con exceptionText y lineTone): una sola regla para las dos.
    const [card] = timeline(service, nameOf).courses
    const fromCard = card.dishes.flatMap(dish => dish.exceptions.map(exception => [exceptionText(dish.dish, exception), lineTone(exception)]))
    expect(lines.map(line => [line.text, line.tone])).toEqual(fromCard)
    expect(new Set(lines.map(line => line.key)).size).toBe(lines.length)
  })
  it('a whole-table restriction goes first, written like the red strip; the line of a dish adapted for it does not repeat the substance', () => {
    const service = dining([course('principal', 'Principal', 'Ready', four('merluza', 'Merluza', 'hot', guest => ({ state: 'Ready', restrictions: [GLUTEN],
      review: { decision: guest === 3 ? 'Adapt' : 'Unaffected', note: guest === 3 ? 'sin harina' : 'no lleva gluten', at: AT } })), READY)], ['fire-next'], { restrictions: [GLUTEN] })
    expect(passLines(service.courses[0]).map(line => [line.text, line.tone])).toEqual([
      ['Toda la mesa · ALERGIA gluten (grave)', 'alarm'],
      ['Merluza · Comensal 3 · Adaptado: sin harina', 'alarm'],
    ])
  })
  it('a review still pending reads «Por revisar en cocina»: red if it alarms, amber if not; a course with nothing to say has no lines', () => {
    const service = dining([course('postre', 'Postre', 'Ready', four('tarta', 'Tarta de Santiago', 'cold', guest => ({ state: 'Ready',
      ...(guest === 2 ? { restrictions: [NUTS], reviewPending: true } : guest === 3 ? { restrictions: [LACTOSE], reviewPending: true } : {}) })), READY)])
    expect(passLines(service.courses[0]).map(line => [line.text, line.tone, line.pending])).toEqual([
      ['Tarta de Santiago · Comensal 2 · ALERGIA frutos secos (grave) · Por revisar en cocina', 'alarm', true],
      ['Tarta de Santiago · Comensal 3 · Intolerancia lactosa (leve) · Por revisar en cocina', 'warn', true],
    ])
    expect(passLines(course('c1', 'Aperitivos', 'Ready', four('ostra', 'Ostra', 'cold', () => ({ state: 'Ready' })), READY))).toEqual([])
  })
})

describe('pases.ts itself', () => {
  const source = readFileSync(fileURLToPath(new URL('../src/pases.ts', import.meta.url))).toString('utf8')
  const code = source.replace(/^\s*\/\/.*$/gm, '')
  const literals = [...code.matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])
  it('has no DOM: it runs in Node and is tested without a browser', () => {
    expect(code).not.toMatch(/\b(window|document|navigator|localStorage|indexedDB|AudioContext)\b/)
  })
  it('no checkout word anywhere, not even in a comment, an identifier or inside another word', () => {
    expect(literals.length).toBeGreaterThan(5)
    expect(source).not.toMatch(/€|precio|saldo|pago|cuenta|importe|cents|amount/i)
  })
  it('Spanish with accents', () => {
    const unaccented = ['estacion', 'revision', 'tambien', 'mas', 'ultimo', 'senal']
    expect(literals.flatMap(text => unaccented.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)))).toEqual([])
  })
  // La regla en si la fija la tabla comun de arriba (kitchen-cases.json); aqui solo que agrupa y escribe con las piezas de cocina.ts.
  it('groups and writes with the common pieces of cocina.ts (dishes, exceptionLine)', () => {
    expect([...source.matchAll(/from '([^']+)'/g)].map(match => match[1]).sort()).toEqual(['./cocina', './configuration', './ficha', './floor', './labels', './types'])
    const fromCocina = (code.match(/import \{([^}]*)\} from '\.\/cocina'/)?.[1] ?? '').split(',').map(name => name.trim())
    expect(fromCocina).toEqual(expect.arrayContaining(['dishes', 'exceptionLine']))
  })
})
