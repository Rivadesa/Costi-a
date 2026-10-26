<script setup lang="ts">
import { computed, reactive } from 'vue'
import type { Session } from '../api'
import { Names, useConfiguration } from '../configuration'
import { restrictionAlarming } from '../floor'
import { elapsedLabel, kitchenTickets, PASS_STATION, PRESEND_DECISIONS, presendReviews, restBody, restOfGuest, REVIEW_DECISIONS, reviewNoteOk,
  type KitchenLine, type KitchenTicket, type PresendGuest, type PresendItem } from '../kitchen'
import { courseLabel, guestLabel, plural, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { useOperations } from '../operations'
import type { Preparation } from '../types'
import Icon from './Icon.vue'
import StatusPanel from './StatusPanel.vue'

// Pantalla de cocina por estacion (D6.4). Misma lectura en vivo y misma orden incierta que el comandero.
// Ninguna regla de negocio: un boton existe si y solo si el servidor anuncia esa accion en ESA elaboracion o en
// ESE pase para este dispositivo (rol + estacion, D4.3a). La estacion local solo decide que se atenua.
// C3a: el aspecto nuevo, botones de 64 px y los NOMBRES de mesa y estacion (de /configuration, releida cada 5 minutos). La agrupacion
// por plato llega en C3d.
// C3c (Costina Cocina, lo minimo hasta C3d): sin el parrafo de ayuda de arriba; la cabecera ya dice «Costiña Cocina · <estacion>».
// E4e (ADR-016, lo minimo hasta C3d): arriba, «Por revisar antes de enviar»: por mesa y comensal, los platos de los pases SIN ENVIAR que
// el servidor le pide revisar a este dispositivo (solo al pase). Cada plato con «No le afecta» o «Adaptado» y su nota obligatoria (una
// orden review-preparation), y «No le afecta a nada más», que da esa decision a todo lo que queda del comensal en UNA orden
// review-preparations. «Rehacer» no se ofrece aqui: solo vale para lo que ya salio.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

const { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))
const { configuration, reload } = useConfiguration(props, () => emit('unauthorized'))
const names = computed(() => new Names(configuration))

const station = computed(() => props.session.station)
const tickets = computed(() => kitchenTickets(state.entries, station.value))

const notes = reactive<Record<string, string>>({})
const lineKey = (ticket: KitchenTicket, preparation: Preparation) => `${ticket.key}:${preparation.id}`
const can = (actions: string[] | undefined, action: string) => actions?.includes(action) === true
const tableName = (ticket: KitchenTicket) => names.value.table(ticket.entry.service.tableId)
// La estacion de cada linea solo donde dice algo: en lo ajeno, en el pase y sin estacion asignada. En lo propio ya la dice la cabecera.
const showStation = (line: KitchenLine) => !line.own || station.value === null || station.value === PASS_STATION

function command(ticket: KitchenTicket, action: string, fields: Record<string, unknown>, description: string): void {
  const service = ticket.entry.service
  void run(`/dining/services/${encodeURIComponent(service.id)}/commands/${action}`, { expectedVersion: ticket.entry.version, courseId: ticket.course.id, ...fields },
    `${description} · ${ticket.course.name} · ${tableName(ticket)}`)
}
const PREPARATION_ACTIONS = [
  { action: 'preparation-start', label: 'Empezar', verb: 'Empezar', icon: 'play' },
  { action: 'preparation-ready', label: 'Marcar lista', verb: 'Marcar lista', icon: 'check' },
] as const
function review(ticket: KitchenTicket, preparation: Preparation, decision: string, label: string): void {
  const key = lineKey(ticket, preparation)
  const note = (notes[key] ?? '').trim()
  if (!reviewNoteOk(note)) return
  command(ticket, 'review-preparation', { itemId: preparation.id, decision, note }, `Revisión (${label}) de ${preparation.name}`)
  notes[key] = ''
}
function refreshAll(): void { void live.refresh(); void reload() }

// ---- E4e: «Por revisar antes de enviar» ----
const presend = computed(() => presendReviews(state.entries))
const guestTable = (guest: PresendGuest) => names.value.table(guest.entry.service.tableId)
const guestId = (guest: PresendGuest) => `${guest.entry.service.tableId}-${guest.guestPosition ?? 'all'}`
const itemId = (guest: PresendGuest, item: PresendItem) => `${guest.entry.service.tableId}-${item.course.id}-${item.preparation.id}`
const itemKey = (guest: PresendGuest, item: PresendItem) => `antes:${guest.entry.service.id}:${item.course.id}:${item.preparation.id}`
// E4e (revision): el id de cada campo de nota (y su etiqueta) sale de su POSICION en la lista, unico por construccion. El id de una
// elaboracion (<plato>-<comensal>) se repite entre mesas con la misma oferta: con el, la etiqueta de una mesa llevaba a la nota de otra.
const rest = computed(() => new Map(presend.value.map(guest => [guest.key, restOfGuest(guest)])))
const restNotes = reactive<Record<string, string>>({})
function presendReview(guest: PresendGuest, item: PresendItem, decision: string, label: string): void {
  const key = itemKey(guest, item)
  const note = (notes[key] ?? '').trim()
  if (!reviewNoteOk(note)) return
  const service = guest.entry.service
  void run(`/dining/services/${encodeURIComponent(service.id)}/commands/review-preparation`,
    { expectedVersion: guest.entry.version, courseId: item.course.id, itemId: item.preparation.id, decision, note },
    `Revisión antes de enviar (${label}) de ${item.preparation.name} · ${guestLabel(guest.guestPosition)} · ${item.course.name} · ${guestTable(guest)}`)
  notes[key] = ''
}
function nothingElse(guest: PresendGuest): void {
  const note = (restNotes[guest.key] ?? '').trim()
  const items = rest.value.get(guest.key) ?? []
  if (!reviewNoteOk(note) || items.length === 0) return
  const service = guest.entry.service
  void run(`/dining/services/${encodeURIComponent(service.id)}/commands/review-preparations`, restBody(guest, note),
    `No le afecta a nada más (${plural(items.length, 'plato', 'platos')}) · ${guestLabel(guest.guestPosition)} · ${guestTable(guest)}`)
  restNotes[guest.key] = ''
}
</script>

<template>
  <section class="kitchen" data-testid="kitchen">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="refreshAll" />

    <!-- E4e: lo que el pase decide antes de enviar, siempre arriba. Tipo, sustancia y gravedad escritos; nunca atenuado. -->
    <section v-if="presend.length > 0" class="presend" aria-labelledby="presend-title" data-testid="presend">
      <h2 id="presend-title"><Icon name="alert" :size="22" :stroke="2.2" />Por revisar antes de enviar</h2>
      <p class="presend-help">Decide cada plato de estos comensales antes de que salga su pase: hasta entonces, ese pase espera.</p>
      <article v-for="(guest, g) in presend" :key="guest.key" class="presend-guest" :data-testid="`presend-${guestId(guest)}`">
        <h3>{{ guestTable(guest) }} · {{ guestLabel(guest.guestPosition) }}</h3>
        <p v-for="restriction in guest.restrictions" :key="restriction.id" class="line-alert" :class="{ alarm: restrictionAlarming(restriction) }">
          <Icon name="alert" :size="16" :stroke="2.2" />{{ restrictionLabel(restriction) }}</p>
        <ul>
          <li v-for="(item, i) in guest.items" :key="itemKey(guest, item)" class="review presend-item" :data-testid="`presend-item-${itemId(guest, item)}`">
            <p class="prep-title"><strong>{{ item.preparation.name }}</strong><template v-if="item.preparation.quantity > 1"> × {{ item.preparation.quantity }}</template>
              · {{ item.course.name }} · {{ names.station(item.preparation.stationId) }}</p>
            <label class="lbl" :for="`presend-note-${g}-${i}`">Nota (obligatoria): no lo lleva, o el plato adaptado</label>
            <input :id="`presend-note-${g}-${i}`" v-model="notes[itemKey(guest, item)]" :name="`presend-note-${g}-${i}`" maxlength="200" autocomplete="off" :disabled="locked" />
            <div class="row two">
              <button v-for="option in PRESEND_DECISIONS" :key="option.decision" type="button" class="btn kbtn" :class="{ 'btn-primary': option.decision === 'adapt' }"
                :disabled="locked || !reviewNoteOk(notes[itemKey(guest, item)] ?? '')" :data-testid="`do-presend-${option.decision}-${itemId(guest, item)}`"
                @click="presendReview(guest, item, option.decision, option.label)">{{ option.label }}</button>
            </div>
          </li>
        </ul>
        <div v-if="guest.items.length > 1 && (rest.get(guest.key)?.length ?? 0) > 0" class="review rest" :data-testid="`presend-rest-${guestId(guest)}`">
          <label class="lbl" :for="`presend-rest-note-${g}`">Nota para todo lo que queda (obligatoria)</label>
          <input :id="`presend-rest-note-${g}`" v-model="restNotes[guest.key]" :name="`presend-rest-note-${g}`" maxlength="200" autocomplete="off" :disabled="locked" />
          <button type="button" class="btn kbtn wide" :disabled="locked || !reviewNoteOk(restNotes[guest.key] ?? '')" :data-testid="`do-presend-rest-${guestId(guest)}`"
            @click="nothingElse(guest)"><Icon name="checkCircle" :size="22" />No le afecta a nada más · {{ plural(rest.get(guest.key)?.length ?? 0, 'plato', 'platos') }}</button>
        </div>
      </article>
    </section>

    <p v-if="state.readAt && tickets.length === 0" class="notice neutral" data-testid="kitchen-empty"><Icon name="chef" />No hay pases enviados a cocina para esta pantalla.</p>

    <ul class="tickets">
      <li v-for="(ticket, t) in tickets" :key="ticket.key" class="ticket" :data-testid="`ticket-${ticket.entry.service.tableId}-${ticket.course.id}`">
        <h3><span>{{ tableName(ticket) }} · {{ ticket.course.name }}</span><span class="pill" :class="ticket.course.state === 'Ready' ? 's-ready' : 's-kitchen'">{{ courseLabel(ticket.course.state) }}</span></h3>
        <p class="ticket-sub"><Icon name="clock" :size="16" />{{ plural(ticket.entry.service.pax, 'persona', 'personas') }} · {{ elapsedLabel(ticket.course.firedAt, now) }}</p>
        <p v-if="ticket.paused" class="notice" role="status"><Icon name="pause" />MESA EN PAUSA</p>
        <p v-if="ticket.pendingReviews > 0" class="line-alert alarm"><Icon name="alert" :size="16" :stroke="2.2" />{{ plural(ticket.pendingReviews, 'elaboración PENDIENTE DE REVISIÓN', 'elaboraciones PENDIENTES DE REVISIÓN') }} tras un cambio de restricciones</p>

        <ul>
          <li v-for="(line, l) in ticket.lines" :key="line.preparation.id" class="prep" :data-testid="`prep-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`">
            <!-- Lo de otra estacion se atenua (solo el rotulo); los avisos de restriccion conservan siempre toda su fuerza. -->
            <p class="prep-title" :class="{ other: !line.own }">
              <strong>{{ line.preparation.name }}</strong><template v-if="line.preparation.quantity > 1"> × {{ line.preparation.quantity }}</template>
              · {{ guestLabel(line.preparation.guestPosition) }}<template v-if="showStation(line)"> · {{ names.station(line.preparation.stationId) }}<template v-if="!line.own"> (otra estación)</template></template>
              · <strong>{{ preparationLabel(line.preparation.state) }}</strong>
            </p>
            <p v-for="restriction in line.preparation.restrictions ?? []" :key="restriction.id" class="line-alert" :class="{ alarm: restrictionAlarming(restriction) }">
              <Icon name="alert" :size="16" :stroke="2.2" />{{ restrictionLabel(restriction) }}</p>
            <p v-if="reviewLabel(line.preparation)" class="line-alert" :class="{ alarm: line.preparation.reviewPending }">
              <Icon :name="line.preparation.reviewPending ? 'alert' : 'checkCircle'" :size="16" :stroke="2.2" />{{ reviewLabel(line.preparation) }}</p>

            <div class="row">
              <template v-for="item in PREPARATION_ACTIONS" :key="item.action">
                <button v-if="can(line.preparation.actions, item.action)" type="button" class="btn btn-primary kbtn" :disabled="locked"
                  :data-testid="`do-${item.action}-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`"
                  @click="command(ticket, item.action, { itemId: line.preparation.id }, `${item.verb} ${line.preparation.name}`)"><Icon :name="item.icon" :size="22" />{{ item.label }}</button>
              </template>
            </div>

            <div v-if="can(line.preparation.actions, 'review-preparation')" class="review" :data-testid="`review-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`">
              <label class="lbl" :for="`note-${t}-${l}`">Nota de la revisión (obligatoria)</label>
              <input :id="`note-${t}-${l}`" v-model="notes[lineKey(ticket, line.preparation)]" :name="`note-${t}-${l}`" maxlength="200" autocomplete="off" :disabled="locked" />
              <div class="row">
                <button v-for="option in REVIEW_DECISIONS" :key="option.decision" type="button" class="btn kbtn" :class="{ 'btn-primary': option.decision === 'remake' }"
                  :disabled="locked || !reviewNoteOk(notes[lineKey(ticket, line.preparation)] ?? '')"
                  :data-testid="`do-review-${option.decision}-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`"
                  @click="review(ticket, line.preparation, option.decision, option.label)">{{ option.label }}</button>
              </div>
            </div>
          </li>
        </ul>

        <button v-if="can(ticket.course.actions, 'ready')" type="button" class="btn btn-primary kbtn wide" :disabled="locked"
          :data-testid="`do-ready-${ticket.entry.service.tableId}-${ticket.course.id}`"
          @click="command(ticket, 'ready', {}, 'Validar pase listo')"><Icon name="checkCircle" :size="24" />Validar pase: listo para servir</button>
      </li>
    </ul>
  </section>
</template>
