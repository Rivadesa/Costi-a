# Primera prueba de escritorio — Windows + modo demo

Este documento describe el primer corte instalable de Hospitality OS. Su objetivo es validar la experiencia operativa de sala/cocina antes de desplegar el servidor local Laravel + PostgreSQL en un establecimiento.

## Qué valida este corte

- Instalación real como aplicación Windows mediante Tauri/NSIS.
- Arranque sin navegador.
- Navegación entre Control de servicio y KDS.
- Tablero de mesas activas.
- Apertura de una mesa nueva.
- Menú y posiciones PAX.
- Restricciones por comensal.
- Envío de pases.
- Trabajo de preparaciones por partida.
- Validación de pase listo.
- Marcado de pase servido.
- Bebidas/extras y cuenta provisional.
- Registro básico de cobro y cierre.

No valida todavía persistencia PostgreSQL, sincronización cloud, impresoras, datáfono ni fiscalidad.

## Modo demo

En la pantalla de acceso pulsar **Entrar en modo demo**. No requiere servidor, Internet ni credenciales.

El demo carga:

- 8 mesas.
- Menú Experiencia y Menú Temporada.
- Estaciones Fríos, Calientes, Pescados, Carnes, Postres y Pase.
- Tres servicios en distintos puntos del menú.
- Una alergia crítica a marisco en PAX 2 de Mesa 1.

El demo usa el mismo contrato lógico que la API local. No son pantallas estáticas: las acciones modifican un estado persistente almacenado en el equipo.

## Guion de prueba recomendado

1. Entrar en demo.
2. Abrir **Mesa 1** y comprobar la alerta crítica.
3. Ir a **Cocina / KDS** y recorrer las partidas hasta encontrar el pase activo de Mesa 1.
4. Marcar una elaboración como LISTO y comprobar el cambio de estado.
5. Completar todas las elaboraciones del pase y pulsar **VALIDAR PASE LISTO**.
6. Volver a Control de servicio, entrar en Mesa 1 y **Marcar servido**.
7. Enviar el siguiente pase y comprobar que aparece en KDS.
8. Añadir una bebida o extra.
9. Abrir una mesa nueva y comprobar creación automática de posiciones PAX.
10. Añadir una restricción crítica a un comensal y verificar que se muestra en las elaboraciones de ese PAX cuando el pase llega a cocina.

## Restablecer el demo

Cerrar sesión y volver a pulsar **Entrar en modo demo**. El botón reinicializa los datos de demostración.

## Servidor real

El mismo instalador permite usar el backend real. En la pantalla de login abrir **Configuración del terminal** e introducir la URL de la API local, por ejemplo:

`http://192.168.1.20:8000/api/v1`

En despliegue final se pretende sustituir la IP manual por descubrimiento/configuración del servidor local.

## Build

Workflow: `.github/workflows/windows-installer.yml`

El workflow compila en `windows-latest` y publica dos artefactos temporales:

- `hospitality-os-windows-installer`: instalador NSIS `.exe`.
- `hospitality-os-windows-portable`: ejecutable directo cuando esté disponible.

El instalador de desarrollo no debe considerarse una release de producción y puede no estar firmado digitalmente, por lo que Windows SmartScreen puede advertir sobre editor desconocido.

## Qué observar durante la prueba

Anotar especialmente:

- acciones que requieran demasiados clics;
- textos poco claros durante un servicio;
- información que sala o cocina necesite ver y no aparezca;
- tamaño de botones/legibilidad;
- estados que no se entiendan a primera vista;
- diferencias entre el ritmo real del restaurante y el flujo propuesto;
- excepciones que obligarían a salir del flujo normal.

El objetivo de esta prueba no es aprobar el diseño visual, sino descubrir fricción operativa antes de consolidar V1A.
