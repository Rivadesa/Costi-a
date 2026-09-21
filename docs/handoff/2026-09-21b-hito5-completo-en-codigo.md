# Traspaso de sesión — 21-09-2026 (tarde) — Hito 5 (#28) completo en código

Documento para **retomar el trabajo en otra sesión o en otro equipo**. Sustituye como punto de entrada a `2026-09-21-hito5-en-curso.md`, que se conserva como registro. Las reglas de trabajo, los invariantes y las trampas del prompt de aquel documento **siguen vigentes**: no se repiten aquí, solo se añade lo aprendido.

## Estado verificable

`develop` = `9976d74` (Merge PR #53). `main` no se toca.

| Hito | Estado |
| --- | --- |
| 0–2 | Integrados. |
| **3 — identidad (#26)** | Completo en código. **Issue abierta**: falta el guion manual del promotor (`docs/native/D4.3b-wpf-login.md`). |
| **4 — empaquetado (#27)** | Completo en código. **Issue abierta**: falta el guion físico del promotor (`docs/native/D5.6-physical-install.md`). Firma: #12. |
| **5 — PWA (#28)** | **Completo en código**: D6.1 (PR #50) · D6.2 (#51) · D6.3 (#52) · D6.4 (#53) mezcladas; **D6.5 en PR #54** (`feat/d6.5-qr-icons-physical`), con su primer CI verde y leído en logs; falta la confirmación de merge del promotor ("Merge PR #54: D6.5 QR de emparejamiento, PWA instalable y guion fisico"). **Issue abierta**: se cierra solo con la tabla de resultados del guion físico Android + iPad (`docs/native/D6.5-qr-install-physical.md`). |

Issue #25: **no tocar sin preguntar**.

## Lo que destaparon los E2E contra el motor real (todo cazado solo en CI)

Ninguna de estas cosas la vio un test unitario ni el simulador local; conviene tenerlas presentes al escribir el siguiente E2E o la siguiente pantalla.

1. **`add-consumption` respondía `chargeId`** y la guarda anti-dinero de la PWA (con razón) rechazaba la respuesta antes de cerrar la orden: consumo aplicado, tablet con ORDEN SIN CONFIRMAR. Decisión del promotor: el **motor** renombra a `consumptionId`; la guarda sigue absoluta. Un éxito con eco **cierra** la orden antes de `assertNoMoney`. Regla: ninguna clave con vocabulario de caja en una respuesta que pueda recibir sala o cocina (`pwa_http.py` lo comprueba con la regla de la PWA).
2. **`Affordances.cs` no filtraba `review-preparation` por estación**: el dominio la anuncia en la **elaboración** y el espejo solo la filtraba a nivel de pase. Arreglado; `station_http.py::test_05`. Regla: una acción de nivel elaboración se filtra por estación a nivel elaboración.
3. El motor anuncia **`preparation-start` y `preparation-ready` a la vez** en una elaboración enviada. Tras confirmarse una orden los botones se rehabilitaban con la versión anterior: ahora siguen bloqueados hasta la relectura (`operations.ts`), y `live.refresh()` espera también la relectura encolada.
4. Las elaboraciones son **por comensal** (`frio-1`, `frio-2`…). `wrong_station` es **409**, no 403.
5. En Playwright: **no afirmar un texto que también está en un botón** ("Lista", "LISTO PARA SERVIR"): afirmar el rótulo de estado (`.prep-title`, `h3`) y una señal positiva de relectura; `pending` a cero es cierto *antes* de actuar y no prueba nada; no recorrer botones uno a uno mientras el tiempo real puede cambiarlos (`button:not([disabled])` con cuenta 0).
6. **El simulador local debe imitar las claves y las affordances REALES del motor**, o la verificación visual da falsa confianza.

## Cosas nuevas del entorno

- Mesas por spec (un servidor y una base por ejecución): `board`=M6, `commands`=M7, `kitchen`=M8, `pwa_http`=M2, `station_http`=M1/M2 (ámbito propio).
- `frontend/pwa` no tiene `@types/node`: un test que necesite Node declara lo que usa en `tests/node-shim.d.ts`.
- El servidor de desarrollo de Vite antepone la base a los `href` de `index.html` (`/app/app/…`): solo en desarrollo; el build es correcto y `pwa_http.py` lo fija.
- Los heredocs largos de Bash siguen fallando en Windows: parche como fichero + `tools/agent/patch.py`.
- La cuota de artefactos sigue agotada: `artifacts/desktop/pairing-qr.png` (el QR real que genera el WPF en CI) **no se pudo descargar**, así que nadie ha leído aún ese QR con una cámara: lo cubre el paso 3 del guion D6.5.

## Pendiente del promotor

- Confirmar el merge de la **PR #54**.
- Guiones: **D4.3b** → #26 · **D5.6** → #27 · **D6.5** → #28. **No cerrar ninguna sin su resultado.** Antes de D5.6 o D6.5: relanzar a mano el workflow *Native Windows installer* (`workflow_dispatch`) sobre `develop` para tener un instalador descargable.
- Guiones manuales de cada corte (D5.1–D5.5, D6.1–D6.4), en su doc.

## Siguiente trabajo posible (a decidir por el promotor)

Con los hitos 3–5 completos en código, lo que bloquea un piloto está en `docs/STATUS.md` ("Bloqueos antes de piloto operativo"): administración editable de mesas, estaciones y menús (hoy fixtures), verificación física de D3.4–D3.6, migración/integración del legado (#17) y la deuda señalada por la revisión externa (lock de dependencias .NET #13, N+1 en `Board()`, audiencia explícita por tipo de evento). **Proponer alcance antes de codificar.**
