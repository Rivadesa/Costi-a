import { ApiError, NetworkError } from './api'
import { MoneyLeakError } from './money-guard'

// Arranque de la PWA (corte A, plan 2026-09-24, F8): que estado toma la aplicacion segun lo que devuelve la primera lectura.
// Logica pura, sin DOM, cubierta por vitest. Un 401 es la UNICA senal de que la credencial ya no vale; cualquier otro fallo
// (503 con PostgreSQL caido, 500, HTML de un proxy, IndexedDB que no abre) es "servidor no listo": se dice y se reintenta.
export type Startup = { state: 'ready' | 'unpaired' | 'offline'; notice: string }

// Un 401 ya no se explica siempre como "revocado": tambien ocurre al desvincular, o tras restaurar una copia anterior.
export const UNPAIRED_NOTICE = 'Este dispositivo ya no está emparejado: lo quitaron desde el PC principal, se desvinculó, o el servidor se restauró desde una copia anterior. Pide un código nuevo en el PC principal.'
export const NOT_READY_PREFIX = 'Servidor no listo'

export function outcomeOf(error: unknown, hadSession: boolean): Startup {
  if (error instanceof ApiError && error.status === 401) return { state: 'unpaired', notice: UNPAIRED_NOTICE }
  if (error instanceof NetworkError) return { state: hadSession ? 'ready' : 'offline', notice: '' }
  if (error instanceof MoneyLeakError) return { state: 'offline', notice: error.message }
  const detail = error instanceof ApiError ? `HTTP ${error.status}` : error instanceof Error ? error.message : String(error)
  return { state: 'offline', notice: `${NOT_READY_PREFIX} (${detail}). Se reintenta solo cada 15 segundos.` }
}
