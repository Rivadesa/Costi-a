<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { call } from '../api'
import { ageLabel, boardSummary, courseLabel, diningLabel, pendingReviews, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { LINK_TEXT, LiveBoard, type LiveState } from '../live'
import { connectHub } from '../realtime'
import type { BoardEntry } from '../types'

// Comandero: SOLO LECTURA en este corte (D6.2). Las acciones llegan con la orden durable (D6.3).
const props = defineProps<{ token: string }>()
const emit = defineEmits<{ unauthorized: [] }>()

const state = reactive<LiveState>({ entries: [], readAt: null, link: 'connecting', error: '' })
const selectedId = ref<string | null>(null)
const selected = computed(() => state.entries.find(entry => entry.service.id === selectedId.value) ?? null)
const live = new LiveBoard(state, {
  load: () => call<BoardEntry[]>(fetch, '/board', { token: props.token }),
  connect: () => connectHub(fetch, props.token),
  onUnauthorized: () => emit('unauthorized'),
})

const now = ref(Date.now())
let clock: number | undefined
const age = computed(() => state.readAt ? Math.max(0, Math.round((now.value - state.readAt.getTime()) / 1000)) : null)
const stale = computed(() => age.value === null || age.value > 45)
const online = () => { void live.refresh() }

onMounted(() => {
  clock = window.setInterval(() => { now.value = Date.now() }, 1000)
  window.addEventListener('online', online)
  void live.start()
})
onUnmounted(() => { window.clearInterval(clock); window.removeEventListener('online', online); void live.stop() })
</script>

<template>
  <section data-testid="board">
    <p class="link" :class="{ warn: state.link !== 'live' }" role="status" data-testid="link">{{ LINK_TEXT[state.link] }}</p>
    <p :class="{ stale }" data-testid="age">Ultima lectura del servidor: {{ ageLabel(age) }}<strong v-if="stale"> · DATOS ANTIGUOS</strong></p>
    <p v-if="state.error" class="notice" role="alert" data-testid="board-error">{{ state.error }}</p>
    <button type="button" @click="live.refresh()">Actualizar</button>

    <template v-if="!selected">
      <h2>Mesas en servicio</h2>
      <p v-if="state.readAt && state.entries.length === 0" data-testid="empty">No hay mesas abiertas.</p>
      <ul class="tables">
        <li v-for="entry in state.entries" :key="entry.service.id">
          <button type="button" class="table" :data-testid="`table-${entry.service.tableId}`" @click="selectedId = entry.service.id">
            <strong>{{ entry.service.tableId }}</strong> · {{ entry.service.pax }} personas · {{ diningLabel(entry.service.state) }}
            <span class="line">{{ boardSummary(entry) }}</span>
            <span v-if="entry.service.restrictions?.length" class="line alert">⚠ {{ entry.service.restrictions.length }} restriccion(es) declaradas</span>
            <span v-if="pendingReviews(entry.service) > 0" class="line alert">⚠ {{ pendingReviews(entry.service) }} elaboracion(es) PENDIENTES DE REVISION por cocina</span>
          </button>
        </li>
      </ul>
    </template>

    <article v-else data-testid="detail">
      <button type="button" class="secondary" @click="selectedId = null">‹ Volver a las mesas</button>
      <h2>{{ selected.service.tableId }} · {{ selected.service.pax }} personas · {{ diningLabel(selected.service.state) }}</h2>
      <section v-if="selected.service.restrictions?.length" class="restrictions" data-testid="restrictions">
        <h3>Restricciones declaradas</h3>
        <ul><li v-for="restriction in selected.service.restrictions" :key="restriction.id">⚠ {{ restrictionLabel(restriction) }}</li></ul>
      </section>
      <section v-for="course in selected.service.courses" :key="course.id" class="course">
        <h3>{{ course.name }} — {{ courseLabel(course.state) }}</h3>
        <p v-if="course.skipReason">Motivo: {{ course.skipReason }}</p>
        <ul>
          <li v-for="preparation in course.preparations" :key="preparation.id">
            <strong>{{ preparation.name }}</strong><span v-if="preparation.guestPosition !== null"> · comensal {{ preparation.guestPosition }}</span>
            · estacion {{ preparation.stationId }} · {{ preparationLabel(preparation.state) }}
            <span v-for="restriction in preparation.restrictions ?? []" :key="restriction.id" class="line alert">⚠ {{ restrictionLabel(restriction) }}</span>
            <span v-if="reviewLabel(preparation)" class="line" :class="{ alert: preparation.reviewPending }">{{ reviewLabel(preparation) }}</span>
          </li>
        </ul>
      </section>
    </article>
  </section>
</template>
