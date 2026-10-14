# C1 — Nuevo aspecto del puesto principal: Fluent claro, barra lateral, Primeros pasos y todo en castellano

Fecha: 2026-10-14. Rama `feat/c1-nuevo-aspecto` sobre `develop` (`2749883`, B.1 mezclada). Primer corte del plan aprobado `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md`, que sigue la propuesta visual que el promotor eligió el 14-10: tema claro, barra lateral y menú. Versión **`0.34.0-c1`**. **Sin cambio de esquema ni del motor.**

Lo que verá el promotor:
- **Un PC sin conectar** abre en **Primeros pasos**: el estado del servidor, los dos pasos (entrar como responsable; dejar este PC conectado) y todo lo que hay dentro. Sustituye a la pantalla casi vacía de antes.
- **Ya dentro**:
  - aspecto Windows 11 en claro (fondo gris claro, tarjetas blancas redondeadas, verde `#0E7A5F`, letra Segoe UI Variable, iconos);
  - una **barra lateral** (Servicio, Gestión, Próximamente) que se pliega a iconos, junto a la barra de menús de siempre;
  - todos los estados en castellano y con nombres: «Mesa 1 · 2 personas · En servicio», no «M1 · InService».
- **Al pedir Caja o Configuración desde el nivel Puesto**, la contraseña se pide en una tarjeta centrada sobre un velo y la vista no cambia hasta entrar.

## Qué cambia

**Arreglo previo (commit propio):**
- Un **puesto adicional ya no revienta al arrancar**. Antes, si `desktop.json` guardaba una dirección distinta de la de por defecto (por ejemplo `https://EQUIPO.local:5443`), `ShellViewModel` fallaba con una NullReference: asignaba la dirección antes de crear sus vistas.
- Check de regresión: falla sin el arreglo.

**Extracción sin cambio visual:**
- Cada vista pasa a su `UserControl` en `Views/` (Inicio, Comedor, Caja, Catálogo, Oferta, Salas, Dispositivos), dentro de las mismas pestañas ocultas.
- Los conversores van a `Converters.xaml`.
- La guarda de `TabChanged` compara la pestaña exacta.
- Dentro de las vistas se navega con `NavigationCommands.GoToPage`, por el mismo camino que el menú, incluida la petición de contraseña.
- Se comprobó que las 10 capturas salían idénticas byte a byte.

**Castellano en todo** (`Costina.Client/Labels.cs`, `Names.cs`):
- **Un vocabulario único** (tabla del plan, sección 4): servicio, pase, elaboración, comensal, ocupación, cuenta, cobertura, forma de pago, tipo y servicio de oferta, roles, presencia, tiempo real, acciones, rechazos, «activo» y plurales.
- **Un valor desconocido se enseña tal cual, nunca suavizado.** Antes una restricción desconocida salía como «Preferencia · leve».
- **Nombres en lugar de códigos**: mesas, salas y personas (sin `user:`/`device:`).
- **Rechazos del servidor como frases**: un código desconocido se ve como «El servidor rechazó la operación (código X)», y una orden sin confirmar dice que puede haberse aplicado o no.
- La hora de «Servido» sale en hora local (antes en UTC).
- «Carta» aparece en el desplegable de abrir mesa y como tipo en la ficha de oferta.
- Sin códigos de hito ni líneas de comando en los textos: las entradas que faltan dicen «Próximamente».

**Tema y cascarón:**
- **Fluent.Light** de WPF como base, con nuestros diccionarios: `Themes/Tokens`, `Icons`, `Controls` y `Components`.
  - El acento se fija con los 7 `SystemColors.AccentColor*Key`, así que no depende de la personalización de cada PC.
  - Sin Mica, para que la ventana sea opaca y las capturas deterministas.
  - Todos los estilos son `BasedOn` Fluent, sin estilo global de `TextBlock`; arregla el texto oscuro sobre los botones principales.
  - Menú compacto de 32 px; tablas con filas de 28 px.
- **Componentes**: tarjeta, ficha, avisos (información, aviso, peligro, alergia), chips de estado, punto de estado, elemento de navegación y acceso directo.
- **Barra lateral `SideNav`**:
  - Comedor aparece solo con el módulo; Próximamente sale deshabilitado;
  - el resaltado sigue a la vista abierta (`CurrentView`), no al botón pulsado;
  - se pliega con su botón o sola si la ventana mide menos de 1.270 px.
- **Barra superior**: indica el nivel («Responsable · jefa» o «Puesto · Sala 1») y su acción.
- **Barra de estado**: cada punto lleva su palabra; la versión se lee del ensamblado.
- **Avisos**: solo aparecen cuando hay algo que decir y se pueden cerrar.
- **Panel de arranque**: «Arrancando el servidor…» mientras el motor no responde.
- **Vistas**: todas con el aspecto nuevo, y las fichas sin recortes a 1.280 × 860 con la barra desplegada.
  - Caja: campos con etiqueta e importes alineados a la derecha.
  - Categorías: con sangría real, ya no en letra Consolas.
  - Comedor: restricciones con etiquetas y «PENDIENTE DE REVISIÓN» en rojo.
  - Dispositivos: con su punto de conexión.
- **Arreglo encontrado por las capturas** (commit propio): la ficha de precio del catálogo ya no revienta si el desplegable de tarifa se vacía.
- **Checks aislados**: ya no tocan la instalación real del PC. `DesktopSettings.DataFolder` reúne `desktop.json`, la credencial del puesto y la orden incierta, y los checks lo apuntan a una carpeta temporal.

**Dependencia (commit propio):** `Microsoft.AspNetCore.SignalR.Client` pasa de 10.0.0 a **10.0.13**. El aviso de seguridad GHSA-334f-vvp4-7g6p (13-10-2026) afecta a SignalR.Common 10.0.0–10.0.12, y como la auditoría de NuGet cuenta como error, cualquier restauración nueva fallaba con NU1903, también la de CI.

Capturas compuestas por los checks del WPF, con datos de ejemplo y sin servidor:
- [Primeros pasos](images/c1/startup.png) · [Inicio](images/c1/home.png) · [Comedor](images/c1/service.png) · [Caja](images/c1/checkout.png)
- [Catálogo](images/c1/catalog.png) · [Oferta](images/c1/offers.png) · [Salas, mesas y estaciones](images/c1/configuration.png) · [Dispositivos](images/c1/devices.png)
- [Contraseña pedida desde el puesto](images/c1/elevation.png) · [Barra plegada](images/c1/sidebar-compact.png) · [Arrancando](images/c1/booting.png)

## Qué NO cambia

- El motor, el esquema (v6), `grants.sql`, las copias y el instalador. Solo cambian versiones.
- La barra de menús: 7 entradas con los mismos nombres y orden, y las mismas entradas deshabilitadas.
- Lo que anuncia el servidor para habilitar botones, la orden incierta y su aislamiento por rol, los niveles Puesto/Responsable (ADR-013), lo que ve cada rol y el invariante F01 (la selección dirige la lectura).
- La PWA (corte C3).

## Comprobaciones automáticas

`sh tools/agent/local-tests.sh` completo (puerto 5090):
- dominio 79;
- **ClientChecks 105** (antes 76; los nuevos recorren todos los valores de cada familia del vocabulario: ninguno se queda en crudo y uno desconocido vuelve tal cual);
- las 14 suites HTTP;
- Realtime 12.

`sh tools/agent/local-tests.sh desktop`, con todos los checks anteriores verdes y estos nuevos:
- **Arranque**: arranque con `desktop.json` de un puesto adicional; las vistas creadas antes de conectarse ya tienen activos los accesos `GoToPage`.
- **Barra lateral**:
  - tiene sus tres secciones;
  - Comedor sigue al módulo y Próximamente sale deshabilitado;
  - Caja desde el puesto pide contraseña y mantiene Inicio seleccionado y resaltado;
  - mientras hay una operación en curso no cambia de vista;
  - se pliega y se despliega.
- **Pantallas**:
  - Primeros pasos aparece sin sesión y sigue a la dirección que se teclea;
  - panel de arranque mientras se espera al servidor.
- **Aspecto**: acento `#0E7A5F`; fondo `#F3F4F6` opaco en las capturas; registro del fichero de letra resuelto (`SEGUIVAR.TTF`).
- **Textos**: barrido de todo el texto pintado en cada vista, que falla ante cualquier estado o código en crudo.
- **Regresión del catálogo**: la ficha de precio con el desplegable de tarifa vacío.

Antes de cerrar, tres revisiones independientes: comportamiento (cero errores de enlace con un registro de errores de enlace), aspecto (todas las capturas frente al lienzo aprobado) y seguridad de textos y pruebas. Se aplicaron todos sus hallazgos menos los nombres de estación, que esperan a C2.

## Guion de prueba manual

Sobre la instalación del promotor, actualizada con esta versión:
1. Si el PC no está conectado como puesto, abre en **Primeros pasos**: entrar con el usuario responsable.
2. **Inicio**: barra lateral con Servicio, Gestión y Próximamente; menú de siempre arriba; «Responsable · …» arriba a la derecha.
3. **Dispositivos** › elegir sala › «Dejar este PC conectado para el servicio». Luego **Archivo › Salir del modo responsable**: arriba pasa a «Puesto · Sala …».
4. Pulsar **Cuenta / Caja** en la barra lateral: tarjeta centrada «Entrar como responsable para abrir Cuenta / Caja» sobre el velo; «Seguir como puesto» la cierra sin cambiar de vista.
5. **Comedor**: abrir una mesa con «Menú de ensayo · Degustación»; título, pases y elaboraciones en castellano; declarar una alergia: sale en rojo y con texto.
6. «Plegar la barra» (abajo a la izquierda): solo iconos; volver a desplegar.
7. Reiniciar el PC: Costiña se abre sola, espera al servidor y entra como puesto.

## Límites

- Las estaciones se ven por código (`hot`) en la tabla de elaboraciones del Comedor hasta que C2 añada `stations` a `/configuration`.
- Inicio y Comedor conservan su distribución: el panel con cifras y el plano de mesas llegan en C2.
- Catálogo, Oferta y Salas siguen densos a 1.280 px (con desplazamiento en sus fichas); C2 las reordena.
- Fluent es experimental en .NET 10: una actualización menor del runtime puede mover píxeles en las capturas.
