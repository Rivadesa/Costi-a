<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { APP_IDS, APPS, type AppId } from './apps'
import { decideRoute, destination, type Route } from './router'
import AppIcon from './views/AppIcon.vue'
import Icon from './views/Icon.vue'

// C3c: /app/ es el ENRUTADOR (router.ts decide; aqui solo se pinta y se navega). No se instala: no lleva manifiesto. Con una
// credencial lleva a su aplicacion sin pintar nada; con un codigo (#pair=) pregunta para que es el dispositivo; sin nada, explica.
const route = ref<Route | { kind: 'loading' }>({ kind: 'loading' })
let timer: number | undefined
// replace: el codigo del fragmento no se queda en el historial del enrutador.
const go = (app: AppId, code = '') => location.replace(destination(app, code))

async function decide(): Promise<void> {
  const next = await decideRoute(location.hash, fetch)
  if (next.kind === 'go') { go(next.app); return }
  route.value = next
}
const code = () => route.value.kind === 'choose' ? route.value.code : ''

onMounted(async () => {
  // Con un QR escaneado con la pagina ya abierta solo cambia el fragmento; sin red, se reintenta solo.
  window.addEventListener('hashchange', decide)
  timer = window.setInterval(() => { if (route.value.kind === 'offline') void decide() }, 15000)
  await decide()
})
onUnmounted(() => { window.clearInterval(timer); window.removeEventListener('hashchange', decide) })
</script>

<template>
  <main class="app">
    <header class="topbar">
      <div class="brand"><span class="brand-mark" aria-hidden="true">C</span><h1>Costiña</h1></div>
    </header>
    <p v-if="route.kind === 'none' && route.notice" class="notice" role="status" data-testid="notice"><Icon name="info" />{{ route.notice }}</p>
    <p v-if="route.kind === 'loading'" class="muted">Cargando…</p>
    <section v-else-if="route.kind === 'choose'" class="pair-card card" data-testid="router-choose">
      <h2>¿Este dispositivo es para la sala o para la cocina?</h2>
      <p>El código del QR ya va puesto: elige la aplicación y sigue allí.</p>
      <div class="choices">
        <button v-for="app in APP_IDS" :key="app" type="button" class="choice" :data-testid="`route-${app}`" @click="go(app, code())">
          <AppIcon :app="app" size="m" /><span class="choice-t"><strong>{{ APPS[app].name }}</strong><span>{{ APPS[app].purpose }}</span></span><Icon name="right" />
        </button>
      </div>
    </section>
    <section v-else-if="route.kind === 'offline'" class="pair-card card offline" data-testid="router-offline">
      <h2><Icon name="wifiOff" :size="24" />Sin conexión con el servidor</h2>
      <p v-if="route.notice">{{ route.notice }}</p>
      <p>Este dispositivo estaba conectado con la versión anterior. Para saber si es de la sala o de la cocina hay que preguntar al servidor. Comprueba la Wi-Fi.</p>
      <button type="button" class="btn btn-primary btn-big" @click="decide"><Icon name="refresh" />Reintentar</button>
    </section>
    <section v-else-if="route.kind === 'unknown'" class="pair-card card" data-testid="router-unknown">
      <h2>Este dispositivo está conectado como {{ route.connected }}.</h2>
      <p>Ninguna de las dos aplicaciones sabe trabajar con ese tipo de dispositivo. Quítalo desde el PC principal (Inicio › Dispositivos) y conéctalo de nuevo con «Conectar {{ APPS.sala.name }}» o «Conectar {{ APPS.cocina.name }}».</p>
    </section>
    <section v-else class="pair-card card" data-testid="router-none">
      <h2><Icon name="mobile" :size="26" />Conectar este dispositivo</h2>
      <p>En el PC principal, <strong>Inicio › Dispositivos</strong>, pulsa «Conectar {{ APPS.sala.name }}» para el móvil de un camarero o «Conectar {{ APPS.cocina.name }}» para la tablet de una estación, y lee el QR con la cámara: abre la aplicación que toca.</p>
      <p>Si ya tienes el código, abre la aplicación y tecléalo allí:</p>
      <div class="choices">
        <button v-for="app in APP_IDS" :key="app" type="button" class="choice" :data-testid="`route-${app}`" @click="go(app)">
          <AppIcon :app="app" size="m" /><span class="choice-t"><strong>{{ APPS[app].name }}</strong><span>{{ APPS[app].purpose }}</span></span><Icon name="right" />
        </button>
      </div>
    </section>
  </main>
</template>
