import type { InjectionKey } from 'vue'
import type { Fetch } from './api'

// C3b: fotos de «Bodega y bebidas» (plan 2026-10-15, seccion 4.4). E3.2 guarda una foto por producto, que cualquier identidad valida
// lee en GET /catalog/images/{producto} con su token: un <img> no puede mandar el token, asi que la PWA pide los bytes y los pinta con
// URL.createObjectURL (la CSP solo gana img-src blob:, sin terceros ni data:). Reglas:
//  - solo se piden las de las filas VISIBLES (DrinkPhoto.vue, IntersectionObserver) y solo si su huella (imageSha) no esta ya en memoria;
//  - se guardan en memoria por huella, hasta unas 150 o 20 MB; al pasarse se descarta la que lleva mas tiempo sin verse y se libera su
//    URL (revokeObjectURL). Nada va a IndexedDB ni a la cache del navegador (no-store), y el service worker sigue sin tocar /api/;
//  - solo JPEG, PNG o WebP (lo unico que guarda el motor); cualquier otra cosa, un 404 o un fallo: recuadro con las iniciales;
//  - una peticion por huella aunque la pidan varias filas a la vez.
const PREFIX = '/api/native/v1'
export const MAX_PHOTOS = 150
export const MAX_PHOTO_BYTES = 20 * 1024 * 1024
const TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])

export interface PhotoDeps {
  fetcher: Fetch
  token: () => string
  createUrl: (blob: Blob) => string
  revokeUrl: (url: string) => void
  maxCount?: number
  maxBytes?: number
}

export class PhotoCache {
  private readonly entries = new Map<string, { url: string; bytes: number }>()   // orden = de la menos a la mas reciente
  private readonly loading = new Map<string, Promise<string | null>>()
  private readonly missing = new Set<string>()      // sin foto en el servidor (404) o con un tipo que no es una foto: no se repite
  private used = 0
  private generation = 0
  private readonly http: Fetch

  constructor(private readonly deps: PhotoDeps) {
    // window.fetch exige invocarse SIN receptor (D6.3): se guarda como funcion suelta.
    const fetcher = deps.fetcher
    this.http = (input, init) => fetcher(input, init)
  }

  get size(): number { return this.entries.size }
  get bytes(): number { return this.used }

  // La URL ya en memoria (y pasa a ser la mas reciente), o null.
  cached(sha: string | null | undefined): string | null {
    if (!sha) return null
    const entry = this.entries.get(sha)
    if (!entry) return null
    this.entries.delete(sha); this.entries.set(sha, entry)
    return entry.url
  }

  load(productId: string, sha: string | null | undefined): Promise<string | null> {
    if (!sha) return Promise.resolve(null)
    const hit = this.cached(sha)
    if (hit !== null) return Promise.resolve(hit)
    if (this.missing.has(sha)) return Promise.resolve(null)
    let work = this.loading.get(sha)
    if (!work) {
      work = this.fetchPhoto(productId, sha).finally(() => { this.loading.delete(sha) })
      this.loading.set(sha, work)
    }
    return work
  }

  // Al salir del comandero (o desvincular): se liberan todas las URL. Lo que aun este en camino se descarta al llegar.
  clear(): void {
    this.generation++
    for (const entry of this.entries.values()) this.deps.revokeUrl(entry.url)
    this.entries.clear(); this.loading.clear(); this.missing.clear(); this.used = 0
  }

  private async fetchPhoto(productId: string, sha: string): Promise<string | null> {
    const generation = this.generation
    let response: Response
    try {
      response = await this.http(`${PREFIX}/catalog/images/${encodeURIComponent(productId)}`, {
        method: 'GET', headers: { Accept: 'image/jpeg, image/png, image/webp', Authorization: `Bearer ${this.deps.token()}` },
        cache: 'no-store', credentials: 'omit', redirect: 'error',
      })
    } catch { return null }                          // sin red: se vuelve a pedir la proxima vez que se vea
    // 404: el producto ya no tiene foto. 401, 5xx...: pasajero, se reintenta la proxima vez.
    if (response.status === 404) { this.missing.add(sha); return null }
    if (!response.ok) return null
    const type = (response.headers.get('Content-Type') ?? '').split(';')[0].trim().toLowerCase()
    if (!TYPES.has(type)) { this.missing.add(sha); return null }
    let blob: Blob
    try { blob = await response.blob() } catch { return null }
    const limit = this.deps.maxBytes ?? MAX_PHOTO_BYTES
    if (blob.size === 0 || blob.size > limit) { this.missing.add(sha); return null }
    if (generation !== this.generation) return null
    const url = this.deps.createUrl(blob.type === type ? blob : new Blob([blob], { type }))
    this.store(sha, url, blob.size)
    return url
  }

  private store(sha: string, url: string, bytes: number): void {
    const old = this.entries.get(sha)
    if (old) { this.entries.delete(sha); this.used -= old.bytes; if (old.url !== url) this.deps.revokeUrl(old.url) }
    this.entries.set(sha, { url, bytes }); this.used += bytes
    const maxCount = this.deps.maxCount ?? MAX_PHOTOS, maxBytes = this.deps.maxBytes ?? MAX_PHOTO_BYTES
    // Se descarta la que lleva mas tiempo sin verse; nunca la que acaba de llegar.
    for (const [oldest, entry] of this.entries) {
      if (this.entries.size <= maxCount && this.used <= maxBytes) break
      if (oldest === sha) continue
      this.entries.delete(oldest); this.used -= entry.bytes; this.deps.revokeUrl(entry.url)
    }
  }
}

export const PHOTOS: InjectionKey<PhotoCache> = Symbol('photos')

// Recuadro sin foto: las iniciales del nombre («Albariño Pazo de Señoráns» -> «AP»), sin las palabras de enlace ni las cifras (la anada
// no es una inicial: «Lalama 2019» -> «L»). Un nombre sin ninguna palabra, con su primer caracter.
const LINKS = new Set(['de', 'del', 'la', 'las', 'el', 'los', 'y', 'e', 'a', 'con', 'sin', 'en', 'al'])
export function initials(name: string): string {
  const words = name.split(/[^\p{L}\p{Nd}]+/u).filter(word => /^\p{L}/u.test(word))
  const first = words[0]
  if (first === undefined) return [...name.trim()][0]?.toLocaleUpperCase('es') ?? '?'
  const second = words.slice(1).find(word => !LINKS.has(word.toLowerCase()))
  return [first, second].filter((word): word is string => word !== undefined).map(word => [...word][0]).join('').toLocaleUpperCase('es')
}
