import type { CatalogItem } from './api'
import { compareByName, referenceHit, search, searchFields, type Searchable } from './search'
import type { TableConsumption } from './types'

// C3b: «Bodega y bebidas» sin DOM (plan 2026-10-15, seccion 4.4). Espejo de Costina.Client/DrinksPicker.cs (el selector de la mesa
// del puesto principal): las dos implementaciones recorren la tabla de casos comun dotnet/tests/fixtures/drinks-cases.json.
// Solo PRESENTA lo que dice el servidor: que es bebida lo dice su seccion (`bodega`, E3.3) y que es la copa lo dice `glass`.
// Sin precios: el catalogo operativo no los trae (ADR-007) y aqui nunca se piden.
//  - Una fila por producto (las filas del catalogo agrupadas por id), de la A a la Z, con un boton por presentacion: la copa
//    primero y despues el orden del servidor; con una sola presentacion, un unico «Añadir».
//  - Detalle: familia › region (la ruta de categorias, sin la seccion); con una sola presentacion que no es la de por defecto
//    («Unidad», que no se escribe), tambien su nombre.
//  - Accesos rapidos: «En esta mesa» (lo ya enviado, para repetir con +1), «Por copa» y una ficha por familia con sus subfamilias.

export const BODEGA = 'bodega'
export const DEFAULT_PRESENTATION = 'unit'
export const OTHER_FAMILY = 'Otros'
export const ADD = 'Añadir'

export interface DrinkButton { presentationId: string; presentation: string; label: string; glass: boolean }
export interface DrinkProduct extends Searchable {
  id: string; name: string; imageSha: string | null; reference: string | null
  family: string | null; subfamily: string | null; path: string[]; detail: string; glass: boolean; buttons: DrinkButton[]
  search: string[]
}
export interface FamilyChip { family: string | null; label: string; count: number }
export interface SubfamilyChip { name: string; count: number }
export interface TableRepeat { productId: string; presentationId: string; name: string; detail: string; imageSha: string | null; product: DrinkProduct; button: DrinkButton }
// Lo que la ronda necesita de un boton: que se vende (producto y presentacion) y como se nombra.
export interface RoundItem { productId: string; presentationId: string; name: string; presentation: string }

// «Unidad» (la presentacion por defecto) no se escribe, como en la linea del servidor (Catalog.LineText).
export const presentationText = (presentationId: string, presentation: string): string => presentationId === DEFAULT_PRESENTATION ? '' : presentation
export function lineText(name: string, presentationId: string, presentation: string): string {
  const text = presentationText(presentationId, presentation)
  return text ? `${name} · ${text}` : name
}

const present = (value: string | null | undefined): value is string => value !== null && value !== undefined && value.trim().length > 0

// La ruta de categorias sin la seccion; un servidor que no la envia: la familia y la categoria del producto.
function pathOf(row: CatalogItem): string[] {
  if (Array.isArray(row.categoryPath)) return row.categoryPath.filter(present)
  const path: string[] = []
  for (const place of [row.family, row.categoryName]) if (present(place) && !path.includes(place)) path.push(place)
  return path
}

export function drinkProducts(rows: readonly CatalogItem[]): DrinkProduct[] {
  const groups = new Map<string, CatalogItem[]>()
  for (const row of rows) {
    if (row.section !== BODEGA || !present(row.presentationId)) continue
    const group = groups.get(row.id)
    if (!group) groups.set(row.id, [row])
    else if (!group.some(other => other.presentationId === row.presentationId)) group.push(row)
  }
  const products = [...groups.values()].map(group => {
    const first = group[0]
    const path = pathOf(first)
    const family = present(first.family) ? first.family : path[0] ?? null
    const ordered = [...group.filter(row => row.glass === true), ...group.filter(row => row.glass !== true)]
    const single = ordered.length === 1
    const buttons = ordered.map(row => ({ presentationId: row.presentationId!, presentation: row.presentation, label: single ? ADD : row.presentation, glass: row.glass === true }))
    const only = single ? presentationText(buttons[0].presentationId, buttons[0].presentation) : ''
    const reference = present(first.reference) ? first.reference : null
    return {
      id: first.id, name: first.name, imageSha: present(first.imageSha) ? first.imageSha : null, reference,
      family, subfamily: path[1] ?? null, path, detail: [path.join(' › '), only].filter(part => part.length > 0).join(' · '),
      glass: buttons.some(button => button.glass), buttons, search: searchFields(first.name, reference, [family, ...path]),
    }
  })
  return products.sort(compareByName)
}

// Las familias, de la A a la Z; lo que no tiene familia, al final como «Otros».
export function familyChips(products: readonly DrinkProduct[]): FamilyChip[] {
  const counts = new Map<string | null, number>()
  for (const product of products) counts.set(product.family, (counts.get(product.family) ?? 0) + 1)
  const named = [...counts.entries()].filter((entry): entry is [string, number] => entry[0] !== null)
    .map(([family, count]) => ({ family, label: family, count }))
    .sort((a, b) => compareByName({ id: a.family, name: a.label }, { id: b.family, name: b.label }))
  const other = counts.get(null)
  return other === undefined ? named : [...named, { family: null, label: OTHER_FAMILY, count: other }]
}

// Subfamilias (la region en los vinos) de una familia; con menos de dos no hay nada que elegir y no se ofrecen.
export function subfamilyChips(products: readonly DrinkProduct[], family: string | null): SubfamilyChip[] {
  const counts = new Map<string, number>()
  for (const product of products) if (product.family === family && product.subfamily !== null) counts.set(product.subfamily, (counts.get(product.subfamily) ?? 0) + 1)
  if (counts.size < 2) return []
  return [...counts.entries()].map(([name, count]) => ({ name, count })).sort((a, b) => compareByName({ id: a.name, name: a.name }, { id: b.name, name: b.name }))
}

export const inFamily = (products: readonly DrinkProduct[], family: string | null, subfamily: string | null = null): DrinkProduct[] =>
  products.filter(product => product.family === family && (subfamily === null || product.subfamily === subfamily))

export const byGlass = (products: readonly DrinkProduct[]): DrinkProduct[] => products.filter(product => product.glass)

export const searchDrinks = (products: readonly DrinkProduct[], query: string | null | undefined): DrinkProduct[] | null => search(products, query)

// Detalle de la fila con una busqueda: si lo escrito encuentra la referencia, se ensena al final.
export const detailFor = (product: DrinkProduct, query: string | null | undefined): string =>
  referenceHit(query, product.reference) ? [product.detail, `Ref. ${product.reference}`].filter(part => part.length > 0).join(' · ') : product.detail

// «En esta mesa»: lo ya enviado y no anulado, una vez por producto y presentacion, en el orden en que se pidio; solo lo que sigue a la
// venta en Bodega y bebidas (un vino retirado, una linea sin producto o un plato de cocina no se ofrecen para repetir).
export function atThisTable(consumptions: readonly TableConsumption[], products: readonly DrinkProduct[]): TableRepeat[] {
  const sold = new Map<string, { product: DrinkProduct; button: DrinkButton }>()
  for (const product of products) for (const button of product.buttons) sold.set(`${product.id}\u0000${button.presentationId}`, { product, button })
  const seen = new Set<string>()
  const repeats: TableRepeat[] = []
  for (const line of consumptions) {
    if (line.voided || !present(line.productId) || !present(line.presentationId)) continue
    const key = `${line.productId}\u0000${line.presentationId}`
    const found = sold.get(key)
    if (!found || seen.has(key)) continue
    seen.add(key)
    const { product, button } = found
    repeats.push({ productId: product.id, presentationId: button.presentationId, name: product.name,
      detail: presentationText(button.presentationId, button.presentation) || (product.family ?? ''), imageSha: product.imageSha, product, button })
  }
  return repeats
}

export const roundItem = (product: DrinkProduct, button: DrinkButton): RoundItem =>
  ({ productId: product.id, presentationId: button.presentationId, name: product.name, presentation: button.presentation })
