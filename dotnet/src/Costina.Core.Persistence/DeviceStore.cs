using System.Security.Cryptography;
using System.Text;
using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// Emparejamiento y dispositivos (D4.2, #26/F06). Codigo de UN solo uso con caducidad corta,
// aprobado por un administrador con rol y estacion. El secreto del dispositivo viaja una unica
// vez; en la base solo quedan hashes (codigo, secreto de recogida y secreto del dispositivo).
public sealed record PairingIssued(string PairingId, string Code, DateTimeOffset ExpiresAt);
public sealed record PairingPending(string PairingId, string DeviceName, DateTimeOffset ClaimedAt);
public sealed record DeviceCredentials(string DeviceId, string Secret, string Role, string Station);
public sealed record AuthenticatedDevice(string DeviceId, string Name, string Role, string Station);
public sealed record DeviceRow(string Id, string Name, string Role, string Station,
    string ApprovedBy, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

public sealed class DeviceStore(NpgsqlDataSource dataSource, BusinessScope scope)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private const string ScopeWhere = "tenant=@tenant AND company=@company AND location=@location";

    private static string HashOf(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private NpgsqlCommand Command(string sql)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        return command;
    }

    public async Task<PairingIssued> CreatePairingAsync(string createdBy, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var code = RandomToken();
        var expires = DateTimeOffset.UtcNow + CodeLifetime;
        await using var command = Command(
            "INSERT INTO core.pairings (tenant,company,location,id,code_hash,created_by,expires_at) " +
            "VALUES (@tenant,@company,@location,@id,@hash,@by,@expires)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("hash", HashOf(code));
        command.Parameters.AddWithValue("by", createdBy);
        command.Parameters.AddWithValue("expires", expires);
        await command.ExecuteNonQueryAsync(ct);
        return new(id, code, expires);
    }

    // Un solo uso por transicion de estado: el segundo claim del mismo codigo no encuentra 'issued'.
    public async Task<(string PairingId, string PollSecret)?> ClaimAsync(string code, string deviceName, CancellationToken ct = default)
    {
        deviceName = deviceName.Trim();
        if (deviceName.Length is < 3 or > 60) return null;
        var pollSecret = RandomToken();
        await using var command = Command(
            "UPDATE core.pairings SET status='claimed', device_name=@name, poll_secret_hash=@poll, claimed_at=now() " +
            "WHERE " + ScopeWhere + " AND code_hash=@hash AND status='issued' AND now() < expires_at RETURNING id");
        command.Parameters.AddWithValue("name", deviceName);
        command.Parameters.AddWithValue("poll", HashOf(pollSecret));
        command.Parameters.AddWithValue("hash", HashOf(code));
        var id = await command.ExecuteScalarAsync(ct) as string;
        return id is null ? null : (id, pollSecret);
    }

    public async Task<IReadOnlyList<PairingPending>> PendingAsync(CancellationToken ct = default)
    {
        await using var command = Command(
            "SELECT id, device_name, claimed_at FROM core.pairings WHERE " + ScopeWhere +
            " AND status='claimed' AND now() < claimed_at + interval '30 minutes' ORDER BY claimed_at");
        var result = new List<PairingPending>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2)));
        return result;
    }

    public async Task<bool> DecideAsync(string pairingId, bool approve, string? role, string? station,
        string decidedBy, CancellationToken ct = default)
    {
        if (approve)
        {
            if (role is not ("main" or "service" or "kitchen")) throw new ArgumentException("Unknown role.");
            station = (station ?? "").Trim();
            if (station.Length is < 2 or > 40) throw new ArgumentException("Station must have 2-40 characters.");
        }
        await using var command = Command(
            "UPDATE core.pairings SET status=@status, approved_role=@role, approved_station=@station, decided_by=@by " +
            "WHERE " + ScopeWhere + " AND id=@id AND status='claimed' RETURNING id");
        command.Parameters.AddWithValue("status", approve ? "approved" : "denied");
        command.Parameters.AddWithValue("role", (object?)role ?? DBNull.Value);
        command.Parameters.AddWithValue("station", (object?)station ?? DBNull.Value);
        command.Parameters.AddWithValue("by", decidedBy);
        command.Parameters.AddWithValue("id", pairingId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    // Entrega el secreto UNA sola vez (transicion approved->consumed) y crea el dispositivo.
    // Devuelve tambien el estado para que el dispositivo distinga pendiente/denegado sin filtrar mas.
    public async Task<(string Status, DeviceCredentials? Credentials)> CollectAsync(
        string pairingId, string pollSecret, CancellationToken ct = default)
    {
        await using (var consume = Command(
            "UPDATE core.pairings SET status='consumed' WHERE " + ScopeWhere +
            " AND id=@id AND poll_secret_hash=@poll AND status='approved' RETURNING device_name, approved_role, approved_station, decided_by"))
        {
            consume.Parameters.AddWithValue("id", pairingId);
            consume.Parameters.AddWithValue("poll", HashOf(pollSecret));
            await using var reader = await consume.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0); var role = reader.GetString(1);
                var station = reader.GetString(2); var approvedBy = reader.GetString(3);
                await reader.DisposeAsync();
                var deviceId = Guid.NewGuid().ToString("N");
                var secret = RandomToken();
                await using var insert = Command(
                    "INSERT INTO core.devices (tenant,company,location,id,name,secret_hash,role,station,approved_by) " +
                    "VALUES (@tenant,@company,@location,@id,@name,@hash,@role,@station,@by)");
                insert.Parameters.AddWithValue("id", deviceId);
                insert.Parameters.AddWithValue("name", name);
                insert.Parameters.AddWithValue("hash", HashOf(secret));
                insert.Parameters.AddWithValue("role", role);
                insert.Parameters.AddWithValue("station", station);
                insert.Parameters.AddWithValue("by", approvedBy);
                await insert.ExecuteNonQueryAsync(ct);
                return ("approved", new DeviceCredentials(deviceId, secret, role, station));
            }
        }
        await using var peek = Command(
            "SELECT status FROM core.pairings WHERE " + ScopeWhere + " AND id=@id AND poll_secret_hash=@poll");
        peek.Parameters.AddWithValue("id", pairingId);
        peek.Parameters.AddWithValue("poll", HashOf(pollSecret));
        var status = await peek.ExecuteScalarAsync(ct) as string;
        return (status ?? "unknown", null);
    }

    public async Task<AuthenticatedDevice?> AuthenticateAsync(string deviceId, string secret, CancellationToken ct = default)
    {
        await using var command = Command(
            "SELECT name, role, station FROM core.devices WHERE " + ScopeWhere +
            " AND id=@id AND secret_hash=@hash AND revoked_at IS NULL");
        command.Parameters.AddWithValue("id", deviceId);
        command.Parameters.AddWithValue("hash", HashOf(secret));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(deviceId, reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task<IReadOnlyList<DeviceRow>> DevicesAsync(CancellationToken ct = default)
    {
        await using var command = Command(
            "SELECT id, name, role, station, approved_by, created_at, revoked_at FROM core.devices WHERE " +
            ScopeWhere + " ORDER BY created_at");
        var result = new List<DeviceRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        return result;
    }

    public async Task<bool> RevokeAsync(string deviceId, string revokedBy, CancellationToken ct = default)
    {
        await using var command = Command(
            "UPDATE core.devices SET revoked_at=now(), revoked_by=@by WHERE " + ScopeWhere +
            " AND id=@id AND revoked_at IS NULL RETURNING id");
        command.Parameters.AddWithValue("by", revokedBy);
        command.Parameters.AddWithValue("id", deviceId);
        return await command.ExecuteScalarAsync(ct) is not null;
    }
}
