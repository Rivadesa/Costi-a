using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Modules.Dining;

// E4a: la mesa se abre con una OFERTA del nucleo (offerId); menuId sigue aceptado una version como alias.
public sealed record OpenRequest(string TableId,int Pax,string? OfferId=null,string? MenuId=null);
public sealed record DiningCommand(long ExpectedVersion,string? CourseId=null,string? ItemId=null,string? Reason=null,
    int? GuestPosition=null,string? Kind=null,string? Substance=null,string? Severity=null,string? RestrictionId=null,
    string? Decision=null,string? Note=null,string? ProductId=null,int Quantity=1,string? PresentationId=null,string? DishId=null);
public sealed record ReleaseCommand(long ExpectedVersion,string Reason);

public static class DiningOperations
{
    public static async Task<object> Open(Unit unit,ExecutionIdentity identity,OpenRequest request)
    {
        var table = await unit.ActiveTable(Required(request.TableId));   // E2: mesa de la organizacion del nucleo, activa
        // E4a: OFERTA del nucleo, vigente ahora, con sus pases y platos activos. Degustacion: un plato por comensal en cada pase;
        // menu cerrado: pases con eleccion pendiente (cada comensal elige con 'choose' antes de disparar).
        var (offer,offerCourses) = await unit.AvailableOffer(Required(request.OfferId??request.MenuId),DateTime.Now);
        if(request.Pax < 1 || request.Pax > table.Capacity) throw new ArgumentException("Pax is outside the configured table capacity.");
        var stamp = new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        var id = Guid.NewGuid().ToString("N");
        var choice = offer.Kind == OfferKind.SetMenu; var carte = offer.Kind == OfferKind.ALaCarte;
        // E4b: en una carta los pases son grupos vacios y opcionales: los platos se piden con add-dish y se cobran al pedirlos.
        var courses = offerCourses.Select(c => new CourseDefinition(c.Id,c.Name,choice || carte ? [] : c.Dishes.SelectMany(d =>
            Enumerable.Range(1,request.Pax).Select(n => new PreparationDefinition(d.Id+"-"+n,d.Name,d.StationId,1,n))).ToArray(),choice,carte)).ToArray();
        var dining = new DiningService(id,identity.Scope,table.Id,request.Pax,courses,offer.Id);
        // La cuenta es del NUCLEO (ventas): el modulo la abre en la mesa y le apunta el menu (producto del catalogo, por persona, a la
        // tarifa de la sala; E3) por el contrato de la Unit, en la misma transaccion. El precio queda congelado con su origen.
        var account = new SettlementAccount(Guid.NewGuid().ToString("N"),identity.Scope,id);
        var occupancy = new TableOccupancy(Guid.NewGuid().ToString("N"),identity.Scope,table.Id,id);
        if(!carte)
        {
            var (product,presentation) = await unit.Sellable(offer.ProductId ?? throw new RuleViolation("offer_unpriced","This offer has no menu product."),Offers.PersonPresentation);
            var zone = await unit.Zone(table.ZoneId);
            long cents; string tariff;
            try { (cents,tariff) = await unit.PriceFor(zone.Active && zone.TariffId is not null ? zone.TariffId : Catalog.GeneralTariff,product.Id,presentation.Id,DateOnly.FromDateTime(DateTime.Now)); }
            catch(RuleViolation e) when(e.Code=="price_missing") { throw new RuleViolation("offer_unpriced","Set the menu price in the catalog before opening tables with this offer."); }
            account.AddCharge(Guid.NewGuid().ToString("N"),offer.Name+" · "+presentation.Name,request.Pax,cents,stamp,product.Id,presentation.Id,tariff);
        }
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
            case "choose":
            {
                // E4a: el plato tiene que ser uno de los ACTIVOS del pase en la oferta con la que se abrio la mesa (nucleo, misma transaccion).
                var dish = (await unit.OfferDish(entity.OfferId ?? throw new RuleViolation("no_choice","This service was not opened with an offer."),Required(request.CourseId),Required(request.DishId)));
                if(!dish.Active) throw new RuleViolation("dish_unavailable","This dish is deactivated.");
                entity.Choose(dish.CourseId,request.GuestPosition ?? throw new ArgumentException("guestPosition is required."),new PreparationDefinition(dish.Id,dish.Name,dish.StationId),stamp);
                break;
            }
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
    // E4b: plato pedido a la carta en un grupo pendiente: elaboracion en el pase (estacion del producto) y cargo en la cuenta al
    // precio vigente de la tarifa de la sala, en la misma transaccion. La respuesta NO lleva importes (ADR-007).
    public static async Task<object> AddDish(Unit unit,ExecutionIdentity identity,string id,DiningCommand request)
    {
        var stored=await unit.Dining(id); Version(stored.Version,request.ExpectedVersion);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        var courseId=Required(request.CourseId);
        var items=await unit.OfferItems(entity.OfferId ?? throw new RuleViolation("no_dishes","This service was not opened with an offer."));
        var item=items.FirstOrDefault(i=>i.CourseId==courseId && i.Item.ProductId==Required(request.ProductId) && i.Item.PresentationId==Required(request.PresentationId) && i.Item.Active).Item
            ?? throw new RuleViolation("item_unavailable","That dish is not on this group of the offer.");
        var (product,presentation)=await unit.Sellable(item.ProductId,item.PresentationId);
        if(product.StationId is null) throw new RuleViolation("station_required","This product has no kitchen station: order it as a consumption.");
        if(request.Quantity<1 || request.Quantity>20) throw new ArgumentException("Quantity must be between 1 and 20.");
        var (cents,tariff)=await unit.PriceFor(await unit.TariffForService(id),product.Id,presentation.Id,DateOnly.FromDateTime(DateTime.Now));
        var account=await unit.Account(id);
        var chargeId=Guid.NewGuid().ToString("N");
        account.Entity.AddCharge(chargeId,product.Name+" · "+presentation.Name,request.Quantity,cents,stamp,product.Id,presentation.Id,tariff);
        var existing=entity.View().Courses.First(c=>c.Id==courseId).Preparations.Count(p=>p.Id.StartsWith(product.Id+"-"+presentation.Id+"-",StringComparison.Ordinal));
        var dishId=product.Id+"-"+presentation.Id+"-"+(existing+1);
        entity.AddDish(courseId,new PreparationDefinition(dishId,product.Name+(presentation.Id==Catalog.DefaultPresentation ? "" : " · "+presentation.Name),product.StationId,request.Quantity,request.GuestPosition),stamp);
        await unit.Save(account.Entity,account.Version);
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
