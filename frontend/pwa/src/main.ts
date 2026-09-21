import { createApp } from 'vue'
import App from './App.vue'
import './style.css'

createApp(App).mount('#app')

// Service worker: solo cachea el "shell" de la aplicacion para poder abrirla sin red. JAMAS intercepta
// /api/: los datos operativos siempre son una lectura autoritativa del servidor o un error visible.
if ('serviceWorker' in navigator && import.meta.env.PROD) {
  window.addEventListener('load', () => { navigator.serviceWorker.register('/app/sw.js', { scope: '/app/' }).catch(() => undefined) })
}
