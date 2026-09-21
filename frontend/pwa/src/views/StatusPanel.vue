<script setup lang="ts">
import type { RunnerState } from '../commands'
import { ageLabel } from '../labels'
import { LINK_TEXT, type LiveState } from '../live'

// Cabecera comun de comandero y cocina: estado del tiempo real, antiguedad de la ultima lectura (siempre a la
// vista) y la ORDEN SIN CONFIRMAR, que manda sobre todo lo demas: inequivoca, persistente y bloqueante.
defineProps<{ state: LiveState; runnerState: RunnerState; age: number | null; stale: boolean }>()
defineEmits<{ retry: []; reconcile: []; discard: []; refresh: [] }>()
</script>

<template>
  <p class="link" :class="{ warn: state.link !== 'live' }" role="status" data-testid="link">{{ LINK_TEXT[state.link] }}</p>
  <p :class="{ stale }" data-testid="age">Ultima lectura del servidor: {{ ageLabel(age) }}<strong v-if="stale"> · DATOS ANTIGUOS</strong></p>
  <p v-if="state.error" class="notice" role="alert" data-testid="board-error">{{ state.error }}</p>

  <section v-if="runnerState.pending" class="pending" role="alert" data-testid="pending">
    <h2>ORDEN SIN CONFIRMAR</h2>
    <p><strong>{{ runnerState.pending.description }}</strong></p>
    <p>No sabemos si el servidor la recibio. Esta guardada en este dispositivo con su identificador: <strong>no la repitas por otro medio</strong>. Reintentarla es seguro: el servidor no la aplicara dos veces.</p>
    <button type="button" :disabled="runnerState.busy" data-testid="retry" @click="$emit('retry')">Reintentar la misma orden</button>
    <button type="button" class="secondary" :disabled="runnerState.busy" data-testid="reconcile" @click="$emit('reconcile')">Preguntar al servidor si la aplico</button>
  </section>
  <section v-else-if="runnerState.blocked" class="pending" role="alert" data-testid="blocked">
    <h2>{{ runnerState.blocked.reason === 'foreign' ? 'ORDEN DE OTRA INSTALACION' : 'ORDEN ANTERIOR ILEGIBLE' }}</h2>
    <p>Hay una orden guardada que no se puede reenviar. No se admiten ordenes nuevas hasta decidir que hacer con ella.</p>
    <button v-if="runnerState.blocked.key" type="button" :disabled="runnerState.busy" @click="$emit('reconcile')">Preguntar al servidor si la aplico</button>
    <button type="button" class="secondary" :disabled="runnerState.busy" data-testid="discard" @click="$emit('discard')">Descartarla</button>
  </section>
  <p v-if="runnerState.notice" class="notice" role="status" data-testid="command-notice">{{ runnerState.notice }}</p>

  <button type="button" @click="$emit('refresh')">Actualizar</button>
</template>
