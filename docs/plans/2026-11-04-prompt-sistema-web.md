<!-- Prompt fundacional del proyecto web nuevo (04-11-2026). Se pega entero en una sesión nueva de Claude Code, abierta en una carpeta vacía. Lleva datos internos de Costiña: cuando exista el repositorio privado del proyecto nuevo, se guarda allí. -->

# Prompt fundacional: gestión de restaurante y tienda, web y local

Pega este texto entero en una sesión nueva de Claude Code, abierta en una carpeta vacía.

---

## 1. Quién soy y qué construimos

Soy Santi, el promotor. No soy programador. Escríbeme siempre en castellano y con palabras llanas. Mensajes cortos, de unas 15 líneas, salvo los lotes de decisiones, y con una recomendación clara en cada decisión. Yo contesto en corto («1b, 2a»). No hagas nada nuevo sin mi permiso.

**Qué construimos.** Un programa de gestión para restaurantes y negocios del sector:
- Es multiempresa desde la base.
- Funciona entero en web, servido por un servidor que está en el propio negocio.
- Funciona sin internet.
- Su nombre comercial está pendiente (sección 7). Mientras tanto lo llamamos `<PRODUCTO>`.

**Primer cliente: Costiña**, con dos empresas:
- **Retiro da Costiña:** restaurante y villas.
- **AÑITSOC SL:** almacén, ventas y stock, y la tienda de vinos y gourmet costinagourmet.com.

**Objetivo: dejar el programa de gestión actual (Verial) para todo:**
- comandas, cobro, tickets y facturas;
- stock, compras e informes;
- la carta de vinos en internet (hoy es una web en XAMPP que lee la base de datos de Verial);
- el stock de la tienda web.

**Proyecto anterior, solo como referencia.** Se llama Costi-a (Hospitality OS).
- Repositorio en GitHub: `Rivadesa/Costi-a`, rama `develop`, versión `f7eebcc` o posterior. En el PC de oficina hay un clon en `C:\Users\SANTIAGO.CORZON\Costina`. Si en este equipo no está, clónalo en una carpeta aparte, fuera del proyecto nuevo, y úsalo solo para leer.
- Cómo está hecho: motor .NET 10 con PostgreSQL 17 como servicio de Windows, programa de PC en WPF y dos aplicaciones web para móvil en Vue («Costiña Sala» y «Costiña Cocina»). Lleva más de 55 entregas; versión 0.47.1-f0.2.
- El 03-11 se aprobó allí el plan por fases y se entregó su F0. Lo que trae ya forma parte de las reglas que nos llevamos:
  - la alergia tardía llega a cocina con sonido y una marca que dura hasta «Visto» (H34);
  - la ronda y la mesa a medio abrir se guardan en el móvil, y «Atrás» baja un nivel (H01).
- **Qué nos llevamos:** sus reglas de negocio, que costaron mucho trabajo. **No** se reutiliza su código de servidor. De sus pantallas en Vue se pueden portar piezas, revisándolas antes.
- **Mientras construimos**, Costiña sigue con Costi-a en sala y cocina y con Verial para cobrar. El sistema nuevo los sustituye por fases: sala y cocina en F4, cobro en F6, tienda y carta en F8, y Verial apagado en F10.
- **SOLO LECTURA:**
  - no edites ni crees nada en ese repositorio;
  - no ejecutes allí pruebas ni servidores;
  - no toques la instalación real de este PC (servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089);
  - tu PostgreSQL de desarrollo va en otro puerto: comprueba antes cuál está libre.

**Qué leer y para qué.** Las rutas son relativas al clon. Al lado va la abreviatura que se usa en las citas.

| Abreviatura | Documento | Para qué |
|---|---|---|
| — | `docs/ORDEN_VIGENTE.md` y `docs/handoff/2026-11-03-f0-entregada.md` | El orden aprobado el 03-11, la F0 entregada y lo que espera mis respuestas |
| — | `docs/native/F0.1-alergias-i-aviso-en-cocina.md` y `docs/native/F0.2-alergias-ii-borrador-en-el-movil.md` | Las dos reglas de alergias más recientes |
| `recap` | `docs/reviews/2026-11-02-recapitulacion.md` | Estado del proyecto y por qué creció sin control |
| `p0311` | `docs/plans/2026-11-03-plan-por-fases.md` | Plan para dejar Verial (borrador) y preguntas abiertas |
| `h0311` | `docs/handoff/2026-11-03-plan-por-fases.md` | Mis decisiones del 03-11 |
| `usab` | `docs/reviews/2026-10-28-usabilidad-sala-y-cocina.md` | 76 problemas de uso y las 15 decisiones del 02-11 (líneas 379-397) |
| `DM` | `docs/DOMAIN_MODEL.md`, con `docs/ARCHITECTURE.md` y `docs/DATA_AND_SYNC.md` | Modelo de datos y sincronización |
| `p0915` | `docs/plans/2026-09-15-windows-multidevice-erp.md` | Multiempresa, stock, tienda y fiscal |
| — | `docs/plans/2026-09-23-referencia-verial.md` | Qué hace Verial hoy |
| `p1015`, `p1022` | `docs/plans/2026-10-15-menus-carta-y-bodega.md` y `docs/plans/2026-10-22-sala-y-cocina.md` | Menús, bodega, sala y cocina |
| `ADR-0xx` | `docs/ADR/`, sobre todo 004, 005, 007 (las dos), 009, 012, 013, 014, 015, 016, 017 y 018 | Decisiones técnicas tomadas |
| `AG` | `dotnet/AGENTS.md` | Reglas acumuladas. Tiene 23.000 palabras: busca en él, no lo leas entero |
| — | `docs/native/*.md` | Un documento por entrega. Solo si una regla no queda clara |
| — | `C:\Users\SANTIAGO.CORZON\.claude\projects\C--Users-SANTIAGO-CORZON-Documents-CCA-CARBALLO\memory\costina-*.md` y `santi-responder-en-castellano.md` | Mi memoria. Solo leer. Solo existe en el PC de oficina: si no está, sáltala, porque lo importante ya está en los documentos de arriba |

Cita con `ruta:línea` todo lo que saques de allí.

## 2. Principios no negociables

1. **Ligero.**
   - Se respetan los presupuestos de la sección 3.
   - Pocas dependencias.
   - No se construye nada que no pida la fase en curso.
2. **Estable.**
   - Versiones estables (LTS) fijadas; nada en alfa ni en versión candidata (RC).
   - Las migraciones de la base de datos solo añaden.
   - Antes de actualizar se hace una copia, y hay vuelta atrás ensayada.
3. **Usable.**
   - Lo normal se hace sin preguntar. Se ven nombres, nunca códigos.
   - Hay una sola acción principal, grande y abajo. Dice qué hace y, si está apagada, por qué (usab:45).
   - Botones de al menos 48 px (64 en cocina), separados al menos 8 px.
   - Objetivos (usab:19,455):
     - abrir una mesa en 3 toques, enviar un pase en 2 y servir en 1;
     - abrir una mesa con alergia en menos de 40 s;
     - enviar un pase o servir en menos de 5 s.
4. **Lineal.** Primero se decide, luego se diseña, se aprueba la maqueta, se construye, se prueba en el local y se cierra. Una fase cerrada no se reabre: lo nuevo va a la cola (sección 6).
5. **El local manda** (ADR-001).
   - Sala, cocina y caja funcionan sin internet.
   - La nube nunca es un segundo servidor principal.
   - Nunca mandan dos servidores a la vez.
6. **La seguridad alimentaria manda sobre todo lo demás** (sección 4.6). La aplicación coordina, pero no certifica que un plato sea seguro (p0915:85).
7. **Ninguna orden se pierde ni se aplica dos veces** (sección 3).
8. **Fiscal** (p0311:130-133,167).
   - No se emite un ticket ni una factura de verdad sin el módulo fiscal.
   - Lo emitido no se toca.
   - Las series no tienen huecos.
   - Nunca emiten dos programas en la misma serie.
9. **Sin dinero en sala ni en cocina.** Allí nunca se ven precios, importes, cuenta, saldo ni pagos (`docs/ADR/ADR-007-service-control-vs-checkout.md:17-38`).
10. **Privacidad.** Las alergias son datos de salud (artículo 9 del RGPD):
    - se guardan por comensal y sin nombre;
    - solo las ven la sala y la cocina de esa mesa.
11. **Pantallas táctiles y legibles.**
    - Tema claro y letra grande.
    - La sustancia de una alergia, en MAYÚSCULAS y legible a 2 m.
    - El rojo es solo para alergias y acciones irreversibles. Los tiempos van en ámbar (ADR-017:28).
12. **Idioma.**
    - Pantallas en castellano con tildes y con un único vocabulario, el de `GLOSARIO.md`.
    - El código y sus nombres internos, en inglés. Los documentos, en castellano.
13. **Nada se borra.** Se desactiva o se anula, y queda el registro (DM:187-190).

## 3. Decisiones técnicas ya tomadas

Comprueba el primer día que estas versiones siguen siendo las estables. No uses versiones alfa ni candidatas (RC).

- **Stack: TypeScript en todo** (motivo: un solo lenguaje y un solo contrato entre servidor y pantallas; las pantallas en Vue del proyecto anterior ya usan Vue 3.5 y Vite 8, según `frontend/pwa/package.json:16,23`; sobra para unos 20 aparatos):
  - Node 26 LTS (si alguna pieza aún no la admite, Node 24 LTS);
  - Fastify 5.12, con esquemas TypeBox compartidos con las pantallas;
  - PostgreSQL 18.6, con Kysely 0.29 y migraciones en SQL;
  - Vue 3.5 y Vite 8;
  - Vitest y Playwright para las pruebas.
- **Dependencias:**
  - lista cerrada, de unas 15 en el servidor, con versiones exactas;
  - se instalan con `npm ci` y se actualizan una vez al trimestre;
  - añadir una nueva exige una ADR (una página que explica la decisión);
  - sin librería de componentes ni framework de CSS: CSS propio con variables.
- **Forma: un solo programa con módulos** (monolito modular).
  - Un proceso, una base de datos y una dirección HTTPS.
  - Un núcleo y módulos que se activan por instalación: comedor, caja, fiscal, stock, compras, reservas, villas, tienda y carta.
  - Un módulo apagado no registra rutas ni pantallas (ADR-012:69-70).
  - La licencia es un fichero firmado (Ed25519) que funciona sin conexión y dice qué módulos puede encender esa instalación.
- **Base de datos: PostgreSQL** (motivo: réplica en caliente, recuperación a cualquier minuto y permisos para que lo fiscal solo se pueda añadir).
  - El dinero va en céntimos (`bigint`).
  - Los identificadores son UUIDv7.
  - Las fotos se guardan en la base, para que vayan en las copias (AG:79):
    - JPEG, PNG o WebP, reconocidos por su contenido;
    - 2 MB como máximo y nunca SVG;
    - el servidor no descarga nada de internet.
- **Multiempresa:**
  - Jerarquía: grupo → empresa (NIF) → establecimiento → puesto. El almacén físico es una cosa y el dueño de las existencias, otra.
  - `company_id` en toda tabla del negocio, sacado de la sesión y nunca de lo que mande el aparato (`docs/ADR/ADR-007-local-auth-and-optimistic-concurrency.md:16-18`).
  - Seguridad por filas de PostgreSQL en dinero, stock y fiscal, como red de seguridad.
- **Una sola aplicación web con cuatro espacios instalables** (`/sala`, `/cocina`, `/caja` y `/gestion`) y la parte pública `/publico`.
  - Cada espacio tiene su manifiesto, su icono, su color, su service worker y su almacén en el aparato (IndexedDB).
  - Se carga por partes: Sala nunca descarga Gestión.
  - Mismo origen con alcances separados, ya probado (ADR-015).
  - Gestión se piensa para el PC, con barra de menús clásica.
- **Marca blanca:**
  - Cada instalación tiene su nombre, su logo y 3 o 4 colores como variables CSS.
  - El servidor genera los manifiestos y los iconos. No hay una compilación por cliente.
  - Costiña: Sala en verde `#0E7A5F` y Cocina en azul pizarra `#2F4A6D` (ADR-015:19-28).
- **Órdenes que no se pierden ni se repiten:**
  - Cada cambio es un `POST` con su clave única (`Idempotency-Key`): un UUIDv7 que el aparato genera **al tocar el botón**, no al enviar.
  - La clave es única en toda la instalación y se guarda con la huella SHA-256 del contenido, el autor y el resultado:
    - misma clave y mismo contenido: devuelve la misma respuesta;
    - misma clave y otro contenido: se rechaza con un 422.
  - Motivo: en el proyecto anterior la idempotencia era por actor y, al volver a emparejar un aparato, una orden pendiente pudo cargarse dos veces (`docs/native/D6.7-orden-pendiente-segura.md:30-32`).
  - El cambio, la auditoría, el resultado y el aviso se guardan en una sola transacción (ADR-004:11-13).
- **Cambios a la vez (concurrencia):**
  - Toda orden lleva la versión que leyó del objeto que cambia (la mesa, la cuenta…) y la sube.
  - El servidor aplica cada orden en una transacción, con la fila bloqueada. Si la versión no coincide, devuelve un 409 recuperable: el aparato relee y pregunta.
  - Es el modelo probado del proyecto anterior (`docs/ADR/ADR-007-local-auth-and-optimistic-concurrency.md`). Si en las pruebas de F4 hay demasiados choques, se relaja solo para las órdenes que solo añaden (una línea, una nota), con la decisión apuntada. Nunca para las alergias.
- **El servidor dice qué se puede hacer** en cada lectura, y las pantallas solo pintan. Nunca recalculan reglas ni enseñan un botón que daría 403 (AG:13).
- **Cola de órdenes en el aparato (IndexedDB):**
  - Es corta y en orden. Cada orden se guarda antes del primer intento y se envían de una en una.
  - Un Web Lock hace que solo envíe una pestaña. Un 409 detiene la cola.
  - En pantalla se ve «Pendiente (n)».
  - El borrador (una ronda o una mesa a medio abrir) va aparte de la cola; al volver se pregunta qué hacer con él (decisión 8 del 02-11).
  - Una orden de otro aparato o de otra persona nunca se reenvía (AG:93).
- **Tiempo real con SSE:**
  - Una conexión `GET /api/events` por espacio.
  - El aviso solo lleva `{tipo, id, versión}`: ni datos ni dinero.
  - Un latido cada 15 s.
  - Después de cada aviso, de cada reconexión y al volver a primer plano (`visibilitychange`), el aparato relee del servidor.
  - Los avisos de dinero solo llegan a Caja y a Gestión.
- **HTTPS real en la red del local desde el primer día:**
  - Dominio del producto y un subdominio opaco por instalación (por ejemplo, `k7q2.local.<dominio>`).
  - Certificado de Let's Encrypt por reto DNS, delegado a acme-dns con una credencial por instalación.
  - Caddy lo renueva solo y avisa cuando queden 20 días.
  - Al instalar, comprobar que el router no bloquea respuestas DNS con IP privada.
  - Nunca se instalan certificados en los aparatos (ADR-014:3).
- **Servidor** (**CONFIRMAR 25**):
  - un mini-PC dedicado con Linux (Debian) y Docker Compose (`caddy`, `app`, `postgres` y `pgbackrest`);
  - imágenes firmadas y fijadas por su huella;
  - SAI con apagado limpio, todo por cable y 4G de respaldo en el router.
- **Segundo servidor:**
  - otro mini-PC igual, con una réplica en caliente;
  - se pasa a él a mano, con el botón «Hacer principal este equipo», que primero comprueba que el otro no responde;
  - sin cambio automático, porque hay riesgo de que los dos manden;
  - el antiguo solo vuelve como réplica.
- **Copias:**
  - pgBackRest a dos destinos: la réplica del local y un almacenamiento en la UE, cifrado antes de salir y con una clave por cliente. Si pgBackRest deja de mantenerse, WAL-G.
  - Copia continua del diario de cambios (WAL) cada 60 s como mucho; completa cada semana y diferencial cada día.
  - Además, una copia diaria (`pg_dump`) con manifiesto y huella SHA-256 (`docs/native/D5.4-backup-restore.md:8-14`).
  - Cada semana se restaura sola en una máquina desechable y comprueba recuentos, huellas, la cadena fiscal y el tablero.
  - Objetivo: perder como mucho 1 minuto de datos; volver a trabajar en 15 minutos en el local o en 4 horas con equipo nuevo (**CONFIRMAR 26**).
- **Último recurso en internet:**
  - una instancia en la UE, apagada, que se alimenta de las copias;
  - solo la enciende el proveedor, con una lista: los dos servidores del local apagados, convertirla en principal y abrir la dirección de emergencia (en una tarjeta impresa);
  - para volver: reconstruir el local desde la nube y apagar la nube;
  - motivo: dos servidores emitiendo a la vez partirían la cadena fiscal.
- **Instalar y actualizar:**
  - `<producto> instalar`:
    - genera los códigos permanentes de grupo, empresa y local;
    - pide el certificado;
    - crea el primer responsable.
  - `<producto> actualizar`:
    - comprueba la firma;
    - no sigue con mesas o una caja abiertas, salvo dentro de la ventana de mantenimiento;
    - hace una copia;
    - aplica migraciones que solo añaden;
    - comprueba que todo va;
    - si algo falla, vuelve sola a la versión anterior.
  - `<producto> volver`: regresa a la versión anterior sin restaurar. Lo que borra columnas o tablas llega en la versión siguiente.
  - En los aparatos, la versión nueva solo se aplica si no hay órdenes pendientes. El servidor acepta el formato anterior durante una versión.
  - Dos canales: «pruebas» y «estable».
- **Parte pública, aparte:**
  - un servicio pequeño en la UE, con el mismo stack y su propia base, para las reservas web y la carta pública;
  - la carta enseña el precio de la tarifa General y «quedan pocas» o «agotado», nunca el stock real;
  - el local solo hace conexiones hacia fuera y la base nunca se abre a la red;
  - mientras siga la carta del XAMPP, puede leer `GET /publico/carta.json`, de solo lectura, en la red del local.
- **Tienda web** (WooCommerce o PrestaShop, por su API, detrás de una interfaz `ShopConnector`: WooCommerce primero y PrestaShop después):
  - el local escribe precio (tarifa General) y stock;
  - recoge los pedidos cada 1 o 2 minutos y compara con la tienda cada cierto tiempo;
  - un pedido creado no es un pedido pagado, y un aviso repetido no duplica nada;
  - cupos por canal para no vender dos veces la última botella (p0915:139,151-155).
- **Impresión:**
  - Impresoras de red ESC/POS: el servidor les envía directamente por el puerto 9100.
  - Cola de impresión en la base:
    - estados: pendiente, enviado, hecho o fallo;
    - reintentos, reimpresión y estado «sin papel»;
    - una tabla de caracteres con € y ñ.
  - Impresora USB: la misma aplicación en modo agente, en el PC que la tiene. Nunca WebUSB ni el diálogo de imprimir.
  - Cajón conectado a la impresora: se abre solo con cobros en efectivo, y la apertura queda registrada.
  - Comandas impresas, opcionales por estación.
  - Lector de códigos en modo teclado.
- **Fiscal mediante una pieza intercambiable** (**CONFIRMAR 22**):
  - interfaz `FiscalGateway` (`registerIssue`, `registerCancellation`, `status` y `qr`), con dos implementaciones: proveedor certificado o VERI*FACTU propio;
  - requisito: emitir sin internet, con registro y QR en el local y cola de envío a Hacienda (AEAT);
  - series por empresa y año, sin huecos, con la fila de la serie bloqueada dentro de la transacción;
  - tablas de documentos y registros en las que solo se puede añadir (permisos de la base y un disparador);
  - las correcciones, solo con rectificativa;
  - en la fase fiscal hay que comprobar si la cadena va por NIF o por serie (Orden HAC/1177/2024).
- **Operación:**
  - Soporte remoto por Tailscale: sin abrir puertos, solo hacia el SSH y el panel, y el cliente puede cortarlo.
  - Latido al proveedor cada minuto.
  - Se vigila: disco, salud del disco (SMART), SAI, antigüedad de la última copia, retraso de la réplica, días que le quedan al certificado, impresoras y envíos fiscales.
  - Pantalla «Estado» en Gestión: verde, ámbar o rojo, en palabras llanas.
- **Presupuestos de «ligero»** (la integración continua falla si se pasan):

| Medida | Límite |
|---|---|
| Código inicial comprimido | Sala 150 KB · Cocina 120 KB · Caja 180 KB · Gestión 300 KB |
| CSS | 30 KB, con las letras del sistema |
| Primera carga por la wifi del local | 1,5 s |
| Respuesta a una orden (95 de cada 100) | 150 ms en el servidor |
| Aviso hasta que la otra pantalla relee (95 de cada 100) | 1 s |
| Memoria del servidor completo | 3 GB |
| Tras un corte de luz, hasta volver a servir | 90 s |
| Ticket impreso desde «Cobrar» | 2 s |

## 4. Modelo de dominio y módulos

**4.1 Organización**
- Grupo (el cliente) → empresas con NIF. Cada empresa tiene sus series, cajas, tarifas, documentos y existencias.
- Establecimientos (salas, mesas, estaciones de cocina, puestos) y almacenes. El catálogo es común al grupo.
- **Usuarios:**
  - se crean en pantalla (en el proyecto anterior solo por consola: recap:198);
  - cada persona tiene su PIN, porque un aparato no es una persona (ADR-018:34);
  - las comandas se toman sin contraseña;
  - cobrar, cerrar, configurar y conectar aparatos piden la contraseña del responsable en ese momento (ADR-013:9-17).
- Los códigos de Verial o de WooCommerce nunca son la clave interna (DM:284).

**4.2 Catálogo y precios**
- Un producto tiene presentaciones: botella, copa, ración…
- El precio va por tarifa, presentación y fecha desde la que vale, con IVA incluido y en céntimos (`docs/native/E3-catalog.md:7,9`).
  - Cada sala puede tener su tarifa; si no hay precio ahí, vale el de la General.
  - Sin precio en la General, no se vende.
- El IVA depende del producto y del canal de venta (sala, mostrador o para llevar, tienda web) y se congela en cada línea. Las reglas exactas se cierran con el gestor (**CONFIRMAR 23**).
- Los 14 alérgenos de declaración obligatoria (UE), por producto y por plato (**CONFIRMAR 14**).
- Atributos del vino: añada, elaborador, crianza, uvas, formato y graduación, con una referencia por añada y formato (**CONFIRMAR 33**).
- La copa sale de una botella abierta. Su precio se pone a mano por vino, y hay una copa por vino (p1015:906).
- Los combinados tienen precio fijo: la base y el refresco se eligen sin suplemento (p1015:907).
- Importación (AG:63,77,91):
  - repetir el mismo fichero no hace nada dos veces, y nunca borra ni desactiva;
  - la categoría se reconoce por su nombre y por su padre;
  - un producto que ya existe conserva su familia, su IVA y su estación.

**4.3 Oferta**
- Tipos: Carta, Menú degustación, Menú con elección y Menú del día, que se propone al abrir mesa (p1015:912).
- Un menú es un producto: precio por persona multiplicado por las personas, congelado al abrir (AG:65).
- Menú cerrado: todos comen lo mismo en cada pase, salvo alergias. La elección por comensal va en «Más opciones» (p1015:908).
- El maridaje es un plato del pase, sin cocina y sin cargo aparte (p1015:919).
- El menú del día se hace en una sola pantalla, con «Así lo verá el camarero» y «Copiar del último día». Hoy cuesta unas 55 acciones (usab:271-274).
- Un pase de «Extras» para los platos que no están en el menú.

**4.4 Sala**
- **Tres ciclos independientes** (ADR-009:11-17):
  - **Servicio de la mesa:** Sentados → En servicio → En pausa → Terminada o Cancelada.
  - **Ocupación:** Ocupada o Libre. Liberar es una acción aparte, con quién, cuándo y motivo.
  - **Cuenta:** Abierta o Cerrada. Si está pagada se calcula aparte.
  - Terminar no cobra, no libera y no factura. Pagar no cambia el ritmo ni la ocupación.
- **«Mis mesas»** primero y después «Todas». Si no se sabe quién abrió una mesa, cuenta como mía (ADR-018:27-35).
- **Abrir mesa** con personas, menú y alergias por comensal, en una sola orden.
- **Numeración de comensales** configurable. La de partida: el 1 es el de espaldas a la puerta y se cuenta en el sentido de las agujas del reloj.
- **Ronda de bebidas:**
  - Copa o Botella con un toque, y «+1»;
  - se puede editar antes de enviar y viaja en una sola orden;
  - no se pierde al liberar la mesa (usab:487-490).
- **«Lo que lleva la mesa»**, sin importes. El botón **«Pide la cuenta»** avisa a Caja sin importes.
- **Avisos:**
  - una píldora fija abajo, «Mesa 5 · Aperitivos listo ↑», que no mueve nada;
  - un tono opcional, apagado de partida.
- **Correcciones desde el móvil:**
  - cambiar de mesa llevándose las alergias;
  - cambiar el número de personas;
  - deshacer «Servido» u «Omitir».
- **Terminar y liberar:** solo el responsable.
- **Contra el doble toque** (usab:194-202): el resultado se ve 1 o 2 s en el botón («Mesa abierta · Mesa 5 ✓») y se ignoran los toques durante 0,6 s después de un aviso.
- **Decisiones antiguas que no vuelven:**
  - «La ronda solo en memoria» (la sustituye el borrador);
  - «En sala no suena nada»;
  - «“Listo” no se deshace».

**4.5 Cocina**
- **Pase:** Pendiente → Enviado → En preparación → Listo → Servido, o bien Omitido o Cancelado.
  - El servidor valida cada paso.
  - «Comiendo» se deduce y no lleva botón (DM:88-104).
- **Plato:** Pendiente → Empezado → Listo. El pase no está listo hasta que lo estén todos sus platos obligatorios (DM:124-138).
- **Ritmo:**
  - no se envía un pase mientras otro siga enviado, en preparación o listo;
  - la pausa llega a cocina con sonido y con el reloj parado.
- **Estación** (ADR-017):
  - ve solo lo suyo: una comanda por mesa y pase, con los platos agrupados («3 + 1 adaptada»);
  - solo tiene el botón «Listo», que se puede deshacer durante 8 a 10 s;
  - lo terminado baja a «Hecho · esperando al pase»;
  - nunca ve lo que no se ha enviado.
- **Tiempos:** «Lleva N min», en ámbar a los 15 minutos y en ámbar fuerte a los 25. Nunca en rojo.
- **Sonido y marcas:**
  - suena después de tocar «Empezar turno»;
  - «NUEVA» se marca durante 60 s;
  - «ALERGIA NUEVA» o «CAMBIO» se quedan hasta que alguien toca «Visto»;
  - una barra «↓ 3 comandas más abajo» cuando no caben en pantalla.
- **El pase** ve la mesa entera:
  - primero lo que pide acción, con «espera desde hace N min»;
  - puede marcar «Listo» por una estación sin tablet.
- **Flujo** (p1022:885): el camarero envía → cada estación toca «Listo» → el pase valida → aviso a sala → «Servido».
- **Opcional:**
  - una nota de cocina aparte («al punto»), sin rojo y sin bloquear (**CONFIRMAR 15**);
  - un destino «barra», apagado en Costiña;
  - una impresora de respaldo por estación.

**4.6 Alergias (mandan sobre todo lo demás; ADR-016)**
- **Siempre con estructura:**
  - de un comensal o de «Toda la mesa»;
  - con tipo (alergia, intolerancia o preferencia), sustancia y gravedad;
  - nunca una nota libre ni solo un color (AG:15).
- **El servidor cruza la alergia con los alérgenos del plato.** Pasan a «Por revisar en cocina»:
  - los platos que chocan;
  - los que no tienen alérgenos declarados;
  - todos, si no reconoce la sustancia;
  - todos, si la restricción es de toda la mesa o el plato es para compartir;
  - también las bebidas.
- **Al anotar o retirar una restricción**, pasa a revisión todo lo que no está servido ni omitido. Nada se limpia solo.
- **Cada decisión lleva nota obligatoria:** «No le afecta», «Adaptado: <cómo>» o «Rehacer».
  - «Rehacer» solo vale para lo ya enviado y devuelve el plato.
  - Con el plato ya hecho no se ofrece «Adaptado».
- **El bloqueo es por pase, no global.** Se revisa antes de enviar, y el menú entero de un comensal se revisa en una sola orden.
- **Dos pasos para lo arriesgado**, nombrando la sustancia:
  - «No le afecta a ninguno de estos N platos»;
  - «Rehacer»;
  - atajos como «No lleva marisco».
- **La estación puede marcar «Listo» con una revisión pendiente**, pero la línea dice «no lo emplates hasta que decida el pase».
- **Al abrir una mesa con alergia:** «¿Quién?» empieza sin elegir, Alergia y Grave vienen marcadas, y nada se copia de la anterior. Copiarla fue el fallo H51 (usab:112-115).
- **Nunca se ocultan:** dicen quién, el tipo, la sustancia y la gravedad, con «ALERGIA» en mayúsculas, también en «Hecho».
- **Aviso al camarero:** «revisada por cocina: ya puedes enviar».
- **Criterio de corte:** una alergia perdida, puesta a otro comensal o con la gravedad equivocada se arregla antes de cualquier servicio real (usab:462).
- **Texto de la alergia:** se borra a los N días y se conserva la decisión de cocina sin datos personales (**CONFIRMAR 29**).

**4.7 Caja**
- Cada cargo congela el producto, el nombre, la tarifa, el precio y el IVA (AG:63). El proyecto anterior no guardaba el IVA en la línea (recap:116). Nunca hay precio libre en una venta normal.
- **Formas de pago:**
  - efectivo, con cálculo del cambio y cajón;
  - datáfono no integrado: se marca, se teclea el importe y se acepta a mano;
  - otras.
- **Pagada no es lo mismo que cerrada:**
  - reabrir una cuenta pide motivo y solo lo hace el responsable;
  - devolver solo gasta el saldo a favor que ya existe;
  - anular conserva el registro.
- **Dividir la cuenta:** cada parte lleva su ticket.
- **En pantalla** (usab:256-269):
  - nada viene preelegido;
  - el motivo va dentro de cada acción y queda vacío después;
  - «Cobrar y cerrar» va seguido de «Terminar y liberar Mesa X».
- Apertura y cierre de caja, con arqueo.

**4.8 Facturación**
- Servicio, cuenta, pago, documento fiscal y registro fiscal son cinco cosas distintas (ADR-005:11-19).
- Tickets (factura simplificada) y facturas completas, a empresa o a particular.
- Una cuenta facturada no se reabre: se hace una rectificativa.
- El día D se cierran las series de Verial.

**4.9 Stock y compras**
- **El stock se lleva por movimientos** (p0915:101,105):
  - cada uno con dueño, causa, usuario y fecha;
  - el saldo sale de ellos;
  - sin negativos, salvo un ajuste con permiso y motivo.
- **Mover una botella de sitio no cambia su dueño.** Usar en Retiro una botella de AÑITSOC necesita una operación documentada (p0915:115-117).
- **Cuándo se descuenta** (p0915:103):
  - un pedido reserva;
  - la entrega descuenta una sola vez;
  - cobrar o facturar no vuelve a descontar;
  - anular un cargo no devuelve una botella ya bebida.
- Proveedores, pedidos de compra, recepción e inventario con recuento.

**4.10 Reservas:** propias del restaurante y las villas, desde la web.
- Las reservas web entran como solicitudes con cupo.
- Consentimiento aparte para las alergias.
- TheFork y CoverManager conviven «una temporada» (**CONFIRMAR 12**).

**4.11 Informes:** ventas por día, empresa, forma de pago e IVA; caja; stock valorado; y exportación CSV para el gestor. La lista se cierra con la lista de usos de Verial.

**4.12 Datos reales**
- **Vinos:** 1.037 referencias de costinagourmet.com, bajo la categoría raíz «Bodega» (id 881): Blancos 337, Tintos 452, Espumosos 86, Dulces 158, Rosados 2 y Vermut 2. Conversor: `tools/catalog/woocommerce-to-csv.py`.
- **Trampas conocidas** (memoria `costina-carta-vinos-woocommerce.md:15-23`; recap:135):
  - la misma región cuelga de varios tipos (Rioja en Tintos y en Blancos);
  - hay un código duplicado, «3755-REPETIDO»;
  - la añada va dentro del nombre;
  - entraron con IVA al 21 %;
  - 4 fotos hay que volver a subirlas en JPEG: 4509, 4510, 4512 y 543.
- **El local:** unas 8 mesas, una sala y una estación de sala; en cocina, caliente, fría y pase (`docs/AI_CONTEXT.md:13`; p1022:64).

## 5. Fases (en orden; ninguna empieza sin cerrar la anterior)

**«Terminado» siempre exige:**
- pruebas automáticas en verde;
- prueba de 30 minutos en el local con un Android, un iPhone, el iPad o la tablet y el PC, por la wifi del local, con la hoja `pruebas/fase-N.md` rellenada y nada que bloquee;
- la versión instalada en el local, con su copia;
- `STATUS.md` y `DECISIONES.md` al día.

Si una ficha pasa de 10 entregas, la fase se parte **antes** de empezarla.

- **F0. Decidir y diseñar** (unas 2 semanas, sin código del producto)
  - Entrega:
    - tanda de decisiones contestada, en `DECISIONES.md`;
    - `GLOSARIO.md`;
    - mapa de pantallas por perfil: camarero, estación, pase, encargado, caja, almacén, administración y web pública;
    - sistema de diseño en una página navegable: colores, letra y espaciado, unos 10 componentes y 6 patrones (lista con ficha, alta corta con «Más opciones», hoja de acción en el móvil, tablero de comandas, pantalla de cobro y tabla de gestión);
    - prototipo navegable del camino crítico, con datos reales: abrir mesa con alergia → enviar → estación → pase → servir → cobrar;
    - lista de usos de Verial, cada uno con su fase o con «se deja de hacer»;
    - `PLAN.md` con las fichas de F1 a F10.
  - Terminado: Santi y un camarero o un cocinero lo prueban en los aparatos reales; hay una vuelta de cambios y un «sí».
  - No entra: código del servidor.
- **F1. Esqueleto instalado**
  - Entrega:
    - repositorio e integración continua;
    - los 4 espacios vacíos, instalables y con marca blanca;
    - HTTPS real;
    - `instalar`, `actualizar` y `volver`;
    - copias a dos destinos, con la restauración semanal;
    - réplica al segundo servidor;
    - pantalla «Estado»;
    - ticket de prueba impreso.
  - Terminado:
    - instalado en el mini-PC;
    - los 4 espacios instalados en los aparatos;
    - actualizar y volver, ensayados;
    - restauración en otra máquina, en verde;
    - paso a la reserva en menos de 15 minutos.
  - No entra: funciones del negocio.
- **F2. Identidad y multiempresa**
  - Entrega:
    - grupo, empresas, establecimientos, almacenes y puestos;
    - seguridad por filas;
    - usuarios con PIN y papeles, gestionados en pantalla;
    - conectar aparatos por QR;
    - órdenes con clave única, cola en el aparato y SSE.
  - Terminado: dos empresas reales configuradas, y una prueba de corte de red sin perder ni duplicar órdenes.
  - No entra: catálogo.
- **F3. Catálogo y oferta**
  - Entrega: secciones 4.2 y 4.3, importación de los vinos y del fichero de Verial, y la carta de solo lectura `/publico/carta.json`.
  - Terminado: catálogo real cargado y revisado por Santi; menú del día hecho en 5 minutos como mucho.
  - No entra: stock.
- **F4. Sala y cocina**
  - Entrega: secciones 4.4 a 4.6.
  - Terminado:
    - servicio simulado de 30 minutos con 8 mesas, 3 de ellas con alergias;
    - toques y tiempos medidos;
    - 12 comandas a la vez en una estación;
    - prueba automática de que no aparece dinero en sala ni en cocina.
  - No entra: cobro. Verial sigue cobrando.
- **F5. Caja e impresión, en ensayo**
  - Entrega: sección 4.7, con tickets marcados «ENSAYO».
  - Terminado: un día de cuentas reales, tecleadas otra vez en la instalación de ensayo, cuadra al céntimo con Verial.
  - No entra: emitir de verdad. En el local real todavía no se cobra.
- **F6. Fiscal y facturación (primer ticket de verdad)**
  - Entrega:
    - la vía fiscal elegida;
    - series, ticket, factura completa y rectificativa;
    - cola de envío a Hacienda y QR;
    - declaración responsable;
    - clientes traídos de Verial.
  - Terminado: prueba en el entorno de pruebas de Hacienda o del proveedor; la restauración comprueba la cadena; cierre de las series de Verial pactado con el gestor.
  - No entra: stock.
- **F7. Stock y compras**
  - Entrega: sección 4.9 e inventario inicial, con recuento físico la víspera.
  - Terminado: el stock de una semana cuadra con el recuento.
  - No entra: tienda web.
- **F8. Tienda web y carta de vinos**
  - Entrega: precio y stock hacia la tienda, pedidos desde la tienda, cupos por canal y carta pública.
  - Terminado: una semana sin descuadres entre la tienda y el programa; se apagan el conector de Verial y la web del XAMPP.
- **F9. Reservas del restaurante y de las villas** (parte pública)
  - Terminado: reservas web reales que llegan al plano de sala, con cupos y sin internet en el local.
- **F10. Informes y apagado de Verial**
  - Entrega: informes y exportación para el gestor.
  - Terminado: Verial queda solo para consultar el histórico.

## 6. Cómo trabajamos

**Por qué así.** En Costi-a:
- el 92 % de las líneas nuevas fue trabajo metido por medio del orden aprobado (recap:40-50);
- las entregas crecieron de unas 486 líneas a unas 2.825;
- se escribieron 142 documentos, con 283.195 palabras;
- hubo 1.282 pruebas automáticas y ninguna en aparatos reales;
- las reservas se aplazaron 3 veces;
- la caja nunca cobró de verdad (recap:15-55).

La raíz común: se construía antes de decidir, diseñar y probar.

**Cada fase sigue siempre estos 6 pasos:**
1. **Ficha de una página:** para qué sirve, qué entra y qué no, cuándo está terminada y con qué aparatos se prueba.
2. **Maqueta navegable** con datos reales, abierta en mis aparatos.
   - Hay una sola vuelta de cambios.
   - Mi «sí» vale para toda la fase: programar, subir, mezclar e instalar en la instalación de ensayo.
   - Instalar en el servidor real del local se pide al cerrar.
3. **Entregas pequeñas:**
   - 500 a 800 líneas de código como mucho, sin contar pruebas;
   - una sola idea y más o menos un día de trabajo;
   - si va a ser más grande, se parte antes de empezar.
4. **Una revisión por entrega**, con una lista fija: alergias, dinero y fiscal, patrones del diseño, que las pruebas cubran lo nuevo, y el tamaño. Lo que no bloquea va a la cola.
5. **Prueba de cierre en el local:** 30 minutos con guion. Dentro de la fase solo se arregla lo que bloquea: una alergia, el dinero, datos perdidos o que no se pueda trabajar.
6. **Cierre:**
   - una versión instalada en el local;
   - `STATUS.md` reescrito;
   - las decisiones apuntadas.

**Pruebas:**
- **Unitarias (Vitest)** de todas las reglas del negocio.
- **De la API contra un PostgreSQL real** y desechable; nunca contra el de Costina.
- **Del navegador (Playwright, con Chromium y WebKit)**, del camino crítico, en tres tamaños: móvil (390×844), tablet (820×1180) y PC (1366×768).
- **Sin dinero:** una prueba que falla si en sala o en cocina aparece «precio», «cuenta», «pago», «saldo» o «€».
- **Carga:** 25 aparatos simulados.
- **Aparatos reales** en cada cierre de fase.

**Documentos (lista cerrada, menos de 15.000 palabras en total):**

| Documento | Contenido | Tope |
|---|---|---|
| `CLAUDE.md` | Tecnología, convenciones, comandos, lo prohibido | 200 líneas |
| `STATUS.md` | Fase, entrega en curso, siguiente paso, bloqueos y versión instalada. Se reescribe, no se añade | 1 página |
| `DECISIONES.md` | Número, fecha, qué se decidió y quién | 1 línea por decisión |
| `GLOSARIO.md` | Las palabras del negocio | 1 página |
| `COLA.md` | Peticiones pendientes | 1 línea por petición |
| `PLAN.md` | Orden de las fases y ficha de cada una | 1 página por fase |
| `diseno/` | Sistema de diseño y maquetas aprobadas | — |
| `pruebas/fase-N.md` | Guion de 30 minutos y hoja rellenada | 1 página |
| `CHANGELOG.md` | Qué trae cada versión | 3 a 5 líneas por versión |
| ADR | Solo decisiones técnicas sin vuelta atrás | 1 página cada una; unas 10 en total |

No hay documentos por entrega, ni traspasos, ni planes largos.

**La regla de la cola.**
- Toda petición nueva, o todo cambio a algo ya aprobado, se apunta en `COLA.md` en una línea: fecha, qué es, quién lo pide y tamaño.
- No entra en la fase en curso, salvo riesgo de alergia, riesgo legal o de datos, o que impida trabajar.
- La cola se repasa solo al cerrar cada fase. Un cambio de aspecto después de aprobar la maqueta es una petición más.

**Git, integración continua y entorno:**
- **Repositorio privado.** `main` protegida, nunca forzada.
- **Una rama por entrega** (`fN/eM-descripcion`), con su PR y squash.
- **Commits convencionales en castellano.**
- **La integración continua comprueba:**
  - estilo y tipos;
  - pruebas;
  - tamaño de lo que se descarga;
  - que las migraciones solo añaden;
  - las dependencias.
- **Secretos:** nunca en el repositorio.
- **Ficheros en UTF-8 sin BOM.** En Windows, nada de `Set-Content`.
- **Desarrollo en Windows y sin administrador:** Node y PostgreSQL portables, con la base de pruebas en un puerto propio. Docker no es obligatorio en el PC de desarrollo; sí se usa en la CI y en el servidor del local.

**Reparto con agentes:**
- La **sesión principal** lleva `STATUS.md`, la cola y las decisiones, y prepara mis lotes.
- **Un agente por entrega**, en su propia rama o worktree.
- **Un revisor**, con una sola ronda.
- **Un agente de pruebas.**
- **Prohibido a los agentes:** crear documentos fuera de la lista, ampliar el alcance y tomar decisiones del negocio. Una duda se apunta en el lote, no se decide.

**Cómo hablas conmigo:**
- **Dos momentos por fase:** al empezar (ficha y maqueta) y al cerrar (resultado de la prueba, lote de decisiones y cola). Entre medias, solo bloqueos.
- **Lotes de decisiones:**
  - numerados, con opciones a, b y c;
  - la recomendación en **negrita** y su consecuencia en una línea.
- **Decisiones pequeñas** dentro de una fase aprobada: se toman con tu recomendación y se apuntan. Nada que amplíe el alcance sin mi «sí».

## 7. Primera tarea

1. **Lee** lo de la sección 1, por este orden:
   - `recap` y `h0311`;
   - `p0311`;
   - `usab`, desde la línea 379;
   - las ADR 016, 017, 018, 009, 005, 004 y 007;
   - `DM` y `p0915`;
   - `referencia-verial`;
   - mi memoria.
2. **Devuélveme en un solo mensaje** (aquí puede pasar de 15 líneas):
   - **(a)** lo que has entendido, en 10 líneas;
   - **(b)** las decisiones de la lista de abajo, numeradas, con opciones y tu recomendación. Puedes corregirlas o añadir alguna que falte; 35 como mucho;
   - **(c)** el plan de F0: entregas, orden, qué necesitas de mí y cuándo.
3. **No crees ficheros, no escribas código, no instales nada y no crees repositorios** hasta mi respuesta. Lo primero después de mi «sí» será `CLAUDE.md` (sacado de este texto), `DECISIONES.md` y `STATUS.md`.

**Decisiones por CONFIRMAR** (mi recomendación provisional en negrita; no inventes datos de Costiña):

*Negocio y empresas*

1. **Nombre y dominio.** Nombre comercial del producto y dominio propio: **pedir ya el dominio** (p0311:48,51).
2. **Titular.** Quién figura como productor y titular del programa: **repositorio nuevo privado** (p0311:40,70-75).
3. **AÑITSOC y el servidor.** ¿Trabaja en el mismo edificio y con el mismo servidor? Si es otro edificio, cambia el diseño (p0311:34).
4. **Usuarios.** ¿Una persona puede entrar en las dos empresas? **Sí: un usuario con papeles por empresa** (p0311:35).
5. **Propiedad.**
   - ¿De qué empresa son los 1.037 vinos?
   - ¿Quién cobra una botella para llevar vendida en Retiro?
   - **Clientes y proveedores comunes al grupo** (p0311:36).
6. **Reposición de AÑITSOC a Retiro:** **una venta semanal con una sola factura** (p0311:46).
7. **Villas:**
   - dónde se apuntan hoy, quién las factura y de qué empresa son;
   - Booking o Airbnb, temporadas y señal (p0311:37,49).
8. **Bonos y experiencias de retirodacostina.com:** qué web los vende, si está unida a Verial y cuántos hay sin canjear (p0311:38).
9. **Invierno.** ¿Cierra Retiro? Cambia cuándo se puede probar (p0311:52).
10. **Día D: por partes** **(comandas en F4, cobro en F6, stock y tienda en F8)** o de una vez. Si es por partes, ¿quién vuelve a teclear la cuenta en Verial mientras tanto?
11. **Orden de las fases.** Reservas **después** de stock y tienda (primero lo que saca de Verial) o antes.
11b. **Costi-a mientras tanto:** ¿se para su F1 (multiempresa) para no hacerla dos veces, o sigue mientras el sistema nuevo arranca? Recomendación: **parar la F1 de Costi-a**, mantener allí solo los arreglos que impidan abrir y llevar la multiempresa directamente al sistema nuevo.
12. **TheFork y CoverManager:** cuál manda hoy y cuánto dura «una temporada» (p0311:50).

*Sala, cocina y alergias*

13. **Las 15 decisiones del 02-11** (usab:383-397): **se mantienen todas**.
14. **Alérgenos UE por plato y por bebida:** **sí**. Así solo va a revisión lo que choca.
15. **Nota de cocina aparte** para «al punto» o «sin sal»: **sí**. Las preferencias siguen pasando por revisión, como se decidió el 15-10.
16. **Personas al abrir mesa:** **sin número elegido, con botones del 1 al 8 de un toque** (usab:494-497 frente a p1015:185).
17. **Numeración de comensales de la casa:** **la de partida**, salvo que la casa tenga otra.
18. **Sala unida a su estación desde el principio:** **sí** (ADR-018:37).

*Cobro y fiscal*

19. **Datáfono:** **teclea el importe Caja**, y «Datáfono» sustituye a «Tarjeta». Las propinas, **registradas aparte de la cuenta** (p0311:42).
20. **Impresora:** **80 mm, ESC/POS, con red y corte (por ejemplo, Epson TM-m30III), con cajón**. Es urgente (p0311:43).
21. **Hacienda en enero.** ¿Está Verial adaptado a VERI*FACTU para enero? AÑITSOC SL está obligada desde el 1-1-2027 (h0311:44).
22. **VERI*FACTU** antes de mediados de diciembre: **proveedor certificado que emita sin internet**.
23. **Para el gestor** (p0311:55-60):
    - ¿Retiro es sociedad?
    - IVA del vino en sala frente a la botella para llevar;
    - IVA de las villas;
    - ¿la señal es anticipo o fianza?
    - cómo se facturan hoy los pedidos de la web.
24. **Tienda:** **WooCommerce, que es la que hay**, preparada para PrestaShop. Las claves de su API las pones tú.

*Operación y datos*

25. **Servidores:** **dos mini-PC con Linux, SAI y 4G**. ¿Compra o alquiler? Para Costi-a elegí el 03-11 el equipo principal de recepción, que es un PC con Windows. Si lo prefiero también aquí, el sistema tiene que correr en Windows: dime qué cambia y qué se pierde.
26. **Cuánto se puede perder y cuánto parar:** **1 minuto de datos; 15 minutos parado en el local y 4 horas con equipo nuevo** (recap:140).
27. **Copias y parte pública:** **en un proveedor de la UE**. Coste a presupuestar en F0.
28. **Carta de vinos** (p0311:47): **en internet, con precio de la tarifa General y «quedan pocas» o «agotado»**. ¿Quién la mantiene? ¿Se ofrece en la wifi de clientes?
29. **Texto de las alergias:** **se borra a los 30 días**, a confirmar con el asesor de protección de datos.
30. **Aparatos reales del local:** modelos de móviles, tablets y PC.
31. **Exportación de Verial:** **pedir ya un fichero real** con bebidas, tarifas, clientes, proveedores e inventario (p0311:82).
32. **Lista de lo que se usa hoy de Verial** (p0311:84): escandallos, televenta, contabilidad, remesas, factura electrónica, lectura de DNI, firma, alta rápida y libro de reservas.
33. **Atributos del vino:** **importarlos todos, con una referencia por añada y formato**.