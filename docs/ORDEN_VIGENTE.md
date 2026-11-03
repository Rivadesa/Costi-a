# Orden vigente del trabajo (desde el 03-11-2026)

Una sola página con lo que se hace y en qué orden. La aprobó el promotor el 03-11 («Adelante con el orden del plan»). Manda sobre cualquier orden anterior (el del 23-09, el README del 13-10 y los traspasos del 14-10 y 15-10 que ponían las reservas primero). El detalle de cada fase, sus cortes y las decisiones pendientes están en `docs/plans/2026-11-03-plan-por-fases.md`; el estado de cada corte, en `docs/STATUS.md`.

## Qué queremos

Dejar Verial para todo y cobrar con este programa. Un producto para varios negocios; el primer cliente es Costiña, con dos empresas: Retiro (el restaurante) y AÑITSOC SL (almacén, ventas y stock). Reservas propias para el restaurante y las villas. Datáfono no integrado, tickets impresos y facturas completas. La carta de vinos online y la tienda de costinagourmet.com se alimentan del programa.

## El orden

| Fase | Qué | Estado |
|---|---|---|
| **F0** Abrir con Verial cobrando | 0.1 alergia tardía con sonido y marca en cocina (H34); 0.2 ronda y mesa a medio abrir guardadas en el móvil, «Atrás» baja un nivel (H01); 0.3 esta página y los papeles al día. Sin código: la prueba de 30 minutos con aparatos reales, la puesta en marcha y los procedimientos escritos | 0.1 y 0.2 entregados el 03-11; 0.3 es este documento |
| **F1** Varias empresas | Empresa por credencial, avisos por empresa, ficha de empresa, usuarios en pantalla, la empresa en el PC, catálogo común, precios e IVA por empresa | Pendiente de las decisiones 2 a 4 del plan |
| **F2** Caja | IVA y formas de pago, anular e invitar, apertura y cierre, cierre del día, clientes, impresora de tickets, traspasar, PIN, venta sin mesa, TPV de mostrador | Se construye; no cobra de verdad hasta acabar la F3 |
| **F3** Facturas y VERI*FACTU | Series y ticket, factura completa, rectificativas, dividir la cuenta; la parte fiscal según la decisión 9 (proveedor o vía propia) | Decisión fiscal a mediados de diciembre |
| **F4** Stock, compras y escandallos | Ficha de vino, almacenes, las ventas descuentan, proveedores y compras, recuentos y traslados, combinados, escandallo sencillo | |
| **F5** Carta de vinos sin Verial | Consulta de solo lectura en la red del local | |
| **F6** Apagar Verial (día D) | Informes, exportación al gestor, tienda web, cupos, bonos vendidos, traspaso desde Verial, ensayo de una semana | Día D: finales de marzo en el mejor caso; mayo, lo realista |
| **F7** Reservas propias | 7A sin internet (villas y restaurante por teléfono); 7B con internet (dominio, carta pública, reserva desde la web, señal, TheFork y CoverManager) | Después del día D |
| **F8** Mejoras y otros clientes | E4f, arreglos rápidos, E4g, los cortes grandes de usabilidad que pida la prueba, nombre del negocio, titular y licencia, instalador firmado, impresora USB y comandas impresas, PIN del camarero | Verano |

## Las reglas

- **Un bloque cada vez:** no se abre una fase sin cerrar la anterior y hacer su prueba corta con aparatos reales.
- **Un corte por PR** desde `develop`, con plan antes de codificar y permiso del promotor para programar, subir y mezclar (puede darlo por fase). **Tamaño M como máximo** (500 a 1.000 líneas); si crece, se parte.
- **Lo nuevo va al final de la cola**, salvo que impida abrir.
- **Documentos cortos.** Cada corte trae su documento en `docs/native/` con capturas y guion, su párrafo en `dotnet/AGENTS.md` y su entrada en `docs/STATUS.md`.
- **Regla legal:** un ticket o una factura emitidos de verdad tienen que cumplir el RD 1007/2023 (VERI*FACTU o no VERI*FACTU). Hasta acabar la F3 cobra Verial. AÑITSOC SL tendría que cumplir el 1-1-2027 (a confirmar con el gestor).
- **Lo que no se toca:** la instalación real de un PC (servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089), nunca como administrador; `main`; la incidencia #25; las #26, #27 y #28 no se cierran.

## Lo que ya no vale

- Las exclusiones del 14-09 («fuera de alcance»: motor de reservas, documentos fiscales, VERI*FACTU, stock, bonos, compras) en `docs/PRODUCT_SCOPE.md`, `docs/BACKLOG.md`, `docs/AI_CONTEXT.md` y `docs/ROADMAP.md`: eran de la primera versión (V1A). Desde el 03-11 todo eso tiene su fase. Esos documentos remiten aquí.
- El orden del 23-09 (E3 → E4 → E8 reservas → E6 stock → E7 tienda → E5 usuarios → caja → fiscal → informes) y los traspasos que lo repetían: queda sustituido por las fases de arriba.
- «Hito 7» (caja) y «Hito 8» (fiscal) del README: son la F2 y la F3.

## Lo que espera al promotor

Las decisiones 2 a 11 del plan (AÑITSOC en el mismo servidor, usuarios en las dos empresas, de quién son los vinos, villas y teléfono, bonos, día D de una vez o por partes, titular del programa, fiscal, datáfono e impresora) y las preguntas para el gestor y para Verial, empezando por el 1-1-2027. Y la prueba de 30 minutos con aparatos reales, con sus 9 hojas en blanco.
