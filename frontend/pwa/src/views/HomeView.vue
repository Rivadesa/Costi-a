<script setup lang="ts">
import { ref } from 'vue'
import type { Session } from '../api'
import { unpairWarning } from '../labels'
import { indexedDbPendingStore } from '../pending'
import { PERSISTENCE_HINT, type Persistence } from '../storage'
import Icon from './Icon.vue'

// C3c: el marco comun de las dos aplicaciones ya emparejadas: su pantalla (en el hueco), el aviso de almacenamiento y «Opciones».
defineProps<{ session: Session; persistence: Persistence }>()
defineEmits<{ unpair: [] }>()
const confirming = ref(false)   // desvincular en dos pasos (F9), sin window.confirm (CSP intacta)
// D6.7: con una orden guardada se AVISA antes de desvincular (no se bloquea): despues solo se comprueba con el mismo nombre.
const warning = ref('')
async function ask(): Promise<void> {
  confirming.value = true
  warning.value = unpairWarning(await indexedDbPendingStore.load().catch(() => null))
}
</script>

<template>
  <section class="screen" data-testid="home">
    <!-- ADR-012: cada vista pertenece a un modulo y solo se monta si la instalacion lo tiene activo. -->
    <!-- C3c: la pantalla ya no se elige por rol: cada aplicacion pone la suya (Sala, el comandero; Cocina, su pantalla por estacion),
         y el rol solo se COMPRUEBA antes de llegar aqui (Shell.vue, appFits). La proyeccion es la operativa: sin importes. -->
    <slot v-if="session.modules.includes('dining')" />
    <p v-else class="notice neutral" data-testid="no-modules">Esta instalación no tiene activo ningún módulo con pantalla para este dispositivo.</p>

    <!-- D6.6: si el navegador puede borrar lo guardado (credencial, orden sin confirmar), se dice siempre a la vista. -->
    <p v-if="PERSISTENCE_HINT[persistence]" class="notice neutral options" data-testid="persistence-hint"><Icon name="info" />{{ PERSISTENCE_HINT[persistence] }}</p>
    <details class="options" data-testid="options">
      <summary><Icon name="dots" :stroke="2.6" />Opciones</summary>
      <div class="options-b card">
        <p class="hint">Servidor {{ session.serverVersion }}.</p>
        <p class="hint">Desvincular deja este dispositivo sin acceso, también en el servidor. Para volver a usarlo habrá que conectarlo de nuevo desde el PC principal.</p>
        <button v-if="!confirming" type="button" class="btn btn-caution" data-testid="unpair" @click="ask">Desvincular este dispositivo</button>
        <template v-else>
          <p v-if="warning" class="pending" role="alert" data-testid="unpair-pending">{{ warning }}</p>
          <div class="row">
            <button type="button" class="btn btn-primary" data-testid="unpair-confirm" @click="confirming = false; $emit('unpair')">Sí, desvincular</button>
            <button type="button" class="btn" @click="confirming = false">No, seguir conectado</button>
          </div>
        </template>
      </div>
    </details>
  </section>
</template>
