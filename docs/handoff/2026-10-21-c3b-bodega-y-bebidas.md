# Traspaso 2026-10-21: C3b (bodega y bebidas); siguiente, E4e

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-20-c3a-comandero-nuevo.md`. El anterior sigue valiendo para C3a, sus decisiones para revisar y su guion con móvil e iPad.

Leer antes, en este orden:
1. este documento;
2. `docs/native/C3b-bodega-y-bebidas.md` (qué cambió, la defensa contra el doble cargo, capturas en `docs/native/images/c3b/`, límites y la pregunta de las copas) y su plan `docs/plans/2026-10-21-c3b-bodega-y-bebidas.md`;
3. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (APROBADO; la sección 4.5 es E4e), con sus secciones 5, 8, 9 y 10;
4. `docs/adr/ADR-007-service-control-vs-checkout.md`, apartado C3b;
5. `dotnet/AGENTS.md`, sobre todo los párrafos D3.5 (la regla que E4e cambia), D6.3, D6.7 y C3b, y `docs/STATUS.md`.

## Estado

- **`develop` = `81c4cd0`**: Merge PR #88, C3b mezclada el 21-10. Versión **`0.42.0-c3b`**, esquema v8 sin cambios. Sin PR abiertas el 22-10.
- **CI leída en la PR #88**:
  - dominio 86 en Linux y en Windows;
  - vitest 306 y ClientChecks 263;
  - las 14 suites HTTP y Playwright 14 (7 de escritorio y 7 de móvil);
  - Realtime 13, servicio Windows 12/12 y escritorio en verde.
- **Instalador de `develop` tras el merge**: run `39578879449`, artefacto `costina-windows-installer` (`Costina-Setup-0.42.0-c3b.exe`), 7/7 comprobaciones. Caduca el 26-10; después se regenera relanzando el flujo del instalador sobre `develop`.
- **Permiso**: el promotor aprobó C3b el 21-10 con «adelante con c3b», que incluía subirla y mezclarla. **Para E4e hay que volver a pedirlo.**
- **El 22-10 pidió «sube todo al repositorio para tenerlo disponible en cualquier sitio».** No quedaba nada solo en el PC de desarrollo: ni ramas sin subir ni cambios sin confirmar. Lo único local son los ajustes de `.claude/`, que no se suben, y el laboratorio `.lab/`, que se crea solo. Este traspaso se puso al día con el estado final.
- **Qué trajo C3b:**
  - **Comandero:** «Bodega y bebidas» nueva:
    - buscador sin tildes por nombre, referencia, familia y región;
    - «Por copa», familias con subfamilias y «En esta mesa»;
    - una fila por vino con foto y un botón por formato;
    - sin precios.

    Un toque suma 1 a la **ronda**, un borrador en memoria. «Enviar» es una sola orden. «Lo que lleva la mesa» enseña lo enviado, sin importes, con «Anulado en Caja».
  - **PC:** «Añadir consumición» en la ficha de la mesa del Comedor, con el mismo selector, sin contraseña en el nivel Puesto y sin precios. Caja busca con la misma regla.
  - **Motor** (solo añade):
    - la orden `add-consumptions`, todo o nada;
    - los identificadores de línea derivados de la orden, la **defensa contra el doble cargo** diferida de D6.7;
    - la línea de la cuenta con comensal, origen y hora;
    - `consumptions` en el tablero, sin dinero;
    - el aviso `service.consumptions_changed`;
    - la CSP con `img-src 'self' blob:`.
  - **Diferidos cerrados:** el puesto deja la orden como pendiente solo después de guardarla, y los rechazos de E3.3.1 tienen texto.
  - **Revisión del 21-10** (antes de subir): en la ficha, el envío de la ronda es «Enviar ronda» en verde claro (un solo botón verde lleno, el grande); tras una respuesta perdida la bandeja dice que la ronda está sin confirmar y, con una orden incierta, por qué no se envía; en el PC, «Añadir consumición» en la cabecera fija de la ficha y «Por copa» discontinuo; vitest rechaza, como ClientChecks, lo que no conoce de `drinks-cases.json`; Caja vuelve a buscar también por el nombre de la categoría.
- **Sin vuelta atrás:** en cuanto `0.42.0-c3b` guarda una cuenta, volver a `0.41.0-c3a` exige restaurar la copia previa a la actualización: el motor anterior no lee las líneas con `guestPosition`, `origin` y `at`. Nada lo impide (esquema v8 sin cambios; el instalador no comprueba que no baja de versión y la restauración no mira la versión que hizo la copia). Está en «Límites y diferido» del documento de C3b.
- **Pruebas compartidas:** `search.ts`, `drinks.ts` y `round.ts` (PWA), y `CatalogSearch.cs`, `DrinksPicker.cs` y `Round.cs` (`Costina.Client`), comparten casos en `dotnet/tests/fixtures/drinks-cases.json`. Se cambian la tabla Y las dos implementaciones.
- **Cifras en la punta:**
  - vitest 306, en 16 ficheros;
  - ClientChecks 263;
  - dominio 86;
  - las 14 suites HTTP (checkout 8, pwa_http 7, affordances 6) y Realtime 13;
  - Playwright 14 (7 de escritorio y 7 de móvil);
  - DesktopChecks, en verde.

## Novedad del entorno y trampas

- **Capturas de C3b:** las del comandero se rehacen con el modo `C3B_CAPTURES` de `e2e/captures-c3b.spec.ts`, sobre una base nueva; el comando está en la cabecera de ese fichero. Las del puesto salen de DesktopChecks en `dotnet/artifacts/desktop/` y se copian a mano.
- **La E2E de la ronda** (`e2e/drinks.spec.ts`) usa la **Mesa 1** en los dos proyectos, uno detrás de otro, y la libera al terminar: `pwa_http.py` (`test_06` y `test_07`) la necesita después.
- **Mesas compartidas en la batería:**
  - tras las E2E, en el ámbito `d6-pwa` solo queda libre la Mesa 1;
  - en `d1-tenant`, tras `affordances_http`, no queda ninguna.

  Una prueba HTTP nueva en esos ámbitos abre, comprueba y libera una mesa cada vez, o reutiliza su propio servicio, como hace `affordances_http.test_06`.
- **Los datos de ensayo** (`LabConfiguration`) no traen ningún vino con «albar», ni una bebida con la presentación por defecto `unit`. Las pruebas crean lo que necesitan por la API (`/erp/catalog`). Cambiar los datos de ensayo cambiaría la lista exacta que fija `pwa_http.test_01`.
- **Pruebas en rojo:** para devolver un fichero tras romperlo a propósito, usar una copia en el `scratchpad`, nunca `git checkout`. Un `git checkout` deshizo una vez cambios aún sin commit.
- **La batería completa con navegador** tarda unos 5 minutos; de ellos, `pwa_http` se lleva unos 140 s. Hay que esperarla en primer plano, y solo una a la vez.

## Para el promotor

1. **Pregunta abierta: varios tamaños de copa por vino** («Copa 15 cl»). Está en la última sección del documento de C3b. Mientras no conteste, sigue una copa por vino.
2. **Veinticuatro decisiones pequeñas para revisar**, en «Decisiones y diferencias con la maqueta» del documento de C3b (las cuatro últimas, de la revisión). Por ejemplo:
   - las restricciones también arriba en Bebidas;
   - las familias de la A a la Z;
   - una ronda sin confirmar que sale de la bandeja;
   - la ronda plegada en el PC;
   - «Enviar ronda» en verde claro en la ficha;
   - «Añadir consumición» en la cabecera de la ficha del PC, no en la de «Lo que lleva la mesa».

   Siguen pendientes las cinco de C3a.
3. **Guion manual de C3b**, solo en un PC de laboratorio, con un móvil y el PC: 13 pasos, con su tabla de resultados.

## Siguiente trabajo, en orden

1. **E4e, alergias adaptadas antes de enviar y cocina por plato** (`0.43.0-e4e`, sin cambio de esquema, pero **sí** de las reglas de las instantáneas), según la sección 4.5 del plan.
   - **Cambia una regla de seguridad documentada (D3.5):** cocina decide cada plato de un comensal con restricción **antes** de que salga el pase. El promotor dijo que sí el 15-10, también para preferencias e intolerancias leves.
   - Lleva un **ADR nuevo** («revisión de alergias antes de enviar»).
   - Lleva también:
     - órdenes por grupo con la matriz de estación sin huecos (`Affordances.Allows`, `StationKeeps`/`PassKeeps` y `StationAllowed`);
     - `open` con alergias;
     - `fire-next` desde «Sentados»;
     - el mínimo de la PWA para el pase.
   - **Sin vuelta atrás:** un motor anterior no lee una mesa guardada con revisiones en pases sin enviar. El documento del corte lo dice. C3b ya tiene el mismo efecto con las cuentas: conviene que E4e (o un corte propio) haga que el instalador nunca baje de versión y que restaurar una copia rechace la de un motor más nuevo.
   - Hay que proponer el plan del corte y **pedir permiso para subir y mezclar**.
2. Después:
   - C3c, que incluye «Carta y menús», la mesa abierta con una oferta que ya no está vigente (el motor debe servirla) y el «Preguntar al servidor» con una orden de otra instalación;
   - E4f, E4c y E4g.
3. **Pendiente del promotor:**
   - el IVA que diga su gestor;
   - el dominio para el certificado;
   - la exportación de Verial;
   - las claves de WooCommerce;
   - los guiones físicos, incluidos el de C3a (móvil e iPad) y el de C3b;
   - el LICENSE;
   - las 4 fotos;
   - la hora de apertura de la mesa;
   - los varios tamaños de copa.

## En el PC del promotor

- **Instalación real**, leída el 19-10: versión 0.37.1-c2.1. Servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089. **No se toca.** El instalador de `develop` la pone al día.
- **Pruebas en este PC:** la batería corre con `.lab/pgsql/bin`, en el puerto 5090, sin administrador y una sola a la vez. Playwright corre en local con Chromium en `%USERPROFILE%\.ms-playwright`.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado. Lee primero, en este orden: docs/handoff/2026-10-21-c3b-bodega-y-bebidas.md (punto de entrada), docs/native/C3b-bodega-y-bebidas.md, docs/plans/2026-10-15-menus-carta-y-bodega.md (APROBADO; 4.5 para E4e, con sus secciones 5, 8, 9 y 10), docs/adr/ADR-007-service-control-vs-checkout.md (apartado C3b) y dotnet/AGENTS.md (sobre todo D3.5, D6.3, D6.7 y C3b).
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ o frontend/ corre `npm run build` y `COSTINA_E2E=1 sh tools/agent/local-tests.sh` entero, y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina: puerto 5090, nunca como administrador, sin tocar sus servicios, puertos 5088/5089 ni carpetas); una sola batería a la vez; los tests de CI se LEEN en los logs; GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano.
Estado: develop = 81c4cd0 con C3b mezclada (PR #88, 0.42.0-c3b, esquema v8), sin PR abiertas el 22-10. Siguiente: E4e (alergias adaptadas antes de enviar y cocina por plato), sección 4.5 del plan aprobado. Cambia la regla D3.5 y lleva un ADR nuevo. Propón el plan del corte en pocas líneas y pídeme permiso para subir y mezclar; después sigue el orden del plan (C3c, E4f, E4c, E4g). Tengo pendiente decidir los varios tamaños de copa («Copa 15 cl»), que está en el documento de C3b.
```
