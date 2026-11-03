import 'fake-indexeddb/auto'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  clearOpenDraft, clearRoundDraft, loadOpenDraft, loadRoundDraft, parseOpenDraft, parseRoundDraft, RECOVER_OPEN_NO, RECOVER_OPEN_TITLE, RECOVER_OPEN_YES,
  RECOVER_ROUND_NO, RECOVER_ROUND_TITLE, RECOVER_ROUND_YES, recoverOpenText, recoverRoundLines, recoverRoundText, saveOpenDraft, saveRoundDraft,
} from '../src/borrador'
import { peek, STORES } from '../src/db'
import { loadCredential } from '../src/credentials'
import { indexedDbPendingStore } from '../src/pending'
import type { RoundLine } from '../src/round'

// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01; decision 8b): la ronda sin enviar y la mesa a medio abrir se guardan en
// este movil (IndexedDB, almacen `pending`, claves propias) y se recuperan solo cuando la persona lo dice. Fallo cerrado al leer.

const LINE: RoundLine = { productId: 'albarino', presentationId: 'copa', name: 'Albariño', presentation: 'Copa', quantity: 2, guestPosition: null }
const OTHER: RoundLine = { ...LINE, productId: 'agua', presentationId: 'unit', name: 'Agua', presentation: '', quantity: 1, guestPosition: 3 }
const DB = 'costina-borrador-test'
const AT = '2026-11-03T20:14:00.000Z'

describe('a round draft is read fail-closed', () => {
  it('accepts exactly a round of this service, with lines inside the limits, and copies them', () => {
    const draft = parseRoundDraft({ kind: 'round', serviceId: 's-1', lines: [LINE, OTHER], savedAt: AT }, 's-1')
    expect(draft).toEqual({ kind: 'round', serviceId: 's-1', lines: [LINE, OTHER], savedAt: AT })
    expect(parseRoundDraft({ kind: 'round', serviceId: 's-1', lines: [LINE] }, 's-1')?.savedAt).toBe('')
  })
  it('anything else counts as absent: another service, another kind, no lines, a quantity or a guest out of range, a missing id', () => {
    const base = { kind: 'round', serviceId: 's-1', lines: [LINE], savedAt: AT }
    for (const bad of [
      null, undefined, 'x', { ...base, serviceId: 's-2' }, { ...base, kind: 'open' }, { ...base, lines: [] }, { ...base, lines: 'x' },
      { ...base, lines: [{ ...LINE, quantity: 0 }] }, { ...base, lines: [{ ...LINE, quantity: 100 }] }, { ...base, lines: [{ ...LINE, quantity: 1.5 }] },
      { ...base, lines: [{ ...LINE, guestPosition: 0 }] }, { ...base, lines: [{ ...LINE, productId: '' }] }, { ...base, lines: [null] },
      { ...base, lines: Array.from({ length: 31 }, () => LINE) },
    ]) expect(parseRoundDraft(bad, 's-1'), JSON.stringify(bad)?.slice(0, 80)).toBeNull()
  })
})

describe('an open-table draft is read fail-closed', () => {
  const ROW = { guest: 2, kind: 'Allergy', substance: 'frutos secos', severity: 'Severe' } as const
  it('accepts exactly a draft of this table: people, menu and rows with known codes; something has to be there', () => {
    expect(parseOpenDraft({ kind: 'open', tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', rows: [ROW], savedAt: AT }, 'M5'))
      .toEqual({ kind: 'open', tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', rows: [ROW], savedAt: AT })
    expect(parseOpenDraft({ kind: 'open', tableId: 'M5', rows: [{ guest: 'all', kind: 'Preference', substance: '', severity: 'Mild' }] }, 'M5')?.rows[0].guest).toBe('all')
    expect(parseOpenDraft({ kind: 'open', tableId: 'M5', pax: 2, rows: [] }, 'M5')?.pax).toBe(2)
    expect(parseOpenDraft({ kind: 'open', tableId: 'M5', rows: [] }, 'M5')).toBeNull()                       // nada a medio abrir
  })
  it('anything else counts as absent: another table, a kind or severity the server does not know, people out of range', () => {
    const base = { kind: 'open', tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', rows: [ROW], savedAt: AT }
    for (const bad of [
      null, 7, { ...base, tableId: 'M6' }, { ...base, kind: 'round' }, { ...base, pax: 0 }, { ...base, pax: 61 }, { ...base, pax: 'tres' },
      { ...base, rows: [{ ...ROW, kind: 'Allergic' }] }, { ...base, rows: [{ ...ROW, severity: 'Deadly' }] }, { ...base, rows: [{ ...ROW, guest: -1 }] },
      { ...base, rows: [{ ...ROW, substance: 'x'.repeat(201) }] }, { ...base, rows: 'x' }, { ...base, rows: [null] }, { ...base, offerId: 'x'.repeat(129) },
    ]) expect(parseOpenDraft(bad, 'M5'), JSON.stringify(bad)?.slice(0, 80)).toBeNull()
  })
})

describe('the store: saved until sent, opened or discarded; never touches the credential nor the uncertain command', () => {
  it('round: save, load, overwrite, clear; absent when nothing was saved', async () => {
    expect(await loadRoundDraft('s-1', DB)).toBeNull()
    await saveRoundDraft('s-1', [LINE], new Date(AT), DB)
    expect(await loadRoundDraft('s-1', DB)).toEqual({ kind: 'round', serviceId: 's-1', lines: [LINE], savedAt: AT })
    expect(await loadRoundDraft('s-2', DB)).toBeNull()
    await saveRoundDraft('s-1', [LINE, OTHER], new Date(AT), DB)
    expect((await loadRoundDraft('s-1', DB))?.lines).toEqual([LINE, OTHER])
    await clearRoundDraft('s-1', DB)
    expect(await loadRoundDraft('s-1', DB)).toBeNull()
    await clearRoundDraft('s-1', DB)                                                              // borrar lo que no esta no rompe nada
  })
  it('open: save, load, clear; what is saved is a plain copy', async () => {
    const rows = [{ guest: 2 as const, kind: 'Allergy' as const, substance: 'gluten', severity: 'Severe' as const }]
    await saveOpenDraft('M5', { pax: 3, offerId: 'LAB-TASTING', rows }, new Date(AT), DB)
    rows[0].substance = 'cambiada despues'
    expect(await loadOpenDraft('M5', DB)).toEqual({ kind: 'open', tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', rows: [{ guest: 2, kind: 'Allergy', substance: 'gluten', severity: 'Severe' }], savedAt: AT })
    expect(await loadOpenDraft('M6', DB)).toBeNull()
    await clearOpenDraft('M5', DB)
    expect(await loadOpenDraft('M5', DB)).toBeNull()
  })
  it('lives in the pending store under its own keys: the uncertain slot and the credential stay as they were', async () => {
    await saveRoundDraft('s-9', [LINE], new Date(AT), DB)
    await saveOpenDraft('M9', { pax: null, offerId: '', rows: [{ guest: null, kind: 'Allergy', substance: 'x', severity: 'Severe' }] }, new Date(AT), DB)
    expect(await peek<unknown>(DB, STORES.pending, store => store.get('uncertain'))).toBeUndefined()
    expect(await loadCredential(DB)).toBeNull()
    expect(await peek<IDBValidKey[]>(DB, STORES.pending, store => store.getAllKeys())).toEqual(expect.arrayContaining(['draft:open:M9', 'draft:round:s-9']))
    expect(indexedDbPendingStore).toBeDefined()                                                   // el hueco de la orden sigue siendo pending.ts
  })
})

describe('the words of the question', () => {
  it('round: how many are unsent, when it was saved here, and that nothing reached the server; each line whole', () => {
    const draft = { kind: 'round' as const, serviceId: 's-1', lines: [LINE, OTHER], savedAt: AT }
    expect(recoverRoundText(draft)).toMatch(/^Tenías 3 sin enviar en esta mesa, guardados en este móvil a las \d\d:\d\d\. Nada de ello ha llegado al servidor\.$/)
    expect(recoverRoundText({ ...draft, savedAt: '' })).toBe('Tenías 3 sin enviar en esta mesa, guardados en este móvil. Nada de ello ha llegado al servidor.')
    expect(recoverRoundLines(draft)).toEqual([{ text: '2 × Albariño · Copa', alarming: false }, { text: '1 × Agua', alarming: false }])
    expect([RECOVER_ROUND_TITLE, RECOVER_ROUND_YES, RECOVER_ROUND_NO]).toEqual(['¿Recuperas la ronda sin enviar?', 'Recuperar la ronda', 'Descartarla'])
  })
  it('open: people, menu and how many allergies, only what there is', () => {
    const rows = [{ guest: 2 as const, kind: 'Allergy' as const, substance: 'gluten', severity: 'Severe' as const }]
    expect(recoverOpenText({ kind: 'open', tableId: 'M5', pax: 3, offerId: 'LAB-TASTING', rows, savedAt: '' }, 'Menú del día'))
      .toBe('Tenías esta mesa a medio abrir, guardada en este móvil: 3 personas · Menú del día · 1 alergia o intolerancia. No se ha abierto: nada ha llegado al servidor.')
    expect(recoverOpenText({ kind: 'open', tableId: 'M5', pax: null, offerId: '', rows: [...rows, ...rows], savedAt: AT }, ''))
      .toMatch(/^Tenías esta mesa a medio abrir, guardada en este móvil a las \d\d:\d\d: 2 alergias o intolerancias\. No se ha abierto/)
    expect(recoverOpenText({ kind: 'open', tableId: 'M5', pax: 1, offerId: '', rows: [], savedAt: '' }, '')).toContain(': 1 persona.')
    expect([RECOVER_OPEN_TITLE, RECOVER_OPEN_YES, RECOVER_OPEN_NO]).toEqual(['¿Sigues abriendo esta mesa?', 'Seguir abriendo', 'Empezar de cero'])
  })
})

// round.ts y abrir.ts siguen sin guardar nada (drinks.test.ts y abrir.test.ts lo exigen): el unico que persiste es este modulo, y solo
// borradores, nunca una orden.
describe('only borrador.ts persists, and it never sends anything', () => {
  it('imports db.ts and nothing of the network or of the uncertain command', () => {
    const code = readFileSync(fileURLToPath(new URL('../src/borrador.ts', import.meta.url))).toString('utf8')
    const modules = [...code.matchAll(/from '([^']+)'/g)].map(match => match[1])
    expect(modules).toContain('./db')
    expect(modules.filter(module => ['./api', './commands', './operations', './pending', './credentials', './realtime'].includes(module))).toEqual([])
    expect(code).not.toMatch(/fetch\(|XMLHttpRequest|navigator\./)
  })
})
