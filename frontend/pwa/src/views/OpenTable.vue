<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { OfferChoice } from '../configuration'
import { offerKindLabel, plural } from '../labels'
import { proposedOffer } from '../offers'
import Icon from './Icon.vue'

// C3a: hoja «Abrir mesa» desde una mesa libre del plano. Conserva los campos de hoy (mesa, personas, menu o carta) con el aspecto
// nuevo; C3c la deja en tres toques y con las alergias. Lo normal sin preguntar (plan, seccion 2): las personas empiezan en 2 y se
// propone SIEMPRE el menu del dia, la primera oferta vigente que da /configuration (E4d); las demas, en «Otro menú…». Abrir es una
// accion de sesion que anuncia el servidor: esta hoja solo se ofrece si la anuncia. Lo unico local es el formato (1 a 40 personas).
const props = defineProps<{ tableName: string; roomName: string; capacity: number; offers: OfferChoice[]; occupied: boolean; locked: boolean }>()
const emit = defineEmits<{ open: [pax: number, offerId: string]; back: [] }>()

const pax = ref(2)
const quick = computed(() => Array.from({ length: Math.min(6, props.capacity > 0 ? props.capacity : 6) }, (_, index) => index + 1))
const proposed = computed(() => proposedOffer(props.offers))
const offerId = ref(proposed.value?.id ?? '')
const touched = ref(false)
const others = ref(false)
// La configuracion se relee al abrir esta hoja: si llega otro menu del dia y nadie ha elegido aun, se propone el nuevo; si lo elegido
// deja de estar vigente, se vuelve a proponer el del dia.
watch(() => props.offers.map(offer => offer.id).join('|'), () => {
  if (!touched.value || !props.offers.some(offer => offer.id === offerId.value)) { offerId.value = proposed.value?.id ?? ''; touched.value = false }
})
const shown = computed(() => others.value || (touched.value && offerId.value !== proposed.value?.id) ? props.offers : props.offers.slice(0, 1))
const chosen = computed(() => props.offers.find(offer => offer.id === offerId.value) ?? null)
const describe = (offer: OfferChoice) => [offerKindLabel(offer.kind) === offer.name ? '' : offerKindLabel(offer.kind),
  offer.kind !== 'a-la-carte' && offer.courses.length > 0 ? plural(offer.courses.length, 'pase', 'pases') : 'Platos sueltos de la carta'].filter(part => part).join(' · ')
const problem = ref('')
const step = (delta: number) => { pax.value = Math.min(40, Math.max(1, (Number.isInteger(pax.value) ? pax.value : 2) + delta)) }

function pick(id: string): void { offerId.value = id; touched.value = true }
function submit(): void {
  problem.value = ''
  if (!Number.isInteger(pax.value) || pax.value < 1 || pax.value > 40) { problem.value = 'Elige cuántas personas (1 a 40).'; return }
  if (!chosen.value) { problem.value = 'Elige el menú o la carta.'; return }
  emit('open', pax.value, chosen.value.id)
}
</script>

<template>
  <article class="ficha stack" data-testid="open-form">
    <header class="appbar">
      <button type="button" class="back" data-testid="back" @click="emit('back')"><Icon name="back" :size="24" :stroke="2.2" /><span class="back-t">Mesas</span></button>
      <div class="appbar-t">
        <div class="appbar-title"><h2>Abrir {{ tableName }}</h2><span class="pill t-free">Libre</span></div>
        <p class="appbar-sub">{{ [roomName, capacity > 0 ? `hasta ${plural(capacity, 'persona', 'personas')}` : ''].filter(part => part).join(' · ') }}</p>
      </div>
    </header>
    <p v-if="occupied" class="notice" role="alert" data-testid="open-occupied"><Icon name="info" />Según la última lectura esta mesa está ocupada: el servidor no la abrirá otra vez.</p>

    <h3 class="q-h">¿Cuántas personas?</h3>
    <div class="pax" role="radiogroup" aria-label="Personas">
      <button v-for="count in quick" :key="count" type="button" role="radio" :aria-checked="pax === count" :data-testid="`pax-${count}`" @click="pax = count">{{ count }}</button>
    </div>
    <div class="stepper">
      <button type="button" class="btn" aria-label="Una persona menos" :disabled="pax <= 1" @click="step(-1)"><Icon name="minus" :stroke="2.4" /></button>
      <input v-model.number="pax" name="pax" type="number" min="1" max="40" inputmode="numeric" aria-label="Personas" :disabled="locked" />
      <button type="button" class="btn" aria-label="Una persona más" :disabled="pax >= 40" @click="step(1)"><Icon name="plus" :stroke="2.4" /></button>
    </div>

    <h3 class="q-h">¿Qué van a comer?</h3>
    <p v-if="offers.length === 0" class="notice neutral" data-testid="no-offers"><Icon name="info" />No hay menús ni cartas vigentes ahora. Se crean o se activan en el PC principal, en Menús y cartas.</p>
    <div v-else class="offers" role="radiogroup" aria-label="Menú o carta">
      <button v-for="offer in shown" :key="offer.id" type="button" class="ocard" role="radio" :aria-checked="offerId === offer.id" :data-testid="`offer-${offer.id}`" @click="pick(offer.id)">
        <span class="radio" aria-hidden="true"></span>
        <span class="oc-t"><b>{{ offer.name }}<span v-if="offer.id === proposed?.id" class="tag">Menú del día</span></b><small>{{ describe(offer) }}</small></span>
      </button>
      <button v-if="offers.length > shown.length" type="button" class="btn wide" data-testid="other-offers" @click="others = true">Otro menú…</button>
    </div>

    <div class="bar">
      <p v-if="problem" class="notice" role="alert" data-testid="action-problem"><Icon name="info" />{{ problem }}</p>
      <p class="bar-note"><b>{{ Number.isInteger(pax) ? plural(pax, 'persona', 'personas') : '' }}</b><template v-if="chosen"> · {{ chosen.name }}</template></p>
      <button type="button" class="btn btn-primary btn-big" :disabled="locked || offers.length === 0" data-testid="do-open" @click="submit"><Icon name="users" :size="22" />Abrir {{ tableName }}</button>
    </div>
  </article>
</template>
