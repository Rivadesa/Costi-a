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
    string? TariffId=null,string? ValidFrom=null,long? PriceCents=null,string? StationId=null,string? ContentType=null,byte[]? Data=null,
    GlassItem[]? Items=null,string[]? Stop=null,string[]? ProductIds=null);
// E3.3: un vino por copa con su precio (glass-sale-set).
public sealed record GlassItem(string? ProductId=null,long? PriceCents=null);

public static class CatalogOperations
{
    public static readonly string[] Actions = [
        "tax-create","tax-update","tax-deactivate","tax-reactivate",
        "category-create","category-update","category-deactivate","category-reactivate",
        "product-create","product-update","product-deactivate","product-reactivate",
        "presentation-create","presentation-update","presentation-deactivate","presentation-reactivate",
        "tariff-create","tariff-update","tariff-deactivate","tariff-reactivate","price-set",
        "product-image-set","product-image-remove","glass-sale-set","products-move","products-tax-set"];
    // E3.3: vinos por orden de glass-sale-set (unos 21 KB en 300: dentro de MaxCommandBody). El puesto parte listas mayores.
    public const int MaxGlassItems=300;
    public static bool Allows(string role,string action) => role=="main" && Actions.Contains(action,StringComparer.Ordinal);
    // E3.2: cuerpo maximo de product-image-set (y solo de esa orden): 2 MB de imagen son ~2,8 MB en base64; el resto es margen para
    // el JSON y para un serializador que escape '+' y '/' como secuencias unicode de seis caracteres. El dominio sigue exigiendo
    // 2 MB a los bytes ya decodificados.
    public const int ImageCommandBody = 4 * 1024 * 1024;
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static async Task<object> Apply(Unit unit,ExecutionIdentity identity,string action,CatalogCommand request)
    {
        var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        DomainEvent Event(string type,string aggregate,params (string Key,string Value)[] data)
            => new(Guid.NewGuid(),"catalog."+type,aggregate,identity.Scope,identity.ActorId,stamp.At,data.ToDictionary(d=>d.Key,d=>d.Value));
        var active=action.EndsWith("-reactivate",StringComparison.Ordinal);
        switch(action)
        {
            // E4d: en las altas el codigo es opcional (generado del nombre, unico en su lista) y el orden tambien (al final).
            case "tax-create":
            {
                var name=Organization.Name(request.Name);
                var taxes=await unit.Taxes();
                var tax=Catalog.Tax(string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>taxes.Any(t=>string.Equals(t.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id,name,request.Rate);
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
                var name=Organization.Name(request.Name);
                var categories=await unit.Categories();
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>categories.Any(x=>string.Equals(x.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id;
                if(Catalog.IsSection(id?.Trim())) throw new RuleViolation("reserved_category","That code is a reserved section of the catalog.");   // E3.3
                var category=Catalog.Category(id,name,request.ParentId,request.Color,request.Sort??Catalog.NextSort(categories.Where(c=>c.ParentId==(string.IsNullOrWhiteSpace(request.ParentId) ? null : request.ParentId)).Select(c=>c.Sort)));
                await CheckParent(unit,category);
                await unit.InsertCategory(category); await unit.Events([Event("category_created",category.Id,("name",category.Name))]); return category;
            }
            case "category-update":
            {
                var current=await unit.Category(Required(request.Id));
                var category=Catalog.Category(current.Id,request.Name??current.Name,request.ParentId??current.ParentId,request.Color??current.Color,request.Sort??current.Sort,current.Active);
                if(request.ParentId=="") category=category with { ParentId=null };       // cadena vacia = sin padre
                if(Catalog.IsSection(current.Id) && category.ParentId is not null) throw new RuleViolation("reserved_category","A section of the catalog stays at the root.");   // E3.3: solo cambia de nombre
                if(request.Color=="") category=category with { Color=null };
                if(category.ParentId!=current.ParentId) await CheckParent(unit,category);
                await unit.UpdateCategory(category); await unit.Events([Event("category_updated",category.Id,("name",category.Name))]); return category;
            }
            case "category-deactivate": case "category-reactivate":
            {
                var current=await unit.Category(Required(request.Id));
                if(!active && Catalog.IsSection(current.Id)) throw new RuleViolation("reserved_category","A section of the catalog cannot be deactivated.");   // E3.3
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
                var name=Organization.Name(request.Name);
                var products=await unit.Products();
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>products.Any(p=>string.Equals(p.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id;
                var product=Catalog.Product(id,name,request.CategoryId,request.TaxId??"iva-10",request.Reference,request.Sort??Catalog.NextSort(products.Select(p=>p.Sort)),stationId:request.StationId);
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
                _=await unit.Product(Required(request.ProductId));
                var name=Organization.Name(request.Name);
                var presentations=await unit.Presentations(request.ProductId!);
                var id=string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>presentations.Any(p=>string.Equals(p.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id;
                var presentation=Catalog.Presentation(request.ProductId,id,name,request.Sort??Catalog.NextSort(presentations.Select(p=>p.Sort)));
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
                var name=Organization.Name(request.Name);
                var tariffs=await unit.Tariffs();
                var tariff=Catalog.Tariff(string.IsNullOrWhiteSpace(request.Id) ? Catalog.FreeCode(name,c=>tariffs.Any(t=>string.Equals(t.Id,c,StringComparison.OrdinalIgnoreCase))) : request.Id,name,request.Sort??Catalog.NextSort(tariffs.Select(t=>t.Sort)));
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
            // E3.2: imagen del articulo. La respuesta, la auditoria y el aviso llevan huella, tamano y tipo: NUNCA los bytes.
            case "product-image-set":
            {
                var product=await unit.Product(Required(request.ProductId));
                var image=Catalog.Image(product.Id,request.ContentType,request.Data);
                var changed=await unit.SetImage(image);
                if(changed) await unit.Events([Event("product_image_set",product.Id,("sha256",image.Sha256),("bytes",image.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)),("content_type",image.ContentType))]);
                return new { productId=product.Id,sha256=image.Sha256,bytes=image.Bytes,contentType=image.ContentType,changed };
            }
            // E3.3: vinos por copa en bloque: por cada vino, la presentacion reservada 'copa' (creada o reactivada) con su precio en la
            // General desde hoy; los de 'stop' dejan de venderse por copa (su precio se queda). Una sola orden, auditada por vino.
            case "glass-sale-set":
            {
                var items=request.Items??[]; var stop=request.Stop??[];
                if(items.Length+stop.Length>MaxGlassItems) throw new RuleViolation("too_many_items",$"At most {MaxGlassItems} wines per order; send the rest in another.");
                if(items.Length+stop.Length==0) throw new RuleViolation("nothing_to_do","items or stop is required.");
                var events=new List<DomainEvent>(); int marked=0,stopped=0,priced=0;
                foreach(var item in items)
                {
                    var product=await unit.Product(Required(item.ProductId));
                    if(!product.Active) throw new RuleViolation("product_unavailable",$"Product {product.Id} is deactivated.");
                    var glass=(await unit.Presentations(product.Id)).FirstOrDefault(p=>p.Id==Catalog.GlassPresentation);
                    if(glass is null)
                    {
                        await unit.InsertPresentation(Catalog.Presentation(product.Id,Catalog.GlassPresentation,"Copa",0));
                        events.Add(Event("presentation_created",product.Id,("presentation",Catalog.GlassPresentation),("name","Copa"))); marked++;
                    }
                    else if(!glass.Active)
                    {
                        await unit.UpdatePresentation(glass with { Active=true });
                        events.Add(Event("presentation_reactivated",product.Id,("presentation",Catalog.GlassPresentation))); marked++;
                    }
                    var price=Catalog.Price(Catalog.GeneralTariff,product.Id,Catalog.GlassPresentation,Today(),item.PriceCents);
                    if(await unit.SetPrice(price))
                    {
                        events.Add(Event("price_set",price.ProductId,("presentation",price.PresentationId),("tariff",price.TariffId),("valid_from",price.ValidFrom.ToString("yyyy-MM-dd")),("price_cents",price.PriceCents.ToString()))); priced++;
                    }
                }
                foreach(var id in stop)
                {
                    var product=await unit.Product(Required(id));
                    var glass=(await unit.Presentations(product.Id)).FirstOrDefault(p=>p.Id==Catalog.GlassPresentation);
                    if(glass is { Active:true })
                    {
                        await unit.UpdatePresentation(glass with { Active=false });
                        events.Add(Event("presentation_deactivated",product.Id,("presentation",Catalog.GlassPresentation))); stopped++;
                    }
                }
                if(events.Count>0) await unit.Events(events);
                return new { marked,priced,stopped };
            }
            // E3.3: mover varios productos de familia en una orden (ordenar la bodega sin ir de uno en uno). categoryId vacio = sin familia.
            case "products-move":
            {
                var ids=(request.ProductIds??throw new RuleViolation("product_ids_required","productIds is required.")).Distinct(StringComparer.Ordinal).ToArray();
                if(ids.Length==0) throw new RuleViolation("product_ids_required","productIds is required.");
                var categoryId=string.IsNullOrWhiteSpace(request.CategoryId) ? null : Organization.Code(request.CategoryId,"category id");
                if(categoryId is not null && !(await unit.Category(categoryId)).Active) throw new RuleViolation("category_inactive","The category is deactivated.");
                var events=new List<DomainEvent>(); var moved=0;
                foreach(var id in ids)
                {
                    var current=await unit.Product(Required(id));
                    if(current.CategoryId==categoryId) continue;
                    var product=current with { CategoryId=categoryId };
                    await unit.UpdateProduct(product); events.Add(Event("product_updated",product.Id,("name",product.Name),("category",categoryId??""))); moved++;
                }
                if(events.Count>0) await unit.Events(events);
                return new { moved,categoryId };
            }
            // E3.3: cambiar el IVA de varios productos de una vez (decision del promotor: el tipo lo dira el gestor; se cambia sin reimportar).
            case "products-tax-set":
            {
                var ids=(request.ProductIds??throw new RuleViolation("product_ids_required","productIds is required.")).Distinct(StringComparer.Ordinal).ToArray();
                if(ids.Length==0) throw new RuleViolation("product_ids_required","productIds is required.");
                var tax=await unit.Tax(Required(request.TaxId));
                if(!tax.Active) throw new RuleViolation("tax_inactive","The tax is deactivated.");
                var events=new List<DomainEvent>(); var changed=0;
                foreach(var id in ids)
                {
                    var current=await unit.Product(Required(id));
                    if(current.TaxId==tax.Id) continue;
                    var product=current with { TaxId=tax.Id };
                    await unit.UpdateProduct(product); events.Add(Event("product_updated",product.Id,("name",product.Name),("tax",tax.Id))); changed++;
                }
                if(events.Count>0) await unit.Events(events);
                return new { changed,taxId=tax.Id };
            }
            case "product-image-remove":
            {
                var product=await unit.Product(Required(request.ProductId));
                var current=await unit.ImageOf(product.Id);
                var changed=await unit.RemoveImage(product.Id);
                if(changed) await unit.Events([Event("product_image_removed",product.Id,("sha256",current!.Sha256))]);
                return new { productId=product.Id,changed };
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
        // El middleware ya ha exigido main y una accion anunciada antes de llegar aqui: solo esa orden de main lee un cuerpo grande.
        app.MapPost(prefix+"/commands/{action}",(HttpContext c,string action)=>
            Write<CatalogCommand>(c,store,scope,(u,i,r)=>Apply(u,i,action,r),action=="product-image-set" ? ImageCommandBody : MaxCommandBody))
            .WithMetadata(new RouteAccess("main") { ActionPolicy=Allows });
    }
}
