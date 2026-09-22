using System.Text.RegularExpressions;
using Npgsql;

namespace Costina.Core.Persistence;

public sealed record TableDigest(long Rows, string Md5);

// D5.4 (#27): huella del CONTENIDO de cada tabla del esquema, independiente del orden fisico. Se
// calcula dentro de la misma instantanea que usa pg_dump al copiar, y otra vez sobre la base
// restaurada: si coinciden, la restauracion tiene los mismos registros (no solo "un fichero").
public static partial class BackupDigest
{
    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")] private static partial Regex Identifier();

    public static async Task<SortedDictionary<string, TableDigest>> ComputeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        // Texto determinista: misma zona y estilo de fecha al copiar y al verificar.
        await using (var settings = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'; SET LOCAL datestyle = 'ISO, MDY'", connection, transaction))
            await settings.ExecuteNonQueryAsync(ct);
        var tables = new List<string>();
        await using (var list = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname='native_d1' ORDER BY tablename", connection, transaction))
        await using (var reader = await list.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
        var result = new SortedDictionary<string, TableDigest>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            // Los nombres salen del catalogo de nuestro propio esquema; aun asi solo se aceptan identificadores simples.
            if (!Identifier().IsMatch(table)) throw new InvalidDataException("Unexpected table name in schema native_d1.");
            await using var digest = new NpgsqlCommand(
                $"SELECT count(*), coalesce(md5(string_agg(h, '' ORDER BY h)), '') FROM (SELECT md5(t::text) AS h FROM native_d1.{table} t) s",
                connection, transaction);
            await using var row = await digest.ExecuteReaderAsync(ct);
            await row.ReadAsync(ct);
            result[table] = new TableDigest(row.GetInt64(0), row.GetString(1));
        }
        return result;
    }
}
