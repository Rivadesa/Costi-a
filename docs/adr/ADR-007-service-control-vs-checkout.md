# ADR-007 — Separación entre control de servicio y Cuenta / Caja

- Estado: **Aceptada**
- Fecha: 2026-09-14
- Alcance: V1A/V1B

## Contexto

Hospitality OS se usa desde varios tipos de terminal durante un servicio: equipo principal, puestos de sala/comanderos y KDS de cocina. El seguimiento de una mesa necesita información operativa de alta frecuencia, pero no necesita exponer su cuenta, importes o cobros.

Mezclar ambos contextos aumenta ruido visual, riesgo de errores y exposición innecesaria de información económica en terminales de servicio.

## Decisión

Se separan de forma explícita dos contextos de interfaz y API.

### Control de servicio

Está orientado a sala, maître y cocina. Puede mostrar:

- mesa y número de comensales;
- menú asignado, **sin precio**;
- pases y estado de cada pase;
- tiempos de servicio;
- preparaciones/KDS;
- alergias, intolerancias y adaptaciones;
- incidencias operativas.

No debe mostrar:

- cuenta provisional;
- precios del menú;
- importes de consumiciones;
- subtotal o saldo;
- pagos;
- acciones de cobro.

Los endpoints `/service-board` y `/services/{serviceId}` se consideran proyecciones operativas y no deben incluir datos financieros.

### Cuenta / Caja

Está orientada al software del **equipo principal**. Puede mostrar y gestionar:

- cuenta provisional;
- menú y precio snapshot aplicado;
- consumiciones añadidas desde el catálogo del restaurante;
- cantidad y precio snapshot de cada artículo;
- anulaciones con motivo;
- pagos;
- saldo pendiente;
- cierre del servicio una vez pagado.

Los endpoints económicos viven bajo `/checkout/...`.

## Catálogo operativo

Las consumiciones de la cuenta provisional no se introducen como texto y precio libre. Se seleccionan desde el catálogo activo del restaurante y la tarifa correspondiente a empresa/localización.

Al añadir una consumición se guarda un snapshot del nombre y precio aplicados, además de `product_id` y `price_list_id`. Cambios posteriores en el catálogo o tarifa no deben reescribir cuentas históricas.

## Consecuencias

- Los comanderos y pantallas de seguimiento no exponen datos económicos.
- El equipo principal dispone de un módulo separado `Cuenta / Caja`.
- Los permisos financieros siguen siendo obligatorios, pero la separación no depende solo de permisos: también está reflejada en rutas, proyecciones y UI.
- V1A incorpora un catálogo operativo mínimo. El PIM completo, stock, vinos/añadas y WooCommerce siguen pospuestos a V1C.
- Cualquier nueva IA o desarrollador debe respetar esta frontera y no volver a añadir subtotal, pagos o cuenta a las proyecciones de servicio.

## Apartado C3b (2026-10-21): «Lo que lleva la mesa» no es la cuenta provisional

**Contexto.** En C3b el camarero pide bebidas y extras desde el comandero («Bodega y bebidas») y desde la ficha de la mesa del puesto principal («Añadir consumición»). Para repetir una ronda y para enterarse de lo que se anula en Caja necesita ver **qué lleva ya la mesa**. Esa lista sale de las líneas de la cuenta, y «Control de servicio» no debe mostrar la cuenta provisional.

**Decisión.** El control de servicio **puede mostrar** «Lo que lleva la mesa»: las bebidas y los extras ya enviados (también los apuntados en Caja), con:
- nombre del producto y formato («Copa», «Botella 0.75 L»; la presentación por defecto no se escribe);
- cantidad;
- comensal, o toda la mesa;
- hora en que se apuntó;
- «Anulado en Caja» cuando se anuló.

**Sigue sin mostrar**: importes, precios unitarios, totales, subtotal, saldo, tarifa, pagos, estado de la cuenta ni acciones de cobro. Tampoco lleva la línea del menú ni los platos, que ya se ven en los pases. Por eso no es la cuenta provisional: es una lista operativa, como los pases, y los importes solo se ven en Caja, también para el responsable en el puesto principal.

**Cómo se garantiza:**
- es una **proyección propia** (`consumptions` en cada entrada de `GET /dining/board`), construida por una lectura del núcleo que solo extrae de la cuenta los campos sin dinero (`AccountLines`). Nunca se reutiliza `ChargeLine` ni `AccountView`, que llevan precio, total y tarifa;
- ninguna clave nueva lleva vocabulario de caja: la guarda de la PWA (`money-guard.ts`) se sigue aplicando a cada respuesta y `pwa_http.py` lo comprueba con la misma regla;
- el aviso de tiempo real `service.consumptions_changed` es operativo y no lleva datos: solo dice qué servicio cambió y cada equipo relee el tablero. Los avisos `account.*` y `payment.*` siguen llegando solo al principal;
- el selector de bebidas no pinta precios en ningún terminal: en el puesto lee `GET /catalog` (sin importes), nunca `/checkout/catalog`.

Lo que no cambia: cobrar, anular y cerrar siguen en Caja; las rutas económicas siguen bajo `/checkout/...` y solo para el principal.
