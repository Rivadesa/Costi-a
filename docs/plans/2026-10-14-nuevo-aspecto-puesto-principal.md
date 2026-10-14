# Plan: nuevo aspecto del puesto principal (cortes C1 y C2; C3 para comanderos y cocina)

Fecha: 2026-10-14. **Estado: APROBADO por el promotor el 14-10** («Claro · Barra lateral y menú · Adelante con el rediseño», sobre la propuesta visual del lienzo privado https://claude.ai/artifact/2nAGKqQSYsvpdMvXi2eCQZ: Inicio, Comedor con plano de mesas y Primeros pasos). Base: `develop` en `2749883` (B.1 mezclada, versión `0.33.1-b1.1`). Va **antes de E8 reservas**; el resto del orden no cambia.

Origen: al instalar la 0.33.1 el promotor escribió «El entorno es feísimo! No se puede hacer algo más moderno y estético?». Ahora mismo pasa esto: los desplegables y los campos usan el aspecto clásico gris de Windows (Aero2, porque `Theme.xaml` les pone colores pero no plantilla) y las tablas no tienen estilo. Los estados salen en inglés (`InService`, `Ready`, `Fired`, `Open`, `a-la-carte`) y en lugar de nombres salen códigos (`hot`, `M1`, `user:jefa`, `fire-next`). No hay iconos, el Inicio está vacío si nadie ha entrado, y los botones principales pintan el texto oscuro sobre cobre (fallo del estilo global de `TextBlock`).

Las decisiones de este plan salen de tres lecturas del código hechas para este plan: un inventario de las vistas y de las restricciones de los checks, una **prueba real del tema Fluent de .NET 10** en un proyecto de ensayo con capturas, y una auditoría de los textos. Las referencias `fichero:línea` son de `2749883`.

## 1. Decisiones de diseño (del promotor)

- **Aspecto Windows 11 (Fluent), tema claro**:
  - fondo neutro `#F3F4F6` y tarjetas blancas con radio 12 y borde `#E2E5E9`;
  - acento verde `#0E7A5F`; el promotor no eligió otro y es el del lienzo;
  - letra Segoe UI Variable (Text para cuerpo, Display para títulos), con Segoe UI de respaldo;
  - iconos de trazo propios;
  - rojo solo para alergias y peligro, ámbar para avisos.
- **Barra lateral y barra de menús clásica a la vez.**
  - **Barra lateral**, por secciones: Servicio (Inicio, Comedor y cocina, Cuenta / Caja); Gestión (Catálogo y tarifas, Oferta: cartas y menús, Salas, mesas y estaciones, Dispositivos); Próximamente (Reservas, Stock y compras, Fiscalidad), deshabilitado con su explicación.
  - **Barra de menús**: sus 7 entradas siguen intactas, en el mismo orden y con los mismos nombres. Es la estructura clásica que el promotor pidió en septiembre.
- **Inicio como panel**:
  - cifras: mesas ocupadas, comensales, pases en cocina, alergias por revisar;
  - mini plano de la sala;
  - dispositivos con su estado de conexión;
  - accesos y avisos (certificado, copia).
- **Comedor con plano de mesas**:
  - tarjetas por sala con el estado en color **y en texto**, el avance de los pases y el tiempo;
  - a la derecha, la ficha de la mesa elegida: restricciones en rojo, pases con su estado y botones grandes.
- **Primeros pasos**: sustituye a la pantalla vacía que ve un PC sin conectar. Muestra:
  - estado del servidor;
  - dos pasos: entrar como responsable y «Dejar este PC conectado»;
  - lo que hay dentro.
- **Todo en castellano**: estados, roles y acciones, con nombres en lugar de códigos.

## 2. Decisiones técnicas (de la prueba real)

1. **Tema Fluent de WPF como base, con nuestros tokens encima.** No se usa un tema hecho a mano desde cero ni una librería de terceros. Fluent ya trae, probadas, las plantillas difíciles: desplegables con teclado, campos de texto, `DataGrid`, barras de desplazamiento, menús, ayudas y foco. En App.xaml se combina `pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml` y, después, nuestros diccionarios. **Nunca `Application.ThemeMode` en C#**: es experimental (error `WPF0001`). **Nunca `Fluent.xaml`**: seguiría el modo oscuro del PC.
2. **Sin Mica.** En el constructor estático de `App` se activa `AppContext.SetSwitch("Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop", true)`. Así llega también a los checks, que hacen `new App()`. Sin esto la ventana es transparente en las capturas y toma el color del fondo de escritorio.
3. **Acento propio en todos los equipos.** Se sustituyen los 7 colores `SystemColors.AccentColor*Key`, con Dark1 = `#0E7A5F` y el resto de la escala calculado. Sin esto, cada PC pondría el azul de su personalización de Windows. Cambiar `SystemAccentColor*` o pinceles sueltos no basta (comprobado).
4. **Todos los estilos implícitos `BasedOn` el de Fluent.** Si no, el control cae al aspecto Aero2 y queda un híbrido. Por ejemplo, el menú actual con Fluent mide 66 px de alto.
   - Botones: el principal `BasedOn AccentButtonStyle`; las demás variantes se cambian sustituyendo claves en `Style.Resources`. Un `Background` suelto se pierde al pasar el ratón.
   - **Se elimina el estilo global de `TextBlock`**: rompe el color del texto de los botones. Letra y tamaño van en la ventana.
5. **Densidad de ERP.** Fluent mide 32 px por control, más compacto que los 36–38 px de hoy. Se reducen:
   - la barra de menús (plantilla propia para los dos roles de primer nivel: 32 px en vez de 49);
   - `ListBoxItemPadding`, `StatusBarItemPadding` y el alto mínimo de celda del `DataGrid` (28 px).
6. **Iconos como geometrías XAML (`Path`)**, no como fuente: la fuente Segoe Fluent Icons no existe en Windows 10 y no se puede redistribuir.
7. **Letra.** El nombre «Segoe UI Variable» no se resuelve en WPF: la ventana actual pinta Segoe UI normal. Los nombres correctos son «Segoe UI Variable Text, Segoe UI» y «Segoe UI Variable Display, Segoe UI». Un check registra qué fichero de letra se resolvió, para explicar diferencias entre las capturas locales y las de CI.
8. **Estructura.**
   - **Cascarón**: `MainWindow` queda con el menú, la barra lateral, los avisos, la capa de Primeros pasos y entrada, las pestañas ocultas y la barra de estado. Conserva los 7 `TabItem` con sus nombres y orden, `LoginCard`, `ConnectButton`, `CancelElevationButton`, `AccessKey` y `PairCode`.
   - **Vistas**: cada una pasa a su `UserControl` en `Views/` y hereda el `DataContext`, así que los enlaces no cambian.
   - **Recursos**: los conversores salen de `Window.Resources` a un diccionario de la aplicación, porque desde un `UserControl` no se ven (comprobado).
   - **Navegación**:
     - la guarda de `TabChanged` pasa a `ReferenceEquals(e.OriginalSource, Tabs)`;
     - dentro de las vistas se navega con `NavigationCommands.GoToPage`, con la misma lógica que `ShowView`;
     - el resaltado de la barra lateral sigue a una propiedad nueva `CurrentView`, no al botón pulsado (si no, Caja se marcaría mientras pide contraseña).
   - **Diccionarios**: `Themes/Tokens.xaml`, `Controls.xaml`, `Components.xaml` e `Icons.xaml`, todos combinados en App.xaml (los checks no ven otros).
9. **Textos.**
   - Una sola clase `Costina.Client/Labels.cs`, con funciones puras por familia: servicio, pase, elaboración, ocupación, cuenta, cobertura, tipo y servicio de oferta, rol, actor, presencia, tiempo real, acción, rechazo, activo y plurales.
   - Los DTO llevan `StateLabel`/`KindLabel` que llaman a `Labels`.
   - Los nombres de mesa y estación salen de la configuración.
   - **Un valor desconocido se muestra tal cual, nunca se rebaja**: hoy una restricción desconocida sale como «Preferencia · leve» (`Models.cs:80-81`), y eso es un fallo de seguridad.

## 3. Cortes

### C1 — Base visual, cascarón y castellano (versión `0.34.0-c1`)

Commits en este orden:
1. **Arreglo**: `ShellViewModel()` revienta al arrancar si `desktop.json` guarda una dirección distinta de la de por defecto, es decir, en cualquier puesto adicional. El motivo: `Endpoint` se asigna antes de crear las vistas y `OnEndpointChanged` → `Sync()` → `Home.Sync()` falla con NullReference (`ShellViewModel.cs:62-65,121,197`). Se arregla y se añade un check de regresión.
2. **Extracción mecánica sin cambio visual.**
   - Vistas a `Views/*View.xaml`.
   - Conversores a `Converters.xaml`.
   - Guarda de `TabChanged` y `GoToPage`.
   - Único check obligatorio: `Program.cs:236` busca `RoomStationBox`/`ApproveStationBox` en `DevicesView`.
   - Las capturas deben salir iguales.
3. **Castellano.**
   - `Labels.cs`; `StateLabel` en `DiningDto`, `CourseDto`, `PreparationDto`, `OccupancyDto` y `AccountDto`; `ServedAtLabel` en hora local.
   - Arreglos:
     - «a-la-carte» → «Carta» en el desplegable de abrir mesa, y «Carta» como tipo en la ficha de oferta;
     - restricciones desconocidas en crudo;
     - plurales;
     - acciones y rechazos del servidor traducidos en los avisos;
     - sin `user:`/`device:`;
     - sin códigos de hito («Hito 7», «E5») ni líneas de comando en los textos para el usuario: sale «Próximamente».
   - Tabla de comprobación en ClientChecks que recorre todos los valores.
4. **Tema y cascarón.**
   - Fluent con tokens; estilos de controles; componentes (tarjeta, aviso, chip de estado, cifra, elemento de navegación); iconos.
   - Barra lateral; menú compacto; avisos como barras de información; barra de estado con la versión leída del ensamblado (deja de estar escrita a mano).
   - Capa de **Primeros pasos**, con la tarjeta de entrada centrada. Pedir un apartado sensible la abre sobre un velo.
   - Todas las vistas reciben el aspecto nuevo de controles y tarjetas sin cambiar su distribución.
   - Checks nuevos:
     - barra lateral con sus secciones, y Comedor solo si hay módulo;
     - Próximamente deshabilitado;
     - pulsar Caja como puesto pide contraseña y no cambia de vista;
     - Primeros pasos visible sin sesión;
     - ningún estado en inglés en las etiquetas;
     - el acento es `#0E7A5F`;
     - letra resuelta.
   - Capturas nuevas.

### C2 — Inicio en panel y Comedor con plano de mesas (versión `0.35.0-c2`)

- **Motor, solo añade**: `GET /configuration` lleva `stations` (`id`, `name`, `kind`, `active`) para que un puesto y la PWA vean los nombres de las estaciones. Hoy `/organization` es solo del responsable.
- **Inicio**:
  - cifras calculadas de `Board` y `Tables`, sin cambios en el servidor;
  - mini plano;
  - lista de dispositivos con conexión (solo responsable; hoy `ApplyDevices` tira las filas);
  - accesos que abren el asistente correcto;
  - avisos.
- **Comedor**:
  - tarjetas de mesa = mesas de la configuración cruzadas con el tablero, agrupadas por sala, con filtros. Una tarjeta solo mueve `SelectedEntry` (ocupada) o `SelectedTable` (libre), así que el invariante F01 no cambia;
  - ficha a la derecha con restricciones, pases como chips y botones grandes; la habilitación sigue saliendo **solo** de lo que anuncia el servidor;
  - estado de la tarjeta, solo de presentación: Libre · Sentados · En cocina · Listo para servir · Comiendo · En pausa · Terminado, por liberar. Con «Alergias por revisar» encima;
  - `RefreshAll` relee `/configuration`, para que una mesa creada aparezca sin reconectar;
  - los datos de ejemplo de los checks pasan a ser coherentes (hoy la captura del comedor mezcla M3 y M1 a mano).
- Caja, Catálogo, Oferta, Configuración y Dispositivos se reordenan para el ancho que deja la barra lateral (ficha a la derecha y listas con buscador), con capturas.

### C3 — Comanderos y pantallas de cocina (PWA)

El mismo aspecto, la misma paleta y el mismo vocabulario en `frontend/pwa` (`labels.ts` con tildes y las palabras de la tabla), con sus E2E. Plan propio cuando acabe C2.

## 4. Vocabulario (una sola tabla para las dos aplicaciones)

| Familia | Valores |
|---|---|
| Servicio | Sentados · En servicio · En pausa · Terminado · Cancelado |
| Pase | Sin enviar · En cocina · En preparación · Listo para servir · Servido · Omitido (con su motivo) · Falta elegir plato · Sin platos pedidos |
| Elaboración | Sin enviar · Por empezar · En preparación · Lista |
| Comensal | Toda la mesa · Comensal n |
| Cuenta | Abierta · Cerrada |
| Cobertura | Sin pagar · Pago parcial · Pagada · Pagado de más |
| Método de pago | Tarjeta · Efectivo · Otro |
| Oferta | Degustación · Menú cerrado · Carta |
| Servicio de la oferta | Comida y cena · Comida · Cena |
| Rol | Responsable · Puesto de sala · Pantalla de cocina |
| Tipo de dispositivo | Comandero · Pantalla de cocina · Principal (antiguo) |
| Dispositivo | Conectado · Sin conexión · Quitado el … |
| Tiempo real | Tiempo real activo · Reconectando… · Sin tiempo real, reintentando |

Reglas de uso:
- Para la gente en mesa se dice «personas». «Plazas» es la capacidad de una mesa. La cifra del Inicio se llama «Comensales».
- Se escriben los nombres de mesa y estación, no sus códigos; el código solo aparece en el campo «Código» de las fichas.
- El mismo rol `service` tiene dos palabras a propósito (revisión de C1): como **sesión** en el puesto principal es «Puesto de sala» (este PC trabajando como puesto); como **dispositivo** en la lista de Dispositivos es «Comandero» (una tablet). Un dispositivo antiguo con rol `main` es «Principal (antiguo)», nunca «Responsable»: responsable es solo una persona con su contraseña.

Decidido por el agente con la delegación del promotor; se puede cambiar cualquier palabra antes de C3. Los textos que el servidor da a los rechazos más comunes (versión cruzada, mesa ocupada, cuenta sin saldar, pases sin servir, restricciones sin revisar, estación equivocada…) se reescriben como frases claras. Los demás salen como «El servidor rechazó la operación (código X)».

## 5. Qué NO cambia

- Lo que hace el programa:
  - las órdenes y su idempotencia;
  - la orden incierta;
  - lo que anuncia el servidor para habilitar botones;
  - los niveles Puesto y Responsable (ADR-013);
  - lo que ve cada rol.
- Esquema (v6), `grants.sql` y copias. En C1, ningún cambio en el motor.
- La barra de menús: 7 entradas, mismos nombres y orden, los mismos elementos deshabilitados.
- Los nombres que buscan los checks y el código.
- La PWA hasta C3.

## 6. Riesgos

- **Fluent es experimental en .NET 10.** Una actualización menor del runtime puede mover píxeles en las capturas. Mitigación: los estilos son `BasedOn`, se pueden sustituir control a control, y se revisa el runtime al actualizar.
- **Ancho.** La barra lateral (unos 232 px) deja unos 1.000 px en la ventana de 1.280. Catálogo y Configuración, con fichas fijas de 250–270 px, se aprietan: en C1 la barra lateral se puede plegar a iconos, y C2 reordena esas vistas.
- **Letra e iconos en Windows 10.** No tiene Segoe UI Variable: pasa a Segoe UI, sin problema visual grave. Los iconos son geometrías, así que no dependen de la letra.
- **Ventanas desplegables en las capturas.** Su animación no se captura bien. Mitigación: no se capturan desplegables abiertos.
- **Muchos textos fijados en los checks.** Unas 30 aserciones del WPF y 6 de ClientChecks cambian con el vocabulario, en la misma PR. La PWA no cambia hasta C3.
- **Orden incierta guardada en disco con su descripción antigua** (`fire-next · M1`): se verá una vez así.

## 7. Pruebas

- **Locales:** `sh tools/agent/local-tests.sh` completo (dominio, ClientChecks, 14 suites, Realtime) y `sh tools/agent/local-tests.sh desktop` con capturas.
- **CI:** se leen los registros de D0, D1, desktop, servicio e instalador.
- **Capturas** en `docs/native/images/c1/` y `c2/`: inicio, comedor, caja, configuración, catálogo, oferta, dispositivos, primeros pasos y elevación.
- **Comprobación visual** de cada captura frente al lienzo antes de cerrar el corte.
