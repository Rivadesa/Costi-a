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
  // Revision de C3d: la nota se borraba en el mismo toque, antes de saber el resultado; tras un version_conflict, «toca otra vez» obligaba a
  // escribirla de nuevo. Ahora cada nota (las tres revisiones de la pantalla) se borra SOLO tras su resultado, si la orden se aplico o
  // quedo pendiente (cocina.ts, noteSpent; kitchen.spec lo prueba con un version_conflict de verdad).
  it('Costiña Cocina clears a review note only after its outcome, never in the same tap (C3d revision)', () => {
    const kitchen = files.find(([name]) => name === 'views/KitchenView.vue')![1]
    const clearing = kitchen.split('\n').filter(line => /\b(?:notes|restNotes)\[[^\]]+\]\s*=\s*''/.test(line))
    expect(clearing).toHaveLength(3)
    expect(clearing.filter(line => !/if \(noteSpent\(outcome\)\)/.test(line))).toEqual([])
    expect(kitchen).not.toMatch(/void send\([^)]*'review-preparations?'/)
  })
  // C3e.1 (usabilidad 28-10). H34: la estacion lee que hacer con lo que esta por revisar (stationLine), en su rejilla y en «Hecho»; el
  // pase sigue con exceptionLine. H53: «Rehacer» va aparte y en dos pasos; su primer toque solo pregunta y la orden es «Sí, rehacer».
  // H56: «No le afecta a nada más» va arriba, justo bajo la alergia del comensal. H48: los atajos salen de las restricciones aplicables.
  it('Costiña Cocina, C3e.1: the station says what to do, «Rehacer» asks first, «No le afecta a nada más» on top, shortcuts by restriction', () => {
    const kitchen = template(files.find(([name]) => name === 'views/KitchenView.vue')![1])
    const station = kitchen.slice(kitchen.indexOf('<template v-if="!whole">'), kitchen.indexOf('<div v-else class="pase-body">'))
    const pass = kitchen.slice(kitchen.indexOf('<div v-else class="pase-body">'))
    expect(station.length).toBeGreaterThan(0)
    expect(station.match(/stationLine\(preparation\)\.text/g)).toHaveLength(2)                 // la rejilla y la franja «Hecho»
    expect(station).not.toMatch(/exceptionLine\(preparation\)\.text/)
    expect(pass).toMatch(/exceptionLine\(preparation\)\.text/)
    // H53: ninguna lista de decisiones con «Rehacer» dentro; su primer paso no manda nada y la orden sale solo de la pregunta.
    expect(kitchen).not.toMatch(/v-for="option in REVIEW_DECISIONS"/)
    expect(kitchen).toMatch(/v-for="option in DIRECT_DECISIONS"/)
    const ask = kitchen.match(/<button v-if="remaking !== sentKey\(ticket, preparation\)"[^>]*>/)?.[0] ?? ''
    expect(ask).toMatch(/@click="askRemake\(ticket, preparation\)"/)
    expect(ask).toMatch(/:data-testid="`remake-\$\{remakeId\(ticket, preparation\)\}`"/)
    const step = kitchen.slice(kitchen.indexOf('<div v-else class="k-remake-ask"'), kitchen.indexOf('</div>', kitchen.indexOf('<div v-else class="k-remake-ask"') + 1))
    expect(kitchen.indexOf('<div v-else class="k-remake-ask"')).toBeGreaterThan(kitchen.indexOf(ask))
    const confirm = kitchen.slice(kitchen.indexOf('<div v-else class="k-remake-ask"')).match(/<button[^>]*do-review-\$\{REMAKE_DECISION\.decision\}[^>]*>/)?.[0] ?? ''
    expect(confirm).toMatch(/@click="reviewSent\(ticket, preparation, REMAKE_DECISION\.decision, REMAKE_DECISION\.label\)"/)
    expect(step).toMatch(/remakeQuestion\(preparation\)/)
    expect(shown(files.find(([name]) => name === 'views/KitchenView.vue')![1])).toEqual(expect.arrayContaining(['Sí, rehacer', 'Cancelar']))
    // H56: la decision para el resto del comensal, entre su alergia y la lista de sus platos.
    const allergy = kitchen.indexOf('v-for="restriction in guest.restrictions"'), restBlock = kitchen.indexOf('`presend-rest-${guestId(guest)}`'), list = kitchen.indexOf('<ul class="rv-list">')
    expect([allergy > 0, restBlock > allergy, list > restBlock]).toEqual([true, true, true])
    // H48: ningun atajo fijo; cada lista sale de las restricciones que aplican.
    expect(kitchen).not.toMatch(/NOTE_SHORTCUTS/)
    expect(kitchen.match(/v-for="option in (?:noteShortcuts\([^)]*\)|restShortcuts\(guest\))"/g)).toHaveLength(3)
  })
  it('HTML is written by Vue, never pasted: the only v-html is the app icon, drawn from app-icons.ts (C3c)', () => {
    const found = files.filter(([, source]) => /v-html/.test(source)).map(([name]) => name)
    expect(found).toEqual(['views/AppIcon.vue'])
    const icon = files.find(([name]) => name === 'views/AppIcon.vue')![1]
    expect(icon).toMatch(/v-html="body"/)
    expect(icon).toMatch(/const body = computed\(\(\) => iconBody\(props\.app\)\)/)
  })
})

// C3e.1 (usabilidad 28-10, H19 y H20): Costiña Sala, mirando las plantillas y style.css sin abrir un navegador (banner.spec y mesas.spec
// lo comprueban contra el motor). H19: la banda escribe bajo su titulo TODAS las lineas de alergia del pase listo, con la regla, el texto y
// el tono de la ficha. H20: confirmada la orden del boton grande (o de «Servido» de la banda), su resultado ocupa su sitio, apagado.
describe('Costiña Sala, C3e.1: the banner remembers the allergy (H19) and the big button shows its result first (H20)', () => {
  const source = (name: string) => files.find(([file]) => file === name)![1]
  const css = readFileSync(folder('../src/style.css')).toString('utf8').replace(/\r\n/g, '\n').replace(/\/\*[\s\S]*?\*\//g, '')
  const body = (selector: string) => [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].filter(match => match[1].split(',').map(text => text.trim()).includes(selector)).map(match => match[2]).join(';')
  it('H19: under its title the banner writes ALL the lines of the ready course, with the rule and the tone of the table card', () => {
    const banner = source('views/ReadyBanner.vue')
    expect(banner).toMatch(/const lines = computed\(\(\) => first\.value \? passLines\(first\.value\.course\) : \[\]\)/)
    const view = template(banner)
    const list = view.match(/<ul v-if="lines\.length > 0"[^>]*>[\s\S]*?<\/ul>/)?.[0] ?? ''
    expect(list).toMatch(/v-for="line in lines"/)
    expect(list).not.toMatch(/slice|v-show|<details|line-clamp/)                                // todas, sin plegar ni cortar
    expect(list).toMatch(/class="fc-tl-x bn-line" :class="line\.tone"/)
    expect(list).toMatch(/\{\{ line\.text \}\}/)
    // Bajo el titulo y antes de «Servido»: se leen antes de tocarlo, sin un segundo toque.
    const at = (testId: string) => view.indexOf(`data-testid="${testId}"`)
    expect([at('banner-title') < at('banner-lines'), at('banner-lines') < at('banner-serve'), at('banner-lines') < at('banner-done')]).toEqual([true, true, true])
    // La ficha pinta sus lineas con las mismas piezas de pases.ts.
    const card = template(source('views/CoursesTimeline.vue'))
    expect(card).toMatch(/:class="lineTone\(exception\)"/)
    expect(card).toMatch(/\{\{ exceptionText\(dish\.dish, exception\) \}\}/)
    // Compactas, pero nunca escondidas: ni alto maximo ni recorte.
    expect(`${body('.bn-lines')};${body('.banner .bn-line')}`).not.toMatch(/max-height|overflow\s*:\s*hidden|display\s*:\s*none|line-clamp/)
  })
  it('H20: the result takes the place of the button, disabled and without a click: table card, «Abrir» and the banner', () => {
    for (const [name, testId] of [['views/TableCard.vue', 'do-done'], ['views/OpenTable.vue', 'do-done'], ['views/ReadyBanner.vue', 'banner-done']]) {
      const tag = template(source(name)).match(new RegExp(`<button v-if="done"[^>]*data-testid="${testId}"[^>]*>`))?.[0] ?? ''
      expect(tag, name).toMatch(/\sdisabled[\s>]/)
      expect(tag, name).not.toMatch(/@click|:disabled/)
    }
    const board = template(source('views/BoardView.vue'))
    for (const bound of [':done="bannerDone"', ':done="cardDone"', ':done="openDone"', '@run="runFromCard"']) expect(board, bound).toContain(bound)
    // En el verde de lo hecho, nunca en el rojo de las alergias.
    expect(`${body('.btn-big.btn-done:disabled')};${body('.bn-serve.bn-done:disabled')}`).toMatch(/--ok-/)
    expect(`${body('.btn-big.btn-done:disabled')};${body('.bn-serve.bn-done:disabled')}`).not.toMatch(/danger/)
  })
})

// C3e.1 (usabilidad 28-10, H06 y H03): Costiña Sala ya no pierde lo escrito. H06: salir de «Abrir mesa» con alergias escritas («Mesas» o
// «Ver mesa» de la banda) pregunta antes, como la ronda, y encima de «Abrir» va cada alergia entera. H03: las hojas de alergias y de
// omitir esperan el resultado de su orden y solo se cierran si se confirma o queda sin confirmar; si se rechaza, se quedan con lo escrito
// y lo dicen (commands.ts, notApplied; abrir.test y commands.test prueban los textos, mesas.spec el recorrido contra el motor).
describe('Costiña Sala, C3e.1: nothing written is lost when leaving «Abrir mesa» (H06) or when a sheet is rejected (H03)', () => {
  // Con los finales de linea de la copia de trabajo (CRLF en Windows) pasados a \n: aqui se miran lineas enteras.
  const source = (name: string) => files.find(([file]) => file === name)![1].replace(/\r\n/g, '\n')
  const script = (name: string) => { const all = source(name); return all.slice(0, all.indexOf('<template>')) }
  const css = readFileSync(folder('../src/style.css')).toString('utf8').replace(/\r\n/g, '\n').replace(/\/\*[\s\S]*?\*\//g, '')
  const body = (selector: string) => [...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)].filter(match => match[1].split(',').map(text => text.trim()).includes(selector)).map(match => match[2]).join(';')
  it('H06: «Mesas» leaves through leave(), which asks with the rows written; «Ver mesa» of the banner too', () => {
    const open = template(source('views/OpenTable.vue'))
    expect(open.match(/<button[^>]*data-testid="back"[^>]*>/)?.[0] ?? '').toMatch(/@click="leave\(\)"/)
    expect(open).not.toMatch(/emit\('back'\)/)                                                  // en la plantilla, ninguna salida directa
    const ask = open.match(/<section class="sheet" role="alertdialog"[^>]*data-testid="open-leave-sheet"[\s\S]*?<\/section>/)?.[0] ?? ''
    expect(ask).toMatch(/\{\{ LEAVE_TITLE \}\}/)
    expect(ask).toMatch(/\{\{ lost \}\}/)
    expect(ask).toMatch(/v-for="\(line, index\) in draftLines\(rows\)"/)                         // las filas, enteras, para decidir
    // La opcion segura recibe el foco; salir es la de aviso (ambar), nunca la principal.
    const stay = ask.match(/<button[^>]*data-testid="open-leave-stay"[^>]*>/)?.[0] ?? ''
    expect([/\sref="stay"/.test(stay), /\sclass="btn btn-primary"/.test(stay)]).toEqual([true, true])
    expect(ask.match(/<button[^>]*data-testid="open-leave-discard"[^>]*>/)?.[0] ?? '').toMatch(/class="btn btn-caution"[^>]*@click="discardAndLeave"/)
    const openScript = script('views/OpenTable.vue')
    expect(openScript).toMatch(/if \(lost\.value\) \{ afterLeave = then \?\? null; leaving\.value = true; return \}/)
    expect(openScript).toMatch(/defineExpose\(\{ leave \}\)/)
    const board = script('views/BoardView.vue')
    const view = board.slice(board.indexOf('function viewTable('), board.indexOf('}', board.indexOf('selectedId.value = serviceId\n')))
    expect(view.indexOf('opening.value.leave(')).toBeGreaterThan(0)
    expect(view.indexOf('opening.value.leave(')).toBeLessThan(view.indexOf('openingTable.value = null\n'))
    expect(template(source('views/BoardView.vue'))).toMatch(/<OpenTable v-else-if="openingTable" ref="opening"/)
  })
  it('H06: above «Abrir», every allergy written whole with its tone, never folded; the summary no longer counts them', () => {
    const open = template(source('views/OpenTable.vue'))
    const bar = open.slice(open.indexOf('<div class="bar">'), open.indexOf('</div>', open.indexOf('data-testid="do-open"')))
    const at = (testId: string) => bar.indexOf(`data-testid="${testId}"`)
    expect([at('open-summary') >= 0, at('open-summary') < at('open-lines'), at('open-lines') < at('do-open')]).toEqual([true, true, true])
    const list = bar.match(/<ul v-if="lines\.length > 0"[^>]*>[\s\S]*?<\/ul>/)?.[0] ?? ''
    expect(list).toMatch(/v-for="\(line, index\) in lines"/)
    expect(list).toMatch(/class="fc-tl-x open-line" :class="line\.alarming \? 'alarm' : 'warn'"/)
    expect(list).toMatch(/\{\{ line\.text \}\}/)
    expect(list).not.toMatch(/slice|v-show|<details|line-clamp/)
    expect(script('views/OpenTable.vue')).toMatch(/const lines = computed\(\(\) => openLines\(rows\.value\)\)/)
    expect(script('views/OpenTable.vue')).toMatch(/openSummary\(count\.value, chosen\.value\)/)
    expect(`${body('.open-lines')};${body('.open-lines > .open-line')}`).not.toMatch(/max-height|overflow\s*:\s*hidden|display\s*:\s*none|line-clamp/)
  })
  it('H03: the allergy sheet waits for its command and closes only when it is confirmed or left unconfirmed', () => {
    const sheet = script('views/AllergySheet.vue')
    expect(sheet).toMatch(/const emit = defineEmits<\{ close: \[\] \}>\(\)/)                       // ya no emite la orden y se cierra
    expect(sheet).not.toMatch(/emit\('run'/)
    expect(sheet).toMatch(/send: \(path: string, body: Record<string, unknown>, description: string\) => Promise<Settlement>/)
    // Las dos ordenes esperan su resultado; la hoja se cierra SOLO cuando notApplied no tiene nada que decir.
    const closes = sheet.split('\n').filter(line => /emit\('close'\)/.test(line))
    expect(closes).toHaveLength(2)
    expect(closes.filter(line => !/^\s*if \((?:text|why)\) [\w.]+ = (?:text|why); else emit\('close'\)$/.test(line))).toEqual([])
    expect(sheet).toMatch(/notApplied\(settled, restrictionSubject\(restriction\.kind\), 'retirado'\)/)
    expect(sheet).toMatch(/notApplied\(settled, subject, 'anotado'\)/)
    const view = template(source('views/AllergySheet.vue'))
    const declareButton = view.match(/<button[^>]*data-testid="do-declare-restriction"[^>]*>[\s\S]*?<\/button>/)?.[0] ?? ''
    expect(declareButton).toMatch(/:disabled="locked \|\| sending !== null"/)
    expect(declareButton).toMatch(/sending === DECLARE \? 'Anotando…' : 'Anotar alergia o intolerancia'/)
    const removeButton = view.match(/<button[^>]*:data-testid="`do-remove-restriction-[^>]*>[\s\S]*?<\/button>/)?.[0] ?? ''
    expect(removeButton).toMatch(/:disabled="locked \|\| sending !== null"/)
    expect(removeButton).toMatch(/'Retirando…' : 'Retirar'/)
    // El rechazo del alta, junto a su boton: en rojo si lo que no se ha anotado alarma (la regla de la franja), si no en ambar; el de
    // retirar, en el ambar de .notice (la alergia sigue anotada).
    const rejected = view.match(/<p v-if="declined"[^>]*>/)?.[0] ?? ''
    expect(rejected).toMatch(/class="notice fc-al-no" :class="\{ alarm: declinedAlarm \}" role="alert" data-testid="declare-rejected"/)
    expect(sheet).toMatch(/const alarm = restrictionAlarming\(\{ id: '', guestPosition, kind: draft\.kind, substance: text, severity: draft\.severity \}\)/)
    expect(view.indexOf('data-testid="declare-rejected"')).toBeLessThan(view.indexOf('data-testid="do-declare-restriction"'))
    expect(body('.notice.fc-al-no.alarm')).toMatch(/--danger-text/)
    expect(body('.notice.fc-al-no')).not.toMatch(/danger/)
    expect(view.match(/<p v-if="removeProblem"[^>]*>/)?.[0] ?? '').toMatch(/class="notice" /)
  })
  it('H03: the skip sheet waits too, in amber (red is only for allergies); the table card passes both sheets the command that settles', () => {
    const skip = script('views/SkipSheet.vue')
    expect(skip).not.toMatch(/emit\('confirm'/)
    expect(skip).toMatch(/send: \(reason: string\) => Promise<Settlement>/)
    expect(skip).toMatch(/const why = notApplied\(settled, courseName, 'omitido'\)\n\s*if \(why\) problem\.value = why; else emit\('close'\)/)
    const skipView = template(source('views/SkipSheet.vue'))
    expect(skipView.match(/<button[^>]*data-testid="do-skip-confirm"[^>]*>[\s\S]*?<\/button>/)?.[0] ?? '').toMatch(/:disabled="locked \|\| sending"[\s\S]*'Omitiendo…'/)
    expect(skipView.match(/<p v-if="problem"[^>]*>/)?.[0] ?? '').toMatch(/class="notice fc-sk-problem"/)
    expect(`${body('.fc-sk-problem')}`).not.toMatch(/danger/)
    const card = template(source('views/TableCard.vue'))
    expect(card.match(/<AllergySheet[^>]*>/)?.[0] ?? '').toMatch(/:send="settle"/)
    expect(card.match(/<AllergySheet[^>]*>/)?.[0] ?? '').not.toMatch(/@run=/)
    expect(card.match(/<SkipSheet[^>]*>/)?.[0] ?? '').toMatch(/:send="skip"/)
    expect(card.match(/<SkipSheet[^>]*>/)?.[0] ?? '').not.toMatch(/@confirm=/)
    expect(script('views/TableCard.vue')).toMatch(/const settled = await props\.sendSettled\(path, body, description\)/)
    expect(template(source('views/BoardView.vue'))).toContain(':send-settled="sendSettled"')
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
    // Revision de C3d: tambien los atajos de la nota, «Más opciones», «Actualizar» y los botones de la orden sin confirmar.
    for (const selector of ['.todo', '.listo', '.pase-go', '.k-btn', '.k-chip', '.k-more > summary', '.kitchen .status .refresh', '.kitchen .status-panels .btn'])
      expect(value(selector, 'min-height'), selector).toBe('var(--touch-kitchen)')
    for (const selector of ['.kitchen .status .refresh', '.kitchen .status-panels .btn']) expect(value(selector, 'font-size'), selector).toBe('17px')
  })
  // Revision de C3d: lo enviado por revisar se pintaba en rojo aunque la restriccion no alarmara (una intolerancia leve). Lo pendiente es
  // ambar, antes y despues de enviar; el rojo es solo de la linea de la alergia que alarma, encima.
  it('a review still to decide is amber, before and after sending: red is only the alarming allergy line above it', () => {
    for (const selector of ['.review.presend-item', '.review.k-review']) {
      expect([value(selector, 'border-color'), value(selector, 'background')], selector).toEqual(['var(--warn-line)', 'var(--warn-soft)'])
      expect(value(`${selector} .lbl`, 'color'), selector).toBe('var(--warn-text)')
    }
  })
  // Revision de C3d: con algo por revisar, «Esperando al camarero» caia bajo la pantalla de 1180 x 820 y verlo desplazaba las comandas.
  // Desde 1000 px, la columna lateral se queda a la vista (sticky, del alto de la pantalla), «Por revisar» se desplaza dentro y «Esperando
  // al camarero» se queda al pie, tambien al abrir la pagina (sticky abajo).
  it('on the pass the side column stays in view: «Por revisar» scrolls inside it and «Esperando al camarero» stays at its foot', () => {
    expect([value('.pside', 'position'), value('.pside', 'top'), value('.pside', 'max-height')]).toEqual(['sticky', '8px', 'calc(100dvh - 16px)'])
    expect([value('.side-presend', 'min-height'), value('.side-presend > .presend', 'min-height'), value('.side-presend > .presend', 'overflow-y')]).toEqual(['0', '0', 'auto'])
    expect([value('.side-waiting', 'position'), value('.side-waiting', 'bottom'), value('.side-waiting', 'flex')]).toEqual(['sticky', '0', 'none'])
    expect(value('.side-waiting', 'background')).toBe('var(--bg)')
  })
  // C3e.1 (usabilidad 28-10). H53: en la comanda del pase las tres decisiones median 80 px y «No le afecta» se partia en dos lineas; ahora
  // van a lo ancho y sin partirse, y «Rehacer» aparte con borde de aviso (ambar, nunca el rojo de las alergias). H48: los atajos estaban
  // a 6 px: al menos 8 entre objetivos vecinos.
  it('C3e.1: decisions wide and never broken, «Rehacer» apart with a warning border, neighbours at least 8 px apart', () => {
    expect([value('.review .row', 'display'), value('.review .row', 'flex-wrap')]).toEqual(['flex', 'wrap'])
    expect([value('.review .row > .k-btn', 'white-space'), value('.review .row > .k-btn', 'min-width'), value('.review .row > .k-btn', 'flex')]).toEqual(['nowrap', 'max-content', '1 1 0'])
    expect(value('.review .k-remake', 'border')).toBe('2px solid var(--warn-text)')
    expect(value('.k-remake-q', 'min-height')).toBe('var(--touch-kitchen)')                    // la pregunta tapa el sitio del primer toque
    expect(block.match(/\.k-remake[^{]*\{[^}]*\}/g)?.join('') ?? '').not.toMatch(/danger/)
    for (const selector of ['.k-shortcuts', '.review .row', '.k-remake-ask .row'])
      expect(Number(value(selector, 'gap').replace('px', '')), selector).toBeGreaterThanOrEqual(8)
  })
  it('nothing operational under 16 px', () => {
    const sizes = rules.flatMap(rule => [...rule.body.matchAll(/font-size\s*:\s*([\d.]+)px/g)].map(match => [rule.selectors.join(', '), Number(match[1])] as const))
    expect(sizes.length).toBeGreaterThan(20)
    expect(sizes.filter(([, size]) => size < 16)).toEqual([])
  })
  // Capturas de C3d: una fila de «Hecho» con alergias es mas alta; centrada, estiraba a las otras filas y bajaba la etiqueta.
  it('the «Hecho» band aligns its rows at the top: a row with allergies stretches no other', () => {
    expect([value('.done-band', 'align-items'), value('.db-list', 'align-items')]).toEqual(['flex-start', 'flex-start'])
    expect([value('.db-l', 'min-height'), value('.db-empty', 'min-height'), value('.hrow', 'min-height')]).toEqual(['56px', '56px', '56px'])
  })
  it('the time is never red: neutral, amber and dark amber with white text', () => {
    expect(value('.tmr.t-amber', 'background')).toBe('var(--time-amber-bg)')
    expect([value('.tmr.t-late', 'background'), value('.tmr.t-late', 'color')]).toEqual(['var(--time-late-bg)', '#FFFFFF'])
    expect(block.match(/\.tmr[^{]*\{[^}]*\}/g)?.join('') ?? '').not.toMatch(/danger/)
  })
})
