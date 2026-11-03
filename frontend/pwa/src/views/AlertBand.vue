<script setup lang="ts">
import type { TicketAlert } from '../cocina'
import Icon from './Icon.vue'

// F0.1 (plan por fases 03-11, corte 0.1; usabilidad 28-10, H34): la marca de una comanda cuyas alergias, o la decision del pase, cambiaron
// DESPUES de leerla (cocina.ts, AlertWatch). Se pinta igual en la rejilla de la estacion, en «Hecho» y en el pase: el titulo, cada cambio
// escrito entero con su tono (rojo si alarma, ambar si no) y «Visto», que la quita solo en esta tablet. Ninguna orden sale de aqui: el
// «Listo» y las decisiones siguen en su sitio. Sin alert no pinta nada.
defineProps<{ alert: TicketAlert | null; id: string }>()
defineEmits<{ seen: [] }>()
</script>

<template>
  <div v-if="alert" class="k-alert" :class="{ warn: !alert.lines.some(line => line.alarming) }" role="alert" :data-testid="`alert-${id}`">
    <p class="k-alert-t"><Icon name="alert" :size="22" :stroke="2.6" />Cambio en esta comanda</p>
    <ul class="k-alert-l">
      <li v-for="line in alert.lines" :key="line.text" class="k-al" :class="line.alarming ? 'alarm' : 'warn'"><Icon name="alert" :size="20" :stroke="2.4" /><span>{{ line.text }}</span></li>
    </ul>
    <button type="button" class="k-visto" :data-testid="`do-visto-${id}`" @click="$emit('seen')"><Icon name="check" :size="22" :stroke="2.8" />Visto</button>
  </div>
</template>
