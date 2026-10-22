// Bases IndexedDB de la PWA. v1: credencial del dispositivo (D6.1). v2: orden incierta (D6.3).
// C3c: UNA BASE POR APLICACION (costina-sala y costina-cocina, con los mismos almacenes). En Android las dos aplicaciones comparten
// origen: con una sola base compartirian la credencial y el hueco de la orden pendiente. Cada entrada elige la suya ANTES de montar
// nada (selectDatabase). La base vieja `costina` (C3b y anteriores) no se borra nunca: la leen el enrutador de /app/ y la adopcion
// (adoption.ts), y la usaria una version anterior si se volviera atras.
export const LEGACY_DATABASE = 'costina'
const VERSION = 2
export const STORES = { device: 'device', pending: 'pending' } as const
// Estos mensajes llegan a la pantalla (startup.ts los pone en «Servidor no listo (…)»): castellano con tildes, como el resto.
export const DB_TEXTS = {
  blocked: 'IndexedDB bloqueada por otra pestaña con una versión anterior.',
  aborted: 'Transacción abortada.',
  empty: 'Transacción completada sin resultado.',
} as const

// La base de la aplicacion que corre en esta pagina. Sin elegir, la vieja (las pruebas de antes de C3c la usan tal cual).
let selected: string = LEGACY_DATABASE
export function selectDatabase(name: string): void { selected = name }
export const currentDatabase = (): string => selected

// create = false: si la base no existe, NO se crea (se aborta su creacion) y se devuelve null. Asi mirar si hay algo en la base
// vieja o en la de la otra aplicacion nunca deja bases vacias por el camino.
function open(name: string, create: boolean): Promise<IDBDatabase | null> {
  return new Promise((resolve, reject) => {
    let absent = false
    const request = indexedDB.open(name, VERSION)
    request.onupgradeneeded = event => {
      if (!create && event.oldVersion === 0) { absent = true; request.transaction?.abort(); return }
      for (const store of Object.values(STORES)) if (!request.result.objectStoreNames.contains(store)) request.result.createObjectStore(store)
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = event => {
      if (absent) { event.preventDefault(); resolve(null) } else reject(request.error)
    }
    request.onblocked = () => reject(new Error(DB_TEXTS.blocked))
  })
}
const opened = async (name: string): Promise<IDBDatabase> => (await open(name, true))!

function request<T>(database: IDBDatabase, store: string, mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const transaction = database.transaction(store, mode, mode === 'readwrite' ? { durability: 'strict' } : undefined)
    const pending = action(transaction.objectStore(store))
    transaction.oncomplete = () => resolve(pending.result)
    transaction.onerror = () => reject(transaction.error)
    transaction.onabort = () => reject(transaction.error ?? new Error(DB_TEXTS.aborted))
  })
}

// La promesa solo se resuelve con la transaccion COMPLETADA: "guardado" significa escrito, no encolado.
export async function run<T>(store: string, mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>, database: string = selected): Promise<T> {
  const connection = await opened(database)
  try { return await request(connection, store, mode, action) } finally { connection.close() }
}

// Lectura de una base que quiza no exista (la vieja, la de la otra aplicacion): undefined si no existe, sin crearla.
export async function peek<T>(database: string, store: string, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T | undefined> {
  const connection = await open(database, false)
  if (connection === null) return undefined
  try { return await request(connection, store, 'readonly', action) } finally { connection.close() }
}

// Leer Y escribir en UNA sola transaccion readwrite (D6.7). IndexedDB serializa las transacciones readwrite que comparten
// almacen, tambien entre pestanas y conexiones: comprobar y escribir es atomico SIN Web Locks (que no existen fuera de un
// contexto seguro, p. ej. la conexion sencilla por http de B.2). `step` lanza las peticiones y fija el resultado con
// `done` desde sus onsuccess; la promesa solo se resuelve con la transaccion COMPLETADA.
export async function transact<T>(store: string, step: (store: IDBObjectStore, done: (value: T) => void) => void, database: string = selected): Promise<T> {
  const connection = await opened(database)
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = connection.transaction(store, 'readwrite', { durability: 'strict' })
      let result: { value: T } | null = null
      transaction.oncomplete = () => result ? resolve(result.value) : reject(new Error(DB_TEXTS.empty))
      transaction.onerror = () => reject(transaction.error)
      transaction.onabort = () => reject(transaction.error ?? new Error(DB_TEXTS.aborted))
      step(transaction.objectStore(store), value => { result = { value } })
    })
  } finally { connection.close() }
}
