<script setup lang="ts">
import { computed } from 'vue'
import { offerName, type Configuration, type FloorTile } from '../configuration'
import { alert, alertIsAlarming, alertWord, courseStep, courseSteps, isTileCode, PENDING_REVIEW, READY, tileElapsed, tileLabel, tileProgress, tileState } from '../floor'
import { plural } from '../labels'
import Icon from './Icon.vue'

// C3e (plan 2026-10-27, paso 3): la tarjeta de una mesa, sacada de FloorGrid (C3a/C3c) para que «Mis mesas» (MineList) y «Todas»
// (FloorGrid) pinten LA MISMA: table-<id>, tile-state, tile-alert, el verde de «Listo para servir» y el borde rojo que manda sobre el.
// Todo es presentacion de lo leido (floor.ts, con su tabla de casos comun). Tocarla SOLO elige la mesa: una ocupada abre su ficha (la
// lectura de siempre, F01) y una libre la hoja de «Abrir mesa»; ninguna construye una orden.
//  - Una libre lleva «Abrir mesa» solo si el servidor anuncia open (canOpen); sin el se ve, pero no se pulsa.
//  - Una lista para servir dice que pase esta listo (maqueta «1 · Sala · Mis mesas»: «Principal» bajo la pastilla), con la regla de
//    floor.ts (courseStep), la misma que pinta el verde del avance.
// Quien la pinta la pinta UNA sola vez (v-if, nunca v-show): un getByTestId de las E2E no admite dos table-<id>.
const props = defineProps<{ tile: FloorTile; configuration: Configuration; now: number; canOpen: boolean }>()
const emit = defineEmits<{ pick: [tile: FloorTile] }>()

const service = computed(() => props.tile.entry?.service ?? null)
const state = computed(() => tileState(service.value))
const free = computed(() => props.tile.entry === null)
const tone = computed(() => isTileCode(state.value) ? `t-${state.value}` : 't-other')
// Libre: sus plazas; ocupada: sus personas y el menu o la carta con que se abrio (si sigue entre los vigentes).
const sub = computed(() => {
  const current = service.value
  if (!current) return props.tile.capacity > 0 ? plural(props.tile.capacity, 'plaza', 'plazas') : ''
  return [plural(current.pax, 'persona', 'personas'), offerName(props.configuration, current.offerId)].filter(part => part).join(' · ')
})
const ready = computed(() => state.value === READY)
const readyCourses = computed(() => ready.value ? (service.value?.courses ?? []).filter(course => courseStep(course) === 'ready').map(course => course.name).join(' · ') : '')
const elapsed = computed(() => tileElapsed(service.value, props.now))
const steps = computed(() => courseSteps(service.value))
const progress = computed(() => tileProgress(service.value))
const warning = computed(() => alert(service.value))
const alarming = computed(() => alertIsAlarming(service.value))
// El aviso, partido por su separador: en un movil corta en «Alergia ·» / «por revisar», nunca en medio de «por revisar».
const pendingReview = computed(() => service.value?.restrictionsPendingAck === true)
// Una mesa libre sin «abrir» anunciado para este dispositivo no lleva a ninguna parte: se ve, pero no se pulsa.
const openable = computed(() => free.value && props.canOpen)
const idle = computed(() => free.value && !props.canOpen)
const describe = computed(() => [props.tile.name, tileLabel(state.value), sub.value, readyCourses.value, warning.value,
  progress.value ? `${progress.value} pases` : '', openable.value ? 'Abrir mesa' : ''].filter(part => part).join(', '))
</script>

<template>
  <button type="button" class="tile" :class="{ free, ready, alarm: alarming }" :disabled="idle" :aria-label="describe"
    :data-testid="`table-${tile.tableId}`" @click="emit('pick', tile)">
    <span class="tile-h">
      <span class="tile-name">{{ tile.name }}</span>
      <span v-if="elapsed" class="tile-time"><Icon name="clock" :size="15" />{{ elapsed }}</span>
    </span>
    <!-- C3e (revision; maqueta «1 · Sala · Mis mesas»): una libre dice su estado en el subtitulo, «Libre · 4 plazas», sin pastilla (bajo
         «Mesas libres» la pastilla repetia lo mismo y alargaba cada tarjeta). tile-state sigue siendo la palabra del estado. -->
    <span v-if="free" class="tile-sub"><span data-testid="tile-state">{{ tileLabel(state) }}</span><template v-if="sub"> · {{ sub }}</template></span>
    <template v-else>
      <span v-if="sub" class="tile-sub">{{ sub }}</span>
      <span class="pill" :class="tone" data-testid="tile-state">{{ tileLabel(state) }}</span>
    </template>
    <span v-if="readyCourses" class="board-tile-what">{{ readyCourses }}</span>
    <span v-if="steps.length > 0" class="steps" aria-hidden="true">
      <span v-for="(step, index) in steps" :key="index" class="step" :class="`st-${step}`"></span>
      <span class="steps-n">{{ progress }}</span>
    </span>
    <span v-if="warning" class="badge" :class="{ alarm: alarming }" data-testid="tile-alert">
      <Icon name="alert" :size="15" :stroke="2.2" /><span>{{ alertWord(service) }}<template v-if="pendingReview"> · <span class="nw">{{ PENDING_REVIEW }}</span></template></span>
    </span>
    <span v-if="openable" class="board-tile-open"><Icon name="plus" :size="18" :stroke="2.4" />Abrir mesa</span>
  </button>
</template>
