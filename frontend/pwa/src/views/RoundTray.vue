<script setup lang="ts">
import { computed } from 'vue'
import { EMPTY_ROUND, forLabel, ROUND_HINT, ROUND_LOCKED, roundNames, sendLabel, trayKey } from '../bodega'
import { MAX_QUANTITY, roundLineText, type Round } from '../round'
import Icon from './Icon.vue'

// C3b: la bandeja de la RONDA, abajo («Ronda · n sin enviar» y «Enviar»), en «Bodega y bebidas» y, si tiene algo, tambien en la ficha
// de la mesa, como en la maqueta («2 · Bodega y bebidas» y «2b · La ronda»). La ronda es un BORRADOR EN MEMORIA (round.ts): se puede
// llenar y cambiar aunque haya una orden sin confirmar, pero «Enviar» solo se habilita sin ninguna (locked, D6.7); con una incierta o
// bloqueada (uncertain) la bandeja dice por que. Tocando el resumen se abre la lista: − y + (en 1, la papelera quita), y «Para: …», que
// abre la hoja de la linea (cantidad y comensal) en la ficha. En la ficha (embedded) el envio es «Enviar ronda», en verde claro y sin el
// icono de enviar: el unico boton verde lleno de la ficha, y el unico con ese icono, es el grande.
// Los botones que cambian el borrador no son ordenes al servidor: su data-testid empieza por «round-»; el unico «do-» es «Enviar».
const props = defineProps<{ round: Round; locked: boolean; uncertain: boolean; open: boolean; embedded?: boolean; message: string; sent: string; badLine: number | null }>()
const emit = defineEmits<{ send: []; edit: [index: number]; 'update:open': [open: boolean] }>()

const canSend = computed(() => props.round.canSend(props.locked))
const again = computed(() => props.round.conflict)
</script>

<template>
  <div :class="embedded ? 'tray-in' : 'bar tray'" data-testid="round">
    <p v-if="round.lines.length === 0" class="tray-empty" role="status" data-testid="round-empty">{{ sent || EMPTY_ROUND }}</p>
    <template v-else>
      <template v-if="open">
        <p class="tray-hint">{{ ROUND_HINT }}</p>
        <ul class="tray-list">
          <li v-for="(line, index) in round.lines" :key="trayKey(line, index)" :class="{ bad: badLine === index }" :data-testid="`round-line-${index}`">
            <span class="tl-n">
              <b>{{ roundLineText(line) }}</b>
              <button type="button" class="para" :disabled="round.sending" :data-testid="`round-for-${index}`"
                :aria-label="`Cambiar cantidad y para quién: ${roundLineText(line)}, ${forLabel(line.guestPosition)}`" @click="emit('edit', index)">
                {{ forLabel(line.guestPosition) }}<Icon name="down" :size="16" :stroke="2.2" /></button>
            </span>
            <span class="mini-step">
              <button type="button" :disabled="round.sending" :data-testid="`round-minus-${index}`"
                :aria-label="line.quantity === 1 ? `Quitar ${roundLineText(line)}` : `Uno menos de ${roundLineText(line)}`" @click="round.decrement(index)">
                <Icon :name="line.quantity === 1 ? 'trash' : 'minus'" :size="18" :stroke="2.2" /></button>
              <b :data-testid="`round-qty-${index}`">{{ line.quantity }}</b>
              <button type="button" :disabled="round.sending || line.quantity >= MAX_QUANTITY" :data-testid="`round-plus-${index}`"
                :aria-label="`Uno más de ${roundLineText(line)}`" @click="round.increment(index)"><Icon name="plus" :size="18" :stroke="2.4" /></button>
            </span>
          </li>
        </ul>
      </template>
      <p v-if="message" class="tray-problem" role="alert" data-testid="round-problem"><Icon name="info" />{{ message }}</p>
      <p v-if="uncertain" class="tray-problem tray-locked" role="status" data-testid="round-locked"><Icon name="info" />{{ ROUND_LOCKED }}</p>
      <div class="tray-row" :class="{ again }">
        <button type="button" class="tray-sum" :aria-expanded="open" data-testid="round-toggle" @click="emit('update:open', !open)">
          <span class="tray-txt"><b data-testid="round-summary"><span class="unsent" aria-hidden="true"></span>{{ round.summary }}</b><small>{{ roundNames(round.lines) }}</small></span>
          <Icon :name="open ? 'down' : 'up'" :size="22" />
        </button>
        <button type="button" class="btn btn-send" :class="embedded ? 'btn-soft' : 'btn-primary'" :disabled="!canSend" data-testid="do-send-round" @click="emit('send')">
          <Icon v-if="!embedded" name="send" />{{ sendLabel(round, embedded === true) }}</button>
      </div>
    </template>
  </div>
</template>
