import { ApiError, call, type Fetch } from './api'

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
    claim = await call<Claim>(options.fetcher, '/auth/pairings/claim', { body: { code: code.trim(), deviceName: deviceName.trim() } })
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

// El QR del PC principal abrira https://servidor:5443/app/#pair=CODIGO (D6.5): el codigo llega en el
// fragmento, que el navegador nunca envia al servidor ni queda en sus logs.
export function codeFromLocation(hash: string): string {
  const match = /(?:^#|&)pair=([A-Za-z0-9_-]{8,128})/.exec(hash)
  return match ? match[1] : ''
}
