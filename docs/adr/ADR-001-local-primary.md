# ADR-001 — Local-primary + cloud-replica

**Estado:** Aprobado

El servidor local es autoridad para la operación del restaurante. Cloud no compite con él como nodo multi-master.

## Consecuencias

- Sala y cocina siguen operativas sin Internet.
- Los eventos se replican a cloud cuando hay conectividad.
- Integraciones externas no bloquean el servicio local.
- Se evita introducir CRDTs y resolución de conflictos generalizada en V1.
