<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { offerName, roomSummary, type Configuration, type FloorRoom, type FloorTile } from '../configuration'
import { alert, alertIsAlarming, courseSteps, isTileCode, tileElapsed, tileLabel, tileProgress, tileState } from '../floor'
import { plural } from '../labels'
import Icon from './Icon.vue'

// C3a: plano del comandero, una rejilla de tarjetas POR SALA (tambien las mesas libres), como el Comedor del puesto. Todo lo que pinta
// es presentacion de lo leido (floor.ts, la copia de FloorPlan.cs con su tabla de casos comun). Tocar una tarjeta SOLO elige la mesa:
// una ocupada abre su ficha (la lectura de siempre, F01) y una libre la hoja de «Abrir mesa»; ninguna construye una orden.
const props = defineProps<{ rooms: FloorRoom[]; configuration: Configuration; now: number; canOpen: boolean }>()
const emit = defineEmits<{ pick: [tile: FloorTile] }>()

// Pestanas de sala solo con dos salas o mas. Una sala que desaparece devuelve a «Todas las salas».
const room = ref('')
const tabs = computed(() => props.rooms.length < 2 ? [] : [
  { id: '', label: `Todas las salas · ${props.rooms.reduce((total, current) => total + current.tiles.length, 0)}` },
  ...props.rooms.map(current => ({ id: current.id, label: `${current.name} · ${current.tiles.length}` })),
])
watch(() => props.rooms.map(current => current.id).join('|'), () => { if (room.value && !props.rooms.some(current => current.id === room.value)) room.value = '' })
const shown = computed(() => props.rooms.filter(current => room.value === '' || current.id === room.value))

const service = (tile: FloorTile) => tile.entry?.service ?? null
const tone = (tile: FloorTile) => { const state = tileState(service(tile)); return isTileCode(state) ? `t-${state}` : 't-other' }
// Libre: sus plazas; ocupada: sus personas y el menu o la carta con que se abrio (si sigue entre los vigentes).
function sub(tile: FloorTile): string {
  const current = service(tile)
  if (!current) return tile.capacity > 0 ? plural(tile.capacity, 'plaza', 'plazas') : ''
  const offer = offerName(props.configuration, current.offerId)
  return [plural(current.pax, 'persona', 'personas'), offer].filter(part => part).join(' · ')
}
// Una mesa libre sin «abrir» anunciado para este dispositivo no lleva a ninguna parte: se ve, pero no se pulsa.
const idle = (tile: FloorTile) => tile.entry === null && !props.canOpen
const describe = (tile: FloorTile) => [tile.name, tileLabel(tileState(service(tile))), sub(tile), alert(service(tile)),
  tileProgress(service(tile)) ? `${tileProgress(service(tile))} pases` : ''].filter(part => part).join(', ')
</script>

<template>
  <nav v-if="tabs.length > 0" class="tabs" aria-label="Salas">
    <button v-for="tab in tabs" :key="tab.id" type="button" class="tab" :aria-pressed="room === tab.id" :data-testid="`room-tab-${tab.id || 'all'}`"
      @click="room = tab.id">{{ tab.label }}</button>
  </nav>
  <section v-for="current in shown" :key="current.id" class="room" :data-testid="`room-${current.id || 'none'}`">
    <div class="room-h"><h3>{{ current.name }}</h3><span class="muted sm">{{ roomSummary(current) }}</span></div>
    <div class="tiles">
      <button v-for="tile in current.tiles" :key="tile.key" type="button" class="tile"
        :class="{ free: tile.entry === null, alarm: alertIsAlarming(service(tile)) }" :disabled="idle(tile)" :aria-label="describe(tile)"
        :data-testid="`table-${tile.tableId}`" @click="emit('pick', tile)">
        <span class="tile-h">
          <span class="tile-name">{{ tile.name }}</span>
          <span v-if="tileElapsed(service(tile), now)" class="tile-time"><Icon name="clock" :size="15" />{{ tileElapsed(service(tile), now) }}</span>
        </span>
        <span v-if="sub(tile)" class="tile-sub">{{ sub(tile) }}</span>
        <span class="pill" :class="tone(tile)" data-testid="tile-state">{{ tileLabel(tileState(service(tile))) }}</span>
        <span v-if="courseSteps(service(tile)).length > 0" class="steps" aria-hidden="true">
          <span v-for="(step, index) in courseSteps(service(tile))" :key="index" class="step" :class="`st-${step}`"></span>
          <span class="steps-n">{{ tileProgress(service(tile)) }}</span>
        </span>
        <span v-if="alert(service(tile))" class="badge" :class="{ alarm: alertIsAlarming(service(tile)) }" data-testid="tile-alert">
          <Icon name="alert" :size="15" :stroke="2.2" />{{ alert(service(tile)) }}
        </span>
      </button>
    </div>
  </section>
</template>
