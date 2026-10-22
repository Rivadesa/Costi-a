import { describe, expect, it } from 'vitest'
import { appFits, appForRole, APPS, connectedAs, headerRole, isIos, kindOf, mismatchMessage, pairingPlace, pairingScreen, type Browser } from '../src/apps'
import { typedCode } from '../src/pairing'

// C3c: dos aplicaciones, Costina Sala y Costina Cocina. Que aplicacion monta que credencial es USO, no seguridad (el servidor filtra
// por rol y estacion), pero tiene que ser exacto y fallar cerrado ante lo que no conoce.
describe('which app fits which role', () => {
  it('Sala takes service and the old main; Cocina only kitchen', () => {
    expect([appFits('sala', 'service'), appFits('sala', 'main'), appFits('sala', 'kitchen')]).toEqual([true, true, false])
    expect([appFits('cocina', 'kitchen'), appFits('cocina', 'service'), appFits('cocina', 'main')]).toEqual([true, false, false])
    expect([appForRole('service'), appForRole('main'), appForRole('kitchen')]).toEqual(['sala', 'sala', 'cocina'])
  })
  it('an unknown role mounts in neither app, not even one named like an inherited property, and is shown raw', () => {
    for (const role of ['bar', 'barra', '', 'constructor', 'toString', '__proto__', 'Service', 'KITCHEN']) {
      expect(appForRole(role), role).toBeNull()
      expect([appFits('sala', role), appFits('cocina', role)], role).toEqual([false, false])
    }
    expect(kindOf('bar')).toBe('bar')
    expect(kindOf('constructor')).toBe('constructor')
    expect(connectedAs({ role: 'bar', station: 'barra-1', stationName: null })).toBe('bar · barra-1')
  })
  it('says what the device is with the app names and the station NAME, never its code when the name is known', () => {
    expect(connectedAs({ role: 'kitchen', station: 'pase', stationName: 'Pase' })).toBe('Costiña Cocina · Pase')
    expect(connectedAs({ role: 'service', station: 'sala-1', stationName: 'Sala 1' })).toBe('Costiña Sala · Sala 1')
    expect(connectedAs({ role: 'main', station: null })).toBe('Principal (antiguo)')
    expect(connectedAs({ role: 'kitchen', station: 'cold' })).toBe('Costiña Cocina · cold')   // un servidor anterior sin stationName
  })
  it('the header shows only the station for the natural role, and keeps the old main warning', () => {
    expect(headerRole('sala', { role: 'service', station: 'sala-1', stationName: 'Sala 1' })).toBe('Sala 1')
    expect(headerRole('cocina', { role: 'kitchen', station: 'hot', stationName: 'Cocina caliente' })).toBe('Cocina caliente')
    expect(headerRole('sala', { role: 'main', station: 'sala-1', stationName: 'Sala 1' })).toBe('Principal (antiguo) · Sala 1')
  })
  it('a kitchen tablet without a station says so next to the brand (the help paragraph is gone, C3c); Sala keeps it empty', () => {
    expect(headerRole('cocina', { role: 'kitchen', station: null })).toBe('Sin estación asignada')
    expect(headerRole('cocina', { role: 'kitchen', station: null, stationName: null })).toBe('Sin estación asignada')
    expect(headerRole('cocina', { role: 'kitchen', station: 'cold' })).toBe('cold')                 // un servidor anterior sin stationName
    expect(headerRole('sala', { role: 'service', station: null })).toBe('')
  })
  it('each app has its own folder, base and colour; Cocina is slate blue, never red (allergies) nor amber (pending)', () => {
    expect([APPS.sala.path, APPS.sala.database, APPS.sala.color]).toEqual(['/app/sala/', 'costina-sala', '#0E7A5F'])
    expect([APPS.cocina.path, APPS.cocina.database, APPS.cocina.color]).toEqual(['/app/cocina/', 'costina-cocina', '#2F4A6D'])
    expect(APPS.sala.database).not.toBe('costina')
  })
})

describe('pairing with a code of the other app (C3c)', () => {
  it('names the app the code was for and the one to ask for, and says when the server could not be told', () => {
    expect(mismatchMessage('sala', 'kitchen', 'unpaired')).toBe('Este código era de Costiña Cocina: pide en el PC uno de Costiña Sala.')
    expect(mismatchMessage('cocina', 'service', 'already')).toBe('Este código era de Costiña Sala: pide en el PC uno de Costiña Cocina.')
    expect(mismatchMessage('cocina', 'bar', 'unpaired')).toContain('«bar»')
    for (const outcome of ['offline', 'error'] as const) expect(mismatchMessage('sala', 'kitchen', outcome)).toContain('quita el dispositivo desde el PC principal')
  })
})

describe('where pairing happens: iPhone and iPad pair from the icon, and in Safari only as a way out', () => {
  const browser = (userAgent: string, more: Partial<Browser> = {}): Browser => ({ userAgent, maxTouchPoints: 5, standalone: undefined, displayStandalone: false, ...more })
  const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'
  const IPAD = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15'   // iPadOS se presenta como un Mac
  const ANDROID = 'Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36'
  it('Safari on iPhone and iPad explains how to add the icon', () => {
    expect(pairingPlace(browser(IPHONE, { standalone: false }))).toBe('add-to-home')
    expect(pairingPlace(browser(IPAD, { standalone: false }))).toBe('add-to-home')
    expect(pairingPlace(browser(IPHONE))).toBe('add-to-home')                                   // otro navegador de iOS: tampoco es el icono
  })
  it('the home-screen icon pairs', () => {
    expect(pairingPlace(browser(IPHONE, { standalone: true }))).toBe('here')
    expect(pairingPlace(browser(IPAD, { standalone: false, displayStandalone: true }))).toBe('here')
  })
  it('Android and a desktop pair where the QR opens them', () => {
    expect(pairingPlace(browser(ANDROID))).toBe('here')
    expect(pairingPlace(browser(IPAD, { maxTouchPoints: 0, standalone: undefined }))).toBe('here')   // un Mac de verdad
    expect(pairingPlace(browser('Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0.0.0', { maxTouchPoints: 0 }))).toBe('here')
  })
  // Salida de emergencia: sin verificar si el icono abierto por http va a pantalla completa; si no, sin ella no se emparejaria nunca.
  it('the way out in Safari: the add-to-home steps come first, and the form only once Safari pairing is chosen', () => {
    for (const safari of [browser(IPHONE, { standalone: false }), browser(IPAD, { standalone: false }), browser(IPHONE)]) {
      expect(pairingScreen(pairingPlace(safari), false)).toBe('add-to-home')
      expect(pairingScreen(pairingPlace(safari), true)).toBe('form-in-safari')
    }
  })
  it('the way out never changes the normal paths: the icon, Android and a desktop always get the usual form', () => {
    const elsewhere = [browser(IPHONE, { standalone: true }), browser(IPAD, { displayStandalone: true }), browser(ANDROID),
      browser(IPAD, { maxTouchPoints: 0 }), browser('Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0.0.0', { maxTouchPoints: 0 })]
    for (const other of elsewhere) for (const chosen of [false, true]) expect(pairingScreen(pairingPlace(other), chosen)).toBe('form')
  })
  it('isIos: iPhone and iPad, in Safari or in the icon, where the camera always opens Safari (the icon pairs by typing the code)', () => {
    expect(isIos(browser(IPHONE, { standalone: true }))).toBe(true)
    expect(isIos(browser(IPHONE, { standalone: false }))).toBe(true)
    expect(isIos(browser(IPAD, { displayStandalone: true }))).toBe(true)
    expect(isIos(browser(ANDROID))).toBe(false)
    expect(isIos(browser(IPAD, { maxTouchPoints: 0 }))).toBe(false)                              // un Mac de verdad
  })
})

describe('typed pairing code', () => {
  it('the short code is accepted as the PC shows it: two groups, hyphens, lower case', () => {
    expect(typedCode('K7M2Q 9XR4T')).toBe('K7M2Q9XR4T')
    expect(typedCode('  k7m2q-9xr4t ')).toBe('K7M2Q9XR4T')
    expect(typedCode('K7M2Q\t9XR4T')).toBe('K7M2Q9XR4T')
  })
  it('the long code is never normalised: it is base64url, case-sensitive and may carry hyphens', () => {
    const long = 'aB3-dEf_ghIJ-klmNOpqr-STUvwxYZ_0123456789ab'
    expect(typedCode(` ${long} `)).toBe(long)
    expect(typedCode('Ab-Cd')).toBe('Ab-Cd')
  })
  it('only ASCII letters and digits of the short alphabet, exactly ten, become a short code', () => {
    expect(typedCode('K7M2Q 9XR4')).toBe('K7M2Q 9XR4')                 // nueve: tal cual
    expect(typedCode('K7M2Q 9XR4TT')).toBe('K7M2Q 9XR4TT')             // once
    expect(typedCode('O7M2Q 9XR4T')).toBe('O7M2Q 9XR4T')               // la O no esta en el alfabeto corto
    expect(typedCode('ſ7M2Q 9XR4T')).toBe('ſ7M2Q 9XR4T')               // «ſ» pasaria a «S» en mayuscula: no vale
  })
})
