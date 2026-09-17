using Costina.Domain;
using Costina.Persistence;
using Npgsql;
namespace Costina.Server;

// Uses the existing middleware and endpoint access metadata, without changing authentication.
public static class DesktopReadRoutes
{
    public static void MapDesktopReadRoutes(this WebApplication app,NpgsqlDataSource source,BusinessScope scope,Guid installation,string serverVersion)
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
                // Affordances de sesion: acciones globales (no ligadas a un agregado) que este rol puede iniciar.
                // add-consumption: consumo a mayores desde sala sin importes (D3.6); cocina no marca consumos.
                actions=new[]{"open","add-consumption"}.Where(a=>Affordances.Allows(role,a)).ToArray()
            });})).WithMetadata(new RouteAccess("main","service","kitchen"));
        app.MapGet(prefix+"/configuration",(Func<HttpContext,Task<IResult>>)(async c=>Results.Json(new {
            tables=await reads.Configuration<TableDefinition>(scope,"table",c.RequestAborted),
            menus=(await reads.Configuration<MenuDefinition>(scope,"menu",c.RequestAborted)).Select(m=>new {m.Id,m.Name}).ToArray()
        }))).WithMetadata(new RouteAccess("main","service","kitchen"));
        app.MapGet(prefix+"/checkout/catalog",(Func<HttpContext,Task<IResult>>)(async c=>Results.Json(
            (await reads.Configuration<ProductDefinition>(scope,"product",c.RequestAborted)).Where(p=>p.Active)
            .Select(p=>new {p.Id,p.Name,p.Presentation,p.PriceCents}).ToArray()
        ))).WithMetadata(new RouteAccess("main"));
        app.MapGet(prefix+"/checkout/accounts",(Func<HttpContext,Task<IResult>>)(async c=>Results.Json(
            await reads.OpenAccounts(scope,c.RequestAborted)
        ))).WithMetadata(new RouteAccess("main"));
    }
}
