# ADR-016 — Revisión de alergias antes de enviar

**Estado:** aceptado (2026-10-26). Decisiones del promotor:
- 15-10, preguntas 1 y 2: **cocina decide cada plato de un comensal con alergia o intolerancia antes de que salga el pase**, y el pase espera; también las intolerancias leves y las preferencias, que cocina despacha con un toque (`docs/plans/2026-10-15-menus-carta-y-bodega.md`, secciones 4.5 y 9; confirmado el 22-10 en `docs/plans/2026-10-22-sala-y-cocina.md`, sección 9);
- 22-10, pregunta 2: **el pase puede marcar «Listo» por una estación con la tablet estropeada** (`docs/plans/2026-10-22-sala-y-cocina.md`, sección 10).

Se hace en E4e (`docs/plans/2026-10-26-e4e-alergias-antes-de-enviar.md`). **Sustituye a D3.5** (`docs/native/D3.5-restriction-review.md`; `dotnet/AGENTS.md`, párrafo D3.5) en lo que dice de «solo lo enviado» y del bloqueo global. **Cambia D4.3a** (`docs/native/D4.3a-station-authz.md`; `dotnet/AGENTS.md`, párrafo D4.1–D4.3b) **solo para el pase**.

## Contexto

D3.5 (hallazgo F05) obliga a cocina a decidir, plato a plato, qué hace con un cambio de restricciones: «no afecta», «adaptar» o «rehacer», siempre con nota. Pero solo marca lo **ya enviado**: un pase sin enviar «proyecta las restricciones vigentes al dispararse», y la cocina las ve escritas en el plato sin que nadie haya tenido que decidir nada.

En Costiña el caso normal es el contrario: la alergia se conoce **al sentarse**, antes de enviar nada. Con D3.5:
- no queda ninguna decisión registrada: el plato del comensal 3 sale con el aviso y con su nombre de siempre, y nadie ve, por ejemplo, «Comensal 3: tosta sin gluten»;
- revisar exige una mesa «En servicio» y un pase activo, así que con la mesa «Sentados» no se puede revisar nada;
- `fire-next` se bloquea ante **cualquier** revisión pendiente, de cualquier pase.

Además, cada estación solo marca su propio trabajo (D4.3a). Si se estropea la tablet de una estación, hoy solo puede marcar por ella el PC con la contraseña del responsable, porque una persona con sesión no tiene estación.

## Decisión

1. **Ningún plato afectado por una restricción se cocina sin una decisión de cocina registrada.**
   - Al declarar o retirar una restricción se marcan «por revisar» todas las elaboraciones a las que aplica **que no estén servidas ni omitidas**, también las de los pases sin enviar.
   - Una elaboración que **nace** para un comensal con restricción nace por revisar: al abrir la mesa con sus alergias, al elegir plato (`choose`, que sustituye la elaboración) y al pedir un plato de la carta (`add-dish`).
   - **Toda la mesa:** una restricción de toda la mesa afecta a todas las elaboraciones, y una elaboración sin comensal (un plato para compartir) recibe cualquier restricción. Revisar de más es seguro; dar por revisado lo que nadie decidió, no.
   - Sin distinción de tipo ni de gravedad: alergias, intolerancias (también leves) y preferencias.
2. **Se revisa también lo que no ha salido**, y con la mesa «Sentados». Las decisiones son las de D3.5, con nota obligatoria:
   - «No afecta»;
   - «Adaptar»: la nota es el plato adaptado («tosta de maíz en vez de pan»), y el nombre del plato no cambia;
   - «Rehacer», **solo para lo enviado**: en un pase sin enviar se rechaza con `nothing_to_remake`.
3. **El bloqueo es por pase, no global.** `fire-next` se niega con `restrictions_unreviewed` solo si **el pase que va a salir** tiene algo por revisar, y el servidor deja de anunciarlo a la vez. Validar y servir siguen como en D3.5: nada por revisar en ese pase.
4. **Lo que no se va a cocinar no se revisa.** Omitir un pase, o cancelar una mesa sin empezar, retira sus revisiones pendientes. Las decisiones ya tomadas se conservan, y el evento cuenta las retiradas (`reviews_withdrawn`).
5. **Menos toques sin perder la regla.**
   - `open` acepta las restricciones en la misma orden y transacción.
   - `fire-next` se admite con la mesa «Sentados»: empieza el servicio y envía el primer pase en una orden, todo o nada.
   - Las órdenes de grupo (`preparations-start|preparations-ready`, `review-preparations`) son **una** orden: el menú entero de un comensal se revisa de una vez, con una decisión y una nota por plato.
6. **El pase puede marcar `preparation(s)-start|ready` de cualquier estación.** Solo cambia D4.3a para el pase: las demás estaciones siguen marcando solo lo suyo, y validar y revisar siguen siendo solo del pase (y del principal, sin estación). El servidor sigue siendo la frontera: la matriz de estación vive en `StationAllowed` y se refleja en las affordances. **La excepción es de un dispositivo de cocina** (o de un principal antiguo) **en el pase, nunca de la sala** (`Affordances.PassMarksAll`, revisión del 26-10): antes de A2 un comandero aprobado sin tocar nada quedaba en `pase` con papel de sala, y A2 no revisa los ya emparejados. Ese móvil sigue marcando solo lo de `pase`.
7. **Las instantáneas aceptan los estados nuevos:** revisiones pendientes en pases sin enviar y con la mesa «Sentados». Una revisión pendiente en un pase servido u omitido, o en una mesa terminada o cancelada, se sigue rechazando. Las instantáneas de antes se leen igual, y la compatibilidad con D3.2 sigue marcando solo los pases activos.

## Alternativas descartadas

- **Seguir con D3.5:** en el caso normal de Costiña (la alergia al sentarse) no quedaría ninguna decisión escrita.
- **Mantener el bloqueo global:** un pase ya revisado no podría salir mientras cocina revisa uno posterior.
- **Cruzar alérgenos en el cliente o en el servidor:** el catálogo no tiene alérgenos por plato, y cruzar el texto libre de una restricción («lactosa» frente a «lácteos») sería una regla de seguridad inventada (D3.1). Si se pide, es un corte propio con esquema.
- **Revisar solo las alergias:** el promotor pidió todas; cocina despacha una preferencia con un toque.
- **Limpiar solas las revisiones pendientes cuando se retira la restricción:** daría por revisado lo que nadie decidió. La retirada pide su propia decisión, como en D3.5.
- **Dejar la estación sin tablet solo al PC del responsable:** el promotor quiere que el pase pueda hacerlo.

## Consecuencias

- **Sin vuelta atrás:** un motor anterior a E4e no lee una mesa guardada con revisiones en pases sin enviar o con la mesa «Sentados» (el esquema no cambia, así que nada lo impide al restaurar una copia). Bajar de versión exige restaurar la copia previa con los binarios anteriores. Por eso E4e trae la guarda del instalador contra bajar de versión y la de `restore` contra una copia de un motor más nuevo; un instalador anterior a E4e no la tiene.
- Una mesa abierta al actualizar conserva su estado: sus pases sin enviar no quedan marcados hasta el siguiente cambio de restricciones.
- Una alergia declarada al abrir **detiene el primer pase hasta que el pase la revisa**. Por eso la pantalla del pase de Costiña Cocina lista también lo que está por revisar en pases sin enviar, y el puesto principal revisa por plato agrupado.
- Un menú degustación de 7 pases son 7 platos por comensal: la revisión de grupo lo deja en una orden por comensal.
- El Comedor y Sala ven «por revisar» desde que se sientan, porque `restrictionsPendingAck` se deriva de todas las elaboraciones marcadas.
- Desde un pase, cualquier elaboración se puede marcar lista: si se toca por error, el pase sigue sin validar hasta ver el plato.
