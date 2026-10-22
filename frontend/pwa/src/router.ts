import { ApiError, getSession, type Fetch } from './api'
import { appForRole, APP_IDS, APPS, connectedAs, type AppId } from './apps'
import { clearCredential, peekCredential } from './credentials'
import { LEGACY_DATABASE } from './db'
import { codeFromLocation } from './pairing'
import { outcomeOf, UNPAIRED_NOTICE } from './startup'

// C3c: /app/ deja de ser una aplicacion y pasa a ser un ENRUTADOR (sin manifiesto ni metas de iOS: no se instala). Decide adonde va
// este dispositivo, sin DOM y sin crear ninguna base:
//  - con #pair= (un QR o un enlace de antes): pregunta si es para la sala o para la cocina y pasa el codigo a la elegida;
//  - con una credencial en costina-sala o costina-cocina (en Android comparten origen con /app/): a esa aplicacion, sin preguntar al
//    servidor (asi tambien sin red: la aplicacion dira que no hay conexion);
//  - con la credencial de la base vieja `costina`: lee /session y va a la aplicacion de su rol, que la adopta (adoption.ts). Un
//    acceso directo viejo a /app/ sigue llegando a su aplicacion. Un 401 la borra (la unica senal, corte A);
//  - sin nada: explica como conectar el dispositivo desde el PC.
export type Route =
  | { kind: 'choose'; code: string }
  | { kind: 'go'; app: AppId }
  | { kind: 'none'; notice: string }
  | { kind: 'unknown'; connected: string }     // credencial vieja de un papel sin aplicacion: se dice tal cual (fallo cerrado)
  | { kind: 'offline'; notice: string }

export async function decideRoute(hash: string, fetcher: Fetch): Promise<Route> {
  const code = codeFromLocation(hash)
  if (code) return { kind: 'choose', code }
  try {
    for (const app of APP_IDS) if (await peekCredential(APPS[app].database)) return { kind: 'go', app }
    const legacy = await peekCredential(LEGACY_DATABASE)
    if (!legacy) return { kind: 'none', notice: '' }
    const session = await getSession(fetcher, legacy.token)
    const app = appForRole(session.role)
    return app ? { kind: 'go', app } : { kind: 'unknown', connected: connectedAs(session) }
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) { await clearCredential(LEGACY_DATABASE); return { kind: 'none', notice: UNPAIRED_NOTICE } }
    return { kind: 'offline', notice: outcomeOf(error, false).notice }
  }
}

// Adonde se va: la carpeta de la aplicacion, y el codigo (ya validado por la expresion cerrada de codeFromLocation) en el fragmento.
export const destination = (app: AppId, code = ''): string => APPS[app].path + (code ? `#pair=${code}` : '')
