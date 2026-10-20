<script setup lang="ts">
import { computed, ref } from 'vue'
import { catalogKey, type CatalogItem } from '../api'
import { serviceCommand } from '../ficha'
import { actionLabel } from '../labels'
import type { BoardEntry } from '../types'
import Icon from './Icon.vue'

// C3a: «Bodega y bebidas» de la ficha. Es el consumo de HOY (add-consumption) con el aspecto nuevo; C3b lo rehace con buscador, Copa
// y Botella, rondas y «Lo que lleva la mesa». El catalogo operativo no trae precios (GET /catalog, ADR-007) y el servidor fija el
// importe con la tarifa de la sala: aqui nunca se ve ni se manda uno. Lo unico local es el formato de la cantidad (1 a 99).
const props = defineProps<{ entry: BoardEntry; catalog: CatalogItem[]; canConsume: boolean; tableName: string; locked: boolean }>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string] }>()

const problem = ref('')
const productKey = ref('')
const quantity = ref(1)
// Agrupado por su categoria del catalogo, en el orden en que llega; lo que no tiene categoria va al final.
const groups = computed(() => {
  const map = new Map<string, CatalogItem[]>()
  for (const item of props.catalog) { const name = item.categoryName?.trim() || 'Otros'; map.set(name, [...(map.get(name) ?? []), item]) }
  const other = map.get('Otros'); map.delete('Otros')
  return [...map.entries(), ...(other ? [['Otros', other] as [string, CatalogItem[]]] : [])]
})
const step = (delta: number) => { quantity.value = Math.min(99, Math.max(1, (Number.isInteger(quantity.value) ? quantity.value : 1) + delta)) }

function consume(): void {
  problem.value = ''
  const item = props.catalog.find(candidate => catalogKey(candidate) === productKey.value)
  if (!item || !Number.isInteger(quantity.value) || quantity.value < 1 || quantity.value > 99) { problem.value = 'Elige un producto y una cantidad entre 1 y 99.'; return }
  // E3: la presentacion vendida viaja con el producto; el servidor fija el importe con la tarifa de la sala (nunca aqui).
  emit('run', serviceCommand(props.entry.service, 'add-consumption'), { expectedVersion: props.entry.version, productId: item.id, presentationId: item.presentationId, quantity: quantity.value },
    `Añadir ${quantity.value} × ${item.name} (${item.presentation}) · ${props.tableName}`)
}
</script>

<template>
  <section class="sheet-panel" data-testid="panel-bodega">
    <p v-if="!canConsume" class="notice neutral" data-testid="bodega-empty"><Icon name="info" />Este dispositivo no puede añadir bebidas ni extras a la mesa.</p>
    <p v-else-if="catalog.length === 0" class="notice neutral" data-testid="bodega-empty"><Icon name="info" />No hay bebidas ni extras disponibles en el catálogo.</p>
    <section v-else class="card stack">
      <p v-if="problem" class="notice" role="alert" data-testid="action-problem"><Icon name="info" />{{ problem }}</p>
      <div class="field">
        <label for="product">Bebida o extra</label>
        <select id="product" v-model="productKey" name="product" :disabled="locked">
          <option value="" disabled>Elige…</option>
          <optgroup v-for="[name, items] in groups" :key="name" :label="name">
            <option v-for="item in items" :key="catalogKey(item)" :value="catalogKey(item)">{{ item.name }} · {{ item.presentation }}</option>
          </optgroup>
        </select>
      </div>
      <div class="field">
        <label for="quantity">Cantidad</label>
        <div class="stepper">
          <button type="button" class="btn" aria-label="Uno menos" :disabled="locked || quantity <= 1" @click="step(-1)"><Icon name="minus" :stroke="2.4" /></button>
          <input id="quantity" v-model.number="quantity" name="quantity" type="number" min="1" max="99" inputmode="numeric" :disabled="locked" />
          <button type="button" class="btn" aria-label="Uno más" :disabled="locked || quantity >= 99" @click="step(1)"><Icon name="plus" :stroke="2.4" /></button>
        </div>
      </div>
      <button type="button" class="btn btn-primary btn-big" :disabled="locked" data-testid="do-add-consumption" @click="consume"><Icon name="plus" :stroke="2.4" />{{ actionLabel('add-consumption') }}</button>
      <p class="hint">Los importes solo se ven en Caja.</p>
    </section>
  </section>
</template>
