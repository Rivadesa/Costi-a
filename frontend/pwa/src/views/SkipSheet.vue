<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { notApplied } from '../commands'
import { REASON_PROBLEM, reasonOk } from '../ficha'
import type { Settlement } from '../round'
import Icon from './Icon.vue'

// C3e (plan 2026-10-27, paso 3): omitir un pase, con su propia hoja. Antes el motivo era un campo compartido de «Más opciones», y se
// escribia para pausar u omitir sin saber para cual. Aqui el motivo es SOLO de este pase. La hoja no envia nada: «Omitir» devuelve el
// motivo y la ficha manda UNA orden skip con la version leida (F01), y solo si el servidor sigue anunciando omitir ese pase. Con borde
// de aviso y nunca en rojo: el rojo es de las alergias. Lo unico que se comprueba aqui es el formato del motivo (3 caracteres o mas).
// C3e.1 (usabilidad 28-10, H03): como la hoja de alergias, ya no se cierra al tocar «Omitir»: send manda la orden (TableCard) y la hoja
// espera su resultado con «Omitiendo…» en el boton apagado. Confirmada o sin confirmar, se cierra (commands.ts, notApplied); rechazada
// o sin salir, se queda con el motivo escrito y dice por que, en ambar («Postre NO se ha omitido: …»): ese pase sigue en la mesa.
const props = defineProps<{ courseName: string; locked: boolean; send: (reason: string) => Promise<Settlement> }>()
const emit = defineEmits<{ close: [] }>()

const reason = ref('')
const problem = ref('')
const field = ref<HTMLInputElement | null>(null)
// El foco entra en el motivo, que hace falta; al cerrar vuelve a lo que abrio la hoja (sin desplazar la pantalla).
const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
onMounted(() => field.value?.focus())
onBeforeUnmount(() => opener?.focus({ preventScroll: true }))

const sending = ref(false)
async function confirm(): Promise<void> {
  if (sending.value) return
  if (!reasonOk(reason.value)) { problem.value = REASON_PROBLEM; field.value?.focus(); return }
  problem.value = ''
  sending.value = true
  const courseName = props.courseName
  let settled: Settlement
  try { settled = await props.send(reason.value.trim()) } finally { sending.value = false }
  const why = notApplied(settled, courseName, 'omitido')
  if (why) problem.value = why; else emit('close')
}
</script>

<template>
  <div class="scrim" aria-hidden="true" @click="emit('close')"></div>
  <section class="sheet fc-sk" role="dialog" aria-modal="true" aria-labelledby="skip-title" aria-describedby="skip-text" data-testid="skip-sheet" @keydown.esc="emit('close')">
    <div class="grab" aria-hidden="true"></div>
    <div class="sheet-scroll">
      <h3 id="skip-title" class="sh-title">¿Omitir {{ props.courseName }}?</h3>
      <p id="skip-text" class="fc-sk-t">Ese pase no se envía a cocina y queda omitido en la mesa.</p>
      <div class="field">
        <label for="skip-reason">Motivo</label>
        <input id="skip-reason" ref="field" v-model="reason" name="skip-reason" maxlength="200" autocomplete="off" :disabled="locked" />
      </div>
      <p v-if="problem" class="notice fc-sk-problem" role="alert" data-testid="skip-problem"><Icon name="info" />{{ problem }}</p>
    </div>
    <div class="sheet-foot fc-sk-foot">
      <button type="button" class="btn" data-testid="skip-cancel" @click="emit('close')"><Icon name="back" />Volver</button>
      <button type="button" class="btn btn-caution" :disabled="locked || sending" data-testid="do-skip-confirm" @click="confirm">
        <Icon name="skip" />{{ sending ? 'Omitiendo…' : `Omitir ${props.courseName}` }}</button>
    </div>
  </section>
</template>
