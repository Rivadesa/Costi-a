<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { call, getCatalog, type CatalogItem, type Session } from '../api'
import { boardSummary, courseLabel, diningLabel, pendingReviews, preparationLabel, restrictionLabel, reviewLabel } from '../labels'
import { useOperations } from '../operations'
import ServiceActions from './ServiceActions.vue'
import StatusPanel from './StatusPanel.vue'

// Comandero. Lectura en vivo (D6.2) + acciones con ORDEN INCIERTA durable (D6.3). Sin datos economicos.
// La lectura en vivo y la orden incierta son las de operations.ts, compartidas con la pantalla de cocina (D6.4).
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

interface TableChoice { id: string; name: string; capacity: number }
interface MenuChoice { id: string; name: string }

const { state, runnerState, live, locked, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))

const selectedId = ref<string | null>(null)
const selected = computed(() => state.entries.find(entry => entry.service.id === selectedId.value) ?? null)
const tables = ref<TableChoice[]>([]); const menus = ref<MenuChoice[]>([]); const catalog = ref<CatalogItem[]>([])
const freeTables = computed(() => tables.value.filter(table => !state.entries.some(entry => entry.occupancy.tableId === table.id && entry.occupancy.state === 'Occupied')))
const openTable = ref(''); const openMenu = ref(''); const openPax = ref(2)
const can = (action: string) => props.session.actions.includes(action)

function open(): void {
  if (!openTable.value || !openMenu.value || !Number.isInteger(openPax.value) || openPax.value < 1 || openPax.value > 40) { runnerState.notice = 'Elige mesa, menu y comensales (1 a 40).'; return }
  void run('/dining/services', { tableId: openTable.value, pax: openPax.value, menuId: openMenu.value }, `Abrir mesa ${openTable.value} para ${openPax.value}`)
  openTable.value = ''
}

onMounted(async () => {
  try {
    const configuration = await call<{ tables: TableChoice[]; menus: MenuChoice[] }>(fetch, '/configuration', { token: props.token })
    tables.value = configuration.tables; menus.value = configuration.menus
    if (menus.value.length === 1) openMenu.value = menus.value[0].id
    if (can('add-consumption')) catalog.value = await getCatalog(fetch, props.token)
  } catch { /* sin configuracion no hay formularios; la lectura del tablero ya informa del problema */ }
})
</script>

<template>
  <section data-testid="board">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="live.refresh()" />

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
