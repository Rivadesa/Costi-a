import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { REJECTIONS } from '../src/commands'
import { DB_TEXTS } from '../src/db'
import { CONFLICT_TEXT, DONE_BAND, elapsedText, exceptionLine, NOTE_SHORTCUTS, PRESEND_DECISIONS, REVIEW_DECISIONS, REVIEW_PENDING_AT_PASS } from '../src/cocina'
import {
  actionLabel, ageLabel, ageShort, blockedTitle, courseLabel, diningLabel, guestLabel, minutesLabel, offerKindLabel, otherActorAdvice, pairPendingAdvice, plural,
  preparationLabel, restrictionKindLabel, restrictionLabel, reviewDecisionLabel, reviewOutcome, severityLabel, stationKindLabel, tableStateLabel,
  unpairWarning, VOCABULARY,
} from '../src/labels'
import { LINK_TEXT } from '../src/live'
import { MoneyLeakError } from '../src/money-guard'
import { UNPAIRED_NOTICE } from '../src/startup'
import { PERSISTENCE_HINT } from '../src/storage'
import type { GuestRestriction, Preparation } from '../src/types'

// C3a: la MISMA tabla de casos que ClientChecks recorre contra Costina.Client/Labels.cs: el comandero habla como el puesto.
const cases = JSON.parse(readFileSync(fileURLToPath(new URL('../../../dotnet/tests/fixtures/vocabulary-cases.json', import.meta.url))).toString('utf8')) as {
  families: Record<string, Record<string, string>>
  guests: Array<{ position: number | null; text: string }>
  restrictions: Array<{ restriction: GuestRestriction; text: string }>
  minutes: Array<{ minutes: number; text: string }>
  plural: Array<{ count: number; one: string; many: string; text: string }>
  reviews: Array<{ case: string; preparation: Preparation; text: string }>
}
const FAMILIES: Record<string, (value: string) => string> = {
  dining: diningLabel, course: courseLabel, preparation: preparationLabel, tableState: tableStateLabel, offerKind: offerKindLabel,
  stationKind: stationKindLabel, restrictionKind: restrictionKindLabel, severity: severityLabel, review: reviewDecisionLabel, action: actionLabel,
}

describe('vocabulary: the case table shared with the main PC (Labels.cs)', () => {
  it.each(Object.entries(cases.families))('%s', (family, rows) => {
    expect(Object.keys(FAMILIES), `unknown family in the shared cases: ${family}`).toContain(family)
    for (const [code, text] of Object.entries(rows)) expect(FAMILIES[family](code), code).toBe(text)
  })
  it('labels.ts has no family and no word that the shared table does not have', () => {
    expect(Object.keys(VOCABULARY).sort()).toEqual(Object.keys(cases.families).sort())
    for (const [family, rows] of Object.entries(VOCABULARY))
      for (const [code, text] of Object.entries(rows)) expect(cases.families[family][code], `${family}.${code}`).toBe(text)
  })
  it('guests, restrictions, minutes and plurals', () => {
    for (const row of cases.guests) expect(guestLabel(row.position)).toBe(row.text)
    for (const row of cases.restrictions) expect(restrictionLabel(row.restriction)).toBe(row.text)
    for (const row of cases.minutes) expect(minutesLabel(row.minutes * 60_000), String(row.minutes)).toBe(row.text)
    for (const row of cases.plural) expect(plural(row.count, row.one, row.many)).toBe(row.text)
  })
  // E4e: lo que la sala lee de la decision de cocina sobre un plato, igual que el puesto (Labels.ReviewOutcome).
  it('what the room reads of a kitchen review', () => {
    expect(cases.reviews.length).toBeGreaterThan(0)
    for (const row of cases.reviews) {
      expect(Object.keys(row).filter(key => !['case', 'preparation', 'text'].includes(key)), `unknown field in the shared cases: ${row.case}`).toEqual([])
      expect(reviewOutcome(row.preparation), row.case).toBe(row.text)
    }
  })
})

describe('vocabulary of the handheld', () => {
  it('station kinds arrive lowercase from /configuration and are named, not shown as codes', () => {
    expect(['kitchen', 'pass', 'bar', 'room'].map(stationKindLabel)).toEqual(['Cocina', 'Pase', 'Barra', 'Sala'])
  })
  it('never labels a checkout action: it would come back raw, never as a word of the till', () => {
    for (const action of ['payment', 'void-charge', 'refund', 'close', 'reopen', 'add-product']) expect(actionLabel(action)).toBe(action)
  })
  it('nothing read is ever shown as an empty word, and an absent value is empty', () => {
    expect(diningLabel(null)).toBe(''); expect(courseLabel(undefined)).toBe(''); expect(diningLabel('Closing')).toBe('Closing')
  })
  it('the kitchen writes a review with the words of Sala and the PC (C3d): «Adaptado: …», and «Por revisar en el pase» while pending', () => {
    const base: Preparation = { id: 'p1', name: 'Tosta', stationId: 'cold', quantity: 1, guestPosition: 3, mandatory: true, state: 'Fired' }
    const adapted: Preparation = { ...base, review: { decision: 'Adapt', note: 'pan de maíz', at: '2026-10-20T12:00:00Z' } }
    expect(exceptionLine(adapted).text).toBe(`Comensal 3 · ${reviewOutcome(adapted)}`)
    expect(reviewOutcome(adapted)).toBe('Adaptado: pan de maíz')
    expect(exceptionLine({ ...base, reviewPending: true }).text).toBe('Comensal 3 · Por revisar en el pase')
  })
})

// Lo que el comandero y la cocina pueden llegar a pintar desde la logica (las vistas, aparte): sin palabras de caja, ni siquiera
// dentro de otra palabra (regla de kitchen.spec.ts), y sin las formas sin tilde que tenia la PWA antes de C3a.
const SHOWN = [
  ...Object.values(VOCABULARY).flatMap(rows => Object.values(rows) as string[]),
  ...Object.values(REJECTIONS), ...Object.values(LINK_TEXT), ...Object.values(PERSISTENCE_HINT), UNPAIRED_NOTICE,
  blockedTitle({ reason: 'actor', actor: 'device:tablet' }), blockedTitle({ reason: 'foreign' }), blockedTitle({ reason: 'unreadable' }),
  otherActorAdvice('device:tablet'), pairPendingAdvice('tablet'), unpairWarning({ kind: 'unreadable', key: 'k' }),
  unpairWarning({ kind: 'restored', command: { key: 'k', path: '/x', body: '{}', description: 'Servir', createdAt: '', installationId: 'i', actor: 'device:tablet' } }),
  new MoneyLeakError('$.x').message, ...REVIEW_DECISIONS.map(option => option.label),
  // E4e: lo que la sala lee de una revision y las decisiones del pase antes de enviar.
  ...cases.reviews.map(row => row.text), ...PRESEND_DECISIONS.map(option => option.label),
  // C3d: las palabras nuevas de Costina Cocina (el resto, en cocina.test.ts, que mira todos los literales de cocina.ts).
  REVIEW_PENDING_AT_PASS, DONE_BAND, elapsedText(null, 0), CONFLICT_TEXT, ...NOTE_SHORTCUTS.map(shortcut => shortcut.label),
  // Los errores de IndexedDB llegan al aviso «Servidor no listo (…)» de la pantalla de arranque (startup.ts).
  ...Object.values(DB_TEXTS), ageLabel(null), ageLabel(75), ageShort(null), ageShort(5),
]
const UNACCENTED = ['aplicacion', 'aplico', 'revision', 'accion', 'estacion', 'codigo', 'conexion', 'envio', 'preparacion', 'eleccion', 'menu',
  'restriccion', 'elaboracion', 'economicos', 'desvinculo', 'restauro', 'instalala', 'usala', 'asi', 'pestana', 'version', 'transaccion',
  'leido', 'ultima']

describe('words on screen', () => {
  it('no checkout word, not even inside another word', () => {
    expect(SHOWN.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
  })
  it('Spanish with accents: none of the unaccented forms the PWA used to show', () => {
    const found = SHOWN.flatMap(text => UNACCENTED.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)).map(word => `${word}: ${text}`))
    expect(found).toEqual([])
  })
})
