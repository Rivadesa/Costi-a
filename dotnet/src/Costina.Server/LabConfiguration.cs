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
            foreach(var product in new ProductDefinition[] {
                new("water","Agua mineral","Botella",400,true),new("wine-glass","Vino de ensayo","Copa",950,true),
                new("wine-bottle","Vino de ensayo","Botella",4200,true)})
                await unit.SeedConfiguration("product",product.Id,product);
            foreach(var module in modules) await module.SeedDemoAsync(unit);
            return new { initialized=true };
        });
    }
}
