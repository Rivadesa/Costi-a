<script setup>
import packageInfo from '../../package.json';
const commit = (import.meta.env.VITE_BUILD_SHA || '').slice(0, 7);
</script>

<template>
  <small class="hint">Versión {{ packageInfo.version }} · Pruebas locales<span v-if="commit"> · {{ commit }}</span></small>
</template>
