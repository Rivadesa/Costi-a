<script setup lang="ts">
import { computed, ref } from 'vue'
import { catalogKey, type CatalogItem } from '../api'
import { restrictionLabel } from '../labels'
import type { BoardEntry, Course } from '../types'

// Acciones del comandero sobre UNA mesa. Ninguna regla de negocio aqui: un boton existe si y solo si el
// servidor anuncia esa accion para este dispositivo (affordances, ya filtradas por rol y estacion). Lo
// unico local es validar el formato de lo tecleado (motivo no vacio, cantidad entera positiva).
import type { OfferChoice } from './BoardView.vue'
const props = defineProps<{ entry: BoardEntry; catalog: CatalogItem[]; offers?: OfferChoice[]; canConsume: boolean; disabled: boolean }>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string] }>()

const reason = ref('')
const productId = ref('')
const quantity = ref(1)
const guest = ref<number | null>(null)
const kind = ref<'Allergy' | 'Intolerance' | 'Preference'>('Allergy')
const severity = ref<'Severe' | 'Moderate' | 'Mild'>('Severe')
const substance = ref('')
const problem = ref('')

const service = computed(() => props.entry.service)
const has = (action: string) => service.value.actions?.includes(action) === true
const SERVICE_LABEL: Record<string, string> = { start: 'Iniciar servicio', 'fire-next': 'Enviar siguiente pase a cocina', resume: 'Reanudar servicio' }
const guests = computed(() => Array.from({ length: service.value.pax }, (_, index) => index + 1))

function command(action: string, fields: Record<string, unknown>, description: string): void {
  problem.value = ''
  emit('run', `/dining/services/${encodeURIComponent(service.value.id)}/commands/${action}`, { expectedVersion: props.entry.version, ...fields }, `${description} · ${service.value.tableId}`)
}
function withReason(action: string, fields: Record<string, unknown>, description: string): void {
  const text = reason.value.trim()
  if (text.length < 3) { problem.value = 'Escribe el motivo (3 caracteres o mas).'; return }
  command(action, { ...fields, reason: text }, description); reason.value = ''
}
function declare(): void {
  const text = substance.value.trim()
  if (text.length < 2) { problem.value = 'Indica la sustancia o preferencia.'; return }
  command('declare-restriction', { guestPosition: guest.value, kind: kind.value, substance: text, severity: severity.value }, `Declarar ${text}`)
  substance.value = ''
}
function consume(): void {
  const item = props.catalog.find(candidate => catalogKey(candidate) === productId.value)
  if (!item || !Number.isInteger(quantity.value) || quantity.value < 1 || quantity.value > 99) { problem.value = 'Elige un producto y una cantidad entre 1 y 99.'; return }
  // E3: la presentacion vendida viaja con el producto; el servidor fija el precio con la tarifa de la sala (nunca aqui).
  command('add-consumption', { productId: item.id, presentationId: item.presentationId, quantity: quantity.value }, `Anadir ${quantity.value} × ${item.name} (${item.presentation})`)
}
const courseActions = (course: Course) => course.actions ?? []
// E4a: pases de menu cerrado en los que el servidor anuncia 'choose': platos elegibles de la oferta con la que se abrio la mesa.
const choice = ref<Record<string, string>>({})
const choosable = computed(() => service.value.courses.filter(c => courseActions(c).includes('choose')))
// E4b: grupos de una carta en los que el servidor anuncia 'add-dish': items pedibles de la oferta de la mesa.
const orderable = computed(() => service.value.courses.filter(c => courseActions(c).includes('add-dish')))
const itemsOf = (course: Course) => (props.offers ?? []).find(o => o.id === service.value.offerId)?.courses.find(c => c.id === course.id)?.items ?? []
const dishPick = ref<Record<string, string>>({}); const dishQty = ref<Record<string, number>>({}); const dishGuest = ref<Record<string, number | null>>({})
function addDish(course: Course): void {
  const item = itemsOf(course).find(i => `${i.productId}/${i.presentationId}` === dishPick.value[course.id])
  const quantity = dishQty.value[course.id] ?? 1
  if (!item || !Number.isInteger(quantity) || quantity < 1 || quantity > 20) { problem.value = 'Elige un plato y una cantidad entre 1 y 20.'; return }
  command('add-dish', { courseId: course.id, productId: item.productId, presentationId: item.presentationId, quantity, guestPosition: dishGuest.value[course.id] ?? null }, `Pedir ${quantity} × ${item.name} en ${course.name}`)
}
const dishesOf = (course: Course) => (props.offers ?? []).find(o => o.id === service.value.offerId)?.courses.find(c => c.id === course.id)?.dishes ?? []
const chosen = (course: Course, guest: number) => course.preparations.find(p => p.guestPosition === guest)?.name ?? 'sin elegir'
function choose(course: Course, guest: number): void {
  const dishId = choice.value[`${course.id}/${guest}`]
  const dish = dishesOf(course).find(d => d.id === dishId)
  if (!dish) { problem.value = 'Elige un plato para ese comensal.'; return }
  command('choose', { courseId: course.id, guestPosition: guest, dishId: dish.id }, `Elegir ${dish.name} para el comensal ${guest}`)
}
</script>

<template>
  <section class="actions" data-testid="actions">
    <p v-if="problem" class="notice" role="alert" data-testid="action-problem">{{ problem }}</p>
    <div class="row">
      <button v-for="action in ['start', 'fire-next', 'resume'].filter(has)" :key="action" type="button" :disabled="disabled" :data-testid="`do-${action}`"
        @click="command(action, {}, SERVICE_LABEL[action])">{{ SERVICE_LABEL[action] }}</button>
    </div>
    <div v-for="course in service.courses.filter(c => courseActions(c).includes('serve') || courseActions(c).includes('skip'))" :key="course.id" class="row">
      <button v-if="courseActions(course).includes('serve')" type="button" :disabled="disabled" :data-testid="`do-serve-${course.id}`"
        @click="command('serve', { courseId: course.id }, `Servir ${course.name}`)">Servir {{ course.name }}</button>
      <button v-if="courseActions(course).includes('skip')" type="button" class="secondary" :disabled="disabled" :data-testid="`do-skip-${course.id}`"
        @click="withReason('skip', { courseId: course.id }, `Omitir ${course.name}`)">Omitir {{ course.name }} (con motivo)</button>
    </div>

    <details v-for="course in choosable" :key="'choose-' + course.id" open :data-testid="`choose-${course.id}`">
      <summary>{{ course.name }}: elegir plato por comensal</summary>
      <div v-for="guest in guests" :key="guest" class="row">
        <label>Comensal {{ guest }} ({{ chosen(course, guest) }})
          <select v-model="choice[`${course.id}/${guest}`]" :name="`choice-${course.id}-${guest}`" :disabled="disabled">
            <option value="" disabled>Elige…</option>
            <option v-for="dish in dishesOf(course)" :key="dish.id" :value="dish.id">{{ dish.name }}</option>
          </select>
        </label>
        <button type="button" :disabled="disabled" :data-testid="`do-choose-${course.id}-${guest}`" @click="choose(course, guest)">Elegir</button>
      </div>
    </details>

    <details v-for="course in orderable" :key="'order-' + course.id" open :data-testid="`order-${course.id}`">
      <summary>{{ course.name }}: pedir platos de la carta</summary>
      <label>Plato
        <select v-model="dishPick[course.id]" :name="`dish-${course.id}`" :disabled="disabled">
          <option value="" disabled>Elige…</option>
          <option v-for="item in itemsOf(course)" :key="`${item.productId}/${item.presentationId}`" :value="`${item.productId}/${item.presentationId}`">{{ item.name }} · {{ item.presentation }}</option>
        </select>
      </label>
      <label>Comensal
        <select v-model="dishGuest[course.id]" :name="`dish-guest-${course.id}`" :disabled="disabled"><option :value="null">Toda la mesa</option><option v-for="position in guests" :key="position" :value="position">Comensal {{ position }}</option></select>
      </label>
      <label>Cantidad <input v-model.number="dishQty[course.id]" :name="`dish-qty-${course.id}`" type="number" min="1" max="20" inputmode="numeric" placeholder="1" :disabled="disabled" /></label>
      <button type="button" :disabled="disabled" :data-testid="`do-add-dish-${course.id}`" @click="addDish(course)">Pedir plato</button>
    </details>

    <details v-if="canConsume && catalog.length > 0">
      <summary>Anadir consumo a mayores</summary>
      <label>Producto
        <select v-model="productId" name="product" :disabled="disabled">
          <option value="" disabled>Elige…</option>
          <option v-for="item in catalog" :key="catalogKey(item)" :value="catalogKey(item)">{{ item.categoryName ? item.categoryName + ' · ' : '' }}{{ item.name }} · {{ item.presentation }}</option>
        </select>
      </label>
      <label>Cantidad <input v-model.number="quantity" name="quantity" type="number" min="1" max="99" inputmode="numeric" :disabled="disabled" /></label>
      <button type="button" :disabled="disabled" data-testid="do-add-consumption" @click="consume">Anadir consumo</button>
    </details>

    <details v-if="has('declare-restriction')">
      <summary>Restricciones del comensal</summary>
      <label>A quien afecta
        <select v-model="guest" name="guest" :disabled="disabled">
          <option :value="null">Toda la mesa</option>
          <option v-for="position in guests" :key="position" :value="position">Comensal {{ position }}</option>
        </select>
      </label>
      <label>Tipo
        <select v-model="kind" name="kind" :disabled="disabled"><option value="Allergy">ALERGIA</option><option value="Intolerance">Intolerancia</option><option value="Preference">Preferencia</option></select>
      </label>
      <label>Severidad
        <select v-model="severity" name="severity" :disabled="disabled"><option value="Severe">grave</option><option value="Moderate">moderada</option><option value="Mild">leve</option></select>
      </label>
      <label>Sustancia <input v-model="substance" name="substance" maxlength="80" autocomplete="off" :disabled="disabled" /></label>
      <button type="button" :disabled="disabled" data-testid="do-declare-restriction" @click="declare">Declarar restriccion</button>
      <ul v-if="has('remove-restriction')">
        <li v-for="restriction in service.restrictions ?? []" :key="restriction.id">
          {{ restrictionLabel(restriction) }}
          <button type="button" class="secondary" :disabled="disabled" @click="withReason('remove-restriction', { restrictionId: restriction.id }, `Retirar ${restriction.substance}`)">Retirar (con motivo)</button>
        </li>
      </ul>
    </details>

    <details v-if="has('pause') || service.courses.some(c => courseActions(c).includes('skip')) || has('remove-restriction')">
      <summary>Motivo (para pausar, omitir o retirar)</summary>
      <label>Motivo <input v-model="reason" name="reason" maxlength="200" autocomplete="off" :disabled="disabled" /></label>
      <button v-if="has('pause')" type="button" class="secondary" :disabled="disabled" data-testid="do-pause" @click="withReason('pause', {}, 'Pausar servicio')">Pausar servicio</button>
    </details>
  </section>
</template>
