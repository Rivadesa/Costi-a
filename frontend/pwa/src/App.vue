<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { ApiError, getSession, NetworkError, type Session } from './api'
import { clearCredential, loadCredential, saveCredential, type Credential } from './credentials'
import { MoneyLeakError } from './money-guard'
import { indexedDbPendingStore } from './pending'
import { requestPersistence, type Persistence } from './storage'
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

async function refresh(): Promise<void> {
  if (!credential.value) { state.value = 'unpaired'; return }
  const stored = await indexedDbPendingStore.load().catch(() => null)
  pendingDescription.value = stored?.kind === 'restored' ? stored.command.description : stored?.kind === 'unreadable' ? 'orden anterior ilegible' : ''
  try {
    session.value = await getSession(fetch, credential.value.token)
    state.value = 'ready'
    // Ya emparejado: lo guardado en este dispositivo (credencial, orden sin confirmar) merece almacenamiento persistente.
    if (!persistenceAsked) { persistenceAsked = true; persistence.value = await requestPersistence() }
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      // Emparejamiento revocado desde el PC principal: la credencial local deja de valer y se borra.
      await clearCredential()
      credential.value = null; session.value = null
      notice.value = 'Este dispositivo ya no esta emparejado (revocado). Pide un codigo nuevo en el PC principal.'
      state.value = 'unpaired'
    } else if (error instanceof NetworkError) {
      state.value = session.value ? 'ready' : 'offline'
    } else if (error instanceof MoneyLeakError) {
      notice.value = error.message; state.value = 'offline'
    } else throw error
  }
}

async function paired(next: Credential): Promise<void> {
  await saveCredential(next)
  credential.value = next; notice.value = ''
  history.replaceState(null, '', location.pathname)   // el codigo del fragmento ya no debe quedar a la vista
  await refresh()
}

async function unpair(): Promise<void> {
  await clearCredential()
  credential.value = null; session.value = null
  notice.value = 'Dispositivo desvinculado en este navegador. Para impedir su uso, revocalo tambien en el PC principal.'
  state.value = 'unpaired'
}

onMounted(async () => {
  credential.value = await loadCredential()
  await refresh()
  timer = window.setInterval(() => { void refresh() }, 15000)
  window.addEventListener('online', refresh)
})
onUnmounted(() => { window.clearInterval(timer); window.removeEventListener('online', refresh) })
</script>

<template>
  <main>
    <header>
      <h1>Costina</h1>
      <p v-if="session?.demo" class="demo" data-testid="demo">DEMOSTRACION · DATOS FICTICIOS</p>
    </header>
    <p v-if="notice" class="notice" role="status" data-testid="notice">{{ notice }}</p>
    <p v-if="state === 'loading'">Cargando…</p>
    <PairView v-else-if="state === 'unpaired'" @paired="paired" />
    <section v-else-if="state === 'offline'" data-testid="offline">
      <h2>Sin conexion con el servidor</h2>
      <p v-if="pendingDescription" class="pending" role="alert" data-testid="offline-pending"><strong>ORDEN SIN CONFIRMAR guardada en este dispositivo: {{ pendingDescription }}.</strong> No la repitas por otro medio: se reintentara, con su mismo identificador, al recuperar la conexion.</p>
      <p>Este dispositivo esta emparejado, pero el servidor no responde. Comprueba la Wi-Fi. No se muestra ningun dato hasta poder leerlo del servidor.</p>
      <button type="button" @click="refresh">Reintentar</button>
    </section>
    <HomeView v-else-if="session && credential" :session="session" :credential="credential" :persistence="persistence" @unpair="unpair" @refresh="refresh" />
  </main>
</template>
