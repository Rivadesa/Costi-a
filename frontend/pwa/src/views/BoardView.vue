<script setup lang="ts">
import { computed, onUnmounted, provide, ref, watch } from 'vue'
import type { Session } from '../api'
import { settlementOf } from '../bodega'
import { reconnections, useCatalog } from '../catalog'
import { floorEmpty, floorRooms, floorSummary, floorView, Names, useConfiguration, type FloorTile } from '../configuration'
import { drinkProducts } from '../drinks'
import { plural } from '../labels'
import { useOperations } from '../operations'
import { PhotoCache, PHOTOS } from '../photos'
import type { Settlement } from '../round'
import FloorGrid from './FloorGrid.vue'
import Icon from './Icon.vue'
import OpenTable from './OpenTable.vue'
import StatusPanel from './StatusPanel.vue'
import TableCard from './TableCard.vue'

// Comandero. Lectura en vivo (D6.2) + acciones con ORDEN INCIERTA durable (D6.3). Sin datos economicos.
// La lectura en vivo y la orden incierta son las de operations.ts, compartidas con la pantalla de cocina (D6.4).
// C3a: el plano por salas (FloorGrid), la ficha de la mesa (TableCard) y la hoja «Abrir mesa» (OpenTable). La configuracion
// (mesas, salas, ofertas y estaciones) se relee al abrir esa hoja y cada 5 minutos; nombres siempre, nunca codigos.
const props = defineProps<{ token: string; session: Session }>()
const emit = defineEmits<{ unauthorized: [] }>()

const { state, runnerState, live, locked, now, age, stale, run, retry, reconcile, discard } = useOperations(props, () => emit('unauthorized'))
const { configuration, reload } = useConfiguration(props, () => emit('unauthorized'))
const can = (action: string) => props.session.actions.includes(action)

// C3b: «Bodega y bebidas». La ronda es una accion de sesion que anuncia el servidor (add-consumptions): sin ella no hay Bebidas ni se
// lee el catalogo. El catalogo operativo (sin precios) se lee al montar y se revalida con ETag al abrir Bebidas, cada 5 minutos y tras
// reconectar (catalog.ts); las fotos, en memoria por huella y solo las de las filas visibles (photos.ts).
const canRound = computed(() => can('add-consumptions'))
const { read: catalogRead, status: catalogStatus, refresh: refreshCatalog } = useCatalog(props, () => canRound.value, () => emit('unauthorized'))
const products = computed(() => drinkProducts(catalogRead.value?.items ?? []))
const reconnected = reconnections()
watch(() => state.link, link => { if (reconnected(link)) void refreshCatalog() })
const photos = new PhotoCache({ fetcher: fetch, token: () => props.token, createUrl: blob => URL.createObjectURL(blob), revokeUrl: url => URL.revokeObjectURL(url) })
provide(PHOTOS, photos)
onUnmounted(() => photos.clear())
// La ronda sale por la orden incierta de siempre. Su resultado decide que pasa con el borrador (bodega.ts, settlementOf): si no hubo
// resultado pero ESTA orden quedo guardada como la pendiente, es incierta, nunca «no enviada» (no se manda dos veces).
async function sendRound(path: string, body: Record<string, unknown>, description: string): Promise<Settlement> {
  const outcome = await run(path, body, description)
  return settlementOf(outcome, runnerState.pending, { path, body: JSON.stringify(body) })
}

const names = computed(() => new Names(configuration))
const rooms = computed(() => floorRooms(configuration, state.entries))
const summary = computed(() => floorSummary(rooms.value))
const tiles = computed(() => rooms.value.flatMap(room => room.tiles))
// El plano, solo desde una lectura real del tablero (configuration.ts, floorView): nunca una mesa «Libre» sin haberlo leido.
const view = computed(() => floorView(state, configuration))
const empty = computed(() => floorEmpty(state, configuration, tiles.value.length))
// La sala elegida vive aqui y no en el plano: ir a una ficha (o a «Abrir mesa») y volver la conserva, como SelectedZone en el puesto.
const room = ref('')

// Lo que se ve: el plano, la ficha de una mesa ocupada (por su servicio: la lectura de siempre, F01) o «Abrir mesa» en una libre.
const selectedId = ref<string | null>(null)
const openingTable = ref<string | null>(null)
const selected = computed(() => state.entries.find(entry => entry.service.id === selectedId.value) ?? null)
const selectedTile = computed(() => tiles.value.find(tile => tile.entry?.service.id === selectedId.value) ?? null)
const openingTile = computed(() => openingTable.value === null ? null : tiles.value.find(tile => tile.tableId === openingTable.value && tile.entry === null)
  ?? tiles.value.find(tile => tile.tableId === openingTable.value) ?? null)
const tableConfig = computed(() => configuration.tables.find(table => table.id === openingTable.value) ?? null)
// Tras cada lectura correcta del tablero: una mesa que ya no esta (liberada, cerrada en Caja) devuelve al plano, y una configuracion
// que no se pudo leer se reintenta (el servidor vuelve a responder) sin esperar a los 5 minutos.
watch(() => state.readAt, () => {
  if (selectedId.value && !selected.value) selectedId.value = null
  if (configuration.failed) void reload()
})
// Cambiar de pantalla (plano, ficha, «Abrir mesa») empieza arriba, no donde se quedo el plano.
watch([selectedId, openingTable], () => { window.scrollTo({ top: 0 }) })
// «Actualizar» relee tambien la configuracion, como RefreshAll en el puesto.
function refreshAll(): void { void live.refresh(); void reload() }

function pick(tile: FloorTile): void {
  if (tile.entry) { openingTable.value = null; selectedId.value = tile.entry.service.id; return }
  if (!can('open')) return
  selectedId.value = null; openingTable.value = tile.tableId
  void reload()   // C3a: la configuracion, al dia al abrir la hoja (un menu cambiado por la manana)
}

async function open(pax: number, offerId: string): Promise<void> {
  const tableId = openingTable.value
  if (!tableId) return
  const name = names.value.table(tableId)
  // menuId: alias para un servidor anterior a E4a.
  const outcome = await run('/dining/services', { tableId, pax, offerId, menuId: offerId }, `Abrir ${name} para ${plural(pax, 'persona', 'personas')}`)
  openingTable.value = null
  // Confirmada: se pasa a la ficha de la mesa recien abierta (la relectura ya termino). Si no, el plano con el aviso o la orden pendiente.
  const serviceId = outcome?.kind === 'confirmed' ? (outcome.response as { serviceId?: unknown } | null)?.serviceId : undefined
  if (typeof serviceId === 'string') selectedId.value = serviceId
}
</script>

<template>
  <section class="screen" data-testid="board">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="refreshAll" />

    <TableCard v-if="selected" :key="selected.service.id" :entry="selected" :table-name="names.table(selected.service.tableId)" :room-name="selectedTile?.roomName ?? ''"
      :configuration="configuration" :names="names" :products="products" :catalog-status="catalogStatus" :can-consume="canRound" :locked="locked"
      :uncertain="runnerState.pending !== null || runnerState.blocked !== null" :now="now"
      :send-round="sendRound" @run="run" @back="selectedId = null" @bodega="refreshCatalog" />

    <OpenTable v-else-if="openingTable" :key="openingTable" :table-name="names.table(openingTable)" :room-name="openingTile?.roomName ?? ''"
      :capacity="tableConfig?.capacity ?? 0" :offers="configuration.offers" :occupied="openingTile?.entry != null" :locked="locked"
      @open="open" @back="openingTable = null" />

    <template v-else>
      <div class="floor-h">
        <h2>Mesas</h2>
        <span v-if="view === 'ready' && summary" class="muted" data-testid="floor-summary">{{ summary }}</span>
      </div>
      <p v-if="view === 'reading'" class="muted" data-testid="floor-reading">Leyendo las mesas…</p>
      <p v-else-if="view === 'unread'" class="notice neutral" data-testid="floor-unread"><Icon name="info" />Sin ninguna lectura del servidor todavía: las mesas salen en cuanto responda.</p>
      <template v-else>
        <p v-if="empty" class="notice neutral" data-testid="empty"><Icon name="info" />No hay mesas activas. Se crean en el PC principal, en Salas, mesas y estaciones.</p>
        <p v-if="configuration.failed && configuration.tables.length === 0" class="notice" role="status" data-testid="configuration-error">
          <Icon name="info" />No se pudo leer la configuración de mesas: solo se ven las mesas ocupadas, y sin «Abrir mesa». Se vuelve a intentar en la próxima lectura.</p>
        <FloorGrid v-model:room="room" :rooms="rooms" :configuration="configuration" :now="now" :can-open="can('open')" @pick="pick" />
      </template>
    </template>
  </section>
</template>
