using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Costina.Client;

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, string Role, string Username);
public sealed record HealthProbe(string Status, string Mode, string Version, bool Demo);
// Corte B (B1): por que no se puede hablar con el servidor al arrancar. Unreachable = esperar y reintentar (arranque en frio,
// red); Certificate = NO reintentar ni sortear (la validacion TLS sigue estricta: hay que instalar la CA o revisar la direccion);
// Other = respuesta que no es del motor (otro servicio en ese puerto, proxy).
public enum StartupProblem { Unreachable, Certificate, Other }
public sealed record PairingClaim(string PairingId, string PollSecret);
public sealed record PairingCollect(string Status, string? DeviceId, string? DeviceToken, string? Role, string? Station);

// Autenticacion previa a tener credencial (login y emparejamiento). Sin reglas de negocio,
// sin almacenamiento: los tokens los guarda quien llama (y el de sesion, solo en memoria).
public sealed class AuthClient : IDisposable
{
    private readonly HttpClient http;

    public AuthClient(Uri endpoint, HttpMessageHandler? handler = null)
    {
        ApiClient.ValidateEndpoint(endpoint);
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = new Uri(endpoint, "/api/native/v1/auth/"), Timeout = TimeSpan.FromSeconds(10) };
    }

    private async Task<(int Status, JsonElement Body)> PostAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        { Content = new StringContent(JsonSerializer.Serialize(body, ApiClient.Json), Encoding.UTF8, "application/json") };
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        try { return ((int)response.StatusCode, JsonSerializer.Deserialize<JsonElement>(text, ApiClient.Json)); }
        catch (JsonException) { throw new ApiError((int)response.StatusCode, "respuesta_no_reconocida"); }
    }

    // GET /health sin credencial. Lanza HttpRequestException (red o TLS) o ApiError (respuesta que no es del motor).
    public async Task<HealthProbe> ProbeAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(http.BaseAddress!, "/health"));
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if ((int)response.StatusCode != 200) throw new ApiError((int)response.StatusCode, "health_unavailable");
        try
        {
            var body = JsonSerializer.Deserialize<JsonElement>(text, ApiClient.Json);
            return new(body.GetProperty("status").GetString() ?? "", body.GetProperty("mode").GetString() ?? "",
                body.GetProperty("version").GetString() ?? "", body.TryGetProperty("demo", out var demo) && demo.ValueKind == JsonValueKind.True);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { throw new ApiError(200, "respuesta_no_reconocida"); }
    }

    // Pura (los checks la ejercitan): clasifica el fallo de un sondeo.
    public static StartupProblem Classify(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is System.Security.Authentication.AuthenticationException) return StartupProblem.Certificate;
        return error is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException ? StartupProblem.Unreachable : StartupProblem.Other;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("Introduce usuario y contraseña.");
        var (status, body) = await PostAsync("login", new { username = username.Trim(), password }, ct);
        if (status == 401) throw new ApiError(401, "invalid_credentials");
        if (status != 200) throw new ApiError(status, "login_failed");
        return new(body.GetProperty("token").GetString()!,
            body.GetProperty("expiresAt").GetDateTimeOffset(),
            body.GetProperty("role").GetString()!, body.GetProperty("username").GetString()!);
    }

    public async Task LogoutAsync(string token, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "logout") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Add("Authorization", "Bearer " + token);
        try { using var _ = await http.SendAsync(request, ct); } catch (HttpRequestException) { } // mejor esfuerzo
    }

    public async Task<PairingClaim> ClaimAsync(string code, string deviceName, CancellationToken ct = default)
    {
        var (status, body) = await PostAsync("pairings/claim", new { code = code.Trim(), deviceName = deviceName.Trim() }, ct);
        if (status != 200) throw new ApiError(status, "invalid_code");
        return new(body.GetProperty("pairingId").GetString()!, body.GetProperty("pollSecret").GetString()!);
    }

    // pending / approved / denied. El secreto del dispositivo llega UNA sola vez con "approved".
    public async Task<PairingCollect> CollectAsync(string pairingId, string pollSecret, CancellationToken ct = default)
    {
        var (status, body) = await PostAsync($"pairings/{Uri.EscapeDataString(pairingId)}/collect", new { pollSecret }, ct);
        var state = body.TryGetProperty("status", out var value) ? value.GetString() ?? "unknown" : "unknown";
        if (status == 200 && state == "approved")
            return new("approved", body.GetProperty("deviceId").GetString(), body.GetProperty("deviceToken").GetString(),
                body.GetProperty("role").GetString(), body.GetProperty("station").GetString());
        if (state is "pending" or "denied") return new(state, null, null, null, null);
        throw new ApiError(status, "unknown_pairing");
    }

    public void Dispose() => http.Dispose();
}
