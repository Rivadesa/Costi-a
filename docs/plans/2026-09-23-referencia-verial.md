# Referencia: Verial Restaurante (el programa que usa hoy Retiro da Costiña)

Fecha: 2026-09-23. Fuente: https://www.verial.es/Soluciones-Principales/Gesti%C3%B3n-de-Restaurantes-y-Pubs.html (texto y capturas de pantalla leídos con el navegador integrado; no se ha usado el programa). Sirve para comparar el alcance del ERP de Costiña con lo que el promotor y el restaurante ya conocen, y para decidir qué patrones conviene conservar (menos formación) y cuáles superar.

## Qué incluye Verial Restaurante de serie

| Área Verial | Qué es (según su web y capturas) | Equivalente en Costiña | Estado |
| --- | --- | --- | --- |
| Terminal punto de venta | Pantalla táctil de venta: lista de líneas de la mesa a la izquierda, familias en botones de colores y artículos con foto a la derecha, teclado numérico, botones de invitación, cliente, cambiar mesa, imprimir, transferir, abrir cajón. Modos mesas / barra / acción directa | Cuenta / Caja (puesto principal) + comandero PWA | Cuenta por mesa hecha; **TPV táctil de barra y acción directa no existen** (Hito 7) |
| Gestión de cartas | Grupos de carta (Café e infusiones, Cervezas, Combinados, Vinos copas, Vinos botellas, Menú…) con artículos, sección para impresora de cocina (1.º, 2.º, 3.º plato), precios con impuestos, imagen | **E3 catálogo + E4 oferta configurable** | Pendiente |
| Control de camareros y comensales | El camarero se identifica con PIN y declara comensales al abrir la mesa | Usuarios y puestos emparejados; pax al abrir | Hecho (sin PIN en pantalla táctil) |
| Traspaso de consumiciones | Mover líneas entre mesas o áreas de venta, por unidades o todo | No existe | Hito 7 |
| Facturación | Factura simplificada y completa, series, cliente, método de cobro, tarifa, almacén | Hito 8 (fiscal) | Pendiente |
| Arqueos | Apertura, importe esperado por método de pago, diferencia, operaciones del cajón | Hito 7 (caja) | Pendiente |
| Televenta | Pedidos telefónicos a domicilio con repartidor | Fuera del alcance actual (módulo delivery futuro) | — |
| Libro de reservas | Calendario semanal con reservas por color y área de venta | **E8 reservas** (restaurante, villas, online y telefónicas) | Pendiente |
| Compras y almacén | Facturas de proveedor con líneas, precios, descuentos, stock por almacén; inventarios; entradas y salidas; movimientos | **E6 stock, compras y proveedores** | Pendiente |
| Alta rápida de artículos | Rejilla para dar de alta muchos artículos a la vez (nombre, coste, precio) con perfil, impuestos y categoría | Importación desde Verial y alta masiva en E3 | Pendiente |
| Comandero y visor de cocina | Comandero en móvil Android (áreas de venta, plano de mesas numeradas por colores, carta por familias) y visor de cocina en tablet con tarjetas por mesa, tiempos y estados por color | Comandero y KDS por estación en PWA | **Hecho y mejor** (por pases, alergias, estaciones, sin precios en sala) |
| Informes | Centro de informes por bloques: contabilidad, artículos (carta, categorías, almacenes, tarifas), clientes, proveedores, almacén | Pendiente (tras caja) | — |
| Plano visual del local | Mesas ubicadas sobre un plano | Diferido tras E3 (corte corto) | Pendiente |
| Escandallos, orden de servicio de platos, estado de preparación, cócteles y combinados, invitaciones | Parte de gestión de cartas y TPV | Escandallo: E6; orden y estado: hecho en Dining; combinados: E4; invitaciones: Hito 7 | Parcial |

**Módulos adicionales de Verial** (de pago): facturación electrónica, contabilidad, centro de costes, lectura de DNI (OCR), copia de seguridad, remesas, firma manuscrita, carta con código QR (carta digital en el móvil del cliente con pedido), Verial Delivery.

## Qué aprender de Verial (lo que el restaurante ya sabe usar)

- **Familias en botones grandes de colores y artículos con foto** en el TPV: reduce la formación a minutos. Lo adoptamos en el TPV de barra y acción directa (Hito 7) y en el comandero cuando llegue la carta libre (E4).
- **Alta rápida en rejilla** para catálogo: útil para la importación y para negocios sin Verial.
- **Arqueo con importe esperado por método de pago y diferencia**: es el mínimo que espera un gerente (Hito 7).
- **Libro de reservas en calendario por colores y área de venta**: base de E8.
- **Documentos de compra con stock por almacén e inventarios**: modelo de E6.

## Qué hacemos distinto (y por qué)

- **Copia de seguridad y restauración verificada de serie**, no como módulo aparte (ya hecho, D5.4).
- **Sala y cocina sin precios** y órdenes que nunca se duplican (ADR-007, D3.4): en Verial el comandero muestra precios.
- **Menú por pases con alergias, revisión y estaciones** (Dining) frente al "orden de servicio 1.º, 2.º, 3.º".
- **Oferta configurable** (carta, menú del día, degustación, combinados, experiencias) definida en el ERP y ejecutada por el módulo (E4), en lugar de una única "carta".
- **Módulos activables por instalación** y una sola base de datos por negocio (ADR-012).
- **Reservas de restaurante y alojamiento** en el mismo libro, con las vendidas online (E8).
- **Sincronización con la tienda web** (WooCommerce o PrestaShop) como parte del catálogo (E7), no como conector externo.

## Consecuencias para el plan (Hito 6 en adelante)

El orden acordado se mantiene: E3 catálogo con importación de Verial → E4 oferta configurable (definición en el ERP; ejecución en sala y cocina) → E8 reservas → E6 stock, compras y proveedores → E7 sincronización web → E5 usuarios con pantalla → Hito 7 caja y TPV táctil (barra, acción directa, traspasos, invitaciones, arqueos) → Hito 8 fiscal (facturas y VERI*FACTU) → informes. Verial no debe copiarse en su forma (pantallas densas de escritorio de 2015), sí en la ergonomía de sala y en la cobertura funcional que el restaurante espera no perder.
