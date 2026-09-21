<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import type { Session } from '../api'
import type { Credential } from '../credentials'

const props = defineProps<{ session: Session; credential: Credential; readAt: Date | null }>()
defineEmits<{ unpair: []; refresh: [] }>()

// Antiguedad de los datos SIEMPRE visible: una pantalla operativa vieja no debe parecer actual.
const now = ref(Date.now())
let tick: number | undefined
onMounted(() => { tick = window.setInterval(() => { now.value = Date.now() }, 1000) })
onUnmounted(() => window.clearInterval(tick))
const age = computed(() => props.readAt ? Math.max(0, Math.round((now.value - props.readAt.getTime()) / 1000)) : null)
const stale = computed(() => age.value === null || age.value > 45)
const ROLES: Record<string, string> = { main: 'Principal', service: 'Sala', kitchen: 'Cocina' }
</script>

<template>
  <section data-testid="home">
    <h2>{{ ROLES[session.role] ?? session.role }}<span v-if="session.station"> · estacion {{ session.station }}</span></h2>
    <dl>
      <dt>Identidad</dt><dd data-testid="actor">{{ session.actor }}</dd>
      <dt>Servidor</dt><dd>{{ session.serverVersion }} · {{ session.companyId }}/{{ session.locationId }}</dd>
      <dt>Ultima lectura</dt>
      <dd :class="{ stale }" data-testid="age">
        <template v-if="age === null">sin lectura</template>
        <template v-else>hace {{ age }} s<strong v-if="stale"> · DATOS ANTIGUOS: sin conexion con el servidor</strong></template>
      </dd>
    </dl>
    <p>Dispositivo emparejado y autorizado. Las pantallas de comandero y cocina llegan en los siguientes cortes (D6.2–D6.4).</p>
    <button type="button" @click="$emit('refresh')">Actualizar</button>
    <button type="button" class="secondary" @click="$emit('unpair')">Desvincular este dispositivo</button>
  </section>
</template>
