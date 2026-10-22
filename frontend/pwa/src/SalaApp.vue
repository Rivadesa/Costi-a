<script setup lang="ts">
import BoardView from './views/BoardView.vue'
import Shell from './views/Shell.vue'

// C3c: Costina Sala, la aplicacion del movil de cada camarero: el marco comun (Shell) con el comandero (BoardView). Lectura en vivo y
// la orden incierta de siempre (operations.ts), sin importes.
</script>

<template>
  <Shell app="sala" v-slot="{ session, credential, unauthorized }">
    <BoardView :token="credential.token" :session="session" @unauthorized="unauthorized" />
  </Shell>
</template>
