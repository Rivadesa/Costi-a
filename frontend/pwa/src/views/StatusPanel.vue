<script setup lang="ts">
import type { RunnerState } from '../commands'
import { ageLabel, ageShort, blockedTitle, otherActorAdvice } from '../labels'
import { LINK_TEXT, type LiveState } from '../live'
import Icon from './Icon.vue'

// Cabecera comun de comandero y cocina: estado del tiempo real, antiguedad de la ultima lectura (siempre a la
// vista) y la ORDEN SIN CONFIRMAR, que manda sobre todo lo demas: inequivoca, persistente y bloqueante.
// D6.7: una orden pendiente es siempre de ESTE dispositivo y nombre (lo de otro nombre llega como bloqueada), asi que la
// frase "reintentarla es segura" solo se dice donde es cierta. Las tres causas de bloqueo (otro nombre, otra instalacion,
// ilegible) se ven con su titulo y solo con los botones que les corresponden. C3a: el aspecto nuevo, los mismos textos y botones.
// C3d: un hueco en la fila del enlace para lo que cada pantalla quiera tener siempre a la vista (en cocina, las comandas y la hora).
defineProps<{ state: LiveState; runnerState: RunnerState; age: number | null; stale: boolean }>()
defineEmits<{ retry: []; reconcile: []; discard: []; refresh: [] }>()
</script>

<template>
  <div class="status">
    <p class="net" :class="{ warn: state.link !== 'live' }" role="status"><span class="dot" aria-hidden="true"></span><span data-testid="link">{{ LINK_TEXT[state.link] }}</span></p>
    <!-- En un movil, la antiguedad en corto al lado del enlace («Leído hace 3 s»): una fila menos sobre la mesa, y siempre a la vista. -->
    <p class="age" :class="{ stale }" data-testid="age"><span class="age-l">Última lectura del servidor: {{ ageLabel(age) }}</span><span class="age-s">{{ ageShort(age) }}</span><strong v-if="stale"> · DATOS ANTIGUOS</strong></p>
    <!-- C3d: Costina Cocina pone aqui, en la misma fila, cuantas comandas hay y la hora. Sala no pone nada. -->
    <slot />
    <button type="button" class="btn btn-quiet refresh" data-testid="refresh" @click="$emit('refresh')"><Icon name="refresh" /><span class="refresh-t">Actualizar</span></button>
  </div>

  <div v-if="state.error || runnerState.pending || runnerState.blocked || runnerState.notice" class="status-panels">
    <p v-if="state.error" class="notice" role="alert" data-testid="board-error"><Icon name="wifiOff" />{{ state.error }}</p>

    <section v-if="runnerState.pending" class="pending" role="alert" data-testid="pending">
      <h2><Icon name="alert" />ORDEN SIN CONFIRMAR</h2>
      <p><strong>{{ runnerState.pending.description }}</strong></p>
      <p>No sabemos si el servidor la recibió. Está guardada en este dispositivo con su identificador: <strong>no la repitas por otro medio</strong>. Reintentarla desde aquí es seguro: es de este mismo dispositivo y nombre, y con el mismo identificador el servidor no la aplica dos veces.</p>
      <div class="row">
        <button type="button" class="btn btn-primary" :disabled="runnerState.busy" data-testid="retry" @click="$emit('retry')"><Icon name="send" />Reintentar la misma orden</button>
        <button type="button" class="btn" :disabled="runnerState.busy" data-testid="reconcile" @click="$emit('reconcile')">Preguntar al servidor si la aplicó</button>
      </div>
    </section>
    <section v-else-if="runnerState.blocked" class="pending" role="alert" data-testid="blocked">
      <h2 data-testid="blocked-title"><Icon name="alert" />{{ blockedTitle(runnerState.blocked) }}</h2>
      <template v-if="runnerState.blocked.reason === 'actor'">
        <p><strong>{{ runnerState.blocked.description }}</strong></p>
        <p data-testid="blocked-advice">{{ otherActorAdvice(runnerState.blocked.actor) }}</p>
      </template>
      <p>Hay una orden guardada que no se puede reenviar. No se admiten órdenes nuevas hasta decidir qué hacer con ella.</p>
      <div class="row">
        <!-- D6.7: la de otro nombre no se pregunta: el servidor solo responde por las ordenes del mismo nombre y diria "no consta". -->
        <button v-if="runnerState.blocked.key && runnerState.blocked.reason !== 'actor'" type="button" class="btn" :disabled="runnerState.busy" data-testid="blocked-reconcile" @click="$emit('reconcile')">Preguntar al servidor si la aplicó</button>
        <button type="button" class="btn btn-caution" :disabled="runnerState.busy" data-testid="discard" @click="$emit('discard')">Descartarla</button>
      </div>
    </section>
    <p v-if="runnerState.notice" class="notice" role="status" data-testid="command-notice"><Icon name="info" />{{ runnerState.notice }}</p>
  </div>
</template>
