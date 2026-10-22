import { ApiError, call, NetworkError, type Fetch } from './api'

// Emparejamiento del dispositivo (contrato D4.2): reclamar el codigo de un solo uso, esperar a que el
// PC principal apruebe con rol y estacion, y recoger UNA vez el token. Logica pura y testeable: sin DOM.
export type PairingOutcome =
  | { kind: 'approved'; token: string; role: string; station: string }
  | { kind: 'denied' }
  | { kind: 'expired' }
  | { kind: 'invalid_code' }
  | { kind: 'throttled' }

interface Claim { pairingId: string; pollSecret: string }
interface Collect { status: 'pending' | 'approved' | 'denied'; deviceToken?: string; role?: string; station?: string }

export interface PairingOptions {
  fetcher: Fetch
  wait?: (milliseconds: number) => Promise<void>
  onWaiting?: () => void
  attempts?: number        // 150 x 2 s = los 5 minutos de vida del codigo
  signal?: { cancelled: boolean }
}

export async function pairDevice(code: string, deviceName: string, options: PairingOptions): Promise<PairingOutcome> {
  const wait = options.wait ?? (ms => new Promise<void>(resolve => setTimeout(resolve, ms)))
  let claim: Claim
  try {
    claim = await call<Claim>(options.fetcher, '/auth/pairings/claim', { body: { code: typedCode(code), deviceName: deviceName.trim() } })
  } catch (error) {
    if (error instanceof ApiError && error.status === 429) return { kind: 'throttled' }
    if (error instanceof ApiError) return { kind: 'invalid_code' }
    throw error
  }
  options.onWaiting?.()
  for (let attempt = 0; attempt < (options.attempts ?? 150); attempt++) {
    if (options.signal?.cancelled) return { kind: 'expired' }
    await wait(2000)
    try {
      const collected = await call<Collect>(options.fetcher, `/auth/pairings/${encodeURIComponent(claim.pairingId)}/collect`, { body: { pollSecret: claim.pollSecret } })
      if (collected.status === 'approved' && collected.deviceToken)
        return { kind: 'approved', token: collected.deviceToken, role: collected.role ?? '', station: collected.station ?? '' }
    } catch (error) {
      if (error instanceof ApiError && error.status === 403) return { kind: 'denied' }
      if (error instanceof ApiError && error.status === 404) return { kind: 'expired' }
      if (!(error instanceof ApiError)) continue   // corte de red durante la espera: se sigue sondeando
      throw error
    }
  }
  return { kind: 'expired' }
}

// Desvincular (corte A, F9): el dispositivo se da de baja EN EL SERVIDOR (revocacion a su nombre) antes de olvidar su
// credencial. Sin conexion no se olvida nada: un dispositivo "desvinculado" solo en local seguiria activo en el servidor.
export type UnpairOutcome = 'unpaired' | 'already' | 'offline'
export async function unpairDevice(fetcher: Fetch, token: string): Promise<UnpairOutcome> {
  try { await call(fetcher, '/auth/unpair', { token, body: {} }); return 'unpaired' }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return 'already'
    if (error instanceof NetworkError) return 'offline'
    throw error
  }
}

// C3c: el CODIGO CORTO (10 caracteres de un alfabeto sin ambiguedades, 50 bits) se teclea en iPhone y iPad desde el icono, y el PC
// lo ensena en dos grupos («K7M2Q 9XR4T»). Se manda en su forma canonica: sin espacios ni guiones y en mayusculas, SOLO si asi son
// exactamente 10 caracteres del alfabeto corto. Cualquier otra cosa (el codigo largo de siempre, base64url, que distingue mayusculas
// y puede llevar guiones) va tal cual, sin mas que quitar los espacios de los extremos: normalizarlo lo romperia. El motor aplica
// la misma regla (DeviceStore.ShortForm) y prueba siempre primero el codigo tal cual.
const SHORT_CODE = /^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{10}$/
export function typedCode(input: string): string {
  const trimmed = input.trim()
  // Solo letras y cifras ASCII, como el motor: una letra de otro alfabeto que pase a mayuscula ASCII («ſ» -> «S») no vale.
  const compact = trimmed.replace(/[\s-]+/g, '')
  return /^[A-Za-z0-9]{10}$/.test(compact) && SHORT_CODE.test(compact.toUpperCase()) ? compact.toUpperCase() : trimmed
}

// El QR del PC principal abrira https://servidor:5443/app/#pair=CODIGO (D6.5): el codigo llega en el
// fragmento, que el navegador nunca envia al servidor ni queda en sus logs.
export function codeFromLocation(hash: string): string {
  const match = /(?:^#|&)pair=([A-Za-z0-9_-]{8,128})/.exec(hash)
  return match ? match[1] : ''
}
