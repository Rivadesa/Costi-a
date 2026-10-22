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
public sealed record AuthenticatedDevice(string DeviceId, string Name, string Role, string Station, string? StationName = null);
public sealed record DeviceRow(string Id, string Name, string Role, string Station,
    string ApprovedBy, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

public sealed class DeviceStore(NpgsqlDataSource dataSource, BusinessScope scope)
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private const string ScopeWhere = "tenant=@tenant AND company=@company AND location=@location";

    private static string HashOf(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // C3c: codigo CORTO para teclear. En iPhone y iPad la camara abre el enlace del QR en Safari, nunca en el icono de la pantalla de
    // inicio, y el icono no ve lo que guarda Safari: se empareja desde el icono tecleando este codigo. 10 simbolos de un alfabeto sin
    // ambiguedades (sin 0/O ni 1/I): 32 simbolos, 50 bits. Mismo uso unico, mismos 5 minutos y mismo freno de intentos que el largo;
    // lo que de verdad protege es la aprobacion en el PC, con el nombre y la intencion a la vista.
    public const string ShortAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int ShortLength = 10;
    public const int IssueAttempts = 8;
    public static string ShortCode() => RandomNumberGenerator.GetString(ShortAlphabet, ShortLength);
    // Forma canonica de un codigo corto TECLEADO: sin espacios ni guiones y en mayusculas, y solo si quedan 10 simbolos del alfabeto
    // corto (si no, null). Solo letras y cifras ASCII: una letra de otro alfabeto que se parezca o que pase a mayuscula ASCII no vale.
    // Nunca se aplica al codigo LARGO, que es base64url y distingue mayusculas: ClaimAsync busca primero el codigo tal cual.
    public static string? ShortForm(string code)
    {
        if (code.Length > 64) return null;
        var compact = new System.Text.StringBuilder(ShortLength);
        foreach (var c in code)
        {
            if (c == '-' || char.IsWhiteSpace(c)) continue;
            if (!char.IsAsciiLetterOrDigit(c) || compact.Length == ShortLength) return null;
            compact.Append(char.ToUpperInvariant(c));
        }
        var form = compact.ToString();
        return form.Length == ShortLength && form.All(c => ShortAlphabet.Contains(c)) ? form : null;
    }
    // La huella del codigo es UNICA en toda la tabla, tambien entre las filas viejas (schema.sql, core.pairings): si una emision choca
    // con una huella existente se genera OTRO codigo, en vez de devolver un conflicto. Con 50 bits es rarisimo y con el largo,
    // imposible en la practica; el limite de intentos solo evita un bucle sin fin. Cualquier otro error de la base no se reintenta.
    public static async Task<PairingIssued> IssueFreshAsync(Func<string> generate, Func<string, Task<PairingIssued>> insert)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await insert(generate()); }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation && attempt < IssueAttempts) { }
        }
    }

    private NpgsqlCommand Command(string sql)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        return command;
    }

    // typed (C3c): el codigo corto para teclear; si no, el largo de siempre. La misma columna y la misma huella para los dos.
    public Task<PairingIssued> CreatePairingAsync(string createdBy, bool typed = false, CancellationToken ct = default)
        => IssueFreshAsync(typed ? ShortCode : RandomToken, code => InsertPairingAsync(code, createdBy, ct));

    private async Task<PairingIssued> InsertPairingAsync(string code, string createdBy, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
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
    // C3c: primero el codigo TAL CUAL (el largo distingue mayusculas y nunca se normaliza). Solo si no casa y lo recibido es un codigo
    // corto escrito de otra forma (minusculas, espacios, guiones), tambien su forma canonica. Cada intento toca como mucho una fila
    // (la huella es unica) y el freno de intentos de la ruta cuenta la peticion entera como un solo fallo.
    public async Task<(string PairingId, string PollSecret)?> ClaimAsync(string code, string deviceName, CancellationToken ct = default)
    {
        deviceName = deviceName.Trim();
        if (deviceName.Length is < 3 or > 60) return null;
        var claimed = await ClaimExactAsync(code, deviceName, ct);
        if (claimed is null && ShortForm(code) is { } typed && typed != code) claimed = await ClaimExactAsync(typed, deviceName, ct);
        return claimed;
    }

    private async Task<(string PairingId, string PollSecret)?> ClaimExactAsync(string code, string deviceName, CancellationToken ct)
    {
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
        // A4 (Inicio y dispositivos): el nombre de la estacion viaja en /session para la cabecera de la tablet. Una estacion
        // desactivada o renombrada se refleja en la siguiente peticion, sin tocar el dispositivo.
        await using var command = Command(
            "SELECT d.name, d.role, d.station, s.name FROM core.devices d LEFT JOIN core.stations s " +
            "ON s.tenant=d.tenant AND s.company=d.company AND s.location=d.location AND s.id=d.station " +
            "WHERE d.tenant=@tenant AND d.company=@company AND d.location=@location" +
            " AND d.id=@id AND d.secret_hash=@hash AND d.revoked_at IS NULL");
        command.Parameters.AddWithValue("id", deviceId);
        command.Parameters.AddWithValue("hash", HashOf(secret));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(deviceId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
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
