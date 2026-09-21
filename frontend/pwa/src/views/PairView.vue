<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { NetworkError } from '../api'
import type { Credential } from '../credentials'
import { codeFromLocation, pairDevice } from '../pairing'

const emit = defineEmits<{ paired: [credential: Credential] }>()
const code = ref(codeFromLocation(location.hash))
const deviceName = ref('')
const busy = ref(false)
const message = ref('')
const signal = { cancelled: false }
// Con la PWA ya abierta, escanear el QR solo cambia el fragmento (no recarga la pagina): hay que escucharlo.
const fromHash = () => { const scanned = codeFromLocation(location.hash); if (scanned && !busy.value) code.value = scanned }
onMounted(() => window.addEventListener('hashchange', fromHash))
onUnmounted(() => { signal.cancelled = true; window.removeEventListener('hashchange', fromHash) })

const MESSAGES: Record<string, string> = {
  denied: 'Solicitud denegada en el PC principal.',
  expired: 'La aprobacion no llego a tiempo. Pide un codigo nuevo.',
  invalid_code: 'Codigo no valido o caducado. Pide uno nuevo.',
  throttled: 'Demasiados intentos. Espera un minuto antes de volver a probar.',
}

async function submit(): Promise<void> {
  if (busy.value) return
  if (!code.value.trim() || deviceName.value.trim().length < 3) { message.value = 'Introduce el codigo y un nombre para este dispositivo (3 caracteres o mas).'; return }
  busy.value = true; message.value = 'Enviando solicitud…'
  try {
    const outcome = await pairDevice(code.value, deviceName.value, { fetcher: fetch, signal, onWaiting: () => { message.value = 'Solicitud enviada. Esperando la aprobacion en el PC principal…' } })
    if (outcome.kind === 'approved') emit('paired', { token: outcome.token, deviceName: deviceName.value.trim(), pairedAt: new Date().toISOString() })
    else message.value = MESSAGES[outcome.kind]
  } catch (error) {
    message.value = error instanceof NetworkError ? 'Servidor sin respuesta. Comprueba la Wi-Fi y vuelve a intentarlo.' : 'No se pudo completar el emparejamiento.'
  } finally { busy.value = false }
}
</script>

<template>
  <section data-testid="pair">
    <h2>Emparejar este dispositivo</h2>
    <p>En el PC principal, pestana <strong>Puestos</strong>, genera un codigo de un solo uso. Despues apruebalo alli con su rol y estacion.</p>
    <form @submit.prevent="submit">
      <label>Codigo de emparejamiento
        <input v-model="code" name="code" autocomplete="off" autocapitalize="off" spellcheck="false" :disabled="busy" />
      </label>
      <label>Nombre de este dispositivo
        <input v-model="deviceName" name="deviceName" autocomplete="off" placeholder="tablet-sala-1" maxlength="60" :disabled="busy" />
      </label>
      <button type="submit" :disabled="busy">Solicitar emparejamiento</button>
    </form>
    <p v-if="message" role="status" data-testid="pair-status">{{ message }}</p>
  </section>
</template>
