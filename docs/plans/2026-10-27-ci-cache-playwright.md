# Plan CI: los paquetes de Playwright en caché (job `postgres-http` de D1)

Fecha: 2026-10-27. **Estado: APROBADO** el 27-10: «Adelante» y «también la caché» (permiso para subir y mezclar). Solo toca la CI: ni el código del producto, ni las pruebas, ni el instalador.

## Qué pasa

El paso «D6.1 PWA hosted…» de `.github/workflows/dotnet-d1.yml` empieza con `npx playwright install --with-deps chromium`. Ese comando hace dos cosas: instala con apt los paquetes del sistema que pide Chromium y baja el navegador. Medido en los logs de las últimas siete ejecuciones de D1 que llegaron a ese paso:

| Run | Corte | Día | apt (paquetes del sistema) | Navegador | Job entero |
|---|---|---|---|---|---|
| 39578030670 | C3b | 21-10 | 11 s | 8 s | 5 min 55 s |
| 39786224786 | C3c | 22-10 | 8 s | 8 s | 6 min 43 s |
| 39788661658 | C3c | 22-10 | **5 min 23 s** (82 kB/s) | 9 s | 12 min 7 s, cancelado |
| 39790679369 | C3c | 22-10 | **10 min 1 s** (38 kB/s) | 6 s | 17 min 12 s |
| 40305136887 | E4e | 26-10 | 7 s | 8 s | 7 min 3 s |
| 40307959687 | E4e | 26-10 | 6 s | 6 s | 6 min 5 s |
| 40483632214 | C3d | 27-10 | **10 min 5 s** (43 kB/s) | 7 s | 16 min 40 s |

- **Lo lento es apt, no el navegador.** apt baja unos 26 MB del espejo de Ubuntu de Azure (`azure.archive.ubuntu.com`): nueve paquetes nuevos (fuentes `fonts-freefont-ttf`, `fonts-ipafont-gothic`, `fonts-unifont`, `fonts-wqy-zenhei`, `fonts-tlwg-loma-otf` y `xfonts-*`) y ocho actualizaciones (`xvfb`, `libnss3`, `libglib2.0`…). Cuando el espejo va bien tarda segundos; en tres de siete ejecuciones fue a 38–82 kB/s y se comió entre 5 y 10 minutos.
- **El navegador baja siempre en 6–9 s** (`cdn.playwright.dev`).
- **Las pruebas en sí no han crecido:** sin la espera de apt, el job va en 6–7 minutos.
- **El run cancelado de C3c no lo paró el límite de 20 minutos:** iba por 12 minutos y todos sus pasos habían pasado; se canceló a mano o por otro push. Sí perdió 5 min 23 s en apt.

## Qué propongo

1. **Guardar en `actions/cache` los `.deb` que baja apt**, en una acción compuesta nueva, `.github/actions/playwright-chromium/action.yml`:
   - durante el paso, un fichero en `/etc/apt/apt.conf.d/` hace que apt use como almacén de paquetes una carpeta de la caché (`~/.cache/playwright-apt`) y los conserve;
   - el comando sigue siendo **el mismo** `npx playwright install --with-deps chromium`: mismos paquetes, mismas fuentes y mismo navegador. Con la caché caliente apt dice «Need to get 0 B» y no toca el espejo;
   - **clave:** la versión de `@playwright/test` del `package-lock.json` (decide la lista de paquetes) y la imagen del runner (`ImageOS` e `ImageVersion`: decide qué trae ya). Con imagen nueva se parte de la entrada anterior (`restore-keys`) y apt solo baja lo que cambió;
   - `apt-get autoclean` quita las versiones viejas, así que la entrada se queda en unos 26 MB.
2. **D1:** la instalación pasa a ser un paso propio, «Playwright Chromium and its system packages», antes de D6.1. Su tiempo se ve aparte en la CI. D6.1 se queda solo con `pwa_http.py`. El filtro de rutas de D1 incluye la acción nueva.
3. **Un job pequeño en `develop`**, `.github/workflows/playwright-apt-cache.yml`: `npm ci` y la acción, con cada mezcla que tocaría D1 y a mano. Hace falta porque **una caché guardada por una PR solo la ve esa PR**, y las de `develop` las ven todas las PR contra `develop`. Sin él, la primera ejecución de cada corte, que suele ser la única, iría siempre en frío. Tarda un minuto.

## Lo que descarto

- **Cachear el navegador** (`~/.cache/ms-playwright`): baja en 6–9 s en las siete ejecuciones. Restaurar sus 250 MB de caché tarda lo mismo y llenaría la cuota de 10 GB del repositorio. La propia documentación de Playwright lo desaconseja.
- **La imagen oficial de Playwright** (`mcr.microsoft.com/playwright`): el job hace mucho más que Playwright (.NET, Python, el cliente de PostgreSQL 17, el servicio `postgres`). Habría que rehacerlo dentro del contenedor, y bajar la imagen (1–2 GB) cuesta más que lo que se ahorra.
- **Quitar `--with-deps`:** la imagen del runner ya trae las librerías de Chrome, pero no esas fuentes. Cambiarían las medidas del texto, y hay comprobaciones que dependen de si algo cabe en la pantalla (trampa de C3c).

## Cómo se mide (terminado)

1. Primera ejecución de D1 en la PR: en frío. Guarda la entrada; en el log, «Cache saved with key: playwright-apt-…».
2. Se relanza solo el job `postgres-http` del mismo run: en caliente. En el log, «Cache restored» y «Need to get 0 B».
3. Se comparan los tiempos de los dos con la API de jobs. **Objetivo: el paso de Playwright, menos de 1 minuto en caliente, y todo en verde.**
4. Tras la mezcla, el job de `develop` deja su entrada (`GET /actions/caches`, `ref` `refs/heads/develop`).
5. Una línea en el apartado de trampas del traspaso vigente (`docs/handoff/2026-10-27-c3d-costina-cocina.md`) sustituye a la que dice «Guardar en caché… todavía sin hacer».

## Lo que no cambia

- El límite de 20 minutos de `postgres-http`: se revisará con más ejecuciones en caliente.
- Las pruebas, el código del producto, el instalador y los demás workflows.
- **Si la caché falla** (no está o no se puede guardar), apt baja como hoy: la caché nunca decide si el corte pasa.
- **Fuera de alcance:** el paso del cliente de PostgreSQL 17 también usa apt (12–54 s, con otro repositorio). Si un día se atasca, se trata aparte.
