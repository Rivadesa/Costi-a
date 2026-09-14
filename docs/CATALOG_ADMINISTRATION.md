# Catálogo operativo editable — V1A

## Alcance de este corte

El equipo principal dispone de `/catalog`, una pantalla Vue de configuración conectada a Laravel/PostgreSQL. No modifica el adaptador de demo. Es una interfaz operativa mínima, **no una implantación de Filament ni el PIM avanzado**.

Permite crear/editar artículos, elegir una categoría existente, presentación (unidad, copa, botella, ración, servicio), formato y precio en céntimos. Puede retirar/reincorporar una presentación a la tarifa activa sin borrarla ni desactivar el producto compartido para todas las empresas. No crea categorías o tarifas: esas pantallas siguen pendientes.

En V1A una presentación vendible corresponde a una fila `products`. Copa y botella pueden ser dos filas con códigos diferentes; no representan todavía existencias separadas. El futuro PIM normalizará vino/añada/formato/envase y vinculará estas referencias mediante migración, no mediante IDs externos.

## Contratos y autorización

- `GET /configuration`: mesas, menús sin precio y estaciones. Ningún artículo de venta ni dato económico.
- `GET /checkout/catalog`: categorías/artículos activos para Cuenta/Caja; requiere `payment.record` siguiendo la autorización existente de esa pantalla.
- `GET /admin/catalog`: catálogo editable, incluida disponibilidad; requiere `catalog.manage`.
- `POST /admin/catalog/products`: crear/actualizar, requiere `catalog.manage` e `Idempotency-Key`.

El selector local `main/service/kds` es UX, **no una prueba de identidad de un dispositivo**. El backend exige el permiso de usuario; sigue pendiente emparejar/autorizar físicamente el equipo principal. El permiso `catalog.manage` autoriza editar descripciones del catálogo compartido del tenant: no debe concederse como si solo autorizara precios de una sociedad.

La tarifa se resuelve en servidor con el mismo criterio que caja: por tenant/empresa, priorizando la tarifa predeterminada específica del local frente a la general. Se exige el `price_list_id` que vio el cliente para detectar cambios de tarifa. La categoría debe pertenecer al tenant; editar un artículo requiere que exista en esa tarifa. Los IDs recibidos nunca sustituyen el contexto autenticado.

## Transacciones, revisiones y reintentos

Una mutación administrativa realiza: bloqueo de administración por tenant → lectura de idempotencia → bloqueo del producto/precio → comparación de revisión → escritura → auditoría → outbox → respuesta idempotente → commit. Un fallo revierte todo. El bloqueo de baja frecuencia no está en el camino habitual de los pases ni exige Redis para la integridad.

Cada artículo devuelve `revision`, hash de la descripción, presentación, precio, disponibilidad y una versión monótona de producto. La versión se incrementa incluso cuando se modifica solo el precio, evitando conflictos ABA aunque dos cambios ocurran dentro del mismo segundo. Una actualización sin la revisión actual devuelve 409; la UI obliga a recargar, no pisa silenciosamente cambios de otro usuario. La misma clave/payload devuelve el resultado original; reutilizar la clave para otra acción se rechaza.

La UI conserva el comando y la clave ante una respuesta incierta mientras el formulario está abierto. **No es todavía una cola duradera que sobreviva al cierre del programa.** El backend conserva la idempotencia en PostgreSQL. El código único y la revisión reducen duplicados/reenvíos accidentales, pero no sustituyen ese futuro trabajo de reconexión.

## Histórico

Al añadir una consumición se congela nombre + presentación + formato en la descripción de la cuenta, además de precio y referencias al producto/tarifa. Cambiar después nombre, formato, precio o disponibilidad no reescribe esa línea. La migración 000007 amplía `consumptions.name` a 320 caracteres sin truncar registros; su reversión se niega si existen descripciones que no caben en 180. Añade también `products.catalog_version` para el control de concurrencia.

Los importes nuevos en la UI se convierten a céntimos desde texto decimal validado, no multiplicando un decimal de coma flotante. EUR únicamente. No se implementan todavía desglose fiscal, impuestos ni factura.

## Pruebas

`CatalogAdministrationApiTest` cubre permisos de sala, inexistencia de precios en configuración y comandos operativos, alta idempotente, auditoría/outbox único, revisión obsoleta, snapshot histórico, retirada de venta, categorías de otro tenant, tarifa ajena, código repetido y precio negativo. `tests/money.test.mjs` comprueba 14 entradas válidas/no válidas.

El flujo `deploy/local-test/smoke.py` usa HTTP real, cambia un precio mediante la API de administración y comprueba supervivencia de la cuenta/configuración/reintento tras recrear contenedores. No equivale a un ensayo físico en Windows o una restauración de backup. Consultar STATUS para distinguir pruebas implementadas de ejecuciones verificadas.

## Pendiente después de este corte

Configuración visual de mesas, menús/pases y estaciones; altas de categorías/tarifas; cuentas de usuario y dispositivos autorizados; WebSockets y reconexión duradera; separar por completo estado económico y estado operativo; prueba física LAN y recuperación de backups; empaquetado productivo de servidor. Inventario, WooCommerce, bonos y fiscalidad propia permanecen fuera de este corte.
