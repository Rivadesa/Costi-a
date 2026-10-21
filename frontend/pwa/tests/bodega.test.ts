import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '../src/api'
import {
  addLabel, addProblem, chipHeader, chipProducts, chosenChip, doneDetail, drinkChips, EMPTY_ROUND, formatParts, GLASS_CHIP, guestChoices, HAD_EMPTY, HAD_FOOT,
  HAD_SUB, HAD_TITLE, hadDetail, leaveText, NO_RESULTS_HINT, noResults, ONE_MORE_LETTER, OTHER_CHIP, resultsCount, RESULTS_WHERE, ROUND_CONFLICT, ROUND_HINT,
  ROUND_SENT, roundNames, roundProblem, restrictionsNote, settlementOf, VOIDED,
} from '../src/bodega'
import type { Outcome } from '../src/commands'
import { drinkProducts, roundItem } from '../src/drinks'
import { Round } from '../src/round'
import type { GuestRestriction, TableConsumption } from '../src/types'

// C3b: lo que pinta «Bodega y bebidas» en el comandero (bodega.ts), sin DOM. Lo comun con el puesto (busqueda, agrupacion y ronda) ya lo
// prueba drinks.test.ts con la tabla de casos compartida; aqui, los accesos, los textos y que pasa con la ronda tras su orden.
const row = (id: string, name: string, presentationId: string, presentation: string, extra: Partial<CatalogItem> = {}): CatalogItem =>
  ({ id, name, presentationId, presentation, section: 'bodega', family: 'Vinos', categoryPath: ['Vinos', 'Rías Baixas'], ...extra })
const catalog: CatalogItem[] = [
  row('albarino', 'Albariño Pazo de Señoráns', 'botella-0-75-l', 'Botella 0.75 L'),
  row('albarino', 'Albariño Pazo de Señoráns', 'copa', 'Copa', { glass: true }),
  row('mencia', 'Guímaro Mencía', 'copa', 'Copa', { glass: true, categoryPath: ['Vinos', 'Ribeira Sacra'] }),
  row('mencia', 'Guímaro Mencía', 'botella-0-75-l', 'Botella 0.75 L', { categoryPath: ['Vinos', 'Ribeira Sacra'] }),
  row('estrella', 'Estrella Galicia', 'cana', 'Caña', { family: 'Cervezas', categoryPath: ['Cervezas'] }),
  row('agua', 'Agua con gas', 'unit', 'Unidad', { family: null, categoryPath: [] }),
  row('croquetas', 'Croquetas', 'unit', 'Ración', { section: 'cocina', family: 'Entrantes', categoryPath: ['Entrantes'] }),
]
const products = drinkProducts(catalog)
const restrictions: GuestRestriction[] = [
  { id: 'r1', guestPosition: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' },
  { id: 'r2', guestPosition: 3, kind: 'Intolerance', substance: 'lactosa', severity: 'Mild' },
]
const UNACCENTED = /(^|[^\p{L}])(linea|lineas|busqueda|region|mas|anadir|aun|ningun|todavia|esta enviando|envia|enviala|quien|cuantas)($|[^\p{L}])/iu

describe('accesses of «Bodega y bebidas»: «Por copa» first, then the families', () => {
  it('«Por copa» (what the server marks as the glass) leads, the families follow A to Z and «Otros» last, keyed by family', () => {
    const chips = drinkChips(products)
    expect(chips.map(chip => [chip.key, chip.label, chip.count])).toEqual([
      [GLASS_CHIP, 'Por copa', 2], ['family:Cervezas', 'Cervezas', 1], ['family:Vinos', 'Vinos', 2], [OTHER_CHIP, 'Otros', 1]])
    expect(drinkChips(products.filter(product => !product.glass)).map(chip => chip.key)).toEqual(['family:Cervezas', OTHER_CHIP])   // sin copas, sin «Por copa»
  })
  it('the chosen chip stays by its key across a new read; one that is gone falls back to the first', () => {
    const chips = drinkChips(products)
    expect(chosenChip(chips, 'family:Vinos')?.label).toBe('Vinos')
    expect(chosenChip(chips, 'family:Licores')?.key).toBe(GLASS_CHIP)
    expect(chosenChip(chips, null)?.key).toBe(GLASS_CHIP)
    expect(chosenChip([], 'family:Vinos')).toBeNull()
  })
  it('lists the glass wines, a family or one of its subfamilies, and says how many', () => {
    const chips = drinkChips(products)
    const ids = (key: string, sub: string | null = null) => chipProducts(products, chosenChip(chips, key), sub).map(product => product.id)
    expect(ids(GLASS_CHIP)).toEqual(['albarino', 'mencia'])
    expect(ids('family:Vinos')).toEqual(['albarino', 'mencia'])
    expect(ids('family:Vinos', 'Ribeira Sacra')).toEqual(['mencia'])
    expect(ids(OTHER_CHIP)).toEqual(['agua'])
    expect(chipProducts(products, null, null)).toEqual([])
    expect(chipHeader(chips[0], 22)).toBe('22 vinos por copa · de la A a la Z')
    expect(chipHeader(chips[2], 1)).toBe('1 producto · de la A a la Z')
    expect([resultsCount(1), resultsCount(3), RESULTS_WHERE]).toEqual(['1 resultado', '3 resultados', 'en toda la bodega y bebidas'])
    expect(noResults('  zzz ')).toBe('No hay nada con «zzz».')
  })
})

describe('one row per product, one button per presentation', () => {
  it('«Botella 0.75 L» in two heights, «Copa» and «Añadir» in one', () => {
    expect(formatParts('Botella 0.75 L')).toEqual(['Botella', '0.75 L'])
    expect(formatParts('Copa')).toEqual(['Copa', ''])
    expect(formatParts('Añadir')).toEqual(['Añadir', ''])
  })
  it('says what one tap adds, to a screen reader too, without the default presentation', () => {
    expect(addLabel('Guímaro Mencía', 'copa', 'Copa', false)).toBe('Añadir 1 Copa de Guímaro Mencía')
    expect(addLabel('Agua con gas', 'unit', 'Unidad', true)).toBe('Añadir 1 Agua con gas')
    expect(addLabel('Estrella Galicia', 'cana', 'Caña', true)).toBe('Añadir 1 Estrella Galicia · Caña')
  })
  it('a tap that cannot add says why (the limits of the server, or the round is on its way)', () => {
    expect(addProblem('added')).toBe(''); expect(addProblem('more')).toBe('')
    expect(addProblem('full')).toContain('30 líneas'); expect(addProblem('max')).toContain('99')
    expect(addProblem('sending')).toContain('enviando')
  })
})

describe('the round in the tray', () => {
  const round = () => {
    const draft = new Round()
    const albarino = products.find(product => product.id === 'albarino')!, agua = products.find(product => product.id === 'agua')!
    draft.add(roundItem(albarino, albarino.buttons[0])); draft.add(roundItem(albarino, albarino.buttons[0])); draft.add(roundItem(agua, agua.buttons[0]))
    return draft
  }
  it('names its lines without the default presentation', () => {
    expect(roundNames(round().lines)).toBe('Albariño Pazo de Señoráns · Copa, Agua con gas')
  })
  it('the last rejection: the table changed, the bad line the server names, or the code of an unknown one', () => {
    const lines = round().lines
    expect(roundProblem(null, lines)).toBe('')
    expect(roundProblem({ code: 'version_conflict', line: null }, lines)).toBe(ROUND_CONFLICT)
    expect(roundProblem({ code: 'use_add_dish', line: 1 }, lines)).toMatch(/^Línea 2, Agua con gas: Ese producto pasa por cocina/)
    expect(roundProblem({ code: 'use_add_dish', line: 7 }, lines)).toMatch(/^Ese producto pasa por cocina/)          // ya no esta en la ronda
    expect(roundProblem({ code: 'brand_new', line: null }, lines)).toBe('El servidor rechazó la ronda (brand_new). No se ha añadido nada.')
  })
  it('its sheet offers the whole table and every guest, with a dot AND the written restriction of who has one', () => {
    const choices = guestChoices(4, restrictions)
    expect(choices.map(choice => [choice.position, choice.label, choice.restricted])).toEqual([[null, 'Toda la mesa', false], [1, '1', false], [2, '2', true], [3, '3', true], [4, '4', false]])
    expect(choices[2].aria).toBe('Comensal 2, Comensal 2 · ALERGIA frutos secos (grave)')
    expect(restrictionsNote(restrictions)).toBe('Comensal 2 · ALERGIA frutos secos (grave) · Comensal 3 · Intolerancia lactosa (leve)')
    expect(guestChoices(0, [])).toHaveLength(1)
    expect([doneDetail(2, null), doneDetail(1, 3)]).toEqual(['2 · toda la mesa', '1 · comensal 3'])
  })
  it('after its command: confirmed empties it, rejected keeps it with the line, unconfirmed or unauthorized empties it (it IS the pending order)', () => {
    const sent = { path: '/dining/services/s1/commands/add-consumptions', body: '{"expectedVersion":4,"lines":[]}' }
    const cases: Array<[Outcome | null, { path: string; body: string } | null, unknown]> = [
      [{ kind: 'confirmed', response: null }, null, { kind: 'confirmed' }],
      [{ kind: 'rejected', status: 422, code: 'use_add_dish', line: 1 }, null, { kind: 'rejected', code: 'use_add_dish', line: 1 }],
      [{ kind: 'rejected', status: 409, code: 'version_conflict' }, null, { kind: 'rejected', code: 'version_conflict', line: null }],
      [{ kind: 'unconfirmed', reason: 'network' }, sent, { kind: 'unconfirmed' }],
      [{ kind: 'unauthorized' }, sent, { kind: 'unconfirmed' }],
      // Sin resultado: no salio... salvo que ESTA orden haya quedado guardada como la pendiente (entonces es incierta).
      [null, null, { kind: 'not_sent' }],
      [null, { path: sent.path, body: '{"expectedVersion":3,"lines":[]}' }, { kind: 'not_sent' }],
      [null, { path: '/dining/services/s1/commands/pause', body: sent.body }, { kind: 'not_sent' }],
      [null, sent, { kind: 'unconfirmed' }],
    ]
    for (const [outcome, pending, expected] of cases) expect(settlementOf(outcome, pending, sent), JSON.stringify([outcome, pending])).toEqual(expected)
    const draft = round(); draft.begin(4, 'Mesa 1'); draft.settle(settlementOf(null, sent, sent))
    expect(draft.lines).toEqual([])                                          // nunca se queda para mandarla otra vez
    const kept = round(); kept.begin(4, 'Mesa 1'); kept.settle(settlementOf(null, null, sent))
    expect(kept.units).toBe(3)
  })
})

describe('what the table has (no amounts, no totals)', () => {
  const line = (extra: Partial<TableConsumption>): TableConsumption =>
    ({ id: 'c', productId: 'p', presentationId: 'copa', name: 'Guímaro Mencía', presentation: 'Copa', quantity: 1, guestPosition: null, at: null, voided: false, ...extra })
  it('format and guest under the name; the default presentation is not written', () => {
    expect(hadDetail(line({}))).toBe('Copa · Toda la mesa')
    expect(hadDetail(line({ guestPosition: 3 }))).toBe('Copa · Comensal 3')
    expect(hadDetail(line({ presentation: null }))).toBe('Toda la mesa')
    expect(hadDetail(line({ presentation: '  ' }))).toBe('Toda la mesa')
  })
})

describe('the words of «Bodega y bebidas»', () => {
  const texts = [EMPTY_ROUND, ROUND_SENT, ROUND_HINT, ROUND_CONFLICT, HAD_TITLE, HAD_SUB, HAD_EMPTY, HAD_FOOT, VOIDED, ONE_MORE_LETTER, NO_RESULTS_HINT, RESULTS_WHERE,
    addProblem('full'), addProblem('max'), addProblem('sending'), leaveText(3), chipHeader(drinkChips(products)[0], 2), resultsCount(2), noResults('albar'),
    roundProblem({ code: 'too_many_lines', line: null }, []), addLabel('Vino', 'copa', 'Copa', false)]
  it('no checkout word, not even inside another word', () => {
    expect(texts.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
  })
  it('Spanish with accents', () => {
    expect(texts.filter(text => UNACCENTED.test(text))).toEqual([])
    expect(HAD_FOOT).toBe('Los importes solo se ven en Caja.')
    expect(VOIDED).toBe('Anulado en Caja')
  })
})
