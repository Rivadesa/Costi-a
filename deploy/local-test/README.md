# Servidor local real de pruebas

Laravel + PostgreSQL + Redis, persistente. **Solo pruebas en un PC; no producción ni datos de clientes.** Requiere Docker con Compose y puerto local 8000 libre. El cliente sigue siendo la aplicación Windows; Docker aloja su servidor, no reemplaza la aplicación de escritorio.

## Primera instalación

Desde la raíz del repositorio:

```sh
docker compose -f deploy/local-test/docker-compose.yml build backend
docker compose -f deploy/local-test/docker-compose.yml up -d postgres redis
docker compose -f deploy/local-test/docker-compose.yml run --rm backend init
docker compose -f deploy/local-test/docker-compose.yml up -d backend
```

`init` crea el esquema e inicializa un entorno vacío. Repetirlo sobre la demo existente no repone usuarios, contraseñas, precios o configuración. No sirve para reparar ni reiniciar datos.

## Uso normal

```sh
docker compose -f deploy/local-test/docker-compose.yml down
docker compose -f deploy/local-test/docker-compose.yml up -d
```

No usar `down -v`: elimina la base de datos de esta instalación. El arranque normal no ejecuta migraciones ni semillas. Para actualizar esquema, detener backend, hacer backup y ejecutar explícitamente `run --rm backend migrate` antes de arrancar la nueva versión.

## Conectar el cliente

En Configuración del terminal, seleccionar **Equipo principal**, API `http://127.0.0.1:8000/api/v1`. Usar el formulario normal de acceso: `demo@hospitality.local` / `demo1234`. No pulsar “modo demo”: ese modo almacena otra simulación independiente dentro del cliente.

El servidor fija tenant/empresa/local de Retiro Demo. Incluye 8 mesas, estaciones, menú de 7 pases, aguas, copas, botellas, cafés y tarifa Restaurante.

Con un cliente que incluya el corte de catálogo: Configuración → Catálogo del restaurante permite gestionar artículos. Cuenta/Caja los consulta desde `/checkout/catalog`; seguimiento y KDS no reciben precios.

## Límites

Puerto publicado solo en 127.0.0.1, credenciales conocidas y servidor HTTP de desarrollo. No exponer a otras redes. Perfiles de pantalla no son autorización de dispositivo; RBAC de servidor sí valida permisos del usuario. Cloud, fiscalidad, backups automáticos, instalador del servidor y prueba física LAN pendientes.

Detalles y criterios: `docs/LOCAL_STACK_READINESS.md`, `docs/CATALOG_ADMINISTRATION.md` y `docs/STATUS.md`.
