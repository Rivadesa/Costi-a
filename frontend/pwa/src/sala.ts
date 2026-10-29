import { commandFor, DONE_MARK, tableActions, type CommandRequest } from './ficha'
import { clockLabel } from './labels'
import type { BoardEntry, Course, Dining } from './types'

// C3c (plan 2026-10-22, seccion 4.1): el aviso de «listo para servir» de Costina Sala, SIN tocar el motor y sin DOM (vitest).
//  - Sale de la RELECTURA autoritativa del tablero, nunca del contenido del aviso del canal (D2: los avisos son finos y sin datos):
//    un aviso repetido o perdido no cambia nada.
//  - Avisa un pase en el que el servidor ANUNCIA serve (ya filtrado por rol y estacion). No basta con el estado Ready: una alergia
//    declarada despues de validar deja el pase Ready pero sin serve hasta que cocina lo revisa; entonces vuelve a avisar.
//  - La clave es servicio:pase:readyAt. «Rehacer» quita readyAt, asi que la siguiente validacion es una clave nueva y avisa otra vez.
//  - Nuevas = las que no estaban en la lectura correcta ANTERIOR (para vibrar). La primera lectura no trae nada nuevo: lo que ya
//    estaba listo sale en la banda y en verde, sin vibrar.
//  - «Visto» se recuerda EN MEMORIA por clave mientras la clave siga en la lectura; si sale de ella (servido, o por revisar) y
//    vuelve, avisa de nuevo. Una recarga lo olvida.
// Nada de esto habilita una accion: «Servido» existe solo si el servidor anuncia serve en ESE pase, y sale con la version LEIDA de esa
// mesa (F01), por la orden incierta de siempre.
// C3e (plan 2026-10-27, paso 2): la banda avisa SOLO de «Mis mesas»: ReadyWatch lee mineEntries(entries, who), no el tablero entero.
// En «Todas» las demas mesas se siguen pintando en verde, sin banda ni vibracion. Sin «rescope»: el actor y la estacion de una sesion no
// cambian mientras dure la credencial (el motor no renombra ni cambia de estacion un dispositivo emparejado; volver a emparejar monta
// otra vez la pantalla con un ReadyWatch nuevo).
export interface ReadyNotice { key: string; entry: BoardEntry; course: Course }

// ---- C3e: «Mis mesas» (ADR-018): presentacion por dispositivo y estacion, nunca permiso ni atribucion a una persona ----
// who: el actor y la estacion de /session. Tipo propio (como Who en apps.ts), sin importar Session: sala.ts solo importa ficha, labels y
// types. Una mesa es mia si:
//  - openedBy es EXACTAMENTE el actor de /session («device:Movil Ana»), el texto que forma el servidor, nunca uno rehecho con el nombre
//    guardado en el movil;
//  - o openedStation no esta vacio y es EXACTAMENTE la estacion (el codigo, que no cambia al renombrarla): los otros moviles de esa
//    estacion y el PC si su estacion es esa. Una estacion nula nunca coincide;
//  - o no se sabe quien la abrio: sin openedBy (abierta antes de E4e) o sin openedStation (una persona en el PC, sin estacion). Mejor
//    avisar de mas que de menos (plan 2026-10-22, 4.4).
export interface Who { actor: string; station: string | null }
const known = (value: string | null | undefined): value is string => typeof value === 'string' && value.length > 0
export function isMine(service: Pick<Dining, 'openedBy' | 'openedStation'>, who: Who): boolean {
  if (!known(service.openedBy) || !known(service.openedStation)) return true
  return service.openedBy === who.actor || (who.station !== null && service.openedStation === who.station)
}
// Lo que lee la banda: las entradas de «Mis mesas», en el orden y con la version de la lectura (F01).
export const mineEntries = (entries: readonly BoardEntry[], who: Who): BoardEntry[] => entries.filter(entry => isMine(entry.service, who))

export const readyKey = (serviceId: string, courseId: string, readyAt: string | null) => `${serviceId}:${courseId}:${readyAt ?? ''}`
const instant = (at: string | null) => { const parsed = at ? Date.parse(at) : Number.NaN; return Number.isNaN(parsed) ? Number.POSITIVE_INFINITY : parsed }
// Orden ORDINAL (nunca la colacion del navegador): el mismo orden en cualquier movil.
const ordinal = (left: string, right: string) => left < right ? -1 : left > right ? 1 : 0

// Los pases que el servidor deja servir, del mas antiguo (readyAt) al mas nuevo; sin hora, al final; a igual hora, por mesa y pase.
export function readyNotices(entries: readonly BoardEntry[]): ReadyNotice[] {
  const list: ReadyNotice[] = []
  for (const entry of entries)
    for (const course of entry.service.courses)
      if (course.actions?.includes('serve')) list.push({ key: readyKey(entry.service.id, course.id, course.readyAt), entry, course })
  // Infinity - Infinity es NaN, que cuenta como empate: decide la mesa.
  return list.sort((a, b) => (instant(a.course.readyAt) - instant(b.course.readyAt)) || ordinal(a.entry.service.tableId, b.entry.service.tableId) || ordinal(a.key, b.key))
}

export class ReadyWatch {
  notices: ReadyNotice[] = []
  private previous: Set<string> | null = null   // claves de la ultima lectura correcta; null hasta la primera
  private readonly seen = new Set<string>()     // «Visto», solo en memoria

  // Tras CADA lectura correcta del tablero. Devuelve las claves nuevas respecto a la lectura anterior (ninguna en la primera).
  read(entries: readonly BoardEntry[]): string[] {
    this.notices = readyNotices(entries)
    const keys = new Set(this.notices.map(notice => notice.key))
    const before = this.previous
    const fresh = before === null ? [] : this.notices.map(notice => notice.key).filter(key => !before.has(key))
    this.previous = keys
    for (const key of [...this.seen]) if (!keys.has(key)) this.seen.delete(key)
    return fresh
  }

  // «Visto»: solo una clave que esta en la lectura (no se puede dar por visto lo que aun no ha llegado).
  dismiss(key: string): void { if (this.previous?.has(key)) this.seen.add(key) }

  // Lo que sale en la banda: lo leido sin lo visto, el mas antiguo arriba.
  get shown(): ReadyNotice[] { return this.notices.filter(notice => !this.seen.has(notice.key)) }
}

// ---- Palabras de la banda: nombres, nunca codigos (la mesa la nombra quien llama, con Names) ----
export const READY_TEXT = 'listo para servir'
export const noticeTitle = (notice: ReadyNotice, table: string) => `${table} · ${notice.course.name}`
export const noticeSince = (notice: ReadyNotice) => notice.course.readyAt ? `El pase lo validó a las ${clockLabel(notice.course.readyAt)}` : ''
export const serveLabel = (notice: ReadyNotice) => `Servido · ${notice.course.name}`
// C3e.1 (usabilidad 28-10, H20): «Servido» confirmado desde la banda; el mismo texto que el boton grande de la ficha (ficha.ts, doneLabel).
export const servedLabel = (notice: ReadyNotice) => `${serveLabel(notice)} ${DONE_MARK}`
// «+2 más: Mesa 6 · Postre y Mesa 2 · Aperitivo».
export function moreText(rest: readonly ReadyNotice[], table: (tableId: string) => string): string {
  const titles = rest.map(notice => noticeTitle(notice, table(notice.entry.service.tableId)))
  return titles.length < 2 ? titles.join('') : `${titles.slice(0, -1).join(', ')} y ${titles.at(-1)}`
}

// «Servido»: la MISMA orden que el boton de la ficha (ficha.ts), con la version leida de esa mesa. Sin serve anunciado, ninguna.
export function serveRequest(notice: ReadyNotice, table: string): CommandRequest | null {
  const action = tableActions(notice.entry).find(candidate => candidate.action === 'serve' && candidate.course?.id === notice.course.id)
  return action ? commandFor(notice.entry, action, table) : null
}

// ---- Vibracion: solo Android con Chrome (el iPhone y el iPad no tienen vibrate) ----
// Solo con claves nuevas y la pagina a la vista. Chrome no deja vibrar hasta que la pagina se ha tocado desde que se cargo (activacion
// del usuario): sin ella ni se llama (la llamada se bloquearia y lo diria en la consola). No necesita https. En la sala no suena nada.
export const VIBRATION: readonly number[] = [200, 100, 200]
export interface Vibrator { vibrate?: (pattern: number[]) => boolean; userActivation?: { hasBeenActive: boolean } }
export function buzz(fresh: readonly string[], visible: boolean, device: Vibrator | undefined): boolean {
  if (fresh.length === 0 || !visible || typeof device?.vibrate !== 'function') return false
  if (device.userActivation !== undefined && !device.userActivation.hasBeenActive) return false
  // Llamada con su receptor (el objeto del navegador lo exige); un fallo nunca rompe la lectura.
  try { return device.vibrate([...VIBRATION]) } catch { return false }
}
