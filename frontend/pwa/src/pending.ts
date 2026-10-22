import { currentDatabase, peek, run, STORES, transact } from './db'

// La ORDEN INCIERTA de este dispositivo (mismo modelo que el cliente Windows, D3.3/D3.4):
// exactamente UNA, persistida ANTES del primer intento con sus bytes exactos y su Idempotency-Key, y
// borrada solo con exito confirmado o rechazo definitivo. No es una cola de trabajo sin conexion: los
// comandos llevan expectedVersion y, encolados, llegarian obsoletos del segundo en adelante.
// D6.7: el hueco es UNO por base y lo comparten todas las pestanas y ventanas de la aplicacion (C3c: una base por aplicacion,
// db.ts). Reservarlo y liberarlo se hace comprobando y escribiendo en la MISMA transaccion de IndexedDB: ninguna pestana atrasada
// sustituye ni borra la orden de otra, con o sin Web Locks.
export interface PendingCommand {
  key: string            // Idempotency-Key: la identidad de la orden ante el servidor
  path: string           // ruta relativa a /api/native/v1
  body: string           // bytes EXACTOS enviados; el reintento reenvia esto, no lo reconstruye
  description: string    // lo que ve la persona: "Servir pase Aperitivos · M3"
  createdAt: string
  installationId: string // una orden de OTRA instalacion se informa y jamas se reenvia
  actor: string          // el servidor guarda la idempotencia POR ACTOR: la de otro actor jamas se reenvia (D6.7)
}

export type PendingLoad =
  | { kind: 'absent' }
  | { kind: 'restored'; command: PendingCommand }
  | { kind: 'unreadable'; key: string | null }   // fallo cerrado: bloquea hasta conciliar o descartar de forma explicita

export interface PendingStore {
  load(): Promise<PendingLoad>
  // Reserva ATOMICA del unico hueco: si ya hay una orden guardada (de esta u otra pestana, legible o no) NO escribe
  // nada y devuelve esa; 'absent' = guardada. Nunca sustituye una orden ajena.
  reserve(command: PendingCommand): Promise<PendingLoad>
  clear(key: string): Promise<void>               // solo borra si lo guardado lleva ESA clave (en la misma transaccion)
  discard(key: string | null): Promise<void>      // decision explicita de la persona, nunca automatica; tambien solo ESA clave
}

const KEY = 'uncertain'
const isText = (value: unknown, max: number): value is string => typeof value === 'string' && value.length > 0 && value.length <= max

export function parsePending(stored: unknown): PendingLoad {
  if (stored === undefined || stored === null) return { kind: 'absent' }
  const candidate = stored as Partial<PendingCommand>
  const key = typeof candidate === 'object' && isText(candidate.key, 128) ? candidate.key : null
  if (typeof candidate === 'object' && key && isText(candidate.path, 512) && candidate.path.startsWith('/') && isText(candidate.body, 65536)
    && isText(candidate.description, 512) && isText(candidate.installationId, 64) && isText(candidate.actor, 256) && typeof candidate.createdAt === 'string') {
    try { JSON.parse(candidate.body) } catch { return { kind: 'unreadable', key } }
    return { kind: 'restored', command: candidate as PendingCommand }
  }
  return { kind: 'unreadable', key }
}

// Clave de lo guardado: la de la orden, la que se pudo leer de un registro ilegible (o null), o undefined si no hay nada.
const keyOf = (load: PendingLoad): string | null | undefined => load.kind === 'restored' ? load.command.key : load.kind === 'unreadable' ? load.key : undefined

// Borra el hueco solo si sigue guardando la orden con esa clave, comprobado en la MISMA transaccion que borra.
const deleteIfKey = (key: string | null, database: string) => transact<void>(STORES.pending, (store, done) => {
  const read = store.get(KEY)
  read.onsuccess = () => { if (keyOf(parsePending(read.result)) === key) store.delete(KEY); done(undefined) }
}, database)

// C3c: el almacen de una base concreta (la de cada aplicacion). `database` se consulta en cada llamada.
export const pendingStoreIn = (database: () => string): PendingStore => ({
  load: async () => parsePending(await run<unknown>(STORES.pending, 'readonly', store => store.get(KEY), database())),
  reserve: command => transact<PendingLoad>(STORES.pending, (store, done) => {
    const read = store.get(KEY)
    read.onsuccess = () => {
      const current = parsePending(read.result)
      if (current.kind === 'absent') store.put(command, KEY)
      done(current)
    }
  }, database()),
  clear: key => deleteIfKey(key, database()),
  discard: key => deleteIfKey(key, database()),
})
export const indexedDbPendingStore: PendingStore = pendingStoreIn(currentDatabase)

// C3c, adopcion (adoption.ts): lo guardado TAL CUAL en el hueco de otra base (la vieja), sin crearla si no existe. Se mueve el
// registro crudo, legible o no: uno ilegible tambien bloquea hasta conciliarlo o descartarlo, y nunca se destruye sin decision.
export const peekPendingRecord = (database: string): Promise<unknown> => peek<unknown>(database, STORES.pending, store => store.get(KEY))
// Ocupa el hueco VACIO de `database` con ese registro, comprobando y escribiendo en UNA transaccion (D6.7, como reserve). Devuelve
// false si el hueco ya tenia algo: entonces no escribe nada.
export const adoptPendingRecord = (record: unknown, database: string): Promise<boolean> => transact<boolean>(STORES.pending, (store, done) => {
  const read = store.get(KEY)
  read.onsuccess = () => {
    const free = parsePending(read.result).kind === 'absent'
    if (free) store.put(record, KEY)
    done(free)
  }
}, database)
// Y lo quita de la base de origen solo si sigue siendo ESE registro (por su clave, en la misma transaccion que borra).
export async function releasePendingRecord(record: unknown, database: string): Promise<void> {
  const key = keyOf(parsePending(record))
  if (key !== undefined) await deleteIfKey(key, database)
}
