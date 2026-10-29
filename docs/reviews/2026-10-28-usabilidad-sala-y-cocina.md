# Costiña — Usabilidad en sala y cocina: informe para el promotor

**Versión revisada:** 0.46.0-c3e, 28-10-2026 (`develop` `a1ca7f6`).

**Qué se ha hecho después:** el 29-10 el promotor aprobó los arreglos rápidos («Haz los arreglos rápidos»). Los seis primeros de la sección 5a, menos H21, se entregaron en C3e.1 (`0.46.1-c3e.1`): H51, H34 (la parte de texto), H19, H06, H03, H53, H56, H48 y H20. Ver `docs/native/C3e.1-arreglos-rapidos-usabilidad.md`. Los números de línea del anexo son los de `a1ca7f6`.

**De dónde salen los datos:**
- 144 pantallas reales medidas con el motor de pruebas, en el móvil, la tableta vertical de sala, la tablet de cocina y un navegador.
- Recorridos de cada tarea por perfil: camarero, estación de cocina, pase y encargado en el PC.
- Cinco revisiones por enfoque: hora punta, personal eventual, prevención de errores, medidas, e iPhone frente a Android.
- 76 problemas encontrados, cada uno comprobado después por separado. No se ha descartado ninguno. Al comprobarlos, 42 se rebajaron de gravedad y ninguno se subió.

**Límite de la medición:** el PC del local no entra en ella. Se ha revisado por su código y sus capturas.

---

## 1. Veredicto

Para lo de cada día, sí es sencillo. El camarero abre una mesa con 3 toques, envía el siguiente pase con 2 y sirve con 1, siempre con un único botón grande abajo que dice lo que hace. La estación de cocina marca su comanda con un solo «Listo». Las alergias van siempre escritas y en rojo, y el sistema no deja validar ni servir un plato afectado sin decisión de cocina.

- **Camareros, 3,5 sobre 5.** Lo diario se aprende en minutos. Pero los avisos de «listo para servir» y de orden sin confirmar salen arriba de la página y no se ven si se ha bajado la pantalla; en iPhone, además, no vibra. El «Atrás» del móvil puede perder lo que se estaba escribiendo. Casi ningún error se puede corregir desde el móvil, y la pantalla no dice dónde se corrige.
- **Estaciones de cocina, 3,5 sobre 5.** Con pocas comandas se usan sin formación. Con el servicio lleno, la comanda nueva queda fuera de la pantalla. Una alergia tardía, o una decisión del pase sobre una comanda que el cocinero ya leyó, llega sin sonido ni marca.
- **Pase, 3 sobre 5.** Las decisiones se entienden, pero con carga es la pantalla más trabajosa. Con solo 4 o 5 mesas de prueba, la página llega a medir 6 pantallas de alto. Revisar cada alergia obliga a desplazarse dos veces, y lo urgente no sube arriba.
- **Encargado en el PC, 3 sobre 5.** Ver el comedor, abrir mesas con alergias y conectar aparatos está muy bien. Falla en dos sitios:
  - Al anotar una alergia en una mesa ya abierta, el comensal, el tipo y la gravedad se copian de la anotación anterior.
  - En el modo de puesto no se puede terminar ni liberar una mesa, y la pantalla no explica por qué.

  La comprobación rebajó el peso de cobrar y liberar: son unas 4 acciones en el Comedor más las de Caja, no 12.

En conjunto, la base es buena y muy segura. Todavía no es «el más intuitivo» en las excepciones ni en hora punta, y nada se ha probado aún en un iPhone, un iPad o con la conexión sencilla de verdad.

| Perfil | Nota | Cambio tras la comprobación |
|---|---|---|
| Camarero (móvil y tableta de sala) | 3,5 | Se mantiene. Siete problemas que parecían altos (H02, H03, H18 a H22) quedan en medios, pero se confirma el de «Atrás» y la recarga. |
| Estación de cocina | 3,5 | Se mantiene. Se confirman los dos problemas altos: alergia tardía en silencio y comanda nueva fuera de la pantalla. |
| Jefe de pase | 3 | Se mantiene. Se confirma el alto de la columna «Por revisar». |
| Encargado en el PC | 3 | Se mantiene. El cierre de mesa pesa menos de lo estimado, pero se confirma el fallo de alergia del PC. |

---

## 2. Lo que ya está bien

1. **Las alergias nunca se esconden ni se atenúan.** Van en la ficha de la mesa, en Carta, en Bodega y en la ronda, en la estación de cocina, incluso en la franja «Hecho», y en el pase. Siempre dicen quién, el tipo, la sustancia y la gravedad, con la palabra ALERGIA en mayúsculas. El rojo se reserva para ellas; los tiempos van en ámbar.
2. **Ningún plato afectado sale sin decisión de cocina.** El sistema bloquea validar, servir y enviar el pase mientras haya algo por revisar. El camarero lo ve escrito en el botón: «Esperando la revisión de cocina».
3. **Una sola acción principal, grande y abajo.** En el móvil siempre está en la zona del pulgar y dice la acción y el pase («Enviar siguiente pase · Aperitivos»). Si está apagada, casi siempre dice por qué (no con una orden sin confirmar, H21). En las 144 pantallas ningún objetivo táctil baja del tamaño mínimo (48 píxeles; 64 en los botones de cocina, pensados para manos mojadas). La separación es otra cosa: hay 109 pares de botones vecinos a menos de 8 píxeles, como los atajos de nota del pase, a 6.
4. **Abrir mesa con alergias es seguro.** Todo sale en una sola orden. «¿Quién?» empieza sin elegir, y Alergia y Grave vienen marcadas de partida. Si el servidor rechaza la apertura, se conserva lo escrito.
5. **Una orden enviada sin red ni se pierde ni se duplica.** Queda guardada, se reintenta sola al volver la red si la aplicación está abierta (con el móvil bloqueado en el bolsillo no, y mientras tanto el móvil queda bloqueado) y se puede preguntar al servidor si llegó.
6. **Las bebidas son cómodas.** Copa o Botella con un toque, «+1» para repetir, ronda editable antes de enviar y confirmación si se sale sin enviarla.
7. **La estación de cocina es limpia.** Cada estación ve solo lo suyo, con «También en: Cocina caliente» para lo de las demás. «Listo» es un toque. El tiempo va siempre escrito («Lleva 27 min»), en ámbar y nunca en rojo. Si no hay comandas, lo dice: «No hay comandas. Las nuevas aparecen aquí solas.»
8. **Detalles cuidados.**
   - Ningún texto con contraste bajo en las 144 pantallas.
   - En la tableta vertical de sala, la ficha de la mesa cabe entera en una pantalla.
   - Emparejar un iPhone está bien guiado, paso a paso (visto en un navegador que imita al iPhone, no en un iPhone).
   - Inicio del PC dice «Sin leer todavía» en vez de un cero engañoso.
   - El PC pide la contraseña en el momento y en contexto.

**Tus reglas, una a una**

| Regla | ¿Se cumple? | Matiz |
|---|---|---|
| Seguridad alimentaria primero | Sí, en lo esencial | Quedan cinco huecos: la estación no se entera de una alergia tardía (H34); el PC copia la gravedad de la anotación anterior (H51); la banda de «listo» no recuerda la alergia (H19); las alergias escritas se pueden perder antes de abrir la mesa (H01, H06); y no hay regla escrita para numerar a los comensales (H02). |
| Sin dinero en móviles ni en cocina | Sí | Las pruebas automáticas lo exigen. |
| Castellano con tildes, nombres y nunca códigos | Casi siempre | Algunos rechazos raros enseñan un código en inglés (H27). El PC pide códigos al preparar el menú (H67). Una captura antigua del PC muestra «Anaphylaxis (Critical)» en inglés (H58). |
| Android y también iPhone o iPad | A medias | La instalación parece bien en los dos, pero solo se ha visto imitando los aparatos. Los avisos no: el iPhone no vibra ni suena (H18). Nada se ha probado en un aparato real ni con la conexión sencilla (H70). |
| Pantallas cuidadas | Sí, con pulido pendiente | Letra pequeña en las tarjetas de mesa (H05), resaltado azul del navegador al tocar (H16) y avisos que empujan el contenido (H23). |
| «El más intuitivo» | En lo diario, sí | En las excepciones (corregir errores, avisos fuera de la vista, «Atrás») y en el pase con carga, todavía no. |

---

## 3. Tareas del día a día

| Tarea | Aparato | Toques | Facilidad (1-5) |
|---|---|---|---|
| Abrir una mesa de 4 sin alergias | Móvil | 3, más bajar hasta las mesas libres | 4 |
| Abrir una mesa con una alergia grave | Móvil | 5, más escribir la sustancia | 4 |
| Enviar el siguiente pase | Móvil o tableta de sala | 2 | 5 |
| Servir desde la banda «listo para servir» | Móvil | 1 (2 desde la ficha) | 3, solo si la banda está a la vista |
| Ronda de bebidas (copas o botellas) | Móvil | 4 | 4 |
| Anotar una alergia con la mesa ya abierta | Móvil | 4, más escribir | 3 |
| Omitir un pase | Móvil | 4, más el motivo | 3 |
| Corregir un error (mesa o personas equivocadas, plato de carta, «Servido» por error) | Móvil | No se puede | 1 |
| Empezar turno (activar el sonido) | Tablet de estación | 1 | 4 |
| Marcar «Listo» | Tablet de estación | 1 | 4 |
| Deshacer un «Listo» | Estación o pase | No se puede | 1 |
| Encontrar la comanda que acaba de sonar con más de 4 abiertas | Tablet de estación | Desplazar la pantalla | 2 |
| Revisar las alergias de un comensal antes de enviar | Tablet del pase | 4, más escribir la nota, con doble desplazamiento | 3 |
| Validar un pase | Tablet del pase | 1, más desplazar hasta su tarjeta | 3 |
| Decidir una alergia tardía con el pase ya enviado | Tablet del pase | 6, en dos sitios de la pantalla | 2 |
| Abrir una mesa con alergia | PC | 7 | 4 |
| Anotar una alergia en una mesa abierta | PC | 6 | 3, y copia datos de la anterior (H51) |
| Ver qué va tarde en cocina | PC | 1, más leer tarjeta a tarjeta | 2 |
| Cobrar, terminar y liberar la mesa | PC | Unas 4 en el Comedor, más las de Caja | 2 |
| Conectar un móvil nuevo | PC y móvil | Android: unos 4 en el móvil. iPhone: unos 9, más teclear un código de 10 caracteres | Android 4, iPhone 3 |

---

## 4. Problemas, por orden de impacto en un servicio real

Cada problema lleva un número entre paréntesis (H01 a H76) que remite al anexo. El esfuerzo va en tres tamaños: pequeño (horas o un día), medio (unos días) y grande (un corte propio). Donde la revisión choca con algo que ya aprobaste, lo marco como **decisión para revisar**: no es un error del producto, es una decisión que conviene volver a mirar.

### Severidad alta (5)

**1. Una alergia tardía, o la decisión del pase, llega a la estación en silencio (H34)**
- **A quién:** estaciones de cocina y pase. **Esfuerzo:** medio (la parte de texto, pequeño).
- **Qué pasa:** la tablet solo suena cuando entra una comanda nueva o cuando el pase la devuelve con «Rehacer» (así que el mecanismo para volver a avisar ya existe). Si el camarero anota una alergia con el pase ya en cocina, o el pase decide «Adaptado: sin almendra» sobre una comanda que el cocinero ya leyó, solo cambia el texto dentro de la tarjeta, sin sonido ni marca.
  - La estación ve «ALERGIA marisco (grave) · Por revisar en el pase» con el «Listo» activo debajo, y nada le dice si debe esperar.
  - Si el pase elige «Adaptado» sobre un plato ya hecho, ese plato no vuelve a la estación.
  - El freno final funciona, porque no se valida ni se sirve sin decisión. Pero la estación puede cocinar con el alérgeno sin que nadie se lo diga.
- **Propuesta:** que la tablet suene y marque la tarjeta («ALERGIA NUEVA» o «CAMBIO: decisión del pase») hasta que alguien toque «Visto». Que la línea diga qué hacer: «no lo emplates hasta que decida el pase».
- **Decisión para revisar:** si el sistema debe dejar marcar «Listo» con una revisión pendiente, o si «Adaptado» debe devolver el plato a la estación. Ojo: bloquear el «Listo» dejaría atascada una comanda ya cocinada.

**2. En el PC, anotar una alergia en una mesa abierta copia el comensal, el tipo y la gravedad de la anterior (H51)**
- **A quién:** encargado. **Esfuerzo:** pequeño.
- **Qué pasa:** tras «Declarar» solo se vacía la sustancia; comensal, tipo y gravedad se quedan, también al cambiar de mesa. Ejemplo: si la última anotación fue «Comensal 3 · Preferencia · Leve» (sin cebolla), una alergia grave al gluten en otra mesa sale como preferencia leve del comensal 3. Se pinta en ámbar, no alarma, y cocina revisa los platos de otra persona. Además, si se deja el comensal vacío, cuenta como toda la mesa. Es justo lo contrario de lo que el alta de mesa y el móvil hacen bien.
- **Propuesta:** usar la misma fila que el alta: «¿Para quién?» sin elegir, y Alergia y Grave de partida cada vez. Vaciarlo todo al declarar y al cambiar de mesa.

**3. Pase: la columna «Por revisar» ocupa media pantalla por plato, se desplaza por dentro y queda tapada (H50)**
- **A quién:** jefe de pase. **Esfuerzo:** medio.
- **Qué pasa:** cada plato por revisar antes de enviar ocupa casi media pantalla. La columna tiene su propio desplazamiento dentro de la página, y al abrir, «Esperando al camarero» tapa justo los botones del primer plato. Con guantes o los dedos mojados, al deslizar se mueve la lista interior y no la página. En la versión anterior se veían 3 platos a la vez; ahora, uno. Pasa con cada mesa que lleva alergias, mientras su camarero espera para enviar.
- **Propuesta:** quitar el desplazamiento dentro de otro desplazamiento. Una opción es una franja «Alergias por decidir» a todo lo ancho, como antes. Otra es compactar cada decisión y ensanchar la columna. Mientras haya algo por revisar, plegar «Esperando al camarero». Comprobar en la medición que los botones del primer plato se ven enteros al abrir.

**4. El «Atrás» del móvil o una recarga pierden la ronda y las alergias a medio escribir (H01)**
- **A quién:** camarero (y la cocina, si se cierra su aplicación). **Esfuerzo:** medio.
- **Qué pasa:** la única salida de la ficha, Carta, Bodega o «Abrir mesa» es la flecha de arriba a la izquierda. Es lo más lejos del pulgar y desaparece al bajar la pantalla.
  - El «Atrás» de Android no vuelve al plano: sale de la aplicación. Es lo esperable, pero no está comprobado en un aparato.
  - Con la conexión sencilla, tirar de la pantalla hacia abajo la recarga.
  - En los dos casos se pierden, sin preguntar, la ronda sin enviar y las alergias escritas en «Abrir mesa». Si el camarero reabre la mesa sin acordarse de ellas, la mesa sale sin alergias.
- **Propuesta:** que «Atrás» baje un nivel con la misma pregunta que ya hace la ronda, que tirar hacia abajo no recargue la página y que salga un aviso antes de cerrar si queda algo sin enviar.
- **Decisión para revisar:** guardar el borrador en el propio aparato. Hoy se decidió no hacerlo.

**5. Con el servicio lleno, la comanda nueva queda fuera de la pantalla de la estación (H35)**
- **A quién:** estaciones de cocina. **Esfuerzo:** medio.
- **Qué pasa:** en la tablet cabe una fila de 4 comandas. La cabecera y la franja del turno ocupan la cuarta parte de arriba, y una comanda con alergia mide casi media pantalla (unos 400 de 820 píxeles). Como van de la más antigua a la más nueva, desde la quinta la que acaba de sonar queda abajo, y nada avisa de que hay más. Con 80 cubiertos pueden juntarse 8 a 12 comandas (estimación): 2 o 3 pantallas que desplazar con las manos mojadas. No se ha medido con esa carga: las pruebas tienen como mucho 3 comandas en una estación, así que es un cálculo.
- **Propuesta:** una barra fija abajo, «↓ 3 comandas más abajo · la más nueva: Mesa 9», que baje con un toque. Ganar alto: la franja del turno se queda en un icono tras empezar, y personas y tiempo van en una línea. Medir un estado con 12 comandas.
- **Decisión para revisar:** pasar a columnas en horizontal cambiaría una decisión aprobada.

### Severidad media (28)

**6. La banda «listo para servir» no recuerda la alergia ni a qué comensal va el plato adaptado (H19)**
- **A quién:** camarero. **Esfuerzo:** pequeño. **Decisión para revisar:** la maqueta aprobada la dejó así.
- **Qué pasa:** la banda que sale sobre cualquier pantalla solo dice «Mesa 5 · Aperitivos · listo para servir», con el botón «Servido». La ficha sí escribe en rojo «Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra». No siempre sirve quien abrió la mesa. Lo atenúa que el pase, que reparte, sí lo ve.
- **Propuesta:** añadir a la banda esa línea roja, o un resumen como «1 plato adaptado · Comensal 2». Sin pedir un segundo toque en «Servido».

**7. Una alergia con el pase ya enviado queda enterrada y se decide en dos sitios (H47)**
- **A quién:** pase. **Esfuerzo:** medio.
- **Qué pasa:** la decisión va dentro de la comanda, en su sitio por antigüedad. En la medición quedaba unas 5 pantallas más abajo. No cuenta en la cabecera, y los platos del mismo comensal que faltan por enviar se deciden en otra columna, plato a plato. La seguridad la cubre el sistema; lo que se pierde es tiempo.
- **Propuesta:** un apartado «Alergias por decidir» arriba, por comensal, que junte lo ya enviado (primero, en ámbar, con la línea roja si alarma) y lo pendiente, con un «No le afecta a nada más» que lo cubra todo.

**8. Un comensal nuevo «por revisar» no suena en el pase, y el camarero espera bloqueado (H46)**
- **A quién:** pase y camarero. **Esfuerzo:** medio. **Decisión para revisar:** que no suene se decidió al construir la cocina.
- **Qué pasa:** cada mesa abierta con alergia espera al pase para su primer envío. La tarjeta no dice desde cuándo espera y la lista va por número de mesa, no por antigüedad.
- **Propuesta:** un tono distinto del de comanda; «espera desde hace N min», con la hora que dé el servidor; orden por antigüedad; y el contador en ámbar, no en rojo.

**9. Los atajos de nota del pase son siempre los mismos y permiten decisiones que no casan con la alergia (H48)**
- **A quién:** pase y estaciones. **Esfuerzo:** pequeño. **Decisión para revisar:** estos atajos están en el plan aprobado.
- **Qué pasa:** ante «ALERGIA marisco (grave)» se ofrece «Sin gluten», y nada impide guardar «Adaptado: Sin gluten», que es lo que leen la estación y Sala. Los atajos están muy juntos. Un atajo solo rellena la nota y la decisión se toma aparte, así que un toque desviado no decide por sí solo.
- **Propuesta:** no ofrecer «Sin gluten» si la restricción no es de gluten y separar más los atajos. Atajos que lleven la sustancia («No lleva marisco»): a consultar contigo.

**10. «Comensal 2», sin ninguna regla de numeración (H02)**
- **A quién:** camarero y pase. **Esfuerzo:** pequeño. **Decisión tuya:** es una norma de la casa.
- **Qué pasa:** toda la seguridad pasa por «Comensal n», pero ninguna pantalla dice quién es el 1 ni en qué sentido se cuenta. Si quien anota y quien sirve cuentan distinto, el plato adaptado llega a otra persona. Lo atenúa que la franja escribe la sustancia, y se puede preguntar «¿el de frutos secos?».
- **Propuesta:** que el responsable escriba la regla de la casa en el PC y que salga igual en Sala y Cocina. Y explicarla al formar al personal.

**11. «Abrir mesa» pierde las alergias escritas sin preguntar (H06)**
- **A quién:** camarero. **Esfuerzo:** pequeño.
- **Qué pasa:** con alergias escritas, tocar «Mesas» o «Ver mesa» en la banda cierra la hoja sin aviso, y hay que reescribirlas. La ronda, en cambio, sí pregunta. Encima del botón solo pone «1 alergia o intolerancia», sin decir quién ni qué. La mesa no se abre, así que nada sale sin alergia, pero al reescribir es fácil olvidar una.
- **Propuesta:** la misma pregunta que la ronda («¿Salir sin abrir? Se perderá 1 alergia») y escribir cada alergia entera encima de «Abrir»; con más de 3, un resumen.

**12. Si el servidor rechaza una alergia anotada tarde, se pierde lo escrito (H03)**
- **A quién:** camarero. **Esfuerzo:** pequeño.
- **Qué pasa:** la hoja se cierra sin esperar respuesta. El aviso de arriba no dice que la alergia NO se anotó. Es raro: un corte de red no pierde nada, solo un choque con otra pantalla.
- **Propuesta:** cerrar la hoja solo si la anotación se confirma. Si se rechaza, dejar la fila escrita y decir «La alergia NO se ha anotado: toca otra vez». Hacer lo mismo al omitir un pase.

**13. En el PC, un solo «Motivo» para todo, que nunca se vacía (H61)**
- **A quién:** encargado. **Esfuerzo:** pequeño.
- **Qué pasa:** el mismo campo sirve para pausar, omitir, cancelar, liberar y retirar una alergia, y no se vacía al cambiar de mesa. El motivo escrito para pausar la Mesa 4 acaba en la liberación de la Mesa 7, o en la retirada de una alergia, que además se hace con un clic y sin confirmar.
- **Propuesta:** pedir el motivo dentro de cada acción, vaciarlo tras cada orden y al cambiar de mesa, y retirar alergias en dos pasos, como en el móvil.

**14. La página del pase mide de 4 a 6 pantallas y lo que pide acción no sube (H49)**
- **A quién:** pase. **Esfuerzo:** medio. **Decisión para revisar:** el orden por antigüedad y en filas está aprobado.
- **Qué pasa:** con 4 o 5 mesas de prueba, en cada estado medido quedan entre 18 y 26 botones de acción fuera de la vista (muchos son las tres decisiones repetidas en cada plato). La medición está algo inflada por un caso raro (3 de 5 mesas con alergia tardía), pero con 15 a 20 comandas también habrá varias pantallas.
- **Propuesta:** sacar los formularios de revisión de las comandas y añadir una barra fija con lo urgente. Ordenar primero lo que se puede validar y usar columnas sin huecos.

**15. «No le afecta a nada más» queda al final de la lista del comensal (H56)**
- **A quién:** pase. **Esfuerzo:** pequeño, a consultar.
- **Qué pasa:** lo normal es adaptar uno o dos platos y dejar el resto, pero el botón para decidir el resto va después de todos los platos. Con un menú de 7 pases hay que pasar 7 formularios.
- **Propuesta:** ponerlo arriba, justo bajo la alergia, manteniendo la nota obligatoria, y plegar cada plato a una línea.

**16. La banda «listo para servir» no se ve si el camarero ha bajado la pantalla, y en iPhone no hay otra señal (H18)**
- **A quién:** camarero. **Esfuerzo:** medio. **Decisión para revisar:** banda que no flota y «en sala no suena nada».
- **Qué pasa:** con la lista de mesas larga o en Bodega, el aviso queda fuera de la vista. En Android vibra; en iPhone o iPad no hay ninguna señal, y el plato espera en el pase.
- **Propuesta:** una píldora fija en la barra de abajo («Mesa 5 · Aperitivos listo ↑»), sin botón «Servido» directo, y un recordatorio cada pocos minutos (ya está previsto). Un tono opcional en iPhone cambiaría tu regla de que en sala no suena nada.

**17. La banda empuja el contenido y pone «Servido» u otra mesa bajo el dedo (H23)**
- **A quién:** camarero. **Esfuerzo:** medio.
- **Qué pasa:** al aparecer, la banda empuja todo hacia abajo (unos 225 píxeles, casi un tercio de la pantalla del móvil). Donde iba el dedo (el buscador de Bodega o las pestañas) queda «Servido», y la mesa recién lista sube al primer puesto. En Sala, «Servido» no se deshace. Es poco probable en cada aviso, pero hay muchos avisos cada noche.
- **Propuesta:** ignorar los toques en la banda durante unos 0,6 segundos tras aparecer, hacerla entrar sin mover nada (la píldora del punto anterior) y nombrar la mesa en el botón grande («Servido · Aperitivos · Mesa 5»).

**18. Tras cada orden, el botón grande pasa a la siguiente acción en el mismo sitio (H20)**
- **A quién:** camarero y estación. **Esfuerzo:** pequeño.
- **Qué pasa:** «Abrir Mesa N» y, justo después, «Enviar siguiente pase» ocupan el mismo botón. Lo mismo ocurre con «Servido» y el envío del pase siguiente. Un doble toque nervioso, o un segundo toque «por si no fue» con la Wi-Fi lenta, puede mandar el pase a cocina sin querer, y eso no se recupera. No se ha medido si la red del local va tan rápido.
- **Propuesta:** mostrar durante 1 o 2 segundos el resultado en el botón apagado («Mesa 5 abierta», «Servido ✓») antes de pasar a la acción siguiente. La banda esperaría lo mismo.

**19. Un «Listo» por error no se deshace y la siguiente comanda se coloca bajo el dedo (H33)**
- **A quién:** estación. **Esfuerzo:** medio. **Decisión para revisar:** «sin deshacer» está aprobado.
- **Qué pasa:** la comanda sale de la rejilla al instante y la siguiente ocupa su sitio. Además, ningún botón cambia de forma visible al pulsarlo. Como el pase no valida lo que no ve, el efecto es un retraso o un olvido, no un riesgo de alergia.
- **Propuesta:** dejar la comanda 8 a 10 segundos en su sitio con «Hecho ✓» y un botón «Deshacer», mostrar «Enviando…» mientras va la orden y dar respuesta visual al toque. El deshacer necesita tu visto bueno.

**20. En el pase, «Rehacer» va pegado a «Adaptado» y sin confirmación (H53)**
- **A quién:** pase. **Esfuerzo:** pequeño.
- **Qué pasa:** «No le afecta», «Adaptado» y «Rehacer» son tres botones estrechos y juntos; «No le afecta» incluso se parte en dos líneas. Tocar «Adaptado» queriendo «Rehacer» deja salir como adaptado un plato ya cocinado.
- **Propuesta:** «Rehacer» en su propia fila, con borde de aviso y en dos pasos. Los otros dos, a lo ancho.

**21. Nadie avisa al camarero cuando cocina termina de revisar una alergia (H24)**
- **A quién:** camarero. **Esfuerzo:** medio.
- **Qué pasa:** el botón se enciende en silencio, así que justo la mesa con alergia espera más su primer pase.
- **Propuesta:** un aviso ligero en «Mis mesas»: «Mesa 5 · revisada por cocina: ya puedes enviar Aperitivos». Que se distinga del de «listo para servir» y no lleve botón «Servido».

**22. Una orden sin confirmar deja los botones en gris sin explicar por qué (H21)**
- **A quién:** camarero y pase. **Esfuerzo:** pequeño en lo básico.
- **Qué pasa:** el aviso «ORDEN SIN CONFIRMAR» sale arriba y no se ve si se ha bajado la pantalla. El camarero ve gris todo, incluso «Servido», y parece que el móvil se ha colgado; es fácil repetir la orden en el PC. Suele arreglarse en segundos al volver la red.
- **Propuesta:** llevar la vista hasta el aviso, como ya hace la ronda, y poner bajo el botón apagado «Hay una orden sin confirmar: no la repitas · Ver la orden».

**23. El aviso de comanda nueva es corto, suena una sola vez y no deja marca (H36)**
- **A quién:** estación y pase. **Esfuerzo:** pequeño.
- **Qué pasa:** son dos notas en menos de medio segundo; si la campana extractora las tapa, no se repiten. Tras una recarga, la tablet queda muda hasta que alguien toque «Empezar turno».
- **Propuesta:** marcar la comanda como «NUEVA» durante 60 segundos, repetir el tono cada 20 o 30 segundos mientras no se haya visto (configurable) y, si el turno no ha empezado, mostrar una franja ámbar fuerte: «SIN SONIDO · toca Empezar turno».

**24. Con la conexión sencilla, la tablet de cocina se apaga si nadie cambió su ajuste (H37)**
- **A quién:** estación, pase y encargado. **Esfuerzo:** pequeño.
- **Qué pasa:** la aplicación no puede mantener la pantalla encendida por sí sola. La ayuda es igual para iPad y Android, y se queda dos renglones en ámbar todo el turno.
- **Propuesta:** dar el paso exacto para cada aparato al conectarlo y en Dispositivos del PC, con una comprobación, y dejar solo un icono discreto en la tablet. La solución de fondo es el certificado público.

**25. En el iPad, el tono puede no sonar con el modo silencio (H38)**
- **A quién:** estación y pase. **Esfuerzo:** pequeño. Sin comprobar.
- **Qué pasa:** la pantalla sigue diciendo «suena con cada comanda nueva» aunque no esté sonando.
- **Propuesta:** preparar el sonido para el modo silencio, recuperarlo al volver del bloqueo, y tras el tono de prueba preguntar «¿Lo has oído?». Si no suena, avisarlo en pantalla.

**26. Leer a 1 o 2 metros, sin comprobar (H39)**
- **A quién:** estación y pase. **Esfuerzo:** pequeño. **Decisión para revisar:** los tamaños de letra están aprobados.
- **Qué pasa:** la letra cumple la regla aprobada, pero, según mi cálculo (no está medido), la sustancia de la alergia se lee bien a 1 metro y queda por debajo de lo recomendado a 2. El recuadro rojo se ve de lejos; «marisco», no.
- **Propuesta:** medirlo primero en la prueba real. Después, línea de alergia más grande con la sustancia en mayúsculas, o una opción de «Letra grande».

**27. Casi ningún error se corrige desde el móvil, y no dice dónde se corrige (H22)**
- **A quién:** camarero y encargado. **Esfuerzo:** pequeño el texto; grande el resto.
- **Qué pasa:** desde el móvil no se puede cambiar de mesa ni de personas, quitar un plato de carta, deshacer «Servido» u «Omitir», cancelar ni liberar. Una mesa abierta por error con una alergia deja la alergia en la mesa equivocada.
- **Propuesta:**
  - Ya: un apartado «¿Te has equivocado?» en «Más opciones», que diga dónde se arregla cada caso.
  - Más adelante, por plan: órdenes para cambiar de mesa llevándose las alergias, cambiar el número de personas y deshacer «Servido» durante unos segundos.

**28. El PC, en modo de puesto, no puede terminar ni liberar mesas y no dice por qué (H59)**
- **A quién:** encargado. **Esfuerzo:** medio.
- **Qué pasa:** la mesa servida muestra «Sin acciones para esta mesa ahora». Despista, aunque no bloquea: para cobrar ya se entra como responsable, y el botón para entrar está siempre arriba.
- **Propuesta:** decide si el puesto fijo puede terminar y liberar. Si no, que el botón diga «Terminar y liberar es del responsable · Entrar» y vuelva a la misma mesa.

**29. Cobrar y liberar va repartido en dos vistas, sin camino de vuelta (H60)**
- **A quién:** encargado. **Esfuerzo:** medio.
- **Qué pasa:** en Caja hay que registrar el pago y cerrar la cuenta por separado, y desde Caja no se vuelve a la mesa. En el Comedor hay que terminar, escribir el motivo y liberar; cada pase pendiente se omite con su propio motivo. Caja es todavía de ensayo.
- **Propuesta:** un botón «Cobrar y cerrar». Al cerrar, ofrecer «Terminar y liberar Mesa X» con un motivo ya propuesto («Cuenta cobrada»). Mejor hacerlo junto al cobro definitivo.

**30. La estación de sala del PC y la de los móviles deben coincidir, y nada lo avisa (H64)**
- **A quién:** encargado y camarero. **Esfuerzo:** pequeño.
- **Qué pasa:** si un móvil se conecta a otra estación de sala, las mesas abiertas desde el PC no salen en su «Mis mesas» ni le avisan, sin ningún síntoma.
- **Propuesta:** proponer la estación del PC al conectar, avisar si se elige otra y marcar en la lista los aparatos que no coinciden.

**31. Caja deja un producto elegido sin que nadie lo elija (H65)**
- **A quién:** encargado. **Esfuerzo:** pequeño.
- **Qué pasa:** aparece «Agua mineral» marcada y la lista de consumos queda fuera de la vista. «Añadir» apunta ese producto.
- **Propuesta:** mostrar primero las líneas de la cuenta, no preelegir nada y dejar «Añadir» apagado hasta que se elija un producto.

**32. Preparar el menú del día es plato a plato y con códigos (H67)**
- **A quién:** encargado. **Esfuerzo:** grande.
- **Qué pasa:** cambiar un plato cuesta unas 8 acciones, y renovar 3 pases de 3 platos, unas 55.
- **Propuesta:** mantener «Menú de hoy» (E4f) como prioridad y, mientras llega, llevar los códigos a «Más opciones».

**33. Nada se ha probado en un iPhone, un iPad ni con la conexión sencilla de verdad (H70)**
- **A quién:** todos. **Esfuerzo:** medio.
- **Qué pasa:** las 144 mediciones son de un navegador de ordenador que imita aparatos. Las hojas de prueba en aparatos de las tres últimas entregas están en blanco. Sigue sin comprobarse si el icono del iPhone abre a pantalla completa, el sonido en silencio, la vibración y la lectura a distancia.
- **Propuesta:** la prueba del apartado 6. Si va antes o después de E4f, lo decides tú.

### Severidad baja (43), en corto

**Sala (móvil):**
- «Mis mesas» tiene mucha cabecera: caben 2 a 4 mesas por pantalla y las libres van al final (decisión para revisar) (H04).
- Estado y tiempo de las tarjetas en letra pequeña, que no crece en la tableta vertical (decisión para revisar; subirla en la tableta es barato) (H05).
- La sustancia se escribe a mano. Propuesta: botones con los 14 alérgenos que solo rellenen el texto (H07).
- En iPhone, el teclado puede tapar «Abrir» y la tecla Intro no lo cierra (H08).
- Con una alergia ya anotada, el enlace dice «Cambiar» y el formulario para otra queda al final (decisión para revisar) (H09).
- Menú con elección: un toque y una espera por plato. Queda para E4g; Costiña tiene menú cerrado (H10).
- Pedir en «Carta y menús» no confirma qué se pidió (H11).
- No se puede quitar un plato pedido, y «Para quién» se queda elegido (decisión; E4g) (H12).
- «Copa» y «Botella» muy juntos, y la ronda no dice qué formato se añadió (añadir el formato es barato) (H13).
- «Más opciones» y «Lo que lleva la mesa» quedan a 1,5 o 2 pantallas en el móvil (H14).
- Iconos sin texto, «0 de 2» sin la palabra «pases» y «Más opciones» con tres sentidos (H15).
- Resaltado azul del navegador al tocar (H16).
- Faltan pruebas automáticas: anotar o retirar una alergia, omitir un pase, elegir y pedir plato (H17).
- No hay forma de avisar de que una mesa pide la cuenta, ni el móvil dice que eso va en Caja. El aviso de cuenta toca la decisión de no llevar dinero al móvil (H25).
- El aviso de orden sin confirmar usa palabras técnicas (H26).
- Algunos rechazos raros muestran un código en inglés (H27).
- Los rechazos no nombran la mesa, y en cocina el choque de versiones sale dos veces (H28).
- «Pase» significa a la vez plato del menú y puesto que valida (decisión de maqueta) (H29).
- Se mezclan «Sala» y «estación» (H30).
- La misma acción tiene nombres distintos en Sala, Cocina y PC, y se alternan «tablet» y «tableta» (H31).
- «Descartarla» borra una orden bloqueada sin confirmar (H32).
- En Android, el móvil no explica cómo poner el icono (H74).
- En un iPhone con Chrome como navegador, los pasos solo hablan de Safari (H75).
- En iPhone, Intro en el campo del código da error en vez de pasar al nombre (H76).
- Emparejar un iPhone cuesta el doble que un Android (H68).
- En Safari, lo único que se puede tocar es la salida de emergencia (H69).

**Cocina:**
- «Rehecho» aparece cuando todavía hay que rehacerlo. Falta una marca «VUELVE DEL PASE» (H40).
- Hay dos botones «Listo» con distinto alcance (H41).
- Sin red, la tarjeta no dice que su «Listo» no ha llegado (H42).
- «También en» parece un enlace y no dice cómo va la otra estación (H43).
- «× 4 (1 adaptada)» obliga a hacer la resta (decisión para revisar) (H44).
- El aviso de almacenamiento ocupa el pie de cada pantalla (decisión para revisar) (H45).
- «Validar pase» no nombra la mesa (H52).
- La cabecera del pase cuenta solo parte de lo pendiente y desaparece al desplazar (H54).
- «Esperando al camarero» va siempre en gris y no dice de quién es la mesa (H55).
- Los estados del pase están en letra de 16 píxeles (decisión para revisar) (H57).

**PC:**
- La alergia del plano pequeño de Inicio va en letra muy pequeña, y la cifra de alergias no lleva a la mesa (H58).
- El PC no marca lo que va tarde en cocina (pendiente de decidir tras la prueba) (H62).
- Para cobrar hace falta la contraseña del único responsable. El PIN por persona llegará con E5 (H63).
- Un móvil que no conecta es difícil de diagnosticar: falta «visto por última vez» (H66).
- «Entrar como responsable para abrir Inicio» confunde (H71).
- Caja no calcula el cambio en efectivo (H72).
- Sin tiempo real, el aviso es solo una línea pequeña abajo (H73).

---

## 5. Qué hacer

### 5a. Arreglos rápidos: esfuerzo pequeño e impacto claro

1. PC: anotar alergias con la misma fila que el alta y vaciar los campos (H51). **Primero de la lista.**
2. Estación: que «Por revisar en el pase» diga qué hacer: «no lo emplates hasta que decida» (H34, solo texto).
3. Banda «listo»: escribir la alergia o el plato adaptado del pase (H19).
4. «Abrir mesa»: preguntar antes de perder las alergias escritas y escribirlas enteras encima del botón (H06). Hoja de alergias: no cerrarla si se rechaza (H03).
5. Pase: «Rehacer» en dos pasos y separado (H53), «No le afecta a nada más» arriba (H56), y no ofrecer «Sin gluten» fuera del gluten (H48).
6. Móvil: mostrar el resultado 1 o 2 segundos en el botón grande antes de la acción siguiente (H20). Orden sin confirmar: llevar la vista al aviso y poner una línea bajo el botón apagado (H21).
7. PC: motivo dentro de cada acción y vaciado al cambiar de mesa; retirar alergias en dos pasos (H61). Aviso de estación distinta al conectar (H64). Nada preelegido en Caja (H65).
8. Cocina: marca «NUEVA» y franja «SIN SONIDO» en ámbar (H36); pasos exactos para la pantalla siempre encendida (H37); comprobación del sonido en iPad (H38).
9. Textos que orientan: «¿Te has equivocado?» (H22), «Todo servido · la cuenta y liberar, en Caja» (H25), «Terminar y liberar es del responsable» (H59), el título de «Entrar como responsable» (H71) y rechazos sin código (H27).
10. Pulido barato: «1 copa · 1 botella en la ronda» (H13), «0 de 2 pases» (H15), respuesta visual propia al tocar (H16, H33) y letra más grande solo en la tableta vertical de sala (H05).

### 5b. Cambios mayores, para planificar como cortes

- **Sala: avisos que se ven y vuelta atrás segura.** Píldora fija de «listo» (H18), banda que no mueve nada bajo el dedo (H23), aviso de «revisada por cocina» (H24), orden sin confirmar a la vista (H21), «Atrás» del sistema y recarga sin perder nada (H01) y un botón «⋯» para «Más opciones» (H14). Objetivo medible: con la banda puesta, al menos 4 mesas a la vista.
- **Pase con carga.** Quitar el desplazamiento dentro de otro (H50), apartado «Alergias por decidir» (H47), formularios fuera de las comandas y barra fija de lo urgente (H49, H54), hora de espera y orden por antigüedad en «Por revisar» (H46), y «Esperando al camarero» con color y nombre (H55). Medirlo con 15 a 20 comandas.
- **Estación con volumen y cambios.** Barra de «más abajo» (H35), sonido y marca de los cambios de alergia (H34), deshacer «Listo» mientras el pase no valide (H33), tono que se repite (H36), letra a distancia tras la prueba (H39) y tarjeta que avisa sin red (H42).
- **Cierre de mesa y personal en el PC.** Terminar y liberar desde el puesto o con entrada directa (H59), cobrar y cerrar encadenados (H60), PIN por persona (adelantar E5) (H63), órdenes para corregir errores (H22) y lo que va tarde en ámbar (H62).
- **Ya planificados, mantenerlos:**
  - E4g, la comanda en borrador. Resuelve H10, H11 y H12; conviene añadirle «Todos lo mismo» y «Faltan 2 de 4 por elegir».
  - E4f, «Menú de hoy». Resuelve H67.

### 5c. Decisiones para revisar contigo

Son cosas que aprobaste y que la revisión cuestiona. Ninguna es un error; decides tú.

1. ¿Un tono opcional en el iPhone para «listo para servir»? Hoy en sala no suena nada y el iPhone no vibra (H18).
2. ¿Que la banda flote o se integre en la barra de abajo? Hoy ocupa su sitio arriba (H18, H23).
3. ¿Adelantar «deshacer Listo»? Hoy no existe por decisión (H33).
4. ¿Que suene en el pase un comensal nuevo por revisar? Hoy no suena por decisión (H46).
5. Seguridad alimentaria en el motor: ¿se puede marcar «Listo» con una revisión pendiente? ¿«Adaptado» devuelve el plato a la estación? (H34)
6. ¿Atajos de nota según la alergia, en lugar de los tres fijos? (H48)
7. ¿Ordenar el pase por «se puede validar» o «algo por decidir», en vez de solo por antigüedad? (H49)
8. ¿Guardar en el aparato la ronda y la mesa a medio abrir? Hoy se decidió que no (H01).
9. La regla de numeración de los comensales en tu local (H02).
10. ¿Puede el puesto fijo terminar y liberar mesas? ¿Se adelanta el PIN por persona? (H59, H63)
11. Tamaños de letra y densidad de la maqueta aprobada: tarjetas de mesa, pase y distancia en cocina (H04, H05, H39, H57).
12. Palabras: «pase» con dos sentidos y «Servido» frente a «Marcar servido» (H29, H31). Formato «× 4 (1 adaptada)» y «Rehecho» (H44, H40).
13. El aviso de almacenamiento siempre al pie (H45) y «Cambiar» en la franja de alergias (H09).
14. Un aviso de «pide la cuenta» sin importes, frente a la regla de no llevar nada de dinero al móvil (H25).
15. ¿Se hace la prueba en aparatos reales antes o después de E4f? (H70)

---

## 6. Lo que una revisión de escritorio no puede saber, y una prueba de 30 minutos para averiguarlo

Desde la mesa no se sabe:
- si el icono del iPhone abre a pantalla completa con la conexión sencilla;
- si el «Atrás» de Android cierra la aplicación;
- si la tablet se apaga en pleno servicio;
- si el tono se oye con la campana encendida, y si el iPad suena en silencio;
- si «marisco» se lee a 1 o 2 metros;
- si el personal cuenta igual a los comensales;
- si el doble toque llega a mandar un pase de más;
- cuánto tarda alguien nuevo en enterarse de un «listo para servir».

**Quién:** 2 o 3 camareros, uno de ellos alguien que no la haya usado nunca, con sus propios móviles: al menos un Android y un iPhone. Además, 1 cocinero en una estación y 1 en el pase, el encargado en el PC y una persona que observa con cronómetro y una hoja. Si se graba, con permiso.

**Montaje, antes de empezar (no cuenta en los 30 minutos):**
- En el local o en un ensayo, con la conexión sencilla de verdad.
- Las tablets fijas en su sitio, a 1 o 2 metros, con la campana y el lavavajillas encendidos.
- El encargado abre antes 6 a 8 mesas, para que cocina tenga 8 a 12 comandas a la vez.
- Una tablet se deja sin tocar desde el principio, para ver si se apaga.

**Minutos 0 a 5, arranque:**
- El camarero nuevo empareja su móvil sin ayuda.
- Se empieza el turno en las tablets.
- Con el iPad en silencio, se comprueba si suena una comanda nueva.

**Minutos 5 a 22, servicio con tarjetas de tareas.** Se reparten en papel, sin explicar cómo se hacen.

Camareros:
1. Abrir la Mesa 3, de 4 personas, sin alergias, y enviar el primer pase.
2. Abrir la Mesa 5, de 3 personas: «el comensal 2 es alérgico grave a los frutos secos». Otro camarero sirve ese pase: ¿a quién lleva el plato adaptado?
3. Ronda para la Mesa 3: 2 copas de albariño y 1 botella.
4. Con la ronda a medias, usar el «Atrás» del Android, o cambiar de aplicación en el iPhone, y volver: ¿sigue la ronda?
5. Estar en Bodega con la lista bajada cuando el pase valida otra mesa, sin avisarle: ¿cuánto tarda en enterarse?
6. «Un cliente de la Mesa 3 avisa tarde: intolerancia a la lactosa.»
7. Error provocado: «has abierto la Mesa 6 y era la 7». ¿Sabe qué hacer?
8. Con la Wi-Fi del móvil apagada 30 segundos, enviar un pase; luego encenderla.

Estación:
9. Con 8 comandas o más, encontrar la que acaba de sonar y marcar «Listo» con guantes o las manos mojadas.
10. Leer la sustancia de una alergia desde 1 metro y desde 2, sin acercarse.
11. Cuando entre la alergia de la tarea 6 sobre una comanda ya leída: ¿se entera sin que nadie se lo diga?

Pase:
12. Revisar las alergias de la Mesa 5. Cronometrar desde que se abre la mesa hasta que el camarero puede enviar.
13. Decidir la alergia tardía de la tarea 6 y validar ese pase.

Encargado en el PC:
14. Anotar dos alergias seguidas en dos mesas abiertas: primero una preferencia leve, después una alergia grave. ¿Con qué gravedad sale la segunda?
15. En modo de puesto, terminar y liberar una mesa servida; después, cobrar otra.

**Minutos 22 a 30, preguntas:** cada persona dice qué le costó, dónde dudó y qué cambiaría, y pone una nota del 1 al 5. Con los resultados se rellenan las hojas de prueba en aparato que ya existen para las tres últimas entregas y que están en blanco.

**Qué medir:**
- **Tiempo por tarea**, desde que lee la tarjeta hasta que termina. Referencias propuestas, a ajustar: abrir una mesa con alergia, menos de 40 segundos; enviar un pase o servir, menos de 5.
- **Errores:** toques equivocados, órdenes repetidas, plato adaptado al comensal equivocado, alergia perdida o con gravedad equivocada, y «Listo» o «Validar» en la mesa que no era.
- **Dudas:** cuántas veces pregunta o mira al encargado, y en qué pantallas se queda parado más de 5 segundos.
- **Avisos:** tiempo desde «listo para servir» hasta «Servido»; si oye el tono de cocina con la campana; si la tablet se apagó; si el iPad sonó en silencio.
- **Aparatos:** si el icono del iPhone abre a pantalla completa y si «Atrás» cierra la aplicación en Android.
- **Lectura:** si lee la sustancia a 1 metro y a 2.

**Criterio de corte:** cualquier alergia perdida, asignada a otro comensal o con la gravedad equivocada se arregla antes de usarlo en un servicio real.

---

## 6b. Lo que añadió la revisión final (comprobado en el código el 28-10)

Un crítico final buscó lo que faltaba. Estos ocho huecos están comprobados y no tienen número H.

1. **Con una sola estación de sala, todos los móviles reciben todos los avisos, y nada dice quién va a por el plato** (alta).
   - Es lo que ya dice ADR-018 en sus consecuencias: en Costiña, «Mis mesas» y «Todas» enseñan lo mismo.
   - Cualquiera toca «Servido» desde la banda: o van dos camareros, o no va nadie.
   - Las pruebas y la medición usan dos estaciones, así que «Mis mesas» se ha medido más limpia de lo que se verá en Costiña.
   - Propuesta: en la banda, «Lo llevo yo», que quite el aviso de los demás móviles. Va unido a atribuir la mesa a una persona (E5, el PIN).
2. **La carta no sabe qué alérgenos lleva cada plato** (alta, decisión de producto).
   - Un plato es solo un nombre: no hay ningún campo de alérgenos en el motor ni en la aplicación.
   - Ante «¿esto lleva frutos secos?», el camarero tiene que ir a cocina.
   - Por eso el motor manda a revisión TODOS los platos del comensal con alergia, y esa es la causa de fondo del atasco del pase (H50, H56).
   - La normativa obliga a informar de los 14 alérgenos; hoy esa información tiene que vivir fuera de Costiña.
   - Propuesta: un corte propio con los alérgenos por plato. Con eso, el pase solo revisaría los platos que de verdad chocan.
3. **No hay nota de cocina que no sea una alergia** (media).
   - «Al punto», «sin sal» o «para compartir» solo caben como «Preferencia».
   - Una preferencia leve manda a revisión del pase cada plato de ese comensal, con nota obligatoria.
4. **Pausar una mesa no llega bien a cocina** (media).
   - Cocina solo lee «Mesa en pausa». No suena, el «Lleva N min» sigue corriendo y pasa a ámbar, y «Listo» sigue activo.
   - Ninguna prueba recorre la pausa en cocina.
5. **La ronda de bebidas no llega a ninguna pantalla de barra** (media; aprobado «por ahora»).
   - Quien la pide la prepara o la avisa de palabra.
   - Una ronda sin enviar se pierde sin aviso si se libera la mesa en el PC.
   - Con la ronda a medias, «Enviar ronda» y «Enviar siguiente pase» van uno encima del otro, y el segundo no tiene vuelta atrás.
6. **Las bebidas no pasan por la revisión de alergias** (media).
   - Los sulfitos del vino o la leche de un café van sin freno; la alergia solo se ve como un punto al editar la línea.
   - La regla «ningún plato afectado sale sin decisión de cocina» cubre los platos, no las rondas.
7. **«Abrir mesa» empieza en 2 personas y con el menú del día ya elegido** (media).
   - Con dos toques se abre una mesa de 4 como de 2.
   - Las cantidades de cocina salen de las personas, y desde el móvil no se cambian (H22).
   - Propuesta: empezar sin número, o con la capacidad de la mesa.
8. **Una estación sin tablet** (media).
   - Existe la salida: el pase marca por ella desde «Más opciones».
   - Pero esos cocineros se quedan sin lista ni líneas de alergia, no hay impresora y no se ha probado.

**Cifras del informe que el crítico corrigió**, ya cambiadas arriba:
- «1.020 objetivos táctiles»: era la suma de las mismas pantallas contadas muchas veces.
- «109 de 138 acciones fuera de la vista»: sumaba cinco estados de la misma página.
- La comanda con alergia mide casi media pantalla, no más.
- La orden sin red se reintenta sola solo con la aplicación abierta.
- Lo bueno del iPhone se vio imitándolo, no en un iPhone.
- La tablet también suena con «Rehacer».
- En Sala fueron siete, no cuatro, los problemas rebajados de alta a media.

---

## 7. Anexo de evidencias

**Dónde está todo:**
- Repositorio: `Rivadesa/Costi-a`, rama `develop`, commit `a1ca7f6`.
- Sala y Cocina: `frontend/pwa/src/` (pantallas en `views/`); pruebas en `frontend/pwa/e2e/`.
- PC: `dotnet/src/Costina.Desktop/`. Motor: `dotnet/src/Costina.Modules.Dining*/`.
- Documentos de cada corte: `docs/native/`. Planes y maquetas: `docs/plans/` y `docs/plans/maquetas/`. Decisiones de arquitectura: `docs/ADR/`.
- Capturas y medidas: 144 pantallas (PNG y JSON con los objetivos táctiles, sus medidas y lo que queda fuera de la vista), tomadas con una copia temporal de las pruebas de navegador contra el motor de laboratorio. No se guardan en el repositorio. Sus nombres siguen `<aparato>__<historia>__<n>__<pantalla>`, y así se citan abajo.

En la columna de severidad, «A→B» es la gravedad propuesta y la que quedó tras comprobarla.

| Id | Severidad | Evidencia principal |
|---|---|---|
| H01 | alta (=) | TableCard.vue:117-131, 210-211; OpenTable.vue:87, 127; solo replaceState en Shell.vue:95 y PairView.vue:49; bodega.ts:76; C3c-dos-apps.md:77; flecha de volver en y=55 (y=-437 en mobile__mesas__044.json) |
| H02 | alta→media | labels.ts:80; OpenTable.vue:187-192; AllergySheet.vue:133-138; mobile__mesas__044.png |
| H03 | alta→media | AllergySheet.vue:47-53, 70-80; commands.ts:71, 248; C3e-costina-sala.md:83; TableCard.vue:198-204 |
| H04 | media→baja | mobile__mesas__042.json y __050.json; mesas.ts:62-67; maqueta 2026-10-22:686, 691, 996 |
| H05 | media→baja | style.css:137-152, 633-662; maqueta 2026-10-22:97, 135, 151-152 |
| H06 | media | OpenTable.vue:127; BoardView.vue:122-124, 187; ReadyBanner.vue:7-9; abrir.ts:125-129 |
| H07 | media→baja | OpenTable.vue:200-202; AllergySheet.vue:146-147; cocina.ts:420-424 |
| H08 | media→baja | mobile__mesas__044.json; mobile__drinks__022.json; DrinksPanel.vue:56-57 |
| H09 | media→baja | TableCard.vue:149-153; AllergySheet.vue:100-157; images/c3e/sala-alergias-375x812.png; maqueta:718 |
| H10 | media→baja | DishPanel.vue:54-58, 95; operations.ts:35-38; C3e-costina-sala.md:88, 470-471 |
| H11 | media→baja | DishPanel.vue:70-74, 121; operations.ts:55; ServiceViewModel.cs:473 |
| H12 | media→baja | DiningOperations.cs:133-161; DishPanel.vue:16-17; plan 2026-10-22:651, 822, 871 |
| H13 | media→baja | style.css:532-534; DrinksPanel.vue:106; maqueta 2026-10-15-comandero:261-264 |
| H14 | baja | mobile__mesas__052.json; mobile__drinks__028.json; sala-vertical__mesas__013.json |
| H15 | baja | style.css:92-104, 860-865; TableTile.vue:62; labels.ts:43-44 |
| H16 | baja | mobile__mesas__044.png; mobile__drinks__023.png; style.css:26 |
| H17 | baja | banner.spec.ts:73; board.spec.ts:36; captures-c3e.spec.ts:320 |
| H18 | alta→media | BoardView.vue:178; style.css:213, 468, 494; sala.ts:96-106; C3c-dos-apps.md:129, 137; mobile__mesas__050.json |
| H19 | alta→media | ReadyBanner.vue:34-47; pases.ts:74-78; mobile__mesas__050.png y __052.png; maqueta:670-673 |
| H20 | alta→media | mobile__commands__011/012.json; mobile__mesas__052/053.json; operations.ts:36-38, 46-64; CourseExecution.cs:136-142 |
| H21 | alta→media | BoardView.vue:177; StatusPanel.vue:31-62; operations.ts:38; ReadyBanner.vue:40; mobile__commands__014 (.png y .json) |
| H22 | alta→media | Affordances.cs:26; DiningOperations.cs:131-161; ficha.ts:70-74; plan 2026-10-15:939 |
| H23 | media | mobile__mesas__042/049/050.json; mobile__drinks__021.json; mobile__banner__002.json; mesas.ts:20-27; BoardView.vue:116-119 |
| H24 | media | sala.ts:47-54; mesas.spec.ts:114-115, 147-163; TableTile.vue:64-66; mobile__mesas__045.png |
| H25 | media→baja | Affordances.cs:26; ficha.ts:70-74; ADR-007:80 |
| H26 | media→baja | StatusPanel.vue:34-41, 58; Shell.vue:145; ficha.ts:113-115 |
| H27 | media→baja | commands.ts:70-103, 248; Labels.cs:85, 144-170; DiningOperations.cs:128, 147, 176 |
| H28 | media→baja | operations.ts:55; commands.ts:71; cocina.ts:353; tableta__kitchen__009 (.png y .json); C3d-costina-cocina.md:178 |
| H29 | media→baja | sala.ts:82; ficha.ts:85; pases.ts:90; KitchenView.vue:353; maqueta:519, 672 |
| H30 | media→baja | mobile__mesas__050.png; mesas.ts:47; DevicesViewModel.cs:497 |
| H31 | baja | ficha.ts:16-21, 90; labels.ts:43-45; Labels.cs:61-63; plan 2026-10-22:756 |
| H32 | baja | StatusPanel.vue:58; commands.ts:197-205 |
| H33 | alta→media | KitchenView.vue:65-66, 93-96, 217-219; AllowedActions.cs:58-61; tableta__kitchen__008.json; ADR-017:43; plan:822, 871 |
| H34 | alta (=) | cocina.ts:150, 330-348; AllowedActions.cs:59-60; CourseExecution.cs:181-189; kitchen.spec.ts:206-210, 262-263; tableta__kitchen__006/008/009.png |
| H35 | alta (=) | tableta__kitchen__008.json y __009.json; style.css:278; cocina.ts:146-149, 183; images/c3d/cocina-empezar-turno-1180x820.png |
| H36 | media | turno.ts:28-53; KitchenView.vue:164-167, 191-221; style.css:269-273 |
| H37 | media | turno.ts:55-69; KitchenView.vue:158-160, 185; C3d-costina-cocina.md:74, 322; tableta__kitchen__008.png |
| H38 | media | turno.ts:37-38, 74; KitchenView.vue:7-11, 169-171; C3d-costina-cocina.md:323 |
| H39 | media | style.css:248-250, 281-305; tableta__kitchen__008.json; C3d-costina-cocina.md:147, 230 |
| H40 | media→baja | labels.ts:106-109; cocina.ts:88-91; CourseExecution.cs:186-189 |
| H41 | media→baja | KitchenView.vue:207-209, 217-219; images/c3d/cocina-estacion-1180x820.png |
| H42 | media→baja | StatusPanel.vue:23-41; operations.ts:28-31, 38, 42; live.ts:80, 121-126 |
| H43 | baja | KitchenView.vue:214; cocina.ts:214-223 |
| H44 | baja | cocina.ts:46, 67, 84-95; plan 2026-10-22:642 |
| H45 | baja | HomeView.vue:29-31; storage.ts:23-28; D6.6-hardening.md:24-26 |
| H46 | alta→media | cocina.ts:346-348, 395-396; KitchenView.vue:248-253; C3d-costina-cocina.md:67, 168; mobile__mesas__045.json; tableta__kitchen__004.png |
| H47 | alta→media | cocina.ts:146-149, 225-251, 276-277; KitchenView.vue:71, 329-345; tableta__kitchen__010.json |
| H48 | alta→media | cocina.ts:420-430; KitchenView.vue:109, 261-280; labels.ts:105-108; tableta__kitchen__012.png; plan 2026-10-22:667 |
| H49 | alta→media | tableta__kitchen__004/010/011.json; style.css:377; C3d-costina-cocina.md:174 |
| H50 | alta (=) | style.css:411-422; tableta__kitchen__004 (.png y .json); tableta__kitchen__012.png; C3d-costina-cocina.md:194 |
| H51 | alta (=) | ServiceViewModel.cs:141-144, 303-310, 530-547; ServiceView.xaml:438-452; FloorPlan.cs:99-101 |
| H52 | media→baja | KitchenView.vue:101-104, 352-353; tableta__kitchen__011; CourseExecution.cs:117-131 |
| H53 | media | tableta__kitchen__010 (.png y .json); style.css:357-359, 366, 407-409; AllergySheet.vue:27-30 |
| H54 | media→baja | cocina.ts:275-277; KitchenView.vue:71, 177; tableta__kitchen__004.png y __010.png |
| H55 | media→baja | cocina.ts:323-328; style.css:372; tableta__kitchen__012.png |
| H56 | media | KitchenView.vue:255-281; tableta__kitchen__004.json; E4e-alergias-antes-de-enviar.md:31 |
| H57 | media→baja | style.css:305, 351, 369, 380, 390, 393, 396; tests/views.test.ts:153 |
| H58 | media→baja | HomeView.xaml:56-70, 184-210; ServiceView.xaml:117-139, 380; images/c2/home.png |
| H59 | alta→media | Affordances.cs:26; DevicesViewModel.cs:466; ServiceViewModel.cs:223, 245-249; HomeViewModel.cs:108; images/c2/service-puesto.png |
| H60 | alta→media | CheckoutView.xaml:130-139; CheckoutViewModel.cs:427-439, 481-490; DiningOperations.cs:153, 289; ADR-009 |
| H61 | media | ServiceViewModel.cs:136, 293, 303-313, 553-562; ServiceView.xaml:317-323, 430-431; CheckoutViewModel.cs:46-48 |
| H62 | media→baja | ServiceView.xaml:114-118; FloorPlan.cs:38-49; handoff 2026-10-27:111 |
| H63 | media→baja | ShellViewModel.cs:20, 164, 532-537; plan 2026-09-24:134, 154; Costina.Server/Program.cs:125-133 |
| H64 | media | DevicesViewModel.cs:235-237, 455-473; C3e-costina-sala.md:18-25, 146 |
| H65 | media | CheckoutViewModel.cs:64, 83-92, 278, 414; images/e3.3/checkout.png |
| H66 | media→baja | Labels.cs:287-289; Models.cs:18-19; DevicesViewModel.cs:161-162, 430, 567; DevicesView.xaml:165-195 |
| H67 | media | OffersView.xaml:145, 216-218, 282-294; OffersViewModel.cs:89-107; images/e4d/offers-menu.png |
| H68 | media→baja | PairView.vue:26, 49, 97, 111; C3c-dos-apps.md:72-76, 86-91, 100 |
| H69 | media→baja | mobile__install__032 (.png y .json); storage.ts:27; C3c-dos-apps.md:93-98, 441 |
| H70 | media | usab/LEEME.md; playwright.config.ts:17-30; install.spec.ts:11-20; C3c-dos-apps.md:419-441; C3d:293-298; C3e:328, 446-456 |
| H71 | baja | MainWindow.xaml.cs:92; ShellViewModel.cs:160, 164, 356-361 |
| H72 | baja | CheckoutViewModel.cs:144-148, 391, 429-439 |
| H73 | baja | MainWindow.xaml:87-89; ServiceViewModel.cs:655-656; ShellViewModel.cs:486-519 |
| H74 | baja | PairView.vue:112; storage.ts:25, 27; DevicesViewModel.cs:116 |
| H75 | baja | PairView.vue:92-93; apps.ts:79-84; C3c-dos-apps.md:84 |
| H76 | baja | PairView.vue:64-66, 117-128 |

**Hallazgos descartados:** ninguno. Los 76 se confirmaron: 42 se rebajaron de gravedad, ninguno se subió, y la cuenta final queda en 5 altos, 28 medios y 43 bajos.

**Afirmaciones que la comprobación corrigió**, para no repetirlas:
- H03: no es «la única hoja así»; la hoja de omitir pase también se cierra antes de la respuesta.
- H07: los atajos de cocina «Sin <sustancia>» no existen; el único es «Sin gluten», fijo.
- H26: la cifra de «82 pantallas» no se puede verificar.
- H34: con el pase ya enviado, el contador del pase no sube; si el pase ya estaba validado, sí suena.
- H42: sin red sí hay un aviso ámbar general.
- H46: no es cierto que el único indicio sea un contador pequeño; la columna tiene título y contador fijos.
- H60: terminar la mesa no pide motivo y cerrar la cuenta no es necesario para liberarla.
- H63: sí se pueden crear más usuarios, aunque solo por línea de órdenes.
- H66: el aviso de nombre repetido no impide aprobar el aparato.
- Citas de líneas corregidas: H06 (abrir.ts:125-129), H20 (operations.ts:36-38), H23 (mesas.ts:20-27 y BoardView.vue:116-119), H32 (StatusPanel.vue:58), H33 (operations.ts:35-38) y H69 (storage.ts:27).