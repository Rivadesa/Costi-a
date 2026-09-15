# Plan de desarrollo — Windows, multidispositivo y gestión integral

Fecha: 2026-09-15. Producto: Costi-a / Hospitality OS. Primera implantación: Retiro da Costiña y AÑITSOC SL.

**Estado: propuesta de arquitectura y ejecución, no implementación ni aprobación de producción.** Este documento no sustituye automáticamente los ADR vigentes. La migración tecnológica requerirá un ADR que identifique qué decisiones quedan sustituidas y actualizar AGENTS.md, arquitectura, contratos y estado. No interpretar esta propuesta como autorización para desplegar sobre datos reales.

## 1. Base, alcance y recomendación

Requisitos confirmados del promotor: aplicación Windows de escritorio; instalación sin Docker gestionado por el restaurante; posibilidad de funcionar en un PC o con varios puestos; tablets Android/iPad y teléfonos Android/iPhone para consultas y comanderos; control del comedor y cocina; catálogo, variantes, precios, stock, multiempresa; conectores de terceros, especialmente TheFork y dos WooCommerce, gourmet y bonos. Cuenta/Caja permanece separada del seguimiento de mesas y limitada a equipos y usuarios autorizados.

Base documental: revisión técnica aportada `costina-revision-tecnica-y-opciones.md`, conversación funcional, ADR-007, docs/STATUS.md y la incidencia #17. La revisión aporta requisitos y alternativas; las decisiones detalladas de este plan son recomendaciones nuevas. Las capacidades externas citadas se han contrastado con documentación oficial; no prueban acceso comercial a ninguna API ni disponibilidad de funciones en nuestra aplicación.

Estado consultado: STATUS describe Laravel/PostgreSQL, cliente Vue/Tauri 0.1.1 y ensayo Docker en un PC; no una implantación de producción. La incidencia #17 continúa abierta: pago y estado operativo siguen acoplados. No se ha realizado una nueva auditoría completa del código en esta tarea de planificación.

**Arquitectura recomendada:** servidor ASP.NET Core/.NET 10 LTS como servicio Windows, PostgreSQL, cliente principal C#/WPF y cliente web instalable Vue/TypeScript para los dispositivos móviles y KDS. Un solo motor de negocio compartido por API. Sin Docker, WSL, Redis o RabbitMQ obligatorios en el local. Sin microservicios ni multi-master. Microsoft documenta WPF como tecnología Windows, alojamiento de ASP.NET Core como servicio y distribución autocontenida; estas capacidades no equivalen a que nuestro instalador ya las implemente. [S1-S4]

Se elige esta referencia para fijar el rumbo Windows, no porque .NET sea la única tecnología posible. No se iniciarán paralelamente implementaciones Python, Go y .NET. No hay mediciones que permitan prometer que este cambio consume menos memoria que el prototipo.

## 2. Un producto, tres modalidades de instalación

### Modo A — Un solo equipo

En el mismo Windows se instalan el servidor, PostgreSQL y el cliente de escritorio. La ventana consume la misma API que utilizará un cliente remoto. Cerrar la ventana no detiene el servidor. Apagar o suspender este PC sí interrumpe el servicio. No existe una segunda versión de negocio ni una base SQLite alternativa para este modo.

### Modo B — Varios puestos

Un Windows aloja el servidor; los demás se conectan a él. El servidor puede convivir inicialmente con el puesto principal, pero para Retiro se recomienda un equipo dedicado, alimentación protegida y red cableada para los puestos fijos. Los terminales no necesitan tener abierto el programa del maître: se conectan al servicio independiente.

### Modo C — Continuidad reforzada

Añadir un segundo equipo con backend compatible, configuración, certificados y réplica de PostgreSQL. Conmutación manual controlada al comienzo. No se promueve una réplica porque un cliente pierda Wi-Fi; primero debe aislarse al primario anterior. Dos servidores escribiendo el mismo inventario quedan expresamente prohibidos. [S11]

La arquitectura de varios locales físicos será una extensión posterior: autoridad por establecimiento/almacén y transferencias entre ellos. Varios puestos del mismo restaurante no requieren varios primarios.

## 3. Superficies y permisos

| Puesto | Cliente | Uso previsto |
| --- | --- | --- |
| Principal Windows | WPF, sin WebView para las pantallas del producto | Administración, control, catálogo, Cuenta/Caja y gestión autorizada |
| Windows adicional | Mismo WPF, perfil restringido | Sala, almacén, oficina o caja solo si se autoriza expresamente |
| Tablet Android / iPad | PWA Vue servida en la LAN | Comandero, control del servicio, KDS o inventario según permisos |
| Android / iPhone | Misma PWA adaptada al tamaño | Consultas y comanderos; cámara/lector se validan por dispositivo |
| Fuera del restaurante | Portal seguro en nube | Consultas autorizadas, inicialmente solo lectura y con fecha de actualización |

La PWA es una aplicación web instalable, no una aplicación nativa iOS/Android. Ese compromiso reduce interfaces que mantener y conserva Vue. No se promete entrega fiable de comandas mientras el navegador o teléfono está suspendido: Background Sync tiene disponibilidad limitada. Operación crítica en primer plano; pruebas con bloqueo/desbloqueo, cierre y reconexión en dispositivos reales. [S5-S6]

El seguimiento muestra mesas, pax, restricciones, pases, tiempos e incidencias, nunca cuenta, importes ni pagos. Registrar una bebida desde un comandero puede habilitarse como pedido operativo sin precio ni saldo: no debe reutilizar la respuesta económica de Cuenta/Caja. El precio lo calcula el servidor y los importes solo se devuelven al contexto financiero autorizado.

## 4. Instalación y conectividad

El instalador tendrá modalidades completa, servidor y puesto adicional. Incluirá dependencias redistribuibles, validará requisitos y puertos, creará credenciales únicas, registrará servicios, configurará acceso limitado de red y ofrecerá diagnóstico. No requerirá PHP, Composer, Node, Python, SDK de .NET, Docker ni comandos manuales. Publicar autocontenido incorpora dependencias: no hace desaparecer PostgreSQL ni las actualizaciones de seguridad. [S2-S4]

Programa y datos estarán separados. Una instalación nueva inicializa; un arranque normal no migra ni repone semillas; una actualización usa un procedimiento explícito. Desinstalar la interfaz no borra la base. El instalador y los binarios de distribución deberán firmarse y acompañarse de versión, commit, manifestación de componentes y hashes.

En la LAN se utilizará un nombre estable, resolución DNS local y HTTPS con certificado confiable en todos los dispositivos. Los service workers necesitan contexto seguro; la excepción localhost no resuelve el acceso desde otro móvil. Alta y renovación de certificados forman parte del instalador y del soporte. No se pedirá ignorar avisos de certificado. [S5]

Un QR de enrolamiento, de corta duración y un solo uso, identifica la instalación y abre la solicitud de vinculación; un administrador aprueba rol y estación. Usuario y dispositivo son identidades distintas. Cambiar localStorage o enviar una cabecera `main` no concede acceso a caja. La API aplica ámbito tenant/empresa/centro, permisos y autorización de terminal.

No se expone PostgreSQL a Internet ni se abre el router para que funcionen los comanderos. Los puestos nunca escriben directamente en la base. Para consultas remotas se usa un portal con autenticación reforzada; para acceso remoto vivo, un canal autenticado iniciado desde el local. En caso de desconexión se mostrará la antigüedad de la información.

## 5. Diseño interno para mantenimiento

Monolito modular con capas dominio, aplicación, infraestructura y API. Clientes separados: WPF y Vue. La nube empleará preferentemente el mismo stack .NET para no mantener un segundo motor de negocio PHP; reutilizar piezas Laravel aisladas requerirá justificación, sin duplicar reglas de stock/pagos.

Módulos previstos: Identidad/Instalación; Servicio de comedor; Cocina; Catálogo/PIM; Tarifas; Ventas/Cuenta/Caja; Inventario; Compras; Empresas; Reservas; Bonos; Fiscalidad; Integraciones; Operación técnica.

Cada módulo expone contratos explícitos. No escribe arbitrariamente en tablas de otro. Contratos OpenAPI versionados y clientes tipados C#/TypeScript; lógica de negocio solo en servidor. PostgreSQL conserva datos, auditoría, idempotencia e inbox/outbox. Los trabajos persistentes usan inicialmente PostgreSQL y workers supervisados; no depender de una cola en memoria.

SignalR transmite avisos por ámbito autorizado. Su reconexión debe configurarse y no sustituye recuperación de datos ni reintentos de comandos. Al reconectar se consultan revisiones y estado autoritativo. El aviso del outbox se publica solo después de confirmar la transacción; los consumidores toleran duplicados. [S7]

## 6. Comedor y cocina: núcleo del primer piloto

Mesas, salas, menús, pases y estaciones configurables, sin límites codificados para Retiro. Cada mesa tiene una ejecución independiente del menú y restricciones estructuradas por comensal. El menú maestro no cambia por una excepción del servicio.

Separar antes de ampliar UI:

- Ritmo operativo: preparado, en curso, pausado, finalizado o anulado.
- Ocupación física: libre, ocupada, pendiente de acondicionar o bloqueada.
- Liquidación: abierta, parcialmente cubierta, cubierta, cerrada o corregida.

Un prepago no detiene cocina; acabar un pase no registra un pago; liberar una mesa no borra la cuenta. Esta es la resolución funcional de #17, con una migración explícita para datos anteriores.

Sala dispara pases manualmente. Preparaciones por estación y comensal, validación final de chef configurable y una acción servido. No exigir botones comiendo/finalizado para producir métricas ficticias: el intervalo entre acciones no demuestra cuánto duró comer. Solapamientos entre pases, pausas y excepciones se autorizan y registran; no se impone una única secuencia rígida para todos los negocios.

Sustituir, omitir, añadir o rehacer se hace con motivo, permisos y aviso. Una restricción nueva después de enviar a cocina requiere nueva versión de preparación y reconocimiento visible; no basta con editar una nota. La aplicación coordina, no certifica que una elaboración sea segura para una alergia. Validación humana y mínima retención de datos personales.

## 7. Catálogo, variantes, presentaciones y tarifas

**Producto maestro:** identidad comercial, marca/productor, categorías, textos, imágenes y atributos. Especialización de vinos: añada, DO, uvas, graduación, notas, fotos, ficha técnica y publicación por idioma/canal. No duplicar la ficha maestra para cada tienda.

**Variante inventariable:** combinación que requiere identidad/stock propios. Un vino 2021 de 750 ml y el mismo vino 2022 o de 1.500 ml son referencias distintas. Lote de recepción y añada son conceptos distintos.

**Presentación vendible:** unidad, botella, copa, caja o palet. Una copa es normalmente una extracción de una botella abierta, no otro stock independiente. Una caja virtual de seis botellas consume seis unidades de la variante; un embalaje sellado o kit mixto con trazabilidad propia requiere transformación/composición explícita. Un palet no tiene un número fijo universal de botellas.

**Tarifa:** precio por empresa, centro, canal y, cuando corresponda, cliente/grupo, cantidad y vigencia. Coste de compra separado del precio de venta. Precedencia de reglas determinista, impuestos explícitos, descuentos con límites y cambio auditado. Totales liquidados en unidades menores de moneda; costes, cantidades y cálculos intermedios con decimal exacto y precisión suficiente. Nunca float binario para dinero.

Al confirmar una línea se conserva referencia, nombre, presentación, cantidad, precio aplicado y tratamiento fiscal correspondiente. Un cambio posterior de tarifa no reescribe cuentas previas. Operaciones concurrentes rechazan una revisión obsoleta y piden recarga; el cliente no fija un precio libre para consumiciones normales.

## 8. Inventario y compras

Inventario basado en movimientos trazables y saldos derivados/reconciliables, no un contador editable sin historia. Dimensiones mínimas: variante, ubicación, propietario, lote cuando aplique y condición. Entradas, reservas, entregas/consumos, traslados, roturas, mermas, devoluciones y ajustes de recuento tienen identidad, usuario, causa y fecha.

Definir exactamente cuándo se reserva y cuándo se consume. Un pedido reserva; su entrega confirmada descuenta físico una vez. Cobrar o emitir una factura no vuelve a descontar. Deshacer un cargo no devuelve mágicamente al stock una botella que ya se bebió. Las devoluciones físicas y abonos son procesos relacionados pero distintos.

Disponible = físico utilizable menos reservas activas, según política. No restar dos veces el mismo compromiso con nombres distintos. Negativos bloqueados por defecto; ajustes excepcionales con permisos, motivo y revisión.

Vino: mantener stock cerrado en botellas y contenedores abiertos con capacidad y remanente. Abrir transfiere a estado abierto; servir copas registra extracción/merma, sin descontar otra botella por cada copa. Porciones y pérdidas configurables, medición y recuentos conciliables.

Compras básicas preceden al ecommerce productivo: proveedores, pedidos, recepciones parciales, lotes/caducidades donde apliquen, precios históricos y documentos. Inventario móvil puede facilitar conteos y lectura de códigos tras validar cámara/periférico; no se presupone compatibilidad universal.

## 9. Multiempresa y almacén compartido

Separar grupo/tenant, entidad jurídica, establecimiento, almacén físico y titularidad de existencias. El almacén puede vincularse a más de una empresa; no se fuerza una jerarquía que lo convierta en propiedad exclusiva del centro.

Retiro y AÑITSOC comparten catálogo y, si procede, espacio físico. Conservan emisores de factura, series, cajas, tarifas, documentos y propiedad separados. Una botella físicamente en la bodega puede pertenecer a AÑITSOC y no estar disponible para consumo de Retiro sin una operación autorizada.

Traslado de ubicación no equivale a cambio de propietario. Las operaciones entre sociedades tendrán documento y validación administrativa adecuados; inicialmente se permite gestión manual auditada, no una simple edición silenciosa de owner_company_id ni una supuesta liquidación legal automática. Su tratamiento debe revisarse con la asesoría.

## 10. Integraciones y autoridad de los datos

El hub en nube recibe webhooks y ejecuta adaptadores externos, con identidades por tienda/restaurante y mínimos permisos. El local establece conexiones salientes; los eventos se persisten antes de confirmar recepción. Ninguna API externa participa de forma síncrona en enviar un pase.

Autoridad por dato: operación local en el servidor; catálogo maestro y tarifas en el módulo designado; reserva externa en su proveedor hasta convertirla en servicio local; pedido ecommerce identificado por tienda y referencia externa. Un evento recibido no puede sobrescribir indistintamente toda una ficha.

Cada conector exige inbox/outbox, idempotencia, reintentos con espera, control de versiones/orden, cursor de conciliación, registro de errores, estado visible y pruebas contractuales. Un error no se oculta como sincronizado. No prometer entrega exactamente una vez: se persigue efecto de negocio único frente a entregas repetidas.

### TheFork

Hay que tramitar acceso y confirmar contrato/sandbox al comienzo. La documentación distingue POS API, que recibe llegada/asiento y envía detalles de cuenta, de B2B API, que contempla reservas, disponibilidad y eventos. POS API no permite crear reservas ni actualizar su estado desde el POS. No se promete sincronización completa solo por obtener una clave POS. [S8-S9]

Primera integración: importar reservas y cambios permitidos, mantener referencias y evitar duplicados; asignación interna de mesa/servicio. Publicación de disponibilidad o cambios de reserva solo cuando el acceso y contrato lo permitan. Modo manual/importación autorizada como alternativa. Sin scraping de sistemas privados.

### WooCommerce gourmet

PIM hacia productos/variaciones, medios, tarifas y disponibilidad de canal. Pedidos, pagos confirmados, cancelaciones y devoluciones hacia gestión. Usar la REST API recomendada y mapeo por tienda/producto/variación; no diseñar el dominio alrededor de los IDs Woo. La API v3 permite trabajar con variantes, atributos, imágenes, precios y stock. [S10]

Las cajas y palets vinculados a las mismas botellas requieren un cálculo común de disponibilidad. No publicar existencias completas en cada variante comercial como si fuesen independientes.

WooCommerce puede desactivar webhooks tras fallos repetidos: siempre se incluye reconciliación periódica. No confundir pedido creado con pago capturado; no regenerar pedidos, movimientos o facturas al recibir notificaciones repetidas. [S10b]

### WooCommerce bonos

Conector independiente del gourmet, con credenciales, catálogo, emisor y conciliación separados. El bono vive en un módulo propio: código, empresa emisora, tipo monetario/experiencia, condiciones, saldo/unidades, reservas de canje, usos, caducidad configurada y devoluciones. Un cupón de descuento Woo no sustituye este libro de bonos.

La emisión se deriva de un pago confirmado y es idempotente. Una experiencia puede cubrir dos menús sin cubrir bebidas. Aplicar un bono reduce la deuda pertinente sin detener el servicio ni contabilizar otra vez su venta como ingreso nuevo. Revisión fiscal del tratamiento y de bonos existentes antes de producción.

Para canje sin Internet solo se admiten autorizaciones sincronizadas y reservadas al establecimiento con autoridad exclusiva de canje. Un código firmado o una copia antigua no impiden por sí solos el doble uso. Bonos desconocidos esperan verificación o una excepción humana auditada; reembolso/canje concurrentes exigen coordinación, no aceptación offline universal.

## 11. Evitar sobreventa desconectada

La sincronización técnica no elimina un conflicto de negocio entre dos canales incomunicados. Asignar cupos no solapados por variante/propietario: por ejemplo, de diez botellas utilizables, seis para restaurante, tres para ecommerce y una de seguridad. Es un ejemplo de política, no una capacidad ya construida.

Ambos canales respetan los cupos incluso durante un corte. El online sigue vendiendo solo su disponibilidad delegada; no puede tomar las últimas unidades reservadas a sala. Su saldo no se repone publicando una cifra antigua desde el local. Primero se concilian pedidos pendientes y sus reservas; después se rebalancea mediante operación versionada.

Para botella única, un canal prioritario o confirmación manual. Firma, CRDT o última escritura no crean la botella que falta. Bonos siguen una política equivalente de autoridad de canje, sin prometer consistencia global desconectada.

## 12. Fiabilidad, seguridad y operación

Cada comando relevante lleva identificador estable, instalación, servicio/entidad y revisión esperada. Operación, auditoría, resultado idempotente y outbox se confirman en una transacción. Mostrar confirmado solo tras respuesta persistida; indicar pendiente, rechazado o pendiente de revisión cuando proceda. Tras una respuesta perdida, reintentar la misma operación, no crear otra.

La cola de dispositivos será durable: almacenamiento local protegido para Windows e IndexedDB para PWA, solo con mínimos datos y sin convertirla en autoridad. Acciones antiguas, restricciones modificadas y servicios cerrados no se reenvían sin revalidación. Probar límites de almacenamiento, cierre/reapertura y fallo de Wi-Fi. [S6]

Seguridad: roles por empresa/centro/estación, dispositivo enrolado, tokens revocables, mínimos privilegios de servicios, TLS, credenciales únicas, secretos de conectores nunca en el móvil. Evitar tokens de larga vida en localStorage; diseñar autenticación del navegador con cookies seguras/CSRF o un esquema equivalente revisado. MFA en portal remoto. Minimización, retención y autorización de datos personales/restricciones; validar obligaciones aplicables con responsable de privacidad.

Diagnóstico de soporte: versión y commit, estado del servicio/bd, colas pendientes, última copia/restauración, antigüedad de sincronización, certificados y almacenamiento. Logs estructurados con correlación, sin contraseñas ni exportaciones indiscriminadas de datos de clientes. Telemetría y acceso remoto autorizados.

## 13. Copias, recuperación y continuidad

El producto base debe incluir copias versionadas fuera del disco del servidor y restauración ensayada antes del piloto. Respaldar base, imágenes/documentos, configuración, certificados y claves necesarias, con protección y recuperación de esas claves. Definir retención, cifrado, alertas y ubicación separada. Una copia local/RAID/réplica no es por sí sola una protección frente a todos los incidentes.

PostgreSQL permite copia base y archivado WAL para recuperar a un momento concreto. La herramienta Windows se selecciona tras probar copia y restauración; no se declara pgBackRest compatible nativo ni se certifica WAL-G solo por una referencia documental. [S12]

Acordar RPO (pérdida tolerable) y RTO (recuperación) por modalidad y medirlos. No prometer cero pérdida con réplica asíncrona. Un corte WAN incrementa el tiempo desde la última copia exterior; el tablero debe mostrarlo. Si una promoción pierde transacciones, también puede perder su idempotencia: reconciliación antes de reanudar operaciones dudosas.

Modo continuidad: aislar primario, comprobar réplica y retraso, promover una sola vez, activar workers y servicios correctos, cambiar endpoint estable y reconstruir redundancia. No probar apagando solo un cliente. No se incluyen facturación offline o sincronización cloud como capacidades ya validadas por este plan.

## 14. Fiscalidad y retirada de Verial

Desde el diseño: servicio, cuenta, pago, factura y registro fiscal separados. Anular no es borrar ventas sin rastro. Una cuenta operativa no sustituye una factura/simplificada ni elimina obligaciones de trazabilidad. La AEAT establece requisitos de integridad, conservación y trazabilidad, y obligaciones del productor del SIF. [S13]

Durante el piloto se mantiene un circuito fiscal existente validado, con conciliación de cuentas e identificadores; no se presupone que exista una API de Verial. Integración automática solo tras comprobarla, o procedimiento asistido documentado. Bonos ya vendidos deben seguir un sistema autorizado hasta completar su migración conciliada.

La fiscalidad propia o un proveedor fiscal compatible se valida antes de retirar el sistema anterior: series por empresa, impuestos, rectificativas, reintentos, conservación, certificados y documentación del productor aplicable. Mantener archivo histórico y no ejecutar dos emisores sobre la misma serie. El desarrollo legal no se da por terminado porque compile el instalador.

## 15. Entregas y criterios de aceptación

Estas entregas describen el producto objetivo; no amplían todas a la vez el MVP. No se fija calendario ficticio sin backlog, capacidad de ejecución y disponibilidad de validación física.

| Entrega | Contenido | Prueba para considerarla terminada |
| --- | --- | --- |
| D0 — Contrato y migración | ADR de destino, separación #17, matriz de dispositivos y permisos, mapeo del sistema actual; solicitud de acceso TheFork | Escenarios y mapping sin pérdida definidos; ADR y backlog revisables |
| D1 — Instalador Windows real | Servicio .NET, PostgreSQL, WPF mínimo y circuito de una mesa; diagnóstico, inicialización única y copia/restauración inicial | Instalar en Windows limpio sin Docker/SDK; cerrar ventana y reiniciar PC conservando datos |
| D2 — Comedor multidispositivo | PWA, HTTPS/enrolamiento, permisos, menú/pases/KDS, catálogo operativo y Cuenta/Caja separada; cola durable y realtime | Windows + Android + iPad + iPhone en LAN; cortes WAN/Wi-Fi y reintentos; sin datos económicos en sala |
| D3 — Gestión comercial | PIM/variantes/presentaciones, tarifas, multiempresa, recepciones, inventario, botellas abiertas y documentos intercompany básicos | Caja/copa/botella no duplican existencias; historia de tarifas y propiedad conciliada; fotos/documentos restaurables |
| D4 — Canales y bonos | Hub, dos WooCommerce, motor de bonos y reservas externas según acceso; cuotas de stock y reconciliación | Pago y webhook duplicados no duplican efectos; última botella, canje y cancelaciones controlados |
| D5 — Sustitución fiscal | Motor/adaptador fiscal validado, migración de saldos/bonos/documentos, cierres y exportación a asesoría | Ventas y emisión conciliadas por empresa; archivo antiguo conservado; retirada explícita de Verial |
| D6 — Producto comercializable | Instalación reproducible para otro negocio, actualizaciones firmadas, soporte, métricas, recuperación y continuidad contratada | Segundo restaurante se configura sin fork y supera la misma batería física/funcional |

TheFork se verifica en D0 y su importador puede adelantarse tras D2 si el acceso lo permite. El diseño fiscal comienza en D0; D5 puede adelantarse frente a ecommerce si sustituir facturación es más prioritario. No hay que esperar a D5 para diseñar impuestos o documentos. El módulo de continuidad, cuando sea requisito del primer piloto, se prueba antes de iniciar ese piloto.

D1 es el primer instalador nuevo, con alcance de una mesa y servidor real, no otra demo desconectada. D2 es el primer candidato a ensayo físico de comedor; no una aprobación automática de producción. D3-D5 completan la gestión empresarial y la retirada del ERP.

## 16. Migrar lo existente sin empezar de cero funcionalmente

Conservar contratos válidos, escenarios de dominio, esquema aprovechable, fixtures, documentación y UI Vue útil. Reimplementar runtime/cliente nativo no equivale a recompilar PHP. Corregir primero los escenarios defectuosos, especialmente #17.

Archivar un punto de referencia Laravel/Tauri; nueva implementación en áreas diferenciadas del repositorio. Comparar contratos y comportamiento con pruebas de paridad; cuando hay cambio intencionado, actualizar especificación y test. Migraciones con ensayo, recuentos, claves, saldos y snapshots comprobados.

Nunca permitir a dos backends modificar libremente las mismas tablas durante transición. Cada entorno/función tiene un escritor definido. El rollback del binario no implica que pueda revertirse cualquier migración: versionar esquema y estrategia de compatibilidad, probar restauración antes del cambio.

## 17. GitHub, mantenibilidad y trabajo con otras IAs

Una issue por entrega acotada, con criterio de aceptación, impacto en API/esquema, seguridad, desconexión y pruebas. Ramas cortas, PR revisable y CI obligatorio. No mezclar refactor de dominio, nuevo módulo y empaquetado en un único cambio enorme.

Documentación mínima por PR: STATUS con implementado/probado/pendiente; ADR cuando cambia arquitectura; OpenAPI y contratos de eventos; migración de datos; instrucciones de instalación y recuperación; limitaciones conocidas; evidencias de pruebas y versión/commit. Conservar AGENTS.md al día tras aprobar la nueva arquitectura; este plan propuesto no autoriza saltarse sus reglas vigentes.

CI: dominio, API/autorización multiempresa, PostgreSQL real, duplicados/concurrencia/rollback, Vue/PWA, WPF, empaquetado y pruebas de actualización. Pruebas físicas aparte: no equiparar navegador emulado a iPhone real ni compilación a instalación aceptada.

Objetivos de rendimiento propuestos, no resultados: prueba inicial con 20 terminales y 10.000 referencias vendibles; medir percentiles de respuesta y propagación de cambios, memoria y arranque sobre hardware de referencia. Objetivo inicial de aviso KDS menor de un segundo en LAN saludable, ajustable tras medición. Ninguna cifra representa garantía comercial ni límite fijo del modelo.

Como mínimo, antes del piloto: emitir dos veces el mismo comando; actuar desde dos usuarios; perder Wi-Fi antes/después de confirmar; reiniciar cliente/servidor; cambiar tarifa con cuenta abierta; pagar con cocina activa; cambiar restricción tras enviar; recuperar copia en otra máquina. Antes de ecommerce: webhooks fuera de orden, pedido pagado/cancelado, última unidad compartida, devoluciones y bono duplicado.

Responsabilidades: desarrollo implementa y aporta pruebas; personal de sala/cocina valida operativa; responsable de implantación comprueba hardware/red/recuperación; asesoría valida procesos fiscales e intercompany. La revisión de código humana o por otra IA complementa, no sustituye, las pruebas físicas y la validación del negocio.

## 18. Criterio de éxito y límites

Éxito: mismo producto instalado en un PC o en LAN, servidor independiente de la ventana, clientes móviles sin duplicar reglas, estados coherentes, inventario explicable, precios históricos intactos, integraciones conciliables y restauración demostrada.

No abrir en paralelo nóminas, contabilidad general, PMS completo, motor propio de reservas, IA predictiva o cinco aplicaciones nativas diferentes. No declarar sin dependencias, exactamente una vez, cero pérdida o funcionamiento offline ilimitado. No modificar producción, migrar automáticamente datos ni publicar una release estable con este documento.

## Fuentes y trazabilidad

Documentación interna consultada el 15-09-2026: `AGENTS.md`, `docs/AI_CONTEXT.md`, `docs/PRODUCT_SCOPE.md`, `docs/STATUS.md`, ADR-007 y https://github.com/Rivadesa/Costi-a/issues/17. Documento aportado: `costina-revision-tecnica-y-opciones.md`; referencia de requisitos y crítica, no certificación de estado.

Fuentes externas oficiales consultadas el 15-09-2026. Describen capacidades de las plataformas, no implementación de Costi-a:

- [S1] Soporte .NET, selección de .NET 10 LTS: https://dotnet.microsoft.com/en-us/platform/support/policy
- [S2] ASP.NET Core como servicio Windows y publicación autocontenida: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
- [S3] WPF para Windows: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- [S4] Instalación/binarios Windows de PostgreSQL: https://www.postgresql.org/download/windows/
- [S5] Service Worker API y contexto seguro: https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API
- [S6] Background Synchronization y compatibilidad limitada: https://developer.mozilla.org/en-US/docs/Web/API/Background_Synchronization_API
- [S7] Cliente SignalR y reconexión: https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0
- [S8] TheFork POS API y sus límites: https://docs.thefork.io/POS-API/introduction
- [S9] TheFork B2B API y eventos de reservas: https://docs.thefork.io/B2B-API/introduction
- [S10] WooCommerce REST v3 y variantes: https://developer.woocommerce.com/docs/apis/rest-api/v3/ ; https://developer.woocommerce.com/docs/apis/rest-api/v3/product-variations/
- [S10b] Webhooks WooCommerce y desactivación: https://woocommerce.com/document/webhooks/
- [S11] Conmutación PostgreSQL y prevención de dos primarios: https://www.postgresql.org/docs/17/warm-standby-failover.html
- [S12] Copias base, WAL y recuperación temporal: https://www.postgresql.org/docs/17/continuous-archiving.html
- [S13] AEAT, requisitos generales SIF: https://sede.agenciatributaria.gob.es/Sede/iva/sistemas-informaticos-facturacion-verifactu/cuestiones-generales.html ; declaración responsable del productor: https://sede.agenciatributaria.gob.es/Sede/iva/sistemas-informaticos-facturacion-verifactu/preguntas-frecuentes/certificacion-sistemas-informaticos-declaracion-responsable.html
