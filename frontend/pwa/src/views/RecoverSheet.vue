<script setup lang="ts">
import { onMounted, ref } from 'vue'
import Icon from './Icon.vue'

// F0.2 (plan por fases 03-11, corte 0.2; usabilidad 28-10, H01; decision 8b): la pregunta al volver a una mesa con algo guardado en este
// movil (borrador.ts): la ronda sin enviar o la mesa a medio abrir. Misma hoja que «¿Salir sin enviar la ronda?» y «¿Salir sin abrir la
// mesa?»: titulo, lo guardado escrito entero y dos botones; el de recuperar es la opcion segura y recibe el foco. Nada se recupera ni
// se borra sin uno de los dos toques, y ninguna orden sale de aqui. El velo no cierra: hay que decidir.
defineProps<{ testId: string; title: string; text: string; lines: Array<{ text: string; alarming: boolean }>; yes: string; no: string }>()
defineEmits<{ yes: []; no: [] }>()
const first = ref<HTMLButtonElement | null>(null)
onMounted(() => first.value?.focus())
</script>

<template>
  <div class="scrim" aria-hidden="true"></div>
  <section class="sheet" role="alertdialog" aria-modal="true" :aria-labelledby="`${testId}-title`" :aria-describedby="`${testId}-text`" :data-testid="`${testId}-sheet`">
    <div class="sheet-scroll">
      <h3 :id="`${testId}-title`" class="sh-title">{{ title }}</h3>
      <p :id="`${testId}-text`" class="leave-t" :data-testid="`${testId}-text`">{{ text }}</p>
      <ul v-if="lines.length > 0" class="open-lines" aria-label="Lo guardado en este móvil">
        <li v-for="(line, index) in lines" :key="index" class="fc-tl-x open-line" :class="line.alarming ? 'alarm' : 'warn'"><Icon name="alert" :size="16" :stroke="2.3" /><span>{{ line.text }}</span></li>
      </ul>
    </div>
    <div class="sheet-foot">
      <button type="button" class="btn btn-caution" :data-testid="`${testId}-no`" @click="$emit('no')">{{ no }}</button>
      <button ref="first" type="button" class="btn btn-primary" :data-testid="`${testId}-yes`" @click="$emit('yes')">{{ yes }}</button>
    </div>
  </section>
</template>
