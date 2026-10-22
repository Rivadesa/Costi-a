<script setup lang="ts">
import KitchenView from './views/KitchenView.vue'
import Shell from './views/Shell.vue'

// C3c: Costina Cocina, la aplicacion de la tablet fija de cada estacion y del pase: el marco comun (Shell) con su pantalla por
// estacion (KitchenView, D6.4). Misma lectura en vivo y misma orden incierta, sin importes.
</script>

<template>
  <Shell app="cocina" v-slot="{ session, credential, unauthorized }">
    <KitchenView :token="credential.token" :session="session" @unauthorized="unauthorized" />
  </Shell>
</template>
