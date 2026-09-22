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
            for (var i=1;i<=8;i++) await unit.SeedConfiguration("table","M"+i,new TableDefinition("M"+i,"Mesa "+i,12));
            foreach(var product in new ProductDefinition[] {
                new("water","Agua mineral","Botella",400,true),new("wine-glass","Vino de ensayo","Copa",950,true),
                new("wine-bottle","Vino de ensayo","Botella",4200,true)})
                await unit.SeedConfiguration("product",product.Id,product);
            foreach(var module in modules) await module.SeedDemoAsync(unit);
            return new { initialized=true };
        });
    }
}
