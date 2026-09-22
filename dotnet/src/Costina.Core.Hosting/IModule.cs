using Costina.Core.Domain;
using Costina.Core.Persistence;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace Costina.Core.Hosting;

// ADR-012: contrato de MODULO. El motor compone al arrancar lo que cada modulo declara; un modulo referencia el
// nucleo y nunca al reves. Un modulo inactivo no registra rutas ni superficies aunque su codigo viaje en el binario.
public interface IModule
{
    // Nombre estable: prefijo de rutas (/api/native/v1/<name>/...), clave en session.modules y nombre del esquema (E1b).
    string Name { get; }
    // Affordances de sesion: acciones globales (no ligadas a un agregado) que este rol puede iniciar.
    IReadOnlyList<string> SessionActions(string role);
    // Claves que el modulo anade a GET /configuration (lectura operativa para los clientes).
    Task<IReadOnlyDictionary<string, object>> ConfigurationAsync(ModuleHost host, CancellationToken ct);
    // Fixtures ficticios del modulo (init-lab / load-demo), dentro del mismo comando idempotente que los del nucleo.
    Task SeedDemoAsync(Unit unit);
    // Rutas del modulo. host.Prefix es el suyo; host.LegacyPrefix permite alias de compatibilidad durante una version.
    void MapRoutes(IEndpointRouteBuilder app, ModuleHost host);
}

public sealed record ModuleHost(PostgresStore Store, NpgsqlDataSource Source, BusinessScope Scope, string Prefix, string LegacyPrefix);

public static class ModuleRegistry
{
    // Modulos activos = los compilados, acotados por COSTINA_MODULES (lista separada por comas) si esta definido.
    // E1b lo lleva a core.installation.modules (editable desde Configuracion); la licencia, cuando exista, solo acota.
    public static IReadOnlyList<IModule> Activate(IEnumerable<IModule> available, string? enabled)
    {
        var all = available.ToList();
        var names = all.Select(m => m.Name).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count) throw new InvalidOperationException("Duplicate module name.");
        if (string.IsNullOrWhiteSpace(enabled)) return all;
        var wanted = enabled.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var unknown = wanted.Except(names, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) throw new InvalidOperationException("Unknown module in COSTINA_MODULES: " + string.Join(", ", unknown));
        return all.Where(m => wanted.Contains(m.Name, StringComparer.Ordinal)).ToList();
    }
}
