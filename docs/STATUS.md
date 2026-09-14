# Estado de desarrollo

## Implementado y probado

### Dominio V1A
- Servicio de mesa.
- Menú como plantilla y snapshot por servicio.
- Comensales por posición.
- Alergias/intolerancias/preferencias estructuradas.
- Pases secuenciales.
- Preparaciones independientes por estación y por comensal.
- Validación de pase únicamente con preparaciones obligatorias listas.
- Salto de pase con motivo.
- Pase extra sin modificar la plantilla.
- Consumos adicionales.
- Anulación trazable de consumos.
- Pago operativo.
- Cierre de servicio.
- Eventos de dominio.

### Proyecciones
- Cola KDS por estación.
- Restricciones críticas trasladadas al item del comensal afectado.
- Vista global de servicio.

### Persistencia diseñada
- Esquema PostgreSQL V1A.
- Outbox transaccional.
- Auditoría.
- Separación explícita entre operación y futura fiscalidad.

### Interfaces
- Spike multi-mesa/multi-estación ejecutable sin Internet exterior.
- Scaffold Vue 3 + Vite.
- Scaffold Tauri 2 para escritorio Windows.
- Contrato OpenAPI V1A.

## Pendiente inmediato

1. Instalar dependencias Laravel/Tauri en un entorno con acceso a paquetes.
2. Generar proyecto Laravel real y migraciones desde `v1a-schema.sql`.
3. Implementar repositorios Eloquent y transacciones + outbox.
4. Implementar endpoints OpenAPI.
5. Conectar las vistas Vue al API Laravel real.
6. Añadir autenticación/roles mínimos.
7. Primera prueba en LAN con PC + tablet + KDS.
