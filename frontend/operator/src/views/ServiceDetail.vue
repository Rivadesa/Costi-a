<script setup>
import { computed, ref } from 'vue';
import { useRoute } from 'vue-router';
import { api } from '../api/client.js';
import { hasPermission } from '../state/session.js';
import { usePolling } from '../composables/usePolling.js';
import StatusPill from '../components/StatusPill.vue';

const route = useRoute();
const service = ref(null);
const config = ref({ tables: [], menus: [], stations: [] });
const actionBusy = ref(false);
const actionError = ref('');
const restrictionGuest = ref(null);
const restriction = ref({ label: '', type: 'allergy', severity: 'important', notes: '' });

const table = computed(() => config.value.tables.find(t => t.id === service.value?.table_id));
const activeCourse = computed(() => service.value?.courses?.find(c => ['fired','preparing','ready'].includes(c.status)) || service.value?.courses?.filter(c => ['served','skipped'].includes(c.status)).at(-1) || null);
const nextCourse = computed(() => service.value?.courses?.find(c => c.status === 'pending') || null);
const allGuestsCreated = computed(() => (service.value?.guests?.length || 0) >= (service.value?.pax || 0));

async function load() {
  const [detail, configuration] = await Promise.all([api.service(route.params.id), api.configuration()]);
  service.value = detail;
  config.value = configuration.data;
}

const { refresh, error } = usePolling(load, 2500);

async function act(fn) {
  actionBusy.value = true;
  actionError.value = '';
  try { await fn(); await refresh(); }
  catch (err) { actionError.value = err.message; await refresh(); }
  finally { actionBusy.value = false; }
}

async function createMissingGuests() {
  const missing = service.value.pax - service.value.guests.length;
  await act(async () => { for (let i = 0; i < missing; i += 1) await api.addGuest(service.value.id); });
}

function openRestriction(guest) {
  restrictionGuest.value = guest;
  restriction.value = { label: '', type: 'allergy', severity: 'important', notes: '' };
}

async function addRestriction() {
  const guestId = restrictionGuest.value?.id;
  if (!guestId) return;
  await act(() => api.addRestriction(service.value.id, guestId, {
    label: restriction.value.label,
    type: restriction.value.type,
    severity: restriction.value.severity,
    notes: restriction.value.notes || null,
  }));
  restrictionGuest.value = null;
}

function assignMenu(event) {
  const menuId = event.target.value;
  if (menuId) act(() => api.assignMenu(service.value.id, menuId));
}
</script>

<template>
  <section v-if="service" class="page service-detail">
    <header class="page-header">
      <div><p class="eyebrow">{{ table?.area || 'Sala' }}</p><h1>{{ table?.name || 'Mesa' }} · {{ service.pax }} pax</h1></div>
      <StatusPill :status="service.status" />
    </header>
    <p v-if="error || actionError" class="error-box">{{ actionError || error?.message }}</p>

    <div class="detail-grid">
      <main class="stack gap-lg">
        <section class="panel">
          <div class="panel-heading"><div><p class="eyebrow">Ritmo</p><h2>{{ activeCourse ? `Pase ${activeCourse.sequence} · ${activeCourse.name}` : 'Servicio preparado' }}</h2></div><StatusPill v-if="activeCourse" :status="activeCourse.status" /></div>
          <div class="primary-action-zone">
            <button v-if="service.status === 'open' && service.menu && hasPermission('service.manage')" class="button primary jumbo" :disabled="actionBusy" @click="act(() => api.startService(service.id))">Iniciar servicio</button>
            <button v-else-if="service.status === 'in_service' && (!activeCourse || ['served','skipped'].includes(activeCourse.status)) && nextCourse && hasPermission('course.fire')" class="button primary jumbo" :disabled="actionBusy" @click="act(() => api.fireNext(service.id))">Enviar pase {{ nextCourse.sequence }}</button>
            <button v-else-if="activeCourse?.status === 'ready' && hasPermission('course.serve')" class="button primary jumbo" :disabled="actionBusy" @click="act(() => api.serveCourse(service.id, activeCourse.id))">Marcar servido</button>
            <div v-else class="action-waiting">{{ activeCourse?.status === 'preparing' || activeCourse?.status === 'fired' ? 'Cocina trabajando…' : 'Sin acción principal pendiente' }}</div>
          </div>
          <ol class="course-line">
            <li v-for="course in service.courses" :key="course.id" :data-current="course.id === activeCourse?.id"><span>{{ course.sequence }}</span><div><strong>{{ course.name }}</strong><StatusPill :status="course.status" /></div></li>
          </ol>
        </section>

        <section class="panel">
          <div class="panel-heading"><div><p class="eyebrow">Comensales</p><h2>Restricciones y adaptaciones</h2></div><button v-if="!allGuestsCreated && hasPermission('service.edit')" class="button secondary" @click="createMissingGuests">Crear posiciones PAX</button></div>
          <div class="guest-grid">
            <div v-for="guest in service.guests" :key="guest.id" class="guest-card">
              <strong>PAX {{ guest.position }}<span v-if="guest.name"> · {{ guest.name }}</span></strong>
              <div v-if="guest.restrictions.length === 0" class="muted">Sin restricciones</div>
              <div v-for="item in guest.restrictions" :key="item.id" class="restriction" :data-severity="item.severity"><strong>⚠ {{ item.label }}</strong><span>{{ item.type }} · {{ item.severity }}</span></div>
              <button v-if="hasPermission('service.edit')" class="button secondary guest-action" @click="openRestriction(guest)">+ Restricción</button>
            </div>
          </div>
        </section>
      </main>

      <aside class="stack">
        <section class="panel compact">
          <p class="eyebrow">Menú operativo</p>
          <template v-if="service.menu"><h3>{{ service.menu.name }}</h3><p class="muted">El importe se gestiona únicamente desde Cuenta / Caja en el equipo principal.</p></template>
          <label v-else-if="hasPermission('service.edit')">Asignar menú<select @change="assignMenu"><option value="">Seleccionar…</option><option v-for="menu in config.menus" :key="menu.id" :value="menu.id">{{ menu.name }}</option></select></label>
        </section>

        <section class="panel compact">
          <p class="eyebrow">Seguimiento</p>
          <h3>Información operativa</h3>
          <p>{{ service.pax }} comensales</p>
          <p>{{ service.courses.length }} pases</p>
          <p class="muted">Esta pantalla no muestra cuenta provisional, importes ni cobros.</p>
        </section>
      </aside>
    </div>

    <div v-if="restrictionGuest" class="modal-backdrop" @click.self="restrictionGuest = null">
      <form class="modal-card stack" @submit.prevent="addRestriction">
        <div><p class="eyebrow">PAX {{ restrictionGuest.position }}</p><h2>Alergia, intolerancia o preferencia</h2></div>
        <label>Descripción<input v-model.trim="restriction.label" placeholder="Ej. Marisco" required autofocus /></label>
        <label>Tipo<select v-model="restriction.type"><option value="allergy">Alergia</option><option value="intolerance">Intolerancia</option><option value="preference">Preferencia / restricción</option></select></label>
        <label>Nivel de atención<select v-model="restriction.severity"><option value="informative">Informativa</option><option value="important">Importante</option><option value="critical">CRÍTICA</option></select></label>
        <label>Observaciones<textarea v-model.trim="restriction.notes" rows="3" placeholder="Detalle útil para sala o cocina"></textarea></label>
        <p v-if="restriction.severity === 'critical'" class="critical-alert"><strong>⚠ Restricción crítica</strong><span>Se mostrará de forma destacada en sala y en las elaboraciones de este comensal.</span></p>
        <div class="modal-actions"><button type="button" class="button secondary" @click="restrictionGuest = null">Cancelar</button><button class="button primary" :disabled="actionBusy">Guardar restricción</button></div>
      </form>
    </div>
  </section>
  <section v-else class="page"><div class="empty-state">Cargando servicio…</div></section>
</template>
