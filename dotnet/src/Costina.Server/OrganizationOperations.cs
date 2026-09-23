using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;
using Npgsql;
using static Costina.Core.Hosting.Requests;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Server;

// E2: comandos de ORGANIZACION (nucleo, solo puesto principal). Misma tuberia que todo: idempotencia, auditoria, outbox.
// Sin version esperada: son datos maestros, la ultima edicion gana; el cliente relee tras cada comando como siempre.
public sealed record OrganizationCommand(string? Id=null,string? Name=null,int? Capacity=null,string? ZoneId=null,int? Sort=null,string? Kind=null,string? TariffId=null);

public static class OrganizationOperations
{
    public static readonly string[] Actions = [
        "zone-create","zone-update","zone-deactivate","zone-reactivate",
        "table-create","table-update","table-deactivate","table-reactivate",
        "station-create","station-update","station-deactivate","station-reactivate"];
    public static bool Allows(string role,string action) => role=="main" && Actions.Contains(action,StringComparer.Ordinal);

    public static async Task<object> Apply(Unit unit,ExecutionIdentity identity,string action,OrganizationCommand request,IReadOnlyList<IModule> modules)
    {
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        var id=Required(request.Id);
        DomainEvent Event(string type,string aggregate,params (string Key,string Value)[] data)
            => new(Guid.NewGuid(),"organization."+type,aggregate,identity.Scope,identity.ActorId,stamp.At,data.ToDictionary(d=>d.Key,d=>d.Value));
        switch(action)
        {
            case "zone-create":
            {
                var zone=Organization.Zone(id,request.Name,request.Sort,tariffId:request.TariffId);
                await CheckTariff(unit,zone.TariffId);
                await unit.InsertZone(zone);
                await unit.Events([Event("zone_created",zone.Id,("name",zone.Name))]);
                return zone;
            }
            case "zone-update":
            {
                var current=await unit.Zone(id);
                // E3: tarifa de la sala; cadena vacia = volver a la general.
                var zone=Organization.Zone(id,request.Name??current.Name,request.Sort??current.Sort,current.Active,request.TariffId is null ? current.TariffId : request.TariffId);
                if(zone.TariffId!=current.TariffId) await CheckTariff(unit,zone.TariffId);
                await unit.UpdateZone(zone);
                await unit.Events([Event("zone_updated",zone.Id,("name",zone.Name))]);
                return zone;
            }
            case "zone-deactivate":
            case "zone-reactivate":
            {
                var current=await unit.Zone(id); var active=action=="zone-reactivate";
                if(!active && (await unit.Tables()).Any(t=>t.ZoneId==id && t.Active))
                    throw new RuleViolation("zone_in_use","Deactivate or move the zone's active tables first.");
                var zone=current with { Active=active };
                await unit.UpdateZone(zone);
                await unit.Events([Event(active ? "zone_reactivated" : "zone_deactivated",zone.Id)]);
                return zone;
            }
            case "table-create":
            {
                var table=Organization.Table(id,request.Name,request.Capacity,request.ZoneId,request.Sort);
                if(!(await unit.Zone(table.ZoneId)).Active) throw new RuleViolation("zone_inactive","The zone is deactivated.");
                await unit.InsertTable(table);
                await unit.Events([Event("table_created",table.Id,("name",table.Name),("zone_id",table.ZoneId))]);
                return table;
            }
            case "table-update":
            {
                var current=await unit.Table(id);
                var table=Organization.Table(id,request.Name??current.Name,request.Capacity??current.Capacity,request.ZoneId??current.ZoneId,request.Sort??current.Sort,current.Active);
                if(table.ZoneId!=current.ZoneId && !(await unit.Zone(table.ZoneId)).Active) throw new RuleViolation("zone_inactive","The zone is deactivated.");
                await unit.UpdateTable(table);
                await unit.Events([Event("table_updated",table.Id,("name",table.Name),("zone_id",table.ZoneId))]);
                return table;
            }
            case "table-deactivate":
            case "table-reactivate":
            {
                var current=await unit.Table(id); var active=action=="table-reactivate";
                // Una mesa con algo vivo encima no se desactiva: se pregunta a cada modulo activo por su contrato, nunca a sus tablas.
                if(!active) foreach(var module in modules)
                    if(await module.TableInUseAsync(unit,id)) throw new RuleViolation("table_in_use",$"Table {id} is in use by module {module.Name}; release it first.");
                if(active && !(await unit.Zone(current.ZoneId)).Active) throw new RuleViolation("zone_inactive","Reactivate the zone first.");
                var table=current with { Active=active };
                await unit.UpdateTable(table);
                await unit.Events([Event(active ? "table_reactivated" : "table_deactivated",table.Id)]);
                return table;
            }
            case "station-create":
            {
                var station=Organization.Station(id,request.Name,ParseEnum<StationKind>(request.Kind),request.Sort);
                await unit.InsertStation(station);
                await unit.Events([Event("station_created",station.Id,("name",station.Name),("kind",OrganizationUnit.KindText(station.Kind)))]);
                return station;
            }
            case "station-update":
            {
                var current=await unit.Station(id);
                var station=Organization.Station(id,request.Name??current.Name,request.Kind is null ? current.Kind : ParseEnum<StationKind>(request.Kind),request.Sort??current.Sort,current.Active);
                await unit.UpdateStation(station);
                await unit.Events([Event("station_updated",station.Id,("name",station.Name),("kind",OrganizationUnit.KindText(station.Kind)))]);
                return station;
            }
            case "station-deactivate":
            case "station-reactivate":
            {
                var current=await unit.Station(id); var active=action=="station-reactivate";
                var station=Organization.Station(id,current.Name,current.Kind,current.Sort,active);
                await unit.UpdateStation(station);
                await unit.Events([Event(active ? "station_reactivated" : "station_deactivated",station.Id)]);
                return station;
            }
            default: throw new StoreNotFound();
        }
    }

    private static async Task CheckTariff(Unit unit,string? tariffId)
    {
        if(tariffId is null) return;
        if(!(await unit.Tariff(tariffId)).Active) throw new RuleViolation("tariff_inactive","The tariff is deactivated.");
    }

    // Rutas (nucleo, solo main): lectura completa de la organizacion y comandos. GET /configuration sigue sirviendo
    // a todos los clientes las mesas ACTIVAS (DesktopReadRoutes).
    public static void MapOrganizationRoutes(this WebApplication app,PostgresStore store,NpgsqlDataSource source,BusinessScope scope,IReadOnlyList<IModule> modules)
    {
        const string prefix="/api/native/v1";
        var reads=new DesktopReadRepository(source);
        app.MapGet(prefix+"/organization",(Func<HttpContext,Task<IResult>>)(async c=>Json(await reads.Organization(scope,c.RequestAborted))))
            .WithMetadata(new RouteAccess("main"));
        app.MapPost(prefix+"/organization/commands/{action}",(HttpContext c,string action)=>
            Write<OrganizationCommand>(c,store,scope,(u,i,r)=>Apply(u,i,action,r,modules)))
            .WithMetadata(new RouteAccess("main") { ActionPolicy=Allows });
    }
}
