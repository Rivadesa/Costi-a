<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { doneDetail, guestChoices, restrictionsNote } from '../bodega'
import { MAX_QUANTITY, roundLineText, type RoundLine } from '../round'
import type { GuestRestriction } from '../types'
import DrinkPhoto from './DrinkPhoto.vue'
import Icon from './Icon.vue'

// C3b: hoja de UNA linea de la ronda («2b · La ronda» de la maqueta): solo sale desde «Para: …» de la bandeja. Cantidad y para quien
// (toda la mesa o un comensal; los que tienen una restriccion llevan un punto rojo y la restriccion va ESCRITA debajo). Lo elegido se
// aplica al tocar «Hecho»; «Quitar» la saca de la ronda. Sigue siendo el borrador en memoria: nada sale hasta «Enviar».
const props = defineProps<{ line: RoundLine; pax: number; restrictions: GuestRestriction[]; imageSha: string | null }>()
const emit = defineEmits<{ done: [quantity: number, guestPosition: number | null]; remove: []; close: [] }>()

const quantity = ref(props.line.quantity)
const guest = ref<number | null>(props.line.guestPosition)
const choices = computed(() => guestChoices(props.pax, props.restrictions))
const note = computed(() => restrictionsNote(props.restrictions))
const first = ref<HTMLButtonElement | null>(null)
onMounted(() => first.value?.focus())
</script>

<template>
  <div class="scrim" aria-hidden="true" @click="emit('close')"></div>
  <section class="sheet" role="dialog" aria-modal="true" :aria-label="`${roundLineText(line)}: cantidad y para quién`" data-testid="line-sheet" @keydown.esc="emit('close')">
    <div class="grab" aria-hidden="true"></div>
    <div class="sheet-scroll">
      <div class="sh-head">
        <DrinkPhoto :product-id="line.productId" :image-sha="imageSha" :name="line.name" />
        <span class="sh-tx"><span class="sh-title">{{ roundLineText(line) }}</span><span class="sh-sub">En la ronda, sin enviar</span></span>
        <button ref="first" type="button" class="icon-btn" aria-label="Cerrar sin cambiar nada" data-testid="sheet-close" @click="emit('close')"><Icon name="x" :size="22" :stroke="2.2" /></button>
      </div>
      <h3 class="q-h">Cantidad</h3>
      <div class="qstep">
        <button type="button" aria-label="Uno menos" :disabled="quantity <= 1" data-testid="sheet-minus" @click="quantity--"><Icon name="minus" :size="24" :stroke="2.4" /></button>
        <output aria-live="polite" data-testid="sheet-qty">{{ quantity }}</output>
        <button type="button" aria-label="Uno más" :disabled="quantity >= MAX_QUANTITY" data-testid="sheet-plus" @click="quantity++"><Icon name="plus" :size="24" :stroke="2.4" /></button>
      </div>
      <h3 class="q-h">¿Para quién? <small>Toda la mesa o el número del comensal</small></h3>
      <div class="guests" role="radiogroup" aria-label="Para quién">
        <button v-for="choice in choices" :key="choice.position ?? 'all'" type="button" class="g" :class="{ 'g-all': choice.position === null }" role="radio"
          :aria-checked="guest === choice.position" :aria-label="choice.aria" :data-testid="choice.position === null ? 'sheet-guest-all' : `sheet-guest-${choice.position}`"
          @click="guest = choice.position">{{ choice.label }}<span v-if="choice.restricted" class="g-dot" aria-hidden="true"></span></button>
      </div>
      <p v-if="note" class="g-note" data-testid="sheet-restrictions"><Icon name="alert" :size="16" :stroke="2.2" /><span>{{ note }}</span></p>
    </div>
    <div class="sheet-foot">
      <button type="button" class="btn" data-testid="sheet-remove" @click="emit('remove')"><Icon name="trash" />Quitar</button>
      <button type="button" class="btn btn-primary sheet-done" data-testid="sheet-done" @click="emit('done', quantity, guest)">
        <span>Hecho</span><small>{{ doneDetail(quantity, guest) }}</small></button>
    </div>
  </section>
</template>
