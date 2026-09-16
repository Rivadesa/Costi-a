# D2 — Realtime: publicador del outbox y suscripción del cliente

Fecha: 2026-09-16. Rama `feat/realtime-outbox-signalr` sobre `develop` (`2d9e904`). Issue #24. Consultar el CI de la PR para resultados ejecutados; escribir una prueba no equivale a ejecutarla.

## Qué cambia

- `OutboxPublisher` (Costina.Persistence): releva los eventos de `native_d1.outbox` con `published_at IS NULL` **del ámbito del servidor**, en orden `occurred_at`, y los marca tras emitirlos. Publica ANTES de marcar: una caída entre ambos produce un duplicado acotado, nunca una pérdida (at-least-once). Un fallo del sink revierte el marcado del lote y se reintenta.
- `EventsHub` + `HubEventSink` + `OutboxPublisherService` (Costina.Server): hub SignalR en `/api/native/v1/events` autenticado por el mismo middleware y metadatos de endpoint; bucle supervisado con backoff que no muere ni escribe secretos en el log.
- **Notificación fina**: solo `{id, type, aggregateId, occurredAt}`. Nunca payload, importes ni datos de cuenta. El estado autoritativo se relee siempre por los GET existentes con versión.
- **Separación económica también en el canal** (ADR-007): grupos `ops` y `fin`. `main` pertenece a ambos; `service`/`kitchen` solo a `ops`. Los tipos `account.*`/`payment.*` van solo a `fin`: sala y cocina no reciben ni el tipo del evento.
- `RealtimeSubscription` (Costina.Client): reconexión automática, estados `connected/reconnecting/closed`, y contrato explícito de **relectura autoritativa tras cada reconexión**. Duplicados inofensivos por construcción: los avisos solo disparan la misma relectura idempotente del botón Actualizar.
- WPF: avisos y reconexiones encolan un refresco con coalescencia (no interrumpen una operación en curso); si el canal falla, la aplicación degrada al refresco manual con mensaje claro. El botón Actualizar se conserva como respaldo.
- Esquema: índice parcial `outbox_unpublished` (por ámbito y `occurred_at`, solo pendientes). Añadirlo a una base existente = repetir `init-lab` una vez (es no-op sobre los datos).

## Qué NO cambia

Contrato HTTP de comandos/lecturas, dominio, autenticación (siguen las claves de laboratorio), alcance loopback y restricciones D1. Ninguna pantalla interpreta un evento del outbox como entrega confirmada a cocina: el aviso solo provoca relectura. Sin cola durable de cliente (Hito 2, #25), sin identidad de dispositivos (#26), sin LAN/TLS (#27), sin PWA (#28), sin cloud.

## Comprobaciones automáticas

`tests/Costina.RealtimeChecks` (job `postgres-http`):

- A1 orden y marcado de pendientes; A2 enrutado económico por tipo; A3 fallo de sink sin pérdida y reintento como duplicado acotado; A4 lo publicado no se repite.
- B1 apertura notifica a todos los roles sin refresco manual; B2 los avisos económicos llegan solo a `main`; B3 el outbox del ámbito queda drenado; B4 el aviso solo lleva identidad; B5 matar y rearrancar el servidor reconecta automáticamente y la relectura autoritativa responde con versión.

## Guion de prueba manual (Windows, para el promotor)

Requiere el motor D2 y PostgreSQL del ensayo D1.2 (mismas variables; repetir `Costina.Server.exe init-lab` una vez para el índice nuevo).

1. Abrir DOS clientes WPF en el mismo PC: uno con clave `main` y otro con clave `service`. Comprobar que ambos muestran "Tiempo real activo".
2. En el cliente `service`, abrir una mesa. El cliente `main` debe mostrarla SIN pulsar Actualizar.
3. En `main`, enviar un pase y marcar elaboraciones. El cliente `service` debe reflejar cada paso solo.
4. En `main`, pestaña Cuenta/Caja, registrar un pago de prueba. El cliente `service` no debe mostrar ningún aviso ni dato económico (su pestaña de cuenta no existe y su estado no cambia).
5. Cerrar el proceso del servidor. Ambos clientes deben pasar a "reconectando". Rearrancar el servidor (sin `init-lab`): deben volver a "activo" y mostrar el estado real sin duplicar nada.
6. Pulsar Actualizar manualmente en cualquier momento: debe seguir funcionando igual que antes.

Anotar resultado por paso y commit exacto probado.

## Límites

At-least-once con duplicado acotado al lote; no hay exactly-once ni cola durable en cliente. Si la reconexión automática agota sus reintentos, el cliente queda en "desconectado" y hay que reconectar manualmente. Publicador único por proceso servidor; varios servidores del mismo ámbito no están soportados (local-primary, ADR-001). Un run verde de CI no certifica LAN, servicio Windows ni dispositivos físicos.
