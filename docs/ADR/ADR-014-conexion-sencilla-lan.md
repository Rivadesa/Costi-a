# ADR-014 — Conexión sencilla por la red local (HTTP provisional) y certificado público después

**Estado:** aceptado (2026-10-14). Decisión del promotor del 14-10-2026, tras no poder conectar un móvil en su PC: «en el restaurante no podemos andar moviendo certificados ni instalando en dispositivos». Supersede en parte a D5.3 (`docs/native/D5.3-lan-https.md`): «la LAN se sirve únicamente por HTTPS».

## Contexto

Desde D5.3 la red local se sirve solo por HTTPS, con un certificado firmado por una CA propia de cada instalación. Para que un móvil o una tablet confíen en él hay que **instalar esa raíz en cada dispositivo**: copiar `ca.crt`, comparar la huella e instalarla. En iOS, además, hay que activar la confianza total a mano. Además el QR usa el nombre `equipo.local`, que muchos Android no resuelven.

En el PC del promotor, el móvil leía el QR y se quedaba en una página en blanco. Se juntaban tres causas:
- la red de Windows estaba en «Pública», así que la regla de firewall de Costina no se aplicaba;
- el móvil no tenía la raíz instalada;
- `GET /ca.crt` devolvía 404 en una instalación real: el servicio no podía leer `tls\ca.crt`.

Arreglar las tres no cambia lo esencial: el camarero o el encargado tendrían que instalar un certificado en cada dispositivo. Para un producto que se vende, eso no vale.

Ya en D5.3 quedó documentada la alternativa: un certificado **público** con un dominio propio que apunte a la IP privada, sin instalar nada en los dispositivos. Necesita un dominio, un pequeño servicio en internet que emita y renueve los certificados (DNS-01), e internet al instalar y en cada renovación.

## Decisión

1. **Modo provisional, ya: conexión sencilla por HTTP.**
   - El motor escucha HTTP en la red en un puerto propio: `COSTINA_LAN_HTTP_PORT`, por defecto **5089**; `0` = sin escucha.
   - El QR lleva `http://IP-PRIVADA:5089/app/#pair=CÓDIGO`, con la IP del adaptador de la red del local. Nunca un nombre: podría resolverse fuera del local y, si el dispositivo ya visitó ese nombre por HTTPS, su navegador lo forzaría a HTTPS (HSTS).
   - **Apagada por defecto.** La enciende y la apaga solo el responsable (orden `lan-http-set`, auditada y con efecto inmediato, sin reiniciar). El puesto principal confirma en dos pasos y mantiene visible un aviso mientras está activa.
   - **Apagada**, el puerto solo contesta con una página que lo explica, antes de la identidad y de la PWA.
   - **Encendida**, sirve exactamente lo mismo que el HTTPS: identidad, permisos, freno de intentos y PWA. Solo atiende a direcciones privadas, aunque un router reenvíe el puerto.
   - La regla de firewall propia (`Costina-HTTP-LAN`) abre el puerto solo en redes **privadas o de dominio**, igual que la de HTTPS.
2. **Siguiente corte: certificado público con dominio propio.** Cuando esté, la conexión sencilla se retira o queda como último recurso, y se actualiza este ADR.
3. **HTTPS con CA local** sigue disponible y sin cambios para quien lo quiera (otros puestos Windows con `import-ca.ps1`, dispositivos con la raíz instalada). Se corrige el permiso de lectura de `ca.crt` para el servicio.
4. **Red «Pública» en Windows:** el puesto principal lo detecta y explica cómo cambiarla. El programa nunca cambia ese ajuste por su cuenta.

## Riesgos aceptados por el promotor

Mientras la conexión sencilla esté encendida, el tráfico entre el PC y los dispositivos **no va cifrado**: contraseñas de responsable tecleadas en un dispositivo, tokens de dispositivo, billetes del tiempo real y datos operativos. Cualquiera conectado al **mismo Wi-Fi** podría verlos. Por eso:
- se recomienda una red Wi-Fi exclusiva del personal, nunca la de los clientes;
- la PWA (que nunca recibe importes, por ADR-007) dice que la conexión es sin cifrar;
- en HTTP el navegador no deja instalar la aplicación ni guardar datos protegidos. La PWA funciona igual y lo dice.

## Alternativas descartadas

- **Seguir solo con la CA local:** hay que instalar la raíz en cada dispositivo. Es justo lo que el promotor no acepta.
- **Excepción de certificado en el navegador** («continuar de todos modos»): enseña al personal a saltarse avisos de seguridad, iOS no lo permite para el tiempo real y el service worker no se registra.
- **Un único certificado público comodín incluido en el instalador:** distribuiría su clave privada en cada copia y la autoridad lo revocaría. Cada instalación necesita el suyo, que es el siguiente corte.
- **App nativa con la raíz fijada:** también obliga a instalar algo en cada dispositivo, y en iOS pasa por la App Store.
- **HTTP abierto sin interruptor:** el riesgo quedaría activo sin que nadie lo decidiera.

## Consecuencias

- Esquema **v8**: `core.installation.lan_http` (ajuste de la instalación, no de un ámbito), y `grants.sql` concede al rol de ejecución `UPDATE (lan_http)`.
- `GET /api/native/v1/installation/network` (solo main) da al puesto principal:
  - el estado de la conexión sencilla;
  - las IPv4 privadas del equipo, sin las de adaptadores virtuales;
  - las redes de Windows con su tipo.
- `POST /api/native/v1/installation/commands/lan-http-set` (solo main, idempotente, auditada como `installation.lan_http_enabled|disabled`).
- Una instalación escucha en 5089 aunque esté apagada (solo con su aviso). Las pruebas que arrancan una instalación ponen `COSTINA_LAN_HTTP_PORT=0` salvo la que lo comprueba: en un PC de desarrollo con Costina instalado, el 5089 es de la instalación real.
- La regla «HTTP solo en loopback, siempre» de D5.3 queda sustituida por esta: HTTP fuera del propio equipo solo por el puerto de la conexión sencilla, solo encendida por el responsable y solo desde direcciones privadas. El 5088 sigue solo en loopback.
