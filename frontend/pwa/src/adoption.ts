import { ApiError, getSession, type Fetch, type Session } from './api'
import { appFits, APPS, otherApp, type AppId } from './apps'
import { clearCredential, peekCredential, saveCredential, type Credential } from './credentials'
import { LEGACY_DATABASE } from './db'
import { adoptPendingRecord, parsePending, peekPendingRecord, releasePendingRecord } from './pending'

// C3c (plan 2026-10-22, seccion 4.1, «Lo guardado, separado por aplicacion»). Una aplicacion SIN credencial en su base mira, antes de
// pedir que se empareje, lo que ya hay en este origen:
//  1. la base vieja `costina` (C3b y anteriores). Con su credencial lee /session y, SOLO si el rol le corresponde, la ADOPTA: copia
//     la orden pendiente al hueco vacio de su base (D6.7: comprobar y escribir en una transaccion), luego la credencial, y por
//     ultimo quita la orden de la base vieja POR SU CLAVE. La base vieja no se borra (la usan el enrutador y una version anterior) y
//     la credencial vieja tampoco: la de otro rol se deja donde esta, para la otra aplicacion;
//  2. la base de la OTRA aplicacion (en Android las dos comparten origen): con una credencial suya, este dispositivo ya esta
//     conectado como la otra aplicacion, y esta no monta nada (se dice y se ofrece desvincular).
// Un 401 es la UNICA senal de que una credencial ya no vale (corte A): se borra de la base donde estaba, como hoy, y se sigue mirando.
// Cualquier otro fallo (sin red, 503, la guarda de dinero) no decide nada: se lanza y quien llama lo dice y reintenta.
export type Found =
  | { kind: 'none' }                                                                        // nada: emparejar
  | { kind: 'adopted'; credential: Credential }                                             // ya esta en la base propia
  | { kind: 'other'; session: Session; credential: Credential; database: string }           // conectado como otra cosa

export async function lookAround(app: AppId, fetcher: Fetch): Promise<Found> {
  const own = APPS[app].database
  let foreign: Found | null = null
  const legacy = await peekCredential(LEGACY_DATABASE)
  if (legacy) {
    const session = await sessionOf(fetcher, legacy, LEGACY_DATABASE)
    if (session && appFits(app, session.role)) return { kind: 'adopted', credential: await adopt(legacy, own) }
    if (session) foreign = { kind: 'other', session, credential: legacy, database: LEGACY_DATABASE }
  }
  const sibling = APPS[otherApp(app)].database
  const theirs = await peekCredential(sibling)
  if (theirs) {
    const session = await sessionOf(fetcher, theirs, sibling)
    if (session) return { kind: 'other', session, credential: theirs, database: sibling }
  }
  return foreign ?? { kind: 'none' }
}

async function sessionOf(fetcher: Fetch, credential: Credential, database: string): Promise<Session | null> {
  try { return await getSession(fetcher, credential.token) }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) { await clearCredential(database); return null }
    throw error
  }
}

// Primero la orden pendiente (si la credencial llegara sin ella, se podria operar sin verla), despues la credencial y por ultimo la
// orden sale de la base vieja. Si el hueco propio ya tenia una orden, la vieja se queda donde estaba: nunca se sustituye ninguna.
async function adopt(credential: Credential, own: string): Promise<Credential> {
  const record = await peekPendingRecord(LEGACY_DATABASE)
  const moved = parsePending(record).kind !== 'absent' && await adoptPendingRecord(record, own)
  await saveCredential(credential, own)
  if (moved) await releasePendingRecord(record, LEGACY_DATABASE)
  return credential
}
