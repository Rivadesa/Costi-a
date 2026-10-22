import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { clockLabel } from '../src/labels'
import { buzz, moreText, noticeSince, noticeTitle, READY_TEXT, readyKey, readyNotices, ReadyWatch, serveLabel, serveRequest, VIBRATION, type Vibrator } from '../src/sala'
import type { BoardEntry, Course, Dining } from '../src/types'

// C3c (plan 2026-10-22, seccion 4.1): el aviso de «listo para servir» de Costina Sala, sin DOM. Sale de las RELECTURAS del tablero y de
// lo que el servidor ANUNCIA (serve), nunca del estado Ready a secas ni del contenido del aviso del canal.
const course = (id: string, name: string, state: Course['state'], actions: string[] = [], readyAt: string | null = null): Course =>
  ({ id, name, state, firedAt: '2026-10-22T19:00:00Z', readyAt, servedAt: null, skipReason: null, preparations: [], actions })
const dining = (id: string, tableId: string, courses: Course[], extra: Partial<Dining> = {}): Dining =>
  ({ id, tableId, pax: 4, state: 'InService', offerId: 'DIA', courses, actions: [], ...extra })
const entry = (service: Dining, version = 12): BoardEntry =>
  ({ version, service, occupancy: { id: `o-${service.id}`, tableId: service.tableId, serviceId: service.id, state: 'Occupied', releasedAt: null, actions: [] }, occupancyVersion: 5 })
// Una mesa con un pase listo que el servidor deja servir (lo que ve un camarero cuando cocina valida).
const ready = (serviceId: string, tableId: string, readyAt: string | null, version = 12, name = 'Principal') =>
  entry(dining(serviceId, tableId, [course('c1', 'Entrantes', 'Served'), course('c2', name, 'Ready', ['serve'], readyAt), course('c3', 'Postre', 'Pending', ['skip'])]), version)
// La misma mesa con el pase aun en cocina.
const cooking = (serviceId: string, tableId: string) =>
  entry(dining(serviceId, tableId, [course('c1', 'Entrantes', 'Served'), course('c2', 'Principal', 'Preparing'), course('c3', 'Postre', 'Pending', ['skip'])]))
const T1 = '2026-10-22T19:19:00Z', T2 = '2026-10-22T19:24:00Z', T3 = '2026-10-22T19:31:00Z'
const keys = (watch: ReadyWatch) => watch.shown.map(notice => notice.key)

describe('which passes announce «listo para servir»', () => {
  it('only the passes where the server announces serve, keyed by service, pass and readyAt', () => {
    const list = readyNotices([ready('s4', 'M4', T1), cooking('s7', 'M7')])
    expect(list.map(notice => notice.key)).toEqual([readyKey('s4', 'c2', T1)])
    expect(readyKey('s4', 'c2', T1)).toBe(`s4:c2:${T1}`)
    expect(list[0].course.name).toBe('Principal')
    expect(list[0].entry.service.tableId).toBe('M4')
  })
  it('a Ready pass WITHOUT serve does not alert: an allergy declared after validating leaves it Ready until the kitchen reviews it', () => {
    const flagged = entry(dining('s4', 'M4', [course('c2', 'Principal', 'Ready', [], T1)], { restrictionsPendingAck: true }))
    expect(readyNotices([flagged])).toEqual([])
  })
  it('the server decides, not the state: a paused table that still announces serve alerts', () => {
    const paused = entry(dining('s4', 'M4', [course('c2', 'Principal', 'Ready', ['serve'], T1)], { state: 'Paused' }))
    expect(readyNotices([paused]).map(notice => notice.key)).toEqual([readyKey('s4', 'c2', T1)])
  })
  it('several at once: the oldest first; without readyAt last; ties by table and pass in ordinal order', () => {
    const list = readyNotices([ready('s6', 'M6', T3), ready('s2', 'M2', null), ready('s5', 'M5', T1), ready('s3', 'M3', T1), ready('s1', 'M1', T2)])
    expect(list.map(notice => notice.entry.service.tableId)).toEqual(['M3', 'M5', 'M1', 'M6', 'M2'])
  })
})

describe('new keys between two good reads (vibration), «Visto» and «Rehacer»', () => {
  it('the first read is not new: what was already ready shows in the banner, without vibrating', () => {
    const watch = new ReadyWatch()
    expect(watch.read([ready('s4', 'M4', T1)])).toEqual([])
    expect(keys(watch)).toEqual([readyKey('s4', 'c2', T1)])
  })
  it('a pass that becomes ready between two reads is new once; the same key in the next read is not', () => {
    const watch = new ReadyWatch()
    expect(watch.read([cooking('s4', 'M4')])).toEqual([])
    expect(watch.shown).toEqual([])
    expect(watch.read([ready('s4', 'M4', T1)])).toEqual([readyKey('s4', 'c2', T1)])
    expect(watch.read([ready('s4', 'M4', T1, 13)])).toEqual([])                       // otra lectura, la misma clave: nada nuevo
    expect(keys(watch)).toEqual([readyKey('s4', 'c2', T1)])
  })
  it('the banner follows the read: a served pass leaves it, and the entry of the notice is always the LAST read (F01)', () => {
    const watch = new ReadyWatch()
    watch.read([ready('s4', 'M4', T1, 12)])
    watch.read([ready('s4', 'M4', T1, 15)])
    expect(watch.shown[0].entry.version).toBe(15)
    const served = entry(dining('s4', 'M4', [course('c2', 'Principal', 'Served')]), 16)
    expect(watch.read([served])).toEqual([])
    expect(watch.shown).toEqual([])
  })
  it('«Visto» hides that key in this device only, remembered in memory while the read still has it; the others still show', () => {
    const watch = new ReadyWatch()
    watch.read([ready('s4', 'M4', T1), ready('s6', 'M6', T2)])
    watch.dismiss(readyKey('s4', 'c2', T1))
    expect(keys(watch)).toEqual([readyKey('s6', 'c2', T2)])
    expect(watch.read([ready('s4', 'M4', T1), ready('s6', 'M6', T2)])).toEqual([])
    expect(keys(watch)).toEqual([readyKey('s6', 'c2', T2)])
    // Una clave que aun no esta en la lectura no se puede dar por vista: cuando llega, avisa y se ve.
    watch.dismiss(readyKey('s9', 'c2', T3))
    expect(watch.read([ready('s4', 'M4', T1), ready('s6', 'M6', T2), ready('s9', 'M9', T3)])).toEqual([readyKey('s9', 'c2', T3)])
    expect(keys(watch)).toEqual([readyKey('s6', 'c2', T2), readyKey('s9', 'c2', T3)])
  })
  it('«Rehacer» clears readyAt, so the next validation is a NEW key: it alerts again even after «Visto»', () => {
    const watch = new ReadyWatch()
    watch.read([ready('s4', 'M4', T1)])
    watch.dismiss(readyKey('s4', 'c2', T1))
    expect(watch.shown).toEqual([])
    expect(watch.read([cooking('s4', 'M4')])).toEqual([])                              // rehacer: el pase vuelve a cocina
    expect(watch.read([ready('s4', 'M4', T3)])).toEqual([readyKey('s4', 'c2', T3)])     // validado otra vez
    expect(keys(watch)).toEqual([readyKey('s4', 'c2', T3)])
  })
  it('«Rehacer» and the new validation between the SAME two reads (no read in between): readyAt makes it a new key anyway', () => {
    const watch = new ReadyWatch()
    watch.read([cooking('s4', 'M4')])
    watch.read([ready('s4', 'M4', T1)])
    watch.dismiss(readyKey('s4', 'c2', T1))
    expect(watch.read([ready('s4', 'M4', T3)])).toEqual([readyKey('s4', 'c2', T3)])
    expect(keys(watch)).toEqual([readyKey('s4', 'c2', T3)])
  })
  it('an allergy declared after validating: no banner until it is reviewed, then it comes back AND vibrates, even after «Visto»', () => {
    const watch = new ReadyWatch()
    watch.read([cooking('s4', 'M4')])
    expect(watch.read([ready('s4', 'M4', T1)])).toEqual([readyKey('s4', 'c2', T1)])
    watch.dismiss(readyKey('s4', 'c2', T1))
    const flagged = entry(dining('s4', 'M4', [course('c2', 'Principal', 'Ready', [], T1)], { restrictionsPendingAck: true }))
    expect(watch.read([flagged])).toEqual([])
    expect(watch.shown).toEqual([])
    expect(watch.read([ready('s4', 'M4', T1)])).toEqual([readyKey('s4', 'c2', T1)])     // revisado: la misma clave vuelve a avisar
    expect(keys(watch)).toEqual([readyKey('s4', 'c2', T1)])
  })
  it('several at once: the oldest on top, and the rest named in «+N más»', () => {
    const watch = new ReadyWatch()
    watch.read([])
    expect(watch.read([ready('s6', 'M6', T3, 12, 'Postre'), ready('s4', 'M4', T1), ready('s2', 'M2', T2, 12, 'Aperitivo')]))
      .toEqual([readyKey('s4', 'c2', T1), readyKey('s2', 'c2', T2), readyKey('s6', 'c2', T3)])
    const names: Record<string, string> = { M2: 'Mesa 2', M4: 'Mesa 4', M6: 'Terraza 6' }
    expect(watch.shown.map(notice => noticeTitle(notice, names[notice.entry.service.tableId]))).toEqual(['Mesa 4 · Principal', 'Mesa 2 · Aperitivo', 'Terraza 6 · Postre'])
    expect(moreText(watch.shown.slice(1), id => names[id])).toBe('Mesa 2 · Aperitivo y Terraza 6 · Postre')
    expect(moreText(watch.shown.slice(2), id => names[id])).toBe('Terraza 6 · Postre')
    expect(moreText([ready('a', 'A', T1), ready('b', 'B', T2), ready('c', 'C', T3)].flatMap(row => readyNotices([row])), id => id)).toBe('A · Principal, B · Principal y C · Principal')
  })
})

describe('words and the command of «Servido»', () => {
  it('the banner says the table NAME and the pass, and when the pass validated it, in local time', () => {
    const [notice] = readyNotices([ready('s4', 'M4', T1)])
    expect(noticeTitle(notice, 'Mesa 4')).toBe('Mesa 4 · Principal')
    expect(READY_TEXT).toBe('listo para servir')
    expect(noticeSince(notice)).toBe(`El pase lo validó a las ${clockLabel(T1)}`)
    expect(noticeSince(readyNotices([ready('s4', 'M4', null)])[0])).toBe('')               // sin hora del servidor no se inventa
    expect(serveLabel(notice)).toBe('Servido · Principal')
  })
  it('«Servido» is ONE serve of that pass with the version READ of that table (F01), described by its name', () => {
    const [notice] = readyNotices([ready('svc/4', 'M4', T1, 21)])
    expect(serveRequest(notice, 'Mesa 4')).toEqual({
      path: '/dining/services/svc%2F4/commands/serve', body: { expectedVersion: 21, courseId: 'c2' }, description: 'Marcar servido · Principal · Mesa 4',
    })
  })
  it('without serve announced on that pass there is no command', () => {
    const [notice] = readyNotices([ready('s4', 'M4', T1)])
    const stale = { ...notice, entry: entry(dining('s4', 'M4', [course('c2', 'Principal', 'Ready', [], T1)])) }
    expect(serveRequest(stale, 'Mesa 4')).toBeNull()
  })
})

describe('vibration: Android with Chrome, with the page in sight and after a first touch', () => {
  const device = (activated?: boolean) => {
    const calls: number[][] = []
    const vibrator: Vibrator = { vibrate: pattern => { calls.push([...pattern]); return true }, ...(activated === undefined ? {} : { userActivation: { hasBeenActive: activated } }) }
    return { vibrator, calls }
  }
  it('vibrates once for new keys with the page visible', () => {
    const { vibrator, calls } = device(true)
    expect(buzz(['k1', 'k2'], true, vibrator)).toBe(true)
    expect(calls).toEqual([[...VIBRATION]])
  })
  it('nothing new, page hidden, no vibrate (iPhone, iPad) or no touch yet since the page loaded: no call at all', () => {
    for (const [fresh, visible, activated] of [[[], true, true], [['k1'], false, true], [['k1'], true, false]] as const) {
      const { vibrator, calls } = device(activated)
      expect(buzz(fresh, visible, vibrator)).toBe(false)
      expect(calls).toEqual([])
    }
    expect(buzz(['k1'], true, {})).toBe(false)
    expect(buzz(['k1'], true, undefined)).toBe(false)
  })
  it('a browser without userActivation decides by itself; one that throws never breaks the read', () => {
    const { vibrator, calls } = device()
    expect(buzz(['k1'], true, vibrator)).toBe(true)
    expect(calls.length).toBe(1)
    expect(buzz(['k1'], true, { vibrate: () => { throw new Error('no') } })).toBe(false)
  })
})

describe('sala.ts has no DOM and no storage', () => {
  it('imports no Vue, no storage, no commands; never touches document, window or navigator by itself', () => {
    const source = readFileSync(fileURLToPath(new URL('../src/sala.ts', import.meta.url))).toString('utf8').replace(/^\s*\/\/.*$/gm, '')
    expect([...source.matchAll(/from '([^']+)'/g)].map(match => match[1]).sort()).toEqual(['./ficha', './labels', './types'])
    expect(source).not.toMatch(/\b(document|window|navigator|localStorage|indexedDB)\b/)
  })
})
