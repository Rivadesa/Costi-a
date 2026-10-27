# ADR-018 — «Mis mesas» por dispositivo y estación: presentación, no atribución a una persona

**Estado:** aceptado (2026-10-27). Decisiones del promotor:
- 22-10, al explicar lo que quería ver en sala: **Sala enseña primero «Mis mesas»** (las que abrió y las de su sala; cada móvil va emparejado a una estación de sala) **y después «Todas»** (`docs/plans/2026-10-22-sala-y-cocina.md:887`; en adelante, `plan22`);
- 22-10, en el mismo plan: el camarero envía cada pase, **recibe el aviso** con la banda, la vibración en Android y su mesa en verde, sirve y toca «Servido»; con la aplicación cerrada no hay aviso (límite del iPhone aceptado, `plan22:885-886`);
- 22-10, «Si a todo, la 5, va haber android y apple»: el plan entero, con su sección 4.4 (C3e);
- 27-10, «Adelante»: permiso para subir y mezclar C3e.

Se hace en C3e (`docs/plans/2026-10-27-c3e-costina-sala.md`; `docs/native/C3e-costina-sala.md`). **Acota la banda de C3c**, que avisaba de todas las mesas (`docs/native/C3c-dos-apps.md`; `dotnet/AGENTS.md`, párrafo C3c): desde C3e avisa solo de «Mis mesas». No cambia la auditoría ni lo que deja para el Hito 7 el plan de Inicio y dispositivos (`docs/plans/2026-09-24-inicio-y-dispositivos.md:135`).

## Contexto

C3c (22-10) hizo Costiña Sala con el aviso de «listo para servir»: la banda «Mesa 5 · Principal listo para servir», la vibración en Android y la mesa en verde. Avisaba de **todas** las mesas: en un local con varias estaciones de sala, cada móvil vibraría por las mesas de los demás.

Lo que había para decidir de quién es una mesa:
- **Quién la abrió.** Desde E4e, la orden `open` guarda en la instantánea de la mesa `openedBy`, el actor de la orden (`device:<nombre>` para un dispositivo emparejado, `user:<nombre>` para una persona), y `openedStation`, la estación del dispositivo (una persona no tiene). Sin columna ni evento nuevo (`Dining/DiningOperations.cs:48`, `dotnet/AGENTS.md`, párrafo E4e). Las mesas abiertas antes de E4e no lo tienen.
- **Quién pregunta.** `/session` da a cada dispositivo su actor y su estación (`Server/DesktopReadRoutes.cs:26-30`); el texto `device:<nombre>` lo forma el servidor (`Server/Program.cs:372-373`). El actor y la estación de una credencial no cambian mientras dure: el motor no tiene ninguna ruta que cambie el nombre o la estación de un dispositivo emparejado.
- **El nombre del dispositivo no es único.** El PC solo avisa de un nombre repetido (F12). Dos móviles con el mismo nombre son el mismo actor, y uno que se empareja de nuevo con su mismo nombre conserva sus mesas.
- **Las estaciones no saben de qué sala son.** Las mesas tienen sala (`Core.Domain/Organization.cs:10`), las estaciones no (`:11`). Unirlas es esquema (`core.stations.zone_id`, `plan22:819`).
- **El PC también abre mesas como dispositivo.** En el nivel Puesto es un dispositivo de sala con la estación elegida al aprobarlo (`Costina.Desktop/ViewModels/DevicesViewModel.cs:466`; sin ninguna estación de sala, Dispositivos ofrece el botón «Crear la estación «Sala»», `:493-497`). Con la contraseña del responsable es una persona, sin estación.
- **Atribuir a una persona** (el camarero) quedó para el Hito 7 y «exige ADR» (`docs/plans/2026-09-24-inicio-y-dispositivos.md:135`; `plan22:178`).

Costiña tiene una sola sala y una sola estación de sala.

## Decisión

1. **Qué mesa es «mía».** Una mesa ocupada es «mía» en un dispositivo si:
   - **la abrió este dispositivo:** `openedBy` es igual, letra a letra, al actor que da `/session` («device:Móvil de Ana»). Se compara con el texto del servidor, nunca con uno rehecho con el nombre guardado en el móvil;
   - **o se abrió desde su misma estación:** `openedStation` no está vacío y es igual al código de la estación de `/session`, que no cambia al renombrarla. Son las de los otros móviles de «Sala 1», y las del PC si su estación es «Sala 1»;
   - **o no se sabe quién la abrió:** sin `openedBy` (abierta antes de E4e) o sin `openedStation` (abierta por una persona, que no tiene estación). Un texto vacío vale como no sabido. Mejor avisar de más que de menos (`plan22:734`).

   Un dispositivo sin estación solo tiene por «mías» las que abrió él y las de origen desconocido.
2. **Es presentación, nunca permiso.** «Mis mesas» ordena la lectura del tablero y nada más. En «Todas», cualquier móvil de sala ve todas las mesas y hace lo que el servidor le anuncie, también en una mesa que no es «suya»: el servidor no filtra «Servido» por la estación de sala (`Dining/Affordances.cs`). Lo que puede hacer cada dispositivo lo siguen decidiendo el servidor y su matriz de estación (D3.1, D4.3a, `DiningOperations.StationAllowed`). Un botón existe solo porque el servidor anuncia su acción.
3. **No es atribución a una persona.** El dispositivo no es quien atiende la mesa: el nombre del dispositivo no es único (F12), y un móvil compartido o prestado sigue siendo el mismo actor. La auditoría sigue siendo el actor `device:<nombre>`, sin ningún cambio. Atribuir una mesa a un camarero, con su PIN, es E5 y el Hito 7, con su propio ADR (`plan22:178,826`).
4. **La banda avisa solo de «Mis mesas».** La banda y la vibración leen solo las mesas «mías» (`pwa/sala.ts`, `mineEntries`), en las dos pestañas. En «Todas», las demás mesas listas se siguen pintando en verde, sin banda ni vibración. Sustituye en este punto la banda de C3c para todas las mesas.
5. **Un requisito de uso: una sola estación de sala por sala.** En un local con una sola sala, como Costiña, el puesto del PC y todos los móviles de Costiña Sala van emparejados a la **misma** estación de sala. Si no, lo que se abre en el PC como puesto no sale en «Mis mesas» de los móviles ni les avisa. La estación de un dispositivo no se cambia: el que esté en otra se vuelve a emparejar. Lo comprueba «Antes de instalar» del documento del corte y lo recorre su guion corto.
6. **No hay «mi sala».** Mientras una estación no sepa de qué sala es, que es esquema (`plan22:819`), no hay «mi sala» ni «otras salas»: «Mis mesas» agrupa las mías por la sala de cada mesa, y al final salen **todas** las libres, agrupadas igual, para que abrir una mesa siga siendo de tres toques. Ver solo las libres de mi sala, o tener por mías las de mi sala abiertas desde otra estación, irá en un corte de esquema propio cuando un local con varias salas lo pida.

## Alternativas descartadas

- **Seguir avisando de todas las mesas (C3c):** con varias estaciones de sala, cada móvil vibraría por las mesas de los demás, y el promotor pidió «Mis mesas» primero.
- **Atribuir la mesa al camarero ya:** hace falta una identidad de persona en el móvil (el PIN de E5) y su ADR. El nombre del dispositivo no sirve para eso (F12).
- **Que el servidor mande a cada móvil solo sus mesas, o rechace lo de otra estación:** cambiaría el motor y la API (C3e no los toca) y le quitaría al camarero servir la mesa de un compañero, que nadie pidió. Como lo pintado no da permisos, filtrar al pintar no abre nada.
- **Que una mesa sin dato de quién la abrió no sea «mía»:** se perdería el aviso de las mesas abiertas antes de E4e o por el responsable en el PC. Avisar de más es lo seguro.
- **Comparar con el nombre guardado en el móvil:** el texto `device:<nombre>` lo forma el servidor, y rehacerlo en el móvil podría no coincidir. Se compara con el actor de `/session`.
- **Unir estación y sala en este corte:** es esquema y configuración nueva, y Costiña tiene una sola sala.

## Consecuencias

- **En Costiña, con una sola estación de sala, «Mis mesas» y «Todas» enseñan las mismas mesas ocupadas**, y todos los móviles reciben todos los avisos. Lo que cambia es el orden: las listas para servir arriba, dentro de cada sala, y las libres al final. «Mis mesas» se distingue de «Todas» cuando hay varias estaciones de sala.
- **Se avisa de más.** Las mesas sin dato de quién las abrió son «mías» para todos los móviles y avisan a todos. Se mide con el guion (`plan22:870`).
- **Dos móviles con el mismo nombre comparten «Mis mesas»**, como ya compartían la auditoría. Lo mitiga el aviso del PC (F12).
- **El puesto del PC en otra estación de sala:** lo que abre no sale en «Mis mesas» de los móviles ni les avisa. Lo cubren «Antes de instalar», el guion corto y este ADR.
- **Sin cambio de esquema, de motor, de API ni de la CSP.** `openedBy` y `openedStation` solo se leen, y el servidor los omite cuando no los sabe. Como el actor y la estación no cambian con la credencial, el filtro no se rehace: volver a emparejar monta otra vez la pantalla. La pestaña elegida va en memoria; al abrir la aplicación sale «Mis mesas».
- **Las pruebas** lo fijan con dos móviles en dos estaciones de sala (`e2e/mesas.spec.ts`, en el móvil y en el proyecto `sala-vertical`): la mesa que abre A no está en «Mis mesas» de B y sí en «Todas», y la banda y la vibración salen solo en A. La mesa la abre el móvil, nunca el token principal de las pruebas, que es una persona sin estación y la haría «mía» para los dos. vitest fija la regla en `tests/sala.test.ts` y el orden en `tests/mesas.test.ts`.
