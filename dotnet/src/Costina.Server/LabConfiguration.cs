using Costina.Domain;
using Costina.Persistence;

namespace Costina.Server;

public sealed record TableDefinition(string Id,string Name,int Capacity);
public sealed record MenuDefinition(string Id,string Name,long UnitPriceCents,CourseDefinition[] Courses);
public sealed record ProductDefinition(string Id,string Name,string Presentation,long PriceCents,bool Active);

// Explicit fictitious fixture installation. Not a product initializer or a migration of Laravel data.
public static class LabConfiguration
{
    public static async Task Seed(PostgresStore store,BusinessScope scope)
    {
        await store.ExecuteAsync(new(scope,"lab-initializer"),"lab-fixtures-v1","lab-fixtures-v1",async unit =>
        {
            for (var i=1;i<=8;i++) await unit.SeedConfiguration("table","M"+i,new TableDefinition("M"+i,"Mesa "+i,12));
            await unit.SeedConfiguration("menu","LAB-TASTING",new MenuDefinition("LAB-TASTING","Menú de ensayo",15000,
            [new("p1","Aperitivos",[new("frio","Preparación fría","cold"),new("caliente","Preparación caliente","hot")]),
             new("p2","Segundo pase",[new("principal","Preparación principal","hot")])]));
            foreach(var product in new ProductDefinition[] {
                new("water","Agua mineral","Botella",400,true),new("wine-glass","Vino de ensayo","Copa",950,true),
                new("wine-bottle","Vino de ensayo","Botella",4200,true)})
                await unit.SeedConfiguration("product",product.Id,product);
            return new { initialized=true };
        });
    }
}
