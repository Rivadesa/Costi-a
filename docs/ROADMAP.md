# Roadmap

> **Desde el 03-11-2026 el orden vigente es el de `docs/ORDEN_VIGENTE.md`** (el plan por fases `docs/plans/2026-11-03-plan-por-fases.md`, aprobado por el promotor). Lo que aquí figuraba como «fuera de alcance» o «todavía no» (reservas, documentos fiscales, VERI*FACTU, stock, bonos, compras) tiene ya su fase. Este documento se conserva como está, por su historia.

Hecho hasta hoy: V0 y V1A (sala y cocina, con menús, carta y bodega), salvo el cobro, que va en la F2. V1B es la F3; V1C, la F1 y la F4; V1D, la F6 y la F7.

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
