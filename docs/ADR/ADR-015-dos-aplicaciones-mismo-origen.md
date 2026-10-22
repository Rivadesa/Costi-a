# ADR-015 — Dos aplicaciones operativas en el mismo origen: Costiña Sala y Costiña Cocina

**Estado:** aceptado (2026-10-22). Decisión del promotor del 22-10-2026: «dos aplicaciones separadas, Costiña Sala y Costiña Cocina, cada una con su nombre, su icono y su color; el QR del PC abre la que toca». Aprobada con el plan `docs/plans/2026-10-22-sala-y-cocina.md` («Si a todo, la 5, va haber android y apple»). Hecha en C3c (`docs/native/C3c-dos-apps.md`). Precisa D6.5 (`docs/native/D6.5-qr-install-physical.md`: una sola PWA instalable y el QR a `/app/#pair=`) y D6.1 (`docs/native/D6.1-pwa-base.md`: un service worker con alcance `/app/`).

## Contexto

Hasta C3b, la PWA era **una sola aplicación** en `/app/`: un único «Costina» que, después de emparejar, montaba el comandero o la pantalla de cocina según el papel que se aprobara en el PC. El promotor no lo encontraba intuitivo («necesitamos una pantalla para los comanderos de los camareros de sala y otra para los de cocina»), y el guion de C3a cambiaba un iPad de papel desvinculándolo y emparejándolo otra vez.

Además:
- habrá **Android y también iPhone y iPad** (respuesta 5 del 22-10);
- en iPhone y iPad, la cámara abre el QR en Safari, y cada icono de la pantalla de inicio guarda aparte, sin ver lo que guarda Safari;
- el service worker de entonces borraba, al activarse, todas las cachés del origen que no fueran la suya;
- había una sola base IndexedDB, `costina`, con una sola credencial y un solo hueco de orden sin confirmar.

La guía de web.dev recomienda un origen por aplicación. Otro origen aquí sería otro puerto (otro listener y otra regla de cortafuegos en la conexión sencilla de B.2) u otro nombre en el certificado, y el QR tendría que saber cuál usar.

## Decisión

1. **Dos aplicaciones de la misma compilación, en el mismo origen y bajo `/app/`:** `/app/sala/` («Costiña Sala», verde `#0E7A5F`) y `/app/cocina/` («Costiña Cocina», azul pizarra `#2F4A6D`). Cada una tiene:
   - su manifiesto, con `id`, `start_url` y `scope` en su carpeta;
   - sus iconos, salidos de un único dibujo (`frontend/pwa/src/app-icons.ts`);
   - su service worker, con alcance y prefijo de caché propios (`costina-sala-*`, `costina-cocina-*`), que solo borra y solo lee lo suyo y nunca toca `/api/`;
   - su base IndexedDB (`costina-sala`, `costina-cocina`), con su credencial y su hueco de orden sin confirmar (D6.7 se aplica a cada una).
2. **`/app/` es un enrutador**, no una aplicación: sin manifiesto ni metas de iOS. Lleva a cada dispositivo a la aplicación de su credencial o de su papel, y con un `#pair=` pregunta si es para la sala o para la cocina.
3. **El service worker de alcance `/app/` se retira:** su fichero pasa a uno que borra su caché y se da de baja, y las páginas dan de baja cualquier registro de alcance exacto `/app/`. Nunca se vuelve a registrar uno con ese alcance.
4. **Lo emparejado antes se adopta, nunca se borra:** sin credencial propia, cada aplicación mira la base vieja `costina` y, solo si el papel le corresponde, copia primero la orden sin confirmar y después la credencial. La base vieja se conserva.
5. **Cada aplicación comprueba su papel:** Sala, `service` y el `main` antiguo; Cocina, `kitchen`; un papel desconocido, ninguna. Con una credencial que no le toca no monta nada y lo dice. Si el PC aprueba un código para la otra aplicación, no se guarda y el dispositivo se da de baja en el servidor. **Es una decisión de uso, no de seguridad:** el servidor sigue filtrando por papel y estación.
6. **El QR del PC abre la aplicación de la intención del asistente** (`PairingLink.Build(dirección, código, aplicación)`) y lleva un **código corto** de 10 caracteres sin ambigüedades, que el PC enseña en dos grupos para teclearlo en el icono de un iPhone o un iPad. En iPhone y iPad, la página que abre Safari no empareja: explica cómo añadir el icono.

## Alternativas descartadas

- **Seguir con una sola aplicación** que elige pantalla por el papel: es lo que el promotor encontró confuso, y un solo icono no puede tener dos nombres ni dos colores.
- **Un origen por aplicación** (otro puerto u otro nombre): más piezas que mantener en cada instalación (listener, cortafuegos, certificado) y un QR que tendría que conocerlas. Las mitigaciones del mismo origen (alcances, cachés y bases separadas) son suficientes y se prueban.
- **Fijar la intención al emitir el código** (papel y estación en `core.pairings`): rechazaría antes una solicitud hecha desde la aplicación equivocada, pero es esquema. Queda diferido (sección 6 del plan): la comprobación del punto 5 basta, porque la seguridad no depende de ella.
- **Emparejar en Safari en iPhone y iPad:** dejaría en el servidor un dispositivo aprobado que el icono no puede usar.
- **Aplicaciones nativas:** obligan a pasar por las tiendas y a instalar algo en cada dispositivo, lo que ADR-014 ya descartó.

## Consecuencias

- Una regla del service worker de D6.1 cambia: **uno por aplicación**, cada uno en su carpeta. Sigue sin tocar `/api/` y sin Background Sync, y por http (B.2) no hay ninguno.
- Una regla de D6.5 cambia: el QR abre `/app/sala/#pair=` o `/app/cocina/#pair=`, y `/app/#pair=` queda para el enrutador. Sigue llevando solo el código de un uso, en el fragmento.
- La CSP, el esquema y el contrato operativo no cambian. El motor solo añade el código corto (`{typed: true}`) y las redirecciones de `/app/sala` y `/app/cocina`.
- En Android, un dispositivo es una aplicación: para cambiarlo, hay que desvincularlo y emparejarlo con el QR de la otra.
- Volver a C3b en un dispositivo emparejado ya con C3c obliga a emparejarlo otra vez, porque su credencial está en una base que la versión anterior no mira.
- Un «Costina» instalado en Android (alcance `/app/`) puede quedarse con los enlaces de las dos aplicaciones hasta que se quite. El documento del corte dice cuándo y cómo.
- La comprobación física con un Android, un iPhone y un iPad (guion de C3c) es parte del cierre: la CI solo prueba Chromium.
