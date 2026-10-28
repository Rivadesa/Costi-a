<script setup lang="ts">
import { computed, watch } from 'vue'
import { roomSummary, type Configuration, type FloorRoom, type FloorTile } from '../configuration'
import TableTile from './TableTile.vue'

// C3a: plano del comandero, una rejilla de tarjetas POR SALA (tambien las mesas libres), como el Comedor del puesto. Todo lo que pinta
// es presentacion de lo leido (floor.ts, la copia de FloorPlan.cs con su tabla de casos comun). Tocar una tarjeta SOLO elige la mesa:
// una ocupada abre su ficha (la lectura de siempre, F01) y una libre la hoja de «Abrir mesa»; ninguna construye una orden.
// room: la sala elegida ('' = todas). La guarda BoardView (v-model:room) para que sobreviva a la ida y vuelta a una ficha.
// C3c: una mesa «Listo para servir» se pinta entera en verde (.tile.ready), con el MISMO estado de la tarjeta (floor.ts, sin tocarlo
// ni su tabla de casos); con una alergia que alarma, el borde rojo manda sobre el verde.
// C3e (plan 2026-10-27, paso 3): es la pestana «Todas» de Costina Sala, con sus pestanas de sala. La tarjeta es TableTile, la misma que
// pinta «Mis mesas» (MineList); BoardView monta una de las dos (v-if), asi que cada tarjeta sale una sola vez.
const props = defineProps<{ rooms: FloorRoom[]; configuration: Configuration; now: number; canOpen: boolean; room: string }>()
const emit = defineEmits<{ pick: [tile: FloorTile]; 'update:room': [room: string] }>()

// Pestanas de sala solo con dos salas o mas. Una sala que desaparece (o que ya no estaba al volver) devuelve a «Todas las salas».
const room = computed(() => props.room !== '' && props.rooms.length > 1 && props.rooms.some(current => current.id === props.room) ? props.room : '')
watch(room, current => { if (current !== props.room) emit('update:room', current) }, { immediate: true })
const tabs = computed(() => props.rooms.length < 2 ? [] : [
  { id: '', label: `Todas las salas · ${props.rooms.reduce((total, current) => total + current.tiles.length, 0)}` },
  ...props.rooms.map(current => ({ id: current.id, label: `${current.name} · ${current.tiles.length}` })),
])
const shown = computed(() => props.rooms.filter(current => room.value === '' || current.id === room.value))
</script>

<template>
  <nav v-if="tabs.length > 0" class="tabs" aria-label="Salas">
    <button v-for="tab in tabs" :key="tab.id" type="button" class="tab" :aria-pressed="room === tab.id" :data-testid="`room-tab-${tab.id || 'all'}`"
      @click="emit('update:room', tab.id)">{{ tab.label }}</button>
  </nav>
  <section v-for="current in shown" :key="current.id" class="room" :data-testid="`room-${current.id || 'none'}`">
    <div class="room-h"><h3>{{ current.name }}</h3><span class="muted sm">{{ roomSummary(current) }}</span></div>
    <div class="tiles">
      <TableTile v-for="tile in current.tiles" :key="tile.key" :tile="tile" :configuration="configuration" :now="now" :can-open="canOpen"
        @pick="emit('pick', tile)" />
    </div>
  </section>
</template>
