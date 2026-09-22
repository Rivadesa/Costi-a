using System.Security.Cryptography;
using System.Text;
using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// Identidad relacional (D4.1, #26/F06). Solo se guardan hashes: PBKDF2 de la contrasena y
// SHA-256 del token de sesion. Nada de esto entra en payloads JSONB ni en logs.
public static class PasswordHashing
{
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed record AuthenticatedSession(string SessionId, string Username, string Role);

public sealed class IdentityStore(NpgsqlDataSource dataSource, BusinessScope scope)
{
    // Vida de sesion: corta con renovacion deslizante en cada peticion, y un tope absoluto
    // que ninguna renovacion supera (un turno largo de restaurante, no un dia entero).
    public static readonly TimeSpan SlidingLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(14);

    private static string TokenHash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private NpgsqlCommand Command(string sql)
    {
        var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        return command;
    }
    private const string ScopeWhere = "tenant=@tenant AND company=@company AND location=@location";

    public async Task<string> CreateUserAsync(string username, string password, string role, CancellationToken ct = default)
    {
        username = username.Trim().ToLowerInvariant();
        if (username.Length < 3 || username.Any(char.IsWhiteSpace))
            throw new ArgumentException("Username must have at least 3 characters and no spaces.");
        if (password.Length < 12) throw new ArgumentException("Password must have at least 12 characters.");
        if (role is not ("main" or "service" or "kitchen")) throw new ArgumentException("Unknown role.");
        var id = Guid.NewGuid().ToString("N");
        await using var command = Command(
            "INSERT INTO native_d1.users (tenant,company,location,id,username,password_hash,role) " +
            "VALUES (@tenant,@company,@location,@id,@username,@hash,@role)");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("hash", PasswordHashing.Hash(password));
        command.Parameters.AddWithValue("role", role);
        await command.ExecuteNonQueryAsync(ct);
        return id;
    }

    // Devuelve el TOKEN en claro una unica vez (viaja al cliente); en la base solo queda su hash.
    public async Task<(string Token, DateTimeOffset ExpiresAt, string Role, string Username)?> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {
        username = username.Trim().ToLowerInvariant();
        await using var find = Command(
            "SELECT id, password_hash, role FROM native_d1.users WHERE " + ScopeWhere + " AND username=@username AND active");
        find.Parameters.AddWithValue("username", username);
        string? userId = null, storedHash = null, role = null;
        await using (var reader = await find.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct)) { userId = reader.GetString(0); storedHash = reader.GetString(1); role = reader.GetString(2); }
        // Coste constante tambien con usuario inexistente: se verifica contra un hash senuelo.
        var candidate = storedHash ?? PasswordHashing.Hash("decoy-password-never-matches");
        if (!PasswordHashing.Verify(password, candidate) || userId is null) return null;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var now = DateTimeOffset.UtcNow;
        var expires = now + SlidingLifetime;
        await using var insert = Command(
            "INSERT INTO native_d1.sessions (tenant,company,location,id,user_id,token_hash,expires_at,absolute_expires_at) " +
            "VALUES (@tenant,@company,@location,@id,@user,@hash,@expires,@absolute)");
        insert.Parameters.AddWithValue("id", Guid.NewGuid().ToString("N"));
        insert.Parameters.AddWithValue("user", userId);
        insert.Parameters.AddWithValue("hash", TokenHash(token));
        insert.Parameters.AddWithValue("expires", expires);
        insert.Parameters.AddWithValue("absolute", now + AbsoluteLifetime);
        await insert.ExecuteNonQueryAsync(ct);
        return (token, expires, role!, username);
    }

    // Autentica y renueva en una sola sentencia: extiende la caducidad deslizante sin superar
    // nunca el tope absoluto. Token revocado, caducado o de usuario inactivo => null.
    public async Task<AuthenticatedSession?> AuthenticateAsync(string token, CancellationToken ct = default)
    {
        await using var command = Command(
            "UPDATE native_d1.sessions s SET expires_at = LEAST(now() + @sliding, s.absolute_expires_at) " +
            "FROM native_d1.users u WHERE s.tenant=@tenant AND s.company=@company AND s.location=@location" +
            " AND s.token_hash=@hash AND s.revoked_at IS NULL AND now() < s.expires_at AND now() < s.absolute_expires_at" +
            " AND u.tenant=s.tenant AND u.company=s.company AND u.location=s.location AND u.id=s.user_id AND u.active" +
            " RETURNING s.id, u.username, u.role");
        command.Parameters.AddWithValue("sliding", SlidingLifetime);
        command.Parameters.AddWithValue("hash", TokenHash(token));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var command = Command(
            "UPDATE native_d1.sessions SET revoked_at = now() WHERE " + ScopeWhere + " AND id=@id AND revoked_at IS NULL");
        command.Parameters.AddWithValue("id", sessionId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> AnyUserAsync(CancellationToken ct = default)
    {
        await using var command = Command("SELECT count(*) FROM native_d1.users WHERE " + ScopeWhere);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct)) > 0;
    }
}
