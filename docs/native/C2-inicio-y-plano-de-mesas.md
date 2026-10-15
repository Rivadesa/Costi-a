# C2 — Inicio con cifras y Comedor con plano de mesas

Fecha: 2026-10-15. Rama `feat/c2-plano-de-mesas` sobre `develop` (`1e37204`, B.2 mezclada). Segundo corte del plan aprobado `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md`, sección C2, aprobado el 15-10 con «seguimos». Versión **`0.37.0-c2`**. **Sin cambio de esquema** (sigue la v8). El motor solo añade `stations` a `/configuration` y arregla el mensaje de una consola sin permisos.

Lo que verá el promotor:
- **Inicio como panel**:
  - el saludo y el servicio del día;
  - cuatro cifras: mesas ocupadas, comensales, pases en cocina y alergias por revisar;
  - «Sala ahora», un mini plano de la sala;
  - los dispositivos con su conexión, y botones para conectar un comandero o una pantalla de cocina.
- **Comedor y cocina con plano de mesas**:
  - una tarjeta por mesa, agrupadas por sala, con pestañas de sala y filtros;
  - a la derecha, la ficha de la mesa: alergias en rojo, pases numerados y un botón grande con la acción principal.
- **Nombres de estación** en lugar de códigos: «Cocina caliente», no `hot`.
- **Una consola sin permisos** dice que hay que abrir PowerShell como administrador, sin traza ni «Set COSTINA_MODE…».

## Qué cambia

### Motor (dos commits propios)

**Consola sin permisos** (`ServerSettings`, `Program.cs` y `StatusReport`):
- **El fallo.** `File.Exists` da falso ante un acceso denegado. En un PowerShell sin administrador, `server.json` parecía no existir y el motor acababa pidiendo «Set COSTINA_MODE…». Le pasó al promotor con `import-catalog`.
- **Lectura directa.** Ahora el fichero se lee sin esa comprobación previa, y solo «no existe» (fichero o carpeta) cuenta como configuración ausente.
- **El mensaje.** Un acceso denegado detiene la orden con «No se puede leer la configuración de Costina (ruta): abre PowerShell como administrador (clic derecho > Ejecutar como administrador) y repite la orden.», seguido de la misma frase en inglés.
- **`status`** sigue funcionando y dice que la configuración existe pero no se puede leer desde esa consola.
- **Excepción.** Si el entorno define el modo (`COSTINA_MODE` o `COSTINA_LAB_MODE`), un fichero ilegible se ignora como antes. Es el caso del laboratorio, la CI y las pruebas en un PC con una instalación real.
- **Errores sin traza.** Toda orden de consola que falla imprime `ERROR: <motivo>` y sale con código 1. Sin argumentos (el servicio o el laboratorio) nada cambia. El instalador ya trataba cualquier código distinto de 0 como fallo.

**Estaciones para todos** (`GET /configuration`):
- **El campo.** `stations` lleva `id`, `name`, `kind` y `active` para el responsable, la sala y la cocina, ordenadas por orden y código.
- **El tipo** va en minúscula (`kitchen`, `pass`, `bar`, `room`), como en las órdenes y en la base.
- **También las desactivadas**, marcadas, para que dispositivos y elaboraciones antiguos sigan teniendo nombre.
- **Solo añade** y no lleva dinero: la guarda anti-dinero de la PWA lo acepta.
- **Cliente:**
  - `StationChoice`;
  - `Configuration.Stations`, nulo con un servidor anterior;
  - `Names(Configuration)`, que resuelve nombres de mesas y estaciones.

### Comedor y cocina (`ServiceView`, `ServiceViewModel` y `Costina.Client/FloorPlan.cs`)

**Cabecera:**
- «Comedor y cocina» con el resumen («7 de 12 mesas ocupadas · 19 personas»).
- «Abrir mesa» abre el formulario de siempre con una mesa libre ya elegida.
- **Pestañas de sala:** «Todas las salas · 12», «Sala principal · 8», «Terraza · 4». Se ocultan cuando solo hay una sala.
- **Filtros con recuento:** Todas · Ocupadas · En cocina · Para servir · Libres.

**Tarjetas.** Hay una por cada mesa activa de la configuración, cruzada con el tablero y agrupada por sala. Cada tarjeta lleva:
- **el nombre y los minutos** en el estado que se ve:
  - en cocina, desde que se envió el pase;
  - listo, desde que quedó listo;
  - comiendo, desde el último servido;
  - sin tiempo cuando no hay una hora en la que basarse, porque no se inventa;
- **las personas y la oferta**, o las plazas si está libre;
- **el estado**, que es solo de presentación y siempre lleva palabra:
  - Libre · Sentados · En cocina · Listo para servir · Comiendo · En pausa · Terminado, por liberar · Cancelado, por liberar;
  - solo se traducen esos códigos exactos; un estado desconocido del servidor se ve en crudo;
- **el avance de pases** en segmentos, con «1 de 3»;
- **la insignia de restricción, siempre con texto**:

  | Restricción | Texto de la insignia |
  |---|---|
  | Alergia | «Alergia» |
  | Tipo o severidad desconocidos, en crudo | «Anaphylaxis (Critical)» |
  | Intolerancia o preferencia grave | Tipo y severidad: «Intolerancia (grave)» |
  | Intolerancia o preferencia leve o moderada | «Restricción» |

  - Mientras cocina no la ha revisado se añade « · por revisar».
  - El rojo y el ámbar llevan siempre palabras distintas.
- **Aspecto:** la mesa libre con borde discontinuo, la elegida con un anillo verde y la de alarma con borde rojo.
- **Mesas fuera de la configuración:** una mesa ocupada que la configuración ya no lista sigue apareciendo, en «Otras mesas ocupadas».

**La tarjeta solo elige**, no ejecuta nada:
- en una mesa ocupada mueve `SelectedEntry`, que hace la lectura de siempre (invariante F01);
- en una libre mueve `SelectedTable` y abre el formulario de abrir mesa.

**Ficha de la mesa**, a la derecha (400 px):
- **Cabecera:** sala y oferta; la mesa con su estado; las personas y el tiempo («en cocina desde las 09:14 (25 min)»).
- **«Ver cuenta»** abre Caja en la cuenta de esa mesa; desde el puesto pide la contraseña.
  - Si la cuenta no está abierta (pagada o cancelada), no cae en otra mesa: Caja queda sin cuenta elegida y lo explica.
- **Restricciones** en rojo:
  - escritas enteras y sin cortes;
  - con la frase de revisión pendiente;
  - con el formulario de declarar debajo.
  - Sin restricciones, el aviso lleva una marca de visto en lugar del triángulo.
- **Pases** numerados con su estado. «Falta elegir plato» es exacto porque se calcula con el número de personas.
- **Elaboraciones** con el **nombre** de la estación.
- **Abajo, fijos:**
  - el motivo;
  - el **botón grande** con la primera acción anunciada: servir, listo, enviar el siguiente, empezar, reanudar, terminar o liberar;
  - debajo, las demás acciones anunciadas.
- **Las acciones** pasan todas por `ActCommand` y solo se activan con lo que anuncia el servidor.
  - Sin nada anunciado, el botón grande queda apagado y dice qué espera, por ejemplo «Esperando la revisión de cocina».
  - `cancel-unstarted` se anunciaba pero no tenía botón. Ahora tiene uno rojo que pide motivo.

**Lecturas:**
- `RefreshAll` relee `/configuration`, así que una mesa nueva aparece sin reconectar.
- Los minutos avanzan solos cada minuto.

### Inicio (`HomeView` y `HomeViewModel`)

**Cabecera:**
- La fecha y el servicio del día: «Jueves, 15 de octubre · Servicio de comida». A partir de las 17:00 es la cena, la misma regla que usan las ofertas.
- El saludo: «Buenos días, Jefa», o el nombre del puesto en el nivel Puesto.
- «Conectar dispositivo», solo para el responsable.
- «Abrir mesa», si el servidor la anuncia.

**Cuatro cifras**, calculadas con las mismas tarjetas del Comedor, sin lecturas propias:

| Cifra | Qué muestra |
|---|---|
| Mesas ocupadas | «7 de 12», con barra |
| Comensales | El total y las ofertas en uso |
| Pases en cocina | El total y los listos para servir |
| Alergias por revisar | En rojo, con la mesa y la alergia escritas |

- Sin nada que revisar, la última cifra lo dice con palabras.
- **Antes de leer** muestran «—» y «Sin leer todavía», nunca «0» ni «Nada por revisar».

**«Sala ahora»:**
- Un mini plano con el color, la palabra del estado y la insignia de alergia, en dos líneas («Alergia» / «por revisar»).
- La leyenda solo lista los estados presentes. La muestra roja sale solo si alguna mesa tiene alarma.
- Pulsar una mesa abre el Comedor en ella, con la lectura de siempre (F01). «Ver plano» abre el Comedor.

**Dispositivos** (solo el responsable):
- **La lista:** hasta 5, con su estación por nombre y «Conectado» o «Sin conexión», más el recuento.
- **La conexión sencilla:** su estado (B.2) y su dirección.
- **Los asistentes:** «Conectar comandero» y «Pantalla de cocina» abren su asistente en Dispositivos.
  - Si hay varias estaciones posibles, el asistente pide primero la estación y no emite código hasta elegirla.
  - La petición se olvida si se cancela la contraseña o si falla la lectura: nunca se abre sola más tarde.

**Avisos:**
- Arriba, el del certificado y el de red pública de Windows.
- Abajo, la conexión sencilla, la última copia y la versión del servidor.
- Inicio lee también `GET /installation/network` (solo el responsable). Si un servidor anterior responde 404, no se muestra.

### Componentes

- **Nuevos** (`Components.xaml`): ToneChip, StepDot, StepBar, Segmented, FilterChips y NoteBar, más el icono `IconClock`.
- **«Listo para servir»** va en `OkText` (`#17744A`) para que el texto blanco pequeño se lea bien.
- **Excepción deliberada:** el botón grande apagado del Comedor lleva el texto más oscuro (`#5A616B`) que los demás botones apagados, porque explica qué se espera.

## Capturas

Las componen los checks del WPF con datos de ejemplo coherentes:
- Sala principal con 8 mesas y Terraza con 4;
- 7 mesas ocupadas en estados distintos;
- una alergia sin revisar y una restricción de tipo desconocido.

**Inicio:**
- [Inicio](images/c2/home.png)
- [Inicio, parte de abajo](images/c2/home-end.png)
- [Inicio en el puesto](images/c2/home-puesto.png)
- [Contraseña pedida, con Inicio sin leer](images/c2/elevation.png)

**Comedor:**
- [Mesa 4](images/c2/service.png)
- [Abrir mesa](images/c2/service-open.png)
- [Comedor en el puesto](images/c2/service-puesto.png)

**Dispositivos:**
- [Asistente: elegir estación](images/c2/devices-choose.png)

## Qué NO cambia

- El esquema (v8), `grants.sql`, las copias y el instalador. Solo cambian las versiones.
- Lo que anuncia el servidor para activar botones, la orden incierta, los niveles Puesto y Responsable (ADR-013) y el invariante F01.
- Caja, Catálogo, Oferta, Salas y Dispositivos conservan su distribución: se reordenan en C2b.
- La PWA, que cambia en C3.

## Comprobaciones automáticas

**`sh tools/agent/local-tests.sh` completo** (puerto 5090):
- **Dominio:** 81.
- **ClientChecks:** **123**, tres más que antes, sobre:
  - las estaciones con un servidor nuevo y con uno anterior;
  - la familia de estados de mesa en el vocabulario;
  - el plano: estados, tiempos, avance, alertas y códigos en crudo.
- **Las 14 suites HTTP**:
  - `packaging_http` **13**. El nuevo `test_06f`, solo en Windows:
    - deniega con icacls la lectura de una carpeta de configuración;
    - comprueba el mensaje, sin traza;
    - comprueba que con `COSTINA_LAB_MODE` no salta.
    - Falla con el motor anterior.
  - `desktop_reads`, `organization_http` y `pwa_http` comprueban `stations`.
- **Realtime:** 12.

**`sh tools/agent/local-tests.sh desktop`**, con estos checks nuevos:
- **Comedor:**
  - una tarjeta por mesa activa, con salas, filtros y recuentos;
  - estados, minutos y alertas con el texto exacto en castellano;
  - la pestaña de sala elegida, pintada también después de un 401;
  - al pulsar una mesa ocupada se lee esa mesa (F01); al pulsar una libre no se lee nada;
  - el botón grande y los secundarios según lo anunciado, con los cuerpos de servir y liberar;
  - nombres de estación, texto de alergia, nivel Puesto, y mesas nuevas o desaparecidas;
  - «Ver cuenta» con la cuenta abierta, sin cuenta abierta y cancelado desde el puesto.
- **Inicio:**
  - las cifras salen de los datos de ejemplo, e Inicio no lee el tablero por su cuenta;
  - «Sin leer todavía» antes de la primera lectura;
  - saludo y servicio a las 16:59 y a las 17:00;
  - la leyenda, sin muestra roja cuando solo hay una intolerancia leve.
- **Dispositivos:**
  - la lista, la conexión sencilla y la red pública en Inicio;
  - los dos asistentes y el paso de elegir estación;
  - la petición se olvida al cancelar la contraseña o si falla la lectura.
- **General:**
  - encaje a 1.280 × 860;
  - barrido de textos sin valores en crudo;
  - ningún error de enlace.

**Revisiones.** Antes de cerrar hubo tres revisiones independientes: comportamiento e invariantes, aspecto frente al lienzo aprobado, y seguridad de alergias y textos. Se aplicaron sus 17 hallazgos. Los más importantes:
- «Ver cuenta» abría sin avisar la cuenta de otra mesa cuando la suya ya estaba cerrada;
- la insignia convertía un tipo de restricción desconocido en «Restricción»;
- ninguna pestaña de sala aparecía elegida;
- Inicio presentaba un comedor sin leer como vacío («0 alergias por revisar»).

## Guion para el promotor

Con la instalación actualizada a esta versión:
1. **Inicio**: la fecha y el servicio, el saludo, las cuatro cifras, «Sala ahora» y, como responsable, Dispositivos.
2. **Abrir una mesa** con «Abrir mesa» y volver a Inicio: aparece «Mesas ocupadas 1 de N» y la mesa sale como «Sentados» en el mini plano.
3. **Pulsar la mesa en el mini plano**: se abre el Comedor con su ficha.
4. **Declarar una alergia**: la tarjeta lleva «Alergia · por revisar» en rojo y sube la cifra «Alergias por revisar».
5. **Enviar el primer pase** con el botón grande: la tarjeta pasa a «En cocina», con minutos, y la elaboración lleva el nombre de la estación.
6. **Desde Inicio, pulsar «Pantalla de cocina»**: pide elegir la estación antes de dar el código.
7. **Probar el mensaje nuevo.** En un PowerShell **sin** administrador:
   ```powershell
   & "C:\Program Files\Costina\server\Costina.Server.exe" import-catalog "$env:USERPROFILE\Documents\costina-vinos-costinagourmet.csv"
   ```
   Debe responder que hay que abrir PowerShell como administrador, sin importar nada.

## Límites

- **Minutos.** Son los del estado que se ve. El servidor no guarda cuándo se abrió la mesa, así que «sentados hace N minutos» necesita un cambio pequeño del motor: la hora de apertura en la vista. Está pendiente de decisión.
- **Foco.** Las tarjetas se rehacen en cada lectura y cada minuto, y en ese momento se pierde el foco del teclado sobre una tarjeta.
- **Lecturas de más.**
  - Cada relectura hace una lectura más, la de `/configuration`.
  - Con Inicio a la vista, hace además tres lecturas del responsable: diagnóstico, dispositivos y red.
- **Mayúsculas del tipo de estación.** `/configuration` lo envía en minúscula y `/organization` con mayúscula.
  - Los dos clientes ignoran las mayúsculas.
  - C3 debe aceptar las minúsculas en `labels.ts`.
- **Hora del servicio.** El servicio del día usa la hora del PC, y el servidor aplica la misma regla con la suya. Coinciden en una instalación de un solo local.
- **Pruebas en el PC de la instalación real.** Las suites que arrancan sin `COSTINA_DATA` leen `C:\ProgramData\Costina\config\server.json`.
  - Sin administrador, el permiso les deniega la lectura y lo ignoran.
  - **Nunca se corre la batería desde una consola de administrador en ese PC.**
- **Resto de vistas.** Caja, Catálogo, Oferta, Salas y Dispositivos se reordenan en C2b.
