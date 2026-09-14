# Arquitectura de instalación

## Objetivo final

Hospitality OS no debe depender de Internet para el servicio del restaurante. El despliegue objetivo separa el **servidor operativo local** de los **clientes de escritorio/comanderos/KDS**.

```text
Internet / cloud (no crítico para servicio)
          │
          ▼
┌─────────────────────────────┐
│ Servidor local Hospitality  │
│ Laravel + PostgreSQL + Redis│
│ API + WebSocket + workers   │
└──────────────┬──────────────┘
               │ LAN
       ┌───────┼────────┐
       ▼       ▼        ▼
  Desktop    Tablet     KDS
 Tauri/Vue   PWA/Vue   PWA/Tauri
```

## Instalador de escritorio

El cliente Windows se empaqueta con Tauri 2 y NSIS.

Responsabilidades del cliente:

- interfaz de sala/maître;
- KDS cuando el hardware sea Windows;
- configuración de URL/descubrimiento del servidor local;
- cache visual y comandos pendientes de corta duración;
- integración futura con periféricos de terminal cuando corresponda.

El cliente **no** es la base de datos del restaurante.

## Servidor local

En producción deberá instalarse en un equipo dedicado o suficientemente aislado de los puestos de trabajo.

Componentes previstos:

- PHP/Laravel;
- PostgreSQL;
- Redis;
- worker de colas;
- servicio realtime/WebSocket;
- supervisor/servicio del sistema;
- backups locales;
- réplica cloud cuando haya conectividad.

La primera prueba de escritorio no incluye todavía este instalador de servidor.

## Fases de instalabilidad

### I0 — instalador demo

Estado actual en desarrollo.

- `.exe` Windows.
- UI real Tauri/Vue.
- modo demo embebido.
- sin servidor.
- pensado para validación UX/operativa.

### I1 — cliente + servidor de desarrollo

- instalador Windows del cliente;
- servidor Laravel/PostgreSQL arrancado manualmente o mediante scripts;
- cliente apunta a IP local;
- prueba PC + segundo terminal/KDS.

### I2 — servidor local reproducible

- instalador/provisionador de servidor;
- creación DB, migraciones y seed inicial;
- servicios arrancan automáticamente;
- backup y health checks;
- configuración del establecimiento.

### I3 — despliegue comercial

- firma digital de ejecutables;
- actualizaciones controladas;
- recuperación/rollback;
- cloud pairing;
- monitorización;
- instalador parametrizable por tenant/local;
- documentación de soporte.

## Descubrimiento del servidor

Durante desarrollo el terminal acepta una URL manual como:

`http://192.168.1.20:8000/api/v1`

Antes de despliegue comercial debe evitarse depender de recordar IPs. Opciones a evaluar:

1. hostname local estable (`hospitality.local`);
2. descubrimiento mDNS/Bonjour;
3. código de emparejamiento mostrado por el servidor;
4. configuración distribuida por el servidor/cloud durante instalación.

No se debe introducir un mecanismo de descubrimiento complejo hasta validar el funcionamiento LAN con IP/hostname estable.

## Actualizaciones

El servicio del restaurante no debe quedar bloqueado por una actualización del cliente. Reglas deseadas:

- actualizaciones fuera de servicio o explícitamente aceptadas;
- compatibilidad de API durante una ventana de versiones;
- el servidor local es la autoridad de versión mínima/máxima soportada;
- posibilidad de mantener el cliente actual si Internet está caído.

## Firma

Los artefactos iniciales de desarrollo no están firmados. Windows puede mostrar SmartScreen/editor desconocido. Para instalación comercial habrá que adquirir y gestionar un certificado de firma de código y proteger su clave fuera del repositorio.
