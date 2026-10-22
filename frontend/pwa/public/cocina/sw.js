// Service worker de UNA aplicacion de Costina. C3c: el MISMO fichero en /app/sala/sw.js (Costina Sala) y en /app/cocina/sw.js
// (Costina Cocina); tests/install.test.ts comprueba que son identicos. Escrito a mano y deliberadamente pequeno para poder auditarlo.
//
// REGLA: solo cachea el "shell" de SU aplicacion: lo de su carpeta (pagina, manifiesto, iconos) y los ficheros con hash de
// /app/assets/ que piden sus paginas. JAMAS intercepta /api/, /health, el enrutador /app/ ni la carpeta de la otra aplicacion: los
// datos operativos son siempre una lectura autoritativa del servidor o un error visible; una respuesta cacheada seria una mentira.
// No hay Background Sync: las ordenes pendientes se reintentan con la aplicacion en primer plano.
// Su carpeta sale de su propia URL (/app/sala/sw.js -> /app/sala/, cache costina-sala-v1). Al activarse borra SOLO las caches de su
// prefijo (costina-sala-*) que no son la actual: nunca la de la otra aplicacion, que vive en el mismo origen. Y solo lee de SU cache
// (caches.match a secas buscaria tambien en la de la otra).
const SCOPE = self.location.pathname.slice(0, self.location.pathname.lastIndexOf('/') + 1)
const PREFIX = `costina-${SCOPE.split('/').filter(part => part).pop()}-`
const CACHE = `${PREFIX}v1`
const ASSETS = '/app/assets/'
const SHELL = [SCOPE, `${SCOPE}manifest.webmanifest`, `${SCOPE}icon.svg`, `${SCOPE}icon-192.png`, `${SCOPE}apple-touch-icon.png`]

self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(async cache => {
    await cache.addAll(SHELL)
    // Los ficheros con hash que pide su pagina (script, estilos y trozos compartidos), leidos de la pagina recien guardada: asi la
    // primera apertura sin red ya arranca. Si alguno falla se cacheara al pedirse (abajo).
    const page = await (await cache.match(SCOPE)).text()
    const assets = [...new Set(page.match(/\/app\/assets\/[A-Za-z0-9_.-]+/g) ?? [])]
    await Promise.all(assets.map(asset => cache.add(asset).catch(() => undefined)))
  }).then(() => self.skipWaiting()))
})

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(key => key.startsWith(PREFIX) && key !== CACHE).map(key => caches.delete(key))))
    .then(() => self.clients.claim()))
})

const keep = (request, response) => {
  if (response.ok) { const copy = response.clone(); caches.open(CACHE).then(cache => cache.put(request, copy)) }
  return response
}
const cached = request => caches.open(CACHE).then(cache => cache.match(request))

self.addEventListener('fetch', event => {
  const request = event.request
  const url = new URL(request.url)
  if (request.method !== 'GET' || url.origin !== self.location.origin) return
  if (url.pathname.startsWith(ASSETS)) {
    // Ficheros con hash en el nombre: inmutables. Cache primero.
    event.respondWith(cached(request).then(hit => hit ?? fetch(request).then(response => keep(request, response))))
    return
  }
  if (!url.pathname.startsWith(SCOPE)) return
  // Lo de su carpeta (pagina, manifiesto, iconos): red primero para recibir actualizaciones; su cache si no hay red.
  event.respondWith(fetch(request).then(response => keep(request, response))
    .catch(() => cached(request).then(hit => hit ?? cached(SCOPE))))
})
