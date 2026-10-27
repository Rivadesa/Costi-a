<script setup lang="ts">
import { computed, ref } from 'vue'
import { guestChoices } from '../bodega'
import type { OfferChoice, OfferItem } from '../configuration'
import type { CommandRequest } from '../ficha'
import { courseDetail, restrictionAlarming } from '../floor'
import { guestLabel, restrictionLabel } from '../labels'
import { addDishRequest, chooseRequest, MAX_DISH_QUANTITY } from '../pases'
import type { BoardEntry, Course, GuestRestriction } from '../types'
import Icon from './Icon.vue'

// C3a: «Carta y menús» de la ficha. C3e (plan 2026-10-27, paso 3; plan 2026-10-15, 678-679): SIN desplegables, un toque por plato.
//  - menu con eleccion (el servidor anuncia 'choose', E4a): una fila por comensal con los platos del pase como botones; lo elegido sale
//    marcado y escrito al lado del comensal. Cada toque es UNA orden choose (pases.chooseRequest). El ya elegido no se vuelve a mandar:
//    el servidor sustituiria la elaboracion y, con una restriccion, borraria lo que cocina ya decidio de ese plato (E4e);
//  - carta (anuncia 'add-dish', E4b): «Para quién» y «Cantidad» arriba, comunes a todos los grupos, y los platos como botones. Cada toque
//    es UNA orden add-dish (pases.addDishRequest) y la cantidad vuelve a 1; para quien se queda, para pedir seguido al mismo comensal;
//  - si el servidor anuncia elegir o pedir y este movil no tiene los platos de la oferta de la mesa (solo conoce las que leyo en esta
//    sesion, configuration.offerFor), se dice y se manda al PC: nunca un pase vacio sin explicacion (plan 2026-10-27, Diferido).
// Hasta E4g, una orden por toque, con la version LEIDA (F01), por la orden incierta de siempre. Nada habilita nada: solo hay botones en
// los pases que anuncian su accion. Las props y el evento son los de C3a (TableCard no cambia). Sin dinero.
const props = defineProps<{ entry: BoardEntry; offer: OfferChoice | null; tableName: string; locked: boolean }>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string] }>()

const service = computed(() => props.entry.service)
const restrictions = computed<GuestRestriction[]>(() => service.value.restrictions ?? [])
const guests = computed(() => Array.from({ length: Math.max(0, service.value.pax) }, (_, index) => index + 1))
const announced = (course: Course, action: string) => course.actions?.includes(action) === true
const offerCourse = (course: Course) => props.offer?.courses.find(candidate => candidate.id === course.id)
const dishesOf = (course: Course) => offerCourse(course)?.dishes ?? []
const itemsOf = (course: Course) => offerCourse(course)?.items ?? []
const choosable = computed(() => service.value.courses.filter(course => announced(course, 'choose')))
const orderable = computed(() => service.value.courses.filter(course => announced(course, 'add-dish')))
// Solo se pintan los pases cuyos platos conoce este movil; si falta alguno, el aviso de la oferta desconocida.
const chooseCourses = computed(() => choosable.value.filter(course => dishesOf(course).length > 0))
const orderCourses = computed(() => orderable.value.filter(course => itemsOf(course).length > 0))
const unknownOffer = computed(() => chooseCourses.value.length < choosable.value.length || orderCourses.value.length < orderable.value.length)

function run(request: CommandRequest | null): void {
  if (request) emit('run', request.path, request.body, request.description)
}

// ---- Menu con eleccion: una fila por comensal ----
// «Cada comensal elige su plato · Han elegido 2 de 4 personas» (courseDetail, el mismo recuento que la ficha).
const chooseSub = (course: Course) => ['Cada comensal elige su plato', courseDetail(course, service.value.pax)].filter(part => part.length > 0).join(' · ')
const ownRestrictions = (guest: number) => restrictions.value.filter(restriction => restriction.guestPosition === guest)
// Lo elegido es la elaboracion de ese comensal en ese pase (el servidor la sustituye al elegir otra); se escribe con su nombre y se marca
// el primer plato de la oferta con ese nombre. Una elaboracion que ya no esta en la oferta se escribe igual, sin marcar ningun boton.
const chosenName = (course: Course, guest: number) => course.preparations.find(preparation => preparation.guestPosition === guest)?.name ?? ''
function chosenIndex(course: Course, guest: number): number {
  const name = chosenName(course, guest)
  return name ? dishesOf(course).findIndex(dish => dish.name === name) : -1
}
function choose(course: Course, guest: number, index: number): void {
  const dish = dishesOf(course)[index]
  if (props.locked || !dish || index === chosenIndex(course, guest)) return
  run(chooseRequest(props.entry, course, guest, dish, props.tableName))
}

// ---- Carta: para quien y cuantos, arriba; los platos, botones ----
const guest = ref<number | null>(null)
const quantity = ref(1)
// Si la mesa baja de personas, un comensal que ya no esta vuelve a «Toda la mesa» (el servidor lo rechazaria: invalid_guest).
const forGuest = computed(() => guest.value !== null && guest.value <= service.value.pax ? guest.value : null)
const guestOptions = computed(() => guestChoices(service.value.pax, restrictions.value))
// El punto del comensal, rojo solo si alguna restriccion suya alarma; si no, ambar. La restriccion va escrita arriba, en la franja.
const alarmingGuest = (position: number | null) => position !== null && ownRestrictions(position).some(restrictionAlarming)
const forWhom = computed(() => forGuest.value === null ? 'toda la mesa' : `el comensal ${forGuest.value}`)
const tapText = computed(() => `Un toque pide ${quantity.value} para ${forWhom.value}.`)
function addDish(course: Course, item: OfferItem): void {
  if (props.locked) return
  run(addDishRequest(props.entry, course, item, quantity.value, forGuest.value, props.tableName))
  quantity.value = 1
}
</script>

<template>
  <section class="sheet-panel" data-testid="panel-carta">
    <p v-if="choosable.length === 0 && orderable.length === 0" class="notice neutral" data-testid="carta-empty">
      <Icon name="info" />Ahora no hay platos que elegir ni pedir en esta mesa. Los pases y su estado están en la ficha.</p>
    <p v-if="unknownOffer" class="notice" role="status" data-testid="carta-unknown-offer">
      <Icon name="info" />Los platos de esta mesa no están en este móvil: su menú o carta ya no se ofrece ahora. Elige o pide en el PC principal.</p>

    <section v-for="course in chooseCourses" :key="'choose-' + course.id" class="card" :data-testid="`choose-${course.id}`">
      <div class="card-h"><h3>{{ course.name }}</h3><span class="muted sm">{{ chooseSub(course) }}</span></div>
      <div v-for="position in guests" :key="position" class="carta-guest" role="group" :aria-label="`${guestLabel(position)} · ${course.name}`"
        :data-testid="`choose-${course.id}-${position}`">
        <p class="carta-guest-h"><b>{{ guestLabel(position) }}</b><span aria-hidden="true"> · </span>
          <span v-if="chosenName(course, position)" class="carta-chosen" :data-testid="`chosen-${course.id}-${position}`">{{ chosenName(course, position) }}</span>
          <span v-else class="carta-missing" :data-testid="`chosen-${course.id}-${position}`">sin elegir</span></p>
        <p v-for="restriction in ownRestrictions(position)" :key="restriction.id" class="line-alert" :class="{ alarm: restrictionAlarming(restriction) }">
          <Icon name="alert" :size="15" :stroke="2.2" />{{ restrictionLabel(restriction) }}</p>
        <div class="carta-dishes">
          <button v-for="(dish, index) in dishesOf(course)" :key="dish.id" type="button" class="carta-dish" :aria-pressed="index === chosenIndex(course, position)"
            :disabled="locked || index === chosenIndex(course, position)" :data-testid="`do-choose-${course.id}-${position}-${index}`" @click="choose(course, position, index)">
            <Icon v-if="index === chosenIndex(course, position)" name="check" :stroke="2.6" /><span class="carta-dish-t"><b>{{ dish.name }}</b></span>
          </button>
        </div>
      </div>
    </section>

    <template v-if="orderCourses.length > 0">
      <section class="card stack" data-testid="carta-for">
        <div class="field"><span class="lbl">Para quién</span>
          <div class="guests" role="radiogroup" aria-label="Para quién">
            <button v-for="choice in guestOptions" :key="choice.position ?? 'all'" type="button" class="g" :class="{ 'g-all': choice.position === null }" role="radio"
              :aria-checked="forGuest === choice.position" :aria-label="choice.aria" :data-testid="choice.position === null ? 'carta-guest-all' : `carta-guest-${choice.position}`"
              @click="guest = choice.position">{{ choice.label }}<span v-if="choice.restricted" class="g-dot" :class="{ 'carta-dot-mild': !alarmingGuest(choice.position) }" aria-hidden="true"></span></button>
          </div>
        </div>
        <div class="field"><span class="lbl">Cantidad</span>
          <div class="qstep">
            <button type="button" aria-label="Uno menos" :disabled="quantity <= 1" data-testid="carta-minus" @click="quantity = Math.max(1, quantity - 1)"><Icon name="minus" :size="24" :stroke="2.4" /></button>
            <output aria-live="polite" data-testid="carta-qty">{{ quantity }}</output>
            <button type="button" aria-label="Uno más" :disabled="quantity >= MAX_DISH_QUANTITY" data-testid="carta-plus" @click="quantity = Math.min(MAX_DISH_QUANTITY, quantity + 1)"><Icon name="plus" :size="24" :stroke="2.4" /></button>
          </div>
        </div>
      </section>

      <section v-for="course in orderCourses" :key="'order-' + course.id" class="card" :data-testid="`order-${course.id}`">
        <div class="card-h"><h3>{{ course.name }}</h3><span class="muted sm">{{ courseDetail(course, service.pax) }}</span></div>
        <p class="carta-tap" data-testid="carta-tap">{{ tapText }}</p>
        <div class="carta-dishes">
          <button v-for="(item, index) in itemsOf(course)" :key="`${item.productId}/${item.presentationId}`" type="button" class="carta-dish"
            :aria-label="`Pedir ${item.name}${item.presentation ? ` · ${item.presentation}` : ''}: ${quantity} para ${forWhom}`" :disabled="locked"
            :data-testid="`do-add-dish-${course.id}-${index}`" @click="addDish(course, item)">
            <span class="carta-dish-t"><b>{{ item.name }}</b><small v-if="item.presentation">{{ item.presentation }}</small></span><Icon name="plus" :stroke="2.4" />
          </button>
        </div>
      </section>
    </template>
  </section>
</template>
