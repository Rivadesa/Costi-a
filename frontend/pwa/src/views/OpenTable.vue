<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import {
  draftProblem, guestChoices, initialPax, KINDS, MAX_DRAFTS, newDraft, offerGroups, openRequest, openSummary, paxChoices, paxLimit, paxProblem,
  SEVERITIES, type RestrictionDraft,
} from '../abrir'
import type { OfferChoice } from '../configuration'
import type { CommandRequest } from '../ficha'
import { restrictionAlarming } from '../floor'
import { offerKindLabel, plural } from '../labels'
import Icon from './Icon.vue'

// C3a: hoja «Abrir mesa» desde una mesa libre del plano. C3e (plan 2026-10-27, paso 3; modelo maq15:789-795 y abrir.png): rehecha con
// abrir.ts, donde esta TODA la logica y su prueba sin DOM; aqui solo se pinta. Tres toques: la mesa, las personas y «Abrir Mesa 5»; los
// de una alergia van aparte (anotar, quien y a que). Personas de 1 a 8 sin pasar de la capacidad y «Más…» con el paso a paso (plan22:738);
// el menu del dia ya elegido, los demas tras «Otro menú…» y los menus con eleccion solo en «Más opciones» (plan15:671); y las alergias,
// filas EN MEMORIA que viajan en la MISMA orden open (E4e; D6.3, una orden incierta por gesto). La hoja solo existe si el servidor
// anuncia open (D3.1) y solo mira el FORMATO: capacidad, oferta vigente, comensal y duplicadas las decide el servidor (422, 409).
// Emite la orden ya hecha (openRequest, con el codigo de la mesa en tableId y su nombre en la descripcion); BoardView la envia con la
// orden incierta de siempre y, si el servidor la rechaza, deja esta hoja montada con sus filas para no escribirlas otra vez (decision 7).
// Las filas se pueden llenar con una orden sin confirmar (como la ronda): solo «Abrir» espera (locked, D6.7). Sin dinero (ADR-007).
// rejection: el motivo del ultimo rechazo de la apertura (o de que no saliera), que BoardView deja aqui para que se lea junto a «Abrir».
const props = defineProps<{
  tableId: string; tableName: string; roomName: string; capacity: number; offers: OfferChoice[]; occupied: boolean; locked: boolean
  rejection: { code: string; text: string } | null
}>()
const emit = defineEmits<{ open: [request: CommandRequest]; back: [] }>()

// ---- ¿Cuantas personas? ----
// v-model.number deja '' con el campo vacio: count lo pasa a NaN, que abrir.ts ya trata como «sin elegir». Empieza en initialPax (2, o 1
// en una mesa de 1); si la capacidad llega despues (la configuracion se relee al abrir la hoja) y nadie ha tocado, se vuelve a calcular.
const pax = ref<number | string>(initialPax(props.capacity))
const paxTouched = ref(false)
const count = computed(() => typeof pax.value === 'number' ? pax.value : Number.NaN)
const choices = computed(() => paxChoices(props.capacity))
const limit = computed(() => paxLimit(props.capacity))
watch(() => props.capacity, capacity => { if (!paxTouched.value) pax.value = initialPax(capacity) })
// El paso a paso y el numero, tras «Más…»; tambien si las personas elegidas ya no son uno de los botones (mas de 8): el numero elegido
// nunca se esconde.
const moreOpen = ref(false)
const stepperShown = computed(() => choices.value.more && (moreOpen.value || !choices.value.counts.includes(count.value)))
// Hasta 6 botones, en una fila (como la maqueta); con 7 u 8, una rejilla de 4 en el movil (48 px o mas cada uno) y de 8 en la tableta.
const twoRows = computed(() => choices.value.counts.length > 6)
function choosePax(value: number): void { pax.value = value; paxTouched.value = true }
const step = (delta: number) => choosePax(Math.min(limit.value, Math.max(1, (Number.isInteger(count.value) ? count.value : initialPax(props.capacity)) + delta)))

// ---- ¿Que van a comer? ----
// La propuesta (la primera vigente, «Menú del día») ya elegida; «Otro menú…» enseña las demas y, si hay menus con eleccion, «Más
// opciones». La configuracion se relee al abrir la hoja (y tras un offer_unavailable): si nadie ha elegido aun se propone la nueva; si lo
// que el camarero eligio deja de estar vigente, la hoja queda sin menu (como tras un offer_unavailable) y lo dice: nunca cambia sola a
// otro menu que el no eligio (revision final de C3e).
const groups = computed(() => offerGroups(props.offers))
const offerId = ref(groups.value.proposed?.id ?? '')
const touched = ref(false)
const others = ref(false)
const choiceOpen = ref(false)
// C3e (revision): tras un offer_unavailable el servidor pidio «elige otro». La hoja NO cambia sola a la nueva propuesta (un segundo toque
// abriria la mesa con un menu que el camarero no eligio): queda sin menu y «Abrir» apagado hasta que toque uno.
const mustPick = ref(false)
const offerGone = ref(false)
watch(() => props.rejection, rejection => { if (rejection?.code === 'offer_unavailable') { offerId.value = ''; touched.value = false; mustPick.value = true } })
watch(() => props.offers.map(offer => offer.id).join('|'), () => {
  if (mustPick.value) return
  if (touched.value && !props.offers.some(offer => offer.id === offerId.value)) {
    offerId.value = ''; touched.value = false; mustPick.value = true; offerGone.value = true
    return
  }
  if (!touched.value) offerId.value = groups.value.proposed?.id ?? ''
})
const chosen = computed(() => props.offers.find(offer => offer.id === offerId.value) ?? null)
// Lo elegido nunca queda escondido: otra oferta que la propuesta deja «Otro menú…» abierto, y un menu con eleccion, «Más opciones».
const othersShown = computed(() => others.value || (chosen.value !== null && chosen.value.id !== groups.value.proposed?.id))
const choiceShown = computed(() => choiceOpen.value || groups.value.withChoice.some(offer => offer.id === offerId.value))
const mainOffers = computed(() => [...(groups.value.proposed ? [groups.value.proposed] : []), ...(othersShown.value ? groups.value.others : [])])
const hiddenOffers = computed(() => groups.value.others.length + groups.value.withChoice.length)
// Sin otras cartas ni menus cerrados, «Otro menú…» lleva directo a los menus con eleccion (no hace falta un segundo toque).
function showOthers(): void { others.value = true; if (groups.value.others.length === 0) choiceOpen.value = true }
function pick(id: string): void { offerId.value = id; touched.value = true; mustPick.value = false; offerGone.value = false }
const describe = (offer: OfferChoice) => [offerKindLabel(offer.kind) === offer.name ? '' : offerKindLabel(offer.kind),
  offer.kind === 'a-la-carte' ? 'Platos sueltos de la carta' : offer.courses.length > 0 ? plural(offer.courses.length, 'pase', 'pases') : ''].filter(part => part).join(' · ')

// ---- ¿Alergias o intolerancias? ----
// Cada fila es UNA alergia con su comensal (decision 5), un borrador de abrir.ts con una clave propia para que Vue no confunda las filas al
// quitar una. El numero de la fila (1, 2…) es su posicion: nombra la fila en pantalla y en los data-testid. Rojo solo si la fila
// alarma con la misma regla que la ficha (una alergia o una grave); si no, ambar. Siempre con su texto: tipo y gravedad van escritos.
interface Row extends RestrictionDraft { key: number }
const rows = ref<Row[]>([])
let lastKey = 0
const guests = computed(() => guestChoices(count.value))
const alarming = (row: RestrictionDraft) => restrictionAlarming({ id: '', guestPosition: null, kind: row.kind, substance: row.substance, severity: row.severity })
const rowList = ref<HTMLElement | null>(null)
const addButton = ref<HTMLButtonElement | null>(null)
// Al anotar, la fila nueva sube a la vista y el foco va a su «¿Quién?» (sin filas, el boton que se toco desaparece); al quitar, el foco
// vuelve al boton de anotar.
function addRow(): void {
  if (rows.value.length >= MAX_DRAFTS) return
  rows.value.push({ ...newDraft(), key: ++lastKey })
  const n = rows.value.length
  void nextTick(() => {
    const row = rowList.value?.querySelector<HTMLElement>(`[data-testid="open-row-${n}"]`)
    row?.scrollIntoView({ block: 'start' })
    row?.querySelector<HTMLElement>('button.g')?.focus({ preventScroll: true })
  })
}
function removeRow(index: number): void {
  rows.value.splice(index, 1)
  void nextTick(() => addButton.value?.focus({ preventScroll: true }))
}

// ---- Abajo: resumen, problema y «Abrir» ----
// El primer problema de FORMATO (las personas antes que las filas); una fila vacia del todo se ignora (decision 6). «Abrir» queda apagado
// con un problema, con la mesa ocupada segun la ultima lectura, sin oferta o con una orden por resolver.
const problem = computed(() => paxProblem(count.value, props.capacity) || draftProblem(rows.value, count.value))
const summary = computed(() => openSummary(count.value, chosen.value, rows.value))
const paxText = computed(() => Number.isInteger(count.value) && count.value >= 1 ? plural(count.value, 'persona', 'personas') : '')
const summaryRest = computed(() => paxText.value && summary.value.startsWith(paxText.value) ? summary.value.slice(paxText.value.length) : summary.value)
const canOpen = computed(() => !props.locked && !props.occupied && chosen.value !== null && problem.value === '')
function submit(): void {
  if (!canOpen.value || !chosen.value) return
  emit('open', openRequest({ id: props.tableId, name: props.tableName }, count.value, chosen.value, rows.value))
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

    <div class="open-body">
      <section class="open-q" aria-labelledby="open-pax-h">
        <h3 id="open-pax-h" class="q-h">¿Cuántas personas?</h3>
        <div class="pax" :class="{ 'open-pax-8': twoRows }" role="radiogroup" aria-labelledby="open-pax-h">
          <button v-for="value in choices.counts" :key="value" type="button" role="radio" :aria-checked="count === value" :data-testid="`pax-${value}`"
            @click="choosePax(value)">{{ value }}</button>
        </div>
        <button v-if="choices.more" type="button" class="btn wide open-toggle" :aria-expanded="stepperShown" aria-label="Más de 8 personas" data-testid="pax-more"
          @click="moreOpen = !stepperShown"><Icon :name="stepperShown ? 'up' : 'down'" />Más…</button>
        <div v-if="stepperShown" class="stepper">
          <button type="button" class="btn" aria-label="Una persona menos" :disabled="count <= 1" @click="step(-1)"><Icon name="minus" :stroke="2.4" /></button>
          <input v-model.number="pax" name="pax" type="number" min="1" :max="limit" inputmode="numeric" aria-label="Personas" @input="paxTouched = true" />
          <button type="button" class="btn" aria-label="Una persona más" :disabled="count >= limit" @click="step(1)"><Icon name="plus" :stroke="2.4" /></button>
        </div>
      </section>

      <section class="open-q" aria-labelledby="open-offer-h">
        <h3 id="open-offer-h" class="q-h">¿Qué van a comer?</h3>
        <p v-if="offers.length === 0" class="notice neutral" data-testid="no-offers"><Icon name="info" />No hay menús ni cartas vigentes ahora. Se crean o se activan en el PC principal, en Menús y cartas.</p>
        <template v-else>
          <div class="offers" role="radiogroup" aria-labelledby="open-offer-h">
            <button v-for="offer in mainOffers" :key="offer.id" type="button" class="ocard" role="radio" :aria-checked="offerId === offer.id" :data-testid="`offer-${offer.id}`" @click="pick(offer.id)">
              <span class="radio" aria-hidden="true"></span>
              <span class="oc-t"><b>{{ offer.name }}<span v-if="offer.id === groups.proposed?.id" class="tag">Menú del día</span></b><small v-if="describe(offer)">{{ describe(offer) }}</small></span>
            </button>
          </div>
          <button v-if="!othersShown && hiddenOffers > 0" type="button" class="btn wide" data-testid="other-offers" @click="showOthers">Otro menú…</button>
          <template v-if="othersShown && groups.withChoice.length > 0">
            <button type="button" class="btn wide open-toggle" :aria-expanded="choiceShown" data-testid="offer-more" @click="choiceOpen = !choiceShown">
              <Icon :name="choiceShown ? 'up' : 'down'" />Más opciones</button>
            <div v-if="choiceShown" class="offers" role="radiogroup" aria-label="Menús con elección">
              <button v-for="offer in groups.withChoice" :key="offer.id" type="button" class="ocard" role="radio" :aria-checked="offerId === offer.id" :data-testid="`offer-${offer.id}`" @click="pick(offer.id)">
                <span class="radio" aria-hidden="true"></span>
                <span class="oc-t"><b>{{ offer.name }}</b><small v-if="describe(offer)">{{ describe(offer) }}</small></span>
              </button>
            </div>
          </template>
        </template>
      </section>

      <section class="open-q" aria-labelledby="open-allergies-h" data-testid="open-allergies">
        <h3 id="open-allergies-h" class="q-h">¿Alergias o intolerancias?</h3>
        <div v-if="rows.length === 0" class="card open-none">
          <p class="open-hint">Se anota por comensal y viaja con la apertura: cocina revisa cada plato afectado antes de que salga su pase. Si aún no lo sabes, se puede añadir después desde la mesa.</p>
          <button ref="addButton" type="button" class="btn wide" data-testid="open-add-restriction" @click="addRow"><Icon name="alert" />Anotar alergia o intolerancia</button>
        </div>
        <template v-else>
          <ol ref="rowList" class="open-rows">
            <li v-for="(row, index) in rows" :key="row.key" class="open-row" :class="{ 'open-alarm': alarming(row) }" :data-testid="`open-row-${index + 1}`">
              <div class="open-row-h"><Icon name="alert" :size="17" :stroke="2.2" /><span>Alergia o intolerancia {{ index + 1 }}</span>
                <button type="button" class="open-remove" :aria-label="`Quitar la alergia o intolerancia ${index + 1}`" :data-testid="`open-remove-${index + 1}`"
                  @click="removeRow(index)">Quitar</button></div>
              <div class="field"><span :id="`open-guest-l-${index + 1}`" class="lbl">¿Quién?</span>
                <div class="guests" role="radiogroup" :aria-labelledby="`open-guest-l-${index + 1}`">
                  <button v-for="choice in guests" :key="String(choice.guest)" type="button" class="g" :class="{ 'g-all': choice.guest === 'all' }" role="radio"
                    :aria-checked="row.guest === choice.guest" :aria-label="choice.guest === 'all' ? undefined : choice.label" :data-testid="`open-guest-${index + 1}-${choice.guest}`"
                    @click="row.guest = choice.guest">{{ choice.guest === 'all' ? choice.label : choice.guest }}</button>
                </div>
              </div>
              <div class="field"><span :id="`open-kind-l-${index + 1}`" class="lbl">Tipo</span>
                <div class="seg" role="radiogroup" :aria-labelledby="`open-kind-l-${index + 1}`">
                  <button v-for="option in KINDS" :key="option.code" type="button" role="radio" :aria-checked="row.kind === option.code"
                    :data-testid="`open-kind-${index + 1}-${option.code}`" @click="row.kind = option.code">{{ option.label }}</button>
                </div>
              </div>
              <div class="field"><label :for="`open-substance-${index + 1}`">¿A qué?</label>
                <input :id="`open-substance-${index + 1}`" v-model="row.substance" :name="`open-substance-${index + 1}`" type="text" maxlength="80" autocomplete="off"
                  placeholder="Por ejemplo, gluten" />
              </div>
              <div class="field"><span :id="`open-severity-l-${index + 1}`" class="lbl">Gravedad</span>
                <div class="seg" role="radiogroup" :aria-labelledby="`open-severity-l-${index + 1}`">
                  <button v-for="option in SEVERITIES" :key="option.code" type="button" role="radio" :aria-checked="row.severity === option.code"
                    :data-testid="`open-severity-${index + 1}-${option.code}`" @click="row.severity = option.code">{{ option.label }}</button>
                </div>
              </div>
            </li>
          </ol>
          <button ref="addButton" type="button" class="btn wide" :disabled="rows.length >= MAX_DRAFTS" data-testid="open-add-restriction" @click="addRow">
            <Icon name="plus" :stroke="2.2" />Otra alergia o intolerancia</button>
          <p v-if="rows.length >= MAX_DRAFTS" class="open-hint">Como mucho {{ MAX_DRAFTS }} al abrir; las demás se anotan después desde la mesa.</p>
        </template>
      </section>
    </div>

    <!-- El motivo de un rechazo va aqui, junto a «Abrir» y siempre a la vista (la barra es fija abajo), ademas de en el aviso de arriba:
         el camarero suele estar mas abajo, anotando alergias. -->
    <div class="bar">
      <p v-if="rejection" class="notice open-problem" role="alert" data-testid="open-rejected"><Icon name="info" />{{ rejection.text }}</p>
      <p v-else-if="offerGone" class="notice open-problem" role="alert" data-testid="open-offer-gone"><Icon name="info" />Ese menú ya no se ofrece ahora: elige otro.</p>
      <p v-if="summary" class="bar-note" data-testid="open-summary"><b v-if="paxText">{{ paxText }}</b>{{ summaryRest }}</p>
      <p v-if="problem" class="notice open-problem" role="status" data-testid="open-problem"><Icon name="info" />{{ problem }}</p>
      <button type="button" class="btn btn-primary btn-big" :disabled="!canOpen" data-testid="do-open" @click="submit"><Icon name="users" :size="22" />Abrir {{ tableName }}</button>
    </div>
  </article>
</template>
