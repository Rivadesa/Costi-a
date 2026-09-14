<script setup>
import { computed } from 'vue';
import { session } from '../state/session.js';

const label = computed(() => {
  if (session.connection === 'online') {
    const version = session.serverMeta?.version;
    return version ? `Servidor local conectado · v${version}` : 'Servidor local conectado';
  }
  if (session.connection === 'offline') return 'Servidor local sin conexión';
  if (session.connection === 'checking') return 'Comprobando servidor…';
  return 'Estado desconocido';
});
</script>

<template>
  <div class="connection" :data-state="session.connection">
    <span class="connection-dot" aria-hidden="true"></span>
    <span>{{ label }}</span>
  </div>
</template>
