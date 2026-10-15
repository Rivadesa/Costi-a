using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;
using Npgsql;
using static Costina.Core.Hosting.Requests;
namespace Costina.Server;

// Lecturas del NUCLEO. Uses the existing middleware and endpoint access metadata, without changing authentication.
public static class DesktopReadRoutes
{
    public static void MapDesktopReadRoutes(this WebApplication app,NpgsqlDataSource source,BusinessScope scope,Guid installation,string serverVersion,Func<bool> demo,
        IReadOnlyList<(IModule Module,ModuleHost Host)> modules)
    {
        var reads=new DesktopReadRepository(source);
        const string prefix="/api/native/v1";
        app.MapGet(prefix+"/session",(Func<HttpContext,IResult>)(c=>{
            var role=(string)c.Items["role"]!;
            return Results.Json(new {
                role,tenantId=scope.TenantId,companyId=scope.CompanyId,locationId=scope.LocationId,
                // Identidad estable de la instalacion y version del motor: el cliente aisla por ellas su
                // orden durable y muestra con que servidor habla. No es autorizacion.
                installationId=installation.ToString("D"),serverVersion,
                // D5.6: instalacion con datos ficticios de demostracion; el cliente lo muestra siempre.
                demo=demo(),
                // actor: usuario real con sesion (user:nombre) o rol de laboratorio (lab-rol). Auditable en tests.
                actor=(string)c.Items["actor"]!,
                // station: solo dispositivos emparejados (D4.2); acota lo que anuncian los modulos (D4.3).
                station=c.Items["station"] as string,
                // A4 (Inicio y dispositivos): nombre legible de esa estacion, para la cabecera de la tablet. Solo anade.
                stationName=c.Items["stationName"] as string,
                // ADR-012: modulos ACTIVOS en esta instalacion. Los clientes montan solo sus superficies.
                modules=modules.Select(m=>m.Module.Name).ToArray(),
                // Affordances de sesion: acciones globales (no ligadas a un agregado) que este rol puede iniciar, por modulo.
                actions=modules.SelectMany(m=>m.Module.SessionActions(role)).Distinct(StringComparer.Ordinal).ToArray()
            });})).WithMetadata(new RouteAccess("main","service","kitchen"));
        // Configuracion operativa: el nucleo aporta la organizacion (mesas ACTIVAS con su sala, E2); cada modulo anade sus claves (p. ej. menus).
        app.MapGet(prefix+"/configuration",(Func<HttpContext,Task<IResult>>)(async c=>{
            var configuration=new Dictionary<string,object>(StringComparer.Ordinal){
                ["tables"]=await reads.ActiveTables(scope,c.RequestAborted),
                // E4a: ofertas VIGENTES ahora (degustaciones y menus cerrados con sus pases y platos): lo que se puede abrir. Sin dinero.
                ["offers"]=await reads.AvailableOffers(scope,DateTime.Now,c.RequestAborted),
                // C2: estaciones (id, nombre, tipo, activa; tambien las desactivadas) para que un puesto y la PWA escriban nombres y no
                // codigos: /organization es solo del responsable. Solo anade; sin dinero.
                ["stations"]=await reads.StationChoices(scope,c.RequestAborted)};
            foreach(var (module,host) in modules)
                foreach(var (key,value) in await module.ConfigurationAsync(host,c.RequestAborted))
                    if(!configuration.TryAdd(key,value)) throw new InvalidOperationException($"Configuration key {key} is claimed twice.");
            return Results.Json(configuration);
        })).WithMetadata(new RouteAccess("main","service","kitchen"));
        // D6.1 (#28, ADR-007): catalogo OPERATIVO para el comandero — que se puede anadir, nunca a que precio.
        // Proyeccion propia: no reutiliza la de caja ni deja pasar un solo campo economico (ni 'tariff': la guarda de la PWA la rechaza).
        // E3: un vendible = producto + presentacion activos con precio vigente en la tarifa general.
        // E3.3: ETag = huella del contenido Y de la fecha (el precio vigente, y con el lo vendible, cambia de un dia a otro); 304 ante
        // If-None-Match, para que el comandero revalide sin bajar 2.000 filas. Cache privada que revalida.
        app.MapGet(prefix+"/catalog",(Func<HttpContext,Task<IResult>>)(async c=>{
            var today=DateOnly.FromDateTime(DateTime.Now);
            var body=Wire.Encode(await reads.Sellables(scope,today,c.RequestAborted));
            var tag="\""+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(today.ToString("yyyy-MM-dd")+body)))[..32]+"\"";
            c.Response.Headers.ETag=tag; c.Response.Headers.CacheControl="private, no-cache";
            if(c.Request.Headers.IfNoneMatch.Any(v=>v==tag)) return Results.StatusCode(304);
            return Results.Text(body,"application/json");
        })).WithMetadata(new RouteAccess("main","service"));
        // E3.2: imagen vigente de un articulo, para cualquier identidad valida (una foto no es dinero). Bytes tal cual se guardaron,
        // con su tipo, ETag = huella y cache PRIVADA que revalida (la URL es del producto: la imagen puede cambiar); 304 si el cliente
        // ya tiene esa huella; nosniff para que nadie la interprete como otra cosa; 404 sin imagen o quitada. No es JSON: la guarda
        // anti-dinero de la PWA no entra aqui y los catalogos solo llevan imageSha.
        app.MapGet(prefix+"/catalog/images/{productId}",(Func<HttpContext,string,Task<IResult>>)(async (c,productId)=>{
            var image=await reads.ActiveImage(scope,productId,c.RequestAborted) ?? throw new StoreNotFound();
            c.Response.Headers.CacheControl="private, no-cache";
            c.Response.Headers.XContentTypeOptions="nosniff";
            return Results.Bytes(image.Content,image.ContentType,entityTag:new Microsoft.Net.Http.Headers.EntityTagHeaderValue("\""+image.Sha256+"\""));
        })).WithMetadata(new RouteAccess("main","service","kitchen"));
        // Caja: el mismo listado con el precio de la tarifa GENERAL como referencia; el importe real lo fija el servidor con la
        // tarifa de la sala de la cuenta al anadir el consumo.
        app.MapGet(prefix+"/checkout/catalog",(Func<HttpContext,Task<IResult>>)(async c=>Json(
            await reads.PricedSellables(scope,DateOnly.FromDateTime(DateTime.Now),c.RequestAborted)
        ))).WithMetadata(new RouteAccess("main"));
        app.MapGet(prefix+"/checkout/accounts",(Func<HttpContext,Task<IResult>>)(async c=>Results.Json(
            await reads.OpenAccounts(scope,c.RequestAborted)
        ))).WithMetadata(new RouteAccess("main"));
    }
}
