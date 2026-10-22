import { createApp } from 'vue'
import RouterApp from './RouterApp.vue'
import { startWorkers } from './worker'
import './style.css'

// C3c: /app/ es el ENRUTADOR (index.html). No tiene service worker propio: solo retira el de antes, de alcance /app/ (worker.ts).
// Por eso un acceso directo viejo a /app/ («Costina») ya no abre sin red, ni por https: lo que abre sin red es el icono de cada aplicacion.
createApp(RouterApp).mount('#app')
startWorkers(null)
