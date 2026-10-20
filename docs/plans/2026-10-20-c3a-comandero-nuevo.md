# Plan C3a: comandero nuevo, aspecto y mesas

Fecha: 2026-10-20. **Estado: APROBADO** por el promotor el 20-10, con «merge y sigue»: es el permiso para hacer el corte, subirlo y mezclarlo.

**Entregado el 20-10**: `docs/native/C3a-comandero-nuevo.md`, con sus capturas en `docs/native/images/c3a/`. Todo el alcance de abajo, salvo dos detalles: «Abrir mesa» ya queda en tres toques con el menú del día propuesto, y las mesas propias de las E2E van por proyecto de Playwright (ver «Qué no cambia»). Cinco palabras o decisiones quedan para revisar con el promotor; están en «Divergencias deliberadas» del documento del corte.

Es el corte 3 del plan aprobado `docs/plans/2026-10-15-menus-carta-y-bodega.md` (sección 4.3). Aquí solo se baja a pasos. Las reglas de las secciones 2 («más intuitivo»), 3 (vocabulario), 9 y 10 (decisiones del promotor) se aplican tal cual.

- **Base:** `develop` en `0fd446e` (`0.40.2-e3.3.1`, esquema v8).
- **Rama:** `feat/c3a-comandero-nuevo`.
- **Versión:** `0.41.0-c3a`.
- **Sin cambios** en el motor, el esquema, la API ni la CSP.

Objetivo visual: la maqueta `docs/plans/maquetas/2026-10-15-comandero.html`, pestañas «0 · Abrir mesa» y «1 · Mesa» (capturas `mesa.png` y `abrir.png`), y el Comedor del puesto principal de C2 (`docs/native/images/c2/`).

## Alcance

### 1. Palabras y lógica sin DOM

- **`labels.ts`** copia el vocabulario de `Costina.Client/Labels.cs`, con tildes:
  - estados de mesa, servicio, pase y elaboración;
  - tipos de oferta («Menú degustación», «Menú con elección», «Carta», decididos en E4d);
  - tipos de estación, que `/configuration` envía en minúscula;
  - palabras de las restricciones y «Toda la mesa» / «Comensal n».
- **Misma regla que el puesto:** un valor desconocido sale tal cual, y ni mayúsculas ni guiones cuentan.
- **Textos de D6.7** (`commands.ts`, `labels.ts`) con tildes. El aviso de cuenta cerrada se reescribe sin la palabra «cuenta».
- **`floor.ts`** es la copia sin DOM de `Costina.Client/FloorPlan.cs`:
  - el estado de la tarjeta;
  - los minutos del estado que se ve, nunca inventados;
  - los pases «n de m»;
  - el aviso de restricciones, siempre escrito;
  - el detalle corto de cada pase.
- **Una sola tabla de casos para las dos implementaciones:** `dotnet/tests/fixtures/floorplan-cases.json` y `vocabulary-cases.json`. La leen vitest y ClientChecks, así que el comandero y el puesto no pueden separarse sin que falle una de las dos.

### 2. Aspecto nuevo del comandero

- **`style.css` con variables:** fondo `#F3F4F6`, tarjetas blancas y acento `#0E7A5F`. El rojo, solo para alergias y restricciones. Solo tema claro.
- **Iconos** de trazo en SVG dentro de las plantillas: ni imágenes ni cambios en la CSP.
- **Objetivos táctiles** de al menos 48 px en el comandero y 64 px en cocina.
- **Nombres, nunca códigos:** mesas, salas y estaciones salen por su nombre de `/configuration`. No se ve ni un `tableId`, `stationId` o código de sala.
- **Ninguna palabra de caja a la vista:** ni «precio», ni «cuenta», ni «pago», ni «saldo», ni «€».

### 3. Mesas

- Una rejilla de tarjetas **por sala**, también con las mesas libres. Cada tarjeta lleva:
  - el estado escrito, como en el Comedor: Libre · Sentados · En cocina · Listo para servir · Comiendo · En pausa · Terminado, por liberar · Cancelado, por liberar;
  - personas o plazas, y los minutos del estado;
  - el avance de los pases;
  - el aviso de restricciones, escrito y en rojo cuando alarma.
- Una mesa ocupada que la configuración ya no lista sale igual: nunca se esconde una mesa con gente.
- **Tocar una tarjeta solo elige la mesa.** Las lecturas y las órdenes siguen saliendo de lo leído (F01).

### 4. Ficha de mesa

Como en la maqueta:
- **Arriba, las alergias**, en rojo y escritas: quién, qué y gravedad.
- **Dos entradas grandes:**
  - **«Carta y menús»** abre lo de hoy (elegir plato, pedir de la carta) con el aspecto nuevo. Se rehace en C3c.
  - **«Bodega y bebidas»** abre la consumición de hoy con el aspecto nuevo. Se rehace en C3b.
- **Los pases, en corto**, con su detalle.
- **Abajo, un solo botón grande** con la acción principal que anuncia el servidor, en el orden del Comedor: servir, validar, enviar siguiente pase, empezar, reanudar, terminar, liberar. El resto (pausar, omitir…) queda en «Más opciones», y solo sale lo que el servidor anuncia.
- **«Abrir mesa»** desde una mesa libre conserva los campos de hoy, con el menú del día propuesto y el aspecto nuevo. C3c la deja en tres toques y con las alergias.

### 5. Configuración al día, cocina y orden pendiente

- **`/configuration`** (mesas, salas, ofertas y estaciones) se relee al abrir «Abrir mesa» y cada 5 minutos.
- **Cocina:** solo el aspecto nuevo y los nombres de estación. La agrupación por plato llega en C3c.
- **Orden sin confirmar y órdenes bloqueadas** de D6.7 (otro nombre de dispositivo, otra instalación, ilegible), con el aspecto nuevo y los mismos textos y botones.

## Qué no cambia

- **Motor, esquema, API y CSP de `PwaHosting`.**
- **Sin dinero:** la guarda de dinero (`money-guard.ts`) y ADR-007 siguen igual.
- **Los botones solo existen por lo que anuncia el servidor** (affordances). El cliente no deduce reglas.
- **Una sola orden incierta durable**, con los bloqueos de D6.7 (`actor`, `foreign`, `unreadable`) a la vista.
- **Restricciones siempre escritas**, nunca solo con color. Lo desconocido sale en crudo.
- **Tiempo real** como en D6.2: el aviso solo provoca una relectura.
- **Mesas propias de cada spec E2E:** board=M6, commands=M7, kitchen=M8 en el proyecto de escritorio, y M3, M4 y M5 en el móvil (`e2e/support.ts`, carriles). M2 sigue siendo de `pwa_http.py`; las capturas usan M1 y M3–M8, solo con una base nueva.

## Pruebas

- **vitest:**
  - `floor.ts` con la tabla de casos común;
  - el vocabulario, también con su tabla común;
  - ninguna palabra de caja en los textos.
- **ClientChecks:** `FloorPlan` y `Labels` contra las mismas tablas. Se comprueba que fallan rompiendo un caso a propósito en cada lado.
- **Playwright:**
  - un proyecto móvil nuevo (375 × 812, táctil) además del de escritorio, con las historias de hoy: abrir, empezar, enviar, servir, pausa y orden incierta;
  - los selectores pasan de `select[name=table|menu]` a un `data-testid` por tarjeta y por botón;
  - el texto visible sigue sin palabras de caja (`board.spec.ts`; `kitchen.spec.ts`, incluso dentro de otra palabra).
- **Batería completa** con `COSTINA_E2E=1`, y DesktopChecks, porque se toca `Costina.Client`.

## Capturas

En `docs/native/images/c3a/`: mesas y ficha de mesa a 375 × 812, a 820 × 1180 (iPad en vertical) y a 1180 × 820, y la pantalla de cocina.

## Diferido

- **A C3b:**
  - el selector nuevo de «Bodega y bebidas»: buscador, Copa y Botella, rondas en una orden, fotos (CSP con `blob:`) y «Lo que lleva la mesa»;
  - en el PC, «Añadir consumición» en la mesa abierta;
  - lo pendiente de D6.7 y E3.3.1 (traspaso del 19-10): la defensa en el servidor, el orden de guardado en el puesto, la copa con otro código, los textos de `reserved_category`, `nothing_to_do` y `product_ids_required`, y el «Preguntar al servidor» que aún se ofrece con una orden de otra instalación.
- **A C3c:**
  - «Abrir mesa» en tres toques, con las alergias en la misma hoja;
  - el menú agrupado («Ostra × 7»);
  - la cocina por plato.
- **Sin fecha:** la hora de apertura de la mesa («sentados hace N minutos»). Necesita un cambio pequeño del motor, como ya dice C2.
