<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { NetworkError } from '../api'
import type { Credential } from '../credentials'
import { pairPendingAdvice, pendingDeviceName } from '../labels'
import { codeFromLocation, pairDevice } from '../pairing'
import { indexedDbPendingStore } from '../pending'
import Icon from './Icon.vue'

const emit = defineEmits<{ paired: [credential: Credential] }>()
const code = ref(codeFromLocation(location.hash))
const deviceName = ref('')
const busy = ref(false)
const message = ref('')
const signal = { cancelled: false }
// Con la PWA ya abierta, escanear el QR solo cambia el fragmento (no recarga la pagina): hay que escucharlo.
const fromHash = () => { const scanned = codeFromLocation(location.hash); if (scanned && !busy.value) code.value = scanned }
// D6.7: si queda una orden guardada, el nombre viene relleno con el de esa orden. Solo con ESE nombre (mismo actor) y con el
// MISMO servidor (misma instalacion) se puede reintentar o preguntar por ella sin riesgo; aqui aun no se sabe el servidor,
// asi que el aviso lo dice de forma condicional. Con otro nombre u otro servidor, quedara bloqueada.
const pendingOwner = ref<{ name: string; description: string } | null>(null)
onMounted(async () => {
  window.addEventListener('hashchange', fromHash)
  pendingOwner.value = pendingDeviceName(await indexedDbPendingStore.load().catch(() => null))
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
    if (outcome.kind === 'approved') emit('paired', { token: outcome.token, deviceName: deviceName.value.trim(), pairedAt: new Date().toISOString() })
    else message.value = MESSAGES[outcome.kind]
  } catch (error) {
    message.value = error instanceof NetworkError ? 'Servidor sin respuesta. Comprueba la Wi-Fi y vuelve a intentarlo.' : 'No se pudo completar el emparejamiento.'
  } finally { busy.value = false }
}
</script>

<template>
  <section class="pair-card card" data-testid="pair">
    <h2><Icon name="mobile" :size="26" />Emparejar este dispositivo</h2>
    <p>En el PC principal, <strong>Inicio › Dispositivos</strong>, pulsa «Conectar comandero» o «Conectar pantalla de cocina» y lee el QR con la cámara (o teclea el código). El responsable aprueba allí la solicitud.</p>
    <div v-if="pendingOwner" class="pending" role="alert" data-testid="pair-pending">
      <h2><Icon name="alert" />ORDEN SIN CONFIRMAR</h2>
      <p>Hay una orden sin confirmar guardada en este dispositivo: <strong>{{ pendingOwner.description }}</strong>. {{ pairPendingAdvice(pendingOwner.name) }}</p>
    </div>
    <form @submit.prevent="submit">
      <div class="field">
        <label for="pair-code">Código de emparejamiento</label>
        <input id="pair-code" v-model="code" name="code" autocomplete="off" autocapitalize="off" spellcheck="false" :disabled="busy" />
      </div>
      <div class="field">
        <label for="pair-name">Nombre de este dispositivo</label>
        <input id="pair-name" v-model="deviceName" name="deviceName" autocomplete="off" placeholder="tablet-sala-1" maxlength="60" :disabled="busy" />
      </div>
      <button type="submit" class="btn btn-primary btn-big" :disabled="busy" data-testid="do-pair">Solicitar emparejamiento</button>
    </form>
    <p v-if="message" class="notice neutral" role="status" data-testid="pair-status">{{ message }}</p>
  </section>
</template>
