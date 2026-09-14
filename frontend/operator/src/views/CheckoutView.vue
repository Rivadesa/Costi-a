<script setup>
import { computed, ref } from 'vue';
import { api } from '../api/client.js';
import { hasPermission } from '../state/session.js';
import { usePolling } from '../composables/usePolling.js';
import StatusPill from '../components/StatusPill.vue';

const rows = ref([]);
const config = ref({ tables: [], sale_categories: [], sale_items: [] });
const selectedServiceId = ref('');
const account = ref(null);
const selectedCategoryId = ref('');
const selectedProductId = ref('');
const quantity = ref(1);
const payment = ref({ method: 'card', amount: '' });
const actionBusy = ref(false);
const actionError = ref('');

const tableMap = computed(() => new Map(config.value.tables.map(table => [table.id, table])));
const selectedRow = computed(() => rows.value.find(row => row.service_id === selectedServiceId.value) || null);
const selectedTable = computed(() => tableMap.value.get(account.value?.table_id || selectedRow.value?.table_id));
const categories = computed(() => config.value.sale_categories || []);
const saleItems = computed(() => {
  const items = config.value.sale_items || [];
  if (!selectedCategoryId.value) return items;
  return items.filter(item => item.category_id === selectedCategoryId.value);
});
const selectedProduct = computed(() => (config.value.sale_items || []).find(item => item.id === selectedProductId.value) || null);

function money(cents) { return `${(Number(cents || 0) / 100).toFixed(2)} €`; }
function tableName(tableId) { return tableMap.value.get(tableId)?.name || 'Mesa'; }

async function loadAccount(serviceId = selectedServiceId.value) {
  if (!serviceId) { account.value = null; return; }
  account.value = await api.checkoutService(serviceId);
  if (!payment.value.amount || Number(payment.value.amount.replace(',', '.')) === 0) {
    payment.value.amount = (account.value.balance_cents / 100).toFixed(2);
  }
}
async function load() {
  const [board, configuration, catalog] = await Promise.all([api.board(), api.configuration(), api.checkoutCatalog()]);
  rows.value = board.data || [];
  config.value = { ...configuration.data, ...catalog.data };
  if (!selectedServiceId.value && rows.value.length) selectedServiceId.value = rows.value[0].service_id;
  if (selectedServiceId.value && !rows.value.some(row => row.service_id === selectedServiceId.value)) {
    selectedServiceId.value = rows.value[0]?.service_id || '';
  }
  await loadAccount();
}
const { refresh, loading, error } = usePolling(load, 4000);
async function selectService(serviceId) {
  selectedServiceId.value = serviceId;
  payment.value.amount = '';
  await loadAccount(serviceId);
}
async function act(fn) {
  actionBusy.value = true;
  actionError.value = '';
  try { await fn(); await refresh(); }
  catch (err) { actionError.value = err.message; }
  finally { actionBusy.value = false; }
}
async function addProduct() {
  if (!selectedServiceId.value || !selectedProductId.value) return;
  await act(() => api.checkoutAddConsumption(selectedServiceId.value, selectedProductId.value, quantity.value));
  selectedProductId.value = '';
  quantity.value = 1;
}
async function recordPayment() {
  const amountCents = Math.round(Number(payment.value.amount.replace(',', '.')) * 100);
  if (amountCents < 1) return;
  await act(() => api.checkoutAddPayment(selectedServiceId.value, { method: payment.value.method, amount_cents: amountCents }));
}
async function cancelConsumption(item) {
  const reason = window.prompt(`Motivo para anular “${item.name}”: `);
  if (!reason?.trim()) return;
  await act(() => api.checkoutCancelConsumption(selectedServiceId.value, item.id, reason.trim()));
}
async function closeService() { await act(() => api.checkoutClose(selectedServiceId.value)); }
</script>

<template>
  <section class="page">
    <header class="page-header">
      <div><p class="eyebrow">Equipo principal</p><h1>Cuenta / Caja</h1></div>
      <button class="button secondary" :disabled="loading" @click="refresh">Actualizar</button>
    </header>
    <p v-if="error || actionError" class="error-box">{{ actionError || error?.message }}</p>
    <div class="checkout-layout">
      <aside class="panel checkout-service-list">
        <div class="panel-heading"><div><p class="eyebrow">Servicios activos</p><h2>Mesas</h2></div></div>
        <button v-for="row in rows" :key="row.service_id" class="checkout-service-button" :data-active="row.service_id === selectedServiceId" @click="selectService(row.service_id)">
          <span><strong>{{ tableName(row.table_id) }}</strong><small>{{ row.pax }} pax</small></span>
          <StatusPill :status="row.service_status || row.status" />
        </button>
        <div v-if="!rows.length" class="muted">No hay mesas activas.</div>
      </aside>
      <main v-if="account" class="stack gap-lg">
        <section class="panel">
          <div class="panel-heading">
            <div><p class="eyebrow">Cuenta provisional</p><h2>{{ selectedTable?.name || 'Mesa' }} · {{ account.pax }} pax</h2></div>
            <div class="account-total">{{ money(account.subtotal_cents) }}</div>
          </div>
          <div class="checkout-lines">
            <div v-if="account.menu" class="account-row"><span>{{ account.menu.quantity }} × {{ account.menu.name }}</span><strong>{{ money(account.menu.total_cents) }}</strong></div>
            <div v-for="item in account.consumptions.filter(item => !item.cancelled)" :key="item.id" class="account-row">
              <span>{{ item.quantity }} × {{ item.name }}</span>
              <div class="account-row-actions"><strong>{{ money(item.total_cents) }}</strong><button v-if="hasPermission('consumption.cancel')" class="link-button" @click="cancelConsumption(item)">Anular</button></div>
            </div>
          </div>
        </section>
        <section v-if="hasPermission('consumption.add')" class="panel">
          <div class="panel-heading"><div><p class="eyebrow">Catálogo restaurante</p><h2>Añadir a la cuenta</h2></div></div>
          <div class="category-tabs">
            <button class="button secondary" :data-active="!selectedCategoryId" @click="selectedCategoryId = ''">Todo</button>
            <button v-for="category in categories" :key="category.id" class="button secondary" :data-active="selectedCategoryId === category.id" @click="selectedCategoryId = category.id">{{ category.name }}</button>
          </div>
          <form class="checkout-add-form" @submit.prevent="addProduct">
            <label>Artículo<select v-model="selectedProductId" required><option value="" disabled>Seleccionar…</option><option v-for="item in saleItems" :key="item.id" :value="item.id">{{ item.name }}<template v-if="item.format_label"> · {{ item.format_label }}</template> · {{ money(item.price_cents) }}</option></select></label>
            <label>Cantidad<input v-model.number="quantity" type="number" min="1" max="999" required /></label>
            <div v-if="selectedProduct" class="catalog-selection-summary"><strong>{{ selectedProduct.name }}</strong><span>{{ money(selectedProduct.price_cents) }} / {{ selectedProduct.sale_unit }}</span></div>
            <button class="button primary" :disabled="actionBusy || !selectedProductId">Añadir</button>
          </form>
        </section>
        <section v-if="hasPermission('payment.record')" class="panel">
          <div class="panel-heading"><div><p class="eyebrow">Cobro</p><h2>Pendiente</h2></div><div class="account-total">{{ money(account.balance_cents) }}</div></div>
          <div class="checkout-payment-summary"><span>Pagado: <strong>{{ money(account.paid_cents) }}</strong></span><span>Total: <strong>{{ money(account.subtotal_cents) }}</strong></span></div>
          <form v-if="account.balance_cents > 0" class="checkout-payment-form" @submit.prevent="recordPayment">
            <select v-model="payment.method"><option value="card">Tarjeta</option><option value="cash">Efectivo</option><option value="other">Otro</option></select>
            <input v-model="payment.amount" inputmode="decimal" required />
            <button class="button primary" :disabled="actionBusy">Registrar cobro</button>
          </form>
          <button v-else-if="account.status === 'paid' && hasPermission('service.close')" class="button primary large" :disabled="actionBusy" @click="closeService">Cerrar servicio</button>
        </section>
      </main>
      <div v-else class="empty-state">Selecciona una mesa para gestionar su cuenta.</div>
    </div>
  </section>
</template>
