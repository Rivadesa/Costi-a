# V1A — Backlog actualizado tras PR #16

Fecha: 2026-09-14. Referencia de código: `312931949b2265a3d85b67c1fa1f827ead042484`. Las Issues son el trabajo ejecutable; `STATUS.md` recoge evidencia y límites. Este documento reemplaza las casillas antiguas que seguían mostrando como inexistentes módulos ya implementados.

## Base implementada; no reconstruir

| Bloque | Estado en este corte |
| --- | --- |
| Dominio | Entidades/estados, reconstitución sin eventos falsos, snapshots y pruebas del flujo de pases |
| Aplicación | Puertos, comandos/consultas, transacciones, idempotencia y outbox |
| Persistencia | Migraciones y repositorios PostgreSQL, pruebas de concurrencia/rollback |
| API | Login/permisos locales, configuración, apertura preparada, pases y proyecciones operativas |
| Cliente | Vue/Tauri, seguimiento, KDS y Cuenta/Caja separados |
| Catálogo | Alta/edición mínima de presentaciones y precios para la tarifa activa, snapshots de consumos |
| Ensayo | Servidor Docker loopback, inicialización explícita y reinicio conservando datos |
| Distribución | Código cliente 0.1.1 y pipeline de instalador/manifiesto; confirmar artefactos del commit antes de entregar |

Los tests automáticos de estos bloques no equivalen a cobertura exhaustiva ni validación de un servicio físico. Algunas excepciones previstas en dominio aún no tienen rutas/pantallas completas.

## P0 — Cerrar la prueba local reproducible

- [ ] Verificar y distribuir cliente/servidor del mismo commit, con manifiesto y hashes.
- [ ] Ejecutar en el Windows del usuario el guion `LOCAL_TEST_0.1.1.md` con datos ficticios.
- [ ] Registrar resultados reales del catálogo, cuenta separada, pases y conservación tras reinicio.
- [ ] No convertir fallos de instalación en resets de base ni usar seeds como reparación.

## P0 — Separación operativa y financiera interna

Issue [#17](https://github.com/Rivadesa/Costi-a/issues/17).

- [ ] Separar ritmo de servicio, ocupación de mesa y liquidación de cuenta sin reescribir historia.
- [ ] Un pago parcial/anticipado no debe bloquear preparaciones o siguientes pases.
- [ ] Fin de servicio/liberación/cobro deben tener acciones, permisos y eventos diferenciados.
- [ ] Migración explícita de estados existentes y regresiones PostgreSQL.
- [ ] Mantener datos monetarios fuera de todas las respuestas de seguimiento/KDS.

## P0 — Preparación de ensayo con varios equipos

- [ ] Identidad/emparejamiento y revocación de terminales; el perfil visual `main` no demuestra que sea el equipo autorizado.
- [ ] Credenciales por instalación, transporte local protegido y despliegue HTTP apropiado; no exponer la receta loopback con credenciales de demo.
- [ ] Ensayo real equipo principal / sala / KDS, con autorizaciones distintas.
- [ ] Validar dos acciones concurrentes, doble pulsación y cambio de selección durante una petición.
- [ ] Asegurar que los reintentos conservan comando, ámbito, destinatario e idempotency key.

## P1 — Fiabilidad cliente y tiempo real

- [ ] Persistencia durable de comandos pendientes a través de cierre/reapertura; la retención del formulario de catálogo actual no basta.
- [ ] Pendiente de confirmación no equivale a recibido por cocina.
- [ ] Publicador outbox/realtime y refresco autoritativo tras reconexión; no reemplazar integridad de PostgreSQL por Redis.
- [ ] Medir latencia con hardware real; probar corte WAN conservando LAN, y pérdida de Wi-Fi por separado.
- [ ] Test de protección frente a respuestas obsoletas al cambiar de mesa/cuenta.

## P1 — Configuración administrable

- [ ] Editores de zonas/mesas/capacidades, estaciones y menús/preparaciones.
- [ ] Historial/snapshots: una edición de plantilla no altera servicios existentes.
- [ ] Interfaz de usuarios/roles y auditoría.
- [ ] Gestión de categorías y tarifas (el catálogo actual selecciona las existentes).
- [ ] Completar las excepciones de sala/cocina priorizadas observando el servicio, no añadiendo estados por anticipado.

## P1 — Operación y distribución fiables

- [ ] Backup automatizado, almacenamiento externo y prueba de restauración; un volumen Docker no es backup.
- [ ] Actualización/rollback del servidor y migraciones con copia previa verificada.
- [ ] Dependencias frontend/Rust bloqueadas y proceso de distribución mantenible.
- [ ] Firma del cliente Windows, instalación/actualización/desinstalación en hardware real.
- [ ] Monitorización local y diagnóstico sin revelar credenciales ni datos de clientes.

## Fuera de este corte

Facturación/SIF, bonos, PIM avanzado, stock/vinos, WooCommerce, motor de reservas, intercompany, hotel, BI e IA. No abrirlos para evitar corregir los bloqueos del núcleo.

## Criterio de entrada al primer servicio piloto

No basta con CI verde. Requiere ensayo físico con los puestos reales, permisos y restricciones inequívocos, operación sin WAN, recuperación/reintentos comprobados, backup/restauración y procedimiento alternativo acordado. Mantener Verial como sistema fiscal mientras no exista sustitución validada.
