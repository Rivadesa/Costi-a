> **Transición D0, 2026-09-15:** la arquitectura objetivo está aceptada en [ADR-008](adr/ADR-008-windows-native-transition.md): .NET/ASP.NET Core + WPF + PostgreSQL + Vue PWA; sin Docker obligatorio en el restaurante. El runtime ejecutable de referencia sigue siendo PHP hasta D1. [ADR-009](adr/ADR-009-independent-lifecycle.md) sustituye cualquier estado de servicio acoplado a pago/cierre descrito abajo. El resto de este documento conserva el diseño previo como referencia; no autoriza volver a Laravel/Tauri como destino ni poner D0 en producción. Consultar [D0](phases/D0.md) y [STATUS](STATUS.md).

# AI / Developer Context

Este documento permite a otra IA o desarrollador incorporarse al proyecto sin leer el historial de conversación original.

## 1. Qué estamos construyendo

Costi-a / Hospitality OS es una plataforma modular para restauración gastronómica y negocios relacionados. El primer cliente real es **Retiro da Costiña**; existe además una segunda sociedad, **AÑITSOC SL**, dedicada a gourmet/vinos y ecommerce.

El objetivo comercial es convertir el desarrollo en un producto exportable, no en software a medida de un único restaurante.

## 2. Caso operativo inicial

Retiro trabaja con menú degustación cerrado y aproximadamente 8 mesas. Cada servicio tiene un número configurable de pases. Sala decide manualmente cuándo disparar cada pase. Cocina necesita pantallas KDS por estación y una vista global del ritmo de todas las mesas.

El cliente paga un menú base y durante la experiencia se añaden bebidas/extras. Debe existir una cuenta provisional separada de la fiscalidad. Las anulaciones deben desaparecer del flujo operativo normal pero conservar auditoría.

Las restricciones alimentarias se modelan por comensal, no como texto global de la mesa. Una preparación puede adaptarse solo para PAX 2 sin afectar a PAX 1/3.

## 3. Flujo V1A

```text
reserva/importación manual
→ llegada
→ abrir servicio de mesa
→ confirmar pax
→ restricciones
→ asignar menú
→ enviar pase
→ enrutar preparaciones a estaciones
→ cada estación prepara y marca listo
→ chef/pase valida el pase
→ sala marca servido
→ sala decide cuándo enviar el siguiente
→ añadir bebidas/extras durante todo el servicio
→ revisar cuenta provisional
→ registrar cobro operativo
→ cerrar servicio
```

## 4. Flexibilidad obligatoria

El menú es una plantilla, no una cárcel. Durante un servicio autorizado debe ser posible:

- saltar un pase con motivo;
- sustituir una preparación;
- añadir un pase extraordinario;
- adaptar por comensal;
- pausar ritmo;
- cambiar mesa;
- anular un consumo con trazabilidad.

Las excepciones modifican la instancia del servicio, nunca la plantilla maestra del menú.

## 5. Estados simplificados

### Pase

`PENDING → FIRED → PREPARING → READY → SERVED`

`EATING` se infiere desde `SERVED` hasta el disparo del siguiente pase; no requiere pulsación manual.

Estados excepcionales: `SKIPPED`, `CANCELLED`.

### Preparación

Cada preparación por estación tiene estado propio. Un pase no puede considerarse listo mientras queden preparaciones obligatorias pendientes.

### Servicio

Estados orientativos: `PREPARED`, `OPEN`, `IN_SERVICE`, `PAUSED`, `PAYMENT_PENDING`, `PAID`, `CLOSED`, `CANCELLED`.

## 6. Arquitectura cerrada

### Servidor local

- Laravel (aplicación/API local)
- PostgreSQL (autoridad de datos de operación)
- Redis (colas/cache/realtime cuando aplique)
- Transactional Outbox para integración y réplica

### Clientes

- Vue 3 + Vite
- Tauri 2 para aplicación Windows
- PWA para comanderos/KDS cuando sea más adecuado
- Filament para administración/backoffice

### Cloud

Durante V1A el cloud NO es un segundo master. El establecimiento es `local-primary`; cloud recibe réplica/backup/reporting y posteriormente gestiona integraciones externas.

No introducir CRDT, multi-master ni event sourcing completo sin un ADR que demuestre necesidad real.

## 7. Principios críticos

1. Operación local debe funcionar aunque Internet exterior falle.
2. Un PC cliente roto no debe implicar pérdida de datos: el servidor local es la autoridad.
3. Sala/KDS deben tener mínima fricción; una acción frecuente idealmente es un toque.
4. Alergias críticas nunca se comunican solo mediante color.
5. No confundir cuenta provisional con factura/factura simplificada.
6. Las anulaciones se auditan; no hay borrado destructivo silencioso.
7. Las integraciones externas nunca deben bloquear una operación de sala.
8. WooCommerce será canal, no maestro de producto.
9. Multiempresa se diseña desde core; automatización intercompany no pertenece a V1A.

## 8. Fases posteriores ya definidas

### V1B — Fiscalidad

Caja, series, documentos fiscales, rectificativas, SIF/VERI*FACTU. El modelo debe respetar desde V1A la separación Service / Account / Payment / FiscalDocument.

### V1C — PIM, bodega, stock, multiempresa

- catálogo maestro;
- vinos, añadas, formatos;
- ubicación física separada de propietario jurídico del stock;
- botella como unidad logística; contenido en ml solo para botella abierta;
- AÑITSOC y Retiro con stock compartido físicamente pero propiedad separada;
- transferencia intercompany manual/auditada inicialmente.

### V1D — Ecommerce y bonos

Dos WooCommerce: gourmet y bonos. Conectores asíncronos mediante webhook + inbox + cola + idempotencia + reconciliación. El ERP/PIM es la fuente maestra.

## 9. Qué NO hacer todavía

- motor de reservas completo;
- microservicios;
- CRDTs;
- cloud multi-master;
- contabilidad general;
- RRHH/nóminas;
- compras avanzadas;
- escandallos completos;
- automatización intercompany;
- IA predictiva en el flujo crítico.

## 10. Cómo tomar decisiones nuevas

Si una decisión:

- cambia arquitectura,
- introduce dependencia fuerte,
- altera modelo de datos central,
- modifica fiscalidad,
- afecta estrategia offline/sync,

debe documentarse como ADR antes o junto con la implementación.

Si una necesidad solo aparece en Retiro, primero preguntar: **¿puede convertirse en configuración reutilizable?** Si no, justificar explícitamente por qué pertenece al core.

## 11. Estado del repositorio

La rama activa es `develop`. `main` se reserva para cortes estables. Consultar `docs/STATUS.md` antes de empezar trabajo y actualizarlo al terminar una unidad significativa.

## 12. Prioridad actual

Convertir el kernel de dominio existente en aplicación V1A real:

1. Laravel real / application layer.
2. Migraciones PostgreSQL.
3. Repositorios Eloquent/DB.
4. API de servicios, pases y KDS.
5. Outbox transaccional.
6. Autenticación/roles mínimos.
7. Conectar Vue/Tauri a API local.
8. Primera prueba LAN con sala + KDS.
