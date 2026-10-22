import { createApp } from 'vue'
import { APPS } from './apps'
import CocinaApp from './CocinaApp.vue'
import { selectDatabase } from './db'
import { startWorkers } from './worker'
import './style.css'

// C3c: entrada de Costina Cocina (cocina/index.html -> /app/cocina/). Su base se elige ANTES de montar nada: la credencial y la orden
// incierta de esta aplicacion nunca se mezclan con las de Costina Sala, aunque en Android compartan origen.
selectDatabase(APPS.cocina.database)
createApp(CocinaApp).mount('#app')
startWorkers('cocina')
