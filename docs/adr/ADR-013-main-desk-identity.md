# ADR-013 — Puesto principal: identidad de equipo + responsable

**Estado:** aceptado (2026-10-13). Plan de origen: `docs/plans/2026-09-24-inicio-y-dispositivos.md` (corte B), decisiones del promotor del 06-10-2026.

## Contexto

El PC principal del restaurante ejecuta el motor como servicio de Windows y, desde D4.3b, el programa de escritorio exige entrar con usuario y contraseña cada vez que se abre: la contraseña nunca va a disco y la sesión de persona vive solo en memoria (30 min deslizantes, 14 h máximo). Es correcto para la caja y la configuración, pero el PC es también el comandero grande de la sala: al encenderlo por la mañana tiene que quedar operativo sin que nadie teclee nada, y lo sensible tiene que seguir atribuido a una persona.

El promotor decidió (06-10-2026): el PC principal es el ERP y desde él se accede a todo; sin contraseña toma comandas; cobrar, cerrar cuentas, configurar y conectar dispositivos piden la contraseña del responsable hasta que E5 traiga el PIN.

## Decisión

El puesto principal lleva **dos identidades** y la pantalla tiene **dos niveles**:

- **Nivel Puesto — identidad de equipo.** El PC se empareja como un dispositivo `service` con una estación de sala (D4.2), igual que una tablet. Su token vive cifrado con DPAPI en el perfil del usuario de Windows (`DpapiDeviceStore`): es el único secreto en disco. Al arrancar, el programa espera al servidor y entra solo con esa identidad. Toma comandas; la auditoría dice `device:pc-<equipo>`.
- **Nivel Responsable — identidad de persona.** Usuario y contraseña (D4.1); sesión solo en memoria; caduca sola. Abre caja, configuración, ERP y dispositivos. Tras 15 minutos sin teclado ni ratón, o si el servidor devuelve 401, la pantalla vuelve al nivel Puesto sin cerrar el programa y sin perder la orden sin confirmar (que sigue en disco, aislada por rol, y reaparece al volver a entrar).
- **Nada se oculta.** La barra de menús y la pantalla Inicio muestran todos los apartados en ambos niveles. Un apartado sensible pedido desde el nivel Puesto abre la entrada del responsable y, al entrar, se abre ese apartado. El servidor sigue siendo la frontera (403 para un dispositivo en `/checkout/*`, `/organization/*`, `/erp/*`, `/auth/*` de administración).
- **El responsable deja el PC conectado** desde Dispositivos («Dejar este PC conectado para el servicio»): el propio programa emite el código, lo reclama, comprueba que la solicitud reclamada es la emitida, la aprueba con la estación de sala elegida y recoge el token. Auditado a nombre de la persona (`decided_by`).
- **El programa arranca con Windows** (acceso directo en Inicio, instalador) y es de instancia única.

## Alternativas descartadas

- Guardar la contraseña o la sesión cifradas: lo prohíben `dotnet/AGENTS.md` y ADR-007 (los tokens de persona caducan por diseño).
- El PC como dispositivo con rol `main`: nadie identificado al cobrar y podría aprobar más principales; el corte A lo vetó en el servidor (`device_role_not_allowed`).
- Una sesión larga «recordarme»: equivale a una contraseña en disco.
- Confiar en todo lo que llega por loopback: cualquier proceso o usuario de Windows tendría acceso y no habría nada que revocar.
- Autenticación integrada de Windows: esquema nuevo en el servidor que casa mal con una LAN sin dominio.
- Ocultar los apartados sensibles en el nivel Puesto: el promotor quiere ver todo el ERP; lo sensible se protege al pulsarlo.

## Consecuencias

- Cualquiera delante del PC toma comandas como `device:pc-<equipo>`, igual que con una tablet; caja, cierre y configuración siguen protegidos. La atribución personal del camarero llega con el PIN (E5).
- DPAPI es por usuario de Windows: otro usuario o una reinstalación obligan a volver a dejar el PC conectado (un clic como responsable).
- El tope de 14 h de la sesión de persona corta al responsable en jornadas largas; el puesto sigue operativo.
- Las preferencias del equipo (dirección del servidor, usuario, dirección para los dispositivos) viven en `%LOCALAPPDATA%\Costina\desktop.json`, nunca secretos.
- Supersede en parte a D4.3b (entrada obligatoria por persona en cada apertura). ADR-007 y ADR-009 intactos.
