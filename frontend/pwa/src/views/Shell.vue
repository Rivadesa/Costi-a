<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { getSession, type Session } from '../api'
import { lookAround } from '../adoption'
import { appFits, APPS, headerRole, type AppId } from '../apps'
import { clearCredential, loadCredential, saveCredential, type Credential } from '../credentials'
import { currentDatabase } from '../db'
import { deviceNameOf } from '../labels'
import { unpairDevice } from '../pairing'
import { indexedDbPendingStore } from '../pending'
import { NOT_READY_PREFIX, outcomeOf } from '../startup'
import { requestPersistence, type Persistence } from '../storage'
import AppIcon from './AppIcon.vue'
import HomeView from './HomeView.vue'
import Icon from './Icon.vue'
import OtherApp from './OtherApp.vue'
import PairView from './PairView.vue'

// C3c: el marco COMUN de Costina Sala y Costina Cocina, que recibe la aplicacion como dato (lo que era App.vue): arranque,
// emparejamiento, desvincular, sin conexion y la cabecera con su icono y su nombre. Cada aplicacion pone su pantalla en el hueco.
// Estado de arranque: 'loading' -> 'unpaired' | 'ready' | 'offline' (emparejado pero sin servidor) | 'other' (conectado como la
// otra aplicacion, o con un papel que ninguna conoce: no se monta nada).
const props = defineProps<{ app: AppId }>()
const definition = computed(() => APPS[props.app])
const state = ref<'loading' | 'unpaired' | 'ready' | 'offline' | 'other'>('loading')
const credential = ref<Credential | null>(null)
const session = ref<Session | null>(null)
const other = ref<{ session: Session; credential: Credential; database: string } | null>(null)
const notice = ref('')
const pendingDescription = ref('')   // tambien sin conexion: lo no confirmado se ve SIEMPRE
const persistence = ref<Persistence>('persisted')   // hasta saberlo no se alarma a nadie
let persistenceAsked = false
let lookedAround = false             // la base vieja y la de la otra aplicacion se miran una vez (otra si no se pudo decidir)
let timer: number | undefined
// A11: la cabecera dice QUE es este dispositivo (la marca, con el nombre de la aplicacion) y DONDE esta (el NOMBRE de la estacion);
// el `main` antiguo lo dice delante, como siempre. Un papel que no se conoce no llega aqui: no se monta.
const role = computed(() => session.value === null ? '' : headerRole(props.app, session.value))

async function refresh(): Promise<void> {
  // F8: ni IndexedDB que no abre ni un servidor que no esta listo dejan la pantalla en "Cargando…": se dice y se reintenta solo.
  try { if (!credential.value) credential.value = await loadCredential() }
  catch (error) { state.value = 'offline'; notice.value = outcomeOf(error, false).notice; return }
  if (!credential.value && other.value) { state.value = 'other'; return }
  if (!credential.value && !lookedAround) {
    // C3c (adoption.ts): antes de pedir que se empareje, lo que ya hay en este origen: la credencial de antes, si es de esta
    // aplicacion, se adopta; la de la otra aplicacion (o vieja de otro rol) hace que esta no monte nada. Sin servidor no se decide.
    try {
      const found = await lookAround(props.app, fetch)
      lookedAround = true
      if (found.kind === 'adopted') credential.value = found.credential
      if (found.kind === 'other') { other.value = found; state.value = 'other'; return }
    } catch (error) {
      const outcome = outcomeOf(error, false)
      state.value = 'offline'; notice.value = outcome.notice; return
    }
  }
  if (!credential.value) { state.value = 'unpaired'; return }
  const stored = await indexedDbPendingStore.load().catch(() => null)
  pendingDescription.value = stored?.kind === 'restored' ? stored.command.description : stored?.kind === 'unreadable' ? 'orden anterior ilegible' : ''
  try {
    const read = await getSession(fetch, credential.value.token)
    if (!appFits(props.app, read.role)) {
      // Una credencial de la otra aplicacion en esta base no deberia existir (adoptar y emparejar comprueban el rol): no se monta nada.
      other.value = { session: read, credential: credential.value, database: currentDatabase() }
      credential.value = null; session.value = null; state.value = 'other'; return
    }
    session.value = read
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

// Desvincular (F9): primero en el servidor, despues en este navegador. Sin conexion no se olvida nada. Vale tambien para la credencial
// de la otra aplicacion (se borra de SU base; la base no se borra nunca).
async function unpair(): Promise<void> {
  const target = credential.value ? { token: credential.value.token, database: currentDatabase() }
    : other.value ? { token: other.value.credential.token, database: other.value.database } : null
  if (!target) return
  const outcome = await unpairDevice(fetch, target.token)
  if (outcome === 'offline') {
    notice.value = 'Sin conexión con el servidor: el dispositivo NO se ha desvinculado. Vuelve a intentarlo con Wi-Fi, o quítalo desde el PC principal.'
    return
  }
  await clearCredential(target.database)
  credential.value = null; session.value = null; other.value = null; lookedAround = false
  notice.value = outcome === 'unpaired' ? 'Dispositivo desvinculado: ha dejado de valer también en el servidor. Para volver a usarlo, conéctalo de nuevo desde el PC principal.'
    : 'Este dispositivo ya no estaba emparejado en el servidor.'
  await refresh()
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
  <main class="app" :data-app="app">
    <header class="topbar">
      <div class="brand"><AppIcon :app="app" /><h1 data-testid="app-name">{{ definition.name }}</h1></div>
      <span v-if="session?.demo" class="demo" data-testid="demo">DEMOSTRACIÓN · DATOS FICTICIOS</span>
      <p v-if="state === 'ready' && session" class="who">
        <span class="who-role" data-testid="role">{{ role }}</span><span class="who-name" data-testid="actor">{{ deviceNameOf(session.actor) }}</span>
      </p>
    </header>
    <p v-if="notice" class="notice" role="status" data-testid="notice"><Icon name="info" />{{ notice }}</p>
    <p v-if="state === 'loading'" class="muted">Cargando…</p>
    <PairView v-else-if="state === 'unpaired'" :app="app" @paired="paired" />
    <OtherApp v-else-if="state === 'other' && other" :app="app" :session="other.session" :database="other.database" @unpair="unpair" />
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
    <HomeView v-else-if="session && credential" :session="session" :persistence="persistence" @unpair="unpair">
      <slot :session="session" :credential="credential" :unauthorized="refresh" />
    </HomeView>
  </main>
</template>
