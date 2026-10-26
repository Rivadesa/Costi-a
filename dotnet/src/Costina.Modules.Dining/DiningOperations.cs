using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Modules.Dining;

// E4a: la mesa se abre con una OFERTA del nucleo (offerId); menuId sigue aceptado una version como alias.
// E4e: Restrictions = las alergias de la mesa en la MISMA orden (campo nuevo y opcional: una apertura de hoy vale igual).
public sealed record OpenRequest(string TableId,int Pax,string? OfferId=null,string? MenuId=null,IReadOnlyList<RestrictionRequest>? Restrictions=null);
// E4e: una restriccion tal como llega al abrir. Todo anulable para decir QUE falta: sin comensal = toda la mesa.
public sealed record RestrictionRequest(int? GuestPosition=null,string? Kind=null,string? Substance=null,string? Severity=null);
// C3b: Lines = la RONDA de add-consumptions (campo nuevo y opcional: la ruta rechaza campos desconocidos y una orden de hoy vale igual).
// E4e: ItemIds = las elaboraciones de preparations-start|ready (de un pase: CourseId) e Items = las decisiones de review-preparations.
// Campos nuevos y opcionales: una orden de hoy vale igual.
public sealed record DiningCommand(long ExpectedVersion,string? CourseId=null,string? ItemId=null,string? Reason=null,
    int? GuestPosition=null,string? Kind=null,string? Substance=null,string? Severity=null,string? RestrictionId=null,
    string? Decision=null,string? Note=null,string? ProductId=null,int Quantity=1,string? PresentationId=null,string? DishId=null,
    IReadOnlyList<ConsumptionLine>? Lines=null,IReadOnlyList<string>? ItemIds=null,IReadOnlyList<ReviewItem>? Items=null);
// C3b: una linea de la ronda tal como llega. Todo es anulable para poder decir QUE falta y en QUE linea (422 con su indice).
public sealed record ConsumptionLine(string? ProductId=null,string? PresentationId=null,int? Quantity=null,int? GuestPosition=null);
// E4e: una decision de review-preparations tal como llega: la elaboracion (pase y plato), la decision y su nota, las cuatro obligatorias.
public sealed record ReviewItem(string? CourseId=null,string? ItemId=null,string? Decision=null,string? Note=null);
public sealed record ReleaseCommand(long ExpectedVersion,string Reason);

public static class DiningOperations
{
    // C3b: tope de una ronda (lineas) y de cada linea (cantidad). Una ronda mayor se manda en otra orden.
    public const int MaxRoundLines=30, MaxLineQuantity=99;
    // E4e: topes de las ordenes de grupo (too_many_items): elaboraciones de preparations-start|ready y decisiones de review-preparations
    // (60 con su nota caben en Requests.MaxCommandBody, 32 KiB).
    public const int MaxGroupPreparations=200, MaxGroupReviews=60;
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
        // E4e: quien abrio la mesa (para «Mis mesas» de C3e): el actor de esta orden (device:<nombre> o user:<nombre>) y la estacion del
        // dispositivo (una persona no tiene). Va en la instantanea, sin columna ni evento nuevo: service.created ya lleva el actor.
        var dining = new DiningService(id,identity.Scope,table.Id,request.Pax,courses,offer.Id,identity.ActorId,identity.Station);
        // E4e (ADR-016): las alergias de la mesa en la MISMA orden y transaccion (una sola orden incierta). Cada una marca por revisar lo
        // que le aplica de todo el menu, como declare-restriction; una mala (invalid_restriction, invalid_guest, duplicada) no abre nada.
        foreach(var restriction in request.Restrictions ?? [])
        {
            if(restriction is null) throw new ArgumentException("A restriction cannot be null.");
            dining.DeclareRestriction(restriction.GuestPosition,RestrictionValue<RestrictionKind>(restriction.Kind),restriction.Substance ?? "",
                RestrictionValue<RestrictionSeverity>(restriction.Severity),stamp);
        }
        // Sus restriction.declared van en la auditoria DETRAS de service.created (el agregado los emitiria antes, al guardarse).
        var declared = dining.PendingEvents.ToArray(); dining.ClearPendingEvents();
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
            account.AddCharge(Guid.NewGuid().ToString("N"),offer.Name+" · "+presentation.Name,request.Pax,cents,stamp,product.Id,presentation.Id,tariff,
                origin:ChargeOrigin.Menu);
        }
        await unit.Save(dining,null); await unit.Save(occupancy,null); await unit.Save(account,null,table.Id);
        await unit.Events(new[] {
            new DomainEvent(Guid.NewGuid(),"service.created",id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"table_id",table.Id}})}.Concat(declared).Concat(new[] {
            new DomainEvent(Guid.NewGuid(),"table.occupied",occupancy.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id},{"table_id",table.Id}}),
            new DomainEvent(Guid.NewGuid(),"account.opened",account.Id,identity.Scope,identity.ActorId,stamp.At,
                new Dictionary<string,string>{{"service_id",id}})}));
        return new { serviceId=id,version=1,occupancyVersion=1 };
    }
    // E4e: tipo y gravedad de una restriccion al abrir: solo un nombre conocido (sin distinguir mayusculas), nunca un numero.
    // Un valor desconocido o vacio da invalid_restriction. declare-restriction sigue como hoy (422 invalid_request, ParseEnum).
    private static T RestrictionValue<T>(string? value) where T : struct, Enum
        => Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n,value?.Trim(),StringComparison.OrdinalIgnoreCase)) is { } name
            ? Enum.Parse<T>(name) : throw new RuleViolation("invalid_restriction","Unknown restriction kind or severity.");
    // Matriz de estacion (D4.3, F06): un dispositivo solo marca elaboraciones de SU estacion, y
    // validar o revisar un pase exige la estacion 'pase'. Un usuario con sesion (Station null)
    // no tiene restriccion: el chef en el PC decide. La estacion nunca amplia el rol, solo acota.
    // E4e (ADR-016), SIN HUECOS: las ordenes de grupo se comprueban elaboracion a elaboracion (una ajena rechaza el grupo entero, sin
    // aplicar nada) y review-preparations es del pase, como review-preparation. Cambia D4.3a SOLO para el pase de COCINA: marca
    // (preparation(s)-start|ready) por cualquier estacion, por ejemplo la de una tablet estropeada. Un comandero de sala que quedo en
    // 'pase' antes de A2 sigue marcando solo lo de 'pase' (Affordances.PassMarksAll mira el rol). Su espejo es StationKeeps/PassKeeps.
    private static void StationAllowed(ExecutionIdentity identity,string role,DiningService entity,string action,DiningCommand request)
    {
        if(identity.Station is null) return;
        var pass=string.Equals(identity.Station,Affordances.PassStation,StringComparison.Ordinal);
        if(action is "preparation-start" or "preparation-ready" or "preparations-start" or "preparations-ready")
        {
            if(Affordances.PassMarksAll(role,identity.Station)) return;
            var preparations=entity.View().Courses.FirstOrDefault(c=>c.Id==request.CourseId)?.Preparations ?? [];
            var items=action.StartsWith("preparations-",StringComparison.Ordinal) ? (IEnumerable<string?>)(request.ItemIds ?? []) : [request.ItemId];
            // Una elaboracion que no existe no tiene estacion: la rechaza el dominio (preparation_not_found), sin aplicar nada.
            foreach(var item in items)
                if(preparations.FirstOrDefault(p=>p.Id==item)?.StationId is { } station && !string.Equals(station,identity.Station,StringComparison.Ordinal))
                    throw new RuleViolation("wrong_station","This device's station cannot mark that preparation.");
        }
        else if(action is "ready" or "review-preparation" or "review-preparations")
        {
            if(!pass) throw new RuleViolation("wrong_station","Course validation and review belong to the 'pase' station.");
        }
    }
    // E4e: elaboraciones de una orden de grupo: de 1 a MaxGroupPreparations (vacia = 422; mas = too_many_items). Repetidas: el dominio.
    private static IReadOnlyList<string> GroupItems(IReadOnlyList<string>? itemIds)
        => itemIds is not { Count: > 0 } ? throw new ArgumentException("itemIds is required: at least one preparation.")
            : itemIds.Count>MaxGroupPreparations ? throw new RuleViolation("too_many_items",$"At most {MaxGroupPreparations} preparations per command; send the rest in another.")
            : itemIds.Select(Required).ToArray();
    // E4e: decisiones de review-preparations: de 1 a MaxGroupReviews, cada una con pase, elaboracion, decision conocida y nota (422 si falta).
    private static IReadOnlyList<PreparationDecision> GroupReviews(IReadOnlyList<ReviewItem>? items)
        => items is not { Count: > 0 } ? throw new ArgumentException("items is required: at least one review.")
            : items.Count>MaxGroupReviews ? throw new RuleViolation("too_many_items",$"At most {MaxGroupReviews} reviews per command; send the rest in another.")
            : items.Select(i => i is null ? throw new ArgumentException("A review cannot be null.")
                : new PreparationDecision(Required(i.CourseId),Required(i.ItemId),ParseEnum<ReviewDecision>(i.Decision),Required(i.Note))).ToArray();
    // E4e (revision): role = el rol de la credencial (Requests.Role), que la matriz de estacion necesita para la excepcion del pase.
    public static async Task<object> Dining(Unit unit,ExecutionIdentity identity,string role,string id,string action,DiningCommand request)
    {
        var stored = await unit.Dining(id); Version(stored.Version,request.ExpectedVersion);
        StationAllowed(identity,role,stored.Entity,action,request);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        switch(action)
        {
            case "start": entity.Start(stamp); break;
            case "fire-next": entity.FireNext(stamp); break;
            case "preparation-start": entity.StartPreparation(Required(request.CourseId),Required(request.ItemId),stamp); break;
            case "preparation-ready": entity.ReadyPreparation(Required(request.CourseId),Required(request.ItemId),stamp); break;
            // E4e: ordenes de grupo, UNA orden (una subida de version) con un evento por elaboracion.
            case "preparations-start": entity.StartPreparations(Required(request.CourseId),GroupItems(request.ItemIds),stamp); break;
            case "preparations-ready": entity.ReadyPreparations(Required(request.CourseId),GroupItems(request.ItemIds),stamp); break;
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
            case "review-preparations": entity.ReviewPreparations(GroupReviews(request.Items),stamp); break;
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
        // La linea del cobro conserva su texto con la presentacion (la demo la llama "Racion": offers_http lo fija); C3b: origen 'dish'.
        account.Entity.AddCharge(chargeId,product.Name+" · "+presentation.Name,request.Quantity,cents,stamp,product.Id,presentation.Id,tariff,
            request.GuestPosition,ChargeOrigin.Dish);
        var existing=entity.View().Courses.First(c=>c.Id==courseId).Preparations.Count(p=>p.Id.StartsWith(product.Id+"-"+presentation.Id+"-",StringComparison.Ordinal));
        var dishId=product.Id+"-"+presentation.Id+"-"+(existing+1);
        entity.AddDish(courseId,new PreparationDefinition(dishId,Catalog.LineText(product.Name,presentation),product.StationId,request.Quantity,request.GuestPosition),stamp);
        await unit.Save(account.Entity,account.Version);
        await unit.Save(entity,stored.Version);
        return new Versioned<DiningView>(stored.Version+1,entity.View());
    }
    // Consumo a mayores desde un comandero (sala): contexto operativo = version del servicio que el camarero ve;
    // la cuenta (nucleo) se anexa bajo su propia version en la misma transaccion. La respuesta NO lleva importes:
    // el precio lo fija el catalogo del servidor y la cuenta se gestiona en el puesto principal (ADR-007).
    // C3b: el identificador de la linea sale de la orden (servicio + clave + indice, SettlementAccount.DerivedLineId) y la cuenta no
    // admite dos iguales: la misma orden reenviada por OTRO actor (la idempotencia del servidor es por actor) responde lo mismo sin
    // volver a cobrar; el texto va sin la presentacion por defecto; la linea lleva origen 'consumption' y hora; y deja el aviso
    // operativo service.consumptions_changed. No rechaza platos de cocina (eso es de add-consumptions), por compatibilidad.
    public static async Task<object> Consumption(Unit unit,ExecutionIdentity identity,string id,DiningCommand request)
    {
        var dining=await unit.Dining(id);
        var account=await unit.Account(id);
        var lineId=SettlementAccount.DerivedLineId(id,unit.CommandKey,0);
        if(AlreadyApplied(account.Entity,[lineId],(_,line)=>line.ProductId==request.ProductId
            && (request.PresentationId is null || line.PresentationId==request.PresentationId) && line.Quantity==request.Quantity) is { } applied)
            return new { version=dining.Version,consumptionId=lineId,productId=applied[0].ProductId,presentationId=applied[0].PresentationId,quantity=applied[0].Quantity };
        Version(dining.Version,request.ExpectedVersion);
        if(dining.Entity.State==DiningState.Cancelled) throw new RuleViolation("service_finished","A cancelled service takes no consumptions.");
        // E3: vendible del catalogo del nucleo (producto + presentacion activos) al precio vigente de la tarifa de la sala de la
        // cuenta (o la general); el cargo congela importe, producto, presentacion y tarifa.
        var (product,presentation)=await unit.Sellable(Required(request.ProductId),request.PresentationId);
        var (cents,tariff)=await unit.PriceFor(await unit.TariffForService(id),product.Id,presentation.Id,DateOnly.FromDateTime(DateTime.Now));
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        account.Entity.AddCharge(lineId,Catalog.LineText(product.Name,presentation),request.Quantity,cents,stamp,product.Id,presentation.Id,tariff,
            origin:ChargeOrigin.Consumption);
        await unit.Save(account.Entity,account.Version);
        await unit.Events([SettlementAccount.ConsumptionsChanged(id,stamp)]);
        // D6.3: la referencia viaja como consumptionId. La ruta de sala no lleva vocabulario de caja (ADR-007): la guarda
        // de la PWA rechaza entera cualquier respuesta con una clave economica, y "charge" (o "tariff") lo es.
        return new { version=dining.Version,consumptionId=lineId,productId=product.Id,presentationId=presentation.Id,quantity=request.Quantity };
    }

    // C3b: la RONDA en una sola orden (sala y principal): de 1 a 30 lineas, cantidades de 1 a 99, comensal opcional dentro de la mesa.
    // Todo o nada en la transaccion de la orden; una linea mala da 422 con su indice (LineRejected) y no se apunta ninguna. Un producto
    // con estacion de cocina o de pase se rechaza con use_add_dish: lo que se pide en Bebidas nunca se cobra sin que cocina lo vea.
    // La version del servicio no se mueve (no se pisan las ordenes de otros camareros), pero se exige la leida (F01). Respuesta sin
    // dinero: {version, consumptionIds}, en el orden de las lineas.
    public static async Task<object> Consumptions(Unit unit,ExecutionIdentity identity,string id,DiningCommand request)
    {
        var lines=request.Lines is { Count: > 0 } given ? given : throw new ArgumentException("lines is required: at least one line.");
        if(lines.Count>MaxRoundLines) throw new RuleViolation("too_many_lines",$"At most {MaxRoundLines} lines per round; send the rest in another.");
        for(var i=0;i<lines.Count;i++) if(lines[i] is null) throw new LineRejected(i,"invalid_request","A line cannot be null.");
        var dining=await unit.Dining(id);
        var account=await unit.Account(id);
        var ids=lines.Select((_,i)=>SettlementAccount.DerivedLineId(id,unit.CommandKey,i)).ToArray();
        if(AlreadyApplied(account.Entity,ids,(i,line)=>line.ProductId==lines[i].ProductId && (lines[i].PresentationId is null || line.PresentationId==lines[i].PresentationId)
            && line.Quantity==lines[i].Quantity && line.GuestPosition==lines[i].GuestPosition) is not null)
            return new { version=dining.Version,consumptionIds=ids };
        Version(dining.Version,request.ExpectedVersion);
        if(dining.Entity.State==DiningState.Cancelled) throw new RuleViolation("service_finished","A cancelled service takes no consumptions.");
        if(account.Entity.State==AccountState.Closed) throw new RuleViolation("account_closed","Closed accounts cannot be edited.");
        var tariff=await unit.TariffForService(id); var today=DateOnly.FromDateTime(DateTime.Now);
        var kinds=new Dictionary<string,StationKind>(StringComparer.Ordinal);
        var sold=new List<(ProductDefinition Product,PresentationDefinition Presentation,long Cents,string Tariff)>();
        for(var i=0;i<lines.Count;i++)
        {
            var line=lines[i];
            try
            {
                var productId=Required(line.ProductId);
                if(line.Quantity is not (>= 1 and <= MaxLineQuantity)) throw new ArgumentException($"quantity must be between 1 and {MaxLineQuantity}.");
                if(line.GuestPosition is { } guest && (guest<1 || guest>dining.Entity.Pax)) throw new ArgumentException("guestPosition is outside this table.");
                var (product,presentation)=await unit.Sellable(productId,line.PresentationId);
                if(product.StationId is { } station)
                {
                    if(!kinds.TryGetValue(station,out var kind)) kinds[station]=kind=(await unit.Station(station)).Kind;
                    if(kind is StationKind.Kitchen or StationKind.Pass) throw new RuleViolation("use_add_dish","This product goes through the kitchen: order it as a dish.");
                }
                var (cents,applied)=await unit.PriceFor(tariff,product.Id,presentation.Id,today);
                sold.Add((product,presentation,cents,applied));
            }
            catch(Exception e) when(e is RuleViolation or ArgumentException or StoreNotFound) { throw LineRejected.From(i,e); }
        }
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        for(var i=0;i<lines.Count;i++)
            account.Entity.AddCharge(ids[i],Catalog.LineText(sold[i].Product.Name,sold[i].Presentation),lines[i].Quantity!.Value,sold[i].Cents,stamp,
                sold[i].Product.Id,sold[i].Presentation.Id,sold[i].Tariff,lines[i].GuestPosition,ChargeOrigin.Consumption);
        await unit.Save(account.Entity,account.Version);
        await unit.Events([SettlementAccount.ConsumptionsChanged(id,stamp)]);
        return new { version=dining.Version,consumptionIds=ids };
    }

    // C3b: la orden con estos identificadores derivados ya esta en la cuenta? Si TODAS sus lineas estan y son las mismas (la misma orden
    // reenviada por otro actor), devuelve esas lineas: se responde lo mismo y no se cobra otra vez. Si ninguna esta, null: se apunta.
    // Si alguna esta pero no todas, o con otro contenido (la misma clave con otras lineas), se rechaza: nunca un segundo cargo.
    private static IReadOnlyList<ChargeLine>? AlreadyApplied(SettlementAccount account,IReadOnlyList<string> ids,Func<int,ChargeLine,bool> same)
    {
        var existing=account.View().Charges.ToDictionary(c=>c.Id,StringComparer.Ordinal);
        var found=ids.Select(i=>existing.GetValueOrDefault(i)).ToArray();
        if(found.All(f=>f is null)) return null;
        if(found.Select((f,i)=>f is not null && same(i,f)).All(ok=>ok)) return found.Select(f=>f!).ToArray();
        throw new RuleViolation("duplicate_consumption","This command key was already used for other consumptions on this table.");
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
