<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { NetworkError } from '../api'
import { orphanOrder } from '../adoption'
import { appFits, APPS, currentBrowser, isIos, mismatchMessage, pairingPlace, pairingScreen, type AppId } from '../apps'
import type { Credential } from '../credentials'
import { pairPendingAdvice, pendingDeviceName } from '../labels'
import { codeFromLocation, pairDevice, unpairDevice } from '../pairing'
import { indexedDbPendingStore } from '../pending'
import AppIcon from './AppIcon.vue'
import Icon from './Icon.vue'

// C3c: emparejar DENTRO de una aplicacion. El codigo llega en el fragmento (el QR) o se teclea: el corto del PC en dos grupos
// («K7M2Q 9XR4T») vale con espacios, guiones y en minusculas (pairing.ts, typedCode). Si el PC aprueba el dispositivo para la OTRA
// aplicacion, no se guarda nada, se da de baja en el servidor y se dice que codigo hay que pedir. En iPhone y iPad, fuera del icono
// (en Safari), lo principal es explicar como anadir el icono, que guarda aparte; debajo, como salida de emergencia por si el icono no
// abre como aplicacion, un enlace secundario ensena aqui el mismo formulario (apps.ts, pairingScreen).
const props = defineProps<{ app: AppId }>()
const emit = defineEmits<{ paired: [credential: Credential] }>()
const definition = computed(() => APPS[props.app])
const browser = currentBrowser()
const place = pairingPlace(browser)
const inSafari = ref(false)
const screen = computed(() => pairingScreen(place, inSafari.value))
// Revision C3c: dentro del icono de iPhone o iPad no se manda a la camara (abriria Safari, que vuelve a explicar el icono): se teclea.
const typeOnly = isIos(browser)
const fromQr = codeFromLocation(location.hash)
const code = ref(place === 'here' ? fromQr : '')
const deviceName = ref('')
const busy = ref(false)
const message = ref('')
const signal = { cancelled: false }
// Con la PWA ya abierta, escanear el QR solo cambia el fragmento (no recarga la pagina): hay que escucharlo.
const fromHash = () => { const scanned = codeFromLocation(location.hash); if (scanned && !busy.value && screen.value !== 'add-to-home') code.value = scanned }
// Salida de emergencia: el codigo del QR que abrio Safari, si lo habia, va al campo al elegirla (como fuera de iOS). Volver a los
// pasos del icono no se puede con una solicitud en marcha. La pantalla nueva empieza arriba: el enlace estaba al pie de los pasos.
function pairInSafari(): void { inSafari.value = true; if (!code.value) code.value = fromQr }
function backToSteps(): void { if (busy.value) return; inSafari.value = false; message.value = '' }
watch(inSafari, () => { window.scrollTo({ top: 0 }) }, { flush: 'post' })
// D6.7: si queda una orden guardada, el nombre viene relleno con el de esa orden. Solo con ESE nombre (mismo actor) y con el
// MISMO servidor (misma instalacion) se puede reintentar o preguntar por ella sin riesgo; aqui aun no se sabe el servidor,
// asi que el aviso lo dice de forma condicional. Con otro nombre u otro servidor, quedara bloqueada.
// Revision C3c: con el hueco propio vacio, tambien la que quedo SIN credencial en la base vieja (un 401 en C3b): nada dice de que
// aplicacion era, asi que se ensena aqui y la toma la aplicacion que se empareja (adoption.ts, reconcileLegacy). Mirarla no la mueve.
const pendingOwner = ref<{ name: string; description: string } | null>(null)
onMounted(async () => {
  // En Safari el codigo del QR no se queda en la direccion (ni en lo que se anada a la pantalla de inicio). Solo la salida de
  // emergencia lo usa, si se elige, desde `fromQr`.
  if (place === 'add-to-home' && location.hash) history.replaceState(null, '', location.pathname)
  window.addEventListener('hashchange', fromHash)
  const own = await indexedDbPendingStore.load().catch(() => null)
  pendingOwner.value = pendingDeviceName(own?.kind === 'absent' ? await orphanOrder().catch(() => null) : own)
  if (pendingOwner.value && !deviceName.value) deviceName.value = pendingOwner.value.name
})
onUnmounted(() => { signal.cancelled = true; window.removeEventListener('hashchange', fromHash) })

const MESSAGES: Record<string, string> = {
  denied: 'Solicitud denegada en el PC principal.',
  expired: 'La aprobación no llegó a tiempo. Pide un código nuevo.',
  invalid_code: 'Código no válido o caducado. Pide uno nuevo.',
  throttled: 'Demasiados intentos. Espera un minuto antes de volver a probar.',
}

async function submit(): Promise<void> {
  if (busy.value) return
  if (!code.value.trim() || deviceName.value.trim().length < 3) { message.value = 'Introduce el código y un nombre para este dispositivo (3 caracteres o más).'; return }
  busy.value = true; message.value = 'Enviando solicitud…'
  try {
    const outcome = await pairDevice(code.value, deviceName.value, { fetcher: fetch, signal, onWaiting: () => { message.value = 'Solicitud enviada. Esperando la aprobación en el PC principal…' } })
    if (outcome.kind !== 'approved') message.value = MESSAGES[outcome.kind]
    else if (appFits(props.app, outcome.role)) emit('paired', { token: outcome.token, deviceName: deviceName.value.trim(), pairedAt: new Date().toISOString() })
    else {
      // Aprobado para la otra aplicacion: la credencial NO se guarda y el dispositivo se da de baja (nada huerfano en el servidor).
      const unpaired = await unpairDevice(fetch, outcome.token).catch(() => 'error' as const)
      message.value = mismatchMessage(props.app, outcome.role, unpaired)
      code.value = ''
    }
  } catch (error) {
    message.value = error instanceof NetworkError ? 'Servidor sin respuesta. Comprueba la Wi-Fi y vuelve a intentarlo.' : 'No se pudo completar el emparejamiento.'
  } finally { busy.value = false }
}
</script>

<template>
  <section v-if="screen === 'add-to-home'" class="pair-card card add-home" data-testid="add-to-home">
    <div class="add-home-brand"><AppIcon :app="app" size="l" /><strong>{{ definition.name }}</strong></div>
    <h2>Añádela a la pantalla de inicio</h2>
    <p>En iPhone y iPad se usa desde su icono y se empareja desde él: lo que guarda Safari no lo ve el icono.</p>
    <p class="add-home-key"><Icon name="share" :size="22" :stroke="2.2" />Compartir › Añadir a pantalla de inicio</p>
    <ol class="add-home-steps">
      <!-- Revision C3c: en iOS 26, con la barra compacta de Safari, Compartir esta dentro de «•••»; y su hoja trae «Abrir como app web». -->
      <li><span class="step-n" aria-hidden="true">1</span><span>Toca <strong>Compartir</strong> (en el iPhone, abajo; en el iPad, arriba). Si no lo ves, toca <strong>•••</strong> y después <strong>Compartir</strong>.</span></li>
      <li><span class="step-n" aria-hidden="true">2</span><span>Elige <strong>Añadir a pantalla de inicio</strong> (si no sale, desliza la lista hacia arriba). Si aparece <strong>Abrir como app web</strong>, déjalo activado. Toca <strong>Añadir</strong>.</span></li>
      <li><span class="step-n" aria-hidden="true">3</span><span>Abre el icono <strong>{{ definition.name }}</strong> y teclea el código corto que enseña el PC.</span></li>
    </ol>
    <p class="hint" data-testid="add-to-home-again">Si al abrir el icono vuelve a salir esta página, quita el icono y añádelo otra vez con «Abrir como app web» activado.</p>
    <p v-if="fromQr" class="hint" data-testid="add-to-home-code">El código de este QR no hace falta aquí. Si caduca mientras tanto, en el PC: «Generar otro código».</p>
    <div v-if="pendingOwner" class="pending" role="alert" data-testid="pair-pending">
      <h2><Icon name="alert" />ORDEN SIN CONFIRMAR</h2>
      <p>Hay una orden sin confirmar guardada en este dispositivo: <strong>{{ pendingOwner.description }}</strong>. {{ pairPendingAdvice(pendingOwner.name) }}</p>
    </div>
    <!-- Salida de emergencia (apps.ts, pairingScreen): al pie y con aspecto de enlace; lo principal siguen siendo los pasos del icono. -->
    <button type="button" class="btn btn-quiet way-out" data-testid="pair-in-safari" @click="pairInSafari">¿El icono no se abre como aplicación? Emparejar aquí, en Safari</button>
  </section>
  <section v-else class="pair-card card" data-testid="pair">
    <h2><Icon :name="app === 'cocina' ? 'chef' : 'mobile'" :size="26" />{{ definition.pairTitle }}</h2>
    <template v-if="screen === 'form-in-safari'">
      <p class="notice" data-testid="pair-in-safari-notice"><Icon name="info" />Costiña funcionará en esta pestaña de Safari. Si después usas el icono, tendrás que emparejarlo de nuevo desde el icono.</p>
      <p>En el PC principal, <strong>Inicio › Dispositivos</strong>, pulsa «Conectar {{ definition.name }}» y teclea aquí el «Código para teclear» que sale junto al QR. El responsable aprueba allí la solicitud.</p>
    </template>
    <p v-else-if="typeOnly">En el PC principal, <strong>Inicio › Dispositivos</strong>, pulsa «Conectar {{ definition.name }}» y teclea aquí el «Código para teclear» que sale junto al QR. En iPhone y iPad no uses la cámara: abriría Safari, no este icono. El responsable aprueba allí la solicitud.</p>
    <p v-else>En el PC principal, <strong>Inicio › Dispositivos</strong>, pulsa «Conectar {{ definition.name }}» y lee el QR con la cámara, o teclea aquí su código. El responsable aprueba allí la solicitud.</p>
    <div v-if="pendingOwner" class="pending" role="alert" data-testid="pair-pending">
      <h2><Icon name="alert" />ORDEN SIN CONFIRMAR</h2>
      <p>Hay una orden sin confirmar guardada en este dispositivo: <strong>{{ pendingOwner.description }}</strong>. {{ pairPendingAdvice(pendingOwner.name) }}</p>
    </div>
    <form @submit.prevent="submit">
      <div class="field">
        <label for="pair-code">Código del PC</label>
        <!-- Revision C3c: el codigo se teclea en mayusculas, como lo ensena el PC, y sin autocorreccion (en iOS spellcheck no la para). -->
        <input id="pair-code" v-model="code" name="code" class="code-input" autocomplete="off" autocorrect="off" autocapitalize="characters" spellcheck="false" enterkeyhint="next" :disabled="busy" />
      </div>
      <div class="field">
        <label for="pair-name">{{ definition.nameLabel }}</label>
        <input id="pair-name" v-model="deviceName" name="deviceName" autocomplete="off" :placeholder="definition.namePlaceholder" maxlength="60" :disabled="busy" />
        <p class="hint">Uno distinto en cada dispositivo: es el nombre con el que el PC lo reconoce.</p>
      </div>
      <button type="submit" class="btn btn-primary btn-big" :disabled="busy" data-testid="do-pair">Solicitar emparejamiento</button>
    </form>
    <p v-if="message" class="notice neutral" role="status" data-testid="pair-status">{{ message }}</p>
    <button v-if="screen === 'form-in-safari'" type="button" class="btn btn-quiet" data-testid="pair-in-safari-back" :disabled="busy" @click="backToSteps"><Icon name="back" />Volver a los pasos del icono</button>
  </section>
</template>
