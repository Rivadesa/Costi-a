# Costiña: plan por fases para dejar Verial (03-11-2026)

**Estado: APROBADO por el promotor el 03-11 («Adelante con el orden del plan»), sin adelantar la 7A ni el almacén de AÑITSOC.** Cada fase lleva además su propio permiso para programar, subir y mezclar; la F0 lo tiene desde el 03-11 («adelante»).
Salió de cuatro lectores (lo diseñado en los planes, lo preparado en el código, la Caja actual y los requisitos de fuera), un planificador y un crítico.

Este plan parte de `develop` = `5a14ae6` (versión 0.46.2-c3e.2) y de tus respuestas del 03-11, que mandan sobre cualquier papel anterior. En este documento, **Retiro** es el restaurante, **AÑITSOC** es la segunda empresa, **Costiña** es el cliente (las dos empresas) y **el programa** es el software. Un **corte** es una entrega pequeña. Uno de tamaño **M** tiene entre 500 y 1.000 líneas. Los tamaños y las fechas son estimaciones sin comprobar.

## 1. Qué queremos

- Un **producto** que se pueda instalar en otros restaurantes y empresas del sector. El primer cliente es Costiña.
- Retiro y AÑITSOC se llevan desde el mismo programa: sala, cocina, caja, almacén, ventas y stock. Cada empresa tiene lo suyo y el catálogo es común.
- **Dejar Verial para todo:** cobro, facturas, stock, compras, informes, la carta de vinos del XAMPP y el stock que hoy recibe costinagourmet.com.
- **Reservas propias** para el restaurante y las villas, también desde la web. TheFork y CoverManager siguen «una temporada».
- Cobro con **datáfono no conectado**, tickets impresos y **facturas completas**. Las comandas se imprimen solo donde no haya pantallas.
- Avanzar sin desbordarnos: un bloque cada vez y cortes pequeños.

## 2. Decisiones

### Ya tomadas (03-11 y 02-11)
- **El orden del plan**, 0 → 1 → 2 → 3 → 4 → 5 → 6 (día D) → 7 → 8, tal cual, sin adelantos (03-11).
- **El servidor va en el equipo principal de recepción**, que ya tiene la 0.46.2-c3e.2 instalada y en marcha (03-11). Los códigos de grupo, empresa y local son los de esa instalación: se leen con `status` como administrador y son permanentes.
- Se deja Verial para todo: **se cobrará con el programa desde el día D**.
- Será un producto para varios negocios. AÑITSOC llevará aquí su almacén, sus ventas y su stock.
- Motor de reservas propio para restaurante y villas, con reservas desde la web. TheFork y CoverManager conviven una temporada.
- Datáfono no integrado: se marca «datáfono», se indica el importe y el cobro se acepta a mano.
- Se imprimen tickets. La impresora de comandas es opcional, porque Retiro usará pantallas en cocina.
- Se hacen facturas completas a empresas y a particulares.
- Lo fiscal (VERI*FACTU propio o proveedor certificado) se decide más adelante.
- Carta de vinos: el catálogo y el stock se consultan con una **consulta de solo lectura**. La base de datos no se abre (hoy solo escucha en el propio PC).
- Un bloque cada vez, cortes M como máximo, lo nuevo al final de la cola, documentos cortos y la prueba de 30 minutos antes de E4f.

### Quedan por decidir: próximas 4 semanas
| # | Decisión | Antes de |
|---|---|---|
| 1 | **Decidida el 03-11: el equipo principal de recepción**, que ya tiene la 0.46.2 instalada. Los tres códigos (grupo, empresa y local) son los de esa instalación, se leen con `status` lanzado como administrador y son **permanentes** | hecho |
| 2 | ¿AÑITSOC trabaja en el mismo local y con el mismo servidor, o en otro edificio? Si es otro edificio, cambia el diseño | 1.1 |
| 3 | ¿Una persona puede entrar en las dos empresas, o cada empresa tiene sus propios usuarios? | 1.4 |
| 4 | ¿De quién son los 1.037 vinos importados? ¿Qué empresa cobra una botella para llevar que se vende en la caja de Retiro? ¿Los clientes y los proveedores son del grupo o de cada empresa? | 1.6 y 2.3 |
| 5 | **Villas y teléfono:** ¿dónde se apuntan hoy esas reservas y quién factura las estancias? ¿De qué empresa son las villas? Si hoy lo hace Verial, la factura de estancia y el libro de villas van antes del día D | cerrar la F1 |
| 6 | **Bonos y experiencias:** ¿qué web los vende y está conectada a Verial? ¿Cuántos hay vendidos sin canjear? | 2.1 |
| 7 | **Día D de una vez o por partes.** Por partes significa que Retiro cobra al acabar la F3 y Verial sigue con el stock y la tienda, con un ajuste semanal de inventario. Depende de la lista de usos de Verial de la F0 | cerrar la F1 |
| 8 | **Titular del programa:** quién figura como productor en la declaración responsable, y si el repositorio sigue siendo público | F3 |
| 9 | **Fiscal: VERI*FACTU propio o proveedor certificado.** La vía propia exige un revisor legal externo, así que hay que buscarlo ya | **mediados de diciembre** |
| 10 | Datáfono: ¿el importe lo teclea el encargado en el PC o el camarero en el móvil? Lo segundo cambia la regla de que Sala no ve importes. ¿«Datáfono» sustituye a «Tarjeta»? ¿Se registran las propinas? | 2.1 |
| 11 | Impresora de tickets para Retiro y para la tienda: modelo, si va por red o por USB, y cajón. **Hay que pedirla ya:** si es USB y va en un PC distinto del servidor, cambia el corte 2.6 | 2.6 |

### Quedan por decidir: más adelante
- Si el vino que vende Retiro es de AÑITSOC, cómo se repone. Por ejemplo, con una venta semanal con una sola factura (a confirmar con el gestor). **Antes de la F4.**
- Carta: ¿la web del XAMPP se ve desde internet o solo en el local? ¿Quién la mantiene? ¿Qué precio enseña? ¿Se ofrece en la wifi de clientes? Los aparatos del personal van en otra wifi. **Antes de la F5.**
- Dominio propio. También trae el certificado de la wifi del local, así que conviene **pedirlo ya**. Aceptar una pieza pequeña en internet: **antes de la 7B**.
- Alcance de las villas: si se anuncian en Booking o Airbnb, las temporadas y si se cobra señal. **Antes de la 7A.**
- TheFork y CoverManager: cuál manda hoy, cuánto dura «una temporada» y si se usan cupos por canal o un solo libro. **Antes de la 7B.** Hay que pedir ya a TheFork el acceso B2B y a CoverManager la documentación de su API.
- Nombre comercial del producto en las pantallas («Costina», sin eñe, se queda como nombre interno). **Antes del segundo cliente (F8).**
- ¿Cierra Retiro en invierno? Cambia cuándo se pueden hacer las pruebas. **Ya.**

**Para el gestor y para Verial (no es código; cuanto antes):**
- **AÑITSOC consta como SL.** De Retiro, los documentos hablan de «segunda sociedad», pero hay que confirmarlo. Con la norma vigente (RDL 15/2025), las sociedades tienen que cumplir el **1-1-2027**, dentro de dos meses. El día D llega después, así que **ese día seguirá facturando Verial**.
- A Verial: ¿cumplirá el 1-1-2027? ¿Cuánto cuesta su actualización y cuándo vence el contrato? **Plan B** si no cumple: un TPV certificado provisional hasta el día D. Adelantar la F3 no llega a enero con este ritmo.
- ¿Se puede ensayar la caja con importes mientras cobra Verial? ¿Basta con la pre-cuenta impresa hasta tener lo fiscal?
- Cómo se documenta la venta de vino entre las dos empresas. IVA de la botella para llevar frente al del vino servido en sala (21 % y 10 %, sin comprobar).
- Cómo factura hoy Verial los pedidos de costinagourmet.com.
- Si la señal de una reserva es un anticipo o una fianza. IVA de las villas: de memoria y sin comprobar, 10 % con servicios de hotel y quizá exento sin ellos.

## 3. Las fases

**Por qué este orden:**
- **Primero las empresas:** la caja, las series, el stock y la carta dependen de saber de qué empresa es cada cosa.
- **Caja antes que facturas:** la factura se apoya en la caja (IVA, cliente y cierre).
- **Stock y carta antes del día D,** si se deja Verial de una vez: desde ese día, Verial ya no sabe qué se vende.
- **Reservas después:** TheFork y CoverManager cubren el restaurante mientras tanto, salvo lo que diga la decisión 5.

> **Regla legal.** Un ticket (que es una factura simplificada, RD 1619/2012) o una factura emitidos de verdad con el programa tienen que cumplir el RD 1007/2023 en una de sus dos modalidades: VERI*FACTU o no VERI*FACTU (Orden HAC/1177/2024). Además, llevan la declaración responsable del productor.
> - **La caja se construye en la F2, pero no cobra de verdad hasta acabar la F3 y llegar el día D. Hasta entonces cobra Verial.**
> - La caja se ensaya en una instalación de pruebas, no en la del local, hasta que conteste el gestor.
> - Productor no es solo quien vende el programa. También lo es quien lo desarrolla para su propio uso (preguntas frecuentes de la AEAT; a confirmar).
> - Desde la F3, cada versión instalada necesita su declaración. Por eso habrá **una versión instalada y declarada por fase**, y los cortes intermedios no se instalan en el local.
> - Hasta la F3, a otros clientes solo se les puede ofrecer sala y cocina (a confirmar con el gestor).

### F0. Abrir con Verial cobrando (ya; 3 cortes)
**Para qué:** usar el programa en sala y cocina mientras Verial cobra.
- **Sin código (tú y el encargado):**
  - ~~Descargar e instalar la 0.46.2~~ Hecho el 03-11: instalada en el equipo principal de recepción, que será el servidor. El `.exe` queda guardado en Descargas de ese equipo.
  - Hacer la prueba de 30 minutos y rellenar las 9 hojas de prueba en blanco.
  - Puesta en marcha: servidor con SAI, salas y mesas reales, usuarios y la exportación de Verial con sus tarifas (pendiente desde el 14-10). También la copia en un segundo destino y una restauración ensayada en otra máquina.
  - Procedimientos escritos: cómo pasar la cuenta a Verial, qué hacer si se cae el servidor o la wifi, cómo numerar a los comensales y quién libera las mesas. La información de los 14 alérgenos, fuera del programa.
  - **Lista de lo que Retiro y AÑITSOC usan hoy de Verial:** escandallos, televenta, contabilidad, remesas, factura electrónica, lectura de DNI, firma, alta rápida, libro de reservas, la web del XAMPP… Cada fila va a un corte o queda como «se deja de hacer» con tu visto bueno.
- **0.1 Alergias I (H34):** una alergia anotada tarde llega a la estación con sonido y marca. **Entregado el 03-11** (`0.47.0-f0.1`, `docs/native/F0.1-alergias-i-aviso-en-cocina.md`): también la decisión del pase, también en «Hecho», con «Visto».
- **0.2 Alergias II (H01):** no se pierde lo escrito al pulsar «Atrás» o al recargar. De lo que salga de la prueba, aquí solo entra lo que impida abrir. El resto va a la F8. **Entregado el 03-11** (`0.47.1-f0.2`, `docs/native/F0.2-alergias-ii-borrador-en-el-movil.md`): la ronda y la mesa a medio abrir guardadas en el móvil y preguntadas al volver (8b), «Atrás» que baja un nivel y sin recarga al tirar. Lo que salga de la prueba del promotor, en su momento.
- **0.3 Papeles al día (solo documentos):** una página con el orden vigente. Se quitan las exclusiones viejas: reservas, stock, facturas y bonos. **Entregado el 03-11**: `docs/ORDEN_VIGENTE.md`, y `ROADMAP`, `PRODUCT_SCOPE`, `BACKLOG`, `AI_CONTEXT`, `DECISION_LOG` y el README remiten a ella.
- **Coste:** hasta el día D, cada cuenta se vuelve a teclear en Verial (unos 5 meses). Hasta el corte 2.6, el programa no imprime nada.
- **Terminada cuando:** se complete un servicio sin alergias perdidas y con todas las cuentas pasadas a Verial. La F1 empieza tras la prueba, 0.1 y 0.2, sin esperar a ese servicio.

### F1. Varias empresas (8 cortes)
**Para qué:** que una sola instalación lleve Retiro y AÑITSOC por separado, con catálogo común.
- **1.1 Empresa por credencial:** cada usuario y cada aparato trabajan en su empresa. Incluye su ADR (documento de decisión técnica).
- **1.2 Avisos por empresa:** los avisos en tiempo real y su cola no se cruzan. Se prueba con las dos empresas a la vez.
- **1.3 Ficha de empresa:** NIF, razón social, domicilio y logotipo. Alta de AÑITSOC. Las copias guardan las dos empresas.
- **1.4 Usuarios en pantalla, con su empresa.** Hoy solo se crean por consola.
- **1.5 La empresa en el PC y módulos:** se ve con qué empresa se trabaja y se puede cambiar. AÑITSOC no ve «Comedor».
- **1.6 Catálogo común I:** una sola migración, que ya incluye la tabla de añada y formato, y las lecturas.
- **1.7 Catálogo común II:** cambios en el catálogo, importación y pantallas del PC.
- **1.8 Precios e IVA por empresa:** el IVA sale del producto y pasa a la tarifa o a la empresa, porque el mismo vino no lleva el mismo IVA en sala que para llevar.
- El cambio de nombre en las pantallas pasa a la F8, porque todavía no hay segundo cliente.
- **Depende de:** la prueba, 0.1, 0.2 y las decisiones 1 a 4.
- **Terminada cuando:** una copia de la instalación real, restaurada en otra máquina y migrada, conserve los 1.037 vinos y las 1.017 fotos, y AÑITSOC tenga su ficha y sus usuarios. Todavía no cobra nadie, y **AÑITSOC no tendrá nada usable hasta la F4**.

### F2. Caja (11 cortes; se construye, pero no cobra de verdad)
- **2.1 IVA y formas de pago:**
  - cada línea guarda su IVA (hoy no lo guarda);
  - «Datáfono» con importe a mano y referencia;
  - efectivo con entregado y cambio;
  - «Bono o experiencia» con su referencia;
  - propina, según la decisión 10.

  Los pagos mixtos y parciales ya existen.
- **2.2 Anular, invitar y descontar:** invitación con motivo, y descuento por línea o sobre el total.
- **2.3 Apertura, cierre y arqueo:** lo esperado frente a lo contado, por forma de pago. Una caja por empresa.
- **2.4 Cierre del día:** ventas por forma de pago, por IVA y por familia, con anulaciones, invitaciones y descuentos.
- **2.5 Clientes para factura:** NIF comprobado, razón social y domicilio. Se buscan y se dan de alta desde el PC.
- **2.6 Impresora de tickets:** pre-cuenta impresa, cajón y aviso si falla.
- **2.7 Traspasar:** pasar líneas a otra mesa, cambiar de mesa y juntar mesas.
- **2.8 PIN de cajero en el PC:** se deja de pedir la contraseña del encargado en cada venta.
- **2.9 Venta sin mesa:** cuentas de «Mostrador n.º».
- **2.10 TPV de mostrador I:** familias con color y foto, y teclado numérico.
- **2.11 TPV de mostrador II:** código de barras en el catálogo, lector y «cobrar, imprimir y siguiente».
- **Fuera de esta fase:** dividir la cuenta pasa a la F3, porque cada parte lleva su ticket y eso necesita series. Las comandas impresas pasan a la F8, porque Retiro usa pantallas.
- **Depende de:** la F1, las decisiones 4, 6, 10 y 11, y la impresora ya comprada.
- **Terminada cuando:** un servicio y un día de mostrador ensayados en la instalación de pruebas cuadren al cierre.

### F3. Facturas y VERI*FACTU (6 cortes con proveedor, 9 por la vía propia)
- **Parte A, que sirve elijas lo que elijas:**
  - **3.1 Series y ticket:** series por empresa y año, numeración sin huecos y documentos que no se pueden tocar. Ticket al cerrar la cuenta.
  - **3.2 Factura completa:** a una empresa o a una persona, o en sustitución de tickets ya emitidos. En A4 o PDF y en la impresora de tickets. Con líneas libres para estancias, si lo pide la decisión 5.
  - **3.3 Rectificativas y devoluciones.** Una cuenta que ya tiene factura no se reabre. La declaración responsable se ve en «Acerca de».
  - **3.4 Dividir la cuenta:** por comensal o a partes iguales, cada parte con su ticket.
- **Parte B, después de la decisión 9.** Cada empresa lleva su propia cadena de registros.
  - **Con proveedor:** 2 cortes, para conectar con su servicio y llevar la cola de envíos pendientes con avisos.
  - **Vía propia:** 5 cortes (registro encadenado, QR, envío a Hacienda con certificado, reintentos y rechazos), más la revisión legal externa.
- **Depende de:** la F2 y las decisiones 8 y 9.
- **Terminada cuando:** un ticket y una factura completa de prueba sean aceptados en el entorno de pruebas de la AEAT o del proveedor.

### F4. Stock, compras y escandallos (8 cortes)
- **4.1 Ficha de vino:** añada, formato, uva, bodega y denominación. Rellena la tabla preparada en 1.6 y le da pantalla.
- **4.2 Almacenes y existencias:** cada movimiento tiene dueño (la empresa) y no se permiten negativos. Inventario inicial cargado desde Verial. La reposición entre empresas, según lo que se decida.
- **4.3 Las ventas descuentan:** la caja y el mostrador descuentan stock, la copa sale de una botella abierta y Sala ve «Agotado».
- **4.4 Proveedores y compras:** pedido, recepción y factura del proveedor.
- **4.5 Recuentos y traslados:** inventario, ajustes y traslados entre almacenes. El paso de mercancía entre empresas queda documentado (venta con factura, a confirmar con el gestor).
- **4.6 y 4.7 Combinados (E4c):** dos cortes.
- **4.8 Escandallo sencillo:** el gin-tonic descuenta la ginebra.
- **Depende de:** la F1. Solo 4.3 necesita la caja.
- **Opción:** adelantar 4.1, 4.2, 4.4 y 4.5 justo después de la F1, para que AÑITSOC tenga almacén antes. Retrasa la F3 unas 2 semanas, y mientras venda Verial el stock solo cuadra con recuentos.
- **Terminada cuando:** el recuento de 50 referencias cuadre tras una semana.

### F5. Carta de vinos sin Verial (1 o 2 cortes)
**Para qué:** que la carta de los clientes lea del programa y no de Verial.
- **5.1 Exportación de solo lectura en la red del local:** catálogo, fotos, precio y «quedan pocas» o «agotado», sin enseñar todo el stock. La web del XAMPP la puede leer. Si nadie puede adaptar esa web, el programa sirve su propia carta en el local (1 corte más).
- No hacen falta dominio ni nube. La carta pública en internet va con las reservas (7B).
- **Depende de:** la F4 y la respuesta sobre la web del XAMPP.
- **Terminada cuando:** la carta muestre el stock del programa y Verial ya no la alimente.

### F6. Apagar Verial (8 cortes y el ensayo)
- **6.1 Informes:** ventas por periodo, empresa, familia y producto.
- **6.2 Exportación al gestor:** libro de facturas con el IVA, cierres y compras por empresa.
- **6.3 Tienda web I:** el programa envía el stock y el precio a costinagourmet.com, a **WooCommerce o PrestaShop**. Es el E7 del orden del 23-09. Lo decidido el 14-10: los precios de la web son los de sala (tarifa General), con sitio para más tarifas. Hoy lo hace el conector de Verial, con su código de artículo como SKU, y escribir en la tienda necesita las claves de la API, que pone el promotor.
- **6.4 Tienda web II:** los pedidos reservan y descuentan stock, con conciliación.
- **6.5 Tienda web III:** factura de cada pedido en la serie de AÑITSOC, y devoluciones.
- **6.6 Cupos por canal:** la sala y la tienda no venden la misma última botella.
- **6.7 Bonos ya vendidos:** dónde se apuntan y cómo se canjean.
- **6.8 Traspaso desde Verial:** clientes y cierre de sus series, porque nunca pueden emitir dos programas en la misma serie. Conviene valorar dejar Verial solo para consultar el histórico, en vez de migrarlo entero.
- **Sin código:**
  - ensayo general: una semana cobrando en paralelo en la instalación de pruebas, cuadrando el cierre con Verial;
  - recuento físico la víspera;
  - **día D**: Verial deja de cobrar, de llevar el stock y de alimentar la tienda.
- **Terminada cuando:** haya una semana de cierres cuadrados con Verial.

### F7. Reservas propias (unos 12 cortes; 14 con TheFork automático)
- **7A, sin internet (4 cortes):**
  - villas: calendario por villa, noches, temporadas, estancia mínima y señal;
  - restaurante: franjas, plazas, estados (confirmada, llegó, no vino) y reservas por teléfono. Cuando el cliente llega, la reserva abre la mesa con sus personas y sus alergias.

  La 7A puede ir antes del día D, y lo retrasaría unas 2 semanas.
- **7B, con internet (unos 8 cortes):**
  - una ADR para la pieza en internet, que tiene que funcionar en los dos sentidos;
  - la pieza: servidor, dominio, certificado y entrada de reservas al local;
  - carta pública con QR, que sustituye la web del XAMPP;
  - reserva desde la web como solicitud. Las alergias llevan un consentimiento aparte (a confirmar con la AEPD);
  - confirmación automática con correo y recordatorio;
  - señal con pasarela de pago. El programa nunca guarda la tarjeta;
  - convivencia con TheFork y CoverManager. Conectar TheFork de forma automática cuesta 2 cortes más y solo compensa si la temporada es larga.
- **Parte de viajeros de las villas (SES.HOSPEDAJES):** al principio, a mano (sin comprobar).
- **Depende de:** la F3 (señal y factura), la decisión 5 y, para la 7B, el dominio.
- **Terminada cuando:** una reserva web de villa llegue, se confirme y quede en el libro.

### F8. Mejoras y otros clientes (unos 25 cortes)
- **Sala y cocina:**
  - E4f, menú del día en una pantalla (1 corte; sube a la F0 si la prueba lo pide);
  - arreglos rápidos (2);
  - E4g, carta por familias (3 o 4);
  - de los 4 cortes grandes de usabilidad, solo los que pida la prueba (8 a 10).
- **Producto vendible:**
  - nombre del negocio en pantallas e iconos, más los textos de las pruebas;
  - titular y licencia;
  - instalador firmado (incidencia #12);
  - actualizaciones y soporte remoto para varios clientes;
  - alta rápida de artículos, para negocios sin Verial;
  - impresora USB y comandas impresas por estación y barra (2 o 3);
  - PIN del camarero en el móvil (1 o 2).
- **Factura electrónica entre empresas:** obligatoria desde el 06-10-2028 si se factura menos de 8 M€ (a confirmar).
- **Terminada cuando:** un segundo negocio se instale sin tocar el código.

## 4. Cuánto es

| Fase | Cortes | Semanas | Ventana orientativa |
|---|---|---|---|
| 0 Abrir | 3 | 1–2 | noviembre |
| 1 Empresas | 8 | 2–3 | mediados de noviembre a principios de diciembre |
| 2 Caja | 11 | 3–4 | diciembre a mediados de enero, con las fiestas en medio |
| 3 Fiscal | 6 con proveedor, 9 propio | 2–3, más la revisión legal | mediados de enero a febrero |
| 4 Stock | 8 | 2 | febrero a principios de marzo |
| 5 Carta local | 1–2 | ½ | marzo |
| 6 Apagar Verial | 8, más el ensayo | 3 | marzo. **Día D: finales de marzo en el mejor caso; mayo, lo realista** |
| 7 Reservas | 12 (14) | 3–4 | después del día D (la 7A, antes si lo pides) |
| 8 Mejoras | unos 25 | unas 7 | verano |
| **Total** | **45–50 hasta el día D; unos 85 en total** | | |

- **En qué me baso:**
  - Se han hecho 54 cortes en 45 días, con 18 días sin ninguno. Entre el 13 y el 30-10 fueron 22, y los últimos han tardado cerca de un día cada uno.
  - Aquí cuento **3 o 4 cortes por semana**, porque ahora hay revisiones, una prueba con aparatos al cerrar cada fase (unos 3 días), esperas por decisiones y dos semanas de fiestas.
- **Lo que depende de otros**, con su fecha límite:
  - descargar el instalador: 04-11;
  - exportación de Verial: antes de cerrar la F0;
  - impresora y dominio: pedirlos en noviembre;
  - decisión fiscal: mediados de diciembre;
  - contrato con el proveedor o revisor legal: enero;
  - TheFork y CoverManager: pedir el acceso en noviembre;
  - tus 7 pruebas de fin de fase hasta el día D, y tus aprobaciones. Un parón como el de septiembre y octubre mueve todo lo que va detrás.
- **VERI*FACTU (confírmalo con el gestor):**
  - La norma vigente es el RDL 15/2025: **1-1-2027 para sociedades y 1-7-2027 para el resto**.
  - El aplazamiento a octubre de 2028 se anunció el 05-10, pero no está en el BOE.
  - Como AÑITSOC es SL, en enero tiene que cumplir el programa con el que facture entonces (Verial o el plan B).

## 5. Reglas para no desbordarnos
- Un bloque cada vez: no se abre una fase sin cerrar la anterior y hacer su prueba corta con aparatos reales.
- Ningún corte mayor que M (500 a 1.000 líneas). Si crece, se parte.
- Lo nuevo va al final de la cola, salvo que impida abrir.
- Las decisiones pequeñas se dan por buenas en bloque. Documentos cortos y una sola página con el orden vigente. La ADR de cada fase va en su primer corte.
- La prueba de 30 minutos va antes de E4f. Desde la F3, una sola versión instalada por fase.

## 6. Qué te pido ahora
1. ~~Instalar la 0.46.2 y decir en qué equipo va~~ Hecho el 03-11 (equipo principal de recepción). Queda **la prueba de 30 minutos** con el comedor cerrado; mientras, el promotor prueba por su cuenta en ese equipo y lo que impida abrir entra en 0.1 o 0.2.
2. ~~Aprobar el orden~~ Aprobado el 03-11, sin adelantos.
3. **Llevar al gestor y a Verial las preguntas del apartado 2,** empezando por el 1-1-2027 y el plan B.
4. **Contestar las decisiones 2 a 7** y hacer con el encargado la lista de lo que se usa de Verial.
5. **Pedir ya:**
   - precio a 2 o 3 proveedores de VERI*FACTU (por ejemplo, fiskaly) y un revisor legal;
   - la impresora;
   - el dominio;
   - el acceso B2B de TheFork y la documentación de la API de CoverManager.

---
**Fuentes:**
- **Situación y recapitulación:** `docs/reviews/2026-11-02-recapitulacion.md:23,66,74,94-99,113,119,125,128,196`.
- **Empresas:**
  - AÑITSOC SL: `docs/AI_CONTEXT.md:7`;
  - una sola empresa por instalación: `dotnet/src/Costina.Server/Program.cs:73`;
  - códigos permanentes: `Costina.Server/Backup.cs:170-171`;
  - códigos en el instalador: `deploy/windows/installer.nsi:147-149,203-253`;
  - caducidad de la descarga: `.github/workflows/dotnet-installer.yml:70`;
  - usuarios solo por consola: `Program.cs:125-136`.
- **Caja:**
  - la línea no guarda el IVA: `Costina.Core.Domain/SettlementAccount.cs:12-14`;
  - formas de pago de hoy: `Costina.Server/CheckoutOperations.cs:34`;
  - IVA en el producto: `Costina.Core.Persistence/schema.sql:25,29`;
  - precios por presentación: `schema.sql:43-48`.
- **Red y base de datos:**
  - PostgreSQL solo escucha en el propio PC: `Costina.Server/ServerSetup.cs:66`;
  - wifi del personal y Sala sin importes: `docs/ADR/ADR-014-conexion-sencilla-lan.md:34-35`;
  - sin órdenes desde internet: `docs/DATA_AND_SYNC.md:112`;
  - un servidor por ámbito: `docs/native/D2-realtime.md:41`.
- **Verial:** `docs/plans/2026-09-23-referencia-verial.md:3,15-24`.
- **Plan del 15-09** (`docs/plans/2026-09-15-windows-multidevice-erp.md`):
  - nada abierto a internet: `:59`;
  - empresas, catálogo común y propiedad: `:113-117`;
  - bonos: `:141-147`;
  - cupos: `:151-155`;
  - fiscal: `:179-183`;
  - TheFork: `:191`;
  - exclusiones viejas: `:229`.
- **Bonos en el modelo:** `docs/DOMAIN_MODEL.md:200,211`.
- **Exclusiones que corrige 0.3:** `docs/DECISION_LOG.md:31,34,35`; `docs/BACKLOG.md:69-75`.
- **Normas:** RDL 15/2025; RD 1007/2023; Orden HAC/1177/2024; RD 1619/2012; preguntas frecuentes de la AEAT sobre productores y declaración responsable.