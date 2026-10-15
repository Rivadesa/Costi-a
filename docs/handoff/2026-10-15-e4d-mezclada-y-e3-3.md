# Traspaso 2026-10-15 (noche): E4d mezclada; siguiente, E3.3 (Cocina, Bodega y vinos por copa)

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-15-c2b-mezclada-y-e4d.md`, que sigue valiendo para el estado de C2b, las respuestas del promotor al plan y las trampas de la CI del WPF.

Leer antes, en este orden:
1. este documento;
2. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (APROBADO; sección 3 con las palabras definitivas del promotor, sección 4.2 para E3.3, secciones 9 y 10 con sus decisiones);
3. `docs/native/E4d-menu-del-dia-y-palabras-claras.md` y `docs/native/C2b-vistas-de-gestion.md` (lo que hay ahora en el puesto principal);
4. `docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md` (decisiones desde el 24-09, PC del promotor y trampas);
5. `dotnet/AGENTS.md`, párrafos A a E4d, y `docs/STATUS.md`.

## Estado verificable

- **`develop` = `d9da03e`** (Merge PR #81, E4d), versión **`0.39.0-e4d`**, esquema **v8**. Sin PR abiertas.
- **PR #81** (`feat/e4d-menu-de-la-casa`, punta `33afc76`): CI leída (D0; D1 con las 14 suites, catálogo 9 y ofertas 7, y Realtime 12; desktop «WPF startup and viewmodel checks passed»). Commits:

| Commit | Qué |
|---|---|
| `d8ab4cc` | Plan: las palabras del promotor (Menú degustación, Menús y cartas, Menú del día) |
| `a1fde30` | Motor: código y orden opcionales en las altas, precio en `offer-update`, auditoría del producto-menú, `offers-reorder` |
| `7139e4b` | Puesto principal y comandero: Menús y cartas, alta sin códigos, Menú del día, Subir/Bajar, versión `0.39.0-e4d` |
| `33afc76` | Documento, capturas, AGENTS, STATUS y plan marcado como entregado |

- **Batería local completa** sobre la punta (PostgreSQL 17 instalado, `PG_BIN`): dominio 81/81, ClientChecks 123, 14 suites HTTP OK, Realtime 12/12; `desktop` pasa; vitest 65.
- **Instalador de `develop`** tras el merge: run `38775024778`, artefacto `costina-windows-installer` (`Costina-Setup-0.39.0-e4d.exe`); caduca a los 5 días.

## Decisiones del promotor del 15-10 (noche), al aprobar E4d

- El tipo `tasting` se llama **«Menú degustación»**: es el nombre del menú de Costiña (estrella Michelin) y no se renombra. `set-menu` = «Menú con elección»; `a-la-carte` = «Carta».
- La sección **«Oferta: cartas y menús» pasa a «Menús y cartas»**: «oferta» suena a rebaja. Los códigos de la API no cambian.
- El menú propuesto al abrir mesa se llama **«Menú del día»** (no «menú de la casa»).
- «Más opciones» para lo técnico y «Subir/Bajar»: sí.
- Permiso de subida y merge de E4d: **«Sube y mergea y adelante»**.

## Siguiente trabajo, en orden

1. **E3.3 — Cocina, Bodega y vinos por copa** (`0.40.0-e3.3`, sin cambio de esquema), sección 4.2 del plan: el catálogo partido en «Cocina» y «Bodega y bebidas» (raíces reservadas, `EnsureOrganizationAsync` también en el `upgrade` del instalador), «Vinos por copa» en bloque (`glass-sale-set`, una orden con `stop`), `products-move`, **`products-tax-set` y «Cambiar IVA de los marcados»** (decisión del promotor: IVA pendiente del gestor y configurable), la reimportación que conserva estación, familia e IVA, y el buscador de Caja agrupado por vino. Antes de codificar, el plan del corte en pocas líneas al promotor y **permiso para subir y mezclar** (no está dado para E3.3).
2. Después, en este orden: C3a, C3b, E4e, C3c, **E4f, E4c**, E4g. Una PR por corte, plan por corte, permiso por corte.
3. Siguen pendientes del promotor: el tipo de IVA que diga su gestor (no bloquea), el dominio para el certificado público, la exportación de Verial con tarifas, las claves de WooCommerce, los guiones físicos, el LICENSE, las 4 fotos y la hora de apertura de la mesa («sentados hace N minutos», de C2).
4. Diferido en E4d (límites del documento): selector de calendario en las fechas; IVA del producto-menú en la ficha.

## Trampas nuevas de esta sesión

- **Un `Expander` plegado no tiene su contenido en el árbol visual**: `Descendants<TextBox>` no encuentra lo que hay dentro de «Más opciones». Los checks comprueban el modelo o despliegan antes.
- **`Visibility` no se enlaza a `Command.CanExecute`** (es un método): el enlace falla en silencio y el botón se queda visible. Una propiedad booleana compuesta (`ShowMakeDaily`).
- **Un comando cuyo `CanExecute` depende de lo compuesto** (`IsDailyOffer`) necesita `NotifyCanExecuteChanged` DESPUÉS de `Compose`: `Sync` avisa antes.
- **Las suites HTTP sueltas** (`local-tests.sh http offers_http catalog_http`) corren sobre la base que dejó la batería anterior y fallan por datos repetidos; `packaging_http` delante tampoco basta (deja la base sin la demo). Para probar una suite nueva, la batería completa.
- **Heredocs de Python por `stdin`** con texto no ASCII o con `\r\n` dentro de cadenas: se rompen. Los parches van en fichero UTF-8 y `python -I fichero`; los saltos de línea, con `chr(13)+chr(10)`.

## En el PC del promotor

- Instalación real sana en **0.36.0-b2** (servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089). Instaladores disponibles: 0.38.0-c2b (run 38752213758) y el de E4d (run de arriba). **La instalación real no se toca.**

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Lee primero, en este orden: docs/handoff/2026-10-15-e4d-mezclada-y-e3-3.md (punto de entrada), docs/plans/2026-10-15-menus-carta-y-bodega.md (APROBADO: sección 3 con mis palabras, 4.2 para E3.3, 9 y 10 con mis decisiones), docs/native/E4d-menu-del-dia-y-palabras-claras.md, docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (decisiones, PC del promotor y trampas) y dotnet/AGENTS.md.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina, en el puerto 5090, nunca como administrador, y sin tocar sus servicios, puertos 5088/5089 ni carpetas; en este PC con PG_BIN al PostgreSQL 17 instalado y `npm run build` en frontend/pwa antes); una sola batería a la vez; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo; antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado.
Háblame siempre en castellano.
Estado: develop = d9da03e (0.39.0-e4d, esquema v8), sin PR abiertas. Siguiente: E3.3 (Cocina, Bodega y vinos por copa), sección 4.2 del plan aprobado: propón el plan del corte en pocas líneas y pídeme permiso para subir y mezclar; después sigue el orden del plan (C3a, C3b, E4e, C3c, E4f, E4c, E4g).
```
