# Traspaso 2026-10-15 (noche, 2): E3.3 mezclada; siguiente, C3a (comandero nuevo: aspecto y mesas)

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-15-e4d-mezclada-y-e3-3.md`, que sigue valiendo para las palabras del promotor y las trampas de E4d.

Leer antes, en este orden:
1. este documento;
2. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (APROBADO; sección 3 con las palabras, 4.3 para C3a, 9 y 10 con las decisiones) y su maqueta `docs/plans/maquetas/2026-10-15-comandero.html`;
3. `docs/native/E3.3-cocina-bodega-y-copas.md`, `docs/native/E4d-menu-del-dia-y-palabras-claras.md` y `docs/native/C2b-vistas-de-gestion.md` (lo que hay ahora en el puesto principal);
4. `docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md` (decisiones desde el 24-09, PC del promotor y trampas);
5. `dotnet/AGENTS.md`, párrafos A a E3.3, y `docs/STATUS.md`.

## Estado verificable

- **`develop` = `71ac3e8`** (Merge PR #83, E3.3), versión **`0.40.0-e3.3`**, esquema **v8**. Sin PR abiertas.
- **PR #83** (`feat/e3-3-cocina-bodega-copas`, punta `d5d3deb`): CI leída (D0 82; D1 con las 14 suites y Realtime 12; desktop «WPF startup and viewmodel checks passed»). Commits:

| Commit | Qué |
|---|---|
| `989b94f` | Motor: secciones reservadas, sección/familia/copa en los catálogos, `/catalog` con ETag, `glass-sale-set`, `products-move`, `products-tax-set`, reimportación que conserva familia, IVA y estación; D0 y `catalog_http.test_09` |
| `0ae5f01` | Puesto: chips de sección, pestaña «Vinos por copa», acciones sobre los marcados, «Mover a Cocina/Bodega», «Más opciones», buscador de Caja agrupado; versión `0.40.0-e3.3` |
| `d5d3deb` | Documento, capturas, AGENTS, STATUS y plan marcado como entregado |

- **Batería local completa** sobre la punta (PostgreSQL 17 instalado, `PG_BIN`; `npm run build` antes): dominio 82/82, ClientChecks 123, 14 suites HTTP OK, Realtime 12/12; `desktop` pasa.
- **Instalador de `develop`** tras el merge: run `38779904027`, artefacto `costina-windows-installer` (`Costina-Setup-0.40.0-e3.3.exe`); caduca a los 5 días. Al actualizar, la instalación recibe las dos secciones del catálogo con el `upgrade` del instalador.

## Decisiones del promotor en esta sesión

- E3.3: permiso de subida y merge («si», 15-10). «Cambiar IVA de los marcados» entra porque el IVA de los vinos queda pendiente del gestor y configurable.
- Siguen en pie las de E4d: «Menú degustación», «Menús y cartas», «Menú del día».

## Siguiente trabajo, en orden

1. **C3a — Comandero nuevo: aspecto y mesas** (`0.41.0-c3a`, sin cambio de esquema ni del motor), sección 4.3 del plan: el mismo aspecto, paleta y vocabulario que el puesto en `frontend/pwa` (`labels.ts` con tildes), las mesas por sala y por su nombre, la ficha de mesa con sus dos entradas, la relectura de `/configuration` al abrir mesa y cada 5 minutos, y la reescritura de los selectores de las E2E. Antes de codificar, el plan del corte en pocas líneas al promotor y **permiso para subir y mezclar** (no está dado para C3a). En este PC, Playwright no corre en local (`COSTINA_E2E=1` con los navegadores en `~/.ms-playwright`): las E2E se leen en la CI.
2. Después, en este orden: C3b (Bodega y bebidas en el comandero y «Añadir consumición» en la mesa del puesto; usa `section`, `family`, `glass` y el ETag de E3.3), E4e, C3c, **E4f, E4c**, E4g. Una PR por corte, plan por corte, permiso por corte.
3. Siguen pendientes del promotor: el tipo de IVA que diga su gestor (ya se cambia desde «Vinos por copa» sin reimportar), el dominio para el certificado público, la exportación de Verial con tarifas, las claves de WooCommerce, los guiones físicos, el LICENSE, las 4 fotos y la hora de apertura de la mesa.
4. Diferido en E3.3 (límites del documento): la fecha «Desde» del precio sigue a la vista; medir las ráfagas de avisos de 300 vinos en una instalación real.

## Trampas nuevas de esta sesión

- **Las suites HTTP sueltas no valen** (`local-tests.sh http a b`): corren sobre la base que dejó la batería anterior; con `packaging_http` delante la base queda sin la demo. Una suite nueva se prueba con la batería completa.
- **El laboratorio llama `bottle` y `glass` a las presentaciones del vino de ensayo**; un CSV con «Botella» crea `botella`. Las pruebas que importan sobre `wine` miran la presentación nueva.
- **`glassRendering`**: el filtro de familia de «Vinos por copa» se rehace al releer y el desplegable escribe `null` y luego el valor: la bandera evita rehacer la tabla en bucle.
- **Un `CanExecute` que depende de un desplegable** (`GlassTaxId`, `MoveTargetId`) necesita su `partial void On…Changed => ComposeGlass()`; si no, el botón sigue apagado hasta el siguiente `Sync`.
- **El `Fits` del catálogo exigía una lista de 360 px**: «Vinos por copa» es una tabla a todo lo ancho, y el check lo tolera (`SingleOrDefault`).

## En el PC del promotor

- Instalación real sana en **0.36.0-b2** (servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089). Instaladores disponibles: 0.38.0-c2b (run 38752213758), 0.39.0-e4d (run 38775024778) y el de E3.3 (run de arriba). **La instalación real no se toca.**

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Lee primero, en este orden: docs/handoff/2026-10-15-e3-3-mezclada-y-c3a.md (punto de entrada), docs/plans/2026-10-15-menus-carta-y-bodega.md (APROBADO: sección 3 con mis palabras, 4.3 para C3a, 9 y 10 con mis decisiones) y su maqueta docs/plans/maquetas/2026-10-15-comandero.html, docs/native/E3.3-cocina-bodega-y-copas.md, docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (decisiones, PC del promotor y trampas) y dotnet/AGENTS.md.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina, en el puerto 5090, nunca como administrador, y sin tocar sus servicios, puertos 5088/5089 ni carpetas; en este PC con PG_BIN al PostgreSQL 17 instalado y `npm run build` en frontend/pwa antes); una sola batería a la vez; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo; antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado.
Háblame siempre en castellano.
Estado: develop = 71ac3e8 (0.40.0-e3.3, esquema v8), sin PR abiertas. Siguiente: C3a (comandero nuevo: aspecto y mesas), sección 4.3 del plan aprobado: propón el plan del corte en pocas líneas y pídeme permiso para subir y mezclar; después sigue el orden del plan (C3b, E4e, C3c, E4f, E4c, E4g).
```
