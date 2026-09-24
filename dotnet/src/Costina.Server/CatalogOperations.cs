using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;
using Npgsql;
using static Costina.Core.Hosting.Requests;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Server;

// E3: comandos del CATALOGO Y TARIFAS (nucleo, solo puesto principal). Misma tuberia que todo: idempotencia, auditoria, outbox.
// Sin version esperada: datos maestros, la ultima edicion gana; el cliente relee tras cada comando.
public sealed record CatalogCommand(string? Id=null,string? Name=null,decimal? Rate=null,string? ParentId=null,string? Color=null,int? Sort=null,
    string? CategoryId=null,string? TaxId=null,string? Reference=null,string? Presentation=null,string? ProductId=null,string? PresentationId=null,
    string? TariffId=null,string? ValidFrom=null,long? PriceCents=null,string? StationId=null);

public static class CatalogOperations
{
    public static readonly string[] Actions = [
        "tax-create","tax-update","tax-deactivate","tax-reactivate",
        "category-create","category-update","category-deactivate","category-reactivate",
        "product-create","product-update","product-deactivate","product-reactivate",
        "presentation-create","presentation-update","presentation-deactivate","presentation-reactivate",
        "tariff-create","tariff-update","tariff-deactivate","tariff-reactivate","price-set"];
    public static bool Allows(string role,string action) => role=="main" && Actions.Contains(action,StringComparer.Ordinal);
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static async Task<object> Apply(Unit unit,ExecutionIdentity identity,string action,CatalogCommand request)
    {
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        DomainEvent Event(string type,string aggregate,params (string Key,string Value)[] data)
            => new(Guid.NewGuid(),"catalog."+type,aggregate,identity.Scope,identity.ActorId,stamp.At,data.ToDictionary(d=>d.Key,d=>d.Value));
        var active=action.EndsWith("-reactivate",StringComparison.Ordinal);
        switch(action)
        {
            case "tax-create":
            {
                var tax=Catalog.Tax(Required(request.Id),request.Name,request.Rate);
                await unit.InsertTax(tax); await unit.Events([Event("tax_created",tax.Id,("name",tax.Name))]); return tax;
            }
            case "tax-update":
            {
                var current=await unit.Tax(Required(request.Id));
                var tax=Catalog.Tax(current.Id,request.Name??current.Name,request.Rate??current.Rate,current.Active);
                await unit.UpdateTax(tax); await unit.Events([Event("tax_updated",tax.Id,("name",tax.Name))]); return tax;
            }
            case "tax-deactivate": case "tax-reactivate":
            {
                var current=await unit.Tax(Required(request.Id));
                if(!active && (await unit.Products()).Any(p=>p.Active && p.TaxId==current.Id)) throw new RuleViolation("tax_in_use","Active products use this tax; change them first.");
                var tax=current with { Active=active };
                await unit.UpdateTax(tax); await unit.Events([Event(active ? "tax_reactivated" : "tax_deactivated",tax.Id)]); return tax;
            }
            case "category-create":
            {
                var category=Catalog.Category(Required(request.Id),request.Name,request.ParentId,request.Color,request.Sort);
                await CheckParent(unit,category);
                await unit.InsertCategory(category); await unit.Events([Event("category_created",category.Id,("name",category.Name))]); return category;
            }
            case "category-update":
            {
                var current=await unit.Category(Required(request.Id));
                var category=Catalog.Category(current.Id,request.Name??current.Name,request.ParentId??current.ParentId,request.Color??current.Color,request.Sort??current.Sort,current.Active);
                if(request.ParentId=="") category=category with { ParentId=null };       // cadena vacia = sin padre
                if(request.Color=="") category=category with { Color=null };
                if(category.ParentId!=current.ParentId) await CheckParent(unit,category);
                await unit.UpdateCategory(category); await unit.Events([Event("category_updated",category.Id,("name",category.Name))]); return category;
            }
            case "category-deactivate": case "category-reactivate":
            {
                var current=await unit.Category(Required(request.Id));
                if(!active)
                {
                    if((await unit.Products()).Any(p=>p.Active && p.CategoryId==current.Id)) throw new RuleViolation("category_in_use","Active products belong to this category; move or deactivate them first.");
                    if((await unit.Categories()).Any(c=>c.Active && c.ParentId==current.Id)) throw new RuleViolation("category_in_use","Active subcategories hang from this category.");
                }
                else if(current.ParentId is not null && !(await unit.Category(current.ParentId)).Active) throw new RuleViolation("category_inactive","Reactivate the parent category first.");
                var category=current with { Active=active };
                await unit.UpdateCategory(category); await unit.Events([Event(active ? "category_reactivated" : "category_deactivated",category.Id)]); return category;
            }
            case "product-create":
            {
                var product=Catalog.Product(Required(request.Id),request.Name,request.CategoryId,request.TaxId??"iva-10",request.Reference,request.Sort,stationId:request.StationId);
                await CheckProductLinks(unit,product);
                await unit.InsertProduct(product);
                // Un producto nace vendible por UNA presentacion ('unit' o la indicada); el precio se fija aparte (price-set).
                var presentation=Catalog.Presentation(product.Id,Catalog.DefaultPresentation,request.Presentation??"Unidad",0);
                await unit.InsertPresentation(presentation);
                await unit.Events([Event("product_created",product.Id,("name",product.Name))]);
                return new { product.Id,product.Name,product.CategoryId,product.TaxId,product.Reference,product.Sort,product.Active,product.StationId,presentationId=presentation.Id };
            }
            case "product-update":
            {
                var current=await unit.Product(Required(request.Id));
                var product=Catalog.Product(current.Id,request.Name??current.Name,request.CategoryId??current.CategoryId,request.TaxId??current.TaxId,request.Reference??current.Reference,request.Sort??current.Sort,current.Active,request.StationId??current.StationId);
                if(request.CategoryId=="") product=product with { CategoryId=null };
                if(request.Reference=="") product=product with { Reference=null };
                if(request.StationId=="") product=product with { StationId=null };   // E4b: sin estacion = bebida o consumo directo
                if(product.CategoryId!=current.CategoryId || product.TaxId!=current.TaxId || product.StationId!=current.StationId) await CheckProductLinks(unit,product);
                await unit.UpdateProduct(product); await unit.Events([Event("product_updated",product.Id,("name",product.Name))]); return product;
            }
            case "product-deactivate": case "product-reactivate":
            {
                var current=await unit.Product(Required(request.Id));
                if(active) await CheckProductLinks(unit,current);
                var product=current with { Active=active };
                await unit.UpdateProduct(product); await unit.Events([Event(active ? "product_reactivated" : "product_deactivated",product.Id)]); return product;
            }
            case "presentation-create":
            {
                var presentation=Catalog.Presentation(Required(request.ProductId),Required(request.Id),request.Name,request.Sort);
                _=await unit.Product(presentation.ProductId);
                await unit.InsertPresentation(presentation); await unit.Events([Event("presentation_created",presentation.ProductId,("presentation",presentation.Id),("name",presentation.Name))]); return presentation;
            }
            case "presentation-update":
            {
                var current=await unit.Presentation(Required(request.ProductId),Required(request.Id));
                var presentation=Catalog.Presentation(current.ProductId,current.Id,request.Name??current.Name,request.Sort??current.Sort,current.Active);
                await unit.UpdatePresentation(presentation); await unit.Events([Event("presentation_updated",presentation.ProductId,("presentation",presentation.Id),("name",presentation.Name))]); return presentation;
            }
            case "presentation-deactivate": case "presentation-reactivate":
            {
                var current=await unit.Presentation(Required(request.ProductId),Required(request.Id));
                var presentation=current with { Active=active };
                await unit.UpdatePresentation(presentation); await unit.Events([Event(active ? "presentation_reactivated" : "presentation_deactivated",presentation.ProductId,("presentation",presentation.Id))]); return presentation;
            }
            case "tariff-create":
            {
                var tariff=Catalog.Tariff(Required(request.Id),request.Name,request.Sort);
                await unit.InsertTariff(tariff); await unit.Events([Event("tariff_created",tariff.Id,("name",tariff.Name))]); return tariff;
            }
            case "tariff-update":
            {
                var current=await unit.Tariff(Required(request.Id));
                var tariff=Catalog.Tariff(current.Id,request.Name??current.Name,request.Sort??current.Sort,current.Active);
                await unit.UpdateTariff(tariff); await unit.Events([Event("tariff_updated",tariff.Id,("name",tariff.Name))]); return tariff;
            }
            case "tariff-deactivate": case "tariff-reactivate":
            {
                var current=await unit.Tariff(Required(request.Id));
                var tariff=Catalog.Tariff(current.Id,current.Name,current.Sort,active);          // general: reserved_tariff
                if(!active && (await unit.Zones()).Any(z=>z.Active && z.TariffId==current.Id)) throw new RuleViolation("tariff_in_use","Active zones use this tariff; change them first.");
                await unit.UpdateTariff(tariff); await unit.Events([Event(active ? "tariff_reactivated" : "tariff_deactivated",tariff.Id)]); return tariff;
            }
            case "price-set":
            {
                var price=Catalog.Price(Required(request.TariffId),Required(request.ProductId),Required(request.PresentationId),Catalog.ValidFrom(request.ValidFrom,Today()),request.PriceCents);
                if(!(await unit.Tariff(price.TariffId)).Active) throw new RuleViolation("tariff_inactive","The tariff is deactivated.");
                _=await unit.Presentation(price.ProductId,price.PresentationId);
                var changed=await unit.SetPrice(price);
                if(changed) await unit.Events([Event("price_set",price.ProductId,("presentation",price.PresentationId),("tariff",price.TariffId),("valid_from",price.ValidFrom.ToString("yyyy-MM-dd")),("price_cents",price.PriceCents.ToString()))]);
                return new { price.TariffId,price.ProductId,price.PresentationId,validFrom=price.ValidFrom.ToString("yyyy-MM-dd"),price.PriceCents,changed };
            }
            default: throw new StoreNotFound();
        }
    }

    // El padre existe, esta activo y no crea ciclos ni mas de cuatro niveles.
    private static async Task CheckParent(Unit unit,CategoryDefinition category)
    {
        if(category.ParentId is null) return;
        var categories=(await unit.Categories()).ToDictionary(c=>c.Id,StringComparer.Ordinal);
        if(!categories.TryGetValue(category.ParentId,out var parent)) throw new StoreNotFound();
        if(!parent.Active) throw new RuleViolation("category_inactive","The parent category is deactivated.");
        Catalog.Depth(category.Id,category.ParentId,id=>categories.GetValueOrDefault(id)?.ParentId);
        // Los descendientes actuales tambien deben caber: la profundidad se comprueba desde el mas hondo.
        var children=categories.Values.Where(c=>c.ParentId==category.Id).ToList();
        foreach(var child in children) Catalog.Depth(child.Id,category.Id,id=>id==category.Id ? category.ParentId : categories.GetValueOrDefault(id)?.ParentId);
    }
    private static async Task CheckProductLinks(Unit unit,ProductDefinition product)
    {
        if(product.CategoryId is not null && !(await unit.Category(product.CategoryId)).Active) throw new RuleViolation("category_inactive","The category is deactivated.");
        if(!(await unit.Tax(product.TaxId)).Active) throw new RuleViolation("tax_inactive","The tax is deactivated.");
        if(product.StationId is not null && !(await unit.Station(product.StationId)).Active) throw new RuleViolation("station_inactive","The station is deactivated.");
    }

    // Rutas (nucleo, solo main): catalogo completo para editar y comandos. Los catalogos operativos (/catalog, /checkout/catalog)
    // siguen en DesktopReadRoutes.
    public static void MapCatalogRoutes(this WebApplication app,PostgresStore store,NpgsqlDataSource source,BusinessScope scope)
    {
        const string prefix="/api/native/v1/erp/catalog";
        var reads=new DesktopReadRepository(source);
        app.MapGet(prefix,(Func<HttpContext,Task<IResult>>)(async c=>Json(await reads.FullCatalog(scope,Today(),c.RequestAborted))))
            .WithMetadata(new RouteAccess("main"));
        app.MapPost(prefix+"/commands/{action}",(HttpContext c,string action)=>
            Write<CatalogCommand>(c,store,scope,(u,i,r)=>Apply(u,i,action,r)))
            .WithMetadata(new RouteAccess("main") { ActionPolicy=Allows });
    }
}
