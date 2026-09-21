// Credencial del DISPOSITIVO emparejado (token dev.*), nunca la de una persona. Vive en IndexedDB de
// este origen: sobrevive a cerrar el navegador y a reinstalar la PWA, y se borra al desvincular o
// cuando el servidor responde 401 (emparejamiento revocado). El navegador no ofrece un almacen
// cifrado por hardware a una web: la defensa es el mismo origen, la CSP estricta sin scripts de
// terceros y la revocacion inmediata desde el PC principal.
const DATABASE = 'costina'
const STORE = 'device'
const KEY = 'credential'

export interface Credential { token: string; deviceName: string; pairedAt: string }

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(STORE)
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

async function run<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const database = await open()
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = database.transaction(STORE, mode)
      const request = action(transaction.objectStore(STORE))
      transaction.oncomplete = () => resolve(request.result)
      transaction.onerror = () => reject(transaction.error)
      transaction.onabort = () => reject(transaction.error)
    })
  } finally { database.close() }
}

export async function loadCredential(): Promise<Credential | null> {
  const stored = await run<unknown>('readonly', store => store.get(KEY))
  if (stored === null || typeof stored !== 'object') return null
  const candidate = stored as Partial<Credential>
  // Fallo cerrado: algo que no sea exactamente una credencial de dispositivo se trata como ausente.
  return typeof candidate.token === 'string' && candidate.token.startsWith('dev.') && typeof candidate.deviceName === 'string'
    ? { token: candidate.token, deviceName: candidate.deviceName, pairedAt: String(candidate.pairedAt ?? '') }
    : null
}

export const saveCredential = (credential: Credential) => run('readwrite', store => store.put(credential, KEY)).then(() => undefined)
export const clearCredential = () => run('readwrite', store => store.delete(KEY)).then(() => undefined)
