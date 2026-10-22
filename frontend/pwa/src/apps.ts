import type { UnpairOutcome } from './pairing'

// C3c (plan 2026-10-22, seccion 4.1): DOS aplicaciones de la misma compilacion en el mismo origen, cada una con su carpeta bajo
// /app/, su manifiesto, su icono, su service worker y su base IndexedDB. Costina Sala va en el movil de cada camarero y Costina
// Cocina en la tablet fija de cada estacion. Logica pura, sin DOM, cubierta por vitest.
// SEGURIDAD: que aplicacion monta que es una decision de USO, no de permisos. El servidor sigue siendo la frontera: filtra por rol
// y estacion lo que se lee y lo que se puede hacer (affordances, D3.1/D4.3a), sea cual sea la pantalla que lo pinte.
export type AppId = 'sala' | 'cocina'

export interface AppDefinition {
  id: AppId
  name: string            // nombre entero: titulo, cabecera, manifiesto y los mensajes
  path: string            // su carpeta: alcance del manifiesto y del service worker
  database: string        // su base IndexedDB (credencial y orden incierta): nunca la comparte con la otra
  color: string           // solo el icono, theme_color y la marca de la cabecera; los botones siguen con el acento de siempre
  pairTitle: string
  nameLabel: string
  namePlaceholder: string // un ejemplo, nunca un valor relleno: el nombre es el actor de la auditoria y tiene que ser distinto
  purpose: string
}

export const APPS: Readonly<Record<AppId, AppDefinition>> = {
  sala: {
    id: 'sala', name: 'Costiña Sala', path: '/app/sala/', database: 'costina-sala', color: '#0E7A5F',
    pairTitle: 'Conectar este móvil', nameLabel: 'Nombre de este móvil', namePlaceholder: 'móvil de Ana',
    purpose: 'El móvil de cada camarero',
  },
  cocina: {
    id: 'cocina', name: 'Costiña Cocina', path: '/app/cocina/', database: 'costina-cocina', color: '#2F4A6D',
    pairTitle: 'Conectar esta tablet', nameLabel: 'Nombre de esta tablet', namePlaceholder: 'tablet del pase',
    purpose: 'La tablet fija de cada estación de cocina y del pase',
  },
}
export const APP_IDS: readonly AppId[] = ['sala', 'cocina']
export const otherApp = (app: AppId): AppId => app === 'sala' ? 'cocina' : 'sala'

// Que aplicacion corresponde a cada rol. Sala admite tambien el `main` antiguo (un dispositivo principal emparejado antes del corte A),
// con el aviso de siempre en la cabecera. Un rol que no se conoce no tiene aplicacion: no se monta en ninguna (fallo cerrado).
// Un Map y no un objeto: un rol como «constructor» no puede encontrar nada heredado.
const ROLE_APP = new Map<string, AppId>([['service', 'sala'], ['main', 'sala'], ['kitchen', 'cocina']])
export const appForRole = (role: string): AppId | null => ROLE_APP.get(role) ?? null
export const appFits = (app: AppId, role: string): boolean => appForRole(role) === app

// C3c: los tipos de dispositivo con las palabras del plan (sustituyen a «Comandero» y «Pantalla de cocina»). Un papel que no se
// conoce sale tal cual, nunca suavizado.
const KINDS = new Map<string, string>([['main', 'Principal (antiguo)'], ['service', APPS.sala.name], ['kitchen', APPS.cocina.name]])
export const kindOf = (role: string): string => KINDS.get(role) ?? role

interface Who { role: string; station: string | null; stationName?: string | null }
const stationOf = (who: Who) => who.stationName || who.station || ''
// «Costina Cocina · Pase»: lo que es el dispositivo y donde esta, con el NOMBRE de la estacion.
export const connectedAs = (who: Who): string => [kindOf(who.role), stationOf(who)].filter(part => part).join(' · ')
// Cabecera: la marca ya dice la aplicacion; al lado, la estacion. Un rol que no es el natural de la aplicacion (el `main` antiguo)
// se dice delante, como siempre.
const NATURAL: Readonly<Record<AppId, string>> = { sala: 'service', cocina: 'kitchen' }
export const headerRole = (app: AppId, who: Who): string => who.role === NATURAL[app] ? stationOf(who) : connectedAs(who)

// Al emparejar con un codigo que el PC aprobo para la OTRA aplicacion: no se guarda la credencial, el dispositivo se da de baja en
// el servidor (como «Desvincular») y se dice que codigo hay que pedir. Asi no queda ningun dispositivo huerfano.
export function mismatchMessage(app: AppId, role: string, unpaired: UnpairOutcome | 'error'): string {
  const other = appForRole(role)
  const was = other ? `Este código era de ${APPS[other].name}` : `Este código era para un dispositivo «${role}», que esta aplicación no conoce`
  const ask = `pide en el PC uno de ${APPS[app].name}.`
  if (unpaired === 'unpaired' || unpaired === 'already') return `${was}: ${ask}`
  return `${was}: ${ask} No se pudo dar de baja en el servidor: quita el dispositivo desde el PC principal (Inicio › Dispositivos).`
}

// iPhone y iPad: la camara abre el enlace en Safari, nunca en el icono, y el icono de la pantalla de inicio guarda aparte (no ve lo
// que guarda Safari, D6.5). Emparejar en Safari dejaria en el servidor un dispositivo aprobado que el icono no puede usar: fuera del
// icono se explica como anadirlo. iPadOS se presenta como un Mac; se reconoce por la pantalla tactil.
export interface Browser { userAgent: string; maxTouchPoints: number; standalone: unknown; displayStandalone: boolean }
export function pairingPlace(browser: Browser): 'here' | 'add-to-home' {
  const ios = /iPhone|iPad|iPod/.test(browser.userAgent) || (/Macintosh/.test(browser.userAgent) && browser.maxTouchPoints > 1)
  if (!ios) return 'here'
  return browser.standalone === true || browser.displayStandalone ? 'here' : 'add-to-home'
}
export function currentBrowser(): Browser {
  const nav = globalThis.navigator as (Navigator & { standalone?: unknown }) | undefined
  return {
    userAgent: nav?.userAgent ?? '', maxTouchPoints: nav?.maxTouchPoints ?? 0, standalone: nav?.standalone,
    displayStandalone: typeof globalThis.matchMedia === 'function' && globalThis.matchMedia('(display-mode: standalone)').matches,
  }
}
