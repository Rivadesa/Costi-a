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
const showService = computed(() => ['main', 'service'].includes(session.terminalMode));
const showKds = computed(() => ['main', 'kds'].includes(session.terminalMode));
const showCheckout = computed(() => session.terminalMode === 'main' && session.apiBase !== DEMO_API_BASE && hasPermission('payment.record'));
const showCatalog = computed(() => session.terminalMode === 'main' && session.apiBase !== DEMO_API_BASE && hasPermission('catalog.manage'));
const terminalLabel = computed(() => ({ main: 'Equipo principal', service: 'Sala / maître', kds: 'Cocina / KDS' }[session.terminalMode] || 'Terminal'));

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
      <div><div class="brand">Hospitality OS</div><div class="brand-subtitle">{{ terminalLabel }}</div></div>
      <nav>
        <RouterLink v-if="showService" to="/service">Control de servicio</RouterLink>
        <RouterLink v-if="showKds" to="/kds">Cocina / KDS</RouterLink>
        <RouterLink v-if="showCatalog" to="/catalog">Catálogo del restaurante</RouterLink>
        <RouterLink v-if="showCheckout" to="/checkout">Cuenta / Caja</RouterLink>
      </nav>
      <div class="sidebar-bottom">
        <ConnectionBadge />
        <div class="user-block"><strong>{{ userName }}</strong><span class="muted">{{ terminalLabel }}</span><button class="link-button" @click="logout">Cerrar sesión</button></div>
      </div>
    </aside>
    <main class="workspace"><RouterView /></main>
  </div>
</template>
