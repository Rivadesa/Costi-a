// Base IndexedDB unica de la PWA. v1: credencial del dispositivo (D6.1). v2: orden incierta (D6.3).
const DATABASE = 'costina'
const VERSION = 2
export const STORES = { device: 'device', pending: 'pending' } as const

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, VERSION)
    request.onupgradeneeded = () => {
      for (const store of Object.values(STORES)) if (!request.result.objectStoreNames.contains(store)) request.result.createObjectStore(store)
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
    request.onblocked = () => reject(new Error('IndexedDB bloqueada por otra pestana con una version anterior.'))
  })
}

// La promesa solo se resuelve con la transaccion COMPLETADA: "guardado" significa escrito, no encolado.
export async function run<T>(store: string, mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const database = await open()
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = database.transaction(store, mode, mode === 'readwrite' ? { durability: 'strict' } : undefined)
      const request = action(transaction.objectStore(store))
      transaction.oncomplete = () => resolve(request.result)
      transaction.onerror = () => reject(transaction.error)
      transaction.onabort = () => reject(transaction.error ?? new Error('Transaccion abortada.'))
    })
  } finally { database.close() }
}

// Leer Y escribir en UNA sola transaccion readwrite (D6.7). IndexedDB serializa las transacciones readwrite que comparten
// almacen, tambien entre pestanas y conexiones: comprobar y escribir es atomico SIN Web Locks (que no existen fuera de un
// contexto seguro, p. ej. la conexion sencilla por http de B.2). `step` lanza las peticiones y fija el resultado con
// `done` desde sus onsuccess; la promesa solo se resuelve con la transaccion COMPLETADA.
export async function transact<T>(store: string, step: (store: IDBObjectStore, done: (value: T) => void) => void): Promise<T> {
  const database = await open()
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = database.transaction(store, 'readwrite', { durability: 'strict' })
      let result: { value: T } | null = null
      transaction.oncomplete = () => result ? resolve(result.value) : reject(new Error('Transaccion completada sin resultado.'))
      transaction.onerror = () => reject(transaction.error)
      transaction.onabort = () => reject(transaction.error ?? new Error('Transaccion abortada.'))
      step(transaction.objectStore(store), value => { result = { value } })
    })
  } finally { database.close() }
}
