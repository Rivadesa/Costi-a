import { onMounted, onUnmounted, reactive, shallowRef } from 'vue'
import { ApiError, readCatalog, type CatalogRead, type Fetch } from './api'

// C3b: el catalogo operativo AL DIA para «Bodega y bebidas» (plan 2026-10-15, seccion 4.4). Se lee al montar el comandero y se
// REVALIDA con ETag (api.ts, readCatalog) al abrir Bebidas, cada 5 minutos y siempre tras reconectar. Un 304 conserva la lista
// anterior, la MISMA (no se rehace nada); cada cuerpo nuevo pasa entero por la guarda de dinero (ADR-007). Un fallo conserva lo ultimo
// leido y lo dice (`failed`); un 401 avisa a la aplicacion. Lecturas solapadas se funden en una. Solo si el servidor anuncia la ronda.
export const CATALOG_MS = 5 * 60_000

export interface CatalogStatus { readAt: Date | null; failed: boolean; reading: boolean }

export function useCatalog(props: { token: string }, enabled: () => boolean, onUnauthorized: () => void = () => undefined,
                           deps: { fetcher?: Fetch; everyMs?: number } = {}) {
  // La lista entera (unas 2.000 filas con la bodega completa) NO es reactiva por dentro: solo cambia la referencia, y con un 304 ni eso.
  const read = shallowRef<CatalogRead | null>(null)
  const status = reactive<CatalogStatus>({ readAt: null, failed: false, reading: false })
  let current: Promise<void> | null = null
  let timer: ReturnType<typeof setInterval> | undefined

  async function load(): Promise<void> {
    status.reading = true
    try {
      // window.fetch se llama sin receptor (D6.3): se resuelve en cada lectura, no se guarda como metodo de un objeto.
      const next = await readCatalog(deps.fetcher ?? ((input, init) => fetch(input, init)), props.token, read.value)
      if (next !== read.value) read.value = next
      status.readAt = new Date(); status.failed = false
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) onUnauthorized()
      status.failed = true
    } finally { status.reading = false }
  }
  function refresh(): Promise<void> {
    if (!enabled()) return Promise.resolve()
    current ??= load().finally(() => { current = null })
    return current
  }
  onMounted(() => { void refresh(); timer = setInterval(() => { void refresh() }, deps.everyMs ?? CATALOG_MS) })
  onUnmounted(() => clearInterval(timer))
  return { read, status, refresh }
}

// Tras reconectar el tiempo real: true cada vez que el enlace VUELVE a estar en vivo (no la primera, que ya leyo el montaje). Sirve
// para reconexiones automaticas ('reconnecting' -> 'live') y tras degradar a sondeo ('polling' -> 'connecting' -> 'live').
export function reconnections(): (link: string) => boolean {
  let wasLive = false
  return link => {
    if (link !== 'live') return false
    const again = wasLive
    wasLive = true
    return again
  }
}
