using Costina.Core.Domain;
using Costina.Core.Persistence;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace Costina.Core.Hosting;

// ADR-012: contrato de MODULO. El motor compone al arrancar lo que cada modulo declara; un modulo referencia el
// nucleo y nunca al reves. Un modulo inactivo no registra rutas ni superficies aunque su codigo viaje en el binario.
public interface IModule
{
    // Nombre estable: prefijo de rutas (/api/native/v1/<name>/...), clave en session.modules y nombre de su esquema PostgreSQL.
    string Name { get; }
    // E1b: su esquema (schema.sql) y sus concesiones (grants.sql). El nucleo los aplica en init/upgrade/restore para TODOS
    // los modulos compilados, activos o no: desactivar un modulo nunca hace desaparecer sus datos ni los deja fuera de la copia.
    ModuleSchema Schema();
    // Affordances de sesion: acciones globales (no ligadas a un agregado) que este rol puede iniciar.
    IReadOnlyList<string> SessionActions(string role);
    // Claves que el modulo anade a GET /configuration (lectura operativa para los clientes).
    Task<IReadOnlyDictionary<string, object>> ConfigurationAsync(ModuleHost host, CancellationToken ct);
    // Fixtures ficticios del modulo (init-lab / load-demo), dentro del mismo comando idempotente que los del nucleo.
    Task SeedDemoAsync(Unit unit);
    // E2: true si el modulo tiene algo vivo sobre esa mesa (el nucleo no desactiva una mesa en uso). Nunca lee tablas ajenas.
    Task<bool> TableInUseAsync(Unit unit, string tableId);
    // Rutas del modulo. host.Prefix es el suyo; host.LegacyPrefix permite alias de compatibilidad durante una version.
    void MapRoutes(IEndpointRouteBuilder app, ModuleHost host);
}

public sealed record ModuleHost(PostgresStore Store, NpgsqlDataSource Source, BusinessScope Scope, string Prefix, string LegacyPrefix);

public static class ModuleRegistry
{
    // Modulos activos = los compilados, acotados por la lista guardada en la instalacion (core.installation.modules;
    // NULL = todos) y, solo en laboratorio/CI, por COSTINA_MODULES. La licencia, cuando exista, solo acota.
    public static IReadOnlyList<IModule> Activate(IEnumerable<IModule> available, IReadOnlyList<string>? stored, string? environment)
    {
        var all = available.ToList();
        var names = all.Select(m => m.Name).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count) throw new InvalidOperationException("Duplicate module name.");
        var active = all;
        if (stored is not null) active = Narrow(active, stored, "the installation's module list");
        if (!string.IsNullOrWhiteSpace(environment))
            active = Narrow(active, environment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), "COSTINA_MODULES");
        return active;
    }

    private static List<IModule> Narrow(List<IModule> modules, IReadOnlyList<string> wanted, string source)
    {
        var unknown = wanted.Where(w => modules.All(m => m.Name != w)).ToArray();
        if (unknown.Length > 0) throw new InvalidOperationException($"Unknown module in {source}: " + string.Join(", ", unknown));
        return modules.Where(m => wanted.Contains(m.Name, StringComparer.Ordinal)).ToList();
    }
}
