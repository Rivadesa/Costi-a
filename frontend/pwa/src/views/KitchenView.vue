<script setup lang="ts">
import { computed, reactive } from 'vue'
import type { Session } from '../api'
import { Names, useConfiguration } from '../configuration'
import { restrictionAlarming } from '../floor'
import { elapsedLabel, kitchenTickets, PASS_STATION, REVIEW_DECISIONS, reviewNoteOk, type KitchenLine, type KitchenTicket } from '../kitchen'
import { courseLabel, guestLabel, plural, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { useOperations } from '../operations'
import type { Preparation } from '../types'
import Icon from './Icon.vue'
import StatusPanel from './StatusPanel.vue'

// Pantalla de cocina por estacion (D6.4). Misma lectura en vivo y misma orden incierta que el comandero.
// Ninguna regla de negocio: un boton existe si y solo si el servidor anuncia esa accion en ESA elaboracion o en
// ESE pase para este dispositivo (rol + estacion, D4.3a). La estacion local solo decide que se atenua.
// C3a: el aspecto nuevo, botones de 64 px y los NOMBRES de mesa y estacion (de /configuration, releida cada 5 minutos). La agrupacion
// por plato llega en C3c.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

const { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))
const { configuration, reload } = useConfiguration(props, () => emit('unauthorized'))
const names = computed(() => new Names(configuration))

const station = computed(() => props.session.station)
const stationName = computed(() => props.session.stationName || names.value.station(station.value))
const tickets = computed(() => kitchenTickets(state.entries, station.value))
const hint = computed(() => station.value === null ? 'Pantalla de cocina sin estación asignada: ve los pases de todas las estaciones.'
  : station.value === PASS_STATION ? 'Pase: ve todas las estaciones, valida los pases y decide las revisiones de restricciones.'
  : `Pases enviados con trabajo de ${stationName.value}, el más antiguo primero. Lo de otras estaciones aparece atenuado y sin botones.`)

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
</script>

<template>
  <section class="kitchen" data-testid="kitchen">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="refreshAll" />

    <p class="kitchen-hint" data-testid="kitchen-hint">{{ hint }}</p>
    <p v-if="state.readAt && tickets.length === 0" class="notice neutral" data-testid="kitchen-empty"><Icon name="chef" />No hay pases enviados a cocina para esta pantalla.</p>

    <ul class="tickets">
      <li v-for="ticket in tickets" :key="ticket.key" class="ticket" :data-testid="`ticket-${ticket.entry.service.tableId}-${ticket.course.id}`">
        <h3><span>{{ tableName(ticket) }} · {{ ticket.course.name }}</span><span class="pill" :class="ticket.course.state === 'Ready' ? 's-ready' : 's-kitchen'">{{ courseLabel(ticket.course.state) }}</span></h3>
        <p class="ticket-sub"><Icon name="clock" :size="16" />{{ plural(ticket.entry.service.pax, 'persona', 'personas') }} · {{ elapsedLabel(ticket.course.firedAt, now) }}</p>
        <p v-if="ticket.paused" class="notice" role="status"><Icon name="pause" />MESA EN PAUSA</p>
        <p v-if="ticket.pendingReviews > 0" class="line-alert alarm"><Icon name="alert" :size="16" :stroke="2.2" />{{ plural(ticket.pendingReviews, 'elaboración PENDIENTE DE REVISIÓN', 'elaboraciones PENDIENTES DE REVISIÓN') }} tras un cambio de restricciones</p>

        <ul>
          <li v-for="line in ticket.lines" :key="line.preparation.id" class="prep" :data-testid="`prep-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`">
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
              <label class="lbl" :for="`note-${line.preparation.id}`">Nota de la revisión (obligatoria)</label>
              <input :id="`note-${line.preparation.id}`" v-model="notes[lineKey(ticket, line.preparation)]" :name="`note-${line.preparation.id}`" maxlength="200" autocomplete="off" :disabled="locked" />
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
