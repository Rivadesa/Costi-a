<script setup>
import { computed, onMounted, ref } from 'vue';
import { api } from '../api/client.js';
import { euroInputToCents } from '../utils/money.js';

const catalog = ref({ products: [], categories: [], price_list: null });
const search = ref('');
const error = ref('');
const notice = ref('');
const loading = ref(false);
const busy = ref(false);
const editing = ref(null);
const pending = ref(null);
const units = { unit: 'Unidad', glass: 'Copa', bottle: 'Botella', portion: 'Ración', service: 'Servicio', other: 'Otro' };
const products = computed(() => catalog.value.products.filter(p => `${p.name} ${p.code} ${p.format_label || ''}`.toLocaleLowerCase().includes(search.value.toLocaleLowerCase())));
const price = cents => `${(cents / 100).toFixed(2)} €`;

async function load() {
  loading.value = true;
  try { catalog.value = (await api.catalogAdmin()).data; }
  catch (err) { error.value = err.message; }
  finally { loading.value = false; }
}
function edit(product = null) {
  if (pending.value) return;
  error.value = ''; notice.value = '';
  editing.value = product ? { ...product, amount: (product.price_cents / 100).toFixed(2) } : {
    id: null, revision: null, price_list_id: catalog.value.price_list?.id,
    code: '', name: '', category_id: '', sku: '', product_type: 'beverage', sale_unit: 'unit', format_label: '', amount: '', available: true,
  };
}
async function save() {
  error.value = ''; notice.value = '';
  if (!pending.value) {
    try {
      const p = editing.value;
      pending.value = { key: crypto.randomUUID(), body: {
        id: p.id, revision: p.revision, price_list_id: p.price_list_id, code: p.code.trim(), name: p.name.trim(),
        category_id: p.category_id || null, sku: p.sku?.trim() || null, product_type: p.product_type,
        sale_unit: p.sale_unit, format_label: p.format_label?.trim() || null,
        price_cents: euroInputToCents(p.amount), available: p.available,
      }};
    } catch (err) { error.value = err.message; return; }
  }
  busy.value = true;
  try {
    await api.catalogSave(pending.value.body, pending.value.key);
    pending.value = null; editing.value = null;
    notice.value = 'Artículo guardado. Las cuentas existentes conservan sus datos originales.';
    await load();
  } catch (err) {
    error.value = err.message;
    // Uncertain network/server failures retain exactly the original command/key.
    if (err.status >= 400 && err.status < 500) {
      pending.value = null;
      if (err.status === 409) { editing.value = null; await load(); }
    }
  } finally { busy.value = false; }
}
onMounted(load);
</script>

<template>
  <section class="page">
    <header class="page-header">
      <div><p class="eyebrow">Equipo principal · Configuración</p><h1>Catálogo del restaurante</h1><p class="muted">Tarifa: {{ catalog.price_list?.name || 'Sin configurar' }}</p></div>
      <div class="header-actions"><button class="button secondary" :disabled="loading || busy || Boolean(pending)" @click="load">Actualizar</button><button class="button primary" :disabled="!catalog.price_list || busy || Boolean(pending)" @click="edit()">Nuevo artículo</button></div>
    </header>
    <p v-if="error" class="error-box" role="alert">{{ error }}</p>
    <p v-if="notice" class="hint" role="status">{{ notice }}</p>
    <p class="hint">Copa y botella se configuran como presentaciones vendibles distintas. El nombre y formato pertenecen al catálogo compartido; la disponibilidad y el precio se aplican a esta tarifa.</p>
    <label>Buscar por nombre, código o formato<input v-model="search" type="search" placeholder="Agua, vino, copa…" /></label>
    <div class="panel catalog-scroll">
      <table class="catalog-table"><thead><tr><th>Artículo</th><th>Presentación</th><th>Precio</th><th>En esta tarifa</th><th>Acción</th></tr></thead>
        <tbody><tr v-for="p in products" :key="p.id"><td><strong>{{ p.name }}</strong><br /><small>{{ p.code }}</small></td><td>{{ units[p.sale_unit] }}<br />{{ p.format_label }}</td><td>{{ price(p.price_cents) }}</td><td>{{ p.available && p.product_active ? 'Disponible' : 'No disponible' }}</td><td><button class="button secondary" :disabled="busy || Boolean(pending)" @click="edit(p)">Editar</button></td></tr></tbody>
      </table>
      <p v-if="!products.length && !loading" class="muted">No hay artículos que coincidan.</p>
    </div>
    <div v-if="editing" class="modal-backdrop">
      <form class="modal-card stack catalog-form" @submit.prevent="save">
        <h2>{{ editing.id ? 'Editar artículo' : 'Nuevo artículo' }}</h2>
        <fieldset class="stack" :disabled="busy || Boolean(pending)">
          <label>Nombre<input v-model="editing.name" required maxlength="180" /></label>
          <label>Código<input v-model="editing.code" required maxlength="64" pattern="[A-Za-z0-9][A-Za-z0-9._-]*" /></label>
          <label>Categoría<select v-model="editing.category_id"><option value="">Sin categoría</option><option v-for="c in catalog.categories" :key="c.id" :value="c.id">{{ c.name }}</option></select></label>
          <label>Tipo<select v-model="editing.product_type"><option value="beverage">Bebida</option><option value="wine">Vino</option><option value="food">Comida</option><option value="extra">Extra</option><option value="other">Otro</option></select></label>
          <label>Presentación<select v-model="editing.sale_unit"><option v-for="(label, value) in units" :key="value" :value="value">{{ label }}</option></select></label>
          <label>Formato<input v-model="editing.format_label" maxlength="80" placeholder="125 ml, 75 cl…" /></label>
          <label>Precio (€)<input v-model="editing.amount" inputmode="decimal" required placeholder="4,50" /></label>
          <label><input v-model="editing.available" type="checkbox" /> Disponible para nuevas consumiciones en esta tarifa</label>
        </fieldset>
        <p v-if="pending && !busy" class="error-box">No se ha podido confirmar el guardado. Reintenta sin cambiar la operación; no se duplicará. Mantén esta ventana abierta.</p>
        <p v-if="error" class="error-box" role="alert">{{ error }}</p>
        <div class="modal-actions"><button type="button" class="button secondary" :disabled="busy || Boolean(pending)" @click="editing = null">Cancelar</button><button class="button primary" :disabled="busy">{{ busy ? 'Guardando…' : pending ? 'Reintentar el mismo guardado' : 'Guardar' }}</button></div>
      </form>
    </div>
  </section>
</template>

<style scoped>
.catalog-scroll { overflow-x: auto; margin-top: 20px; }
.catalog-table { width: 100%; border-collapse: collapse; text-align: left; }
.catalog-table th, .catalog-table td { padding: 12px; border-bottom: 1px solid #d0d5dd; }
.catalog-form { max-height: 90vh; overflow-y: auto; }
fieldset { border: 0; margin: 0; padding: 0; }
input[type="checkbox"] { width: auto; }
</style>
