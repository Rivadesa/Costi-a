import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  draftLabel, draftLines, draftProblem, guestChoices, initialPax, KINDS, LEAVE_DISCARD, LEAVE_STAY, LEAVE_TITLE, lostText, MAX_DRAFTS, newDraft, offerGroups,
  OPEN_LINES, openDescription, openLines, openRequest, openSummary, paxChoices, paxLimit, paxProblem, restrictionSubject, SEVERITIES, WHAT_MISSING, WHO_MISSING,
  type RestrictionDraft,
} from '../src/abrir'
import { restrictionLabel } from '../src/labels'
import { assertNoMoney } from '../src/money-guard'

// C3e (plan 2026-10-27, paso 2, «Abrir mesa»): la hoja de abrir de Costina Sala SIN DOM. Personas de 1 a 8 y «Más…» sin pasar de la
// capacidad, el menu del dia ya elegido, las alergias como borradores en memoria y UNA orden open con todo (E4e). Solo se comprueba el
// formato: el servidor decide (capacidad, oferta vigente, comensal, duplicadas) y su rechazo llega por la orden incierta de siempre.

const read = (path: string) => readFileSync(fileURLToPath(new URL(path, import.meta.url))).toString('utf8')
const draft = (extra: Partial<RestrictionDraft> = {}): RestrictionDraft => ({ ...newDraft(), ...extra })
const range = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, index) => from + index)

// El contrato del servidor, leido de su codigo: los campos de OpenRequest y RestrictionRequest (camelCase, JsonSerializerDefaults.Web,
// que ademas rechaza un campo desconocido) y los nombres de los enum de tipo y gravedad. Si el servidor los cambia, esta prueba cae.
const operations = read('../../../dotnet/src/Costina.Modules.Dining/DiningOperations.cs')
const restrictionsDomain = read('../../../dotnet/src/Costina.Modules.Dining.Domain/Restrictions.cs')
function recordFields(record: string): string[] {
  const match = operations.match(new RegExp(`record ${record}\\(([^)]*)\\)`))
  expect(match, `record ${record} in DiningOperations.cs`).not.toBeNull()
  return (match?.[1] ?? '').split(',').map(part => part.replace(/=.*$/s, '').trim().split(/\s+/).pop() ?? '')
    .map(name => name.charAt(0).toLowerCase() + name.slice(1))
}
function enumMembers(name: string): string[] {
  const match = restrictionsDomain.match(new RegExp(`enum ${name} \\{([^}]*)\\}`))
  expect(match, `enum ${name} in Restrictions.cs`).not.toBeNull()
  return (match?.[1] ?? '').split(',').map(part => part.trim())
}

describe('how many people', () => {
  it('buttons from 1 to 8, never above the capacity; «Más…» only when the table takes more than 8 or its capacity is unknown', () => {
    expect(paxChoices(1)).toEqual({ counts: [1], more: false })
    expect(paxChoices(4)).toEqual({ counts: [1, 2, 3, 4], more: false })
    expect(paxChoices(8)).toEqual({ counts: range(1, 8), more: false })
    expect(paxChoices(9)).toEqual({ counts: range(1, 8), more: true })
    expect(paxChoices(12)).toEqual({ counts: range(1, 8), more: true })
    // Sin capacidad conocida (0 es lo que pasa la vista si la configuracion no tiene la mesa).
    for (const unknown of [0, -2, Number.NaN, 2.5, null, undefined]) expect(paxChoices(unknown)).toEqual({ counts: range(1, 8), more: true })
  })
  it('the limit is the capacity of the table, or 40 when it is unknown', () => {
    expect([paxLimit(1), paxLimit(12), paxLimit(60)]).toEqual([1, 12, 60])
    expect([paxLimit(0), paxLimit(-1), paxLimit(Number.NaN), paxLimit(3.5), paxLimit(null), paxLimit(undefined)]).toEqual([40, 40, 40, 40, 40, 40])
  })
  it('starts at 2, as today, except on a table for one', () => {
    expect([initialPax(12), initialPax(2), initialPax(0), initialPax(1)]).toEqual([2, 2, 2, 1])
  })
  it('out of range: the capacity of the table is said in words; with an unknown capacity, 1 to 40 as today', () => {
    expect([paxProblem(1, 12), paxProblem(12, 12), paxProblem(40, 0)]).toEqual(['', '', ''])
    expect(paxProblem(13, 12)).toBe('Esta mesa admite hasta 12 personas.')
    expect(paxProblem(2, 1)).toBe('Esta mesa admite hasta 1 persona.')
    expect(paxProblem(0, 12)).toBe('Elige cuántas personas (1 a 12).')
    expect(paxProblem(2.5, 12)).toBe('Elige cuántas personas (1 a 12).')
    expect(paxProblem(Number.NaN, 0)).toBe('Elige cuántas personas (1 a 40).')
    expect(paxProblem(41, 0)).toBe('Elige cuántas personas (1 a 40).')
  })
})

describe('what they will eat', () => {
  // El laboratorio: el menu del dia es LAB-TASTING («Menú de ensayo»), no LAB-DAILY, que es un menu con eleccion (e2e/commands.spec.ts:24).
  const LAB = [
    { id: 'LAB-TASTING', name: 'Menú de ensayo', kind: 'tasting' },
    { id: 'LAB-DAILY', name: 'Menú del día de ensayo', kind: 'set-menu' },
    { id: 'LAB-CARTA', name: 'Carta de ensayo', kind: 'a-la-carte' },
  ]
  it('the proposal is the first offer in force; the rest behind «Otro menú…»; menus with a choice only under «Más opciones»', () => {
    const groups = offerGroups(LAB)
    expect(groups.proposed?.id).toBe('LAB-TASTING')
    expect(groups.others.map(offer => offer.id)).toEqual(['LAB-CARTA'])
    expect(groups.withChoice.map(offer => offer.id)).toEqual(['LAB-DAILY'])
  })
  it('keeps the order of /configuration inside each group', () => {
    const offers = [{ id: 'A', kind: 'tasting' }, { id: 'B', kind: 'set-menu' }, { id: 'C', kind: 'tasting' }, { id: 'D', kind: 'SetMenu' }, { id: 'E', kind: 'a-la-carte' }]
    const groups = offerGroups(offers)
    expect([groups.proposed?.id, groups.others.map(offer => offer.id), groups.withChoice.map(offer => offer.id)]).toEqual(['A', ['C', 'E'], ['B', 'D']])
  })
  it('the first offer is proposed even when it has a choice (E4d, like the main PC), and it is not repeated under «Más opciones»', () => {
    const groups = offerGroups([{ id: 'ELIGE', kind: 'set-menu' }, { id: 'CERRADO', kind: 'tasting' }])
    expect([groups.proposed?.id, groups.others.map(offer => offer.id), groups.withChoice]).toEqual(['ELIGE', ['CERRADO'], []])
  })
  it('one offer or none', () => {
    expect(offerGroups([{ id: 'SOLO', kind: 'tasting' }])).toEqual({ proposed: { id: 'SOLO', kind: 'tasting' }, others: [], withChoice: [] })
    expect(offerGroups([])).toEqual({ proposed: null, others: [], withChoice: [] })
  })
})

describe('allergies or intolerances', () => {
  it('a new row has no guest («Toda la mesa» is one more option), Alergia and Grave by default, and is a fresh object each time', () => {
    expect(newDraft()).toEqual({ guest: null, kind: 'Allergy', substance: '', severity: 'Severe' })
    expect(newDraft()).not.toBe(newDraft())
  })
  it('KINDS and SEVERITIES are the server enums, in its order, with the words of the waiter', () => {
    expect(KINDS.map(option => option.code)).toEqual(enumMembers('RestrictionKind'))
    expect(SEVERITIES.map(option => option.code)).toEqual(enumMembers('RestrictionSeverity'))
    expect(KINDS.map(option => option.label)).toEqual(['Alergia', 'Intolerancia', 'Preferencia'])
    expect(SEVERITIES.map(option => option.label)).toEqual(['Grave', 'Moderada', 'Leve'])
  })
  it('the guests to choose from: each one and «Toda la mesa» last; until the people are a number, only «Toda la mesa»', () => {
    expect(guestChoices(3)).toEqual([
      { guest: 1, label: 'Comensal 1' }, { guest: 2, label: 'Comensal 2' }, { guest: 3, label: 'Comensal 3' }, { guest: 'all', label: 'Toda la mesa' }])
    for (const pax of [0, -1, Number.NaN, 2.5]) expect(guestChoices(pax)).toEqual([{ guest: 'all', label: 'Toda la mesa' }])
  })
  it('at most 30 rows, like the main PC', () => { expect(MAX_DRAFTS).toBe(30) })

  describe('draftProblem: only the format, with the words of the main PC', () => {
    const GUEST = 'Elige para quién es cada alergia o intolerancia: un comensal o toda la mesa.'
    const SUBSTANCE = 'Escribe a qué es cada alergia o intolerancia (2 caracteres o más).'
    const DUPLICATE = 'Esa alergia o intolerancia ya está anotada para ese comensal.'
    it('no rows, or rows empty through and through, are no problem (a kind or a severity alone does not fill a row)', () => {
      expect(draftProblem([], 3)).toBe('')
      expect(draftProblem([newDraft(), draft({ kind: 'Intolerance', severity: 'Mild', substance: '   ' })], 3)).toBe('')
    })
    it('a row with what but without who', () => {
      expect(draftProblem([draft({ substance: 'gluten' })], 3)).toBe(GUEST)
    })
    it('a row with who but without what, or with less than 2 characters once trimmed', () => {
      expect(draftProblem([draft({ guest: 2 })], 3)).toBe(SUBSTANCE)
      expect(draftProblem([draft({ guest: 'all', substance: ' a ' })], 3)).toBe(SUBSTANCE)
      expect(draftProblem([draft({ guest: 'all', substance: 'ab' })], 3)).toBe('')
    })
    it('a guest outside the table, with the people of the sheet', () => {
      expect(draftProblem([draft({ guest: 4, substance: 'gluten' })], 3)).toBe('El comensal de una alergia tiene que estar entre 1 y 3.')
      expect(draftProblem([draft({ guest: 0, substance: 'gluten' })], 3)).toBe('El comensal de una alergia tiene que estar entre 1 y 3.')
      expect(draftProblem([draft({ guest: 3, substance: 'gluten' })], 3)).toBe('')
      // Con las personas a medio teclear habla antes paxProblem: aqui no se dice «entre 1 y NaN».
      expect(draftProblem([draft({ guest: 4, substance: 'gluten' })], Number.NaN)).toBe('')
    })
    it('the duplicate_restriction of the server, ahead of time: same guest, same kind, same what (case and spaces aside)', () => {
      const nuts = draft({ guest: 2, substance: 'Frutos secos' })
      expect(draftProblem([nuts, draft({ guest: 2, substance: '  frutos SECOS ' })], 3)).toBe(DUPLICATE)
      expect(draftProblem([draft({ guest: 'all', substance: 'gluten' }), draft({ guest: 'all', substance: 'Gluten', severity: 'Mild' })], 3)).toBe(DUPLICATE)
      expect(draftProblem([nuts, draft({ guest: 2, kind: 'Intolerance', substance: 'frutos secos' })], 3)).toBe('')
      expect(draftProblem([nuts, draft({ guest: 3, substance: 'frutos secos' })], 3)).toBe('')
      expect(draftProblem([nuts, draft({ guest: 'all', substance: 'frutos secos' })], 3)).toBe('')
      // Como OrdinalIgnoreCase del servidor: «ß» y «ss» no son la misma; «Á» y «á», si.
      expect(draftProblem([draft({ guest: 1, substance: 'ß-test' }), draft({ guest: 1, substance: 'SS-test' })], 3)).toBe('')
      expect(draftProblem([draft({ guest: 1, substance: 'Ápio' }), draft({ guest: 1, substance: 'ápio' })], 3)).toBe(DUPLICATE)
    })
    it('the first problem in the order of the rows, format before duplicates, like the main PC', () => {
      const rows = [draft({ guest: 1, substance: 'gluten' }), draft({ guest: 1, substance: 'gluten' }), draft({ substance: 'lactosa' }), draft({ guest: 2 })]
      expect(draftProblem(rows, 3)).toBe(GUEST)
      expect(draftProblem([draft({ guest: 2 }), draft({ substance: 'lactosa' })], 3)).toBe(SUBSTANCE)
    })
  })
})

describe('the open command', () => {
  const table = { id: 'M5', name: 'Mesa 5' }
  const offer = { id: 'LAB-TASTING', name: 'Menú de ensayo' }
  it('without rows: the body of today, which an older server also understands', () => {
    for (const drafts of [[], [newDraft()]]) {
      const request = openRequest(table, 3, offer, drafts)
      expect(request.path).toBe('/dining/services')
      expect(request.body).toEqual({ tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', menuId: 'LAB-TASTING' })
      expect(Object.keys(request.body).sort()).toEqual(['menuId', 'offerId', 'pax', 'tableId'])
      expect(request.description).toBe('Abrir Mesa 5 para 3 personas')
    }
  })
  it('with rows: ONE command with its restrictions, the empty row left out, the text trimmed and «Toda la mesa» as guestPosition null', () => {
    const request = openRequest(table, 3, offer, [
      draft({ guest: 2, substance: ' frutos secos ' }), newDraft(), draft({ guest: 'all', kind: 'Intolerance', substance: 'lactosa', severity: 'Moderate' })])
    expect(request.body).toEqual({
      tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', menuId: 'LAB-TASTING', restrictions: [
        { guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' },
        { guestPosition: null, kind: 'Intolerance', substance: 'lactosa', severity: 'Moderate' }],
    })
    expect(request.description).toBe('Abrir Mesa 5 para 3 personas con 2 alergias o intolerancias')
    expect(openRequest(table, 3, offer, [draft({ guest: 2, substance: 'frutos secos' })]).description).toBe('Abrir Mesa 5 para 3 personas con 1 alergia o intolerancia')
  })
  // El conjunto de campos, no su orden: el servidor los une por nombre (JsonSerializerDefaults.Web), y reordenar los parametros del
  // record no cambia nada. Ni uno de mas (lo rechazaria) ni uno de menos.
  it('the body has exactly the fields of the server records (an unknown field is rejected there), in any order', () => {
    const request = openRequest(table, 3, offer, [draft({ guest: 1, substance: 'gluten' })])
    expect(Object.keys(request.body).sort()).toEqual(recordFields('OpenRequest').sort())
    const [row] = request.body.restrictions as Array<Record<string, unknown>>
    expect(Object.keys(row).sort()).toEqual(recordFields('RestrictionRequest').sort())
  })
  it('a row without its guest never travels as «Toda la mesa»: with a format problem there is no command', () => {
    expect(() => openRequest(table, 3, offer, [draft({ substance: 'gluten' })])).toThrow('Elige para quién es cada alergia o intolerancia: un comensal o toda la mesa.')
    expect(() => openRequest(table, 3, offer, [draft({ guest: 4, substance: 'gluten' })])).toThrow('El comensal de una alergia tiene que estar entre 1 y 3.')
  })
  it('nothing in the body that the money guard would stop', () => {
    expect(() => assertNoMoney(openRequest(table, 3, offer, [draft({ guest: 1, substance: 'gluten' })]).body)).not.toThrow()
  })
  it('the description of the pending command names the table, the people and how many allergies', () => {
    expect(openDescription('Mesa 1', 1, [])).toBe('Abrir Mesa 1 para 1 persona')
    expect(openDescription('Terraza 2', 4, [newDraft(), draft({ guest: 1, substance: 'gluten' })])).toBe('Abrir Terraza 2 para 4 personas con 1 alergia o intolerancia')
  })
  // C3e.1 (usabilidad 28-10, H06): sin «1 alergia o intolerancia» al final; las alergias van debajo, enteras (openLines).
  it('the line above «Abrir»: people and menu, only what there is (the allergies go below it, whole)', () => {
    expect(openSummary(3, offer)).toBe('3 personas · Menú de ensayo')
    expect(openSummary(1, null)).toBe('1 persona')
    expect(openSummary(Number.NaN, offer)).toBe('Menú de ensayo')
    expect(openSummary(Number.NaN, null)).toBe('')
  })
})

// C3e.1 (usabilidad 28-10, H06): encima de «Abrir» decia solo «1 alergia o intolerancia», sin quien ni que, y salir de la hoja la
// cerraba sin aviso. Ahora cada fila escrita va entera, con el formato de la ficha, y salir con alguna escrita pregunta antes.
describe('C3e.1 (H06): every allergy written whole above «Abrir», and a question before leaving', () => {
  const nuts = draft({ guest: 2, substance: ' frutos secos ' })
  const lactose = draft({ guest: 'all', kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' })
  it('a complete row says exactly what the table card will say (labels.ts, restrictionLabel)', () => {
    expect(draftLabel(nuts)).toBe('Comensal 2 · ALERGIA frutos secos (grave)')
    expect(draftLabel(lactose)).toBe('Toda la mesa · Intolerancia lactosa (leve)')
    for (const row of [nuts, lactose, draft({ guest: 5, kind: 'Preference', substance: 'Cerdo', severity: 'Moderate' })]) {
      const guestPosition = row.guest === 'all' || row.guest === null ? null : row.guest
      expect(draftLabel(row)).toBe(restrictionLabel({ id: 'x', guestPosition, kind: row.kind, substance: row.substance.trim(), severity: row.severity }))
    }
  })
  it('what a row still lacks is written as its question: a row without who is NEVER «Toda la mesa»', () => {
    expect([WHO_MISSING, WHAT_MISSING]).toEqual(['¿Quién?', '¿A qué?'])
    expect(draftLabel(draft({ substance: 'gluten' }))).toBe('¿Quién? · ALERGIA gluten (grave)')
    expect(draftLabel(draft({ guest: 3 }))).toBe('Comensal 3 · ALERGIA ¿A qué? (grave)')
    expect(draftLabel(draft({ guest: 'all', substance: '   ' }))).toBe('Toda la mesa · ALERGIA ¿A qué? (grave)')
    expect(draftLabel(draft({ substance: 'gluten' }))).not.toContain('Toda la mesa')
  })
  it('only the rows with something written, in their order, red when they alarm with the rule of the table card, amber otherwise', () => {
    const rows = [nuts, newDraft(), draft({ kind: 'Intolerance', severity: 'Mild' }), lactose, draft({ guest: 1, kind: 'Intolerance', substance: 'gluten', severity: 'Severe' })]
    expect(draftLines(rows)).toEqual([
      { text: 'Comensal 2 · ALERGIA frutos secos (grave)', alarming: true },
      { text: 'Toda la mesa · Intolerancia lactosa (leve)', alarming: false },
      { text: 'Comensal 1 · Intolerancia gluten (grave)', alarming: true },
    ])
    expect(draftLines([])).toEqual([])
    expect(draftLines([newDraft()])).toEqual([])
  })
  it('above «Abrir»: up to 3 rows whole; with more, ONE line that never hides that there are some, red if any alarms', () => {
    expect(OPEN_LINES).toBe(3)
    const three = [nuts, lactose, draft({ guest: 1, substance: 'apio' })]
    expect(openLines(three)).toEqual(draftLines(three))
    const four = [...three, draft({ guest: 3, kind: 'Preference', substance: 'cerdo', severity: 'Mild' })]
    expect(openLines(four)).toEqual([{ text: '4 alergias o intolerancias anotadas: revísalas arriba antes de abrir.', alarming: true }])
    const mild = Array.from({ length: 5 }, (_, index) => draft({ guest: index + 1, kind: 'Preference', substance: 'cerdo', severity: 'Mild' }))
    expect(openLines(mild)).toEqual([{ text: '5 alergias o intolerancias anotadas: revísalas arriba antes de abrir.', alarming: false }])
    // Las filas vacias no suman para pasar de 3.
    expect(openLines([...three, newDraft(), newDraft()])).toEqual(draftLines(three))
  })
  it('leaving with something written asks first, with the right plural; with nothing written it does not ask', () => {
    expect([LEAVE_TITLE, LEAVE_STAY, LEAVE_DISCARD]).toEqual(['¿Salir sin abrir la mesa?', 'Seguir abriendo', 'Salir sin abrir'])
    expect(lostText([])).toBe('')
    expect(lostText([newDraft(), draft({ kind: 'Intolerance' })])).toBe('')
    expect(lostText([nuts])).toBe('Se perderá 1 alergia: habrá que escribirla otra vez.')
    expect(lostText([nuts, draft({ guest: 1, substance: 'gluten' })])).toBe('Se perderán 2 alergias: habrá que escribirlas otra vez.')
    // Con alguna que no es una alergia, la palabra de la hoja: «alergia o intolerancia».
    expect(lostText([lactose])).toBe('Se perderá 1 alergia o intolerancia: habrá que escribirla otra vez.')
    expect(lostText([nuts, lactose, newDraft()])).toBe('Se perderán 2 alergias o intolerancias: habrá que escribirlas otra vez.')
    // Una fila a medias tambien se perderia: se pregunta igual.
    expect(lostText([draft({ substance: 'gluten' })])).toBe('Se perderá 1 alergia: habrá que escribirla otra vez.')
  })
})

// C3e.1 (usabilidad 28-10, H03): como se nombra una restriccion que NO se ha anotado o retirado (commands.ts, notApplied).
describe('C3e.1 (H03): the subject of «… NO se ha anotado»', () => {
  it('the kind in words, feminine like the reasons; an unknown kind is never softened', () => {
    expect(KINDS.map(option => restrictionSubject(option.code))).toEqual(['La alergia', 'La intolerancia', 'La preferencia'])
    expect([restrictionSubject('allergy'), restrictionSubject('Anaphylaxis')]).toEqual(['La alergia', 'La restricción'])
  })
})

describe('abrir.ts itself', () => {
  const source = read('../src/abrir.ts')
  const code = source.replace(/^\s*\/\/.*$/gm, '')
  const literals = [...code.matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])
  it('has no DOM and no storage: it runs in Node and is tested without a browser', () => {
    expect([...code.matchAll(/from '([^']+)'/g)].map(match => match[1]).sort()).toEqual(['./ficha', './floor', './labels', './offers', './types'])
    expect(code).not.toMatch(/\b(window|document|navigator|localStorage|indexedDB)\b/)
  })
  it('no checkout word anywhere, not even in a comment or inside another word', () => {
    expect(literals.length).toBeGreaterThan(10)
    expect(source).not.toMatch(/€|precio|saldo|pago|cuenta|cents|amount/i)
  })
  it('Spanish with accents', () => {
    const unaccented = ['quien', 'cuantas', 'mas', 'menu', 'dia', 'eleccion']
    expect(literals.flatMap(text => unaccented.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)))).toEqual([])
  })
})
