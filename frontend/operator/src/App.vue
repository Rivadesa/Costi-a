<script setup>
import { computed, onMounted, onBeforeUnmount } from 'vue';
import { useRouter } from 'vue-router';
import ConnectionBadge from './components/ConnectionBadge.vue';
import { DEMO_API_BASE } from './api/demo.js';
import { api } from './api/client.js';
import { clearSession, hasPermission, isAuthenticated, session } from './state/session.js';

const router = useRouter();
let pingTimer = null;
const userName = computed(() => session.user?.display_name || 'Usuario');
const showCheckout = computed(() => session.apiBase !== DEMO_API_BASE && hasPermission('payment.record'));

async function ping() {
  try { await api.meta(); } catch { /* badge reflects offline state */ }
}

async function logout() {
  try { await api.logout(); } catch { /* local session still ends */ }
  clearSession();
  await router.replace('/login');
}

function authLost() { router.replace('/login'); }

onMounted(() => {
  ping();
  pingTimer = window.setInterval(ping, 5000);
  window.addEventListener('hospitality:auth-lost', authLost);
});
onBeforeUnmount(() => {
  if (pingTimer) window.clearInterval(pingTimer);
  window.removeEventListener('hospitality:auth-lost', authLost);
});
</script>

<template>
  <RouterView v-if="!isAuthenticated" />
  <div v-else class="shell">
    <aside class="sidebar">
      <div><div class="brand">Hospitality OS</div><div class="brand-subtitle">Operación local</div></div>
      <nav>
        <RouterLink to="/service">Control de servicio</RouterLink>
        <RouterLink to="/kds">Cocina / KDS</RouterLink>
        <RouterLink v-if="showCheckout" to="/checkout">Cuenta / Caja</RouterLink>
      </nav>
      <div class="sidebar-bottom">
        <ConnectionBadge />
        <div class="user-block"><strong>{{ userName }}</strong><button class="link-button" @click="logout">Cerrar sesión</button></div>
      </div>
    </aside>
    <main class="workspace"><RouterView /></main>
  </div>
</template>
