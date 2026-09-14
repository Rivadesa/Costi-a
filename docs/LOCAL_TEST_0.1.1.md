# Ensayo local 0.1.1 — aplicación Windows y servidor real

Este corte es para probar con datos ficticios en **un PC Windows de 64 bits**. No es el instalador comercial del servidor, no sustituye Verial y no emite facturas.

## Qué descargar

El paquete de cliente contiene el instalador/portable, con su manifiesto de versión, commit y SHA-256. El paquete de servidor contiene el código de la misma versión, `backend/`, `deploy/` y la documentación. Extraer todo el servidor en una carpeta propia. No copiar solo el CMD ni mezclar archivos de ZIPs de versiones distintas.

Requiere Docker Desktop operativo en modo contenedores Linux, con Docker Compose disponible. La primera construcción descarga dependencias y necesita Internet. No instalar Laravel, PHP o PostgreSQL manualmente en Windows para este procedimiento. El puerto local 8000 debe estar libre.

**No desactivar el antivirus ni abrir el puerto al exterior.** Los binarios de ensayo pueden estar sin firma; comprobar origen y manifiesto antes de ejecutarlos. Un hash verifica coincidencia con el archivo distribuido, no sustituye una firma de editor.

## Primera instalación del servidor

Abrir `deploy/local-test/test-server.cmd` y elegir **I — Primera instalación**. También es posible invocarlo desde terminal:

```bat
deploy\local-test\test-server.cmd init
```

El lanzador verifica que Docker/Compose responden, construye el backend, inicia PostgreSQL y Redis, ejecuta la inicialización explícita y arranca la API. Solo anuncia disponibilidad si responde el endpoint de comprobación.

La inicialización rechaza una base ocupada por otros tenants. Sobre la demo ya existente no repone precios, usuarios ni configuración. No usarla como reparación/reset ni como actualizador de producción.

## Conectar la aplicación

Abrir Hospitality OS **0.1.1**, pulsar **Conectar al servidor local de pruebas** y usar el formulario normal de acceso. El selector fija `http://127.0.0.1:8000/api/v1`, perfil Equipo principal y empresa/local vacíos para que los resuelva el servidor.

Credenciales ficticias de esta instalación: `demo@hospitality.local` / `demo1234`.

No pulsar **Entrar en modo demo** para esta prueba: ese botón utiliza una simulación independiente en el cliente. El nuevo selector evita conservar `demo-company` y `demo-location` cuando se vuelve al servidor real. No introduce la contraseña automáticamente ni crea datos.

## Guion de aceptación manual

| Prueba | Resultado que comprobar |
| --- | --- |
| Identificación | La pantalla muestra 0.1.1 y, en builds de CI, el commit abreviado |
| Catálogo | Crear agua o vino con código único; copa/botella, formato y precio |
| Cuenta/Caja | Seleccionar mesa y añadir el artículo desde el catálogo, sin introducir precio libre |
| Cambio de tarifa | Modificar precio/nombre del artículo; el consumo ya cargado conserva los originales |
| Servicio y KDS | No aparecen cuenta, saldo, precio del menú ni botones de pago |
| Cocina | Enviar pase, completar sus partidas, validar y servir; no validar antes de terminar lo obligatorio |
| Reinicio del cliente | Cerrar y volver a abrir; iniciar sesión y recuperar datos del servidor |
| Reinicio del servidor | Parar/arrancar con el lanzador; la cuenta y el catálogo siguen presentes |
| Internet exterior | Tras la instalación, ensayar sin Internet conservando servidor y Docker en marcha; es distinto de apagar servidor/Wi-Fi |

Registrar versión/commit, pasos exactos y resultado. No marcar una prueba como realizada por haber leído esta tabla. Utilizar solo alergias, personas y consumiciones ficticias.

## Uso diario de la prueba

```bat
deploy\local-test\test-server.cmd start
deploy\local-test\test-server.cmd status
deploy\local-test\test-server.cmd stop
```

`start` no construye imágenes, no descarga, no migra ni carga semillas. `stop` detiene contenedores sin borrar sus volúmenes. La persistencia local de Docker **no es un backup** y no protege ante pérdida del disco/equipo.

No usar `docker compose down -v`, `migrate:fresh` ni `db:seed` para arrancar o corregir este ensayo: pueden eliminar o sobrescribir datos.

## Si algo falla

Guardar el mensaje y consultar `status`. Para los registros:

```bat
docker compose -f deploy\local-test\docker-compose.yml logs --no-color --tail=100 backend
```

No inicializar otra base, borrar volúmenes ni cambiar puertos a ciegas. Antes de migrar a otro corte, conservar los datos y seguir un procedimiento de actualización con copia/restauración verificada; el lanzador de ensayo no automatiza ese paso.

## Límites

Servidor HTTP de desarrollo limitado a loopback; autenticación de usuario, no emparejamiento seguro del puesto físico; permisos y perfiles diferenciados, pero el ensayo no demuestra una instalación de producción. No hay datáfono, facturas, bonos, inventario ni sincronización cloud. La matriz de estado verificable está en `docs/STATUS.md`.
