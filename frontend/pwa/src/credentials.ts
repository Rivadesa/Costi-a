import { currentDatabase, peek, run, STORES } from './db'

// Credencial del DISPOSITIVO emparejado (token dev.*), nunca la de una persona. Vive en IndexedDB de
// este origen: sobrevive a cerrar el navegador y a reinstalar la PWA, y se borra al desvincular o
// cuando el servidor responde 401 (emparejamiento revocado). El navegador no ofrece un almacen
// cifrado por hardware a una web: la defensa es el mismo origen, la CSP estricta sin scripts de
// terceros y la revocacion inmediata desde el PC principal.
// C3c: cada aplicacion guarda la suya en SU base (db.ts); la de otra base (la vieja o la de la otra aplicacion) se lee sin crearla.
const KEY = 'credential'

export interface Credential { token: string; deviceName: string; pairedAt: string }

// Fallo cerrado: algo que no sea exactamente una credencial de dispositivo se trata como ausente.
export function parseCredential(stored: unknown): Credential | null {
  if (stored === null || typeof stored !== 'object') return null
  const candidate = stored as Partial<Credential>
  return typeof candidate.token === 'string' && candidate.token.startsWith('dev.') && typeof candidate.deviceName === 'string'
    ? { token: candidate.token, deviceName: candidate.deviceName, pairedAt: String(candidate.pairedAt ?? '') }
    : null
}

export const loadCredential = async (database: string = currentDatabase()): Promise<Credential | null> =>
  parseCredential(await run<unknown>(STORES.device, 'readonly', store => store.get(KEY), database))
export const peekCredential = async (database: string): Promise<Credential | null> =>
  parseCredential(await peek<unknown>(database, STORES.device, store => store.get(KEY)))
export const saveCredential = (credential: Credential, database: string = currentDatabase()) =>
  run(STORES.device, 'readwrite', store => store.put(credential, KEY), database).then(() => undefined)
export const clearCredential = (database: string = currentDatabase()) =>
  run(STORES.device, 'readwrite', store => store.delete(KEY), database).then(() => undefined)
