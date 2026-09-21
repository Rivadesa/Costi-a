<script setup lang="ts">
import type { Session } from '../api'
import type { Credential } from '../credentials'
import { PERSISTENCE_HINT, type Persistence } from '../storage'
import BoardView from './BoardView.vue'
import KitchenView from './KitchenView.vue'

defineProps<{ session: Session; credential: Credential; persistence: Persistence }>()
defineEmits<{ unpair: []; refresh: [] }>()
const ROLES: Record<string, string> = { main: 'Principal', service: 'Sala', kitchen: 'Cocina' }
</script>

<template>
  <section data-testid="home">
    <h2 data-testid="role">{{ ROLES[session.role] ?? session.role }}<span v-if="session.station"> · estacion {{ session.station }}</span></h2>
    <p class="identity"><span data-testid="actor">{{ session.actor }}</span> · servidor {{ session.serverVersion }} · {{ session.companyId }}/{{ session.locationId }}</p>

    <!-- Sala (y un dispositivo principal) ven el comandero. La proyeccion es la operativa: sin importes. -->
    <BoardView v-if="session.role !== 'kitchen'" :token="credential.token" :session="session" @unauthorized="$emit('refresh')" />
    <!-- Cocina ve SU pantalla por estacion (D6.4): misma lectura en vivo y misma orden incierta, sin importes. -->
    <KitchenView v-else :token="credential.token" :session="session" @unauthorized="$emit('refresh')" />

    <footer>
      <p v-if="PERSISTENCE_HINT[persistence]" class="identity" data-testid="persistence-hint">{{ PERSISTENCE_HINT[persistence] }}</p>
      <button type="button" class="secondary" @click="$emit('unpair')">Desvincular este dispositivo</button>
    </footer>
  </section>
</template>
