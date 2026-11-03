# Traspaso 2026-11-03 (noche): F0 entregada; siguiente, la prueba del promotor y la F1

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-11-03-plan-por-fases.md`, que sigue valiendo para el rumbo nuevo (dejar Verial), «cómo se trabaja» y lo que no se toca.

## Leer antes, en este orden

1. Este documento.
2. `docs/ORDEN_VIGENTE.md` (una página: las fases, las reglas, lo que ya no vale) y `docs/plans/2026-11-03-plan-por-fases.md` (APROBADO el 03-11; las decisiones pendientes, con fecha, y la regla legal).
3. `docs/native/F0.1-alergias-i-aviso-en-cocina.md` y `docs/native/F0.2-alergias-ii-borrador-en-el-movil.md` (lo que hay ahora en Costiña Cocina y Costiña Sala, con sus guiones).
4. `docs/handoff/2026-11-03-plan-por-fases.md` (rumbo, cómo se trabaja, trampas de la CI y lo que no se toca).
5. `dotnet/AGENTS.md` (párrafos F0.1 y F0.2, al final) y `docs/STATUS.md`.

## Estado verificable

- **`develop` = `f34f055`** (Merge PR #110). Versión **`0.47.1-f0.2`**, esquema v8. Ningún PR abierto.
- **La F0 está entregada en código y papeles**, con el permiso del promotor para la F0 entera («adelante», 03-11):

| PR | Corte | Merge | Qué |
|---|---|---|---|
| #107 | 0.0 | `81e3f9e` | Plan marcado APROBADO; decisión 1 (el servidor es el equipo principal de recepción) |
| #108 | 0.1 | `6f162ca` | H34: alergia tardía o decisión del pase con sonido (tres notas) y marca en la comanda hasta «Visto», también en «Hecho» y en el pase; `0.47.0-f0.1` |
| #109 | 0.2 | `38fe056` | H01 (decisión 8b): ronda y mesa a medio abrir guardadas en el móvil y preguntadas al volver; «Atrás» baja un nivel; sin recarga al tirar; `0.47.1-f0.2` |
| #110 | 0.3 | `f34f055` | `docs/ORDEN_VIGENTE.md`; `ROADMAP`, `PRODUCT_SCOPE`, `BACKLOG`, `AI_CONTEXT`, `DECISION_LOG` y README remiten a él |

- **CI leída** en los registros de #108 y #109: dominio 97/97; D1 con las 14 suites, Playwright 31 y Realtime 13; ClientChecks 288 y «WPF startup and viewmodel checks passed». Batería local completa (`COSTINA_E2E=1`) y DesktopChecks en verde en este PC para los dos cortes (`.lab-test/f01-*.log`, `.lab-test/f02-*.log`).
- **Instalador de `develop`** con el código de la F0: run `41687659139` sobre `38fe056` (`Costina-Setup-0.47.1-f0.2.exe`; el artefacto caduca a los 5 días). El 0.3 no toca código: no hay otro instalador.
- **El equipo principal de recepción (el del promotor) tiene instalada la `0.46.2-c3e.2`** (instalada el 03-11 desde el artefacto de C3e.2) y es el servidor decidido. Para probar la F0 hay que instalar la `0.47.1-f0.2` encima, con el comedor cerrado; lo hace el promotor.

## Decisiones del promotor en esta sesión (03-11)

- **El orden del plan**, tal cual y sin adelantos («Adelante con el orden del plan»).
- **El servidor: el equipo principal de recepción** (decisión 1). Sus códigos de grupo, empresa y local son permanentes.
- **Permiso para la F0 entera** («adelante»): programar, subir y mezclar los cuatro cortes. Gastado: la F1 necesita el suyo.
- Prueba por su cuenta en ese equipo mientras tanto («voy haciendo yo pruebas sobre lo que tengo instalado en este equipo»).

## Lo que espera al promotor

1. **Instalar la `0.47.1-f0.2`** en recepción (run de arriba) y hacer **la prueba de 30 minutos** con aparatos reales (`docs/reviews/2026-10-28-usabilidad-sala-y-cocina.md`, apartado 6) con los guiones de F0.1 y F0.2. Lo que salga: si impide abrir, un corte correctivo de la F0; si no, a la cola de la F8.
2. **Las decisiones 2 a 11 del plan** (`docs/plans/2026-11-03-plan-por-fases.md`, apartado 2), empezando por las 2, 3 y 4, que bloquean la F1 (AÑITSOC en el mismo servidor; una persona en las dos empresas; de quién son los vinos, los clientes y los proveedores). Recomendaciones dadas el 03-11: sí, sí con empresa por defecto, y de AÑITSOC con venta semanal a Retiro (a confirmar con el gestor).
3. **Las preguntas al gestor y a Verial**, empezando por el 1-1-2027 (AÑITSOC es SL) y si Verial cumplirá.
4. **El móvil Samsung que no conecta** (abajo).

## Siguiente trabajo, en orden

1. Lo que pida la prueba del promotor que impida abrir (correctivo de la F0, tamaño S o M, con su permiso).
2. **F1 — Varias empresas** (8 cortes; `0.48.0-f1.1` en adelante), cuando conteste las decisiones 2 a 4: 1.1 empresa por credencial con su ADR, 1.2 avisos por empresa, 1.3 ficha de empresa, 1.4 usuarios en pantalla, 1.5 la empresa en el PC y módulos, 1.6 y 1.7 catálogo común, 1.8 precios e IVA por empresa. Antes de codificar, el plan de la fase en pocas líneas y **permiso para programar, subir y mezclar la F1** (no está dado).
3. Después, el orden de `docs/ORDEN_VIGENTE.md`: F2 caja, F3 facturas y VERI*FACTU, F4 stock, F5 carta, F6 apagar Verial, F7 reservas, F8 mejoras.

## El móvil del promotor que no conecta (abierto)

La tablet de cocina (`192.168.0.103`) entra por el QR; el móvil Samsung (`192.168.0.77`) dice «No se puede acceder a este sitio web» con `http://192.168.0.81:5089`. Comprobado desde el PC, solo lectura: el motor responde en esa dirección, la red de Windows está en «Privada», la regla `Costina-HTTP-LAN` del firewall de Windows existe, el PC resuelve la dirección física del móvil (está en la red) y **no le llega ninguna conexión** suya al 5089. El PC lleva **ESET Security con su propio cortafuegos**, que manda sobre el de Windows: el promotor ya permitió el puerto (o la red) en ESET y la tablet entró. Para el móvil queda por saber el código de error de Chrome (`ERR_CONNECTION_TIMED_OUT` apunta a ESET vetando esa IP; `ERR_ADDRESS_UNREACHABLE` o `ERR_NETWORK_CHANGED`, al móvil) y mirar en ESET «Direcciones IP bloqueadas temporalmente» y el registro de «Protección de red»; en el Samsung, «Wi-Fi segura» (una VPN), «Cambiar a datos móviles», VPN y DNS privado, y «Usar siempre conexiones seguras» en Chrome. **Para la cola de la F8:** que Dispositivos avise de un cortafuegos de terceros (ESET, Kaspersky…) como avisa de la red pública; y que el registro del servidor pueda apuntar las conexiones de la red local cuando se pida (hoy no apunta peticiones, D5.2, y no se puede ver desde el PC si un aparato llega).

## Trampas nuevas de esta sesión

- **Un `watch` de Vue no corre si el componente se desmonta en el mismo ciclo** (el padre se actualiza antes que el hijo y Vue descarta los trabajos del hijo): el borrado del borrador al «Salir sin enviar» tuvo que ir explícito en el descarte (F0.2). Para lo que deba pasar sí o sí al cerrar, no fiarse del `watch`.
- **Una hoja modal nueva en Sala cambia las pruebas E2E viejas:** `drinks.spec` recargaba con un agua sin enviar y, con F0.2, al entrar en la mesa salía la pregunta; el toque siguiente de la prueba se quedó esperando detrás del velo hasta agotar los 5 minutos (y la M1 quedó abierta para el proyecto `mobile`). Playwright no lo dice como un fallo del paso: lo dice el `error-context.md` de `test-results/`.
- **`deviceName()` de `e2e/support.ts` exige una etiqueta por proyecto** (`TAGS`): un spec de capturas nuevo usa un nombre propio con `RUN`, como `captures-c3e.spec.ts`.
- **`remove-restriction` no cierra la revisión pendiente** que esa alergia abrió: en el spec de capturas de C3d, retirar la alergia tardía dejaba la Mesa 4 sin poder validar; se decide por API (`review-preparation`) en su lugar.
- **Los finales de línea de la copia de trabajo son CRLF** (autocrlf): un parche de Python con textos LF no encuentra nada; los scripts de esta sesión convierten según el fichero. Y un bloque añadido con LF deja el fichero mezclado: normalizar.
- `rm -f "$VAR"/*.png` lo para una comprobación de seguridad del propio Claude Code; no hacía falta (los specs sobrescriben).
- El registro del servidor **no apunta las peticiones** (D5.2): no sirve para ver si un aparato de la red llega. `Get-NetNeighbor` y `netstat -ano` desde el PC, sí.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión. Háblame siempre en castellano, en palabras llanas, con una recomendación clara en cada decisión; suelo contestar en corto ("1b, 2a…").

Primero comprueba que `develop` está en `f34f055` o posterior (git fetch; git log -1 origin/develop). Lee, en este orden:
1. docs/handoff/2026-11-03-f0-entregada.md (punto de entrada: estado, decisiones, lo que me queda por decidir, el móvil que no conecta y las trampas);
2. docs/ORDEN_VIGENTE.md y docs/plans/2026-11-03-plan-por-fases.md (APROBADO; decisiones pendientes con fecha);
3. docs/native/F0.1-alergias-i-aviso-en-cocina.md y docs/native/F0.2-alergias-ii-borrador-en-el-movil.md;
4. docs/handoff/2026-11-03-plan-por-fases.md (rumbo, cómo se trabaja, lo que no se toca);
5. dotnet/AGENTS.md (párrafos F0.1 y F0.2) y docs/STATUS.md.

Reglas: un corte por PR desde `develop`; propón el plan de cada fase antes de codificar; cada fase necesita mi permiso para programar, subir y mezclar (la F0 ya está entregada; la F1 no tiene permiso); merge commit "Merge PR #N: ..." hecho en local con --no-ff y push cuando la CI está en verde y LEÍDA en los registros; antes de cada push que toque dotnet/ o frontend/pwa/: `cd frontend/pwa && npm run build`, `COSTINA_E2E=1 sh tools/agent/local-tests.sh` entero (en segundo plano, leyendo el registro hasta `exit`) y `sh tools/agent/local-tests.sh desktop` (leer su última línea), en el puerto 5090, nunca como administrador, y en este PC con PG_BIN al PostgreSQL 17 instalado; una sola batería a la vez; GitHub sin gh (sh tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop, frontend/pwa/package.json (+lock) y el diagnóstico simulado de DesktopChecks; commits convencionales sin acentos con la línea Co-Authored-By; un bloque cada vez, cortes M como máximo, lo nuevo al final de la cola, documentos cortos; nunca cierres #26/#27/#28 ni toques #25; nunca toques la instalación real de un PC (servicios Costina y CostinaPostgres, puertos 5088 y 5089), nunca como administrador, nunca npm run dev; nunca metas credenciales en ninguna web; pide permiso antes de cada descarga (fichero, origen, tamaño); antes de nada, `git fetch` y el último docs/handoff.

Estado: develop = f34f055 (0.47.1-f0.2, esquema v8), sin PR abiertas; la F0 está entregada (0.0 a 0.3). El servidor es mi equipo principal de recepción, con la 0.46.2 instalada; el instalador de la F0 es el run 41687659139. Siguiente: lo que salga de mi prueba de 30 minutos (si impide abrir) y después la F1 (varias empresas), que necesita mis decisiones 2 a 4 del plan y mi permiso para la fase. Empieza resumiéndome en pocas líneas dónde estamos y qué tengo pendiente de decidir, y espera mi respuesta.
```
