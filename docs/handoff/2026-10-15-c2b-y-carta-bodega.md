# Traspaso 2026-10-15: C2b a medias, error de «Guardar» por arreglar y plan de menús, carta y bodega en borrador

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-14-rediseno-c1-y-catalogo-real.md`, que sigue valiendo para las decisiones, el estado de las PR hasta C2.1, lo que hay en el PC del promotor y las trampas.

Leer antes, en este orden:
1. este documento;
2. `docs/handoff/2026-10-15-c2b-estado-y-revisiones.md`: informes de las cinco etapas de C2b y 19 hallazgos de revisión sin aplicar;
3. `docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md`: decisiones desde el 24-09, PR hasta C2, PC del promotor y trampas;
4. `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md`, sección «C2b», que está en la rama de C2b (commit `ab3e940`);
5. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (borrador) con su maqueta `docs/plans/maquetas/2026-10-15-comandero.html`, que se abre en el navegador, y sus capturas;
6. `dotnet/AGENTS.md`, párrafos A a C2.1, y `docs/STATUS.md`.

## Estado verificable

- **`develop` = `8bb94a0`** (PR #77, C2.1), versión `0.37.1-c2.1`, esquema v8. C2 es la PR #76 y C2.1 la #77, las dos con la CI leída.
- **Rama `feat/c2b-vistas-de-gestion`**, subida con punta `6955407`. Está sin PR y sin mezclar:

| Commit | Qué |
|---|---|
| `ab3e940` | Plan de C2b aprobado (`0.38.0-c2b`) |
| `dcfb7aa` | Piezas comunes de lista y ficha; Caja con cuentas en tarjetas |
| `210bc53` | Catálogo con pestañas Productos · Categorías · Tarifas · Impuestos, con lista y ficha |
| `1e26d74` | Oferta con lista, chips de tipo, pases numerados y platos desplegados |
| `061ae23` | Salas con pestañas, salas, mesas en tarjetas y ficha |
| `6955407` | Dispositivos con cabecera y tarjetas, repaso de coherencia y versión `0.38.0-c2b` |

- **El flujo de agentes se paró a mitad de las revisiones** porque el promotor necesitaba el equipo libre. Lo que falta está en el documento del punto 2 de la lectura.
- **Sin PR abiertas.** La sesión aparte de C2.1 dejó un worktree `~/Costina-c2.1` en el PC de desarrollo; no se usa.

## Peticiones del promotor del 15-10, tras probar su instalación

**1. Error: el programa se cierra al guardar sin «Código (fijo)».**

> «anota este error, al dar de alta un producto, estoy probando en Oferta: cartas y menú, si no indico el código fijo se cierra el programa»

- **Causa** (verificada en `8bb94a0`): en `OffersViewModel`, `CatalogViewModel` y `OrganizationViewModel`, los `Save*` validan antes de `shell.Run`. Validan `Code`, `Name`, `Number`, `DateText`, precio y días, con `throw new ArgumentException(...)`.
  - Ejemplos: `OffersViewModel.cs:169-176,202-206,222-225,245-249`; `CatalogViewModel.cs:336-353,375-394,442-470`; `OrganizationViewModel.cs:137-148,163-196`.
  - La excepción sale síncrona del `AsyncRelayCommand` sin capturar y Windows cierra el programa. `ShellViewModel.Run` (`:194-215`) sí convierte `ArgumentException` en el mensaje de la barra de estado, pero esas validaciones no pasan por él.
- **Alcance**: cualquier «Guardar» de Catálogo (categoría, producto, presentación, precio, tarifa e impuesto), Oferta (oferta, pase y plato) y Salas (sala, mesa y estación), con un código o un nombre vacío, un orden que no es un número, una fecha mal escrita, un precio mal escrito o ningún día. Caja, Comedor y Dispositivos validan dentro de `Run` y están bien.
- **Arreglo prometido, dentro de C2b y en un commit propio**:
  1. Toda validación pasa a contarse en la barra de estado sin cerrar el programa. Por ejemplo, con un envoltorio en `ShellViewModel` que capture `ArgumentException` y ponga `Status`. **No** se puede meter el `Command(...)` dentro de otro `shell.Run`, porque `Run` no hace nada si `Busy`.
  2. **Un código vacío se genera a partir del nombre**, con la regla de la importación (`Catalog.Slug`: minúsculas, sin tildes, guiones) y único en su lista (sufijos `-2`, `-3`…).
     - El código **de una oferta** se corta a **27 caracteres**, porque su producto-menú se llama `menu-<código>` (`OfferOperations.cs:48`) y un código admite 32.
     - Pases, platos y el resto se cortan a 32.
     - Se le dijo al promotor: «si prefieres que el código siga siendo obligatorio, dímelo». No contestó.
  3. Un check de regresión en DesktopChecks pulsa cada «Guardar» de las tres vistas con datos vacíos o mal escritos y comprueba que no salta ninguna excepción, que la barra de estado lo dice y que no se envía nada (o, con el código vacío, que se envía el generado).
- **Mientras tanto** se le dijo que rellene siempre código, nombre, orden en número y fechas como AAAA-MM-DD.

**2. Menús, carta y bodega.**

> «Debemos poder definir un menú, menú cerrado tipo degustación, menú diario, o poder definir una carta de comida de platos entrantes, carnes, pescados y mariscos, postres, etc. Yo separaría esto de la bodega y bebidas, en bodega y bebidas tenemos que poder visualizarlo mejor, es complicado desde un comandero poder añadir una copa de un vino sin un buscador o sin ser más intuitivo. En el caso de vinos y otras bebidas debemos poder seleccionar copas o botellas y combinados»

Hoy el comandero añade consumos con un único `<select>` de todo el catálogo: más de 1.000 vinos (`frontend/pwa/src/views/ServiceActions.vue:115-125`).

**Decisiones del promotor (15-10, respuestas a tres preguntas):**
- **Prioridad**: justo **después de C2b** y antes de E8, e **incluye C3**, el aspecto nuevo de la PWA.
- **Copas**: **precio a mano por vino**. Se marcan los vinos que van por copa y se escribe su precio, con la posibilidad de marcar varios a la vez.
- **Combinados**: **precio fijo por combinado**, por ejemplo «Gin tonic Premium». Se eligen la base y el refresco sin cambiar el precio.

**3. Aclaración clave del menú.**

> «para Costiña, por ejemplo, el menú es cerrado y no se elige cada plato, los comensales comen todos lo mismo en cada uno de los pases a menos que hay intolerancias o alergias. Tenemos que hacer el sistema más intuitivo»

- **Su menú** es lo que hoy el programa llama **Degustación** (`tasting`).
- **Choque de palabras:** lo que hoy el programa llama «Menú cerrado» (`set-menu`) es justo lo contrario, porque cada comensal elige. El plan lo renombra: `tasting` = «Menú cerrado» y `set-menu` = «Menú con elección», que va en «Más opciones».
- **Para sus pruebas** se le dijo que cree su menú como Degustación.
- **«Más intuitivo» es un principio para todo:** menos pasos, valores por defecto, nada técnico en el camino principal y palabras llanas.

**4. Consumición en la mesa del PC.**

> «para añadir una consumición a una mesa no tenemos por qué salir de la mesa e ir a caja, no podemos hacerlo directamente en la mesa abierta?»

- **Hoy:** el PC solo añade consumos en Caja, que en el nivel Puesto pide contraseña. El motor ya lo permite en la mesa (`add-consumption`, `DiningModule.cs:20`) y la PWA lo usa.
- **Respuesta dada:** irá al corte de bodega (C3b). La ficha de la mesa del Comedor tendrá «Añadir consumición», con el mismo buscador de Bodega y bebidas, desde el puesto y sin contraseña. Caja queda para cobrar, anular y cerrar. Ya está en el plan y en la maqueta (pestaña 5).

**Plan resultante**: `docs/plans/2026-10-15-menus-carta-y-bodega.md`.
- **Estado:** BORRADOR, **pendiente de aprobación del promotor**. Se le enseñaron la maqueta y el resumen el 15-10 y todavía no contestó.
- **Contenido:** nueve cortes después de C2b:
  1. E4d: palabras claras, menú de la casa y altas sin códigos;
  2. E3.3: Cocina / Bodega y bebidas y vinos por copa;
  3. C3a: aspecto del comandero;
  4. C3b: Bodega y bebidas en el comandero y en la mesa del PC;
  5. E4e: alergias adaptadas antes de enviar;
  6. C3c: abrir y servir el menú;
  7. E4c: combinados, esquema v9;
  8. E4f: el menú en una pantalla;
  9. E4g: carta por familias.
- **Siete preguntas abiertas** en su «Resumen para el promotor»: alergias decididas antes de enviar el pase; intolerancias leves y preferencias; IVA de los vinos en sala; pantalla de barra; comensal sin menú o cambio de personas; maridaje; menú en una pantalla antes que combinados.
- **Al aprobarse**, quitar «BORRADOR» del plan y empezar por E4d, con un plan por corte y permiso de subida y merge en cada uno.
- **Fallos reales del código** que el estudio encontró y que el plan recoge:
  - reimportar borra la estación de un producto (`CatalogImport.cs:177`) y pisa su categoría e IVA (`:172-177`);
  - `offer-create` no deja auditoría `catalog.*` de su producto-menú;
  - `add-consumption` y la Caja no avisan a los comanderos.

## Siguiente trabajo, en orden

1. **Terminar C2b** en `feat/c2b-vistas-de-gestion`:
   1. Hacer la revisión que falta: textos, dinero y pruebas no debilitadas.
   2. Aplicar los 19 hallazgos del documento de revisiones, verificándolos antes.
   3. Hacer el arreglo del cierre al guardar, en un commit propio y con su check.
   4. Pasar la batería completa y los checks del WPF en primer plano, una sola vez a la vez.
   5. Revisar las capturas.
   6. Escribir `docs/native/C2b-….md`, con capturas en `docs/native/images/c2b/`, y añadir los párrafos de `AGENTS.md` y `STATUS.md`. Actualizar este traspaso.
   7. Abrir la PR, leer la CI y hacer el merge. **Permiso ya dado** («Adelante», 15-10).
   8. Pasarle el enlace del artefacto `costina-windows-installer` de develop.
2. **Preguntarle por el plan de menús, carta y bodega**: «adelante» y respuestas a las siete preguntas. Después, E4d.
3. Siguen pendientes los puntos del traspaso del 14-10: el dominio para el certificado público, la exportación de Verial con tarifas, las claves de WooCommerce, los guiones físicos, el LICENSE y las 4 fotos. A eso se suma la decisión de la hora de apertura de la mesa («sentados hace N minutos», de C2).

## En el PC del promotor

- La sesión de C2.1 le dejó en `Descargas` el instalador **`Costina-Setup-0.37.1-c2.1.exe`**. Venía de la 0.35 y sus pruebas del 15-10 son con esa versión o la anterior.
- **La instalación real no se toca:** servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089, `C:\Program Files\Costina`, `C:\ProgramData\Costina` y `%LOCALAPPDATA%\Costina`. Solo se leen estados.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión, que se cortó el 15-10 porque necesitaba el equipo libre.
Lee primero, en este orden: docs/handoff/2026-10-15-c2b-y-carta-bodega.md (punto de entrada: estado, mis peticiones del 15-10 y siguiente trabajo), docs/handoff/2026-10-15-c2b-estado-y-revisiones.md (C2b a medias: informes y 19 hallazgos sin aplicar), docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (decisiones anteriores, PC del promotor y trampas), docs/plans/2026-10-15-menus-carta-y-bodega.md (borrador pendiente de mi aprobación) y dotnet/AGENTS.md.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto: para C2b ya la di); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina, en el puerto 5090, nunca como administrador, y sin tocar sus servicios, puertos 5088/5089 ni carpetas); una sola batería a la vez y en primer plano; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano.
Estado: develop = 8bb94a0 (0.37.1-c2.1, esquema v8). C2b a medias en la rama feat/c2b-vistas-de-gestion (6955407): termina la revisión, aplica los hallazgos, arregla el cierre al guardar sin código (código generado del nombre, el de una oferta a 27 caracteres), pasa las pruebas, documenta, haz la PR, lee la CI, mézclala y pásame el instalador. Después pregúntame si apruebo el plan de menús, carta y bodega y mis respuestas a sus siete preguntas.
```
