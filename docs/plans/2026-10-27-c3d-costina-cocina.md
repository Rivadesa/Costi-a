# Plan C3d: Costiña Cocina, comandas por mesa (estaciones y pase)

Fecha: 2026-10-27. **Estado: APROBADO.** Aquí solo se baja a pasos lo que ya está aprobado: la sección 4.3 de `docs/plans/2026-10-22-sala-y-cocina.md` (`plan22:614-708`), aprobada el 22-10 con «Si a todo, la 5, va haber android y apple», con las secciones 5 a 10 y la «Revisión» de ese plan. El objetivo visual es la maqueta aprobada `docs/plans/maquetas/2026-10-22-sala-y-cocina.html`, pestañas «3 · Cocina · estación» y «4 · Cocina · pase», con sus capturas `docs/plans/maquetas/2026-10-22-sala-y-cocina/3-estacion.png` y `4-pase.png`.

**Respuestas del promotor que fija este corte** (22-10, `plan22:888-907`):
1. **En las estaciones, solo «Listo»**, sin «Empezar».
2. **El pase puede marcar el trabajo de una estación como listo**: en «Más opciones» de cada comanda, «Marcar Cocina fría como lista», con la orden de grupo que E4e ya permite al pase de cocina (`Affordances.PassMarksAll`).
3. **El tiempo:** neutro al principio, ámbar desde 15 minutos y ámbar oscuro con letra blanca desde 25. **Nunca rojo**, y siempre escrito: «Lleva N min».
4. **Sí suena** cuando llega una comanda nueva, solo después de tocar «Empezar turno».

**27-10, «si, adelante»:** permiso para subir y mezclar C3d cuando la CI esté en verde.

- **Base:** `develop` en `d4f095b` (`0.44.0-e4e`, esquema v8).
- **Rama:** `feat/c3d-costina-cocina`.
- **Versión:** `0.45.0-c3d`.
- **Sin cambio de esquema, de motor, de API ni de CSP.** Solo la PWA (Costiña Cocina), `Costina.Client` (la regla de agrupar, compartida) y las pruebas.
- **ADR nuevo:** `docs/ADR/ADR-017-la-estacion-ve-solo-lo-suyo.md`. Sustituye la regla de D6.4 «una estación ve el pase entero» (`dotnet/AGENTS.md`, párrafo D6.4; `pwa/kitchen.ts:31-33`) y cambia dos frases de `docs/native/D6.4-pwa-kds.md:48`: «sin alarmas por tiempo» y «no hay vista de lo siguiente sin enviar».

Rutas cortas: `pwa/` = `frontend/pwa/src/`, `Client/` = `dotnet/src/Costina.Client/` y `plan22:` = `docs/plans/2026-10-22-sala-y-cocina.md`. Las referencias `fichero:línea` son de `d4f095b`.

## Alcance

### 1. Lógica sin DOM: `pwa/cocina.ts` sustituye a `pwa/kitchen.ts`

Todo es **presentación** de la lectura autoritativa del tablero: nada habilita nada. Un botón existe solo porque el servidor anuncia la acción en esa elaboración o en ese pase, ya filtrada por rol y estación (D3.1, D4.3a, E4e).

- **Comandas de estación** (cualquier estación que no sea el pase): una por mesa y pase **con solo su trabajo**. Pases enviados o en preparación; nunca los sin enviar, los ya validados ni los de mesas terminadas o canceladas. La clave y el orden son los de hoy (`kitchen.ts:35,41-45`): `servicio:pase`, de la más antigua (`firedAt`) a la más nueva.
  - **«También en: Cocina fría»**: las demás estaciones del pase, por su nombre entero, en lugar de sus líneas atenuadas.
  - **«Listo»**: una orden `preparations-ready` con las elaboraciones propias que anuncian `preparation-ready`. Con dos platos propios o más, cada plato lleva también su «Listo», con solo las suyas. Nada de «Empezar» (respuesta 1).
  - **«Hecho · esperando al pase»**: cuando todo lo propio está listo, la comanda sale de la rejilla a esa franja y desaparece cuando el pase valida. Sin hora: el motor no la guarda.
- **Comandas del pase** (y de una tablet de cocina sin estación, que ve lo que el servidor le anuncie, como hoy): una por mesa y pase, **con la mesa entera**.
  - **Progreso de cada estación, con su nombre entero:** «Cocina caliente: lista · Cocina fría: 1 de 2 platos». Cuenta platos agrupados.
  - **Qué falta**, mientras el servidor no anuncia `ready`: «Falta Cocina fría», «Falta revisar la alergia».
  - **«Siguiente: Postre (sin enviar)»**, solo de lectura: el primer pase sin enviar de la mesa.
  - **«Marcar Cocina fría como lista»** por cada estación con algo que el servidor deja marcar (respuesta 2), y el «Listo» de lo que se emplata en el propio pase: una orden `preparations-ready` cada uno.
  - **Lo enviado por revisar** (una alergia anotada con el pase ya en cocina) sigue en su comanda, con «No le afecta», «Adaptado» y «Rehacer» (D3.5; «Rehacer» solo para lo enviado).
  - Un pase validado con algo por revisar (alergia anotada después de validar) sigue en la rejilla: no se puede servir hasta revisarlo.
- **«Esperando al camarero»** (pase): lo validado y sin servir, sin nada por revisar, con «listo hace 3 min» desde `readyAt`, del más antiguo al más nuevo. Nunca «Sala avisada»: el servidor no sabe si el aviso llegó (`dotnet/AGENTS.md`, D2).
- **«Por revisar antes de enviar»** (pase, de E4e): se mueve tal cual de `kitchen.ts` (`presendReviews`, `restOfGuest`, `restBody`).
- **Platos agrupados por nombre y estación**, sumando las cantidades: «Merluza × 4». Es solo presentación: `PreparationView` no lleva el identificador del plato (`Dining.Domain/ServiceModels.cs:20-29`).
  - **Las excepciones van aparte, bajo su plato, y nunca atenuadas:** una elaboración por revisar, con una decisión que cambia el plato («Adaptado», «Rehecho») o con una restricción de un comensal concreto. Siempre con tipo, sustancia y gravedad: «Comensal 3 · ALERGIA marisco (grave) · Adaptado: vieira». Pendiente: «Por revisar en el pase». El plato dice «Merluza × 4 (1 adaptada)».
  - **Una restricción de toda la mesa sale una sola vez, arriba de la comanda**, nunca repetida en cada línea («Merluza × 4» no son cuatro líneas rojas iguales). Un «No le afecta» bajo una restricción de toda la mesa no es una excepción: no cambia el plato y la restricción ya está arriba.
  - **La regla de agrupar es común con el PC:** `dotnet/tests/fixtures/kitchen-cases.json`, leída por vitest (`cocina.ts`) y por ClientChecks (`Client/DishGroups.cs`, el Comedor de E4e), como `floorplan-cases.json`. `DishGroups` se alinea con ella: la misma excepción y la línea del comensal sin las restricciones de toda la mesa (en el PC ya salen en la franja de restricciones de la ficha). Un apartado o un campo que un lado no conoce es un fallo en los dos.
- **El tiempo** (respuesta 3): «Lleva 12 min» desde `firedAt`, nunca negativo y siempre escrito; tono neutro, ámbar desde 15 y ámbar oscuro desde 25. Los dos umbrales son constantes de `cocina.ts`.
- **Comanda nueva, para el sonido:** como `sala.ts`, las claves nuevas entre dos lecturas correctas; la primera lectura no trae nada nuevo. Una comanda que vuelve a la rejilla (un «Rehacer» del pase) cuenta como nueva.
- **Sin palabras de caja**, ni dentro de otra palabra (regla de `kitchen.spec`).

### 2. Pantalla: `pwa/views/KitchenView.vue` nueva

- **Estación:** rejilla de comandas en una tablet horizontal (1180 × 820, leída a uno o dos metros): «Mesa 4 · Principal» de 24 px, platos de 22 px con la cantidad de 28 px, la línea de alergia de 18 px en negrita, el tiempo de 18 px y botones de 64 px; nada operativo por debajo de 16 px. La franja «Hecho · esperando al pase» abajo. Nada que escribir.
- **Pase:** «Por revisar antes de enviar», siempre arriba, con atajos que rellenan la nota a la vista («No lo lleva», «Sin gluten», «Cambiado por …»); las comandas con la mesa entera; «Validar pase: listo para servir», grande, solo cuando el servidor anuncia `ready` (ese toque es la llamada al camarero); «Más opciones» con «Marcar <estación> como lista»; «Esperando al camarero».
- **«Empezar turno»** al encender: el toque que deja sonar al navegador. Con https, la pantalla siempre encendida (Wake Lock) si el navegador la da; con la conexión sencilla (http) no existe, y «Empezar turno» dice que se deje encendida en los ajustes de la tablet.
- **El sonido:** un tono corto sintetizado con Web Audio. Sin ficheros de audio: la CSP no cambia.
- **Misma lectura en vivo y misma orden incierta** (`pwa/operations.ts`, `StatusPanel.vue`): una orden por toque. Un `version_conflict` (dos estaciones a la vez en la misma mesa) relee y pide tocar otra vez; nunca se reenvía solo con otra clave.
- **Tema claro** con la identidad de Costiña Cocina (cabecera de C3c); el acento `#0E7A5F` en los botones, el rojo solo para las alergias que alarman y el ámbar para lo pendiente y el tiempo.
- **Palabras de cocina iguales a las de Sala y el PC:** «Por revisar en el pase» y «Adaptado: …» sustituyen a «PENDIENTE DE REVISIÓN POR COCINA» y «Revisada: …».
- `pwa/kitchen.ts` y sus pruebas se retiran cuando la vista nueva deja de usarlos.

### 3. Pruebas de navegador: el proyecto `tableta`

- **Proyecto nuevo `tableta`** de Playwright: 1180 × 820, táctil, Chromium (el único de la CI). Corre **solo** las historias de cocina (`testMatch`), y esas historias salen de los proyectos `desktop` y `mobile` (`testIgnore`): la cocina es una tablet, y así la CI no tarda más.
- **Carril:** la mesa de cocina del carril de escritorio (M8), que deja de usarse allí; M5 queda libre en el móvil. No hace falta crear mesas.
- **`kitchen.spec.ts` reescrito** (hoy fija lo atenuado y «Marcar lista» del pase en otra estación): la estación no ve lo ajeno y su «Listo» es una orden con la versión que sube en uno; `wrong_station` con el token del propio dispositivo; el pase revisa antes de enviar, ve «Cocina caliente: lista», marca por otra estación desde «Más opciones» y valida; «Esperando al camarero»; ninguna palabra de caja.
- **Límite de la CI:** el job `postgres-http` tiene 20 minutos y ya tarda unos 17. Se mide `pwa_http` en local; si no cabe, se sube `timeout-minutes` en `.github/workflows/dotnet-d1.yml`, con su comentario.

### 4. Documentación

`docs/native/C3d-costina-cocina.md` (capturas en `docs/native/images/c3d/`, decisiones para revisar con el promotor, pruebas en rojo y guion físico corto), ADR-017, el párrafo en `dotnet/AGENTS.md`, la línea en `docs/STATUS.md`, la sección 4.3 de `plan22` marcada como entregada y la versión `0.45.0-c3d` en los csproj del servidor y del escritorio y en `frontend/pwa/package.json` (y su lock).

## Qué no cambia

- **El motor, el esquema (v8), la API y la CSP.** Envío a estaciones igual.
- **El servidor decide los botones** (D3.1): lo que pinta cada pantalla (solo lo suyo, grupos, colores) es presentación, nunca permiso; la matriz de estación sigue en el servidor.
- **Una sola orden incierta durable, una por gesto** (D6.3, D6.7): «Listo», «Marcar … como lista», «Validar pase», una revisión o «No le afecta a nada más».
- **Tiempo real con avisos finos y relectura autoritativa** (D2): el sonido sale de la relectura, nunca del contenido del aviso.
- **Alergias y restricciones** nunca escondidas ni atenuadas, siempre con tipo, sustancia y gravedad; rojo solo para las que alarman.
- **Sin dinero** en cocina (ADR-007), nombres y nunca códigos, castellano con tildes.
- **El pase sigue siendo la única estación que valida y revisa.**

## Diferido

- **Deshacer «Listo»** en el motor (`plan22:806`): sería una acción nueva y aditiva. Hasta entonces, el pase no valida sin ver el plato y la estación lo ve en la franja «Hecho».
- **La franja de totales** del turno («Merluza: 11 en marcha», `plan22:808`).
- **La hora por estación** («Cocina caliente: lista hace 3 min», `plan22:805`): `PreparationView` no guarda horas.
- **El modo quiosco** y la reapertura tras reiniciar la tablet: Hito 7 (`docs/plans/2026-09-24-inicio-y-dispositivos.md:135`).
- **Los umbrales del tiempo en la configuración**: por ahora, constantes de `cocina.ts`.
- Lo de C3e: «Mis mesas», abrir con alergias desde Sala, «Carta y menús» y «Preguntar al servidor».

## Pruebas

Cada prueba nueva falla antes de su arreglo, y se apunta en el documento del corte.

- **vitest** (`tests/cocina.test.ts`): la tabla común (grupos, excepciones, restricción de toda la mesa una sola vez); comandas de estación (solo lo suyo, orden, «También en», «Listo» con lo anunciado, franja «Hecho»); comandas del pase (progreso por estación, qué falta, «Siguiente», marcar por otra estación); «Esperando al camarero»; umbrales del tiempo, nunca rojos y siempre escritos; comanda nueva para el sonido; sin palabras de caja ni dentro de otra palabra.
- **ClientChecks:** la misma tabla contra `DishGroups`. Romper un caso de la tabla rompe las dos pruebas.
- **DesktopChecks**, porque cambian `Client/` y `dotnet/tests`.
- **Playwright**, con el proyecto `tableta` (sección 3).
- **Batería completa** con `COSTINA_E2E=1`, `npm run build` antes, y la duración de `pwa_http` apuntada.

## Capturas

En `docs/native/images/c3d/`, con `C3D_CAPTURES=<carpeta>` (`e2e/captures-c3d.spec.ts`, nunca en la CI) sobre una base nueva y el motor compilado con el build actual de la PWA, en 1180 × 820: «Empezar turno», la estación (con una alergia adaptada, «También en» y el tiempo en ámbar), la franja «Hecho · esperando al pase», el pase (progreso, «Falta …», «Siguiente», «Más opciones») y «Por revisar antes de enviar» con «Esperando al camarero». Revisadas una a una antes de cerrar.

## Pasos (un commit por paso)

1. `docs(plans)`: este plan.
2. `feat(pwa)` y `feat(client)`: `pwa/cocina.ts` con la tabla común `kitchen-cases.json`, `DishGroups` alineado, vitest y ClientChecks (sección 1).
3. `feat(pwa)`: la pantalla nueva de Costiña Cocina, «Empezar turno», el sonido y la pantalla encendida; se retira `kitchen.ts` (sección 2).
4. `test(pwa)`: el proyecto `tableta` y `kitchen.spec.ts` reescrito (sección 3).
5. `docs(adr)` y `docs(native)`: ADR-017, el documento del corte con sus capturas y la versión (sección 4).
