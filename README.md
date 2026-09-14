# Costi-a / Hospitality OS

Plataforma modular para restauración gastronómica, iniciada con Retiro da Costiña / AÑITSOC SL y diseñada como producto exportable.

## Objetivo V1A

Coordinar un servicio gastronómico por pases entre sala y cocina con mínima fricción y sin depender de Internet exterior.

## Arquitectura acordada

- **Servidor local-primary:** Laravel + PostgreSQL + Redis.
- **Escritorio Windows:** Tauri 2 + Vue 3/Vite.
- **Comanderos / KDS:** Vue PWA o Tauri según hardware.
- **Backoffice:** Filament.
- **Realtime:** WebSockets en producto.
- **Cloud:** réplica, backup, monitorización e integraciones; no autoridad simétrica durante V1A.
- **Persistencia de eventos:** Transactional Outbox; no event sourcing completo.
- **Fiscalidad:** frontera diseñada desde V1A, implementación SIF/VERI*FACTU en V1B.

El desarrollo activo se realiza en la rama `develop`; `main` se reserva para cortes estables.
