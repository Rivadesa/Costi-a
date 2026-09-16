# Costiña — Revisión de avances, seguridad y funcionamiento

**Fecha:** 16 de septiembre de 2026.  
**Repositorio:** `Rivadesa/Costi-a`.  
**Rama revisada:** `develop`.  
**Commit de referencia:** `ed715a4927dee6bf04571839b82ac9279ce9d469` (integración de PR #33).  
**Árbol:** `719f92900be28b4d7cdb40cc7e1d84605c43a00f`.  
**Tipo de revisión:** lectura de código, contraste entre componentes, comparación con D1.2 y revisión de evidencias existentes de CI. **No es una certificación, pentest contra una instalación ni una nueva ejecución de todos los tests.**

## 1. Dictamen

El avance respecto al corte D1.2 es real y está integrado: publicador del outbox y SignalR; interfaz WPF refactorizada a MVVM; acciones calculadas en servidor y filtradas por rol; restricciones estructuradas por comensal con acuse de cocina; y persistencia local cifrada de la orden incierta. La documentación raíz ya reconoce el stack nativo y deja congelado el legado Laravel/Tauri.

El producto sigue siendo un laboratorio local, no una instalación operativa lista para reemplazar el sistema del restaurante. Antes del piloto corregiría, sobre todo, el contexto de selección tras lecturas fallidas y la recuperación de comandos pendientes. Antes de LAN hacen falta identidades de usuarios y dispositivos, comunicaciones seguras, permisos por estación y un procedimiento de instalación y restauración comprobado.

No recomiendo cambiar de lenguaje ni reescribir el motor. Los problemas identificados son de comportamiento, persistencia del cliente, autorización operativa y entrega.

## 2. Alcance y procedencia de las pruebas

Se consultaron ramas actuales, el historial reciente, `docs/STATUS.md`, PR #33 y los workflows correspondientes. Se descargó el artefacto de fuentes completo de la ejecución de PR #33. Su comentario Git identifica el merge de prueba `37f374e9c200d5558c720000a4c21a63088d8069`, cuyo árbol coincide con el árbol de `develop` revisado. Se contrastaron también archivos críticos mediante lecturas directas del commit de `develop`.

La revisión cubre especialmente:

- `Costina.Domain`: servicio, pases, restricciones, ocupación y cuenta.
- `Costina.Persistence`: ámbito de datos, transacciones, idempotencia y outbox.
- `Costina.Server`: autenticación, metadatos de autorización, contratos y SignalR.
- `Costina.Client` y `Costina.Desktop`: reintentos, almacenamiento DPAPI, MVVM y selección de contexto.
- Tests, workflows, documentación, versiones e integración del instalador.

No se ha auditado en profundidad todo el runtime PHP congelado, los periféricos, la configuración de un Windows real del usuario, la infraestructura de nube ni APIs externas todavía no implementadas. No se han realizado cambios, commits, ejecuciones de ataque, borrados ni migraciones en el repositorio o en una base real.

Este entorno no tenía `dotnet` disponible. Por tanto, las nuevas reproducciones propuestas abajo **no se presentan como ejecutadas**. Los defectos de flujo se deducen de rutas de ejecución concretas del código, y la evidencia de ejecución es la ya publicada en GitHub Actions.

### Evidencia de CI consultada

| Ejecución | Resultado consultado |
|---|---|
| `35081131708` — Native D0 domain acceptance | Completada, correcta |
| `35081131717` — Native D1 PostgreSQL and HTTP | Completada, ambos trabajos correctos |
| `35081131715` — Native WPF desktop acceptance | Completada, correcta |

Informes descargados y leídos:

- `client-checks.json`: **27 pruebas pasadas, 0 fallidas**.
- `d1-http-results.json`: **11 escenarios, 0 fallos, 0 errores**.
- `d1-security-results.json`: **6 comprobaciones correctas**.
- `artifacts/realtime/realtime-checks.json`: **9 pruebas pasadas, 0 fallidas**.
- `window-checks.txt`: ventana WPF renderizada y comprobaciones de mapeo; el informe aclara que no afirma interacción con backend real.

Los pasos adicionales de affordances, restricciones y regresiones están en verde. No se asignan cifras nuevas a esos pasos sin su correspondiente informe contado. El verde confirma los casos ejecutados, no todos los casos de fallo enumerados en este documento.

## 3. Controles bien orientados

**Frontera económica.** Los endpoints `/checkout/...` siguen reservados al rol `main`; la configuración operativa omite precios. El publicador dirige tipos `account.*` y `payment.*` a la audiencia financiera. Los avisos solo contienen identidad, tipo, agregado y fecha, no el payload económico.

**Persistencia transaccional.** Estado, auditoría, outbox y respuesta idempotente se escriben dentro de una transacción. Se comprueba versión y se serializan reintentos por clave, actor y ámbito. Hay un índice único que impide dos ocupaciones activas de la misma mesa y claves foráneas compuestas por ámbito.

**SQL y autoridad.** En las rutas inspeccionadas se parametrizan los valores. Los identificadores SQL dinámicos proceden de llamadas internas con constantes, no del usuario HTTP. El ámbito lo determina el servidor y se rechazan cabeceras que intentan sustituirlo. No he encontrado en estas rutas una concatenación directa de datos HTTP en SQL; esto no equivale a un escaneo exhaustivo de todo el repositorio.

**Precio aplicado.** El comando de consumo obtiene el artículo y su precio desde configuración del servidor; el cargo conserva el valor aplicado. La interfaz no decide el precio de una venta normal.

**Separación del dominio.** `DiningService`, `TableOccupancy` y `SettlementAccount` son independientes. El pago ya no cambia el estado de cocina en el motor nativo. El legado y su migración son otra cuestión y no deben darse por resueltos por esa separación.

**Reconexión.** El canal de avisos no se confunde con la confirmación de una orden. Se relee el estado autorizado tras reconectar; los eventos se publican antes de marcarse. Esto permite reintentar la publicación, aunque no garantiza por sí mismo recepción o lectura humana de todos los avisos.

**Contención del laboratorio.** El servidor escucha en loopback, requiere configuración explícita y una base separada; el cliente HTTP rechaza endpoints externos y redirecciones. No procede describir esta instalación actual como un servidor expuesto a Internet.

## 4. Hallazgos prioritarios

Las prioridades indican orden de corrección del producto, no puntuaciones CVSS. **P1**: corregir antes de uso real o antes de abrir LAN, según el caso. **P2**: deuda que debe cerrarse durante estabilización. Se distingue defecto de código, limitación declarada y riesgo de diseño.

### F01 — P1: selección y datos cargados pueden apuntar a mesas/cuentas distintas

**Tipo:** defecto de flujo identificado por inspección; reproducción nueva pendiente.

**Localización:**

- `dotnet/src/Costina.Desktop/ViewModels/ServiceViewModel.cs:78–101,113–117,124–131`.
- `dotnet/src/Costina.Desktop/ViewModels/CheckoutViewModel.cs:48–76,81–99`.
- `dotnet/src/Costina.Desktop/ViewModels/ShellViewModel.cs:30–32,54–76`.

Al seleccionar otra mesa se actualiza `serviceId` antes de que termine la nueva lectura. Para cuentas ocurre lo mismo con `accountServiceId`. Si esa lectura falla, `Dining` o `Account` pueden seguir conteniendo el detalle anterior. El coordinador muestra el error pero libera `Busy`; `Writable` no comprueba que el identificador cargado coincida con el destino seleccionado.

Las acciones construyen la ruta con el identificador nuevo y utilizan la versión/acciones del objeto antiguo. Con dos entidades que tengan la misma versión, el control optimista no detecta que el operador estaba viendo otro contexto. En comedor, los identificadores de los pases de una misma plantilla se reutilizan entre servicios, por lo que tampoco constituyen necesariamente otra barrera.

**Escenario:** cargar A; seleccionar B; hacer fallar solo la lectura de B; pulsar una acción permitida según los datos de A cuando la API vuelve a aceptar peticiones. La solicitud puede dirigirse a B. En caja el riesgo es cargar una consumición o registrar un pago en la cuenta equivocada.

**Corrección propuesta:** conservar selección solicitada y entidad cargada como estados distintos; invalidar acciones al cambiar destino; construir cada comando con un contexto inmutable `{aggregateId, version, selectionGeneration}` obtenido de una lectura correcta. Publicar detalle, identidad y selección de forma conjunta. Rechazar localmente cualquier discrepancia. Mantener también la validación del servidor.

**Aceptación:** fallo o demora de la lectura de B no permite mutar ni A ni B; respuestas fuera de orden no cambian el destino; después de una lectura válida el comando contiene identidad y versión de la misma entidad. Repetir para comedor, preparación y caja.

### F02 — P1: un archivo pendiente ilegible se elimina y se pierde la capacidad de reintento

**Tipo:** defecto en la política de recuperación; observado directamente en código.

**Localización:** `DpapiPendingStore.cs:20–42`; `ApiClient.cs:69–106`; `PostgresStore.cs:41–64`; tests de escritorio que exigen borrar el archivo corrupto.

`Load()` captura errores criptográficos, JSON y de entrada/salida, elimina el archivo y devuelve `null`. Eso convierte una situación de resultado desconocido en aparente ausencia de operaciones pendientes.

La afirmación del comentario y de D3.3 de que la idempotencia protege «en todo caso» no se cumple. El servidor deduplica mediante **ámbito + actor + clave**. Una repetición manual genera una nueva clave; si la operación anterior se aceptó, esa segunda operación no es su reintento y puede aplicarse también.

**Escenario:** el servidor acepta un consumo, se pierde la respuesta, el pendiente queda dañado o temporalmente ilegible y el programa se reinicia. Al descartarlo se permite volver a cargar el consumo con otra clave. No se afirma que cualquier corrupción implique un duplicado: el riesgo aparece cuando la operación inicial ya fue aceptada o su resultado sigue sin conocerse.

Además, el guardado sobrescribe directamente el archivo final con `File.WriteAllBytes`; no hay protocolo de sustitución atómica, respaldo ni vaciado explícito duradero. Una caída en el guardado no tiene siempre el mismo efecto que una caída después del envío, y ambas deben probarse por separado.

**Corrección propuesta:** fallo cerrado para mutaciones; conservar archivo en cuarentena; diferenciar ausencia, corrupción y fallo transitorio de acceso. Guardado duradero con reemplazo controlado y copia recuperable o una pequeña cola transaccional. Incorporar consulta autorizada del estado de un comando y conciliación asistida. Nunca resolver incertidumbre destruyendo la evidencia.

**Aceptación:** pérdida de respuesta más archivo dañado; archivo temporalmente bloqueado; disco lleno; fallo antes/durante/después de guardar; cierre forzado y corte abrupto. Debe conservarse un pendiente verificable o bloquearse la operación hasta conciliar. Cambiar el test que actualmente exige borrado silencioso.

### F03 — P1 en uso multiventana: el almacenamiento de pendientes colisiona

**Tipo:** limitación documentada con impacto funcional presente.

**Localización:** `DpapiPendingStore.cs:13–24,42`; `ApiClient.cs:31,71–89`; `ShellViewModel.cs:100–107`.

La ruta solo incluye puerto y rol. Dos clientes `main` del mismo usuario Windows y servidor usan el mismo archivo. El semáforo del cliente es una protección dentro de cada instancia, no entre procesos.

Una ventana puede sobrescribir el pendiente de otra; al completar una petición también puede borrar el archivo que corresponde a la otra. Además, reutilizar el puerto para otra instalación puede recuperar un comando de un negocio anterior: el sobre no incluye identidad estable del servidor, empresa, centro o usuario. El hecho de volver a autenticar un rol no identifica la instalación original del comando.

**Corrección propuesta:** identificar instalación, ámbito, usuario, dispositivo y slot durable; impedir instancias incompatibles mediante bloqueo entre procesos o usar almacenamiento concurrente con registros por clave. Borrar únicamente el comando confirmado, no un archivo genérico del rol. Verificar identidad de servidor y ámbito antes de habilitar reenvío. No basta cambiar el nombre por uno aleatorio si después no se sabe recuperar el pendiente.

**Aceptación:** dos clientes del mismo rol guardan y resuelven pendientes independientes; cerrar uno no elimina el del otro; un pendiente no se reenvía automáticamente al reemplazar el servidor por otra instalación en el mismo puerto.

### F04 — P2: el cliente da por definitiva cualquier respuesta 403/404/409/422

**Tipo:** debilidad de protocolo; escenarios de concurrencia/infraestructura requieren reproducción.

**Localización:** `ApiClient.cs:98–118`; `Program.cs:72–86`; `PostgresStore.cs:49–58`.

Se borra el pendiente antes de validar el cuerpo y el código de error. El servidor utiliza 409 tanto para rechazos del dominio como para conflictos y esperas de almacenamiento. Un error al esperar el bloqueo de un comando que otro proceso está ejecutando no demuestra que ese otro proceso nunca vaya a confirmar la operación original.

**Corrección propuesta:** distinguir rechazo definitivo reconocido de respuesta inválida, fallo transitorio y estado desconocido. Una respuesta de rechazo debería identificar el comando y explicar su resultado; no basarse solo en un código HTTP. Conservar la clave durante conflictos transitorios y consultar el estado autorizado antes de cerrar la incertidumbre.

**Aceptación:** 409 de versión conocido, 409 `storage_conflict`, respuesta HTML con 403/404, JSON inválido y dos reintentos concurrentes mientras una petición sigue pendiente. Verificar no duplicación y no pérdida de evidencia.

### F05 — P1 operativo: reconocer una alergia no equivale a revisar un plato ya listo

**Tipo:** riesgo de diseño del flujo, no incumplimiento oculto del protocolo documentado.

**Localización:** `Restrictions.cs:25–47,68–75`; `DiningService.cs:62–75`; `AllowedActions.cs:33–53`; `docs/native/D3.2-guest-restrictions.md`.

La declaración durante trabajo activo bloquea validar/servir/disparar, lo cual es un avance importante. Sin embargo, `AcknowledgeRestrictions()` solamente retira un booleano del servicio. No cambia la validación de un pase previamente `Ready` ni registra una decisión sobre cada preparación afectada. El acuse agrupa todos los cambios y todos los puestos de cocina comparten actualmente el rol.

**Escenario:** pase listo; se declara una restricción relevante; cocina reconoce que vio el aviso; el pase anterior sigue listo y vuelve a ser servible sin una nueva decisión explícita sobre esa elaboración.

**Corrección propuesta:** separar «aviso visto» de «elaboraciones revisadas/autorizadas». Registrar revisión de restricciones, preparaciones afectadas, responsable y decisión —no afecta, adaptar o rehacer—. Invalidar o suspender la autorización de salida anterior hasta esa revisión. Mantener trazabilidad y el control humano; el software no debe afirmar que un plato es seguro simplemente por pulsar acuse.

**Aceptación:** restricción añadida con pase listo; dos cambios consecutivos; dos estaciones afectadas; retirada de restricción; reconexión con aviso pendiente y acción iniciada desde una pantalla obsoleta. El `expectedVersion` ya protege frente a cambios simultáneos del agregado; no sustituye el significado de una validación de elaboración.

### F06 — P1 antes de LAN: identidades y transporte siguen siendo de laboratorio

**Tipo:** limitación explícita, no prueba de un ataque remoto actual.

**Localización:** `Program.cs:29–69,89`; `RealtimeEvents.cs:9–17`; `ApiClient.cs:45–59`; `Realtime.cs:19–28`.

Hay tres claves compartidas por rol y actores de auditoría `lab-main`, `lab-service` y `lab-kitchen`. No hay sesiones por persona, caducidad/revocación individual, emparejamiento de dispositivos o permisos por estación. El servidor escucha HTTP en `127.0.0.1` y el cliente rechaza otras direcciones. No es correcto extrapolar esta seguridad a tablets conectadas por Wi-Fi.

**Corrección propuesta:** mantener loopback hasta implantar usuario + dispositivo + ámbito, permisos explícitos, revocación de sesión y conexión, TLS local y configuración de origen. No publicar al router ni eliminar validaciones TLS para facilitar las pruebas. Las identidades durables del cliente deben coordinarse con F03.

Para la futura PWA, la autenticación de SignalR necesita diseño específico: las APIs WebSocket/SSE del navegador no permiten utilizar los encabezados arbitrarios igual que el cliente .NET. Deben considerarse cookies seguras o tokens efímeros con tratamiento limitado al hub y exclusión de registros. No trasladar las claves estáticas del laboratorio a URLs.

**Aceptación:** rol sala sin accesos de caja por HTTP ni SignalR; estación sin privilegios de otra; dispositivo revocado; token caducado; reconexión; origen no autorizado; ningún secreto en logs. Conocer la URL o elegir un perfil no otorga permisos.

### F07 — P2: cuentas cerradas anticipadamente y créditos no tienen un recorrido completo

**Tipo:** limitación funcional del corte, no fallo de aritmética.

**Localización:** `SettlementAccount.cs:37–89`; `AllowedActions.cs:69–84`; `schema.sql:24–31`; `CheckoutViewModel.cs:92–112`.

Una cuenta equilibrada se puede cerrar mientras sigue el servicio. Una vez cerrada, no admite nuevos cargos. El esquema permite una sola cuenta por servicio y no hay circuito de cuenta adicional o reapertura auditada. Esto deja sin recorrido una consumición posterior a ese cierre anticipado.

Los pagos superiores al total o la anulación de un cargo ya pagado pueden generar crédito. Se conserva correctamente ese crédito, pero no existe comando de devolución/aplicación; el cierre exige saldo y crédito cero.

**Corrección propuesta:** definir claramente pagada frente a cerrada. Mantener prepagos en una cuenta abierta mientras puedan llegar extras, o soportar nuevas liquidaciones vinculadas al mismo servicio con un modelo explícito. Añadir devolución/corrección auditada, sin borrar pagos o inventar consumos para equilibrar.

**Aceptación:** pago anticipado seguido de bebida; intento de cerrar antes del fin; cobro excesivo; anulación después de pagar; devolución parcial y conciliación de cantidades.

### F08 — P2: versiones y documentos mezclan estados distintos

**Localización:** `.github/workflows/dotnet-desktop.yml:37–42`; `Costina.Desktop.csproj`; `Costina.Server.csproj`; `Program.cs:/health`; `docs/STATUS.md`.

El escritorio muestra `0.6.0-d3.3`; el servidor se identifica como `0.5.0-d3.2`; el manifiesto generado sigue declarando `0.2.0-d1.2` y los artefactos conservan nombres D1.2. Tener versiones de componentes distintas puede ser intencionado; usar una versión fija antigua para el manifiesto del paquete sí dificulta identificar la entrega.

STATUS comienza con el avance nativo correcto pero mantiene secciones antiguas de bloqueos y «prueba siguiente» del legado. Están separadas parcialmente por títulos, aunque un colaborador puede confundirlas con el estado activo.

**Corrección propuesta:** versión de paquete, versiones de componentes, versión de contrato y commit derivados de una fuente verificable; compatibilidad explícita al conectar. Separar material legado de instrucciones activas. Actualizar artefactos y manifiestos, no solo el título WPF.

## 5. Aspectos adicionales de mantenimiento

### Tiempo real

La separación `ops`/`fin` es adecuada en el servidor de un único ámbito actual. No es ya un hub multiempresa: si un proceso llega a servir varias empresas, los grupos deberán incluir y autorizar ese ámbito.

La clasificación económica se basa en prefijos. Recomiendo una audiencia explícita por tipo de evento y fallo cerrado para eventos nuevos, con pruebas que impidan enviar un nuevo tipo económico a operaciones.

`Unit.Board()` obtiene primero las ocupaciones y luego lee servicio y ocupación por cada fila. Cada aviso dispara una relectura global; caja puede añadir consultas de catálogo y cuentas. Esto justifica medir y optimizar el patrón N+1 y agrupar avisos, no afirmar sin medir que ocho mesas saturarán el sistema.

El endpoint `/health` comprueba la versión del esquema, no la salud del publicador, edad de la cola, espacio en disco o última copia. Debe existir diagnóstico separado y accesible solo a personal autorizado. La publicación aceptada por SignalR no es un acuse humano de cocina.

### Dominio y datos

El catálogo nativo actual es básico (`ProductDefinition`: id, nombre, presentación, precio y activo). Los fixtures separan copa y botella como artículos de prueba; no modelan todavía añadas, propiedad de existencias, conversiones logísticas o stock compartido. No hay que confundir preparación de identificadores de empresa con gestión multiempresa completa.

No conviene cargar datos reales basándose en fixtures de laboratorio ni escribir con los dos backends sobre las mismas tablas. Las restricciones nuevas se guardan como campos aditivos de snapshot con versión 1. El lector estricto de campos exige comprobar explícitamente qué ocurre al volver a un binario anterior: rollback de ejecutable no equivale a rollback de esquema/payload.

### Cadena de suministro y GitHub

Los workflows inspeccionados limitan `contents` a lectura y no persisten las credenciales de checkout: buena base. Mantienen acciones por etiqueta de versión y no he localizado `packages.lock.json` en la carpeta nativa. Añadiría bloqueo de dependencias, revisión de avisos de vulnerabilidades, firmas o atestaciones de publicación y política de revisión de cambios del instalador. No se afirma que haya una CVE explotable en las versiones instaladas: no se ha realizado un barrido completo de dependencias e historial de secretos.

La API de ramas marca `main` y `develop` con `protected:false`. Conviene configurar o verificar reglas efectivas de protección y checks obligatorios; esta observación no sustituye revisar todos los rulesets de la cuenta.

### Instalación y continuidad

PR #23 sigue fuera de `develop`. En la última ejecución consultada para su cabeza, el workflow específico del instalador `35064484646` terminó **cancelado**. No puede contarse como validación completa del paquete. Un instalador basado en la rama D1.3 anterior tampoco demuestra que incluya D2/D3.

No se han verificado aquí servicios Windows instalados con privilegios mínimos, arranque sin sesión, actualización en una máquina limpia, dispositivos Android/iOS, backups restaurados ni failover. Siguen siendo puertas de entrada a piloto, no razones para hacer otra reescritura.

## 6. Matriz de funcionamiento real

| Área | Evidencia actual | Lo que falta |
|---|---|---|
| Windows nativo | WPF compilado/renderizado; núcleo y API .NET | Ensayo completo de la versión integrada en un Windows representativo |
| Comedor y pases | Estado, acciones y preparaciones; dominio separado de pagos | Corregir F01; completar excepciones y validar UX en servicio |
| Tiempo real | Outbox y SignalR; 9 comprobaciones existentes | Carga física, degradación, identidades y adaptación web |
| Restricciones | Estructura por comensal, proyección y acuse | Revisión de elaboraciones ya listas y protocolo por responsables |
| Cuenta/Caja | Catálogo servidor, consumos y pago operativo | Reaperturas/cuentas adicionales, devoluciones, cierre seguro |
| Recuperación de orden | Persistencia previa al envío y DPAPI | Corrupción, bloqueo de archivo, instancias simultáneas y ámbito |
| Tablets/móviles | Dirección de arquitectura documentada | PWA, TLS, identidad y pruebas Android/iPad/iPhone |
| Stock/PIM/multiempresa | Fronteras y plan; catálogo de ensayo | Movimientos, propiedad, variantes, conversiones y tarifas completas |
| WooCommerce/TheFork | Planificación | Accesos confirmados y conectores implementados/conciliados |
| Recuperación del sistema | Reinicios de proceso en tests | Backup/restauración de máquina y continuidad real |

## 7. Orden recomendado de trabajo

1. Corregir F01 y F02; añadir regresiones que fallen con el código actual y pasen con la corrección.
2. Cerrar F03/F04: identidad durable de cada operación y semántica de resultado confirmado/rechazado/desconocido.
3. Revisar con cocina el flujo F05 y traducirlo a decisiones explícitas y tests.
4. Implementar identidad de usuario/dispositivo y TLS sin quitar antes la contención de loopback.
5. Actualizar versionado/documentación y estabilizar el instalador sobre el mismo commit integrado que pasa CI.
6. Probar la versión completa en Windows limpio; restaurar una copia en otro equipo; ensayar red con Windows, Android e iOS.
7. Después ampliar catálogo, stock y conectores con los mismos controles de datos e idempotencia.

No marcar estos puntos como completados por actualizar documentación. Cada uno requiere evidencia de una ejecución adecuada a su alcance.

## 8. Fuentes y trazabilidad

Todas las referencias de código anteriores son relativas a `Rivadesa/Costi-a` en `ed715a4927dee6bf04571839b82ac9279ce9d469`.

- PR #33: cola durable integrada; documenta también el slot compartido entre clientes del mismo rol/puerto.
- `docs/STATUS.md`: integración nativa, congelación del legado y límites de instalación.
- Workflows y artefactos identificados en la sección 2.
- Microsoft Learn, **File.WriteAllBytes**: el archivo existente se trunca/sobrescribe. Contraste técnico de F02.
- Microsoft Learn, **SignalR authentication and authorization**, ASP.NET Core 10: limitaciones de encabezados en WebSockets/SSE del navegador y uso de tokens en el transporte. Contraste técnico de F06.
- Microsoft Learn, **DpapiDataProtector.Scope**: protección por credenciales de usuario; no es aislamiento respecto de cualquier proceso que ya corre con las mismas credenciales.

El documento de visión técnica del 15/09/2026 se refería al commit `297fa59`. Es un antecedente de requisitos, no evidencia del estado actual. Esta revisión no conserva como pendientes cosas que el código nuevo ya ha implementado, ni atribuye a `develop` funciones que permanecen en ramas sin integrar.
