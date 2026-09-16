using Costina.Domain;
using Costina.Persistence;

namespace Costina.Server;

public sealed record OpenRequest(string TableId,int Pax,string MenuId);
public sealed record DiningCommand(long ExpectedVersion,string? CourseId=null,string? ItemId=null,string? Reason=null,
    int? GuestPosition=null,string? Kind=null,string? Substance=null,string? Severity=null,string? RestrictionId=null);
public sealed record AccountCommand(long ExpectedVersion,string? ProductId=null,int Quantity=1,string? PaymentId=null,
    string? Method=null,long AmountCents=0,string? ChargeId=null,string? Reason=null);
public sealed record ReleaseCommand(long ExpectedVersion,string Reason);

public static class LocalOperations
{
    private static string Required(string? value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A required command field is missing.");
    private static T ParseEnum<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(Required(value), true, out var parsed) && Enum.IsDefined(parsed) ? parsed
            : throw new ArgumentException($"Unsupported {typeof(T).Name} value.");
    private static void Version(long actual,long expected)
    {
        if(expected < 1) throw new ArgumentException("expectedVersion must be positive.");
        if(actual != expected) throw new StoreConflict("version_conflict","Record changed; reload before deciding.");
    }
    public static async Task<object> Open(Unit unit,ExecutionIdentity identity,OpenRequest request)
    {
        var table = await unit.Configuration<TableDefinition>("table",Required(request.TableId));
        var menu = await unit.Configuration<MenuDefinition>("menu",Required(request.MenuId));
        if(request.Pax < 1 || request.Pax > table.Capacity) throw new ArgumentException("Pax is outside the configured table capacity.");
        var stamp = new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        var id = Guid.NewGuid().ToString("N");
        var courses = menu.Courses.Select(c => c with { Preparations = c.Preparations.SelectMany(p =>
            Enumerable.Range(1,request.Pax).Select(n => p with { Id=p.Id+"-"+n,GuestPosition=n })).ToArray() }).ToArray();
        var dining = new DiningService(id,identity.Scope,table.Id,request.Pax,courses);
        var account = new SettlementAccount(Guid.NewGuid().ToString("N"),identity.Scope,id);
        var occupancy = new TableOccupancy(Guid.NewGuid().ToString("N"),identity.Scope,table.Id,id);
        account.AddCharge(Guid.NewGuid().ToString("N"),menu.Name,request.Pax,menu.UnitPriceCents,stamp);
        await unit.Save(dining,null); await unit.Save(occupancy,null); await unit.Save(account,null);
        await unit.Events(new[] {
            new DomainEvent(Guid.NewGuid(),"service.created",id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"table_id",table.Id}}),
            new DomainEvent(Guid.NewGuid(),"table.occupied",occupancy.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id},{"table_id",table.Id}}),
            new DomainEvent(Guid.NewGuid(),"account.opened",account.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id}})});
        return new { serviceId=id,version=1,occupancyVersion=1 };
    }
    public static async Task<object> Dining(Unit unit,ExecutionIdentity identity,string id,string action,DiningCommand request)
    {
        var stored = await unit.Dining(id); Version(stored.Version,request.ExpectedVersion);
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
            case "acknowledge-restrictions": entity.AcknowledgeRestrictions(stamp); break;
            default: throw new StoreNotFound();
        }
        await unit.Save(entity,stored.Version);
        return new Versioned<DiningView>(stored.Version+1,entity.View());
    }
    public static async Task<object> Account(Unit unit,ExecutionIdentity identity,string id,string action,AccountCommand request)
    {
        var stored=await unit.Account(id); Version(stored.Version,request.ExpectedVersion);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        switch(action)
        {
            case "add-product":
                var product=await unit.Configuration<ProductDefinition>("product",Required(request.ProductId));
                if(!product.Active) throw new RuleViolation("product_unavailable","Product unavailable.");
                entity.AddCharge(Guid.NewGuid().ToString("N"),product.Name+" · "+product.Presentation,request.Quantity,product.PriceCents,stamp);
                break;
            case "payment":
                if(request.Method is not ("cash" or "card" or "other")) throw new ArgumentException("Unsupported payment method.");
                entity.RecordPayment(Required(request.PaymentId),request.Method,request.AmountCents,stamp); break;
            case "void-charge": entity.VoidCharge(Required(request.ChargeId),Required(request.Reason),stamp); break;
            case "close": entity.Close(stamp); break;
            default: throw new StoreNotFound();
        }
        await unit.Save(entity,stored.Version);
        return new Versioned<AccountView>(stored.Version+1,entity.View());
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
