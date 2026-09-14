<script setup>
import { ref } from 'vue';
import ConnectionBadge from './components/ConnectionBadge.vue';

const connected = ref(true);
</script>

<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">Hospitality OS</div>
      <nav>
        <RouterLink to="/service">Servicio</RouterLink>
        <RouterLink to="/tables">Mesas</RouterLink>
        <RouterLink to="/kds">KDS</RouterLink>
      </nav>
      <ConnectionBadge :connected="connected" />
    </aside>
    <main class="workspace">
      <RouterView />
    </main>
  </div>
</template>
