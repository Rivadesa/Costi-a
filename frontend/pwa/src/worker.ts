import { APPS, type AppId } from './apps'

// Service workers (C3c): UNO POR APLICACION, cada uno en su carpeta y con su alcance (/app/sala/sw.js y /app/cocina/sw.js), su cache y
// su prefijo de cache. Solo cachean el "shell" de su aplicacion para poder abrirla sin red. JAMAS interceptan /api/: los datos
// operativos siempre son una lectura autoritativa del servidor o un error visible. Sin Background Sync.
// El de antes (/app/sw.js, alcance /app/) cubria tambien las dos carpetas nuevas: se retira. Su fichero pasa a ser uno que borra su
// cache y se da de baja, pero el navegador solo lo actualiza al navegar por paginas que controla; por eso el enrutador Y cada
// aplicacion dan de baja ademas cualquier registro de alcance EXACTO /app/.
// Por http (la conexion sencilla de B.2, sin contexto seguro) el navegador no ofrece service workers: aqui no se hace nada y nada
// depende de ellos.
export function startWorkers(app: AppId | null): void {
  if (!import.meta.env.PROD || !globalThis.isSecureContext || !('serviceWorker' in navigator)) return
  window.addEventListener('load', () => {
    void retireOldWorker().catch(() => undefined)
    if (app) navigator.serviceWorker.register(`${APPS[app].path}sw.js`, { scope: APPS[app].path }).catch(() => undefined)
  })
}

export const OLD_SCOPE = '/app/'
export async function retireOldWorker(): Promise<number> {
  let retired = 0
  for (const registration of await navigator.serviceWorker.getRegistrations())
    if (new URL(registration.scope).pathname === OLD_SCOPE && await registration.unregister()) retired++
  return retired
}
