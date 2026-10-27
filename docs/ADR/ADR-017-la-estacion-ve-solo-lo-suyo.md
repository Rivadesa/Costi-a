# ADR-017 — Cada estación de cocina ve solo lo suyo; el pase, la mesa entera

**Estado:** aceptado (2026-10-27). Decisiones del promotor:
- 22-10, al explicar lo que quería ver en cocina: **comandas por mesa**, una tarjeta por mesa y pase, con los platos agrupados («Merluza × 4»), la alergia en rojo con su adaptación, el tiempo que lleva y un botón grande «Listo»; **sin líneas por comensal, salvo las de alergias**; y **la tablet del pase ve la mesa entera** y valida cuando todas las estaciones han terminado (`docs/plans/2026-10-22-sala-y-cocina.md`, «Origen» y sección 9);
- 22-10, respuestas a las preguntas 1 a 4 del mismo plan (sección 10): en las estaciones **solo «Listo»**; el pase **puede marcar por una estación**; el tiempo **en ámbar a los 15 minutos y más fuerte a los 25, nunca en rojo**; y **suena** al llegar una comanda, después de «Empezar turno»;
- 27-10, «si, adelante»: permiso para subir y mezclar C3d.

Se hace en C3d (`docs/plans/2026-10-27-c3d-costina-cocina.md`; `docs/native/C3d-costina-cocina.md`). **Sustituye la regla de D6.4** «una estación ve el pase entero, con lo de las demás atenuado, rotulado "(otra estación)" y sin botones» (`docs/native/D6.4-pwa-kds.md`, decisión 1 del promotor del 21-09; `dotnet/AGENTS.md`, párrafo D6.4). **Cambia tres frases de los límites de D6.4:** «sin alarmas por tiempo», «sin sonido […] al entrar un pase» y «no hay vista […] de "lo siguiente" sin enviar».

## Contexto

D6.4 (21-09) hizo la primera pantalla de cocina de la PWA. El promotor decidió entonces que cada estación viera el pase entero, con lo ajeno atenuado y sin botones, y que cada elaboración fuera una línea por comensal con sus botones «Empezar» y «Marcar lista».

Con eso, en una cocina real:
- «Merluza × 4» eran cuatro líneas, y cuatro órdenes para marcarla (ocho con «Empezar»), con la pantalla bloqueada entre una y otra hasta la relectura;
- cada estación leía también las líneas atenuadas de las demás: lo que tenía que hacer quedaba mezclado con lo que no;
- el tiempo era una frase sin aviso, no sonaba nada al llegar un pase y el pase no veía lo que venía después.

Desde C3c cada estación tiene su tablet fija con Costiña Cocina, y desde E4e existen las órdenes de grupo (`preparations-ready`, de 1 a 200 elaboraciones, una subida de versión) y el pase puede marcar por cualquier estación. El 22-10 el promotor pidió comandas por mesa y por estación. Es también la práctica del sector: cada estación de preparación ve sus platos, y una pantalla de expedición (el pase) ve el pedido entero y lo despacha (Toast, Square KDS, Lightspeed, Oracle MICROS y Qamarero; fuentes en el plan del 22-10).

## Decisión

1. **Una estación ve solo su trabajo.** Toda estación que no sea `pase` ve una comanda por mesa y pase con **sus** elaboraciones, de los pases enviados o en preparación. Nunca ve los pases sin enviar, los ya validados ni los de una mesa terminada o cancelada. Las demás estaciones del pase no se pintan: se nombran, con su nombre configurado entero, en «También en: Cocina fría».
2. **El pase ve la mesa entera.** El pase, y un dispositivo de cocina sin estación (que ve lo que el servidor le anuncie, como en D6.4), ven todas las estaciones de cada pase: el progreso de cada una («Cocina caliente: lista · Cocina fría: 1 de 2 platos»), qué falta, «Siguiente: Postre (sin enviar)» y, aparte, lo validado y sin servir («Esperando al camarero»). Un pase validado con algo por revisar sigue en su rejilla hasta que se revise.
3. **Las alergias nunca se esconden ni se atenúan.** Cada estación ve, bajo **su** plato, la línea de cada comensal con restricción o con una decisión del pase, con tipo, sustancia y gravedad y en rojo solo si alarma. Una restricción de toda la mesa sale una vez, arriba de la comanda. Lo que una estación deja de ver son los platos de otra estación, que no cocina, con sus líneas.
4. **Es presentación, nunca permiso.** Lo que pinta cada tablet no es una frontera de seguridad. El servidor sigue decidiendo qué puede hacer cada dispositivo (`DiningOperations.StationAllowed`) y qué se le anuncia (`Affordances`, ya filtrado por papel y estación, D3.1, D4.3a y ADR-016). Un botón existe solo porque el servidor anuncia su acción en esa elaboración o en ese pase.
5. **Una orden por gesto, sin «Empezar».** En una estación, «Listo» es **una** orden `preparations-ready` con lo suyo que el servidor le deja marcar; con dos platos o más, cada plato tiene también su «Listo». Lo terminado baja a «Hecho · esperando al pase» y sale cuando el pase valida. En el pase, «Marcar <estación> como lista» va en «Más opciones» de cada comanda y es también una orden (ADR-016, punto 6).
6. **El tiempo avisa sin rojo.** «Lleva N min», siempre escrito: neutro, ámbar desde 15 minutos y ámbar oscuro con letra blanca desde 25. El rojo sigue siendo solo de las alergias. Sustituye «sin alarmas por tiempo».
7. **Suena tras «Empezar turno».** Un tono corto sintetizado con Web Audio cuando la relectura trae una comanda nueva, solo si se ha tocado «Empezar turno» (el navegador no deja sonar sin un toque). Sin ficheros de audio: la CSP no cambia. Sustituye «sin sonido al entrar un pase». Las notificaciones del sistema siguen sin existir.
8. **El pase ve lo siguiente.** «Siguiente: Postre (sin enviar)», solo de lectura: el ritmo lo sigue marcando el camarero. Sustituye «no hay vista de lo siguiente sin enviar». Las estaciones siguen sin ver nada sin enviar.

## Alternativas descartadas

- **Seguir con D6.4:** el promotor pidió lo contrario, y las líneas atenuadas por comensal de otras estaciones son ruido para quien está cocinando.
- **Que el servidor mande a cada estación solo lo suyo:** cambiaría el motor y la API (C3d no los toca), el tablero dejaría de ser el mismo para todos los papeles y el pase seguiría necesitando la mesa entera. Como lo pintado no da permisos, filtrar al pintar no abre nada.
- **Alergias solo en el pase:** nunca. Quien cocina el plato tiene que leer la alergia y su adaptación en su propia comanda.
- **Que las estaciones vean también lo que viene sin enviar:** lo que no se ha enviado no se cocina; el pase sí lo ve.
- **Rojo para una comanda que se retrasa:** se confundiría con una alergia.

## Consecuencias

- **Una estación ve menos que con D6.4.** Para saber qué más lleva el pase tiene «También en: …», y el pase ve la mesa entera. Es un riesgo aceptado en el plan del 22-10 (sección 8).
- **«Listo» no se deshace** (el motor no tiene esa acción; diferido). Si se toca por error, la comanda sigue en la franja «Hecho» con sus alergias, y el pase no valida sin ver el plato.
- **La regla de agrupar platos es común con el PC**, en la tabla `dotnet/tests/fixtures/kitchen-cases.json`, leída por vitest (`cocina.ts`) y por ClientChecks (`Costina.Client/DishGroups.cs`). El Comedor del PC cambia un poco con ella: su línea de comensal ya no repite una restricción de toda la mesa, un «No le afecta» bajo una restricción solo de toda la mesa deja de ser una línea, y un plato para compartir dice de quién es la alergia.
- **Las pruebas de navegador de cocina** corren en un proyecto propio, `tableta` (1180 × 820, táctil), y fijan que la estación no ve lo ajeno; `wrong_station` se sigue probando con el token del propio dispositivo.
- `pwa/kitchen.ts` y sus pruebas se retiran; la lógica vive en `pwa/cocina.ts` (sin DOM) y el turno en `pwa/turno.ts`.
