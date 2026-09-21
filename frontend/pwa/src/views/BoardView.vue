<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { call, getCatalog, type CatalogItem, type Session } from '../api'
import { CommandRunner, tabLock, type Outcome, type RunnerState } from '../commands'
import { ageLabel, boardSummary, courseLabel, diningLabel, pendingReviews, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { LINK_TEXT, LiveBoard, type LiveState } from '../live'
import { MoneyLeakError } from '../money-guard'
import { indexedDbPendingStore } from '../pending'
import { connectHub } from '../realtime'
import type { BoardEntry } from '../types'
import ServiceActions from './ServiceActions.vue'

// Comandero. Lectura en vivo (D6.2) + acciones con ORDEN INCIERTA durable (D6.3). Sin datos economicos.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

interface TableChoice { id: string; name: string; capacity: number }
interface MenuChoice { id: string; name: string }

const state = reactive<LiveState>({ entries: [], readAt: null, link: 'connecting', error: '' })
const runnerState = reactive<RunnerState>({ pending: null, blocked: null, busy: false, lastFailure: null, notice: '' })
const runner = new CommandRunner(runnerState, {
  fetcher: fetch, store: indexedDbPendingStore, token: () => props.token, lock: tabLock,
  identity: { installationId: props.session.installationId, actor: props.session.actor },
})
let lastAutoRetry = 0
const live = new LiveBoard(state, {
  load: () => call<BoardEntry[]>(fetch, '/board', { token: props.token }),
  connect: () => connectHub(fetch, props.token),
  onUnauthorized: () => emit('unauthorized'),
  // El servidor vuelve a responder y la ultima tentativa fallo por RED: se reintenta la MISMA orden (misma clave,
  // mismos bytes; el servidor no duplica). Solo en primer plano: no hay Background Sync.
  onRead: () => {
    if (runnerState.pending && !runnerState.busy && runnerState.lastFailure === 'network' && Date.now() - lastAutoRetry > 5000) {
      lastAutoRetry = Date.now(); void resolve(runner.retry())
    }
  },
})

const selectedId = ref<string | null>(null)
const selected = computed(() => state.entries.find(entry => entry.service.id === selectedId.value) ?? null)
const tables = ref<TableChoice[]>([]); const menus = ref<MenuChoice[]>([]); const catalog = ref<CatalogItem[]>([])
const freeTables = computed(() => tables.value.filter(table => !state.entries.some(entry => entry.occupancy.tableId === table.id && entry.occupancy.state === 'Occupied')))
const openTable = ref(''); const openMenu = ref(''); const openPax = ref(2)
const can = (action: string) => props.session.actions.includes(action)
const locked = computed(() => runnerState.busy || runnerState.pending !== null || runnerState.blocked !== null)

const now = ref(Date.now())
let clock: number | undefined
const age = computed(() => state.readAt ? Math.max(0, Math.round((now.value - state.readAt.getTime()) / 1000)) : null)
const stale = computed(() => age.value === null || age.value > 45)

async function resolve(work: Promise<Outcome | null>): Promise<void> {
  try {
    const outcome = await work
    if (!outcome) return
    if (outcome.kind === 'unauthorized') { emit('unauthorized'); return }
    if (outcome.kind === 'confirmed') runnerState.notice = ''
    if (outcome.kind !== 'unconfirmed') await live.refresh()
  } catch (error) {
    // La orden se aplico (se cerro con su eco) pero la respuesta traia datos economicos: no se pinta, se avisa y se relee.
    if (error instanceof MoneyLeakError) { runnerState.notice = `La orden SI se aplico. ${error.message}`; await live.refresh() }
    else if (!runnerState.notice) runnerState.notice = 'No se pudo enviar la orden.'
  }
}
const run = (path: string, body: Record<string, unknown>, description: string) => resolve(runner.send(path, body, description))
function open(): void {
  if (!openTable.value || !openMenu.value || !Number.isInteger(openPax.value) || openPax.value < 1 || openPax.value > 40) { runnerState.notice = 'Elige mesa, menu y comensales (1 a 40).'; return }
  void run('/services', { tableId: openTable.value, pax: openPax.value, menuId: openMenu.value }, `Abrir mesa ${openTable.value} para ${openPax.value}`)
  openTable.value = ''
}
async function reconcile(): Promise<void> { if (await runner.reconcile() === 'applied') await live.refresh() }

const online = () => { void live.refresh() }
const visible = () => { if (document.visibilityState === 'visible') { void runner.restore(); void live.refresh() } }   // otra pestana, o el movil vuelve del bloqueo

onMounted(async () => {
  clock = window.setInterval(() => { now.value = Date.now() }, 1000)
  window.addEventListener('online', online); document.addEventListener('visibilitychange', visible)
  await runner.restore()                          // ANTES de nada: una orden sin confirmar de una sesion anterior manda
  void live.start()
  try {
    const configuration = await call<{ tables: TableChoice[]; menus: MenuChoice[] }>(fetch, '/configuration', { token: props.token })
    tables.value = configuration.tables; menus.value = configuration.menus
    if (menus.value.length === 1) openMenu.value = menus.value[0].id
    if (can('add-consumption')) catalog.value = await getCatalog(fetch, props.token)
  } catch { /* sin configuracion no hay formularios; la lectura del tablero ya informa del problema */ }
})
onUnmounted(() => { window.clearInterval(clock); window.removeEventListener('online', online); document.removeEventListener('visibilitychange', visible); void live.stop() })
</script>

<template>
  <section data-testid="board">
    <p class="link" :class="{ warn: state.link !== 'live' }" role="status" data-testid="link">{{ LINK_TEXT[state.link] }}</p>
    <p :class="{ stale }" data-testid="age">Ultima lectura del servidor: {{ ageLabel(age) }}<strong v-if="stale"> · DATOS ANTIGUOS</strong></p>
    <p v-if="state.error" class="notice" role="alert" data-testid="board-error">{{ state.error }}</p>

    <!-- La orden sin confirmar manda sobre todo lo demas: inequivoca, persistente y bloqueante. -->
    <section v-if="runnerState.pending" class="pending" role="alert" data-testid="pending">
      <h2>ORDEN SIN CONFIRMAR</h2>
      <p><strong>{{ runnerState.pending.description }}</strong></p>
      <p>No sabemos si el servidor la recibio. Esta guardada en este dispositivo con su identificador: <strong>no la repitas por otro medio</strong>. Reintentarla es seguro: el servidor no la aplicara dos veces.</p>
      <button type="button" :disabled="runnerState.busy" data-testid="retry" @click="resolve(runner.retry())">Reintentar la misma orden</button>
      <button type="button" class="secondary" :disabled="runnerState.busy" data-testid="reconcile" @click="reconcile">Preguntar al servidor si la aplico</button>
    </section>
    <section v-else-if="runnerState.blocked" class="pending" role="alert" data-testid="blocked">
      <h2>{{ runnerState.blocked.reason === 'foreign' ? 'ORDEN DE OTRA INSTALACION' : 'ORDEN ANTERIOR ILEGIBLE' }}</h2>
      <p>Hay una orden guardada que no se puede reenviar. No se admiten ordenes nuevas hasta decidir que hacer con ella.</p>
      <button v-if="runnerState.blocked.key" type="button" :disabled="runnerState.busy" @click="reconcile">Preguntar al servidor si la aplico</button>
      <button type="button" class="secondary" :disabled="runnerState.busy" data-testid="discard" @click="runner.discardBlocked()">Descartarla</button>
    </section>
    <p v-if="runnerState.notice" class="notice" role="status" data-testid="command-notice">{{ runnerState.notice }}</p>

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
      <details v-if="can('open') && menus.length > 0" data-testid="open-form">
        <summary>Abrir mesa</summary>
        <label>Mesa
          <select v-model="openTable" name="table" :disabled="locked"><option value="" disabled>Elige…</option><option v-for="table in freeTables" :key="table.id" :value="table.id">{{ table.name }}</option></select>
        </label>
        <label>Comensales <input v-model.number="openPax" name="pax" type="number" min="1" max="40" inputmode="numeric" :disabled="locked" /></label>
        <label>Menu
          <select v-model="openMenu" name="menu" :disabled="locked"><option value="" disabled>Elige…</option><option v-for="menu in menus" :key="menu.id" :value="menu.id">{{ menu.name }}</option></select>
        </label>
        <button type="button" :disabled="locked" data-testid="do-open" @click="open">Abrir mesa</button>
      </details>
    </template>

    <article v-else data-testid="detail">
      <button type="button" class="secondary" @click="selectedId = null">‹ Volver a las mesas</button>
      <h2>{{ selected.service.tableId }} · {{ selected.service.pax }} personas · {{ diningLabel(selected.service.state) }}</h2>
      <ServiceActions :entry="selected" :catalog="catalog" :can-consume="can('add-consumption')" :disabled="locked" @run="run" />
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
