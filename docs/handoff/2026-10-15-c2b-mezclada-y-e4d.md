# Traspaso 2026-10-15 (tarde): C2b mezclada, plan de menús, carta y bodega aprobado; siguiente, E4d

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-15-c2b-y-carta-bodega.md`, que sigue valiendo para las peticiones del promotor del 15-10 y el contexto del plan de menús. El de `2026-10-15-c2b-estado-y-revisiones.md` queda como historia (sus 19 hallazgos están aplicados).

Leer antes, en este orden:
1. este documento;
2. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (**APROBADO el 15-10**, con las respuestas del promotor en su sección 10 y las decisiones en la 9) y su maqueta `docs/plans/maquetas/2026-10-15-comandero.html`;
3. `docs/native/C2b-vistas-de-gestion.md` (lo que hay ahora en el puesto principal) y `docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md` (decisiones desde el 24-09, PC del promotor y trampas);
4. `dotnet/AGENTS.md`, párrafos A a C2b, y `docs/STATUS.md`.

## Estado verificable

- **`develop` = `56d9c0c`** (Merge PR #79, C2b), versión **`0.38.0-c2b`**, esquema **v8**. Sin PR abiertas.
- **PR #79** (`feat/c2b-vistas-de-gestion`, punta `e6f05e3`): CI leída (D0; D1 con las 14 suites y Realtime 12; desktop con «WPF startup and viewmodel checks passed»; el instalador se construye al mezclar en `develop`). Commits de esta sesión sobre la punta anterior `6955407`:

| Commit | Qué |
|---|---|
| `3f0e179` | Merge de `develop` (`44fe713`) en la rama |
| `4eb40e1` | **Guardar sin código ya no cierra el programa**: `ShellViewModel.Attempt`, `Codes` (código generado del nombre; oferta a 27), tooltips, checks de regresión |
| `a8d8ee0` | **Los 19 hallazgos** de las revisiones de aspecto y comportamiento, con sus checks |
| `cbc64eb` | Documento `docs/native/C2b-vistas-de-gestion.md`, 22 capturas, párrafo de AGENTS y STATUS |
| `a7df24a`, `05d2755`, `e6f05e3` | Tres arreglos de checks que solo fallaban en CI (pantalla estrecha y cursor sobre una tarjeta): ver «Trampas» |

- **Batería local completa** sobre la punta (PostgreSQL 17 instalado, `PG_BIN="/c/Program Files/PostgreSQL/17/bin"`): dominio 81/81, ClientChecks 123, 14 suites HTTP OK, Realtime 12/12; `desktop` pasa.
- **Instalador de `develop`** tras el merge: run `38752213758`, artefacto `costina-windows-installer` (`Costina-Setup-0.38.0-c2b.exe`); caduca a los 5 días.
- **Rama de docs mezclada en esta misma PR de traspaso:** el plan aprobado iba en `docs/plan-menus-aprobado` (`21929b3`); se recoge aquí.

## Decisiones del promotor del 15-10 (tarde)

Respuestas a las siete preguntas del plan de menús, carta y bodega (ya escritas en el plan, secciones 9 y 10):
1. Alergias e intolerancias: cocina decide cada plato del comensal **antes** de que salga el pase; el pase espera. **Sí.**
2. Preferencias e intolerancias leves: **lo mismo.**
3. IVA de los vinos: **pendiente del gestor y configurable desde el programa** → E3.3 añade `products-tax-set` y «Cambiar IVA de los marcados»; no se cambia el 21 % de la importación.
4. Bebidas y combinados: **basta** verlos en la mesa y en Caja; sin pantalla de barra.
5. Comensal sin menú y cambio de personas con la mesa abierta: **más adelante** (después de E4g).
6. Maridaje: **un menú puede llevarlo o no** → E4f admite un vino de la Bodega como plato del pase en los menús cerrados (sin cocina, sin cargo).
7. Menú del día: **el programa sirve para distintos tipos de negocio y hay que poder añadirlo** → **E4f va antes que E4c** (`0.45.0-e4f`, `0.46.0-e4c`).

Y «sigue»: terminar C2b con el permiso ya dado.

## Siguiente trabajo, en orden

1. **E4d — Menú de la casa y palabras claras** (`0.39.0-e4d`, sin cambio de esquema), sección 4.1 del plan. Antes de codificar, el plan del corte en una respuesta corta al promotor y **permiso para subir y mezclar** (no está dado para E4d). Parte del arreglo de C2b: el código generado en el cliente pasa al motor (`offer-create`, `course-create`, `dish-create` con código vacío; `menu-<código>` derivado aparte).
2. Después, en este orden: E3.3, C3a, C3b, E4e, C3c, **E4f, E4c**, E4g. Una PR por corte, plan por corte, permiso por corte.
3. Siguen pendientes del promotor: el tipo de IVA que diga su gestor (no bloquea), el dominio para el certificado público, la exportación de Verial con tarifas, las claves de WooCommerce, los guiones físicos, el LICENSE, las 4 fotos y la hora de apertura de la mesa («sentados hace N minutos», de C2).
4. Pendientes técnicos anotados en C2b (`docs/native/C2b-vistas-de-gestion.md`, «Límites»): N+2 lecturas de Caja, botón de anular consumo, producto del plato por código.

## Trampas nuevas de esta sesión

- **La CI del WPF no es este PC.** Su pantalla es más estrecha que 1270 px (la ventana pliega la barra por sí sola) y el cursor puede quedar sobre una tarjeta (el `IsMouseOver` pisa el fondo). Tres runs rojos por eso: un check de recorte con la barra forzada abierta (ahora solo a partir de 1270 px), un contenedor recién generado sin plantilla aplicada (`ApplyTemplate`) y el gris de una mesa desactivada, que ahora se mira en el color del texto y no en el fondo. Los checks del WPF se leen en el log de CI: en local pasan siempre.

- **`tools/agent/patch.py` y los espacios duros.** `OffersViewModel.OfferDetail` y `OfferRowsText()` de los checks llevaban U+00A0; un fragmento tecleado con espacio normal no coincide. Para esas líneas, sustituir por prefijo de línea.
- **Texto no ASCII por `stdin` de Python** (heredoc `python - <<EOF`) llega en cp1252: «·» se convierte en «�» y el parche falla. Los scripts de parche se escriben en fichero UTF-8 y se lanzan con `python -I fichero`.
- **`[]` y `ToArray()` vacíos comparten instancia** (`Array.Empty<T>()`): una propiedad `[ObservableProperty]` que recibe una lista vacía nueva calculada con `ToArray()` no avisa si ya valía `[]`. `CheckoutViewModel.Ordered` devuelve la instancia leída cuando hay menos de dos cuentas.
- **`ScrollIntoView` en una lista virtualizada** termina en un paso `Loaded` posterior del dispatcher: un check debe bombear (`Wait`) antes de pedir el contenedor.
- **`ProductName` ya existe** en `CatalogViewModel` (campo del formulario): la búsqueda por catálogo se llama `NameOfProduct`.
- **La batería necesita `PG_BIN`** en este PC (no hay `.lab/pgsql`): `PG_BIN="/c/Program Files/PostgreSQL/17/bin" sh tools/agent/local-tests.sh`. Y `npm run build` en `frontend/pwa` antes, para que `pwa_http` sirva la PWA de la versión.

## En el PC del promotor

- Instalación real sana en **0.36.0-b2** (servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089). Tiene en `Descargas` el `Costina-Setup-0.37.1-c2.1.exe`; el de C2b es el del run de arriba.
- **La instalación real no se toca.** Solo se leen estados.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Lee primero, en este orden: docs/handoff/2026-10-15-c2b-mezclada-y-e4d.md (punto de entrada), docs/plans/2026-10-15-menus-carta-y-bodega.md (APROBADO: mis respuestas en la sección 10), docs/native/C2b-vistas-de-gestion.md, docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (decisiones, PC del promotor y trampas) y dotnet/AGENTS.md.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina, en el puerto 5090, nunca como administrador, y sin tocar sus servicios, puertos 5088/5089 ni carpetas; en este PC con PG_BIN al PostgreSQL 17 instalado); una sola batería a la vez; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo; antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado.
Háblame siempre en castellano.
Estado: develop = 56d9c0c (0.38.0-c2b, esquema v8), sin PR abiertas. Siguiente: E4d (menú de la casa y palabras claras), sección 4.1 del plan aprobado: propón el plan del corte en pocas líneas y pídeme permiso para subir y mezclar; después sigue el orden del plan (E3.3, C3a, C3b, E4e, C3c, E4f, E4c, E4g).
```
