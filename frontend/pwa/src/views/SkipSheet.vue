<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { REASON_PROBLEM, reasonOk } from '../ficha'
import Icon from './Icon.vue'

// C3e (plan 2026-10-27, paso 3): omitir un pase, con su propia hoja. Antes el motivo era un campo compartido de «Más opciones», y se
// escribia para pausar u omitir sin saber para cual. Aqui el motivo es SOLO de este pase. La hoja no envia nada: «Omitir» devuelve el
// motivo y la ficha manda UNA orden skip con la version leida (F01), y solo si el servidor sigue anunciando omitir ese pase. Con borde
// de aviso y nunca en rojo: el rojo es de las alergias. Lo unico que se comprueba aqui es el formato del motivo (3 caracteres o mas).
const props = defineProps<{ courseName: string; locked: boolean }>()
const emit = defineEmits<{ confirm: [reason: string]; close: [] }>()

const reason = ref('')
const problem = ref('')
const field = ref<HTMLInputElement | null>(null)
// El foco entra en el motivo, que hace falta; al cerrar vuelve a lo que abrio la hoja (sin desplazar la pantalla).
const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
onMounted(() => field.value?.focus())
onBeforeUnmount(() => opener?.focus({ preventScroll: true }))

function confirm(): void {
  if (!reasonOk(reason.value)) { problem.value = REASON_PROBLEM; field.value?.focus(); return }
  problem.value = ''
  emit('confirm', reason.value.trim())
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
      <button type="button" class="btn btn-caution" :disabled="locked" data-testid="do-skip-confirm" @click="confirm"><Icon name="skip" />Omitir {{ props.courseName }}</button>
    </div>
  </section>
</template>
