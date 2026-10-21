import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import type { CatalogItem } from '../src/api'
import { REJECTIONS } from '../src/commands'
import { ADD, atThisTable, byGlass, detailFor, drinkProducts, familyChips, inFamily, OTHER_FAMILY, roundItem, searchDrinks, subfamilyChips, type RoundItem } from '../src/drinks'
import { guestLabel } from '../src/labels'
import { MAX_LINES, MAX_QUANTITY, Round, roundLineText, SEND, SEND_AGAIN, type Settlement } from '../src/round'
import { fold, isSearching } from '../src/search'
import type { TableConsumption } from '../src/types'

// C3b: la MISMA tabla de casos que ClientChecks recorre contra Costina.Client (CatalogSearch, DrinksPicker y Round: el selector
// «Añadir consumición» del Comedor del puesto principal). Si search.ts, drinks.ts o round.ts cambian y el puesto no (o al reves),
// falla una de las dos pruebas. Un campo, una orden de ronda o un resultado que esta prueba no conoce es un fallo, nunca se salta.
type Step = { op: string } & Record<string, unknown>
const cases = JSON.parse(readFileSync(fileURLToPath(new URL('../../../dotnet/tests/fixtures/drinks-cases.json', import.meta.url))).toString('utf8')) as {
  catalog: CatalogItem[]
  fold: Array<{ text: string; folded: string }>
  products: Array<{ id: string; name: string; family: string | null; subfamily: string | null; detail: string; glass: boolean; buttons: Array<[string, string]> }>
  families: Array<{ family: string | null; label: string; count: number }>
  subfamilies: Array<{ case: string; family: string | null; expect: Array<[string, number]> }>
  inFamily: Array<{ case: string; family: string | null; subfamily: string | null; expect: string[] }>
  glass: string[]
  search: Array<{ case: string; query: string; expect: string[] | null; details?: string[] }>
  consumptions: TableConsumption[]
  atThisTable: Array<{ item: string; name: string; detail: string }>
  round: Array<{ case: string; steps: Step[] }>
}
const products = drinkProducts(cases.catalog)
const ids = (list: ReadonlyArray<{ id: string }> | null) => list === null ? null : list.map(product => product.id)

function itemOf(key: unknown): RoundItem {
  const [id, presentationId] = String(key).split('/')
  const product = products.find(candidate => candidate.id === id)
  const button = product?.buttons.find(candidate => candidate.presentationId === presentationId)
  if (!product || !button) throw new Error(`item not in the shared catalogue: ${String(key)}`)
  return roundItem(product, button)
}

describe('drinks picker: the case table shared with the main PC (CatalogSearch.cs, DrinksPicker.cs)', () => {
  it.each(cases.fold.map(row => [row.text, row] as const))('fold «%s»', (_, row) => { expect(fold(row.text)).toBe(row.folded) })

  it('one row per product of the bodega section, A to Z, the glass first and one button per presentation («Añadir» if only one)', () => {
    expect(ids(products)).toEqual(cases.products.map(row => row.id))
    for (const [index, row] of cases.products.entries()) {
      const product = products[index]
      expect({ id: product.id, name: product.name, family: product.family, subfamily: product.subfamily, detail: product.detail, glass: product.glass,
        buttons: product.buttons.map(button => [button.presentationId, button.label]) }, row.id).toEqual(row)
    }
  })
  it('family chips, A to Z, with «Otros» last', () => {
    expect(familyChips(products)).toEqual(cases.families)
  })
  it.each(cases.subfamilies.map(row => [row.case, row] as const))('subfamilies: %s', (_, row) => {
    expect(subfamilyChips(products, row.family).map(chip => [chip.name, chip.count])).toEqual(row.expect)
  })
  it.each(cases.inFamily.map(row => [row.case, row] as const))('in a family: %s', (_, row) => {
    expect(ids(inFamily(products, row.family, row.subfamily))).toEqual(row.expect)
  })
  it('«Por copa»: the products the server marks with a glass', () => {
    expect(ids(byGlass(products))).toEqual(cases.glass)
  })
  it.each(cases.search.map(row => [row.case, row] as const))('search: %s', (_, row) => {
    const found = searchDrinks(products, row.query)
    expect(ids(found)).toEqual(row.expect)
    expect(isSearching(row.query)).toBe(row.expect !== null)
    if (row.details) expect(found!.map(product => detailFor(product, row.query))).toEqual(row.details)
  })
  it('«En esta mesa»: what the table already has, once, in order, only if it is still sold in the bodega', () => {
    const repeats = atThisTable(cases.consumptions, products)
    expect(repeats.map(repeat => ({ item: `${repeat.productId}/${repeat.presentationId}`, name: repeat.name, detail: repeat.detail }))).toEqual(cases.atThisTable)
    expect(repeats.map(repeat => roundItem(repeat.product, repeat.button))).toEqual(cases.atThisTable.map(row => itemOf(row.item)))
  })
})

const STATE: Record<string, (round: Round) => unknown> = {
  lines: round => round.lines.map(line => `${line.productId}/${line.presentationId} × ${line.quantity} · ${guestLabel(line.guestPosition)}`),
  texts: round => round.lines.map(roundLineText),
  units: round => round.units,
  summary: round => round.summary,
  sendLabel: round => round.sendLabel,
  sending: round => round.sending,
  conflict: round => round.conflict,
  problem: round => round.problem === null ? null : [round.problem.code, round.problem.line],
  count: round => round.lines.length,
}
const FIELDS: Record<string, string[]> = {
  add: ['item', 'result'], addMany: ['count', 'result'], increment: ['line', 'result'], decrement: ['line', 'result'], remove: ['line', 'result'],
  quantity: ['line', 'value', 'result'], guest: ['line', 'value', 'result'], canSend: ['locked', 'result'], begin: ['version', 'table', 'result'],
  settle: ['kind', 'code', 'line'], state: [...Object.keys(STATE), 'unitsOf'],
}
const SETTLE_KINDS = ['confirmed', 'unconfirmed', 'not_sent', 'rejected']

function apply(round: Round, step: Step): unknown {
  const at = Number(step.line)
  switch (step.op) {
    case 'add': return round.add(itemOf(step.item))
    case 'addMany': {
      let last: unknown
      for (let n = 1; n <= Number(step.count); n++) last = round.add({ productId: `x${n}`, presentationId: 'unit', name: `Producto ${n}`, presentation: 'Unidad' })
      return last
    }
    case 'increment': return round.increment(at)
    case 'decrement': return round.decrement(at)
    case 'remove': return round.remove(at)
    case 'quantity': return round.setQuantity(at, Number(step.value))
    case 'guest': return round.setGuest(at, step.value === null ? null : Number(step.value))
    case 'canSend': return round.canSend(step.locked === true)
    case 'begin': return round.begin(Number(step.version), String(step.table))
    case 'settle': {
      expect(SETTLE_KINDS, `unknown settle kind in the shared cases: ${String(step.kind)}`).toContain(step.kind)
      const result: Settlement = step.kind === 'rejected' ? { kind: 'rejected', code: String(step.code), line: step.line === undefined ? null : Number(step.line) } : { kind: step.kind as 'confirmed' | 'unconfirmed' | 'not_sent' }
      round.settle(result)
      return undefined
    }
    case 'state': {
      for (const [field, value] of Object.entries(step).filter(([field]) => field !== 'op')) {
        if (field === 'unitsOf') {
          for (const [productId, units] of value as Array<[string, number]>) expect(round.unitsOf(productId), `unitsOf ${productId}`).toBe(units)
        } else expect(STATE[field](round), field).toEqual(value)
      }
      return undefined
    }
    default: throw new Error(`unknown round step in the shared cases: ${step.op}`)
  }
}

describe('round: the case table shared with the main PC (Round.cs)', () => {
  it.each(cases.round.map(row => [row.case, row] as const))('%s', (_, row) => {
    const round = new Round()
    for (const [index, step] of row.steps.entries()) {
      expect(Object.keys(FIELDS), `unknown round step: ${step.op}`).toContain(step.op)
      for (const field of Object.keys(step).filter(field => field !== 'op'))
        expect(FIELDS[step.op], `unknown field «${field}» in step ${index} (${step.op})`).toContain(field)
      const result = apply(round, step)
      if ('result' in step) expect(result, `step ${index} (${step.op})`).toEqual(step.result)
    }
  })
  it('the limits are the server ones', () => {
    expect([MAX_LINES, MAX_QUANTITY]).toEqual([30, 99])
  })
})

// La ronda es un borrador EN MEMORIA (plan, seccion 2; D6.3): ni IndexedDB, ni almacenamiento del navegador, ni el almacen de la orden
// incierta. Los tres modulos del selector no importan nada que persista.
describe('the round is never stored', () => {
  const source = (name: string) => readFileSync(fileURLToPath(new URL(`../src/${name}`, import.meta.url))).toString('utf8')
  it.each(['round.ts', 'drinks.ts', 'search.ts'])('%s imports nothing that persists', name => {
    const code = source(name)
    expect(code.length).toBeGreaterThan(0)
    expect([...code.matchAll(/from '([^']+)'/g)].map(match => match[1]).filter(module => ['./db', './pending', './storage', './credentials', './commands', './operations'].includes(module))).toEqual([])
    expect(code).not.toMatch(/indexedDB|localStorage|sessionStorage|caches\.|IDB/)
  })
})

describe('words of the picker and the round', () => {
  const shown = [SEND, SEND_AGAIN, ADD, OTHER_FAMILY, 'Ronda · 3 sin enviar', REJECTIONS.use_add_dish, REJECTIONS.too_many_lines, REJECTIONS.duplicate_consumption,
    ...products.flatMap(product => [product.detail, ...product.buttons.map(button => button.label)])]
  it('the round rejections have a sentence', () => {
    for (const code of ['use_add_dish', 'too_many_lines', 'duplicate_consumption', 'version_conflict']) expect(REJECTIONS[code], code).toBeTruthy()
  })
  it('no checkout word, not even inside another word', () => {
    expect(shown.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
  })
})
