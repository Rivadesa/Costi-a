<script setup lang="ts">
import { computed } from 'vue'
import { ICONS, type IconName } from '../icons'

// C3a: icono de trazo en linea (SVG en la plantilla, nunca una imagen). Siempre decorativo: el texto del boton o del aviso dice lo que
// es, asi que va oculto a los lectores de pantalla. Sin atributo style: la CSP del motor no admite estilos en linea.
const props = withDefaults(defineProps<{ name: IconName; size?: number; stroke?: number }>(), { size: 20, stroke: 2 })
const paths = computed(() => ICONS[props.name])
</script>

<template>
  <svg class="icon" :width="size" :height="size" viewBox="0 0 24 24" fill="none" stroke="currentColor" :stroke-width="stroke"
    stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
    <path v-for="(d, index) in paths" :key="index" :d="d" />
  </svg>
</template>
