import { createApp } from 'vue'
import RouterApp from './RouterApp.vue'
import { startWorkers } from './worker'
import './style.css'

// C3c: /app/ es el ENRUTADOR (index.html). No tiene service worker propio: solo retira el de antes, de alcance /app/ (worker.ts).
createApp(RouterApp).mount('#app')
startWorkers(null)
