<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { getSession, type Session } from './api'
import { clearCredential, loadCredential, saveCredential, type Credential } from './credentials'
import { deviceNameOf } from './labels'
import { unpairDevice } from './pairing'
import { indexedDbPendingStore } from './pending'
import { NOT_READY_PREFIX, outcomeOf } from './startup'
import { requestPersistence, type Persistence } from './storage'
import Icon from './views/Icon.vue'
import PairView from './views/PairView.vue'
import HomeView from './views/HomeView.vue'

// Estado de arranque: 'loading' -> 'unpaired' | 'ready' | 'offline' (emparejado pero sin servidor).
const state = ref<'loading' | 'unpaired' | 'ready' | 'offline'>('loading')
const credential = ref<Credential | null>(null)
const session = ref<Session | null>(null)
const notice = ref('')
const pendingDescription = ref('')   // tambien sin conexion: lo no confirmado se ve SIEMPRE
const persistence = ref<Persistence>('persisted')   // hasta saberlo no se alarma a nadie
let persistenceAsked = false
let timer: number | undefined
// A11: la cabecera dice QUE es este dispositivo y DONDE esta, con el nombre de la estacion (no su codigo). C3a: las palabras del puesto
// (Labels.DeviceRoles) y el nombre del dispositivo sin el prefijo tecnico del actor. Un papel que no se conoce sale tal cual.
const KINDS: Record<string, string> = { main: 'Principal (antiguo)', service: 'Comandero', kitchen: 'Pantalla de cocina' }
const role = computed(() => session.value === null ? ''
  : [KINDS[session.value.role] ?? session.value.role, session.value.stationName || session.value.station || ''].filter(part => part).join(' · '))

async function refresh(): Promise<void> {
  // F8: ni IndexedDB que no abre ni un servidor que no esta listo dejan la pantalla en "Cargando…": se dice y se reintenta solo.
  try { if (!credential.value) credential.value = await loadCredential() }
  catch (error) { state.value = 'offline'; notice.value = outcomeOf(error, false).notice; return }
  if (!credential.value) { state.value = 'unpaired'; return }
  const stored = await indexedDbPendingStore.load().catch(() => null)
  pendingDescription.value = stored?.kind === 'restored' ? stored.command.description : stored?.kind === 'unreadable' ? 'orden anterior ilegible' : ''
  try {
    session.value = await getSession(fetch, credential.value.token)
    state.value = 'ready'
    if (notice.value.startsWith(NOT_READY_PREFIX)) notice.value = ''
    // Ya emparejado: lo guardado en este dispositivo (credencial, orden sin confirmar) merece almacenamiento persistente.
    if (!persistenceAsked) { persistenceAsked = true; persistence.value = await requestPersistence() }
  } catch (error) {
    const outcome = outcomeOf(error, session.value !== null)
    if (outcome.state === 'unpaired') {
      // La credencial ya no vale en el servidor (quitado, desvinculado o copia restaurada): se borra y se explica.
      await clearCredential()
      credential.value = null; session.value = null
    }
    if (outcome.notice) notice.value = outcome.notice
    state.value = outcome.state
  }
}

async function paired(next: Credential): Promise<void> {
  await saveCredential(next)
  credential.value = next; notice.value = ''
  history.replaceState(null, '', location.pathname)   // el codigo del fragmento ya no debe quedar a la vista
  await refresh()
}

// Desvincular (F9): primero en el servidor, despues en este navegador. Sin conexion no se olvida nada.
async function unpair(): Promise<void> {
  if (!credential.value) return
  const outcome = await unpairDevice(fetch, credential.value.token)
  if (outcome === 'offline') {
    notice.value = 'Sin conexión con el servidor: el dispositivo NO se ha desvinculado. Vuelve a intentarlo con Wi-Fi, o quítalo desde el PC principal.'
    return
  }
  await clearCredential()
  credential.value = null; session.value = null
  notice.value = outcome === 'unpaired' ? 'Dispositivo desvinculado: ha dejado de valer también en el servidor. Para volver a usarlo, conéctalo de nuevo desde el PC principal.'
    : 'Este dispositivo ya no estaba emparejado en el servidor.'
  state.value = 'unpaired'
}

onMounted(async () => {
  // F8: el temporizador y el aviso 'online' se instalan ANTES de la primera lectura: un servidor que aun arranca no bloquea nada.
  timer = window.setInterval(() => { void refresh() }, 15000)
  window.addEventListener('online', refresh)
  await refresh()
})
onUnmounted(() => { window.clearInterval(timer); window.removeEventListener('online', refresh) })
</script>

<template>
  <main class="app">
    <header class="topbar">
      <div class="brand"><span class="brand-mark" aria-hidden="true">C</span><h1>Costina</h1></div>
      <span v-if="session?.demo" class="demo" data-testid="demo">DEMOSTRACIÓN · DATOS FICTICIOS</span>
      <p v-if="state === 'ready' && session" class="who">
        <span class="who-role" data-testid="role">{{ role }}</span><span class="who-name" data-testid="actor">{{ deviceNameOf(session.actor) }}</span>
      </p>
    </header>
    <p v-if="notice" class="notice" role="status" data-testid="notice"><Icon name="info" />{{ notice }}</p>
    <p v-if="state === 'loading'" class="muted">Cargando…</p>
    <PairView v-else-if="state === 'unpaired'" @paired="paired" />
    <section v-else-if="state === 'offline'" class="pair-card offline" data-testid="offline">
      <h2><Icon name="wifiOff" :size="24" />Sin conexión con el servidor</h2>
      <!-- D6.7: sin conexion aun no se sabe si se puede reenviar (otra instalacion, otro nombre, ilegible): no se promete un reintento que quiza no ocurra. -->
      <div v-if="pendingDescription" class="pending" role="alert" data-testid="offline-pending">
        <h2><Icon name="alert" />ORDEN SIN CONFIRMAR</h2>
        <p><strong>Guardada en este dispositivo: {{ pendingDescription }}.</strong> No la repitas por otro medio. Al recuperar la conexión, si se puede reenviar sin riesgo (misma instalación y mismo nombre de dispositivo) se reintentará sola con su mismo identificador; si no, quedará bloqueada hasta comprobarla.</p>
      </div>
      <p>Este dispositivo está emparejado, pero el servidor no responde. Comprueba la Wi-Fi. No se muestra ningún dato hasta poder leerlo del servidor.</p>
      <button type="button" class="btn btn-primary btn-big" @click="refresh"><Icon name="refresh" />Reintentar</button>
    </section>
    <HomeView v-else-if="session && credential" :session="session" :credential="credential" :persistence="persistence" @unpair="unpair" @refresh="refresh" />
  </main>
</template>
