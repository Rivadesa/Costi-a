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
            foreach(var module in modules) await module.SeedDemoAsync(unit);
            return new { initialized=true };
        });
    }
}
