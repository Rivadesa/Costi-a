using System.Text.RegularExpressions;
using Npgsql;

namespace Costina.Core.Persistence;

public sealed record TableDigest(long Rows, string Md5);

// D5.4 (#27): huella del CONTENIDO de cada tabla de los esquemas indicados, independiente del orden fisico. Se
// calcula dentro de la misma instantanea que usa pg_dump al copiar, y otra vez sobre la base restaurada: si
// coinciden, la restauracion tiene los mismos registros (no solo "un fichero").
// E1b: varios esquemas (core y uno por modulo); cada clave del resultado es "esquema.tabla". Con qualify=false las
// claves son solo "tabla": es la forma del manifiesto v1 (un unico esquema native_d1), que sigue siendo restaurable.
public static partial class BackupDigest
{
    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")] private static partial Regex Identifier();

    public static async Task<SortedDictionary<string, TableDigest>> ComputeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        IReadOnlyList<string> schemas, bool qualify = true, CancellationToken ct = default)
    {
        // Texto determinista: misma zona y estilo de fecha al copiar y al verificar.
        await using (var settings = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'; SET LOCAL datestyle = 'ISO, MDY'", connection, transaction))
            await settings.ExecuteNonQueryAsync(ct);
        var tables = new List<(string Schema, string Table)>();
        await using (var list = new NpgsqlCommand(
            "SELECT schemaname, tablename FROM pg_tables WHERE schemaname = ANY(@schemas) ORDER BY schemaname, tablename", connection, transaction))
        {
            list.Parameters.AddWithValue("schemas", schemas.ToArray());
            await using var reader = await list.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        var result = new SortedDictionary<string, TableDigest>(StringComparer.Ordinal);
        foreach (var (schema, table) in tables)
        {
            // Los nombres salen del catalogo de nuestros propios esquemas; aun asi solo se aceptan identificadores simples.
            if (!Identifier().IsMatch(schema) || !Identifier().IsMatch(table)) throw new InvalidDataException("Unexpected table name in schema " + schema + ".");
            await using var digest = new NpgsqlCommand(
                $"SELECT count(*), coalesce(md5(string_agg(h, '' ORDER BY h)), '') FROM (SELECT md5(t::text) AS h FROM {schema}.{table} t) s",
                connection, transaction);
            await using var row = await digest.ExecuteReaderAsync(ct);
            await row.ReadAsync(ct);
            result[qualify ? schema + "." + table : table] = new TableDigest(row.GetInt64(0), row.GetString(1));
        }
        return result;
    }
}
