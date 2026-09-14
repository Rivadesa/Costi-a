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
const consumption = ref({ name: '', quantity: 1, unit_price: '' });
const payment = ref({ method: 'card', amount: '' });

const table = computed(() => config.value.tables.find(t => t.id === service.value?.table_id));
const activeCourse = computed(() => service.value?.courses?.find(c => ['fired','preparing','ready'].includes(c.status)) || service.value?.courses?.filter(c => ['served','skipped'].includes(c.status)).at(-1) || null);
const nextCourse = computed(() => service.value?.courses?.find(c => c.status === 'pending') || null);
const allGuestsCreated = computed(() => (service.value?.guests?.length || 0) >= (service.value?.pax || 0));

async function load() {
  const [detail, configuration] = await Promise.all([api.service(route.params.id), api.configuration()]);
  service.value = detail;
  config.value = configuration.data;
  if (!payment.value.amount && detail.balance_cents > 0) payment.value.amount = (detail.balance_cents / 100).toFixed(2);
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

async function addConsumption() {
  const cents = Math.round(Number(consumption.value.unit_price.replace(',', '.')) * 100);
  await act(() => api.addConsumption(service.value.id, { name: consumption.value.name, quantity: Number(consumption.value.quantity), unit_price_cents: cents }));
  consumption.value = { name: '', quantity: 1, unit_price: '' };
}

async function addPayment() {
  const cents = Math.round(Number(payment.value.amount.replace(',', '.')) * 100);
  await act(() => api.addPayment(service.value.id, { method: payment.value.method, amount_cents: cents }));
}

function money(cents) { return `${(Number(cents || 0) / 100).toFixed(2)} €`; }
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
            <div v-for="guest in service.guests" :key="guest.id" class="guest-card"><strong>PAX {{ guest.position }}<span v-if="guest.name"> · {{ guest.name }}</span></strong><div v-if="guest.restrictions.length === 0" class="muted">Sin restricciones</div><div v-for="restriction in guest.restrictions" :key="restriction.id" class="restriction" :data-severity="restriction.severity"><strong>⚠ {{ restriction.label }}</strong><span>{{ restriction.type }} · {{ restriction.severity }}</span></div></div>
          </div>
        </section>
      </main>

      <aside class="stack">
        <section class="panel compact">
          <p class="eyebrow">Menú</p>
          <template v-if="service.menu"><h3>{{ service.menu.name }}</h3><p>{{ money(service.menu.unit_price_cents) }} / persona</p></template>
          <label v-else-if="hasPermission('service.edit')">Asignar menú<select @change="act(() => api.assignMenu(service.id, $event.target.value))"><option value="">Seleccionar…</option><option v-for="menu in config.menus" :key="menu.id" :value="menu.id">{{ menu.name }}</option></select></label>
        </section>

        <section class="panel compact">
          <p class="eyebrow">Cuenta provisional</p><div class="account-total">{{ money(service.subtotal_cents) }}</div>
          <div v-for="item in service.consumptions.filter(c => !c.cancelled)" :key="item.id" class="account-row"><span>{{ item.quantity }} × {{ item.name }}</span><strong>{{ money(item.quantity * item.unit_price_cents) }}</strong></div>
          <form v-if="hasPermission('consumption.add')" class="mini-form" @submit.prevent="addConsumption"><input v-model.trim="consumption.name" placeholder="Bebida / extra" required /><input v-model="consumption.quantity" type="number" min="1" required /><input v-model="consumption.unit_price" inputmode="decimal" placeholder="€" required /><button class="button secondary">Añadir</button></form>
        </section>

        <section v-if="hasPermission('payment.record') && service.balance_cents > 0" class="panel compact">
          <p class="eyebrow">Cobro pendiente</p><div class="account-total">{{ money(service.balance_cents) }}</div>
          <form class="mini-form vertical" @submit.prevent="addPayment"><select v-model="payment.method"><option value="card">Tarjeta</option><option value="cash">Efectivo</option><option value="other">Otro</option></select><input v-model="payment.amount" inputmode="decimal" required /><button class="button primary">Registrar cobro</button></form>
        </section>
        <button v-if="service.status === 'paid' && hasPermission('service.close')" class="button primary large" @click="act(() => api.closeService(service.id))">Cerrar servicio</button>
      </aside>
    </div>
  </section>
  <section v-else class="page"><div class="empty-state">Cargando servicio…</div></section>
</template>
