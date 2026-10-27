import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  CHIME, CHIME_PEAK, chime, screenHint, screenSupport, Shift, shiftIntro, soundText, type AudioLike, type ScreenEnv, type SentinelLike, type ShiftStatus,
} from '../src/turno'

// C3d (plan 2026-10-27, seccion 2; respuesta 4 del 22-10): «Empezar turno» de Costina Cocina. El toque deja sonar al navegador; el
// sonido es un tono corto SINTETIZADO (sin ficheros: la CSP no cambia) y, con https, la pantalla se queda encendida (Wake Lock). Con la
// conexion sencilla (http) no hay Wake Lock: se dice que se deje encendida en los ajustes de la tablet. El navegador llega como dobles.

type Call = [string, ...unknown[]]
// Un AudioContext de mentira: apunta cada llamada (oscilador, ganancia, conexiones, arranque y parada).
function fakeAudio(state = 'running'): AudioLike & { calls: Call[] } {
  const calls: Call[] = []
  const param = (owner: string) => ({
    value: 0,
    setValueAtTime: (value: number, at: number) => calls.push([`${owner}.set`, value, at]),
    exponentialRampToValueAtTime: (value: number, at: number) => calls.push([`${owner}.ramp`, value, at]),
  })
  let made = 0
  const audio = {
    calls, currentTime: 10, state, destination: 'speakers',
    resume: async () => { calls.push(['resume']) },
    createOscillator: () => {
      const id = `osc${made++}`
      const frequency = param(`${id}.frequency`)
      const oscillator = {
        type: 'sine', frequency,
        connect: (target: unknown) => { calls.push([`${id}.connect`, target]); return target },
        start: (at: number) => { calls.push([`${id}.start`, at, oscillator.type, frequency.value]) },
        stop: (at: number) => { calls.push([`${id}.stop`, at]) },
      }
      return oscillator
    },
    createGain: () => {
      const id = `gain${made++}`
      return { id, gain: param(`${id}.gain`), connect: (target: unknown) => { calls.push([`${id}.connect`, target]); return target } }
    },
  }
  return audio
}
function fakeSentinel(): SentinelLike & { fire: () => void } {
  let listener: () => void = () => undefined
  const sentinel = {
    released: false,
    release: async () => { sentinel.released = true },
    addEventListener: (_: 'release', handler: () => void) => { listener = handler },
    fire: () => { sentinel.released = true; listener() },
  }
  return sentinel
}
const status = (): ShiftStatus => ({ started: false, sound: 'off', screen: 'off' })

describe('the tone: short and synthesized, no audio files', () => {
  it('two short notes made with an oscillator, under half a second, never louder than the peak', () => {
    const audio = fakeAudio()
    chime(audio)
    const starts = audio.calls.filter(call => /^osc\d+\.start$/.test(call[0]))
    const stops = audio.calls.filter(call => /^osc\d+\.stop$/.test(call[0]))
    expect(starts).toHaveLength(CHIME.length)
    expect(starts.map(call => call[3])).toEqual(CHIME.map(note => note.frequency))
    expect(Math.max(...stops.map(call => call[1] as number)) - audio.currentTime).toBeLessThan(0.5)
    expect(Math.min(...starts.map(call => call[1] as number))).toBe(audio.currentTime)
    const volumes = audio.calls.filter(call => /gain\.(set|ramp)$/.test(call[0])).map(call => call[1] as number)
    expect(Math.max(...volumes)).toBe(CHIME_PEAK)
    expect(CHIME_PEAK).toBeLessThanOrEqual(1)
    expect(Math.min(...volumes)).toBeGreaterThan(0)                                         // las rampas exponenciales no admiten 0
    expect(audio.calls.filter(call => /gain\d+\.connect$/.test(call[0])).map(call => call[1])).toEqual(CHIME.map(() => 'speakers'))
  })
  it('a suspended context is resumed first (a phone that came back from the lock screen)', () => {
    const audio = fakeAudio('suspended')
    chime(audio)
    expect(audio.calls[0]).toEqual(['resume'])
  })
  it('no audio file anywhere: the CSP of the engine does not change', () => {
    const source = readFileSync(fileURLToPath(new URL('../src/turno.ts', import.meta.url))).toString('utf8')
    expect(source).not.toMatch(/new Audio\(|\.(mp3|wav|ogg|m4a|aac)\b|<audio|fetch\(/)
    // Lo publico: ficheros con extension y carpetas sin ella (sala/, cocina/).
    const publicFiles = (folder: string): string[] => readdirSync(folder).flatMap(name => name.includes('.') ? [name] : publicFiles(`${folder}/${name}`))
    expect(publicFiles(fileURLToPath(new URL('../public', import.meta.url))).filter(name => /\.(mp3|wav|ogg|m4a|aac|flac)$/i.test(name))).toEqual([])
  })
})

describe('«Empezar turno»', () => {
  const https = (sentinel: SentinelLike | Error): ScreenEnv => ({ secure: true, wakeLock: { request: async () => { if (sentinel instanceof Error) throw sentinel; return sentinel } } })
  it('before the touch nothing sounds; the touch starts the shift, rings once so it is heard and keeps the screen on (https)', async () => {
    const audio = fakeAudio(), sentinel = fakeSentinel()
    const shift = new Shift(status(), { audio: () => audio, screen: () => https(sentinel) })
    expect(shift.ring()).toBe(false)                                                      // sin el toque el navegador no deja sonar
    expect(audio.calls).toEqual([])
    await shift.start()
    expect(shift.status).toEqual({ started: true, sound: 'on', screen: 'on' })
    expect(audio.calls.filter(call => /\.start$/.test(call[0]))).toHaveLength(CHIME.length)   // se oye como sonara
    expect(shift.ring()).toBe(true)
  })
  it('the screen lock goes away when the tablet hides the page and comes back when it is seen again', async () => {
    const sentinels = [fakeSentinel(), fakeSentinel()]
    let asked = 0
    const shift = new Shift(status(), { audio: () => fakeAudio(), screen: () => https(sentinels[asked++]) })
    await shift.start()
    sentinels[0].fire()                                                                   // pagina oculta: el navegador la suelta
    await shift.awake()
    expect([asked, shift.status.screen]).toEqual([2, 'on'])
    await shift.awake()                                                                   // ya la tiene: no se pide otra
    expect(asked).toBe(2)
  })
  it('over plain http (B.2) there is no Wake Lock: it says to keep the screen on in the tablet settings', async () => {
    const shift = new Shift(status(), { audio: () => fakeAudio(), screen: () => ({ secure: false, wakeLock: { request: async () => fakeSentinel() } }) })
    expect(screenSupport({ secure: false })).toBe('http')
    expect(shiftIntro('http')).toBe('Tócalo al empezar: así la tablet suena cada vez que llega una comanda nueva. Con esta conexión la pantalla no se queda encendida sola: déjala siempre encendida en los ajustes de la tablet.')
    await shift.start()
    expect(shift.status).toEqual({ started: true, sound: 'on', screen: 'http' })
    expect(screenHint('http')).toBe('Con esta conexión la pantalla no se queda encendida sola: déjala siempre encendida en los ajustes de la tablet.')
  })
  it('a browser without Wake Lock, or one that refuses it, says the same; one without Web Audio says to watch the screen', async () => {
    expect(screenSupport({ secure: true })).toBe('unsupported')
    const refused = new Shift(status(), { audio: () => null, screen: () => https(new Error('NotAllowedError')) })
    await refused.start()
    expect(refused.status).toEqual({ started: true, sound: 'unavailable', screen: 'failed' })
    expect(refused.ring()).toBe(false)
    expect(screenHint('failed')).toBe('Este navegador no mantiene la pantalla encendida: déjala siempre encendida en los ajustes de la tablet.')
    expect(screenHint('unsupported')).toBe(screenHint('failed'))
    expect(soundText('unavailable')).toBe('Turno empezado, pero este navegador no puede sonar: mira la pantalla.')
    const broken = new Shift(status(), { audio: () => { throw new Error('no audio') }, screen: () => https(fakeSentinel()) })
    await broken.start()
    expect(broken.status.sound).toBe('unavailable')
  })
  it('the words: with https the intro promises the screen on; once started it says it rings', () => {
    expect(shiftIntro('available')).toBe('Tócalo al empezar: así la tablet suena cada vez que llega una comanda nueva. Además, la pantalla se queda encendida.')
    expect(soundText('on')).toBe('Turno empezado: suena con cada comanda nueva.')
    expect([soundText('off'), screenHint('off'), screenHint('on')]).toEqual(['', '', 'La pantalla se queda encendida.'])
  })
})

describe('turno.ts itself', () => {
  const source = readFileSync(fileURLToPath(new URL('../src/turno.ts', import.meta.url))).toString('utf8')
  const code = source.replace(/^\s*\/\/.*$/gm, '')
  const literals = [...code.matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])
  it('touches no browser global: the browser arrives as dependencies', () => {
    expect(code).not.toMatch(/\b(window|document|navigator|localStorage|indexedDB)\b|new AudioContext/)
  })
  it('no checkout word, not even inside another word, and Spanish with accents', () => {
    expect(literals.filter(text => /€|precio|saldo|pago|cuenta/i.test(text))).toEqual([])
    const unaccented = ['conexion', 'tocalo', 'ademas', 'dejala', 'asi']
    expect(literals.flatMap(text => unaccented.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)))).toEqual([])
  })
})
