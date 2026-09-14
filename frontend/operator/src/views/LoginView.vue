<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, ApiError } from '../api/client.js';
import { DEMO_API_BASE, resetDemo } from '../api/demo.js';
import { saveDeviceSettings, session } from '../state/session.js';

const router = useRouter();
const email = ref('');
const password = ref('');
const apiBase = ref(session.apiBase);
const companyId = ref(session.companyId);
const locationId = ref(session.locationId);
const showAdvanced = ref(false);
const busy = ref(false);
const demoBusy = ref(false);
const error = ref('');

async function submit() {
  busy.value = true;
  error.value = '';
  saveDeviceSettings({ apiBase: apiBase.value, companyId: companyId.value, locationId: locationId.value });
  try {
    await api.login({ email: email.value, password: password.value });
    await router.replace('/service');
  } catch (err) {
    if (err instanceof ApiError && err.code === 'authentication_failed' && /company_id|location_id/i.test(err.message)) {
      showAdvanced.value = true;
      error.value = 'Este servidor todavía no tiene fijados empresa y local. Indícalos en configuración avanzada.';
    } else {
      error.value = err.message || 'No fue posible iniciar sesión.';
    }
  } finally {
    busy.value = false;
  }
}

async function enterDemo() {
  demoBusy.value = true;
  error.value = '';
  try {
    resetDemo();
    saveDeviceSettings({ apiBase: DEMO_API_BASE, companyId: 'demo-company', locationId: 'demo-location' });
    apiBase.value = DEMO_API_BASE;
    companyId.value = 'demo-company';
    locationId.value = 'demo-location';
    await api.login({ email: 'demo@hospitality.local', password: 'demo' });
    await router.replace('/service');
  } catch (err) {
    error.value = err.message || 'No fue posible iniciar el modo demo.';
  } finally {
    demoBusy.value = false;
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-card">
      <div class="brand-mark">H</div>
      <p class="eyebrow">Hospitality OS</p>
      <h1>Acceso al servicio</h1>
      <p class="muted">Conexión directa con el servidor local del establecimiento.</p>

      <form class="stack" @submit.prevent="submit">
        <label>Correo<input v-model.trim="email" type="email" autocomplete="username" required /></label>
        <label>Contraseña<input v-model="password" type="password" autocomplete="current-password" required /></label>
        <p v-if="error" class="error-box">{{ error }}</p>
        <button class="button primary large" :disabled="busy || demoBusy">{{ busy ? 'Entrando…' : 'Entrar' }}</button>
      </form>

      <div class="demo-entry">
        <span class="muted">¿Quieres probar la aplicación sin servidor?</span>
        <button class="button secondary large" type="button" :disabled="busy || demoBusy" @click="enterDemo">{{ demoBusy ? 'Preparando demo…' : 'Entrar en modo demo' }}</button>
        <small class="hint">Carga 8 mesas, dos menús, varias partidas de cocina y tres servicios en curso. Los cambios se guardan solo en este equipo.</small>
      </div>

      <button class="link-button" type="button" @click="showAdvanced = !showAdvanced">Configuración del terminal</button>
      <div v-if="showAdvanced" class="advanced-panel stack">
        <label>Servidor API<input v-model.trim="apiBase" placeholder="http://hospitality.local/api/v1" /></label>
        <p class="hint">Empresa y local solo son necesarios durante desarrollo o si el servidor no tiene un ámbito fijo.</p>
        <label>Empresa (ID técnico)<input v-model.trim="companyId" /></label>
        <label>Local (ID técnico)<input v-model.trim="locationId" /></label>
      </div>
    </section>
  </main>
</template>
