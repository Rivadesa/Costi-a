using Costina.Core.Hosting;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static Costina.Core.Hosting.Requests;

namespace Costina.Modules.Dining;

// Modulo Dining (ADR-012): servicio gastronomico por pases, el construido para Costiña. Todo lo que aqui se anuncia
// pasa por la misma autenticacion, ambito, idempotencia, auditoria y outbox del nucleo.
public sealed class DiningModule : IModule
{
    public string Name => "dining";
    // E1b: esquema "dining" (services, occupancies, configuration de menus) y sus concesiones, embebidos en este ensamblado.
    public ModuleSchema Schema() => ModuleSchema.FromAssembly(typeof(DiningModule).Assembly, Name);

    // open: abrir mesa; add-consumption: consumo a mayores desde sala sin importes (D3.6); cocina no hace ninguna de las dos.
    public IReadOnlyList<string> SessionActions(string role) => new[] { "open", "add-consumption" }.Where(a => Affordances.Allows(role, a)).ToArray();

    // E4a: las ofertas vigentes las publica el nucleo en /configuration.offers; el modulo mantiene 'menus' (id, name) una
    // version como alias para clientes con cache.
    public async Task<IReadOnlyDictionary<string, object>> ConfigurationAsync(ModuleHost host, CancellationToken ct)
    {
        var offers = await new DesktopReadRepository(host.Source).AvailableOffers(host.Scope, DateTime.Now, ct);
        return new Dictionary<string, object> { ["menus"] = offers.Select(o => new { o.Id, o.Name }).ToArray() };
    }

    // E2: una mesa con ocupacion viva no se puede desactivar; el nucleo pregunta por este contrato.
    public async Task<bool> TableInUseAsync(Unit unit, string tableId)
        => (await unit.Rows("SELECT 1 FROM dining.occupancies WHERE " + Unit.ScopeWhere + " AND table_id=@table AND state='Occupied'", [("table", tableId)], r => 1)).Count > 0;

    // E4a: los menus de demostracion son OFERTAS del nucleo (LabConfiguration); el modulo ya no tiene fixtures propios.
    public Task SeedDemoAsync(Unit unit) => Task.CompletedTask;

    public void MapRoutes(IEndpointRouteBuilder app, ModuleHost host)
    {
        var (store, scope) = (host.Store, host.Scope);
        var everyone = new RouteAccess("main", "service", "kitchen");
        // Rutas propias del modulo y, durante una version, alias en las rutas anteriores (clientes con la PWA en cache).
        void Get(string path, Delegate handler, RouteAccess access)
        { foreach (var prefix in new[] { host.Prefix, host.LegacyPrefix }) app.MapGet(prefix + path, handler).WithMetadata(access); }
        void Post(string path, Delegate handler, RouteAccess access)
        { foreach (var prefix in new[] { host.Prefix, host.LegacyPrefix }) app.MapPost(prefix + path, handler).WithMetadata(access); }

        Get("/board", async (HttpContext c) =>
        {
            var rows = await store.ReadAsync(Identity(c, scope), u => u.Board(), c.RequestAborted);
            return Json(rows.Select(r => Affordances.Filter(r, Role(c), Station(c))).ToArray());
        }, everyone);
        Get("/services/{id}", async (HttpContext c, string id) => Json(await store.ReadAsync(Identity(c, scope), async u =>
        {
            var d = await u.Dining(id);
            return new Versioned<DiningView>(d.Version, Affordances.Filter(d.Entity.View(true), Role(c), Station(c)));
        }, c.RequestAborted)), everyone);
        Post("/services", (HttpContext c) => Write<OpenRequest>(c, store, scope, DiningOperations.Open), new RouteAccess("main", "service"));
        // La matriz rol x accion que el middleware aplica es la MISMA que filtra las affordances: nada se anuncia que de 403.
        Post("/services/{id}/commands/{action}", (HttpContext c, string id, string action) =>
            Write<DiningCommand>(c, store, scope, (u, i, r) => action == "add-consumption" ? DiningOperations.Consumption(u, i, id, r) : DiningOperations.Dining(u, i, id, action, r)),
            new RouteAccess("main", "service", "kitchen") { ActionPolicy = Affordances.Allows });
        Post("/occupancy/{id}/release", (HttpContext c, string id) =>
            Write<ReleaseCommand>(c, store, scope, (u, i, r) => DiningOperations.Release(u, i, id, r)), new RouteAccess("main"));
    }
}
