<script setup lang="ts">
import { computed } from 'vue'
import type { Configuration, FloorTile } from '../configuration'
import { FREE_TITLE, freeLabel, MINE_EMPTY, mineHint, mineRoomLabel, type MesasSection } from '../mesas'
import Icon from './Icon.vue'
import TableTile from './TableTile.vue'

// C3e (plan 2026-10-27, paso 3; maqueta «1 · Sala · Mis mesas»): la pestana «Mis mesas» de Costina Sala, la primera al abrir. Solo PINTA
// lo que ordena mesas.ts (mineSections y freeSections, que BoardView calcula una vez y tambien resume en la cabecera): las ocupadas mias
// por la sala de cada mesa, con las listas para servir arriba, y al final TODAS las libres, por sala. Sin pestanas de sala. Es
// presentacion por dispositivo y estacion, nunca permiso ni atribucion a una persona (ADR-018): en «Todas» se ve y se hace lo demas.
// La tarjeta es TableTile, la misma de «Todas»; BoardView monta una de las dos pestanas (v-if), asi que cada tarjeta sale una vez.
const props = defineProps<{ mine: MesasSection[]; free: MesasSection[]; stationName: string | null | undefined; configuration: Configuration; now: number; canOpen: boolean }>()
const emit = defineEmits<{ pick: [tile: FloorTile] }>()

const hint = computed(() => mineHint(props.stationName))
// mineRoomLabel y freeLabel dan «Sala 1 · 6 abiertas» y «Mesas libres · 3 libres»: el nombre va en el titulo y el numero a su lado, como
// la maqueta. Las palabras siguen saliendo de mesas.ts; si su forma cambiara, se ve la etiqueta entera junto al nombre (nunca nada mal).
const after = (label: string, head: string) => label.startsWith(`${head} · `) ? label.slice(head.length + 3) : label
const roomCount = (section: MesasSection) => after(mineRoomLabel(section), section.name)
const freeCount = computed(() => after(freeLabel(props.free), FREE_TITLE))
const testId = (prefix: string, section: MesasSection) => `${prefix}-${section.id || 'none'}`
</script>

<template>
  <p class="board-hint" data-testid="mine-hint">{{ hint }}</p>
  <p v-if="mine.length === 0" class="notice neutral board-empty" data-testid="mine-empty"><Icon name="info" />{{ MINE_EMPTY }}</p>
  <section v-for="section in mine" :key="section.id" class="room board-room" :data-testid="testId('mine-room', section)">
    <div class="room-h"><h3>{{ section.name }}</h3><span class="muted sm">{{ roomCount(section) }}</span></div>
    <div class="tiles">
      <TableTile v-for="tile in section.tiles" :key="tile.key" :tile="tile" :configuration="configuration" :now="now" :can-open="canOpen"
        @pick="emit('pick', tile)" />
    </div>
  </section>
  <section v-if="free.length > 0" class="room board-room board-free" data-testid="mine-free">
    <div class="room-h"><h3>{{ FREE_TITLE }}</h3><span class="muted sm">{{ freeCount }}</span></div>
    <div v-for="section in free" :key="section.id" class="board-free-room" :data-testid="testId('free-room', section)">
      <h4 class="board-sub-h">{{ section.name }}</h4>
      <div class="tiles">
        <TableTile v-for="tile in section.tiles" :key="tile.key" :tile="tile" :configuration="configuration" :now="now" :can-open="canOpen"
          @pick="emit('pick', tile)" />
      </div>
    </div>
  </section>
</template>
