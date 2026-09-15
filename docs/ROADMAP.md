> **Transición D0, 2026-09-15:** la arquitectura objetivo está aceptada en [ADR-008](adr/ADR-008-windows-native-transition.md): .NET/ASP.NET Core + WPF + PostgreSQL + Vue PWA; sin Docker obligatorio en el restaurante. El runtime ejecutable de referencia sigue siendo PHP hasta D1. [ADR-009](adr/ADR-009-independent-lifecycle.md) sustituye cualquier estado de servicio acoplado a pago/cierre descrito abajo. El resto de este documento conserva el diseño previo como referencia; no autoriza volver a Laravel/Tauri como destino ni poner D0 en producción. Consultar [D0](phases/D0.md) y [STATUS](STATUS.md).

# Roadmap

## V0 — Spike técnico

Objetivo: demostrar envío de pase sala → cocina → listo → servido en tiempo real dentro de red local.

Criterios:
- Un toque para enviar pase.
- KDS recibe el pase sin recargar.
- Cocina cambia ENVIADO → PREPARANDO → LISTO.
- Sala recibe LISTO.
- Sala marca SERVIDO.
- Pantalla global refleja el estado.
- Funciona sin acceso a Internet mientras la LAN y el servidor local sigan activos.

## V1A — Servicio gastronómico

- Tenant/empresa/centro mínimos.
- Usuarios, roles y permisos.
- Salas y mesas configurables.
- TableService.
- Comensales y restricciones estructuradas.
- Menús y pases configurables.
- Excepciones por servicio: saltar, sustituir, añadir pase, pausar.
- Comandero PWA.
- KDS por estaciones.
- Pantalla global de servicio.
- Bebidas y extras.
- Cuenta provisional.
- Cobro básico.
- Auditoría.
- Adaptador fiscal temporal.

## V1B — Sustitución fiscal

- Caja.
- Series.
- Documentos fiscales.
- Rectificativas/anulaciones regladas.
- SIF / VERI*FACTU.

## V1C — PIM, inventario y multiempresa

- Catálogo/PIM.
- Vinos, añadas y formatos.
- Ubicaciones y bodega.
- Stock físico y propietario.
- Transferencias manuales intercompany.

## V1D — Ecommerce y bonos

- WooCommerce Gourmet.
- WooCommerce Bonos.
- Cola, inbox, idempotencia y reconciliación.
- Motor propio de bonos/prepagos.
