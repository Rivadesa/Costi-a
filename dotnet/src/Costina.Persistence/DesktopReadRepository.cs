using Costina.Domain;
using Npgsql;
namespace Costina.Persistence;

public sealed record AccountListing(string ServiceId,string TableId,string State);

// Read models only, scoped by the authenticated server. No SQL identifiers come from clients.
public sealed class DesktopReadRepository(NpgsqlDataSource source)
{
    public async Task<List<T>> Configuration<T>(BusinessScope scope,string kind,CancellationToken ct)
    {
        await using var command=source.CreateCommand("SELECT payload::text FROM native_d1.configuration WHERE tenant=@tenant AND company=@company AND location=@location AND kind=@kind ORDER BY id LIMIT 200");
        Bind(command,scope); command.Parameters.AddWithValue("kind",kind);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var result=new List<T>();
        while(await reader.ReadAsync(ct)) result.Add(Wire.Decode<T>(reader.GetString(0)));
        return result;
    }
    public async Task<List<AccountListing>> OpenAccounts(BusinessScope scope,CancellationToken ct)
    {
        await using var command=source.CreateCommand("SELECT a.service_id,s.table_id,a.state FROM native_d1.accounts a JOIN native_d1.services s ON s.tenant=a.tenant AND s.company=a.company AND s.location=a.location AND s.id=a.service_id WHERE a.tenant=@tenant AND a.company=@company AND a.location=@location AND a.state='Open' ORDER BY a.service_id LIMIT 200");
        Bind(command,scope); await using var reader=await command.ExecuteReaderAsync(ct);
        var result=new List<AccountListing>();
        while(await reader.ReadAsync(ct)) result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2)));
        return result;
    }
    private static void Bind(NpgsqlCommand command,BusinessScope scope)
    {
        command.Parameters.AddWithValue("tenant",scope.TenantId);
        command.Parameters.AddWithValue("company",scope.CompanyId);
        command.Parameters.AddWithValue("location",scope.LocationId);
    }
}
