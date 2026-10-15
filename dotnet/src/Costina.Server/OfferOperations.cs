using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;
using Npgsql;
using static Costina.Core.Hosting.Requests;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Server;

// E4a: comandos de la OFERTA (nucleo, solo puesto principal): ofertas, pases y platos. Misma tuberia que todo. Sin version
// esperada: datos maestros. Una oferta cerrada nace con su producto-menu en el catalogo (categoria 'menus', presentacion
// 'person') si no se indica uno; su precio se fija con price-set del catalogo o con priceCents al crearla.
// E4d: Id opcional en las altas (se genera del nombre, unico entre hermanos; el de una oferta a 27 para que quepa su producto-menu);
// Sort opcional (al final); offer-update admite PriceCents (precio por persona en la General desde hoy); offers-reorder lleva OfferIds.
public sealed record OfferCommand(string? Id=null,string? Name=null,string? Kind=null,string? ProductId=null,string? Service=null,
    string? ValidFrom=null,string? ValidTo=null,int? Weekdays=null,int? Sort=null,long? PriceCents=null,string? TaxId=null,
    string? OfferId=null,string? CourseId=null,string? StationId=null,string? PresentationId=null,string[]? OfferIds=null);

public static class OfferOperations
{
    public static readonly string[] Actions = [
        "offer-create","offer-update","offer-deactivate","offer-reactivate",
        "course-create","course-update","course-deactivate","course-reactivate",
        "dish-create","dish-update","dish-deactivate","dish-reactivate",
        "item-create","item-deactivate","item-reactivate","offers-reorder"];
    public const int OfferCodeMax=Organization.MaxCodeLength-5;   // "menu-" + codigo de la oferta cabe en un codigo de producto
    public static bool Allows(string role,string action) => role=="main" && Actions.Contains(action,StringComparer.Ordinal);

    private static DateOnly? Date(string? text,string name)
    {
        _=name;
        return string.IsNullOrWhiteSpace(text) ? null : Catalog.ValidFrom(text,DateOnly.MinValue);   // invalid_date si no es AAAA-MM-DD
    }

    public static async Task<object> Apply(Unit unit,ExecutionIdentity identity,string action,OfferCommand request)
    {
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        DomainEvent Event(string type,string aggregate,params (string Key,string Value)[] data)
            => new(Guid.NewGuid(),"offer."+type,aggregate,identity.Scope,identity.ActorId,stamp.At,data.ToDictionary(d=>d.Key,d=>d.Value));
        // E4d: el producto-menu y su precio dejan rastro en la auditoria del catalogo (antes offer-create no lo dejaba).
        DomainEvent CatalogEvent(string type,string aggregate,params (string Key,string Value)[] data)
            => new(Guid.NewGuid(),"catalog."+type,aggregate,identity.Scope,identity.ActorId,stamp.At,data.ToDictionary(d=>d.Key,d=>d.Value));
        DomainEvent PriceEvent(PriceDefinition price) => CatalogEvent("price_set",price.ProductId,("presentation",price.PresentationId),("tariff",price.TariffId),("valid_from",price.ValidFrom.ToString("yyyy-MM-dd")),("price_cents",price.PriceCents.ToString()));
        var active=action.EndsWith("-reactivate",StringComparison.Ordinal);
        switch(action)
        {
            case "offer-create":
            {
                var name=Organization.Name(request.Name);
                var offers=await unit.Offers();
                // E4d: sin codigo, uno generado del nombre (a 27: su producto-menu es "menu-" + codigo); sin orden, al final.
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>offers.Any(o=>string.Equals(o.Id,c,StringComparison.OrdinalIgnoreCase)),OfferCodeMax) : Organization.Code(request.Id,"offer id");
                var sort=request.Sort??Catalog.NextSort(offers.Select(o=>o.Sort));
                var kind=OfferUnit.KindOf(request.Kind??"tasting");
                var productId=request.ProductId;
                var events=new List<DomainEvent>();
                if(string.IsNullOrWhiteSpace(productId) && kind!=OfferKind.ALaCarte)
                {
                    // Producto-menu del catalogo: categoria 'menus' (se crea si falta), impuesto indicado o IVA 10 %, presentacion 'person'.
                    // E4d: su codigo es "menu-<codigo de la oferta>" si cabe y esta libre; si no, se deriva del nombre ("menu <nombre>").
                    var products=await unit.Products();
                    bool Taken(string c) => products.Any(p=>string.Equals(p.Id,c,StringComparison.OrdinalIgnoreCase));
                    var wanted="menu-"+id.ToLowerInvariant();
                    productId=wanted.Length<=Organization.MaxCodeLength && !Taken(wanted) && wanted.All(c=>char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? wanted : Catalog.FreeCode("menu "+name,Taken);
                    await unit.SeedCategory(new CategoryDefinition(Offers.MenusCategory,"Menús",null,null,99,true));
                    var tax=await unit.Tax(request.TaxId??"iva-10");
                    if(!tax.Active) throw new RuleViolation("tax_inactive","The tax is deactivated.");
                    await unit.InsertProduct(Catalog.Product(productId,name,Offers.MenusCategory,tax.Id,null,90));
                    await unit.InsertPresentation(Catalog.Presentation(productId,Offers.PersonPresentation,"Por persona",0));
                    events.Add(CatalogEvent("product_created",productId,("name",name)));
                    if(request.PriceCents is not null)
                    {
                        var price=Catalog.Price(Catalog.GeneralTariff,productId,Offers.PersonPresentation,CatalogOperations.Today(),request.PriceCents);
                        await unit.SetPrice(price); events.Add(PriceEvent(price));
                    }
                }
                var offer=Offers.Offer(id,name,kind,productId,request.Service is null ? OfferService.Any : OfferUnit.ServiceOf(request.Service),
                    Date(request.ValidFrom,"validFrom"),Date(request.ValidTo,"validTo"),request.Weekdays,sort);
                await CheckProduct(unit,offer);
                await unit.InsertOffer(offer);
                events.Add(Event("offer_created",offer.Id,("name",offer.Name),("kind",OfferUnit.KindText(offer.Kind))));
                await unit.Events(events);
                return offer;
            }
            case "offer-update":
            {
                var current=await unit.Offer(Required(request.Id));
                var offer=Offers.Offer(current.Id,request.Name??current.Name,request.Kind is null ? current.Kind : OfferUnit.KindOf(request.Kind),
                    request.ProductId is null ? current.ProductId : request.ProductId,request.Service is null ? current.Service : OfferUnit.ServiceOf(request.Service),
                    request.ValidFrom is null ? current.ValidFrom : Date(request.ValidFrom,"validFrom"),request.ValidTo is null ? current.ValidTo : Date(request.ValidTo,"validTo"),
                    request.Weekdays??current.Weekdays,request.Sort??current.Sort,current.Active);
                if(offer.ProductId!=current.ProductId) await CheckProduct(unit,offer);
                await unit.UpdateOffer(offer);
                var events=new List<DomainEvent>{ Event("offer_updated",offer.Id,("name",offer.Name)) };
                // E4d: precio por persona en la General desde hoy, solo si cambia; una carta no tiene producto-menu.
                if(request.PriceCents is not null)
                {
                    if(offer.Kind==OfferKind.ALaCarte || offer.ProductId is null) throw new RuleViolation("product_not_allowed","An a la carte offer has no menu product to price.");
                    var price=Catalog.Price(Catalog.GeneralTariff,offer.ProductId,Offers.PersonPresentation,CatalogOperations.Today(),request.PriceCents);
                    if(await unit.SetPrice(price)) events.Add(PriceEvent(price));
                }
                await unit.Events(events);
                return offer;
            }
            // E4d: el orden de las ofertas en una sola orden: las indicadas, en ese orden, delante; las demas detras, como estaban.
            case "offers-reorder":
            {
                var ids=(request.OfferIds??throw new RuleViolation("offer_ids_required","offerIds is required.")).Select(i=>Organization.Code(i,"offer id")).ToArray();
                if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length) throw new RuleViolation("duplicate_offer","offerIds repeats an offer.");
                var all=await unit.Offers();
                var byId=all.ToDictionary(o=>o.Id,StringComparer.Ordinal);
                foreach(var i in ids) if(!byId.ContainsKey(i)) throw new StoreNotFound();
                var ordered=ids.Select(i=>byId[i]).Concat(all.Where(o=>!ids.Contains(o.Id,StringComparer.Ordinal))).ToArray();
                var events=new List<DomainEvent>(); var result=new List<object>();
                for(var n=0;n<ordered.Length;n++)
                {
                    var sort=Math.Min(9999,n*10);
                    if(ordered[n].Sort!=sort)
                    {
                        var changed=ordered[n] with { Sort=sort };
                        await unit.UpdateOffer(changed); events.Add(Event("offer_updated",changed.Id,("name",changed.Name),("sort",sort.ToString())));
                    }
                    result.Add(new { id=ordered[n].Id,sort });
                }
                if(events.Count>0) await unit.Events(events);
                return result;
            }
            case "offer-deactivate": case "offer-reactivate":
            {
                var current=await unit.Offer(Required(request.Id));
                var offer=current with { Active=active };
                await unit.UpdateOffer(offer); await unit.Events([Event(active ? "offer_reactivated" : "offer_deactivated",offer.Id)]); return offer;
            }
            case "course-create":
            {
                _=await unit.Offer(Required(request.OfferId));
                var courses=await unit.OfferCourses(request.OfferId!);
                var name=Organization.Name(request.Name);
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>courses.Any(x=>string.Equals(x.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id;
                var course=Offers.Course(request.OfferId,id,name,request.Sort??Catalog.NextSort(courses.Select(c=>c.Sort)));
                await unit.InsertOfferCourse(course); await unit.Events([Event("course_created",course.OfferId,("course",course.Id),("name",course.Name))]); return course;
            }
            case "course-update":
            {
                var current=await unit.OfferCourse(Required(request.OfferId),Required(request.Id));
                var course=Offers.Course(current.OfferId,current.Id,request.Name??current.Name,request.Sort??current.Sort,current.Active);
                await unit.UpdateOfferCourse(course); await unit.Events([Event("course_updated",course.OfferId,("course",course.Id),("name",course.Name))]); return course;
            }
            case "course-deactivate": case "course-reactivate":
            {
                var current=await unit.OfferCourse(Required(request.OfferId),Required(request.Id));
                var course=current with { Active=active };
                await unit.UpdateOfferCourse(course); await unit.Events([Event(active ? "course_reactivated" : "course_deactivated",course.OfferId,("course",course.Id))]); return course;
            }
            case "dish-create":
            {
                _=await unit.OfferCourse(Required(request.OfferId),Required(request.CourseId));
                var dishes=(await unit.OfferDishes(request.OfferId!)).Where(d=>d.CourseId==request.CourseId).ToList();
                var name=Organization.Name(request.Name);
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>dishes.Any(x=>string.Equals(x.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id;
                var dish=Offers.Dish(request.OfferId,request.CourseId,id,name,request.StationId,request.ProductId,request.Sort??Catalog.NextSort(dishes.Select(d=>d.Sort)));
                await CheckDishLinks(unit,dish);
                await unit.InsertOfferDish(dish); await unit.Events([Event("dish_created",dish.OfferId,("course",dish.CourseId),("dish",dish.Id),("name",dish.Name))]); return dish;
            }
            case "dish-update":
            {
                var current=await unit.OfferDish(Required(request.OfferId),Required(request.CourseId),Required(request.Id));
                var dish=Offers.Dish(current.OfferId,current.CourseId,current.Id,request.Name??current.Name,request.StationId??current.StationId,
                    request.ProductId is null ? current.ProductId : request.ProductId,request.Sort??current.Sort,current.Active);
                if(dish.StationId!=current.StationId || dish.ProductId!=current.ProductId) await CheckDishLinks(unit,dish);
                await unit.UpdateOfferDish(dish); await unit.Events([Event("dish_updated",dish.OfferId,("course",dish.CourseId),("dish",dish.Id),("name",dish.Name))]); return dish;
            }
            // E4b: items de un grupo de carta (producto + presentacion activos del catalogo; el producto necesita estacion para ser plato).
            case "item-create":
            {
                var item=Offers.Item(Required(request.OfferId),Required(request.CourseId),Required(request.ProductId),Required(request.PresentationId),request.Sort);
                var offer=await unit.Offer(item.OfferId);
                if(offer.Kind!=OfferKind.ALaCarte) throw new RuleViolation("not_a_la_carte","Items belong to a la carte offers; set menus and tastings use dishes.");
                _=await unit.OfferCourse(item.OfferId,item.CourseId);
                var (product,_)=await unit.Sellable(item.ProductId,item.PresentationId);
                if(product.StationId is null) throw new RuleViolation("station_required","A dish needs a kitchen station on its product (drinks are ordered as consumptions).");
                await unit.InsertOfferItem(item);
                await unit.Events([Event("item_created",item.OfferId,("course",item.CourseId),("product",item.ProductId),("presentation",item.PresentationId))]);
                return item;
            }
            case "item-deactivate": case "item-reactivate":
            {
                var current=(await unit.OfferItems(Required(request.OfferId))).FirstOrDefault(i=>i.CourseId==Required(request.CourseId) && i.Item.ProductId==Required(request.ProductId) && i.Item.PresentationId==Required(request.PresentationId));
                if(current.Item is null) throw new StoreNotFound();
                var item=new OfferItemDefinition(current.OfferId,current.CourseId,current.Item.ProductId,current.Item.PresentationId,current.Item.Sort,active);
                await unit.UpdateOfferItem(item);
                await unit.Events([Event(active ? "item_reactivated" : "item_deactivated",item.OfferId,("course",item.CourseId),("product",item.ProductId),("presentation",item.PresentationId))]);
                return item;
            }
            case "dish-deactivate": case "dish-reactivate":
            {
                var current=await unit.OfferDish(Required(request.OfferId),Required(request.CourseId),Required(request.Id));
                var dish=current with { Active=active };
                await unit.UpdateOfferDish(dish); await unit.Events([Event(active ? "dish_reactivated" : "dish_deactivated",dish.OfferId,("course",dish.CourseId),("dish",dish.Id))]); return dish;
            }
            default: throw new StoreNotFound();
        }
    }

    // El producto-menu existe, esta activo y se vende por persona (presentacion 'person').
    private static async Task CheckProduct(Unit unit,OfferDefinition offer)
    {
        if(offer.ProductId is null) return;
        var product=await unit.Product(offer.ProductId);
        if(!product.Active) throw new RuleViolation("product_unavailable","The menu product is deactivated.");
        if((await unit.Presentations(product.Id)).All(p=>p.Id!=Offers.PersonPresentation || !p.Active))
            throw new RuleViolation("presentation_required",$"The menu product needs an active '{Offers.PersonPresentation}' presentation.");
    }
    private static async Task CheckDishLinks(Unit unit,OfferDishDefinition dish)
    {
        if(!(await unit.Station(dish.StationId)).Active) throw new RuleViolation("station_inactive","The station is deactivated.");
        if(dish.ProductId is not null && !(await unit.Product(dish.ProductId)).Active) throw new RuleViolation("product_unavailable","The dish product is deactivated.");
    }

    public static void MapOfferRoutes(this WebApplication app,PostgresStore store,NpgsqlDataSource source,BusinessScope scope)
    {
        const string prefix="/api/native/v1/erp/offers";
        var reads=new DesktopReadRepository(source);
        app.MapGet(prefix,(Func<HttpContext,Task<IResult>>)(async c=>Json(await reads.Offers(scope,c.RequestAborted)))).WithMetadata(new RouteAccess("main"));
        app.MapPost(prefix+"/commands/{action}",(HttpContext c,string action)=>Write<OfferCommand>(c,store,scope,(u,i,r)=>Apply(u,i,action,r)))
            .WithMetadata(new RouteAccess("main") { ActionPolicy=Allows });
    }
}
