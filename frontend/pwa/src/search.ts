// C3b: la busqueda de «Bodega y bebidas» (plan 2026-10-15, seccion 4.4). Es la MISMA regla que la del puesto principal
// (Costina.Client/CatalogSearch.cs, que usan el selector del Comedor y el buscador de Caja de E3.3): las dos implementaciones
// recorren la tabla de casos comun dotnet/tests/fixtures/drinks-cases.json (vitest y ClientChecks). Solo trabaja sobre la lista ya
// leida, en memoria: ni servidor ni dependencias.
//  - Sin tildes ni mayusculas: se compara el texto plegado (descompuesto, sin marcas combinantes y en minusculas). La ene cuenta como n.
//  - Por nombre, referencia, familia y region (la ruta de categorias). La uva, la anada o la bodega solo si estan en el nombre.
//  - Desde 2 letras (sin contar los espacios de los extremos). Cada palabra escrita tiene que estar en algun campo.
//  - Orden: primero lo que EMPIEZA por la primera palabra (o cuya referencia es exactamente lo escrito), despues lo que la lleva al
//    empezar una palabra del nombre y al final el resto; dentro de cada grupo, por nombre plegado (orden de unidades UTF-16, el mismo
//    en los dos clientes: nada de reglas de idioma que dependan de la version de ICU de cada uno).

export const MIN_LETTERS = 2

// Plegado: NFD, sin marcas combinantes (categoria Mn: tildes, dieresis, la virgulilla de la ene, la cedilla) y en minusculas.
export const fold = (text: string | null | undefined): string => (text ?? '').normalize('NFD').replace(/\p{Mn}/gu, '').toLowerCase()

export const tokens = (query: string | null | undefined): string[] => fold(query).split(/\s+/).filter(token => token.length > 0)

export const isSearching = (query: string | null | undefined): boolean => fold(query).trim().length >= MIN_LETTERS

// Campos en los que se busca, ya plegados y sin repetir: nombre, referencia y lugares (familia y ruta de categorias).
export function searchFields(name: string, reference: string | null | undefined, places: ReadonlyArray<string | null | undefined>): string[] {
  const fields: string[] = []
  for (const value of [name, reference, ...places]) {
    const folded = fold(value)
    if (folded.length > 0 && !fields.includes(folded)) fields.push(folded)
  }
  return fields
}

export const matches = (words: readonly string[], fields: readonly string[]): boolean =>
  words.every(word => fields.some(field => field.includes(word)))

// Palabras de un texto plegado: tramos de letras y cifras (como Labels.Vocabulary.Key del puesto).
const words = (folded: string): string[] => folded.split(/[^\p{L}\p{Nd}]+/u).filter(word => word.length > 0)

// 0: el nombre empieza por la primera palabra, o la referencia es exactamente lo escrito; 1: una palabra del nombre empieza por
// ella; 2: el resto (familia, region, dentro de una palabra o parte de la referencia).
export function rank(written: readonly string[], name: string, reference: string | null | undefined): number {
  const first = written[0] ?? ''
  const folded = fold(name)
  if (folded.startsWith(first) || written.length === 1 && fold(reference) === first) return 0
  return words(folded).some(word => word.startsWith(first)) ? 1 : 2
}

const ordinal = (a: string, b: string): number => a < b ? -1 : a > b ? 1 : 0

// De la A a la Z sin tildes ni mayusculas; empate por el nombre tal cual y por el identificador (orden total y estable).
export const compareByName = (a: { id: string; name: string }, b: { id: string; name: string }): number =>
  ordinal(fold(a.name), fold(b.name)) || ordinal(a.name, b.name) || ordinal(a.id, b.id)

export interface Searchable { id: string; name: string; reference: string | null; search: readonly string[] }

// null = aun no se busca (menos de 2 letras): la pantalla sigue con los accesos rapidos y las familias.
export function search<T extends Searchable>(items: readonly T[], query: string | null | undefined): T[] | null {
  if (!isSearching(query)) return null
  const written = tokens(query)
  return items.filter(item => matches(written, item.search))
    .map(item => ({ item, rank: rank(written, item.name, item.reference) }))
    .sort((a, b) => a.rank - b.rank || compareByName(a.item, b.item))
    .map(entry => entry.item)
}

// La referencia se ensena en la fila cuando lo escrito la encuentra (el camarero la ha tecleado y espera verla).
export const referenceHit = (query: string | null | undefined, reference: string | null | undefined): boolean =>
  isSearching(query) && reference !== null && reference !== undefined && tokens(query).some(word => fold(reference).includes(word))
