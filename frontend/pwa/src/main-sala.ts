import { createApp } from 'vue'
import { APPS } from './apps'
import { selectDatabase } from './db'
import SalaApp from './SalaApp.vue'
import { startWorkers } from './worker'
import './style.css'

// C3c: entrada de Costina Sala (sala/index.html -> /app/sala/). Su base se elige ANTES de montar nada: la credencial y la orden
// incierta de esta aplicacion nunca se mezclan con las de Costina Cocina, aunque en Android compartan origen.
selectDatabase(APPS.sala.database)
createApp(SalaApp).mount('#app')
startWorkers('sala')
