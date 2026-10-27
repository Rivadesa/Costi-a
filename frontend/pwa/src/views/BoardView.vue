<script setup lang="ts">
import { computed, nextTick, onUnmounted, provide, reactive, ref, watch } from 'vue'
import type { Session } from '../api'
import { settlementOf } from '../bodega'
import { reconnections, useCatalog } from '../catalog'
import { floorEmpty, floorRooms, floorSummary, floorView, Names, useConfiguration, type FloorTile } from '../configuration'
import { drinkProducts } from '../drinks'
import type { CommandRequest } from '../ficha'
import { ALL_TITLE, freeSections, MINE_TITLE, mineSections, mineSummary } from '../mesas'
import { useOperations } from '../operations'
import { PhotoCache, PHOTOS } from '../photos'
import type { Settlement } from '../round'
import { buzz, mineEntries, ReadyWatch, serveRequest, type ReadyNotice, type Who } from '../sala'
import FloorGrid from './FloorGrid.vue'
import Icon from './Icon.vue'
import MineList from './MineList.vue'
import OpenTable from './OpenTable.vue'
import ReadyBanner from './ReadyBanner.vue'
import StatusPanel from './StatusPanel.vue'
import TableCard from './TableCard.vue'

// Comandero. Lectura en vivo (D6.2) + acciones con ORDEN INCIERTA durable (D6.3). Sin datos economicos.
// La lectura en vivo y la orden incierta son las de operations.ts, compartidas con la pantalla de cocina (D6.4).
// C3a: el plano por salas (FloorGrid), la ficha de la mesa (TableCard) y la hoja «Abrir mesa» (OpenTable). La configuracion
// (mesas, salas, ofertas y estaciones) se relee al abrir esa hoja y cada 5 minutos; nombres siempre, nunca codigos.
// C3c: Costina Sala. Encima de todo eso, debajo de la cabecera, la banda «listo para servir» (ReadyBanner, logica en sala.ts).
// C3e (plan 2026-10-27, paso 3): el plano tiene dos pestanas, «Mis mesas» (MineList, la primera) y «Todas» (FloorGrid, el de hoy), con
// la MISMA tarjeta (TableTile); la banda avisa solo de «Mis mesas». «Abrir mesa» manda una sola orden con sus alergias (abrir.ts).
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
// C3e: «Mis mesas» o «Todas», tambien aqui y EN MEMORIA, como la sala: ir a una ficha y volver la conserva, y al abrir la aplicacion sale
// «Mis mesas». Es presentacion por dispositivo y estacion (ADR-018), nunca permiso: en «Todas» se ve todo y se hace lo que se anuncie.
const scope = ref<'mine' | 'all'>('mine')
// El segmento sigue el patron de pestanas (tablist): solo la elegida entra con el tabulador y las flechas cambian de pestana y llevan el
// foco a la nueva. Inicio y Fin, a la primera y a la ultima.
const SCOPES = ['mine', 'all'] as const
const tabMine = ref<HTMLButtonElement | null>(null)
const tabAll = ref<HTMLButtonElement | null>(null)
function scopeKey(event: KeyboardEvent): void {
  const index = SCOPES.indexOf(scope.value)
  const next = event.key === 'ArrowRight' ? (index + 1) % SCOPES.length : event.key === 'ArrowLeft' ? (index + SCOPES.length - 1) % SCOPES.length
    : event.key === 'Home' ? 0 : event.key === 'End' ? SCOPES.length - 1 : -1
  if (next < 0) return
  event.preventDefault()
  scope.value = SCOPES[next]
  void nextTick(() => (SCOPES[next] === 'mine' ? tabMine : tabAll).value?.focus())
}
// Quien es este movil para «Mis mesas»: el actor y la estacion TAL CUAL los da /session (sala.ts, isMine), nunca rehechos aqui.
const who = computed<Who>(() => ({ actor: props.session.actor, station: props.session.station }))
const mine = computed(() => mineSections(rooms.value, who.value))
const free = computed(() => freeSections(rooms.value))
const mineText = computed(() => mineSummary(mine.value))

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
// C3c: el aviso de «listo para servir» sale de cada lectura CORRECTA (nunca del aviso del canal) y vibra solo con una clave nueva,
// con la pagina a la vista (sala.ts). La primera lectura no vibra. Sin sonido: en la sala no suena nada.
// C3e: solo de «Mis mesas» (mineEntries), en las dos pestanas. En «Todas» las demas mesas se pintan en verde, sin banda ni vibracion.
const ready = reactive(new ReadyWatch()) as ReadyWatch
watch(() => state.readAt, () => {
  const fresh = ready.read(mineEntries(state.entries, who.value))
  buzz(fresh, document.visibilityState === 'visible', navigator)
})
const card = ref<InstanceType<typeof TableCard> | null>(null)
// C3e (revision; maqueta «6 · Sala · servido», «Al tocar «Ver mesa»»): con la ficha de una mesa a la vista, su boton grande ya es
// «Servido · Primero»; la banda no repite el aviso de ESA mesa (dos botones iguales y la ficha 230 px mas abajo). En Carta y Bodega de la
// misma mesa, donde el boton grande no se ve, sigue saliendo; y los avisos de las OTRAS mesas salen siempre (maqueta «5 · Sala · aviso»).
// Solo es lo que se pinta: el aviso sigue en ReadyWatch (vibra igual, «Visto» igual) y vuelve a salir al dejar la ficha si no se sirvio.
const fichaShown = computed(() => selected.value !== null && (card.value?.panel ?? 'ficha') === 'ficha')
const notices = computed(() => ready.shown.filter(notice => !(fichaShown.value && notice.entry.service.id === selectedId.value)))
// «Servido» desde la banda: la orden de servir ESE pase con la version leida de esa mesa (F01), una por toque.
function serve(notice: ReadyNotice): void {
  const request = serveRequest(notice, names.value.table(notice.entry.service.tableId))
  if (request) void run(request.path, request.body, request.description)
}
// «Ver mesa»: la ficha de esa mesa. Si ya esta abierta (en Carta o Bodega), vuelve a la ficha; si hay otra abierta, sale de ella como
// con «Mesas» (con una ronda sin enviar, pregunta).
function viewTable(notice: ReadyNotice): void {
  const serviceId = notice.entry.service.id
  openingTable.value = null
  if (selectedId.value === serviceId) { card.value?.show(); return }
  if (selectedId.value !== null && card.value) { card.value.leave(() => { selectedId.value = serviceId }); return }
  selectedId.value = serviceId
}

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

// C3e: la hoja arma UNA orden POST /dining/services con las personas, la oferta y sus alergias (abrir.ts, openRequest; una orden
// incierta por gesto, D6.3) y aqui sale por la orden incierta de siempre. Segun su resultado (bodega.ts, settlementOf, la misma regla
// que la ronda: sin resultado pero guardada como la pendiente es incierta, nunca «no enviada»):
//  - confirmada: la ficha de la mesa recien abierta (la relectura ya termino), como hoy;
//  - rechazada: la hoja se queda con sus filas y el aviso, para no escribir otra vez las alergias. Con offer_unavailable se relee
//    ademas la configuracion: la hoja vuelve a proponer entre las vigentes;
//  - sin confirmar: al plano, con la orden pendiente arriba, que ya lleva las filas;
//  - no salio (no se pudo guardar, u otra orden ocupaba el hueco): nada se aplico; la hoja se queda y el aviso dice por que.
// Si el camarero salio de la hoja mientras tanto, no se le mueve de donde esta.
// C3e (revision): con la hoja quedandose, el aviso de arriba (command-notice) no se ve si el camarero bajo a anotar alergias. El motivo
// del rechazo (o de que no saliera) va TAMBIEN a la barra de la hoja, junto a «Abrir» (openRejection), hasta el siguiente intento o hasta
// dejar la hoja. Con offer_unavailable la hoja ademas deja de proponer un menu: «Abrir» queda apagado hasta que el camarero elija otro.
const openRejection = ref<{ code: string; text: string } | null>(null)
watch(openingTable, () => { openRejection.value = null })
async function open(request: CommandRequest): Promise<void> {
  const tableId = openingTable.value
  if (!tableId) return
  openRejection.value = null
  const outcome = await run(request.path, request.body, request.description)
  const settled = settlementOf(outcome, runnerState.pending, { path: request.path, body: JSON.stringify(request.body) })
  const here = openingTable.value === tableId
  if (settled.kind === 'rejected' || settled.kind === 'not_sent') {
    if (settled.kind === 'rejected' && settled.code === 'offer_unavailable') void reload()
    if (here) openRejection.value = { code: settled.kind === 'rejected' ? settled.code : 'not_sent', text: runnerState.notice || 'La orden no se ha enviado: la mesa no se ha abierto.' }
    return
  }
  if (here) openingTable.value = null
  const serviceId = outcome?.kind === 'confirmed' ? (outcome.response as { serviceId?: unknown } | null)?.serviceId : undefined
  if (typeof serviceId === 'string' && (here || (openingTable.value === null && selectedId.value === null))) selectedId.value = serviceId
}
</script>

<template>
  <section class="screen" data-testid="board">
    <StatusPanel :state="state" :runner-state="runnerState" :age="age" :stale="stale" @retry="retry" @reconcile="reconcile" @discard="discard" @refresh="refreshAll" />
    <ReadyBanner :notices="notices" :names="names" :locked="locked" @serve="serve" @view="viewTable" @seen="notice => ready.dismiss(notice.key)" />

    <TableCard v-if="selected" ref="card" :key="selected.service.id" :entry="selected" :table-name="names.table(selected.service.tableId)" :room-name="selectedTile?.roomName ?? ''"
      :configuration="configuration" :names="names" :products="products" :catalog-status="catalogStatus" :can-consume="canRound" :locked="locked"
      :uncertain="runnerState.pending !== null || runnerState.blocked !== null" :now="now"
      :send-round="sendRound" @run="run" @back="selectedId = null" @bodega="refreshCatalog" />

    <OpenTable v-else-if="openingTable" :key="openingTable" :table-id="openingTable" :table-name="names.table(openingTable)" :room-name="openingTile?.roomName ?? ''"
      :capacity="tableConfig?.capacity ?? 0" :offers="configuration.offers" :occupied="openingTile?.entry != null" :locked="locked" :rejection="openRejection"
      @open="open" @back="openingTable = null" />

    <template v-else>
      <!-- El resumen va SIEMPRE en su propia linea (aunque este vacio): las dos pestanas tienen la misma cabecera y el segmento no se mueve
           bajo el dedo al cambiar de una a otra. -->
      <div class="floor-h board-h">
        <h2>{{ scope === 'mine' ? MINE_TITLE : ALL_TITLE }}</h2>
        <p class="muted board-h-sum">
          <span v-if="view === 'ready' && scope === 'mine' && mineText" data-testid="mine-summary">{{ mineText }}</span>
          <span v-else-if="view === 'ready' && scope === 'all' && summary" data-testid="floor-summary">{{ summary }}</span>
        </p>
      </div>
      <div class="board-scope" role="tablist" aria-label="Qué mesas ver" @keydown="scopeKey">
        <button id="board-tab-mine" ref="tabMine" type="button" role="tab":aria-selected="scope === 'mine'" aria-controls="board-panel"
          :tabindex="scope === 'mine' ? 0 : -1" data-testid="board-scope-mine" @click="scope = 'mine'">Mis mesas</button>
        <button id="board-tab-all" ref="tabAll" type="button" role="tab":aria-selected="scope === 'all'" aria-controls="board-panel"
          :tabindex="scope === 'all' ? 0 : -1" data-testid="board-scope-all" @click="scope = 'all'">Todas</button>
      </div>
      <div id="board-panel" role="tabpanel" :aria-labelledby="scope === 'mine' ? 'board-tab-mine' : 'board-tab-all'">
        <p v-if="view === 'reading'" class="muted" data-testid="floor-reading">Leyendo las mesas…</p>
        <p v-else-if="view === 'unread'" class="notice neutral" data-testid="floor-unread"><Icon name="info" />Sin ninguna lectura del servidor todavía: las mesas salen en cuanto responda.</p>
        <template v-else>
          <p v-if="empty" class="notice neutral" data-testid="empty"><Icon name="info" />No hay mesas activas. Se crean en el PC principal, en Salas, mesas y estaciones.</p>
          <p v-if="configuration.failed && configuration.tables.length === 0" class="notice" role="status" data-testid="configuration-error">
            <Icon name="info" />No se pudo leer la configuración de mesas: solo se ven las mesas ocupadas, y sin «Abrir mesa». Se vuelve a intentar en la próxima lectura.</p>
          <MineList v-if="scope === 'mine' && !empty" :mine="mine" :free="free" :station-name="session.stationName" :configuration="configuration"
            :now="now" :can-open="can('open')" @pick="pick" />
          <FloorGrid v-else-if="scope === 'all'" v-model:room="room" :rooms="rooms" :configuration="configuration" :now="now" :can-open="can('open')" @pick="pick" />
        </template>
      </div>
    </template>
  </section>
</template>
