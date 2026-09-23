using Costina.Core.Domain;
using Costina.Core.Hosting;
using Costina.Core.Persistence;

namespace Costina.Server;

// Explicit fictitious fixture installation. Not a product initializer or a migration of Laravel data.
// El nucleo siembra organizacion y catalogo; cada modulo activo anade lo suyo dentro del MISMO comando idempotente
// (misma clave lab-fixtures-v1: la marca de demo de D5.6 no cambia).
public static class LabConfiguration
{
    public static async Task Seed(PostgresStore store,BusinessScope scope,IReadOnlyList<IModule> modules)
    {
        await store.ExecuteAsync(new(scope,"lab-initializer"),"lab-fixtures-v1","lab-fixtures-v1",async unit =>
        {
            // E2: organizacion relacional (salas, mesas, estaciones); la estacion de pase existe siempre (init la garantiza).
            await unit.SeedZone(new ZoneDefinition("sala","Sala",0,true));
            for (var i=1;i<=8;i++) await unit.SeedTable(new TableDefinition("M"+i,"Mesa "+i,12,"sala",i-1,true));
            foreach(var station in new StationDefinition[] { new("pase","Pase",StationKind.Pass,0,true), new("cold","Cocina fría",StationKind.Kitchen,1,true),
                new("hot","Cocina caliente",StationKind.Kitchen,2,true), new("sala-1","Sala 1",StationKind.Room,3,true) })
                await unit.SeedStation(station);
            // E3: catalogo relacional. Impuestos y tarifa general los garantiza el motor (EnsureOrganizationAsync); aqui categorias,
            // productos con sus presentaciones vendibles y precios en la tarifa general.
            await unit.SeedCategory(new CategoryDefinition("bebidas","Bebidas",null,"#1F4D3A",0,true));
            await unit.SeedCategory(new CategoryDefinition("vinos","Vinos","bebidas","#8C1D1D",1,true));
            await unit.SeedProduct(new ProductDefinition("water","Agua mineral","bebidas","iva-10",null,0,true));
            await unit.SeedPresentation(new PresentationDefinition("water","bottle","Botella",0,true));
            await unit.SeedPrice(new PriceDefinition("general","water","bottle",new DateOnly(1970,1,1),400));
            await unit.SeedProduct(new ProductDefinition("wine","Vino de ensayo","vinos","iva-21","3754",1,true));
            await unit.SeedPresentation(new PresentationDefinition("wine","glass","Copa",0,true));
            await unit.SeedPresentation(new PresentationDefinition("wine","bottle","Botella",1,true));
            await unit.SeedPrice(new PriceDefinition("general","wine","glass",new DateOnly(1970,1,1),950));
            await unit.SeedPrice(new PriceDefinition("general","wine","bottle",new DateOnly(1970,1,1),4200));
            // E4a: OFERTA. La degustacion de ensayo (antes menu JSONB del modulo) y un menu cerrado con eleccion por comensal;
            // cada una se vende como producto-menu por persona a las tarifas del catalogo.
            await unit.SeedCategory(new CategoryDefinition("menus","Menús",null,"#B5673A",99,true));
            await unit.SeedProduct(new ProductDefinition("menu-lab-tasting","Menú de ensayo","menus","iva-10",null,90,true));
            await unit.SeedPresentation(new PresentationDefinition("menu-lab-tasting","person","Por persona",0,true));
            await unit.SeedPrice(new PriceDefinition("general","menu-lab-tasting","person",new DateOnly(1970,1,1),15000));
            await unit.SeedOffer(new OfferDefinition("LAB-TASTING","Menú de ensayo",OfferKind.Tasting,"menu-lab-tasting",OfferService.Any,null,null,127,0,true));
            await unit.SeedOfferCourse(new OfferCourseDefinition("LAB-TASTING","p1","Aperitivos",0,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-TASTING","p1","frio","Preparación fría","cold",null,0,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-TASTING","p1","caliente","Preparación caliente","hot",null,1,true));
            await unit.SeedOfferCourse(new OfferCourseDefinition("LAB-TASTING","p2","Segundo pase",1,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-TASTING","p2","principal","Preparación principal","hot",null,0,true));
            await unit.SeedProduct(new ProductDefinition("menu-lab-daily","Menú del día de ensayo","menus","iva-10",null,91,true));
            await unit.SeedPresentation(new PresentationDefinition("menu-lab-daily","person","Por persona",0,true));
            await unit.SeedPrice(new PriceDefinition("general","menu-lab-daily","person",new DateOnly(1970,1,1),1800));
            await unit.SeedOffer(new OfferDefinition("LAB-DAILY","Menú del día de ensayo",OfferKind.SetMenu,"menu-lab-daily",OfferService.Any,null,null,127,1,true));
            await unit.SeedOfferCourse(new OfferCourseDefinition("LAB-DAILY","primeros","Primeros",0,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-DAILY","primeros","ensalada","Ensalada de la huerta","cold",null,0,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-DAILY","primeros","sopa","Sopa del día","hot",null,1,true));
            await unit.SeedOfferCourse(new OfferCourseDefinition("LAB-DAILY","segundos","Segundos",1,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-DAILY","segundos","pescado","Pescado del día","hot",null,0,true));
            await unit.SeedOfferDish(new OfferDishDefinition("LAB-DAILY","segundos","carne","Carne guisada","hot",null,1,true));
            foreach(var module in modules) await module.SeedDemoAsync(unit);
            return new { initialized=true };
        });
    }
}
