<script setup lang="ts">
import { computed, ref } from 'vue'
import type { OfferChoice } from '../configuration'
import { serviceCommand } from '../ficha'
import { guestLabel } from '../labels'
import type { BoardEntry, Course } from '../types'
import Icon from './Icon.vue'

// C3a: «Carta y menús» de la ficha. Es lo de HOY con el aspecto nuevo (C3c lo rehace en tres toques y agrupado): elegir plato por
// comensal en los pases donde el servidor anuncia 'choose' (menu con eleccion, E4a) y pedir platos de la carta donde anuncia 'add-dish'
// (E4b). Los platos salen de la oferta con que se abrio la mesa (/configuration): la vigente o, si ya no lo es, la ultima leida en esta
// sesion (configuration.offerFor). Sin dinero; lo unico local es el formato.
const props = defineProps<{ entry: BoardEntry; offer: OfferChoice | null; tableName: string; locked: boolean }>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string] }>()

const service = computed(() => props.entry.service)
const problem = ref('')
const guests = computed(() => Array.from({ length: service.value.pax }, (_, index) => index + 1))
const announced = (course: Course, action: string) => course.actions?.includes(action) === true
const choosable = computed(() => service.value.courses.filter(course => announced(course, 'choose')))
const orderable = computed(() => service.value.courses.filter(course => announced(course, 'add-dish')))
const offerCourse = (course: Course) => props.offer?.courses.find(candidate => candidate.id === course.id)
const dishesOf = (course: Course) => offerCourse(course)?.dishes ?? []
const itemsOf = (course: Course) => offerCourse(course)?.items ?? []

function command(action: string, fields: Record<string, unknown>, description: string): void {
  problem.value = ''
  emit('run', serviceCommand(service.value, action), { expectedVersion: props.entry.version, ...fields }, `${description} · ${props.tableName}`)
}

// E4a: lo elegido por cada comensal se ve al lado de su nombre (la elaboracion de ese pase y comensal), o «sin elegir».
const choice = ref<Record<string, string>>({})
const chosen = (course: Course, guest: number) => course.preparations.find(preparation => preparation.guestPosition === guest)?.name ?? 'sin elegir'
function choose(course: Course, guest: number): void {
  const dish = dishesOf(course).find(candidate => candidate.id === choice.value[`${course.id}/${guest}`])
  if (!dish) { problem.value = 'Elige un plato para ese comensal.'; return }
  command('choose', { courseId: course.id, guestPosition: guest, dishId: dish.id }, `Elegir ${dish.name} para el comensal ${guest}`)
}

// E4b: un plato de la carta, para un comensal o para toda la mesa, con su cantidad (1 a 20).
const dishPick = ref<Record<string, string>>({})
const dishQty = ref<Record<string, number>>({})
const dishGuest = ref<Record<string, number | null>>({})
const quantityOf = (course: Course) => dishQty.value[course.id] ?? 1
const step = (course: Course, delta: number) => { dishQty.value[course.id] = Math.min(20, Math.max(1, quantityOf(course) + delta)) }
function addDish(course: Course): void {
  const item = itemsOf(course).find(candidate => `${candidate.productId}/${candidate.presentationId}` === dishPick.value[course.id])
  const quantity = quantityOf(course)
  if (!item || !Number.isInteger(quantity) || quantity < 1 || quantity > 20) { problem.value = 'Elige un plato y una cantidad entre 1 y 20.'; return }
  command('add-dish', { courseId: course.id, productId: item.productId, presentationId: item.presentationId, quantity, guestPosition: dishGuest.value[course.id] ?? null },
    `Pedir ${quantity} × ${item.name} en ${course.name}`)
}
</script>

<template>
  <section class="sheet-panel" data-testid="panel-carta">
    <p v-if="problem" class="notice" role="alert" data-testid="action-problem"><Icon name="info" />{{ problem }}</p>
    <p v-if="choosable.length === 0 && orderable.length === 0" class="notice neutral" data-testid="carta-empty">
      <Icon name="info" />Ahora no hay platos que elegir ni pedir en esta mesa. Los pases y su estado están en la ficha.</p>

    <section v-for="course in choosable" :key="'choose-' + course.id" class="card" :data-testid="`choose-${course.id}`">
      <div class="card-h"><h3>{{ course.name }}</h3><span class="muted sm">Cada comensal elige su plato</span></div>
      <div v-for="guest in guests" :key="guest" class="guest-row">
        <label :for="`choice-${course.id}-${guest}`">{{ guestLabel(guest) }} · {{ chosen(course, guest) }}</label>
        <div class="row">
          <select :id="`choice-${course.id}-${guest}`" :value="choice[`${course.id}/${guest}`] ?? ''" :name="`choice-${course.id}-${guest}`" :disabled="locked"
            @change="choice[`${course.id}/${guest}`] = ($event.target as HTMLSelectElement).value">
            <option value="" disabled>Elige…</option>
            <option v-for="dish in dishesOf(course)" :key="dish.id" :value="dish.id">{{ dish.name }}</option>
          </select>
          <button type="button" class="btn btn-primary" :disabled="locked" :data-testid="`do-choose-${course.id}-${guest}`" @click="choose(course, guest)">Elegir</button>
        </div>
      </div>
    </section>

    <section v-for="course in orderable" :key="'order-' + course.id" class="card stack" :data-testid="`order-${course.id}`">
      <div class="card-h"><h3>{{ course.name }}</h3><span class="muted sm">Pedir de la carta</span></div>
      <div class="field">
        <label :for="`dish-${course.id}`">Plato</label>
        <select :id="`dish-${course.id}`" :value="dishPick[course.id] ?? ''" :name="`dish-${course.id}`" :disabled="locked" @change="dishPick[course.id] = ($event.target as HTMLSelectElement).value">
          <option value="" disabled>Elige…</option>
          <option v-for="item in itemsOf(course)" :key="`${item.productId}/${item.presentationId}`" :value="`${item.productId}/${item.presentationId}`">{{ item.name }} · {{ item.presentation }}</option>
        </select>
      </div>
      <div class="field">
        <span class="lbl">Para quién</span>
        <div class="seg" role="radiogroup" aria-label="Para quién">
          <button type="button" role="radio" :aria-checked="(dishGuest[course.id] ?? null) === null" @click="dishGuest[course.id] = null">Toda la mesa</button>
          <button v-for="position in guests" :key="position" type="button" role="radio" :aria-checked="dishGuest[course.id] === position" :aria-label="guestLabel(position)"
            @click="dishGuest[course.id] = position">{{ position }}</button>
        </div>
      </div>
      <div class="field">
        <label :for="`dish-qty-${course.id}`">Cantidad</label>
        <div class="stepper">
          <button type="button" class="btn" aria-label="Uno menos" :disabled="locked || quantityOf(course) <= 1" @click="step(course, -1)"><Icon name="minus" :stroke="2.4" /></button>
          <input :id="`dish-qty-${course.id}`" v-model.number="dishQty[course.id]" :name="`dish-qty-${course.id}`" type="number" min="1" max="20" inputmode="numeric" placeholder="1" :disabled="locked" />
          <button type="button" class="btn" aria-label="Uno más" :disabled="locked || quantityOf(course) >= 20" @click="step(course, 1)"><Icon name="plus" :stroke="2.4" /></button>
        </div>
      </div>
      <button type="button" class="btn btn-primary btn-big" :disabled="locked" :data-testid="`do-add-dish-${course.id}`" @click="addDish(course)"><Icon name="send" />Pedir plato</button>
    </section>
  </section>
</template>
