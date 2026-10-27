// C3d (plan 2026-10-27, seccion 2; plan 2026-10-22, seccion 4.3; respuesta 4 del 22-10): «Empezar turno» de Costina Cocina.
//  - El toque es lo que deja sonar al navegador (activacion del usuario): el audio se crea DENTRO del toque y suena una vez, para que
//    se oiga como sonara. Despues suena con cada comanda nueva (cocina.ts, ringKeys, TicketWatch y shouldChime).
//  - El sonido es un tono corto SINTETIZADO con Web Audio: sin ficheros de audio, asi que la CSP del motor no cambia.
//  - Con https, la pantalla se queda encendida (Wake Lock) si el navegador la da; el navegador la suelta al ocultar la pagina y se pide
//    otra vez al volver. Con la conexion sencilla (http, B.2) no existe: se dice que se deje encendida en los ajustes de la tablet.
//  - El turno dura lo que la pagina cargada: una recarga lo olvida (el navegador vuelve a exigir un toque para sonar). El modo quiosco
//    y la reapertura tras reiniciar la tablet siguen en el Hito 7.
// Sin globales del navegador: el audio y la pantalla llegan como dependencias (la vista los da; vitest, dobles).

export type Sound = 'off' | 'on' | 'unavailable'
export type Screen = 'off' | 'on' | 'http' | 'unsupported' | 'failed'
export interface ShiftStatus { started: boolean; sound: Sound; screen: Screen }

// Lo que se usa de Web Audio (AudioContext, OscillatorNode, GainNode, AudioParam).
export interface ParamLike { value: number; setValueAtTime(value: number, at: number): unknown; exponentialRampToValueAtTime(value: number, at: number): unknown }
export interface NodeLike { connect(target: never): unknown }
export interface OscillatorLike extends NodeLike { type: string; frequency: ParamLike; start(at: number): void; stop(at: number): void }
export interface GainLike extends NodeLike { gain: ParamLike }
export interface AudioLike {
  readonly currentTime: number; readonly state: string; readonly destination: unknown
  resume(): Promise<void>; createOscillator(): OscillatorLike; createGain(): GainLike
}
// Lo que se usa de Wake Lock (navigator.wakeLock y WakeLockSentinel).
export interface SentinelLike { readonly released: boolean; release(): Promise<void>; addEventListener(type: 'release', listener: () => void): void }
export interface ScreenEnv { secure: boolean; wakeLock?: { request(type: 'screen'): Promise<SentinelLike> } }

// Dos notas cortas (la y mi agudos) con forma triangular, que se oyen sobre el ruido de una cocina sin ser estridentes. Menos de medio
// segundo en total. Las rampas exponenciales evitan el chasquido (y no admiten 0: empiezan y acaban casi en silencio).
export const CHIME = [
  { frequency: 880, at: 0, length: 0.16 },
  { frequency: 1318.5, at: 0.18, length: 0.24 },
] as const
export const CHIME_PEAK = 0.5
const QUIET = 0.0001

export function chime(audio: AudioLike): void {
  if (audio.state === 'suspended') void audio.resume().catch(() => undefined)
  const now = audio.currentTime
  for (const note of CHIME) {
    const oscillator = audio.createOscillator(), gain = audio.createGain()
    const at = now + note.at, end = at + note.length
    oscillator.type = 'triangle'
    oscillator.frequency.value = note.frequency
    gain.gain.setValueAtTime(QUIET, at)
    gain.gain.exponentialRampToValueAtTime(CHIME_PEAK, at + 0.015)
    gain.gain.exponentialRampToValueAtTime(QUIET, end)
    oscillator.connect(gain as never)
    gain.connect(audio.destination as never)
    oscillator.start(at)
    oscillator.stop(end + 0.02)
  }
}

// Wake Lock solo existe en un contexto seguro (https o el propio equipo); la conexion sencilla (http por IP) no lo es.
export function screenSupport(env: ScreenEnv): 'available' | 'http' | 'unsupported' {
  if (!env.secure) return 'http'
  return env.wakeLock && typeof env.wakeLock.request === 'function' ? 'available' : 'unsupported'
}

// ---- Las palabras ----
export const SHIFT_TITLE = 'Empezar turno'
const RINGS = 'Tócalo al empezar: así la tablet suena cada vez que llega una comanda nueva.'
export function screenHint(screen: Screen): string {
  if (screen === 'on') return 'La pantalla se queda encendida.'
  if (screen === 'http') return 'Con esta conexión la pantalla no se queda encendida sola: déjala siempre encendida en los ajustes de la tablet.'
  if (screen === 'unsupported' || screen === 'failed') return 'Este navegador no mantiene la pantalla encendida: déjala siempre encendida en los ajustes de la tablet.'
  return ''
}
// Antes del toque: que hace «Empezar turno» y, si no hay Wake Lock, que hacer con la pantalla.
export const shiftIntro = (support: 'available' | 'http' | 'unsupported'): string =>
  support === 'available' ? `${RINGS} Además, la pantalla se queda encendida.` : `${RINGS} ${screenHint(support)}`
export function soundText(sound: Sound): string {
  if (sound === 'on') return 'Turno empezado: suena con cada comanda nueva.'
  if (sound === 'unavailable') return 'Turno empezado, pero este navegador no puede sonar: mira la pantalla.'
  return ''
}

// ---- El turno de esta pagina ----
// status es el estado que se pinta (la vista lo pasa reactivo); el audio y la pantalla se guardan aparte, FUERA de lo reactivo: un
// AudioContext envuelto en un Proxy rompe sus metodos (Illegal invocation).
export class Shift {
  private audio: AudioLike | null = null
  private sentinel: SentinelLike | null = null
  constructor(readonly status: ShiftStatus, private readonly deps: { audio: () => AudioLike | null; screen: () => ScreenEnv }) {}

  // En el manejador del toque, y sin esperar a nada antes de crear el audio: la activacion del usuario dura poco.
  async start(): Promise<void> {
    if (!this.audio) { try { this.audio = this.deps.audio() } catch { this.audio = null } }
    this.status.started = true
    this.status.sound = this.audio ? 'on' : 'unavailable'
    this.ring()
    await this.awake()
  }

  // Un tono, si el turno empezo y hay audio. Un fallo nunca rompe la lectura del tablero.
  ring(): boolean {
    if (!this.status.started || !this.audio) return false
    try { chime(this.audio); return true } catch { return false }
  }

  // La pantalla encendida: al empezar y cada vez que la pagina vuelve a verse (el navegador la suelta al ocultarla).
  async awake(): Promise<void> {
    if (!this.status.started || (this.sentinel && !this.sentinel.released)) return
    const env = this.deps.screen()
    const support = screenSupport(env)
    if (support !== 'available') { this.status.screen = support; return }
    try {
      const sentinel = await env.wakeLock!.request('screen')
      this.sentinel = sentinel
      this.status.screen = 'on'
      sentinel.addEventListener('release', () => { if (this.sentinel === sentinel) this.sentinel = null })
    } catch { this.status.screen = 'failed' }
  }
}
