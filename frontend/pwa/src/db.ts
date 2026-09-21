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
