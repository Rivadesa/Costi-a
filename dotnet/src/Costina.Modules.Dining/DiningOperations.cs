using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Modules.Dining;

public sealed record OpenRequest(string TableId,int Pax,string MenuId);
public sealed record DiningCommand(long ExpectedVersion,string? CourseId=null,string? ItemId=null,string? Reason=null,
    int? GuestPosition=null,string? Kind=null,string? Substance=null,string? Severity=null,string? RestrictionId=null,
    string? Decision=null,string? Note=null,string? ProductId=null,int Quantity=1,string? PresentationId=null);
public sealed record ReleaseCommand(long ExpectedVersion,string Reason);
// Menu por pases: configuracion del modulo (las mesas y los productos son del nucleo).
public sealed record MenuDefinition(string Id,string Name,long UnitPriceCents,CourseDefinition[] Courses);

public static class DiningOperations
{
    public static async Task<object> Open(Unit unit,ExecutionIdentity identity,OpenRequest request)
    {
        var table = await unit.ActiveTable(Required(request.TableId));   // E2: mesa de la organizacion del nucleo, activa
        var menu = await unit.ModuleConfiguration<MenuDefinition>("dining","menu",Required(request.MenuId));   // menus: configuracion del modulo (E1b)
        if(request.Pax < 1 || request.Pax > table.Capacity) throw new ArgumentException("Pax is outside the configured table capacity.");
        var stamp = new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        var id = Guid.NewGuid().ToString("N");
        var courses = menu.Courses.Select(c => c with { Preparations = c.Preparations.SelectMany(p =>
            Enumerable.Range(1,request.Pax).Select(n => p with { Id=p.Id+"-"+n,GuestPosition=n })).ToArray() }).ToArray();
        var dining = new DiningService(id,identity.Scope,table.Id,request.Pax,courses);
        // La cuenta es del NUCLEO (ventas): el modulo la abre en la mesa y le apunta el menu por el contrato de la Unit, en la misma transaccion.
        var account = new SettlementAccount(Guid.NewGuid().ToString("N"),identity.Scope,id);
        var occupancy = new TableOccupancy(Guid.NewGuid().ToString("N"),identity.Scope,table.Id,id);
        account.AddCharge(Guid.NewGuid().ToString("N"),menu.Name,request.Pax,menu.UnitPriceCents,stamp);
        await unit.Save(dining,null); await unit.Save(occupancy,null); await unit.Save(account,null,table.Id);
        await unit.Events(new[] {
            new DomainEvent(Guid.NewGuid(),"service.created",id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"table_id",table.Id}}),
            new DomainEvent(Guid.NewGuid(),"table.occupied",occupancy.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id},{"table_id",table.Id}}),
            new DomainEvent(Guid.NewGuid(),"account.opened",account.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id}})});
        return new { serviceId=id,version=1,occupancyVersion=1 };
    }
    // Matriz de estacion (D4.3, F06): un dispositivo solo marca elaboraciones de SU estacion, y
    // validar o revisar un pase exige la estacion 'pase'. Un usuario con sesion (Station null)
    // no tiene restriccion: el chef en el PC decide. La estacion nunca amplia el rol, solo acota.
    private static void StationAllowed(ExecutionIdentity identity,DiningService entity,string action,DiningCommand request)
    {
        if(identity.Station is null) return;
        if(action is "preparation-start" or "preparation-ready")
        {
            var station=entity.View().Courses.FirstOrDefault(c=>c.Id==request.CourseId)?
                .Preparations.FirstOrDefault(p=>p.Id==request.ItemId)?.StationId;
            if(station is not null && !string.Equals(station,identity.Station,StringComparison.Ordinal))
                throw new RuleViolation("wrong_station","This device's station cannot mark that preparation.");
        }
        else if(action is "ready" or "review-preparation")
        {
            if(!string.Equals(identity.Station,"pase",StringComparison.Ordinal))
                throw new RuleViolation("wrong_station","Course validation and review belong to the 'pase' station.");
        }
    }
    public static async Task<object> Dining(Unit unit,ExecutionIdentity identity,string id,string action,DiningCommand request)
    {
        var stored = await unit.Dining(id); Version(stored.Version,request.ExpectedVersion);
        StationAllowed(identity,stored.Entity,action,request);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        switch(action)
        {
            case "start": entity.Start(stamp); break;
            case "fire-next": entity.FireNext(stamp); break;
            case "preparation-start": entity.StartPreparation(Required(request.CourseId),Required(request.ItemId),stamp); break;
            case "preparation-ready": entity.ReadyPreparation(Required(request.CourseId),Required(request.ItemId),stamp); break;
            case "ready": entity.ValidateReady(Required(request.CourseId),stamp); break;
            case "serve": entity.Serve(Required(request.CourseId),stamp); break;
            case "skip": entity.Skip(Required(request.CourseId),Required(request.Reason),stamp); break;
            case "pause": entity.Pause(Required(request.Reason),stamp); break;
            case "resume": entity.Resume(stamp); break;
            case "complete": entity.Complete(stamp); break;
            case "cancel-unstarted": entity.CancelUnstarted(Required(request.Reason),stamp); break;
            case "declare-restriction": entity.DeclareRestriction(request.GuestPosition,
                ParseEnum<RestrictionKind>(request.Kind),Required(request.Substance),
                ParseEnum<RestrictionSeverity>(request.Severity),stamp); break;
            case "remove-restriction": entity.RemoveRestriction(Required(request.RestrictionId),Required(request.Reason),stamp); break;
            case "review-preparation": entity.ReviewPreparation(Required(request.CourseId),Required(request.ItemId),
                ParseEnum<ReviewDecision>(request.Decision),Required(request.Note),stamp); break;
            default: throw new StoreNotFound();
        }
        await unit.Save(entity,stored.Version);
        return new Versioned<DiningView>(stored.Version+1,entity.View());
    }
    // Consumo a mayores desde un comandero (sala): contexto operativo = version del servicio que el camarero ve;
    // la cuenta (nucleo) se anexa bajo su propia version en la misma transaccion. La respuesta NO lleva importes:
    // el precio lo fija el catalogo del servidor y la cuenta se gestiona en el puesto principal (ADR-007).
    public static async Task<object> Consumption(Unit unit,ExecutionIdentity identity,string id,DiningCommand request)
    {
        var dining=await unit.Dining(id); Version(dining.Version,request.ExpectedVersion);
        if(dining.Entity.State==DiningState.Cancelled) throw new RuleViolation("service_finished","A cancelled service takes no consumptions.");
        var account=await unit.Account(id);
        // E3: vendible del catalogo del nucleo (producto + presentacion activos) al precio vigente de la tarifa de la sala de la
        // cuenta (o la general); el cargo congela importe, producto, presentacion y tarifa.
        var (product,presentation)=await unit.Sellable(Required(request.ProductId),request.PresentationId);
        var (cents,tariff)=await unit.PriceFor(await unit.TariffForService(id),product.Id,presentation.Id,DateOnly.FromDateTime(DateTime.Now));
        var chargeId=Guid.NewGuid().ToString("N");
        account.Entity.AddCharge(chargeId,product.Name+" · "+presentation.Name,request.Quantity,cents,
            new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow),product.Id,presentation.Id,tariff);
        await unit.Save(account.Entity,account.Version);
        // D6.3: la referencia viaja como consumptionId. La ruta de sala no lleva vocabulario de caja (ADR-007): la guarda
        // de la PWA rechaza entera cualquier respuesta con una clave economica, y "charge" (o "tariff") lo es.
        return new { version=dining.Version,consumptionId=chargeId,productId=product.Id,presentationId=presentation.Id,quantity=request.Quantity };
    }
    public static async Task<object> Release(Unit unit,ExecutionIdentity identity,string id,ReleaseCommand request)
    {
        var stored=await unit.Occupancy(id); Version(stored.Version,request.ExpectedVersion);
        var dining=await unit.Dining(id);
        stored.Entity.Release(dining.Entity,Required(request.Reason),new(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow));
        await unit.Save(stored.Entity,stored.Version);
        return new Versioned<OccupancyView>(stored.Version+1,stored.Entity.View());
    }
}
