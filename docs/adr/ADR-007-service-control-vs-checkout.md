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
