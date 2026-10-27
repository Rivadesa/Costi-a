import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// C3a: lo que las PLANTILLAS escriben tal cual (texto, aria-label, placeholder, title) cumple las reglas del comandero sin abrir un
// navegador: ninguna palabra de caja (ni dentro de otra palabra, como kitchen.spec.ts), castellano con tildes, ni un codigo pintado
// en lugar de un nombre, y nada que la CSP del motor rechace (estilos en linea) ni imagenes (los iconos son SVG en la plantilla).
const folder = (path: string) => fileURLToPath(new URL(path, import.meta.url))
// C3c: las tres paginas (RouterApp, SalaApp y CocinaApp, en src/) y todas las vistas.
const files = [
  ...readdirSync(folder('../src/')).filter(name => name.endsWith('.vue')).map(name => [name, readFileSync(folder(`../src/${name}`)).toString('utf8')]),
  ...readdirSync(folder('../src/views/')).filter(name => name.endsWith('.vue')).map(name => [`views/${name}`, readFileSync(folder(`../src/views/${name}`)).toString('utf8')]),
] as Array<[string, string]>

const template = (source: string) => source.slice(source.indexOf('<template>'), source.lastIndexOf('</template>')).replace(/<!--[\s\S]*?-->/g, '')
// Texto fijo: sin las interpolaciones {{ }} y sin las etiquetas; mas los atributos visibles escritos tal cual (no los enlazados con :).
function shown(source: string): string[] {
  const body = template(source)
  const attributes = [...body.matchAll(/(?<![:\w-])(?:aria-label|placeholder|title|label)="([^"]*)"/g)].map(match => match[1])
  const text = body.replace(/\{\{[\s\S]*?\}\}/g, ' ').replace(/<[^>]*>/g, '\n').split('\n').map(line => line.trim()).filter(line => line.length > 0)
  return [...text, ...attributes]
}
// Literales de las funciones del script (mensajes, descripciones de las ordenes): tambien pueden acabar en pantalla.
const literals = (source: string) => [...source.slice(0, source.indexOf('<template>')).replace(/^\s*\/\/.*$/gm, '').matchAll(/'([^'\n]*)'|`([^`]*)`/g)].map(match => match[1] ?? match[2])

const UNACCENTED = ['aplicacion', 'aplico', 'revision', 'accion', 'estacion', 'codigo', 'conexion', 'envio', 'preparacion', 'eleccion', 'menu', 'menus',
  'restriccion', 'elaboracion', 'economicos', 'desvinculo', 'instalacion', 'ningun', 'aqui', 'alli', 'mas',
  'aprobacion', 'camara', 'tambien', 'habra', 'demostracion', 'anadir', 'quitalo', 'conectalo', 'proxima', 'catalogo', 'ultima']

describe('words the views write as they are', () => {
  it('found something to check in every view (the scan itself works)', () => {
    // Icon, AppIcon y DrinkPhoto solo pintan un dibujo, una foto o unas iniciales; SalaApp y CocinaApp solo montan el marco con su
    // pantalla: no escriben ningun texto propio.
    const silent = ['views/Icon.vue', 'views/AppIcon.vue', 'views/DrinkPhoto.vue', 'SalaApp.vue', 'CocinaApp.vue']
    for (const [name, source] of files.filter(([name]) => !silent.includes(name))) expect(shown(source).length, name).toBeGreaterThan(0)
    expect(files.map(([name]) => name)).toEqual(expect.arrayContaining(['views/TableCard.vue', 'RouterApp.vue', 'SalaApp.vue', 'CocinaApp.vue', 'views/Shell.vue', 'views/PairView.vue', 'views/OtherApp.vue']))
  })
  it('no checkout word, not even inside another word', () => {
    const found = files.flatMap(([name, source]) => [...shown(source), ...literals(source)].filter(text => /€|precio|saldo|pago|cuenta/i.test(text)).map(text => `${name}: ${text}`))
    expect(found).toEqual([])
  })
  it('Spanish with accents: none of the unaccented forms', () => {
    const found = files.flatMap(([name, source]) => shown(source).flatMap(text =>
      UNACCENTED.filter(word => new RegExp(`(^|[^\\p{L}])${word}($|[^\\p{L}])`, 'iu').test(text)).map(word => `${name}: ${word} in «${text}»`)))
    expect(found).toEqual([])
  })
  it('never paints a code where a name goes (table, station, room or offer ids)', () => {
    const found = files.flatMap(([name, source]) => [...template(source).matchAll(/\{\{\s*[\w.?]*\.(tableId|stationId|zoneId|offerId|station|companyId|locationId|productId|presentationId|imageSha)\s*\}\}/g)].map(match => `${name}: ${match[0]}`))
    expect(found).toEqual([])
  })
  it('nothing the CSP of the engine refuses (inline styles) and no images: icons are inline SVG', () => {
    // C3b: la UNICA imagen es la foto de una bebida (DrinkPhoto.vue), con su src ENLAZADO a la URL blob: que crea photos.ts con los
    // bytes pedidos con el token (la CSP solo gana img-src blob:). Ninguna otra vista pinta imagenes, ni con una ruta escrita ni data:.
    const found = files.flatMap(([name, source]) => [...source.matchAll(/(?:\s|:|v-bind:)style=|<style|<img\b/g)]
      .filter(match => !(name === 'views/DrinkPhoto.vue' && match[0] === '<img')).map(match => `${name}: ${match[0]}`))
    expect(found).toEqual([])
    const photo = files.find(([name]) => name === 'views/DrinkPhoto.vue')![1]
    const images = [...template(photo).matchAll(/<img\b[^>]*>/g)].map(match => match[0])
    expect(images).toHaveLength(1)
    expect(images[0]).toMatch(/\s:src="url"/)
    expect(images[0]).not.toMatch(/(?<!:)\bsrc=|data:|https?:/)
  })
  it('the pairing code field is made for typing a code on a phone: capitals, no autocorrect, no spelling (C3c revision)', () => {
    // En iOS, spellcheck="false" no para la autocorreccion: al pulsar el espacio tras un grupo de solo letras la cambiaria por una
    // palabra del diccionario, y el fallo gastaria un intento del freno. El PC lo ensena en mayusculas («K7M2Q 9XR4T»).
    const pair = files.find(([name]) => name === 'views/PairView.vue')![1]
    const input = template(pair).match(/<input id="pair-code"[^>]*>/)?.[0] ?? ''
    for (const attribute of ['autocomplete="off"', 'autocorrect="off"', 'autocapitalize="characters"', 'spellcheck="false"'])
      expect(input, attribute).toContain(` ${attribute}`)
  })
  it('a field and its label never take their id from a preparation id: it repeats between tables and courses (E4e revision)', () => {
    // El id de una elaboracion es <plato>-<comensal>: dos mesas con la misma oferta lo comparten (y un plato puede repetir id en dos
    // pases). Con el mismo id en «Por revisar antes de enviar», la etiqueta de la segunda mesa llevaba el foco a la nota de la PRIMERA y
    // su boton, ya activo, registraba la decision de cocina en otra mesa. El id sale de la posicion en la lista, unica por construccion.
    const bound = /:(?:id|for|name)="`[^`"]*\$\{[^}`"]*preparation\.id[^}`"]*\}[^`"]*`"/g
    const found = files.flatMap(([name, source]) => [...template(source).matchAll(bound)].map(match => `${name}: ${match[0]}`))
    expect(found).toEqual([])
    const kitchen = template(files.find(([name]) => name === 'views/KitchenView.vue')![1])
    const ids = [...kitchen.matchAll(/:id="`([^`"]*)`"/g)].map(match => match[1])
    const labels = [...kitchen.matchAll(/:for="`([^`"]*)`"/g)].map(match => match[1])
    expect(ids.length).toBeGreaterThan(0)
    expect(labels).toEqual(ids)                                         // cada etiqueta apunta a SU campo
  })
  it('Costiña Cocina: a station has only «Listo», never «Empezar» (promoter, 22-10); kitchen.ts is retired (C3d)', () => {
    const kitchen = files.find(([name]) => name === 'views/KitchenView.vue')![1]
    expect(kitchen).not.toMatch(/preparations?-start/)
    expect(shown(kitchen).filter(text => /Empezar/.test(text) && text !== 'Empezar turno')).toEqual([])
    expect(kitchen).not.toMatch(/from '\.\.\/kitchen'/)
    expect(existsSync(folder('../src/kitchen.ts'))).toBe(false)
  })
  it('HTML is written by Vue, never pasted: the only v-html is the app icon, drawn from app-icons.ts (C3c)', () => {
    const found = files.filter(([, source]) => /v-html/.test(source)).map(([name]) => name)
    expect(found).toEqual(['views/AppIcon.vue'])
    const icon = files.find(([name]) => name === 'views/AppIcon.vue')![1]
    expect(icon).toMatch(/v-html="body"/)
    expect(icon).toMatch(/const body = computed\(\(\) => iconBody\(props\.app\)\)/)
  })
})

// C3d (plan 2026-10-22, seccion 4.3): Costiña Cocina se lee a uno o dos metros, en una tablet horizontal de 1180 x 820. La hoja de la
// cocina va en su propio bloque de style.css; aqui se miden sus reglas sin abrir un navegador (la E2E mide los botones de verdad).
describe('Costiña Cocina reads at one or two metres (style.css)', () => {
  const css = readFileSync(folder('../src/style.css')).toString('utf8').replace(/\r\n/g, '\n')
  const start = css.indexOf('/* ---------- C3d: Costiña Cocina'), end = css.indexOf('/* ---------- Fin de Costiña Cocina')
  const block = start >= 0 && end > start ? css.slice(start, end).replace(/\/\*[\s\S]*?\*\//g, '') : ''
  const rules = [...block.matchAll(/([^{}]+)\{([^{}]*)\}/g)].map(match => ({ selectors: match[1].split(',').map(text => text.trim()), body: match[2] }))
  const value = (selector: string, property: string) => {
    const found = rules.filter(rule => rule.selectors.includes(selector)).map(rule => rule.body.match(new RegExp(`(?:^|;)\\s*${property}\\s*:\\s*([^;]+)`))?.[1].trim()).filter(Boolean)
    return found.at(-1) ?? ''
  }
  it('has its own block', () => { expect(rules.length).toBeGreaterThan(20) })
  it('ticket title 24 px, dishes 22 px with the quantity at 28 px, the allergy line 18 px bold and the time 18 px', () => {
    expect(value('.tk-name', 'font-size')).toBe('24px')
    expect([value('.dg-n b', 'font-size'), value('.pd-n', 'font-size')]).toEqual(['22px', '22px'])
    expect([value('.dg-q', 'font-size'), value('.pd-q', 'font-size')]).toEqual(['28px', '28px'])
    expect([value('.k-al', 'font-size'), value('.k-al', 'font-weight')]).toEqual(['18px', '700'])
    expect(value('.tmr', 'font-size')).toBe('18px')
  })
  it('the kitchen buttons are 64 px high', () => {
    expect(css).toMatch(/--touch-kitchen: 64px/)
    for (const selector of ['.todo', '.listo', '.pase-go', '.k-btn']) expect(value(selector, 'min-height'), selector).toBe('var(--touch-kitchen)')
  })
  it('nothing operational under 16 px', () => {
    const sizes = rules.flatMap(rule => [...rule.body.matchAll(/font-size\s*:\s*([\d.]+)px/g)].map(match => [rule.selectors.join(', '), Number(match[1])] as const))
    expect(sizes.length).toBeGreaterThan(20)
    expect(sizes.filter(([, size]) => size < 16)).toEqual([])
  })
  it('the time is never red: neutral, amber and dark amber with white text', () => {
    expect(value('.tmr.t-amber', 'background')).toBe('var(--time-amber-bg)')
    expect([value('.tmr.t-late', 'background'), value('.tmr.t-late', 'color')]).toEqual(['var(--time-late-bg)', '#FFFFFF'])
    expect(block.match(/\.tmr[^{]*\{[^}]*\}/g)?.join('') ?? '').not.toMatch(/danger/)
  })
})
