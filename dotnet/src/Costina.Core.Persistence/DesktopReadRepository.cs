using Costina.Core.Domain;
using Npgsql;
namespace Costina.Core.Persistence;

public sealed record AccountListing(string ServiceId,string TableId,string State);

// Read models only, scoped by the authenticated server. No SQL identifiers come from clients.
public sealed class DesktopReadRepository(NpgsqlDataSource source)
{
    // Configuracion JSONB de un modulo (<modulo>.configuration). El catalogo del nucleo es relacional (CatalogReads, E3).
    public Task<List<T>> ModuleConfiguration<T>(BusinessScope scope,string module,string kind,CancellationToken ct) => Read<T>(PostgresStore.CheckedSchema(module),scope,kind,ct);
    private async Task<List<T>> Read<T>(string schema,BusinessScope scope,string kind,CancellationToken ct)
    {
        await using var command=source.CreateCommand($"SELECT payload::text FROM {schema}.configuration WHERE tenant=@tenant AND company=@company AND location=@location AND kind=@kind ORDER BY id LIMIT 200");
        Bind(command,scope); command.Parameters.AddWithValue("kind",kind);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var result=new List<T>();
        while(await reader.ReadAsync(ct)) result.Add(Wire.Decode<T>(reader.GetString(0)));
        return result;
    }
    // E1b: la cuenta guarda la mesa en la que se abrio; el nucleo no lee ninguna tabla de modulo (ADR-012).
    public async Task<List<AccountListing>> OpenAccounts(BusinessScope scope,CancellationToken ct)
    {
        await using var command=source.CreateCommand("SELECT service_id,table_id,state FROM core.accounts WHERE tenant=@tenant AND company=@company AND location=@location AND state='Open' ORDER BY service_id LIMIT 200");
        Bind(command,scope); await using var reader=await command.ExecuteReaderAsync(ct);
        var result=new List<AccountListing>();
        while(await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2)));
        return result;
    }
    // Consulta de solo lectura acotada al ambito (E2: organizacion). El SQL es constante; los valores van como parametros.
    public async Task<List<T>> Query<T>(BusinessScope scope,string sql,Func<NpgsqlDataReader,T> project,CancellationToken ct,params (string Name,object Value)[] values)
    {
        await using var command=source.CreateCommand(sql);
        Bind(command,scope); foreach(var (name,value) in values) command.Parameters.AddWithValue(name,value);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var result=new List<T>();
        while(await reader.ReadAsync(ct)) result.Add(project(reader));
        return result;
    }
    private static void Bind(NpgsqlCommand command,BusinessScope scope)
    {
        command.Parameters.AddWithValue("tenant",scope.TenantId);
        command.Parameters.AddWithValue("company",scope.CompanyId);
        command.Parameters.AddWithValue("location",scope.LocationId);
    }
}
