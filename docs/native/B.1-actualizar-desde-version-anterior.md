# B.1 — Actualizar desde una versión anterior: la copia previa admite una base en una versión más antigua

Fecha: 2026-10-14. Rama `fix/b1.1-actualizar-desde-version-anterior` sobre `develop` (`9956cfb`, corte B mezclado en PR #69). Corte correctivo. Versión **`0.33.1-b1.1`**. **Sin cambio de esquema ni de `grants.sql`.**

## El fallo

El promotor actualizó su instalación de prueba (instalada el 23-09 con `0.29.0-e4`, esquema v4) con `Costina-Setup-0.33.0-b1.exe` (esquema v6). El instalador terminó con «No se pudo configurar el servidor (código -532462766). Revisa los detalles. Los datos existentes no se han tocado.» y el servicio `Costina` quedó **parado**: los programas ya eran los nuevos y la base seguía en v4.

Causa (registro de eventos de Windows, `.NET Runtime` 1026): `setup-server` actualiza con los binarios NUEVOS en dos pasos, `backup` y después `upgrade` (`ServerSetup.InstallAsync`). El comando `backup` empezaba con `store.CheckAsync`, la comprobación de arranque que exige que la base esté **ya** en la versión de estos binarios: «The database schema is not version 6: run upgrade with these binaries.» La copia previa a una actualización se negaba siempre, así que **ninguna actualización con cambio de esquema podía completarse** (de E1b en adelante, todas). Los datos no se tocaban (la copia va antes que `upgrade`), pero el servicio no volvía a arrancar. El código `-532462766` es `0xE0434352`: la excepción no capturada del proceso hijo y luego la de `setup-server`.

No lo vio ninguna prueba: `installer_checks.ps1` reinstala el **mismo** paquete sobre sus datos (sin cambio de esquema), y `packaging_http.test_06e` migraba una base v1 con `upgrade` directamente, sin la copia previa que hace el instalador.

## Qué cambia

- **`backup` admite una base en una versión ANTERIOR** (`Program.cs`, `PostgresStore.StoredSchemaVersionAsync`): lee la versión guardada sin exigir la de estos binarios. En la versión actual sigue comprobando todo como antes (`CheckAsync`). En una versión anterior del núcleo (v2 en adelante), copia los esquemas `core` y de cada módulo con el manifiesto de formato 2, como siempre. En una base v1 (esquema único `native_d1`), copia en el **formato 1**, el mismo que `restore` ya sabe verificar y migrar (`BackupRunner.LegacyV1`). Una base **más nueva** que los binarios se rechaza: estos binarios no sabrían restaurarla. El mensaje dice que es la copia previa a la actualización y que se restaura con estos binarios.
- **`setup-server` explica el fallo** con su motivo (`SETUP FAILED: …` en los detalles del instalador) y sale con código **1**, en vez de terminar con una excepción sin capturar.
- **Instalador**: el aviso añade que, si era una actualización, el servicio queda parado hasta completarla y que hay que volver a ejecutar el instalador cuando se haya corregido la causa.

## Qué NO cambia

Esquema (v6), `grants.sql`, `upgrade` y su cadena `upgrade-v{n}.sql`, `restore` (ya verificaba copias de formato 1 y 2 y migraba después), la copia diaria del servicio (corre con la base ya en la versión del motor), el orden del instalador (copia y después esquema; si la copia falla, no se toca nada).

## Comprobaciones automáticas

`sh tools/agent/local-tests.sh` completo (puerto 5090). La historia nueva estaba **en rojo antes del arreglo**, con el mismo código `0xE0434352` que vio el promotor. `packaging_http.test_06e` reproduce ahora el orden del instalador sobre una base v1 **real** (los fixtures congelados de 0.26.0-e1 con las filas de la batería): `backup` con los binarios nuevos antes de `upgrade` → formato 1, la misma identidad de instalación y **las mismas huellas por tabla** que la copia de 0.26.0-e1; después `upgrade` hasta v6, como antes. Al final, sobre la base restaurada, una base del núcleo marcada en una versión anterior (5) se copia con el formato 2, y una marcada como más nueva (7) se rechaza («newer than these binaries»). La marca vuelve a 6.

## Para el promotor (instalación de prueba parada)

1. Descargar el instalador de `develop` con esta versión (`Costina-Setup-0.33.1-b1.1.exe`) y ejecutarlo encima, en el mismo modo. Hará la copia de la base v4, la actualizará a v6 y arrancará el servicio.
2. Al terminar: `Costina.Server.exe status` (consola de administrador) o abrir el programa del PC; la versión mostrada debe ser `0.33.1-b1.1`, y la última copia la de hoy.

Mientras tanto la base y sus copias diarias siguen intactas en `C:\ProgramData\Costina`.

## Límites

Una actualización desde una versión **más nueva** que el paquete (volver atrás) sigue sin poder hacerse: se rechaza antes de tocar nada. Las pruebas del instalador en CI siguen sin instalar un paquete anterior real antes del nuevo; la historia de `packaging_http` cubre el mismo orden de comandos con una base antigua real.
