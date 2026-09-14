# ADR-007 — Autenticación local y concurrencia optimista

## Estado

Aceptado para V1A.

## Contexto

El restaurante debe seguir operando sin Internet y varios terminales pueden modificar el mismo servicio de mesa en paralelo. No podemos depender de un proveedor de identidad cloud ni aceptar sobrescrituras silenciosas entre comandero, TPV, KDS y pantalla de control.

## Decisión

1. La API local usa autenticación Bearer con tokens opacos. El token completo solo se entrega al cliente; PostgreSQL almacena su hash.
2. El login valida usuario, contraseña, tenant, empresa y localización autorizados.
3. Los permisos se resuelven en servidor a partir de roles y ámbito empresa/local. La interfaz puede ocultar botones, pero nunca sustituye la autorización de backend.
4. `tenant_id`, `company_id`, `location_id` y `user_id` efectivos se derivan de la identidad autenticada, no se confían al cliente.
5. `TableService` usa una columna `version` y persistencia con compare-and-swap. Una escritura basada en una versión antigua lanza un conflicto de concurrencia recuperable.
6. Mutación de agregado, idempotencia y outbox comparten la misma transacción PostgreSQL. Si cualquiera falla, todo se revierte.
7. La operación no requiere conexión a Internet.

## Consecuencias

- Dos terminales no pueden aplicar `last write wins` de forma silenciosa.
- El cliente debe refrescar el servicio y reintentar cuando reciba `409 concurrency_conflict`.
- Los tokens pueden revocarse localmente y caducan.
- Los permisos deben mantenerse como catálogo versionado y probado.
- La futura sincronización cloud no se convierte en autoridad sobre el servicio activo local.
