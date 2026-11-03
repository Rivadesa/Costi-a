# Traspaso 2026-11-03: rumbo nuevo (dejar Verial para todo) y plan por fases (aprobado el 03-11)

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-29-c3e1-arreglos-rapidos-usabilidad.md`. Ese sigue valiendo para C3e.1 y C3e.2: lo que trajeron, sus cifras y sus trampas.

## Leer antes, en este orden

1. Este documento.
2. `docs/plans/2026-11-03-plan-por-fases.md` (**APROBADO el 03-11**, con permiso para la F0 entera): las fases F0 a F8, las decisiones pendientes con su fecha y la regla legal de VERI*FACTU.
3. `docs/reviews/2026-11-02-recapitulacion.md`: dónde estamos, por qué crece el proyecto y qué falta para abrir.
4. `docs/reviews/2026-10-28-usabilidad-sala-y-cocina.md`: la evaluación de sala y cocina, con las 15 decisiones de la sección 5c contestadas el 02-11.
5. `dotnet/AGENTS.md` (los párrafos C3e, C3e.1 y C3e.2 primero) y `docs/STATUS.md`.

## Estado

- **`develop` = `cf63f97`** (Merge PR #106) más este PR. Versión **`0.46.2-c3e.2`**, esquema v8. Ningún PR abierto.
- **Instalador `Costina-Setup-0.46.2-c3e.2.exe`**:
  - run 41032348890, artefacto `costina-windows-installer` 12965624447, con 8 de 8 comprobaciones;
  - **la descarga caduca el 04-11 a las 10:25**; el `.exe` ya descargado no caduca;
  - si caduca antes de bajarlo, se vuelve a generar lanzando el flujo del instalador sobre `develop`.
- **El equipo principal de recepción (el del promotor) tiene instalada la `0.46.2-c3e.2` desde el 03-11**, con los dos servicios en marcha. **Es el servidor** (decisión 1): sus códigos de grupo, empresa y local son permanentes. El promotor prueba por su cuenta en él; nadie más lo toca.
- **Lo hecho del 28-10 al 03-11:**
  - la evaluación de usabilidad y C3e.1, los arreglos rápidos (PR #100 y #101);
  - C3e.2, «No le afecta a ninguno de estos N platos» en dos pasos (PR #102 y #103);
  - las decisiones de la sección 5c y la recapitulación (PR #104);
  - el plan por fases (PR #105).

## El rumbo nuevo (respuestas del promotor del 03-11)

- **Dejar Verial para todo:** se cobrará con este programa.
- **Producto para varios negocios.** El primer cliente es Costiña, con dos empresas:
  - **Retiro**, el restaurante;
  - **AÑITSOC SL**, con almacén, ventas y stock.

  La base multiempresa va primero (F1).
- **Reservas propias** para el restaurante y las villas, desde la web. TheFork y CoverManager siguen «una temporada».
- **Cobro:**
  - datáfono no integrado: se marca «datáfono», se teclea el importe y se acepta a mano;
  - tickets impresos;
  - impresora de comandas opcional, porque Retiro usa pantallas en cocina;
  - facturas completas a empresa o particular.
- **Fiscal** (VERI*FACTU propio o proveedor certificado): lo decide más adelante. El plan pide decidirlo a mediados de diciembre.
- **Carta de vinos online:** hoy es una web en un XAMPP que lee la base de Verial. Tiene que leer del programa, con una consulta pública de solo lectura que no abra la base (F5).
- **La tienda de costinagourmet.com**, en WooCommerce o PrestaShop, la alimentará el programa: precio y stock hacia la tienda y pedidos hacia el programa. Es el E7 del 23-09. Lo decidido el 14-10: los precios de la web son los de sala (tarifa General), y escribir en la tienda necesita las claves de su API, que pone el promotor (F6).
- **Regla legal:** la caja se construye en la F2, pero no cobra de verdad hasta acabar la F3. Hasta entonces cobra Verial. AÑITSOC SL tendría que cumplir VERI*FACTU el 1-1-2027 (a confirmar con el gestor), así que en enero factura Verial o un TPV certificado provisional.

## Decidido el 03-11

- **El orden del plan**, tal cual y sin adelantos («Adelante con el orden del plan»).
- **El servidor: el equipo principal de recepción**, que ya tiene la 0.46.2 instalada.
- **Permiso para la F0 entera** («adelante»): programar, subir y mezclar 0.0, 0.1, 0.2 y 0.3, uno detrás de otro, avisando al mezclar cada uno. Este PR es el 0.0.

## Lo que espera respuesta del promotor

1. **Las decisiones 2 a 7 del plan:**
   - si AÑITSOC trabaja en el mismo local y servidor;
   - si una persona puede entrar en las dos empresas;
   - de quién son los vinos;
   - villas y teléfono;
   - bonos;
   - día D de una vez o por partes.
2. **Las preguntas para el gestor y para Verial** del apartado 2 del plan, empezando por el 1-1-2027.
3. **La prueba de 30 minutos con aparatos reales**, que la decisión 15 del 02-11 pone antes de E4f, y las 9 hojas de prueba física en blanco. Mientras, el promotor prueba por su cuenta: lo que impida abrir entra en 0.1 o 0.2; el resto, a la cola de la F8.

**La F0 está en marcha:** 0.0 (este PR), 0.1 Alergias I (H34: la tablet suena y marca la tarjeta ante una alergia tardía o la decisión del pase, hasta «Visto»), 0.2 Alergias II (H01, decisión 8b: la ronda y la mesa a medio abrir se guardan en el móvil; «Atrás» baja un nivel con pregunta; tirar hacia abajo no recarga) y 0.3 papeles al día.

## Cómo se trabaja (lo que la memoria del otro PC no tiene)

- **Con el promotor:**
  - siempre en castellano y en palabras llanas, sin jerga;
  - una recomendación clara en cada decisión;
  - contesta en corto («1b, 2a…»);
  - nada nuevo sin su permiso, y las peticiones nuevas van al final de la cola, salvo que impidan abrir.
- **Reglas de tamaño:** un bloque cada vez y cortes de tamaño M como máximo (500 a 1.000 líneas); si crece, se parte. Documentos cortos.
- **Git:**
  - un corte por rama desde `develop`; `main` no se toca;
  - commits convencionales, sin acentos;
  - el merge se hace en local («Merge PR #N: …», con `--no-ff`) cuando la CI está en verde y `git diff --stat` entre la punta verificada y el merge sale vacío; después, push;
  - los PR solo de documentación no lanzan CI y se mezclan igual.
- **GitHub sin `gh`:**
  - `sh tools/agent/gh.sh GET /pulls/N`, sin el prefijo `/repos/…`;
  - los cuerpos en JSON, desde un fichero UTF-8 (`sh tools/agent/gh.sh POST /pulls cuerpo.json`);
  - los registros de la CI con `sh tools/agent/gh.sh LOGS <job_id>`.
- **Pruebas en Windows, nunca como administrador:**
  - `cd frontend/pwa && npm run build`;
  - después `COSTINA_E2E=1 sh tools/agent/local-tests.sh`, en segundo plano, leyendo el registro hasta la línea `exit`;
  - y `sh tools/agent/local-tests.sh desktop`.

  Los requisitos están en la cabecera de `tools/agent/local-tests.sh`:
  - el SDK .NET de `dotnet/global.json`;
  - PostgreSQL 17 portable en `.lab/pgsql/bin` o `PG_BIN=` (ver `docs/native/lab-engineering.md`);
  - Python 3;
  - Chromium de Playwright en `%USERPROFILE%\.ms-playwright`.

  El servidor de pruebas usa el puerto 5090.
- **Trampas:**
  - `local-tests.sh desktop` siempre devuelve 0: hay que leer la última línea («WPF startup and viewmodel checks passed»);
  - DesktopChecks puede fallar en sitios ajenos si alguien usa el PC: se repite;
  - las especificaciones de capturas van una por ejecución (`C3E_CAPTURES`, `C3D_CAPTURES`, `E4E_CAPTURES`);
  - en el pase, la franja fija «Esperando al camarero» tapa el pie de la columna «Por revisar»: para medir y tocar un botón de ahí, hay que centrarlo antes;
  - los heredocs largos de Bash fallan en Windows: se escribe el fichero y se pasa a Python.
- **Lo que no se toca:**
  - la instalación real de un PC: los servicios `Costina` y `CostinaPostgres`, los puertos 5088 y 5089, `C:\Program Files\Costina` y `C:\ProgramData\Costina`; solo se lee su estado;
  - nunca `npm run dev`, porque su proxy apunta a 5088;
  - nunca credenciales en webs;
  - la importación en su instalación la hace el promotor;
  - la incidencia #25 no se toca y la #26, la #27 y la #28 no se cierran.
