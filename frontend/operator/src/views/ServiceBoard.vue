<script setup>
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { api } from '../api/client.js';
import { hasPermission } from '../state/session.js';
import { usePolling } from '../composables/usePolling.js';
import StatusPill from '../components/StatusPill.vue';

const router = useRouter();
const rows = ref([]);
const config = ref({ tables: [], menus: [], stations: [] });
const showOpen = ref(false);
const form = ref({ table_id: '', pax: 2, menu_id: '' });
const actionError = ref('');
const actionBusy = ref(false);

const tableMap = computed(() => new Map(config.value.tables.map(t => [t.id, t])));
const activeTableIds = computed(() => new Set(rows.value.map(row => row.table_id)));
const availableTables = computed(() => config.value.tables.filter(table => !activeTableIds.value.has(table.id)));

function tableName(id) {
  const table = tableMap.value.get(id);
  return table ? table.name : 'Mesa';
}

function elapsed(iso) {
  if (!iso) return '';
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  const m = Math.floor(seconds / 60);
  const s = String(seconds % 60).padStart(2, '0');
  return `${m}:${s}`;
}

async function load() {
  const [board, configuration] = await Promise.all([api.board(), api.configuration()]);
  rows.value = board.data || [];
  config.value = configuration.data || config.value;
}

const { refresh, loading, error } = usePolling(load, 3000);

async function openService() {
  actionBusy.value = true;
  actionError.value = '';
  try {
    const opened = await api.openService({
      table_id: form.value.table_id,
      pax: Number(form.value.pax),
      menu_id: form.value.menu_id || null,
    });
    showOpen.value = false;
    await router.push(`/service/${opened.service_id}`);
  } catch (err) {
    actionError.value = err.message;
    await refresh();
  } finally {
    actionBusy.value = false;
  }
}
</script>

<template>
  <section class="page">
    <header class="page-header">
      <div><p class="eyebrow">Servicio en directo</p><h1>Control de sala</h1></div>
      <div class="header-actions">
        <button class="button secondary" :disabled="loading" @click="refresh">Actualizar</button>
        <button v-if="hasPermission('service.open')" class="button primary" @click="showOpen = true">Abrir mesa</button>
      </div>
    </header>

    <p v-if="error" class="error-box">{{ error.message }}</p>
    <div v-if="rows.length === 0 && !loading" class="empty-state">No hay servicios activos.</div>

    <div class="board-grid">
      <button v-for="row in rows" :key="row.service_id" class="table-card" @click="router.push(`/service/${row.service_id}`)">
        <div class="table-card-top"><strong>{{ tableName(row.table_id) }}</strong><span>{{ row.pax }} pax</span></div>
        <div v-if="row.course" class="course-focus">
          <span class="course-number">P{{ row.course.sequence }}</span>
          <div><strong>{{ row.course.name }}</strong><StatusPill :status="row.course.status" /></div>
        </div>
        <div v-else class="course-focus muted">Sin pase iniciado</div>
        <div v-if="row.course?.served_at" class="timer">Servido · {{ elapsed(row.course.served_at) }}</div>
        <div v-else-if="row.course?.fired_at" class="timer">En curso · {{ elapsed(row.course.fired_at) }}</div>
        <div v-if="row.critical_restrictions?.length" class="critical-alert">
          <strong>⚠ Restricción crítica</strong>
          <span v-for="item in row.critical_restrictions" :key="item">{{ item }}</span>
        </div>
      </button>
    </div>

    <div v-if="showOpen" class="modal-backdrop" @click.self="showOpen = false">
      <form class="modal-card stack" @submit.prevent="openService">
        <div><p class="eyebrow">Nuevo servicio</p><h2>Abrir mesa</h2></div>
        <label>Mesa<select v-model="form.table_id" required><option value="" disabled>Seleccionar…</option><option v-for="table in availableTables" :key="table.id" :value="table.id">{{ table.name }} · {{ table.capacity }} pax</option></select></label>
        <label>Comensales<input v-model.number="form.pax" type="number" min="1" max="100" required /></label>
        <label>Menú<select v-model="form.menu_id"><option value="">Asignar después</option><option v-for="menu in config.menus" :key="menu.id" :value="menu.id">{{ menu.name }}</option></select></label>
        <p class="hint">Las posiciones PAX y, si se selecciona, el menú se crean en la misma operación para evitar aperturas incompletas. Los importes se gestionan desde Cuenta / Caja.</p>
        <p v-if="actionError" class="error-box">{{ actionError }}</p>
        <div class="modal-actions"><button type="button" class="button secondary" @click="showOpen = false">Cancelar</button><button class="button primary" :disabled="actionBusy">{{ actionBusy ? 'Abriendo…' : 'Abrir mesa' }}</button></div>
      </form>
    </div>
  </section>
</template>
