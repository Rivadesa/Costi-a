// Service worker de Costina. Escrito a mano y deliberadamente pequeno para poder auditarlo.
//
// REGLA: solo cachea el "shell" de la aplicacion (HTML, JS, CSS, icono, manifiesto) bajo /app/.
// JAMAS intercepta /api/, /health ni nada fuera de /app/: los datos operativos son siempre una
// lectura autoritativa del servidor o un error visible; una respuesta cacheada seria una mentira.
// No hay Background Sync: las ordenes pendientes se reintentan con la aplicacion en primer plano.
const CACHE = 'costina-shell-v1'

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(['/app/', '/app/manifest.webmanifest', '/app/icon.svg'])).then(() => self.skipWaiting()))
})

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key !== CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()))
})

self.addEventListener('fetch', event => {
  const request = event.request
  const url = new URL(request.url)
  if (request.method !== 'GET' || url.origin !== self.location.origin || !url.pathname.startsWith('/app/')) return
  if (url.pathname.startsWith('/app/assets/')) {
    // Ficheros con hash en el nombre: inmutables. Cache primero.
    event.respondWith(caches.match(request).then(hit => hit ?? fetch(request).then(response => {
      if (response.ok) { const copy = response.clone(); caches.open(CACHE).then(cache => cache.put(request, copy)) }
      return response
    })))
    return
  }
  // Shell (index.html, manifiesto, icono): red primero para recibir actualizaciones; cache si no hay red.
  event.respondWith(fetch(request).then(response => {
    if (response.ok) { const copy = response.clone(); caches.open(CACHE).then(cache => cache.put(request, copy)) }
    return response
  }).catch(() => caches.match(request).then(hit => hit ?? caches.match('/app/'))))
})
