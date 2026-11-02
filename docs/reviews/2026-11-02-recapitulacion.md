# Costiña: dónde estamos (recapitulación a 02-11-2026)

Pedida por el promotor el 02-11 («el proyecto se está haciendo demasiado grande»). Hecha con cuatro lectores del repositorio y de GitHub y un sintetizador. La sesión principal comprobó las cifras que más pesan: el instalador, la Caja de ensayo, que la cuenta no guarda el IVA y la versión instalada.

## 1. Dónde estamos

- **Versión 0.46.2-c3e.2**, del 30-10. Costiña tiene cuatro piezas:
  - el **motor**, que es el programa del PC servidor y guarda todos los datos;
  - el **programa del PC** del encargado;
  - **Costiña Sala**, para los camareros;
  - **Costiña Cocina**, para las estaciones y el pase.

  Las dos últimas se abren en el navegador del móvil o de la tablet, sin instalar nada.
- **Lo que ya hace en sala y cocina:** abrir mesa con personas, menú y alergias; enviar pases; una comanda por estación; el pase valida y decide sobre las alergias; aviso de «listo para servir» y botón «Servido»; ronda de bebidas sin precios. Además hay catálogo (1.037 vinos, 1.017 con foto), menús, instalador de Windows y copia diaria.
- **Pruebas automáticas:** unas 1.282, todas en verde el 29-10.
- **Nada se ha probado con aparatos reales.** Las hojas de prueba física están en blanco. Nunca se ha usado en un iPhone, en un iPad ni con la wifi del local.
- **No cobra de verdad.** La Caja es un «registro de ensayo»: no hace factura ni ticket, no cierra caja, no imprime y no da informes. Para cobrar, Verial sigue siendo imprescindible.
- **Este PC tiene la 0.43.0-c3c** (22-10), cinco versiones por detrás.
- **Tus respuestas del 02-11** («Usa tus recomendaciones») están apuntadas en la sección 5c del informe de usabilidad, con el corte de cada una, y subidas a GitHub junto con esta recapitulación.

## 2. Lo hecho

Llamamos «corte» a cada entrega pequeña con su propio número de versión. Se han mezclado **54 cortes en 45 días**: 32 entre el 16 y el 24-09, ninguno entre el 25-09 y el 12-10, y 22 entre el 13 y el 30-10.

| Bloque | Cortes | Qué trajo |
|---|---|---|
| Motor y datos | 9 (D0–D4.3a, 16–17 sep) | Guardar sin perder ni duplicar órdenes, tiempo real, alergias por comensal con acuse de cocina, reabrir cuentas, usuarios, aparatos emparejados y permisos por puesto |
| PC del encargado | 10 (D1.2–C2b) | Programa de Windows. Con C1, C2 y C2b (14-15 oct), aspecto nuevo: Inicio, plano del Comedor, Caja, Catálogo, Menús, Salas y Dispositivos |
| Instalación | 9 (D5.1–D5.6, B.1, B.2, C2.1) | Instalador con su propia base de datos, servicio que arranca con Windows, copia diaria verificada y «conexión sencilla» por la wifi (sin cifrar, provisional) |
| Catálogo y menús | 12 (E1a–E4e) | Impuestos, familias, precios con fecha, importar la tienda, Cocina y Bodega, vinos por copa, menú degustación, menú con elección, carta, menú del día, alergias decididas antes de enviar |
| Sala y Cocina en móvil y tablet | 14 (D6.1–C3e.2) | **Costiña Sala:** «Mis mesas»; abrir mesa en 3 toques; enviar pase en 2; servir en 1. **Costiña Cocina:** comanda por estación, el pase ve la mesa entera, sonido. Android se conecta por QR; iPhone e iPad, con un código |

**Tamaño actual:**
- unas 51.000 líneas en uso: 21.099 del motor y el PC, 7.899 de las aplicaciones del móvil y 19.854 de pruebas;
- otras 18.553 líneas del sistema antiguo, congelado;
- la documentación son 142 documentos con 283.195 palabras.

## 3. Por qué crece

- **El orden que aprobaste el 23-09** tenía 9 bloques: E3 catálogo → E4 menús → E8 reservas → E6 stock → E7 tienda web → E5 usuarios → Hito 7 caja → Hito 8 fiscal → informes. Solo están hechos los dos primeros. Los otros 7 no tienen ni plan.
- **Desde entonces se han mezclado 25 cortes. Solo 3 eran de ese orden; los otros 22 se metieron por medio:**

  | Tipo | Cortes | Líneas |
  |---|---|---|
  | Rediseño y aspecto (C1, C2, C2b, C3a, C3c, C3d, C3e) | 7 | 23.023 |
  | Funciones pedidas después (A, B, E3.2, E4d, E3.3, C3b, E4e) | 7 | 11.796 |
  | Arreglos de fallos (B.1, E3.1, B.2, C2.1, D6.7, E3.3.1) | 6 | 1.932 |
  | Usabilidad (C3e.1, C3e.2) | 2 | 2.100 |

  En total, **el 92 % de las líneas nuevas desde el 23-09 fue a lo intercalado** (38.851 frente a 3.570).
- **«C3», el aspecto nuevo del móvil, iba a ser un solo corte.** Han sido 7, con 19.773 líneas. De los 4 cortes grandes que quedan, 3 vuelven a ser de Sala y Cocina.
- **Cada corte pesa más:** unas 486 líneas de media en los Hitos 3 a 5, frente a unas 2.825 en los C3.
- **Las reservas (E8) se han aplazado tres veces por escrito** (06-10, 14-10 y 15-10). Desde el 22-10 ningún documento las nombra.
- **La lista de pendientes ha pasado de 8 bloques a 15.**
- **El papeleo también crece.** Desde el 13-10, de 36 entregas, 13 son solo documentos. Cada plan nuevo tiene unas 1.000 líneas.
- **Tus respuestas de hoy cambian reglas ya escritas y añaden trabajo:** sonido en sala, guardar la ronda en el móvil, sonido en el pase y «Deshacer» en cocina.

## 4. Lo que queda

**Cómo leer los tamaños.** Son estimaciones comparando con cortes ya hechos (sin comprobar):
- S: menos de 400 líneas;
- M: de 500 a 1.000;
- L: de 1.200 a 2.500;
- XL: más de 2.700, o varios cortes.

Los últimos cortes han tardado cerca de un día cada uno.

### (a) Imprescindible para el primer servicio real, con Verial cobrando

1. **LO LEGAL.** Costiña no puede emitir factura simplificada y no cumple VERI*FACTU (registro encadenado de cada factura, código QR y declaración responsable del fabricante). Por eso Verial, u otro programa que cumpla, tiene que seguir cobrando.
   - Plazos vigentes: 1-1-2027 para sociedades y 1-7-2027 para el resto. Se ha anunciado otro aplazamiento a octubre de 2028 que no está en el BOE (sin comprobar: conocimiento general y prensa).
   - Al gestor: ¿la empresa es una sociedad? (si lo es, el plazo cae dentro de dos meses). ¿Usar la Caja de Costiña con importes al lado de Verial la convierte en programa de facturación?
   - A Verial: ¿su versión cumple VERI*FACTU? (sin comprobar).
   - Alérgenos: la información de los 14 alérgenos tiene que estar fuera de Costiña, porque Costiña no sabe qué alérgenos lleva cada plato.
2. **Actualizar este PC de la 0.43.0 a la 0.46.2.**
   - Con el comedor cerrado: no hay vuelta atrás salvo restaurar la copia previa.
   - El PC y todos los móviles tienen que estar en la misma estación de sala.
   - El instalador ya generado (`Costina-Setup-0.46.2-c3e.2.exe`, 8 de 8 comprobaciones) caduca el **04-11 a las 10:25**, hora peninsular. Después habrá que generarlo otra vez.
   - No es código.
3. **La prueba de 30 minutos con aparatos reales y los guiones físicos.**
   - La prueba: 2 o 3 camareros (al menos un Android y un iPhone), un cocinero, el pase, de 6 a 8 mesas y 15 tareas.
   - Los guiones: instalación y copias (D5.6), QR (D6.5), C3c, C3d, C3e y E4e.
   - Regla del informe: cualquier alergia perdida, asignada a otro comensal o con la gravedad equivocada se arregla antes de un servicio real.
   - La haces tú; no es código.
4. **Un corte solo de seguridad de alergias**, de tamaño **M–L**:
   - H34: una alergia anotada tarde llega a la estación sin sonido ni marca;
   - H01: al pulsar «Atrás» o recargar se pierde lo escrito al abrir la mesa;
   - más lo que salga de la prueba.
5. **Puesta en marcha en el local.**
   - El servidor: se recomienda un equipo dedicado, con SAI y cable.
   - Salas, mesas y menús reales.
   - Usuarios: hoy solo se crean por consola.
   - Bebidas: falta la exportación de Verial.
   - Copias con segundo destino y una restauración ensayada en otra máquina.
6. **Procedimientos escritos**, de tamaño **S**. No existe ninguno:
   - cómo pasa la cuenta de Costiña a Verial;
   - qué hacer si se cae el servidor o la wifi en pleno servicio;
   - cómo se numera a los comensales;
   - quién termina y libera las mesas.

### (b) Puede esperar

- **E4f, el menú del día en una pantalla** (**M**). Hoy renovar 3 pases de 3 platos cuesta unas 55 acciones. Sería lo primero después de abrir si el menú cambia de verdad cada día (sin comprobar: `AI_CONTEXT` habla de «menú degustación cerrado»).
- **El resto de arreglos rápidos de usabilidad**, unos 18 detalles (**L**, se puede partir).
- **Los 4 cortes grandes de usabilidad:**
  - «Sala: avisos que se ven» (**L–XL**);
  - «Pase con carga» (**L–XL**);
  - «Estación con volumen» (**L**);
  - «Cierre de mesa en el PC» (**L–XL**).

  Lo de la carga (H35, H50) hay que medirlo en la prueba: con unas 8 mesas quizá aguante.
- **E4c, combinados a precio fijo** (**L**, cambia la base de datos). **E4g, carta por familias y comanda de una vez** (**XL**).
- **Los bloques del 23-09:** E8 reservas (**XL**), E6 stock, compras y proveedores (**XL+**), E7 tienda web (**XL**), E5 usuarios y PIN de camarero (**L–XL**) e informes (**M–L**).
- **Solo para dejar Verial en el comedor:**
  - Hito 7, caja (**XL**): apertura y cierre, arqueo, turnos, traspasos, invitaciones y TPV de barra;
  - Hito 8, fiscal (**XL+**, con revisión legal externa). Hoy las líneas de la cuenta ni siquiera guardan el IVA. La alternativa es conectar un proveedor fiscal certificado;
  - **impresora**, que no está en ningún corte;
  - informe de cierre para el gestor.
- **El certificado con dominio propio** traerá https, avisos con el móvil bloqueado y la pantalla siempre encendida. No tiene tamaño estimado y espera a tu dominio.

## 5. Lo que depende de ti (de lo que más bloquea a lo que menos)

1. **Instalar la 0.46.2 y hacer la prueba de 30 minutos.** Tu respuesta 15 de hoy la pone antes de E4f. Mejor antes del 04-11, que es cuando caduca el instalador. Hay **9 hojas de prueba física, todas en blanco**: D4.3b, D5.6, D6.5, C3a, C3b, C3c, E4e, C3d y C3e. Sin ellas siguen abiertas en GitHub las incidencias #25 a #28.
2. **Hablar con el gestor y con Verial:** la forma jurídica y el plazo de VERI*FACTU; si la Caja de Costiña cuenta como facturación; si Verial cumple; y el IVA de los vinos en sala (este último no bloquea: se cambia en bloque).
3. **La exportación real de Verial con sus tarifas.** Sin ella no se puede cargar el catálogo real de bebidas.
4. **Elegir qué va después y dar su permiso**, porque cada corte necesita el suyo: E4f, el resto de arreglos, uno de los 4 cortes grandes, E4c o E4g.
5. **El dominio propio** para el certificado.
6. **El LICENSE.** El repositorio es público y no tiene licencia: hay que decidir el titular y si sigue público. Sin prisa: las licencias de uso (5 subdecisiones) y la firma del instalador (incidencia #12).
7. **Preguntas sueltas:**
   - el pie «Los importes solo se ven en Caja.», ¿se queda?
   - «Copa 15 cl»: ¿algún vino tiene dos tamaños de copa?
   - el aviso de mesa cerrada en el comandero, que aún no has visto;
   - la regla de numeración de comensales: hay una de partida y puedes escribir la tuya;
   - la hora de apertura de la mesa;
   - las 4 fotos de la tienda, que hay que volver a subir en JPEG.
8. **Las claves REST de WooCommerce.** Solo hacen falta para E7.
9. **105 decisiones pequeñas y 13 diferencias con la maqueta** (C3a a C3e y E4e). Unas 9 ya han quedado superadas. No bloquean nada.
10. **Decisiones antiguas, sin prisa:**
    - qué hacer con los aparatos tras restaurar una copia antigua;
    - cuántos datos se pueden perder y cuánto tiempo puede estar parado el sistema;
    - devoluciones parciales por medio de pago.

## 6. Propuesta para que no se desborde

- **A. Congelar funciones nuevas hasta la prueba con aparatos.** No se empieza ningún corte nuevo hasta hacer la prueba de 30 minutos; después, solo lo que la prueba pida.
  - Efecto: frena el crecimiento ya, y lo siguiente se decide con datos reales y no con capturas de pantalla.
  - Coste: unos días sin entregas.
- **B. Un «mínimo para abrir» con fecha.** Solo los 6 puntos del apartado 4(a), y Verial cobra.
  - Efecto: el desarrollo pendiente baja de 15 cosas a 1 o 2: el corte de alergias y lo que salga de la prueba. Todo lo demás pasa a «después de abrir».
- **C. Aplazar bloques enteros.** E8, E6, E7, E4c y E4g salen del plan activo hasta después del primer servicio. Y hay que decidir una cosa: si el objetivo es dejar Verial, los Hitos 7 y 8 (o un proveedor fiscal certificado) van antes que E8, E6 y E7. El plan del 15-09 ya permite adelantar lo fiscal.
  - Efecto: queda un único camino: abrir → caja y fiscal → lo demás.
- **D. Reglas para que no vuelva a pasar:**
  - una sola página con el orden vigente;
  - toda petición nueva va al final de la cola, salvo que impida abrir;
  - las decisiones pequeñas se dan por buenas en bloque;
  - ningún corte más grande que M;
  - documentos más cortos.

**Recomiendo A + B ahora, decidir C en la misma conversación y adoptar D como norma.** Por qué:
- desde el 13-10 todo se ha construido sin probarlo en un aparato real;
- las notas de 3 a 3,5 sobre 5 salen de una revisión de escritorio;
- la prueba cuesta 30 minutos y puede cambiar el orden de todo;
- por ley, Verial tiene que seguir cobrando, así que abrir no depende ni de la caja, ni de lo fiscal, ni de las reservas, ni del stock;
- sin una regla de cola, el 92 % intercalado seguirá creciendo.

## Papeles que se contradicen

- **¿Se ha hecho algún guion?** Un lector dice que ninguno; otro, que el de D2 sí. `docs/STATUS.md:15` dice «guion manual completado por el promotor», así que tiene razón el segundo. Todas las hojas físicas con tabla siguen en blanco.
- **«Adaptado» con el plato ya hecho.** El traspaso del 29-10, en su línea 123, dice que no está decidido. Tu respuesta 5 de hoy lo decide: con el plato hecho, no se ofrece «Adaptado». Vale la de hoy, y hay que corregir esa línea.
- **El orden de los bloques.**
  - El README (13-10) pone la caja y lo fiscal delante.
  - Los traspasos ponen primero las reservas (E8).
  - `PRODUCT_SCOPE` (14-09) excluye un motor de reservas propio. Ese documento, `ROADMAP` y `BACKLOG` son del 14-09 y están desfasados.
  - Lo último escrito (14-10 y 15-10) pone E8 primero, pero nadie lo ha confirmado desde entonces.
- **¿105 o 118 decisiones pequeñas?** No se contradicen: son 105 decisiones más 13 diferencias con la maqueta.

---

## Fuentes y cifras

- **Base:** `develop` = `44f1434` (Merge PR #103, 30-10). Las respuestas del 02-11 y esta recapitulación entran con su propio PR de documentación.
- **Versión 0.46.2-c3e.2:** `dotnet/src/Costina.Server/Costina.Server.csproj:2` y `Costina.Desktop.csproj:4`.
- **Esquema v8:** `PostgresStore.cs:25`; 27 tablas.
- **54 cortes (PR #20 a #102) y 84 PR mezcladas:** `git log --first-parent`; API `/pulls?state=all`. El reparto 32/0/22 sale de las fechas de merge.
- **Líneas:** `wc -l` sobre `git archive 44f1434`, sin imágenes ni `package-lock`:
  - `dotnet/src`: 21.099;
  - `frontend/pwa/src`: 7.899;
  - pruebas: 19.854;
  - sistema congelado: 18.553;
  - `docs`: 142 ficheros .md, 283.195 palabras.
- **Pruebas:** CI de la PR #102 (`adeee5d` = `0d755ac`). vitest 609, ClientChecks 288, dominio 97, persistencia 88, HTTP 136, Playwright 31, tiempo real 13, servicio 12 e instalador 8: suman 1.282. No se han vuelto a ejecutar.
- **Instalador:** run 41032348890, artefacto 12965624447 (`Costina-Setup-0.46.2-c3e.2.exe`, «Installer checks: 8 passed»), caduca el 04-11 a las 09:25 UTC (10:25 hora peninsular). Comprobado por la sesión principal en la API y el log del job.
- **Caja de ensayo:** `dotnet/src/Costina.Desktop/Views/CheckoutView.xaml:26`.
- **Menús desactivados en el PC:** caja en `MainWindow.xaml:56-57`, fiscal en `:73-74`, resto en `:63-70`.
- **La línea de la cuenta no guarda el IVA:** `SettlementAccount.cs:12-14`.
- **Sin impresión:** búsqueda de print, printer e imprim en `dotnet/src` y `frontend/pwa/src`.
- **Terminar la mesa es solo del responsable:** `Modules.Dining/Affordances.cs:26`.
- **Usuarios solo por consola:** `Server/Program.cs:125-136`.
- **«Unas 8 mesas» y «menú degustación cerrado»:** `docs/AI_CONTEXT.md:13`.
- **Versión instalada 0.43.0-c3c** (`c5632e1`, PR #91): `VersionInfo` de `Costina.Server.exe` y `Costina.Desktop.exe` en `C:\Program Files\Costina`, con el servicio `Costina` en marcha. Leído sin modificar nada; la sesión principal lo volvió a comprobar.
- **El orden y sus cambios:**
  - 23-09: `docs/handoff/2026-09-23-hito6-e3.md:7` y `2026-09-23-hito6-e4.md:7`;
  - 06-10: `docs/plans/2026-09-24-inicio-y-dispositivos.md:5,153`;
  - 14-10: `docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md:23-31`;
  - 15-10: `docs/plans/2026-10-15-menus-carta-y-bodega.md:3,904,920`;
  - 22-10: `docs/plans/2026-10-22-sala-y-cocina.md:3,328-336`;
  - 29-10: `docs/handoff/2026-10-29-c3e1-arreglos-rapidos-usabilidad.md:106-126`.
- **25 cortes desde el 23-09 (#62 a #102):** la clasificación en planificados e intercalados es de un lector. Las líneas son añadidas, con `git diff --shortstat` de cada merge: 3.570 frente a 38.851 (23.023, 11.796, 1.932 y 2.100). C3 suma 19.773. Media por PR: 486 (7.283 líneas en 15 PR) frente a 2.825.
- **Informe de usabilidad** (`docs/reviews/2026-10-28-usabilidad-sala-y-cocina.md`, líneas de `develop`):
  - notas: `:32-38`;
  - 5 hallazgos altos, 28 medios y 43 bajos: `:101`, `:137`, `:281`;
  - H67, unas 55 acciones: `:271-274`;
  - prueba de 30 minutos: `:381-441`;
  - huecos de la sección 6b: `:445-491`.
- **Respuestas del 02-11:** el mismo fichero, la tabla de la sección 5c.
- **Lo que espera al promotor:** `docs/handoff/2026-10-27-c3e-costina-sala.md:82-102`.
- **Decisiones pequeñas** (`docs/native/`): C3a `:122-128`, C3b `:238-276`, C3c `:259-273`, E4e `:176-189`, C3d `:162-189`, C3e `:209-261`.
- **Hojas en blanco:**
  - `D5.6-physical-install.md:75-90`;
  - `D6.5-qr-install-physical.md:81-95`;
  - `C3c-dos-apps.md:411-427`;
  - `C3d-costina-cocina.md:293-304`;
  - `C3e-costina-sala.md:447-457`;
  - `E4e-alergias-antes-de-enviar.md:301-312`.

  El guion de D2 sí está hecho: `docs/STATUS.md:15`. Ver también `docs/STATUS.md:37` y `:206`.
- **GitHub (API, 02-11):** 10 incidencias abiertas (#11, #12, #13, #17, #25, #26, #27, #28, #36 y #37) y 0 PR abiertas.
- **Legal en el repositorio:**
  - `docs/plans/2026-09-15-windows-multidevice-erp.md:179-183,196,199`;
  - `docs/ADR/ADR-005-operational-vs-fiscal.md:21,30-32`;
  - `docs/ADR/ADR-012-core-and-modules.md:25-27`;
  - `docs/BACKLOG.md:75`;
  - `docs/STATUS.md:221`.
- **Normas** (RD 1619/2012, RD 1007/2023, Orden HAC/1177/2024, RD 254/2025, RDL 15/2025): conocimiento general y prensa que enlazó un lector. El aplazamiento a 2028 solo aparece en prensa (sin comprobar).
- **Tamaños de lo pendiente:** estimación de un lector comparando con cortes hechos, de B.1 (84 líneas) a C2b (5.403). No hay plan para E5 a E8, los Hitos 7 y 8 ni los informes (sin comprobar).