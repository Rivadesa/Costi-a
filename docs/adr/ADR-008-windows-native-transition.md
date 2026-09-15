# ADR-008 — Producto Windows nativo y transición controlada

- Estado: aceptada como arquitectura objetivo por indicación del usuario (2026-09-15).
- Implementación: D0 corrige el contrato de negocio en el backend de referencia existente; D1 implementará el primer corte .NET/WPF. No confundir decisión con software entregado.
- Sustituye ADR-002 y ADR-003 en la elección futura de runtime/cliente. Conserva ADR-001, ADR-004, ADR-006 y la separación de superficies de ADR-007.

## Decisión

Servidor C#/.NET 10 LTS / ASP.NET Core, PostgreSQL y puesto principal WPF. SignalR para notificaciones, recuperación autoritativa por API. Comanderos/KDS Android, iPad y teléfonos Android/iPhone con Vue + TypeScript/PWA y HTTPS. Un solo propietario de escritura por instalación.

Instalación completa (servidor + cliente en un Windows), servidor dedicado o puesto Windows adicional. Cerrar WPF no detiene el motor. Apagar el servidor sí interrumpe operación, salvo recuperación/conmutación ensayada. Docker, WSL, Redis y herramientas de desarrollo no serán requisitos de instalación del restaurante.

Esto NO equivale a ausencia de dependencias: instalador y mantenimiento incluyen runtime, PostgreSQL, certificados, actualizaciones, firma y credenciales propias. Publicar un EXE no satisface estos requisitos por sí solo.

## Transición

1. D0: eliminar el acoplamiento pago/servicio con pruebas y migración conservadora en la referencia PHP. Es una corrección acotada para no portar un defecto, no un nuevo compromiso con Laravel para el producto.
2. Mantener 0.1.1 como artefacto histórico. No sobrescribir sus binarios ni presentarlo como .NET.
3. D1: portar el circuito vertical a .NET, usar los contratos D0 como criterio, instalar en Windows limpio, guardar y recuperar PostgreSQL. Sin prometer el ERP completo.
4. Probar equivalencia de reglas y migración; PHP y .NET nunca escriben concurrentemente en las tablas de negocio. Corte con todos los escritores detenidos y restauración ensayada.
5. Reutilizar el cliente Vue adecuado para PWA; no volver a empaquetar una WebView como respuesta al requisito WPF.

## Límites permanentes

Cuenta/Caja es superficie independiente del equipo principal. Los datos económicos no se emiten en sala, KDS ni notificaciones sin autorización. El dispositivo y el usuario tienen identidades separadas; un selector local `main` no otorga permisos. Una cuenta provisional no es factura ni permite borrar ventas o registros fiscales.

Nube: integraciones y copias/proyecciones, no segundo maestro simétrico. Webhook + inbox/outbox + reintentos idempotentes + conciliación. TheFork condicionado al acceso y alcance autorizados; WooCommerce es canal, no maestro. Estas integraciones no se implementan en D0.

## Criterios D1, no cumplidos por este ADR

Instalador Windows sin Docker, motor como servicio con arranque automático, cliente WPF, una mesa/pase real persistente, diagnóstico, copia/restauración de ensayo, sin contraseñas fijas fuera de datos ficticios. Medir memoria, latencia y arranque; no estimar ligereza por el tamaño del ZIP.

## Fuentes de plataforma revisadas

- https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- Plan propuesto en PR #19; revisión aportada por el usuario, 2026-09-15. Este ADR resuelve la elección de stack; no valida afirmaciones de rendimiento no medidas.
