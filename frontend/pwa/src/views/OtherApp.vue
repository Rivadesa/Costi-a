<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Session } from '../api'
import { appForRole, APPS, connectedAs, type AppId } from '../apps'
import { unpairWarning } from '../labels'
import { pendingStoreIn } from '../pending'
import AppIcon from './AppIcon.vue'
import Icon from './Icon.vue'

// C3c: este dispositivo esta conectado como la OTRA aplicacion (o con un papel que ninguna conoce): esta no monta nada. Dice como esta
// conectado, con nombres y nunca codigos (un papel desconocido sale tal cual, fallo cerrado), y ofrece desvincularlo en dos pasos.
// No da ni quita permisos: el servidor seguiria filtrando por rol y estacion lo que se pintara.
// database: la base donde esta esa credencial (la de la otra aplicacion, la vieja o, en teoria, la propia), con su orden guardada.
const props = defineProps<{ app: AppId; session: Session; database: string }>()
defineEmits<{ unpair: [] }>()
const owner = computed(() => appForRole(props.session.role))
const confirming = ref(false)
const warning = ref('')
async function ask(): Promise<void> {
  confirming.value = true
  warning.value = unpairWarning(await pendingStoreIn(() => props.database).load().catch(() => null))
}
</script>

<template>
  <section class="pair-card card other-app" data-testid="other-app">
    <AppIcon v-if="owner" :app="owner" size="l" />
    <h2 data-testid="other-app-as">Este dispositivo está conectado como {{ connectedAs(session) }}.</h2>
    <p v-if="owner">Ábrelo con el icono de {{ APPS[owner].name }}.</p>
    <p v-else>Ninguna de las dos aplicaciones sabe trabajar con ese tipo de dispositivo, así que aquí no se enseña nada.</p>
    <button v-if="!confirming" type="button" class="btn btn-caution" data-testid="unpair" @click="ask">Desvincular este dispositivo</button>
    <template v-else>
      <p v-if="warning" class="pending" role="alert" data-testid="unpair-pending">{{ warning }}</p>
      <div class="row">
        <button type="button" class="btn btn-primary" data-testid="unpair-confirm" @click="confirming = false; $emit('unpair')">Sí, desvincular</button>
        <button type="button" class="btn" @click="confirming = false">No, dejarlo así</button>
      </div>
    </template>
    <p class="hint"><Icon name="info" :size="16" />«Desvincular» lo desconecta también en el servidor; después se puede emparejar con un código de {{ APPS[app].name }}.</p>
  </section>
</template>
