<script setup lang="ts">
import type { Session } from '../api'
import type { Credential } from '../credentials'
import BoardView from './BoardView.vue'

defineProps<{ session: Session; credential: Credential }>()
defineEmits<{ unpair: []; refresh: [] }>()
const ROLES: Record<string, string> = { main: 'Principal', service: 'Sala', kitchen: 'Cocina' }
</script>

<template>
  <section data-testid="home">
    <h2 data-testid="role">{{ ROLES[session.role] ?? session.role }}<span v-if="session.station"> · estacion {{ session.station }}</span></h2>
    <p class="identity"><span data-testid="actor">{{ session.actor }}</span> · servidor {{ session.serverVersion }} · {{ session.companyId }}/{{ session.locationId }}</p>

    <!-- Sala (y un dispositivo principal) ven el comandero. La proyeccion es la operativa: sin importes. -->
    <BoardView v-if="session.role !== 'kitchen'" :token="credential.token" @unauthorized="$emit('refresh')" />
    <p v-else data-testid="kitchen-placeholder">Dispositivo de cocina emparejado. La pantalla de cocina por estacion llega en el corte D6.4.</p>

    <footer>
      <button type="button" class="secondary" @click="$emit('unpair')">Desvincular este dispositivo</button>
    </footer>
  </section>
</template>
