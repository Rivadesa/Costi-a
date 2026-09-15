# D1.1 — persistencia y API nativa, corte de ingeniería

Fecha: 2026-09-15. Base: PR #20, commit f93e9c352b03e5b4d89a223f70dca4b322db48fe. Implementación en feat/d1-postgres-api. Consultar CI de esta rama para resultados; escribir una prueba no equivale a ejecutarla.

## Qué cambia

Se añade Costina.Persistence (Npgsql) y Costina.Server (ASP.NET Core/Kestrel, soporte de alojamiento como servicio Windows), sin modificar Laravel/Tauri ni ninguna instalación del restaurante. Las tres entidades D0 tienen snapshots tipados y restauración explícita que no llama comandos ni genera eventos. Los proyectos nuevos se compilan por su csproj; el slnx D0 conserva su alcance.

El servidor exige COSTINA_LAB_MODE=true, base aislada terminada en _d1_lab/_d1_test y solo escucha 127.0.0.1. No se distribuye como instalación LAN ni se permite usar la base del legado. No es una nueva demo visual: son operaciones HTTP reales sobre PostgreSQL para validar la siguiente capa antes de conectarle WPF.

## Persistencia y decisiones acotadas

Tres tablas de estado actual independientes (services, occupancies, accounts), claves de ámbito/relación y estados/versiones relacionales; payloads tipados/versionados JSONB para el contenido de cada agregado. No es event sourcing ni un único documento que mezcle sala/caja. Las lecturas rechazan estados/snapshots incompatibles. Antes de generalizar PIM/informes se deberá revisar la normalización de colecciones; no presentar esta base de laboratorio como esquema ERP final.

Npgsql directo con SQL parametrizado se usa en este corte para comprobar transacciones, bloqueo optimista e idempotencia explícitos. No se incorpora EF Core en este adaptador aún; no cambia las reglas de negocio. Fuente oficial de paquetes: nuget.org. Versiones directas fijadas; lock transitorio de dependencias pendiente antes de una distribución comercial.

La apertura guarda servicio, ocupación y cuenta en una transacción. Un índice único parcial impide dos ocupaciones activas de la misma mesa/ámbito. Cada mutación exige expectedVersion y una Idempotency-Key; la respuesta original se guarda como texto, preservando bytes/orden al reproducirla. La huella incluye método, ruta y cuerpo exacto: el reintento debe conservar esos bytes. El ámbito y actor vienen del servidor, no de cabeceras elegidas por el cliente.

Estado, respuesta idempotente, outbox y auditoría se confirman juntos. Un fallo revierte todo. El outbox se guarda, pero todavía NO tiene publicador SignalR ni cloud; ninguna pantalla debe interpretar ese registro como entrega a cocina.

## API /api/native/v1 — distinta de la API Laravel

| Ruta | Función |
| --- | --- |
| GET /board | Servicio y ocupación activos, sin economía |
| GET /services/{id} | DTO operativo y versión |
| POST /services | Abrir con tableId, pax, menuId de configuración persistida |
| POST /services/{id}/commands/{action} | start, fire-next, preparation-start, preparation-ready, ready, serve, skip, pause, resume, complete, cancel-unstarted |
| GET /checkout/services/{id} | Cuenta y versión; solo principal |
| POST /checkout/services/{id}/commands/{action} | add-product, payment, void-charge, close; solo principal |
| POST /occupancy/{id}/release | Liberación explícita tras terminar comedor; no liquida la cuenta |

Comandos operativos: expectedVersion, courseId/itemId/reason cuando corresponda. Cuenta: expectedVersion y datos de operación; add-product recibe productId y quantity, nunca precio libre. GET /health comprueba la disponibilidad de PostgreSQL y esquema. Respuestas: 200, 401, 403, 404, 409, 422 o 503. Un 503 no acredita que la operación no se haya guardado: reconsultar/reintentar la misma clave.

## Arranque técnico

Dependencias de ingeniería: SDK fijado por dotnet/global.json y PostgreSQL. El binario autocontenido no necesita SDK/.NET instalado manualmente, pero necesita PostgreSQL configurado. No tiene interfaz, asistente ni instalador de servicios.

Variables obligatorias: COSTINA_LAB_MODE=true; COSTINA_DB (cadena Npgsql); COSTINA_TENANT; COSTINA_COMPANY; COSTINA_LOCATION. Para atender HTTP: COSTINA_KEY_MAIN, COSTINA_KEY_SERVICE y COSTINA_KEY_KITCHEN, distintas, aleatorias y de al menos 32 caracteres. No se guardan secretos en GitHub. Puerto opcional COSTINA_PORT (5088).

Desde dotnet/: `dotnet run --project src/Costina.Server -- init-lab` inicializa explícitamente fixtures ficticios (o ejecutar el binario con init-lab). Arranque normal sin argumentos: comprueba esquema y nunca migra/resemilla. Repetir init-lab no repone datos existentes. No existe operación reset ni se borran históricos.

Credenciales de laboratorio por rol NO equivalen al sistema final de usuarios, dispositivos emparejados o permisos por estación. Sin HTTPS/LAN, apps móviles, alergias completas, WPF, instalador, stock, bonos, fiscalidad ni backups en este corte. El pago es registro técnico, no ejecución de datáfono ni documento fiscal. Mantener #17 abierto hasta migración/integración y validación de producto; D1 aislado no corrige la aplicación 0.1.1 instalada.

## Comprobaciones

D0: runner previo, 52 escenarios. D1 snapshots: 16 comprobaciones. HTTP: 11 escenarios con procesos reales y PostgreSQL, incluidos pago anticipado y reinicio, liberación con deuda conservada, reapertura, reintento concurrente, versión obsoleta, apertura simultánea, permisos, ámbito ajeno, inicialización repetida y fallo inyectado después de escribir estado. Los números son alcance previsto hasta leer resultados ejecutados.

CI Linux utiliza un contenedor PostgreSQL descartable únicamente como infraestructura de pruebas; el runtime de la aplicación no invoca Docker. CI Windows compila/prueba dominio y snapshots y publica el servidor win-x64 autocontenido; no demuestra instalación como servicio Windows ni PostgreSQL nativo funcionando en un PC físico.

Próximo corte: endurecer autenticación/configuración, conectar WPF por esta API, empaquetar servicio+PostgreSQL y probar instalación Windows limpia. No abrir nuevos módulos ERP hasta tener ese circuito operativo.

Referencias: https://www.npgsql.org/doc/basic-usage.html ; https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
