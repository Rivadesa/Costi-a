<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, type ComponentPublicInstance } from 'vue'
import { draftProblem, DUPLICATE_PROBLEM, GUEST_PROBLEM, guestChoices, KINDS, newDraft, SEVERITIES, type RestrictionDraft } from '../abrir'
import { REASON_PROBLEM, reasonLabel, reasonOk, serviceCommand } from '../ficha'
import { guestLabel, plural, restrictionLabel } from '../labels'
import { allergyList } from '../pases'
import type { BoardEntry, GuestRestriction } from '../types'
import Icon from './Icon.vue'

// C3e (plan 2026-10-27, paso 3; plan 2026-10-15, 680): la hoja de alergias, desde la franja roja de la ficha. Cada restriccion de la
// mesa ESCRITA entera (las de toda la mesa primero), con los platos a los que afecta y su estado (pases.ts, allergyList): nunca se
// esconde, atenua ni corta una alergia. Rojo solo si alarma; ambar lo que espera a cocina.
// Lo que se puede hacer sale de lo que ANUNCIA el servidor para esta mesa (D3.1): «Retirar» con remove-restriction (antes en «Más
// opciones») y el alta con declare-restriction (antes el <details> de la franja). Sin ninguna de las dos (una mesa terminada) la hoja
// solo se lee. Cada toque es UNA orden con la version leida (F01), y la hoja se cierra para que el resultado se vea arriba (aviso u
// orden sin confirmar). Lo unico que se comprueba aqui es el FORMATO, con los textos de abrir.ts (los del PC).
const props = defineProps<{ entry: BoardEntry; tableName: string; locked: boolean }>()
const emit = defineEmits<{ run: [path: string, body: Record<string, unknown>, description: string]; close: [] }>()

const service = computed(() => props.entry.service)
const has = (action: string) => service.value.actions?.includes(action) === true
const items = computed(() => allergyList(service.value))
const canDeclare = computed(() => has('declare-restriction'))
const canRemove = computed(() => has('remove-restriction') && items.value.length > 0)
const sub = computed(() => `${props.tableName} · ${plural(service.value.pax, 'persona', 'personas')}`)

// ---- Retirar: en DOS pasos dentro de la tarjeta de esa restriccion ----
// C3e (revision): «Retirar» de una tarjeta abre ahi mismo su motivo (3 caracteres o mas) con «Volver» y la confirmacion (do-remove-
// restriction-<id>), como la hoja de omitir: el campo ya no queda debajo de TODAS las tarjetas, a una pantalla del boton. Una sola a la
// vez; al cambiar de tarjeta el motivo empieza vacio (nunca se reutiliza el de otra restriccion).
const removing = ref<string | null>(null)
const reason = ref('')
const removeProblem = ref('')
let reasonField: HTMLInputElement | null = null
const setReasonField = (element: Element | ComponentPublicInstance | null) => { reasonField = element instanceof HTMLInputElement ? element : null }
const list = ref<HTMLElement | null>(null)
const reasonText = reasonLabel(['remove-restriction'])
function startRemove(restriction: GuestRestriction): void {
  removing.value = restriction.id; reason.value = ''; removeProblem.value = ''
  void nextTick(() => { reasonField?.focus({ preventScroll: true }); reasonField?.scrollIntoView({ block: 'nearest' }) })
}
function cancelRemove(): void {
  const id = removing.value
  removing.value = null; removeProblem.value = ''
  void nextTick(() => list.value?.querySelector<HTMLElement>(`[data-testid="remove-restriction-${CSS.escape(id ?? '')}"]`)?.focus({ preventScroll: true }))
}
function remove(restriction: GuestRestriction): void {
  if (!reasonOk(reason.value)) { removeProblem.value = REASON_PROBLEM; reasonField?.focus(); return }
  removeProblem.value = ''
  emit('run', serviceCommand(service.value, 'remove-restriction'), { expectedVersion: props.entry.version, restrictionId: restriction.id, reason: reason.value.trim() },
    `Retirar restricción · ${restrictionLabel(restriction)} · ${props.tableName}`)
  emit('close')
}

// ---- Anotar: una fila como las de «Abrir mesa» (quien sin valor por defecto; Alergia y Grave por defecto) ----
const draft = reactive<RestrictionDraft>(newDraft())
const guests = computed(() => guestChoices(service.value.pax))
const declareProblem = ref('')
// Las ya anotadas, como filas, para adelantar el duplicate_restriction del servidor con el mismo texto que la hoja de abrir.
const noted = computed<RestrictionDraft[]>(() => (service.value.restrictions ?? []).map(restriction => ({
  guest: restriction.guestPosition ?? 'all', kind: restriction.kind, substance: restriction.substance, severity: restriction.severity,
})))
function problemOf(): string {
  // Sin tocar nada no hay fila: lo primero que falta es para quien.
  if (draft.guest === null && draft.substance.trim().length === 0) return GUEST_PROBLEM
  const own = draftProblem([draft], service.value.pax)
  if (own) return own
  return draftProblem([...noted.value, draft], service.value.pax) === DUPLICATE_PROBLEM ? DUPLICATE_PROBLEM : ''
}
function declare(): void {
  const problem = problemOf()
  declareProblem.value = problem
  if (problem) return
  const text = draft.substance.trim()
  const guestPosition = draft.guest === 'all' ? null : draft.guest
  emit('run', serviceCommand(service.value, 'declare-restriction'),
    { expectedVersion: props.entry.version, guestPosition, kind: draft.kind, substance: text, severity: draft.severity },
    `Declarar restricción · ${guestLabel(guestPosition)} · ${text} · ${props.tableName}`)
  emit('close')
}

// El foco entra en «Cerrar»; al cerrar vuelve a lo que abrio la hoja (sin desplazar la pantalla).
const first = ref<HTMLButtonElement | null>(null)
const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
onMounted(() => first.value?.focus())
onBeforeUnmount(() => opener?.focus({ preventScroll: true }))
</script>

<template>
  <div class="scrim" aria-hidden="true" @click="emit('close')"></div>
  <section class="sheet fc-al" role="dialog" aria-modal="true" aria-labelledby="allergy-title" data-testid="allergy-sheet" @keydown.esc="emit('close')">
    <div class="grab" aria-hidden="true"></div>
    <div class="sheet-scroll">
      <div class="sh-head">
        <span class="sh-tx"><h3 id="allergy-title" class="sh-title">Alergias e intolerancias</h3><span class="sh-sub">{{ sub }}</span></span>
        <button ref="first" type="button" class="icon-btn" aria-label="Cerrar" data-testid="allergy-close" @click="emit('close')"><Icon name="x" :size="22" :stroke="2.2" /></button>
      </div>

      <p v-if="items.length === 0" class="fc-al-none" data-testid="allergy-none"><Icon name="checkCircle" :size="18" :stroke="2.2" />Sin alergias ni intolerancias anotadas.</p>
      <ul v-else ref="list" class="fc-al-list">
        <li v-for="item in items" :key="item.restriction.id" class="fc-al-item" :class="{ alarm: item.alarming }" :data-testid="`allergy-${item.restriction.id}`">
          <p class="fc-al-r"><Icon name="alert" :size="18" :stroke="2.3" /><span>{{ item.text }}</span></p>
          <!-- Cada plato es texto, no un enlace: un punto neutro; el aviso solo en el que espera a cocina. -->
          <ul v-if="item.dishes.length > 0" class="fc-al-dishes">
            <li v-for="(dish, index) in item.dishes" :key="index" :class="{ 'fc-al-pending': dish.pending }">
              <Icon v-if="dish.pending" name="alert" :size="15" :stroke="2.2" /><span v-else class="fc-al-dot" aria-hidden="true"></span><span>{{ dish.text }}</span>
            </li>
          </ul>
          <p v-else class="fc-al-nodish">Todavía no afecta a ningún plato de la mesa.</p>
          <template v-if="canRemove">
            <div v-if="removing === item.restriction.id" class="fc-al-rm" :data-testid="`remove-step-${item.restriction.id}`">
              <div class="field">
                <label for="allergy-reason">{{ reasonText }}</label>
                <input id="allergy-reason" :ref="setReasonField" v-model="reason" name="allergy-reason" maxlength="200" autocomplete="off" :disabled="locked" />
              </div>
              <p v-if="removeProblem" class="notice" role="alert" data-testid="allergy-problem"><Icon name="info" />{{ removeProblem }}</p>
              <div class="fc-al-rm-b">
                <button type="button" class="btn" data-testid="remove-cancel" @click="cancelRemove"><Icon name="back" />Volver</button>
                <button type="button" class="btn btn-caution" :disabled="locked" :aria-label="`Retirar ${item.text}`"
                  :data-testid="`do-remove-restriction-${item.restriction.id}`" @click="remove(item.restriction)"><Icon name="x" />Retirar</button>
              </div>
            </div>
            <button v-else type="button" class="btn btn-caution fc-al-remove" :aria-label="`Retirar ${item.text}`"
              :data-testid="`remove-restriction-${item.restriction.id}`" @click="startRemove(item.restriction)"><Icon name="x" />Retirar</button>
          </template>
        </li>
      </ul>

      <!-- El alta: los mismos campos que la franja tenia en su <details> (C3a, E4e) y que cada fila de «Abrir mesa». -->
      <section v-if="canDeclare" class="fc-al-add stack" aria-labelledby="allergy-add-title" data-testid="declare">
        <h4 id="allergy-add-title" class="fc-al-add-h"><Icon name="plus" :stroke="2.2" />Nueva alergia o intolerancia</h4>
        <!-- «¿Para quién?», el MISMO control que cada fila de «Abrir mesa» (.guests/.g, «Toda la mesa» en la misma fila). -->
        <div class="field"><span id="declare-guest-l" class="lbl">¿Para quién?</span>
          <div class="guests" role="radiogroup" aria-labelledby="declare-guest-l">
            <button v-for="choice in guests" :key="String(choice.guest)" type="button" class="g" :class="{ 'g-all': choice.guest === 'all' }" role="radio"
              :aria-checked="draft.guest === choice.guest" :aria-label="choice.guest === 'all' ? undefined : choice.label" :data-testid="`declare-guest-${choice.guest}`"
              @click="draft.guest = choice.guest">{{ choice.guest === 'all' ? choice.label : choice.guest }}</button>
          </div>
        </div>
        <div class="field"><span class="lbl">Tipo</span>
          <div class="seg" role="radiogroup" aria-label="Tipo">
            <button v-for="option in KINDS" :key="option.code" type="button" role="radio" :aria-checked="draft.kind === option.code"
              :data-testid="`declare-kind-${option.code}`" @click="draft.kind = option.code">{{ option.label }}</button>
          </div>
        </div>
        <div class="field"><label for="substance">¿A qué?</label>
          <input id="substance" v-model="draft.substance" name="substance" maxlength="80" autocomplete="off" placeholder="Por ejemplo, gluten" :disabled="locked" />
        </div>
        <div class="field"><span class="lbl">Gravedad</span>
          <div class="seg" role="radiogroup" aria-label="Gravedad">
            <button v-for="option in SEVERITIES" :key="option.code" type="button" role="radio" :aria-checked="draft.severity === option.code"
              :data-testid="`declare-severity-${option.code}`" @click="draft.severity = option.code">{{ option.label }}</button>
          </div>
        </div>
        <p v-if="declareProblem" class="notice" role="alert" data-testid="declare-problem"><Icon name="info" />{{ declareProblem }}</p>
        <button type="button" class="btn btn-primary wide" :disabled="locked" data-testid="do-declare-restriction" @click="declare"><Icon name="plus" :stroke="2.2" />Anotar alergia o intolerancia</button>
      </section>
    </div>
  </section>
</template>
