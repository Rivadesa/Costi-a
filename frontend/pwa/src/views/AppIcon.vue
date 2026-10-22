<script setup lang="ts">
import { computed } from 'vue'
import { iconBody } from '../app-icons'
import type { AppId } from '../apps'

// C3c: el icono de cada aplicacion, el MISMO dibujo que su icon.svg y sus PNG (app-icons.ts), pintado como SVG dentro de la pagina:
// no es una imagen ni un estilo en linea, asi que la CSP del motor no cambia. El cuerpo sale de app-icons.ts, sin datos de nadie.
// Siempre decorativo: al lado va el nombre escrito. El tamano, por clase (ai-s, ai-m, ai-l).
const props = withDefaults(defineProps<{ app: AppId; size?: 's' | 'm' | 'l' }>(), { size: 's' })
const body = computed(() => iconBody(props.app))
</script>

<template>
  <svg class="app-icon" :class="`ai-${size}`" viewBox="0 0 96 96" aria-hidden="true" focusable="false" v-html="body"></svg>
</template>
