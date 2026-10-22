import 'fake-indexeddb/auto'
import { IDBFactory } from 'fake-indexeddb'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { lookAround, orphanOrder, reconcileLegacy } from '../src/adoption'
import { type Fetch } from '../src/api'
import { APPS } from '../src/apps'
import { loadCredential, peekCredential, saveCredential } from '../src/credentials'
import { LEGACY_DATABASE, run, STORES } from '../src/db'
import { MoneyLeakError } from '../src/money-guard'
import { pendingStoreIn, peekPendingRecord, type PendingCommand } from '../src/pending'
import { decideRoute, destination } from '../src/router'
import { UNPAIRED_NOTICE } from '../src/startup'

// C3c, lo guardado separado por aplicacion: costina-sala y costina-cocina, y la base vieja `costina` (C3b y anteriores), que NUNCA se
// borra ni se crea. Con fake-indexeddb y una IDBFactory nueva en cada prueba (como operations.test.ts).
const SALA = APPS.sala.database, COCINA = APPS.cocina.database
const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const session = (role: string, extra: Record<string, unknown> = {}) => ({
  role, actor: 'device:movil-ana', station: role === 'kitchen' ? 'pase' : 'sala-1', stationName: role === 'kitchen' ? 'Pase' : 'Sala 1', tenantId: 't',
  companyId: 'c', locationId: 'l', installationId: 'inst-1', serverVersion: '0.43.0-c3c', demo: false, actions: [], modules: ['dining'], ...extra,
})
let calls: string[] = []
const answering = (answer: (token: string) => Response): Fetch => async (url, init) => {
  const token = String(((init?.headers ?? {}) as Record<string, string>).Authorization ?? '').replace('Bearer ', '')
  calls.push(`${String(url)} ${token}`)
  return answer(token)
}
const offline: Fetch = async () => { throw new TypeError('sin red') }
const credential = (token: string) => ({ token, deviceName: 'movil-ana', pairedAt: '2026-10-21T10:00:00Z' })
const order = (key: string): PendingCommand => ({
  key, path: '/dining/services/s1/commands/serve', body: '{"expectedVersion":7,"courseId":"c2"}', description: 'Servir Principal · Mesa 4',
  createdAt: '2026-10-21T21:00:00Z', installationId: 'inst-1', actor: 'device:movil-ana',
})
const putPending = (database: string, value: unknown) => run(STORES.pending, 'readwrite', store => store.put(value, 'uncertain'), database)
const names = async () => (await indexedDB.databases()).map(database => database.name).sort()

beforeEach(() => { calls = []; vi.stubGlobal('indexedDB', new IDBFactory()) })
afterEach(() => { vi.unstubAllGlobals() })

describe('adopting what C3b stored (lookAround)', () => {
  it('with nothing anywhere: pair, and looking creates no database at all', async () => {
    expect(await lookAround('sala', answering(() => json(500, {})))).toEqual({ kind: 'none' })
    expect(calls).toEqual([])
    expect(await names()).toEqual([])
  })
  it('the old credential of THIS app is adopted with its pending order; the old base keeps the credential and is never deleted', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-serve'))
    const found = await lookAround('sala', answering(() => json(200, session('service'))))
    expect(found).toEqual({ kind: 'adopted', credential: credential('dev.old.s') })
    expect(await loadCredential(SALA)).toEqual(credential('dev.old.s'))
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-serve') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()                     // la orden sale de la vieja POR SU CLAVE
    expect(await peekCredential(LEGACY_DATABASE)).toEqual(credential('dev.old.s'))       // la vieja no se borra (enrutador, version anterior)
    expect(await names()).toEqual([LEGACY_DATABASE, SALA])                               // ni se crea la de la otra aplicacion
    expect(calls).toEqual(['/api/native/v1/session dev.old.s'])
  })
  it('the old main device is still Sala', async () => {
    await saveCredential(credential('dev.main.s'), LEGACY_DATABASE)
    expect((await lookAround('sala', answering(() => json(200, session('main'))))).kind).toBe('adopted')
  })
  it('an old credential of the OTHER role is left where it is: this app does not mount, the other one adopts it', async () => {
    await saveCredential(credential('dev.kds.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-ready'))
    const kitchen = answering(() => json(200, session('kitchen')))
    const found = await lookAround('sala', kitchen)
    expect(found).toMatchObject({ kind: 'other', database: LEGACY_DATABASE, credential: credential('dev.kds.s'), session: { role: 'kitchen', stationName: 'Pase' } })
    expect(await peekCredential(SALA)).toBeNull()
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-ready'))           // su orden, intacta para Cocina
    expect(await lookAround('cocina', kitchen)).toEqual({ kind: 'adopted', credential: credential('dev.kds.s') })
    expect(await pendingStoreIn(() => COCINA).load()).toEqual({ kind: 'restored', command: order('k-ready') })
  })
  it('an unknown role mounts nowhere (fail closed)', async () => {
    await saveCredential(credential('dev.bar.s'), LEGACY_DATABASE)
    const bar = answering(() => json(200, session('bar')))
    expect((await lookAround('sala', bar)).kind).toBe('other')
    expect((await lookAround('cocina', bar)).kind).toBe('other')
    expect(await peekCredential(SALA)).toBeNull(); expect(await peekCredential(COCINA)).toBeNull()
  })
  it('a 401 with the old credential clears it, as always (the only signal), and nothing is adopted', async () => {
    await saveCredential(credential('dev.gone.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-left'))
    expect(await lookAround('sala', answering(() => json(401, { error: 'unauthorized' })))).toEqual({ kind: 'none' })
    expect(await peekCredential(LEGACY_DATABASE)).toBeNull()
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-left'))            // la orden no se borra con la credencial
    expect(await names()).toEqual([LEGACY_DATABASE])
  })
  it('without the server nothing is decided and nothing moves: no network, a 503, money in /session', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-serve'))
    await expect(lookAround('sala', offline)).rejects.toThrow('Servidor sin respuesta')
    await expect(lookAround('sala', answering(() => json(503, { error: 'storage_unavailable' })))).rejects.toMatchObject({ status: 503 })
    await expect(lookAround('sala', answering(() => json(200, session('service', { totalCents: 1200 }))))).rejects.toBeInstanceOf(MoneyLeakError)
    expect(await peekCredential(LEGACY_DATABASE)).toEqual(credential('dev.old.s'))
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-serve'))
    expect(await names()).toEqual([LEGACY_DATABASE])
  })
  it('never replaces an order already in its own slot (D6.7): the old one stays in the old base', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-old'))
    await putPending(SALA, order('k-own'))
    expect((await lookAround('sala', answering(() => json(200, session('service'))))).kind).toBe('adopted')
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-own') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-old'))
  })
  it('an adoption cut short after copying the order is finished on the next start, never duplicated (D6.7)', async () => {
    // Revision C3c: la orden ya estaba copiada en su base (se cerro la pestana antes de guardar la credencial y de soltar la vieja).
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, order('k-serve'))
    await putPending(SALA, order('k-serve'))
    expect((await lookAround('sala', answering(() => json(200, session('service'))))).kind).toBe('adopted')
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-serve') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()                     // la misma orden por su clave: sale de la vieja
  })
  it('two unreadable orders without a key are never taken for the same one: the old one stays', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    await putPending(LEGACY_DATABASE, { path: 'sin clave vieja' })
    await putPending(SALA, { path: 'sin clave propia' })
    await lookAround('sala', answering(() => json(200, session('service'))))
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual({ path: 'sin clave vieja' })
    expect(await peekPendingRecord(SALA)).toEqual({ path: 'sin clave propia' })
  })
  it('an unreadable old order moves as it is: it still blocks until it is reconciled or discarded', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    const unreadable = { key: 'k-bad', path: 'sin barra', body: '{' }
    await putPending(LEGACY_DATABASE, unreadable)
    await lookAround('sala', answering(() => json(200, session('service'))))
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'unreadable', key: 'k-bad' })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()
  })
  it('on Android both apps share the origin: a credential of Costiña Cocina makes Costiña Sala say so instead of pairing', async () => {
    await saveCredential(credential('dev.kds.s'), COCINA)
    const found = await lookAround('sala', answering(() => json(200, session('kitchen'))))
    expect(found).toMatchObject({ kind: 'other', database: COCINA, session: { role: 'kitchen' } })
    expect(await peekCredential(SALA)).toBeNull()
    // Revocada en el servidor: se borra de SU base (el 401 es la unica senal) y Sala ya puede emparejar.
    expect(await lookAround('sala', answering(() => json(401, { error: 'unauthorized' })))).toEqual({ kind: 'none' })
    expect(await peekCredential(COCINA)).toBeNull()
  })
})

describe('what is left in the old base once the app has its own credential (reconcileLegacy, C3c revision)', () => {
  const OWN = credential('dev.old.s')
  it('the credential was saved but the old order not released yet: the next start releases it, never a second copy', async () => {
    await saveCredential(OWN, LEGACY_DATABASE); await putPending(LEGACY_DATABASE, order('k-serve'))
    await saveCredential(OWN, SALA); await putPending(SALA, order('k-serve'))
    await reconcileLegacy(SALA, OWN)
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-serve') })
    expect(await peekCredential(LEGACY_DATABASE)).toEqual(OWN)                            // la base vieja y su credencial no se borran
  })
  it('an order a C3b tab still open saved with the same credential after the adoption moves to the app slot', async () => {
    await saveCredential(OWN, LEGACY_DATABASE); await putPending(LEGACY_DATABASE, order('k-late'))
    await saveCredential(OWN, SALA)
    await reconcileLegacy(SALA, OWN)
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-late') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()
  })
  it('with ANOTHER credential in the old base nothing moves: the order is that credential\'s (the other app adopts it)', async () => {
    await saveCredential(credential('dev.kds.s'), LEGACY_DATABASE); await putPending(LEGACY_DATABASE, order('k-ready'))
    await saveCredential(OWN, SALA)
    await reconcileLegacy(SALA, OWN)
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-ready'))
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'absent' })
  })
  it('never replaces an order already in the app slot, and never creates the old base', async () => {
    await saveCredential(OWN, SALA); await putPending(SALA, order('k-own'))
    await reconcileLegacy(SALA, OWN)
    expect(await names()).toEqual([SALA])
    await saveCredential(OWN, LEGACY_DATABASE); await putPending(LEGACY_DATABASE, order('k-old'))
    await reconcileLegacy(SALA, OWN)
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-own') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-old'))
  })
  it('an order left WITHOUT a credential (a 401 in C3b) is shown before pairing and taken by the app that pairs (D6.7: always seen)', async () => {
    await putPending(LEGACY_DATABASE, order('k-left'))
    expect(await orphanOrder()).toEqual({ kind: 'restored', command: order('k-left') })
    expect(await lookAround('sala', offline)).toEqual({ kind: 'none' })                  // nada que preguntar: se empareja
    expect(await peekPendingRecord(LEGACY_DATABASE)).toEqual(order('k-left'))            // mirar no la mueve: la toma quien se empareja
    await saveCredential(OWN, SALA)
    await reconcileLegacy(SALA, OWN)
    expect(await pendingStoreIn(() => SALA).load()).toEqual({ kind: 'restored', command: order('k-left') })
    expect(await peekPendingRecord(LEGACY_DATABASE)).toBeUndefined()
    expect(await orphanOrder()).toEqual({ kind: 'absent' })
  })
  it('an old order WITH its credential is not an orphan: the adoption decides whose it is', async () => {
    expect(await orphanOrder()).toEqual({ kind: 'absent' })
    expect(await names()).toEqual([])                                                    // mirar no crea la base vieja
    await saveCredential(credential('dev.kds.s'), LEGACY_DATABASE); await putPending(LEGACY_DATABASE, order('k-ready'))
    expect(await orphanOrder()).toEqual({ kind: 'absent' })
  })
})

describe('the /app/ router (C3c)', () => {
  it('a QR code asks sala or cocina, whatever is stored, and passes only the validated code', async () => {
    await saveCredential(credential('dev.old.s'), SALA)
    expect(await decideRoute('#pair=K7M2Q9XR4T', offline)).toEqual({ kind: 'choose', code: 'K7M2Q9XR4T' })
    expect(await decideRoute('#pair=<img src=x>', offline)).toEqual({ kind: 'go', app: 'sala' })
    expect(destination('cocina', 'K7M2Q9XR4T')).toBe('/app/cocina/#pair=K7M2Q9XR4T')
    expect(destination('sala')).toBe('/app/sala/')
  })
  it('a credential in an app base goes there without asking the server, also offline', async () => {
    await saveCredential(credential('dev.kds.s'), COCINA)
    expect(await decideRoute('', offline)).toEqual({ kind: 'go', app: 'cocina' })
    await saveCredential(credential('dev.sala.s'), SALA)
    expect(await decideRoute('', offline)).toEqual({ kind: 'go', app: 'sala' })        // primero Sala, despues Cocina
  })
  it('an old credential goes to the app of its role, which adopts it', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    expect(await decideRoute('', answering(() => json(200, session('kitchen'))))).toEqual({ kind: 'go', app: 'cocina' })
    expect(await decideRoute('', answering(() => json(200, session('main'))))).toEqual({ kind: 'go', app: 'sala' })
    expect(await decideRoute('', answering(() => json(200, session('bar', { stationName: 'Barra' }))))).toEqual({ kind: 'unknown', connected: 'bar · Barra' })
    expect(await peekCredential(SALA)).toBeNull()                                      // el enrutador no adopta: lo hace la aplicacion
  })
  it('a 401 clears the old credential and explains; no server keeps it and retries', async () => {
    await saveCredential(credential('dev.old.s'), LEGACY_DATABASE)
    expect(await decideRoute('', offline)).toMatchObject({ kind: 'offline' })
    expect(await peekCredential(LEGACY_DATABASE)).not.toBeNull()
    expect(await decideRoute('', answering(() => json(401, { error: 'unauthorized' })))).toEqual({ kind: 'none', notice: UNPAIRED_NOTICE })
    expect(await peekCredential(LEGACY_DATABASE)).toBeNull()
  })
  it('with nothing stored it explains how to connect, and creates no database by looking', async () => {
    expect(await decideRoute('', offline)).toEqual({ kind: 'none', notice: '' })
    expect(await names()).toEqual([])
  })
})
