using System.Globalization;
using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E4e (plan docs/plans/2026-10-26-e4e-alergias-antes-de-enviar.md, seccion 8): NUNCA BAJAR DE VERSION. C3b y E4e no tienen vuelta
// atras (el decodificador estricto de un motor anterior no lee lo que guarda uno nuevo, y el esquema sigue siendo v8, asi que la
// version del esquema no lo detecta). Unos binarios MAS ANTIGUOS que el motor que ya sirvio estos datos no los tocan: el arranque
// (el servicio), backup, upgrade y las ordenes de consola se niegan antes de escribir nada, y restore rechaza una copia de datos asi.
// La senal vive en los propios datos: cada motor en modo instalacion deja al arrancar UNA fila idempotente en core.commands (actor
// "engine", clave "served-by:<version>"), como la marca de demo de D5.6: viaja con las copias y no necesita esquema. No es una orden
// de dominio: no lleva evento, ni auditoria, ni aviso; nadie la puede pedir por GET /commands (ningun actor de sesion se llama asi).
// Solo cuenta la parte numerica (mayor.menor.parche: 0.44.0 de 0.44.0-e4e), comparada como numeros; una version ilegible cuenta como
// MAS NUEVA (falla cerrado) y se muestra tal cual.
public static class EngineVersions
{
    public const string Actor = "engine";
    public const string KeyPrefix = "served-by:";

    public static Version? Numeric(string? version)
    {
        if (string.IsNullOrEmpty(version)) return null;
        var end = version.IndexOfAny(['-', '+']);
        var parts = (end < 0 ? version : version[..end]).Split('.');
        if (parts.Length != 3) return null;
        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
        return new Version(numbers[0], numbers[1], numbers[2]);
    }

    // Verdadero si "candidate" es mas nueva que estos binarios ("own"), o si alguna de las dos no se puede leer.
    public static bool Newer(string candidate, string own)
        => Numeric(candidate) is not { } other || Numeric(own) is not { } mine || other > mine;

    // La mas nueva de una lista (la primera ilegible, si la hay); null si la lista esta vacia.
    public static string? Newest(IEnumerable<string> versions)
    {
        string? newest = null;
        foreach (var version in versions)
        {
            if (Numeric(version) is null) return version;
            if (newest is null || Numeric(version) > Numeric(newest)) newest = version;
        }
        return newest;
    }

    // Consola: castellano sin tildes (la salida del instalador no las muestra bien) y despues ingles, como Installation.cs.
    public static string Refusal(string newest, string own) =>
        $"Estos datos ya los sirvio Costina {newest}, mas nuevo que estos binarios ({own}): no se ha cambiado nada. Instala Costina {newest} o una version mas nueva; para volver a una version anterior hay que restaurar, con los binarios anteriores y en una instalacion limpia, la copia hecha antes de actualizar."
        + Environment.NewLine + $"This data was already served by Costina {newest}, newer than these binaries ({own}): nothing was changed. Install Costina {newest} or newer; going back to an older version means restoring, with the older binaries and on a clean installation, the backup taken before the update.";

    public static string RestoreRefusal(string newest, string own) =>
        $"Esta copia es de datos que ya sirvio Costina {newest}, mas nuevo que estos binarios ({own}): no se ha restaurado nada. Restaurala con Costina {newest} o una version mas nueva."
        + Environment.NewLine + $"This backup holds data already served by Costina {newest}, newer than these binaries ({own}): nothing was restored. Restore it with Costina {newest} or newer.";

    // Versiones de los motores que han servido los datos de este ambito, en el orden en que llegaron. Vacia si solo los sirvieron
    // motores anteriores a la marca. Una base v1 (antes de E1b) que aun no se ha actualizado guarda sus ordenes en native_d1.commands.
    public static async Task<IReadOnlyList<string>> ServedByAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, BusinessScope scope, CancellationToken ct = default)
    {
        string? table;
        await using (var probe = new NpgsqlCommand("SELECT CASE WHEN to_regclass('core.commands') IS NOT NULL THEN 'core.commands' WHEN to_regclass('native_d1.commands') IS NOT NULL THEN 'native_d1.commands' END", connection, transaction))
            table = await probe.ExecuteScalarAsync(ct) as string;
        if (table is not ("core.commands" or "native_d1.commands")) return [];
        await using var command = new NpgsqlCommand(
            $"SELECT fingerprint FROM {table} WHERE tenant=@tenant AND company=@company AND location=@location AND actor=@actor AND starts_with(key, @prefix) ORDER BY created_at, key",
            connection, transaction);
        command.Parameters.AddWithValue("tenant", scope.TenantId); command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        command.Parameters.AddWithValue("actor", Actor); command.Parameters.AddWithValue("prefix", KeyPrefix);
        var versions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) versions.Add(reader.GetString(0));
        return versions;
    }
}
