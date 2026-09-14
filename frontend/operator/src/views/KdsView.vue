<script setup>
import { computed, ref, watch } from 'vue';
import { api } from '../api/client.js';
import { hasPermission } from '../state/session.js';
import { usePolling } from '../composables/usePolling.js';
import StatusPill from '../components/StatusPill.vue';

const config = ref({ tables: [], menus: [], stations: [] });
const stationId = ref('');
const queue = ref([]);
const actionError = ref('');
const tableMap = computed(() => new Map(config.value.tables.map(t => [t.id, t])));

function tableName(id) { return tableMap.value.get(id)?.name || 'Mesa'; }

async function load() {
  const configuration = await api.configuration();
  config.value = configuration.data;
  if (!stationId.value && config.value.stations.length) stationId.value = config.value.stations[0].id;
  if (!stationId.value) { queue.value = []; return; }
  const result = await api.kds(stationId.value);
  queue.value = result.data || [];
}

const { refresh, loading, error } = usePolling(load, 2000);
watch(stationId, refresh);

async function act(fn) {
  actionError.value = '';
  try { await fn(); await refresh(); } catch (err) { actionError.value = err.message; await refresh(); }
}

function age(iso) {
  if (!iso) return '0:00';
  const sec = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  return `${Math.floor(sec / 60)}:${String(sec % 60).padStart(2, '0')}`;
}
</script>

<template>
  <section class="page kds-page">
    <header class="page-header">
      <div><p class="eyebrow">Kitchen Display System</p><h1>Cocina</h1></div>
      <span class="muted">{{ loading ? 'Actualizando…' : `${queue.length} comandas` }}</span>
    </header>
    <div class="station-tabs"><button v-for="station in config.stations" :key="station.id" :class="['station-tab', { active: station.id === stationId }]" @click="stationId = station.id">{{ station.name }}</button></div>
    <p v-if="error || actionError" class="error-box">{{ actionError || error?.message }}</p>
    <div v-if="queue.length === 0" class="empty-state">Sin preparaciones pendientes en esta estación.</div>
    <div class="kds-grid">
      <article v-for="ticket in queue" :key="ticket.service_id + ticket.course_id" class="kds-ticket">
        <header><div><strong>{{ tableName(ticket.table_id) }}</strong><span>Pase {{ ticket.course_sequence }} · {{ ticket.course_name }}</span></div><div class="ticket-time">{{ age(ticket.fired_at) }}</div></header>
        <StatusPill :status="ticket.course_status" />
        <div class="kds-items">
          <div v-for="item in ticket.items" :key="item.id" class="kds-item">
            <div class="kds-item-copy"><strong>{{ item.quantity }} × {{ item.name }}</strong><span v-if="item.guest_position">PAX {{ item.guest_position }}</span><span v-if="item.modification_reason" class="modification">CAMBIO: {{ item.modification_reason }}</span></div>
            <div v-if="item.restrictions?.length" class="restriction-stack"><div v-for="r in item.restrictions" :key="r.label" class="restriction" :data-severity="r.severity"><strong>⚠ {{ r.label }}</strong><span>{{ r.type }} · {{ r.severity }}</span></div></div>
            <button v-if="item.status === 'fired' && hasPermission('kitchen.update')" class="button kitchen" @click="act(() => api.startItem(ticket.service_id, ticket.course_id, item.id))">INICIAR</button>
            <button v-else-if="item.status === 'preparing' && hasPermission('kitchen.update')" class="button kitchen ready" @click="act(() => api.readyItem(ticket.service_id, ticket.course_id, item.id))">LISTO</button>
            <StatusPill v-else :status="item.status" />
          </div>
        </div>
        <button v-if="ticket.course_status === 'preparing' && ticket.items.every(i => ['ready','cancelled'].includes(i.status)) && hasPermission('kitchen.pass')" class="button primary large" @click="act(() => api.courseReady(ticket.service_id, ticket.course_id))">VALIDAR PASE LISTO</button>
      </article>
    </div>
  </section>
</template>
