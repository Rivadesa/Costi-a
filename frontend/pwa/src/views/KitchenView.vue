<script setup lang="ts">
import { computed, reactive } from 'vue'
import type { Session } from '../api'
import { elapsedLabel, kitchenTickets, PASS_STATION, REVIEW_DECISIONS, reviewNoteOk, type KitchenTicket } from '../kitchen'
import { courseLabel, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { useOperations } from '../operations'
import type { Preparation } from '../types'
import StatusPanel from './StatusPanel.vue'

// Pantalla de cocina por estacion (D6.4). Misma lectura en vivo y misma orden incierta que el comandero.
// Ninguna regla de negocio: un boton existe si y solo si el servidor anuncia esa accion en ESA elaboracion o en
// ESE pase para este dispositivo (rol + estacion, D4.3a). La estacion local solo decide que se atenua.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

const { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))

const station = computed(() => props.session.station)
const tickets = computed(() => kitchenTickets(state.entries, station.value))
const hint = computed(() => station.value === null ? 'Dispositivo de cocina sin estacion asignada: ve los pases de todas las estaciones.'
  : station.value === PASS_STATION ? 'Pase: ve todas las estaciones, valida los pases y decide las revisiones de restricciones.'
  : `Pases enviados con trabajo de la estacion ${station.value}, el mas antiguo primero. Lo de otras estaciones aparece atenuado y sin botones.`)

const notes = reactive<Record<string, string>>({})
const lineKey = (ticket: KitchenTicket, preparation: Preparation) => `${ticket.key}:${preparation.id}`
const can = (actions: string[] | undefined, action: string) => actions?.includes(action) === true

function command(ticket: KitchenTicket, action: string, fields: Record<string, unknown>, description: string): void {
  const service = ticket.entry.service
  void run(`/services/${encodeURIComponent(service.id)}/commands/${action}`, { expectedVersion: ticket.entry.version, courseId: ticket.course.id, ...fields },
    `${description} · ${ticket.course.name} · ${service.tableId}`)
}
const PREPARATION_ACTIONS = [
  { action: 'preparation-start', label: 'Empezar', verb: 'Empezar' },
  { action: 'preparation-ready', label: 'Marcar lista', verb: 'Marcar lista' },
] as const
function review(ticket: KitchenTicket, preparation: Preparation, decision: string, label: string): void {
  const key = lineKey(ticket, preparation)
  const note = (notes[key] ?? '').trim()
  if (!reviewNoteOk(note)) return
  command(ticket, 'review-preparation', { itemId: preparation.id, decision, note }, `Revision (${label}) de ${preparation.name}`)
  notes[key] = ''
}
</script>

<template>
  <section class="kitchen" data-testid="kitchen">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="live.refresh()" />

    <p data-testid="kitchen-hint">{{ hint }}</p>
    <p v-if="state.readAt && tickets.length === 0" data-testid="kitchen-empty">No hay pases enviados a cocina para esta pantalla.</p>

    <ul class="tickets">
      <li v-for="ticket in tickets" :key="ticket.key" class="ticket" :data-testid="`ticket-${ticket.entry.service.tableId}-${ticket.course.id}`">
        <h3>{{ ticket.entry.service.tableId }} · {{ ticket.course.name }} — {{ courseLabel(ticket.course.state) }}</h3>
        <p class="line">{{ ticket.entry.service.pax }} personas · {{ elapsedLabel(ticket.course.firedAt, now) }}</p>
        <p v-if="ticket.paused" class="line alert">⏸ SERVICIO EN PAUSA</p>
        <p v-if="ticket.pendingReviews > 0" class="line alert">⚠ {{ ticket.pendingReviews }} elaboracion(es) PENDIENTES DE REVISION tras un cambio de restricciones</p>

        <ul>
          <li v-for="line in ticket.lines" :key="line.preparation.id" class="prep" :data-testid="`prep-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`">
            <!-- Lo de otra estacion se atenua (solo el rotulo); los avisos de restriccion conservan siempre toda su fuerza. -->
            <p class="prep-title" :class="{ other: !line.own }">
              <strong>{{ line.preparation.name }}</strong><span v-if="line.preparation.quantity > 1"> × {{ line.preparation.quantity }}</span>
              <span v-if="line.preparation.guestPosition !== null"> · comensal {{ line.preparation.guestPosition }}</span>
              · estacion {{ line.preparation.stationId }}<span v-if="!line.own"> (otra estacion)</span> · <strong>{{ preparationLabel(line.preparation.state) }}</strong>
            </p>
            <span v-for="restriction in line.preparation.restrictions ?? []" :key="restriction.id" class="line alert">⚠ {{ restrictionLabel(restriction) }}</span>
            <span v-if="reviewLabel(line.preparation)" class="line" :class="{ alert: line.preparation.reviewPending }">{{ reviewLabel(line.preparation) }}</span>

            <div class="row">
              <template v-for="item in PREPARATION_ACTIONS" :key="item.action">
                <button v-if="can(line.preparation.actions, item.action)" type="button" class="big" :disabled="locked"
                  :data-testid="`do-${item.action}-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`"
                  @click="command(ticket, item.action, { itemId: line.preparation.id }, `${item.verb} ${line.preparation.name}`)">{{ item.label }}</button>
              </template>
            </div>

            <div v-if="can(line.preparation.actions, 'review-preparation')" class="review" :data-testid="`review-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`">
              <label>Nota de la revision (obligatoria)
                <input v-model="notes[lineKey(ticket, line.preparation)]" :name="`note-${line.preparation.id}`" maxlength="200" autocomplete="off" :disabled="locked" />
              </label>
              <div class="row">
                <button v-for="option in REVIEW_DECISIONS" :key="option.decision" type="button" class="big" :class="{ secondary: option.decision !== 'remake' }"
                  :disabled="locked || !reviewNoteOk(notes[lineKey(ticket, line.preparation)] ?? '')"
                  :data-testid="`do-review-${option.decision}-${ticket.entry.service.tableId}-${ticket.course.id}-${line.preparation.id}`"
                  @click="review(ticket, line.preparation, option.decision, option.label)">{{ option.label }}</button>
              </div>
            </div>
          </li>
        </ul>

        <button v-if="can(ticket.course.actions, 'ready')" type="button" class="big wide" :disabled="locked"
          :data-testid="`do-ready-${ticket.entry.service.tableId}-${ticket.course.id}`"
          @click="command(ticket, 'ready', {}, 'Validar pase LISTO')">Validar pase: LISTO PARA SERVIR</button>
      </li>
    </ul>
  </section>
</template>
