<script setup lang="ts">
import { ref } from 'vue'
import type { Session } from '../api'
import type { Credential } from '../credentials'
import { PERSISTENCE_HINT, type Persistence } from '../storage'
import BoardView from './BoardView.vue'
import KitchenView from './KitchenView.vue'

defineProps<{ session: Session; credential: Credential; persistence: Persistence }>()
defineEmits<{ unpair: []; refresh: [] }>()
// A11: la cabecera dice QUE es este dispositivo y DONDE esta, con el nombre de la estacion (no su codigo).
const KINDS: Record<string, string> = { main: 'PRINCIPAL (antiguo)', service: 'COMANDERO', kitchen: 'PANTALLA DE COCINA' }
const confirming = ref(false)   // desvincular en dos pasos (F9), sin window.confirm (CSP intacta)
</script>

<template>
  <section data-testid="home">
    <h2 class="headline" data-testid="role">{{ KINDS[session.role] ?? session.role }}<span v-if="session.stationName || session.station"> · {{ session.stationName || session.station }}</span></h2>
    <p class="identity"><span data-testid="actor">{{ session.actor }}</span> · servidor {{ session.serverVersion }} · {{ session.companyId }}/{{ session.locationId }}</p>

    <!-- ADR-012: cada vista pertenece a un modulo y solo se monta si la instalacion lo tiene activo. -->
    <template v-if="session.modules.includes('dining')">
      <!-- Sala (y un dispositivo principal) ven el comandero. La proyeccion es la operativa: sin importes. -->
      <BoardView v-if="session.role !== 'kitchen'" :token="credential.token" :session="session" @unauthorized="$emit('refresh')" />
      <!-- Cocina ve SU pantalla por estacion (D6.4): misma lectura en vivo y misma orden incierta, sin importes. -->
      <KitchenView v-else :token="credential.token" :session="session" @unauthorized="$emit('refresh')" />
    </template>
    <p v-else data-testid="no-modules">Esta instalacion no tiene activo ningun modulo con pantalla para este dispositivo.</p>

    <footer>
      <p v-if="PERSISTENCE_HINT[persistence]" class="identity" data-testid="persistence-hint">{{ PERSISTENCE_HINT[persistence] }}</p>
      <details data-testid="options">
        <summary>Opciones</summary>
        <p class="identity">Desvincular deja este dispositivo sin acceso, tambien en el servidor. Para volver a usarlo habra que conectarlo de nuevo desde el PC principal.</p>
        <button v-if="!confirming" type="button" class="secondary" @click="confirming = true">Desvincular este dispositivo</button>
        <p v-else class="row">
          <button type="button" data-testid="unpair-confirm" @click="confirming = false; $emit('unpair')">Si, desvincular</button>
          <button type="button" class="secondary" @click="confirming = false">No, seguir conectado</button>
        </p>
      </details>
    </footer>
  </section>
</template>
