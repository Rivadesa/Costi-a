import { run, STORES } from './db'

// La ORDEN INCIERTA de este dispositivo (mismo modelo que el cliente Windows, D3.3/D3.4):
// exactamente UNA, persistida ANTES del primer intento con sus bytes exactos y su Idempotency-Key, y
// borrada solo con exito confirmado o rechazo definitivo. No es una cola de trabajo sin conexion: los
// comandos llevan expectedVersion y, encolados, llegarian obsoletos del segundo en adelante.
export interface PendingCommand {
  key: string            // Idempotency-Key: la identidad de la orden ante el servidor
  path: string           // ruta relativa a /api/native/v1
  body: string           // bytes EXACTOS enviados; el reintento reenvia esto, no lo reconstruye
  description: string    // lo que ve la persona: "Servir pase Aperitivos · M3"
  createdAt: string
  installationId: string // una orden de OTRA instalacion se informa y jamas se reenvia
  actor: string          // la conciliacion por clave solo vale para el mismo actor
}

export type PendingLoad =
  | { kind: 'absent' }
  | { kind: 'restored'; command: PendingCommand }
  | { kind: 'unreadable'; key: string | null }   // fallo cerrado: bloquea hasta conciliar o descartar de forma explicita

export interface PendingStore {
  load(): Promise<PendingLoad>
  save(command: PendingCommand): Promise<void>
  clear(key: string): Promise<void>      // solo borra si lo guardado lleva ESA clave
  discard(): Promise<void>               // decision explicita de la persona; nunca automatica
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

export const indexedDbPendingStore: PendingStore = {
  load: async () => parsePending(await run<unknown>(STORES.pending, 'readonly', store => store.get(KEY))),
  save: command => run(STORES.pending, 'readwrite', store => store.put(command, KEY)).then(() => undefined),
  clear: async key => {
    const current = parsePending(await run<unknown>(STORES.pending, 'readonly', store => store.get(KEY)))
    if (current.kind === 'restored' && current.command.key !== key) return
    if (current.kind === 'unreadable' && current.key !== key) return
    await run(STORES.pending, 'readwrite', store => store.delete(KEY))
  },
  discard: () => run(STORES.pending, 'readwrite', store => store.delete(KEY)).then(() => undefined),
}
