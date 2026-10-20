import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { alert, alertIsAlarming, alertWord, courseDetail, courseSteps, isTileCode, TILE_CODES, tileElapsed, tileLabel, tileProgress, tileSince, tileState } from '../src/floor'
import { clockLabel, courseStateLabel } from '../src/labels'
import type { Course, Dining } from '../src/types'

// C3a: la MISMA tabla de casos que ClientChecks recorre contra Costina.Client/FloorPlan.cs (el Comedor del puesto principal).
// Si floor.ts cambia y FloorPlan.cs no (o al reves), falla una de las dos pruebas. Cada caso compara solo los campos que trae;
// un campo que esta prueba no conoce es un fallo, nunca se salta.
const cases = JSON.parse(readFileSync(fileURLToPath(new URL('../../../dotnet/tests/fixtures/floorplan-cases.json', import.meta.url))).toString('utf8')) as {
  now: string
  tiles: Array<{ case: string; service: Dining | null } & Record<string, unknown>>
  courses: Array<{ case: string; pax: number; course: Course; label: string; detail: string }>
}
const now = Date.parse(cases.now)

const FIELDS: Record<string, (service: Dining | null) => unknown> = {
  state: tileState,
  label: service => tileLabel(tileState(service)),
  elapsed: service => tileElapsed(service, now),
  progress: tileProgress,
  steps: courseSteps,
  alert,
  alertWord,
  alarming: alertIsAlarming,
}

describe('floor plan: the case table shared with the main PC (FloorPlan.cs)', () => {
  it.each(cases.tiles.map(tile => [tile.case, tile] as const))('%s', (_, tile) => {
    const fields = Object.keys(tile).filter(field => field !== 'case' && field !== 'service')
    expect(fields.length, 'a shared case that compares nothing').toBeGreaterThan(0)
    for (const field of fields) {
      expect(Object.keys(FIELDS), `unknown field in the shared cases: ${field}`).toContain(field)
      expect(FIELDS[field](tile.service), field).toEqual(tile[field])
    }
  })
  it('covers every tile code, so a new state cannot slip in untested', () => {
    const covered = new Set(cases.tiles.map(tile => tile.state).filter(state => typeof state === 'string'))
    expect(TILE_CODES.filter(code => !covered.has(code))).toEqual([])
  })
  // {firedAt}, {readyAt} y {servedAt}: la hora LOCAL de ese campo, con el reloj de este cliente (en el puesto, el suyo).
  it.each(cases.courses.map(row => [row.case, row] as const))('course: %s', (_, row) => {
    const at = (value: string | null, field: string) => value ? clockLabel(value) : `{${field}}`
    const detail = row.detail.replace('{firedAt}', at(row.course.firedAt, 'firedAt')).replace('{readyAt}', at(row.course.readyAt, 'readyAt'))
      .replace('{servedAt}', at(row.course.servedAt, 'servedAt'))
    expect(courseStateLabel(row.course, row.pax)).toBe(row.label)
    expect(courseDetail(row.course, row.pax)).toBe(detail)
  })
})

describe('floor plan: presentation only, never invented', () => {
  it('only the tile codes are translated, by exact comparison', () => {
    expect(TILE_CODES.every(isTileCode)).toBe(true)
    expect(isTileCode('Ready')).toBe(false); expect(tileLabel('Ready')).toBe('Ready'); expect(tileLabel('ready')).toBe('Listo para servir')
  })
  it('has no time for a free, seated, paused or finished table', () => {
    for (const tile of cases.tiles.filter(tile => ['free', 'seated', 'paused', 'finished', 'cancelled'].includes(String(tile.state))))
      expect(tileSince(tile.service), tile.case).toBeNull()
  })
})
