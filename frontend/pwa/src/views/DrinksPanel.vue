<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { addLabel, chipHeader, chipProducts, chosenChip, drinkChips, formatParts, NO_RESULTS_HINT, noResults, ONE_MORE_LETTER, PAGE, resultsCount, RESULTS_WHERE, type DrinkChip } from '../bodega'
import type { CatalogStatus } from '../catalog'
import { atThisTable, detailFor, roundItem, searchDrinks, subfamilyChips, type DrinkButton, type DrinkProduct, type RoundItem } from '../drinks'
import type { Round } from '../round'
import { fold } from '../search'
import type { BoardEntry } from '../types'
import DrinkPhoto from './DrinkPhoto.vue'
import Icon from './Icon.vue'

// C3b: «Bodega y bebidas» de la ficha de una mesa (plan 2026-10-15, seccion 4.4; maqueta «2 · Bodega y bebidas»). Solo la seccion
// `bodega` del catalogo operativo, que NO trae precios (ADR-007): aqui nunca se ve ni se manda un importe; lo fija el servidor.
//  - El buscador, siempre arriba: sin tildes ni mayusculas, por nombre, referencia, familia y region, desde 2 letras (search.ts, la misma
//    regla que el puesto principal).
//  - Antes de buscar: «Por copa» (lo que el servidor marca como copa) y las familias, con sus subfamilias; en la vista de entrada,
//    «En esta mesa» para repetir con +1 lo ya enviado.
//  - Una fila por producto con su foto, nombre, familia › region y un boton por presentacion (la copa primero; con una sola, «Añadir»).
//  - UN TOQUE SUMA 1 A LA RONDA, para toda la mesa, sin hoja intermedia. La ronda (y su bandeja) es de la ficha: sigue al volver a la mesa.
// Las filas se pintan por tandas (PAGE) para que una familia de cientos de vinos no llene el movil de golpe.
const props = defineProps<{ entry: BoardEntry; products: DrinkProduct[]; status: CatalogStatus; canConsume: boolean; round: Round }>()
const emit = defineEmits<{ add: [item: RoundItem] }>()

const query = ref('')
const chipKey = ref<string | null>(null)
const subfamily = ref<string | null>(null)
const visible = ref(PAGE)

const chips = computed(() => drinkChips(props.products))
const chip = computed(() => chosenChip(chips.value, chipKey.value))
const entrance = computed(() => chip.value !== null && chip.value.key === chips.value[0]?.key)
const searching = computed(() => searchDrinks(props.products, query.value))
const oneLetter = computed(() => fold(query.value).trim().length === 1)
const subchips = computed(() => chip.value === null || chip.value.glass ? [] : subfamilyChips(props.products, chip.value.family))
const listed = computed(() => chipProducts(props.products, chip.value, subchips.value.some(sub => sub.name === subfamily.value) ? subfamily.value : null))
const rows = computed(() => (searching.value ?? listed.value).slice(0, visible.value))
const left = computed(() => (searching.value ?? listed.value).length - rows.value.length)
// «En esta mesa»: lo ya enviado y no anulado, que sigue a la venta, una vez por producto y presentacion (drinks.ts).
const repeats = computed(() => atThisTable(props.entry.consumptions ?? [], props.products))

watch([query, chipKey, subfamily], () => { visible.value = PAGE })
function pick(next: DrinkChip): void { chipKey.value = next.key; subfamily.value = null; query.value = '' }
const add = (product: DrinkProduct, button: DrinkButton) => emit('add', roundItem(product, button))
const detail = (product: DrinkProduct) => detailFor(product, searching.value === null ? '' : query.value)
</script>

<template>
  <section class="bodega" data-testid="panel-bodega">
    <p v-if="!canConsume" class="notice neutral" data-testid="bodega-empty"><Icon name="info" />Este dispositivo no puede añadir bebidas ni extras a la mesa.</p>
    <template v-else>
      <div class="searchbar">
        <label class="search">
          <Icon name="search" :size="22" />
          <span class="sr">Buscar en la bodega y bebidas</span>
          <input v-model="query" type="search" name="drinks-search" placeholder="Busca un vino, una cerveza…" autocomplete="off" spellcheck="false"
            enterkeyhint="search" data-testid="drinks-search" />
          <button v-if="query" type="button" class="search-x" aria-label="Borrar la búsqueda" data-testid="search-clear" @click="query = ''"><Icon name="x" :size="20" :stroke="2.2" /></button>
        </label>
      </div>

      <p v-if="products.length === 0 && status.readAt === null && !status.failed" class="muted pad" data-testid="bodega-reading">Leyendo la bodega y bebidas…</p>
      <p v-else-if="products.length === 0 && status.failed" class="notice" role="status" data-testid="bodega-failed"><Icon name="info" />No se pudo leer la bodega y bebidas. Se vuelve a intentar al abrirla y cada 5 minutos.</p>
      <p v-else-if="products.length === 0" class="notice neutral" data-testid="bodega-empty"><Icon name="info" />No hay bebidas ni extras a la venta en el catálogo.</p>
      <div v-else class="bodega-b">
        <nav class="types" aria-label="Tipos de bebida">
          <button v-for="(option, index) in chips" :key="option.key" type="button" class="chip" :class="{ 'chip-glass': option.glass }"
            :aria-pressed="searching === null && option.key === chip?.key" :data-testid="option.glass ? 'chip-glass' : `chip-${index}`" @click="pick(option)">
            <Icon v-if="option.glass" name="glass" :size="18" /><span class="chip-t">{{ option.label }}</span><span class="cnt">{{ option.count }}</span></button>
        </nav>

        <div class="results" data-testid="drinks-results">
          <p v-if="status.failed" class="notice" role="status" data-testid="bodega-stale"><Icon name="info" />No se pudo releer la bodega y bebidas: se ve la última lista leída.</p>
          <template v-if="searching !== null">
            <div v-if="searching.length === 0" class="empty" data-testid="no-results"><b>{{ noResults(query) }}</b><span>{{ NO_RESULTS_HINT }}</span></div>
            <p v-else class="res-h" data-testid="results-count"><b>{{ resultsCount(searching.length) }}</b> {{ RESULTS_WHERE }}</p>
          </template>
          <template v-else-if="chip !== null">
            <p v-if="oneLetter" class="hint">{{ ONE_MORE_LETTER }}</p>
            <section v-if="entrance && repeats.length > 0" class="top" aria-label="En esta mesa" data-testid="at-this-table">
              <div class="sec-h"><h3>En esta mesa</h3><span>Un toque y se repite: +1 para toda la mesa</span></div>
              <div class="top-grid">
                <button v-for="repeat in repeats" :key="`${repeat.productId}/${repeat.presentationId}`" type="button" class="top-i" :disabled="round.sending"
                  :data-testid="`repeat-${repeat.productId}-${repeat.presentationId}`" :aria-label="`Uno más de ${repeat.name}${repeat.detail ? `, ${repeat.detail}` : ''}`"
                  @click="add(repeat.product, repeat.button)">
                  <DrinkPhoto :product-id="repeat.productId" :image-sha="repeat.imageSha" :name="repeat.name" size="sm" />
                  <span class="top-t"><b>{{ repeat.name }}</b><small>{{ repeat.detail }}</small></span>
                  <span class="top-plus" aria-hidden="true">+1</span>
                </button>
              </div>
            </section>
            <div class="sec-h"><h3>{{ chip.label }}</h3><span>{{ chipHeader(chip, listed.length) }}</span></div>
            <div v-if="subchips.length > 0" class="subchips" role="group" aria-label="Subfamilias">
              <button type="button" class="subchip" :aria-pressed="!subchips.some(sub => sub.name === subfamily)" data-testid="subchip-all" @click="subfamily = null">Todas</button>
              <button v-for="(sub, index) in subchips" :key="sub.name" type="button" class="subchip" :aria-pressed="sub.name === subfamily"
                :data-testid="`subchip-${index}`" @click="subfamily = sub.name">{{ sub.name }}</button>
            </div>
          </template>

          <ul class="plist">
            <li v-for="product in rows" :key="product.id" class="prow" :class="{ many: product.buttons.length > 2 }" :data-testid="`drink-${product.id}`">
              <DrinkPhoto :product-id="product.id" :image-sha="product.imageSha" :name="product.name" />
              <span class="pinfo">
                <span class="pname">{{ product.name }}</span>
                <span v-if="detail(product)" class="pmeta">{{ detail(product) }}</span>
                <span v-if="round.unitsOf(product.id) > 0" class="in-tray" :data-testid="`in-round-${product.id}`"><Icon name="check" :size="14" :stroke="2.8" />{{ round.unitsOf(product.id) }} en la ronda</span>
              </span>
              <span class="pbtns">
                <button v-for="button in product.buttons" :key="button.presentationId" type="button" class="fmt" :disabled="round.sending"
                  :data-testid="`add-${product.id}-${button.presentationId}`" :aria-label="addLabel(product.name, button.presentationId, button.presentation, product.buttons.length === 1)"
                  @click="add(product, button)">
                  <span>{{ formatParts(button.label)[0] }}</span><small v-if="formatParts(button.label)[1]">{{ formatParts(button.label)[1] }}</small></button>
              </span>
            </li>
          </ul>
          <button v-if="left > 0" type="button" class="btn wide more-rows" data-testid="more-rows" @click="visible += PAGE">Ver {{ Math.min(left, PAGE) }} más (quedan {{ left }})</button>
        </div>
      </div>
    </template>
  </section>
</template>
