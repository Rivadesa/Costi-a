import { ApiError, getSession, type Fetch, type Session } from './api'
import { appFits, APPS, otherApp, type AppId } from './apps'
import { clearCredential, peekCredential, saveCredential, type Credential } from './credentials'
import { LEGACY_DATABASE } from './db'
import { adoptPendingRecord, parsePending, peekPendingRecord, releasePendingRecord, type PendingLoad } from './pending'

// C3c (plan 2026-10-22, seccion 4.1, «Lo guardado, separado por aplicacion»). Una aplicacion SIN credencial en su base mira, antes de
// pedir que se empareje, lo que ya hay en este origen:
//  1. la base vieja `costina` (C3b y anteriores). Con su credencial lee /session y, SOLO si el rol le corresponde, la ADOPTA: copia
//     la orden pendiente al hueco vacio de su base (D6.7: comprobar y escribir en una transaccion) y la quita de la base vieja POR SU
//     CLAVE, y despues guarda la credencial. La base vieja no se borra (la usan el enrutador y una version anterior) y
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

// Primero la orden pendiente (si la credencial llegara sin ella, se podria operar sin verla) y despues la credencial.
async function adopt(credential: Credential, own: string): Promise<Credential> {
  await moveLegacyOrder(own)
  await saveCredential(credential, own)
  return credential
}

// La orden de la base vieja pasa al hueco de `own` (comprobar y escribir en una transaccion, D6.7) y despues sale de la vieja POR SU
// CLAVE. Si el hueco propio ya tenia OTRA orden, la vieja se queda donde estaba: nunca se sustituye ninguna. Revision C3c: si se corta
// entre los dos pasos, repetirlo lo termina sin duplicarla (adoptPendingRecord reconoce la misma orden por su clave).
async function moveLegacyOrder(own: string): Promise<void> {
  const record = await peekPendingRecord(LEGACY_DATABASE)
  if (parsePending(record).kind === 'absent') return
  if (await adoptPendingRecord(record, own)) await releasePendingRecord(record, LEGACY_DATABASE)
}

// Revision C3c (D6.7: una orden incierta siempre se ve y nunca queda en dos huecos). Con la credencial YA en su base, al arrancar y
// antes de montar nada, lo que quede en la base vieja de ESTE dispositivo pasa tambien a la suya:
//  - la orden de una adopcion cortada (copiada sin soltar, o la credencial guardada antes de soltarla): Shell no vuelve a mirar
//    alrededor con su credencial puesta;
//  - la que una pestana de la version anterior, aun abierta con la MISMA credencial, guardo despues de la adopcion;
//  - la que quedo SIN credencial en la base vieja (un 401 en C3b la borro): nada dice de que aplicacion era, asi que la ensena la
//    pantalla de emparejar (orphanOrder) y la toma la aplicacion que se empareja. Al restaurarla rigen las reglas de siempre: con otro
//    nombre o de otra instalacion queda bloqueada, y el servidor sigue filtrando por papel y estacion lo que se reintenta.
// Con OTRA credencial en la base vieja no se toca nada: la orden es suya (la adopta la otra aplicacion). Nunca crea la base vieja.
export async function reconcileLegacy(own: string, credential: Credential): Promise<void> {
  const legacy = await peekCredential(LEGACY_DATABASE)
  if (legacy && legacy.token !== credential.token) return
  await moveLegacyOrder(own)
}

// La orden que quedo en la base vieja SIN credencial, para ensenarla antes de emparejar (PairView). Con credencial vieja no es
// huerfana: la decide la adopcion. Mirar no la mueve ni crea ninguna base.
export async function orphanOrder(): Promise<PendingLoad> {
  if (await peekCredential(LEGACY_DATABASE)) return { kind: 'absent' }
  return parsePending(await peekPendingRecord(LEGACY_DATABASE))
}
