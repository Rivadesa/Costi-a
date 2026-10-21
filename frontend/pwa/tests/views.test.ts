import { readdirSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// C3a: lo que las PLANTILLAS escriben tal cual (texto, aria-label, placeholder, title) cumple las reglas del comandero sin abrir un
// navegador: ninguna palabra de caja (ni dentro de otra palabra, como kitchen.spec.ts), castellano con tildes, ni un codigo pintado
// en lugar de un nombre, y nada que la CSP del motor rechace (estilos en linea) ni imagenes (los iconos son SVG en la plantilla).
const folder = (path: string) => fileURLToPath(new URL(path, import.meta.url))
const files = [
  ['App.vue', readFileSync(folder('../src/App.vue')).toString('utf8')],
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
    // Icon y DrinkPhoto solo pintan un dibujo, una foto o unas iniciales: no escriben ningun texto propio.
    for (const [name, source] of files.filter(([name]) => !['views/Icon.vue', 'views/DrinkPhoto.vue'].includes(name))) expect(shown(source).length, name).toBeGreaterThan(0)
    expect(files.map(([name]) => name)).toContain('views/TableCard.vue')
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
})
