<script setup>
import { computed } from 'vue';
import { session } from '../state/session.js';

const label = computed(() => ({
  online: 'Servidor local conectado',
  offline: 'Servidor local sin conexión',
  checking: 'Comprobando servidor…',
}[session.connection] || 'Estado desconocido'));
</script>

<template>
  <div class="connection" :data-state="session.connection">
    <span class="connection-dot" aria-hidden="true"></span>
    <span>{{ label }}</span>
  </div>
</template>
