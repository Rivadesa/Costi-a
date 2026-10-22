// C3c: RETIRADA del service worker de antes (alcance /app/, cache costina-shell-v1). Con dos aplicaciones en /app/sala/ y
// /app/cocina/, cada una con el suyo (public/sala/sw.js), uno de alcance /app/ las cubriria tambien a ellas. El navegador busca
// actualizaciones de este fichero al navegar por las paginas que el viejo controla: recibe esto, que no intercepta NADA (sin
// manejador fetch, todo va a la red), borra la cache vieja y se da de baja. Ademas, el enrutador y cada aplicacion dan de baja
// cualquier registro de alcance exacto /app/ (src/worker.ts). No toca /api/ ni ninguna otra cache.
const OLD_CACHE = 'costina-shell-v1'

self.addEventListener('install', () => self.skipWaiting())

self.addEventListener('activate', event => {
  event.waitUntil(caches.delete(OLD_CACHE).then(() => self.registration.unregister()))
})
