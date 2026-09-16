using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Costina.Client;

public sealed class ApiError(int status, string code) : Exception($"HTTP {status}: {code}")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record PendingCommand(string Key, string Path, string Body, string Description);

// Cola durable minima: exactamente UNA orden incierta, persistida fuera del proceso.
// La implementacion decide cifrado y ubicacion; esta biblioteca solo define el contrato.
// Nunca se persisten la clave de acceso ni credenciales: solo el comando y su Idempotency-Key.
public interface IPendingStore
{
    void Save(PendingCommand command);
    PendingCommand? Load();
    void Clear();
}

// One uncertain command at a time; with an attached store it survives process shutdown.
// No financial/domain rules or database driver are present in this library.
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly SemaphoreSlim mutation = new(1, 1);
    private IPendingStore? store;
    public PendingCommand? Pending { get; private set; }

    // Se llama tras autenticar (cuando ya se conoce el rol que identifica el fichero).
    // Si hay una orden persistida de una sesion anterior, queda restaurada y bloquea
    // nuevas mutaciones hasta reintentarla identica o recibir un rechazo explicito.
    public void AttachPendingStore(IPendingStore pendingStore)
    {
        store = pendingStore;
        if (Pending is not null) { store.Save(Pending); return; }
        Pending = store.Load();
    }
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    public ApiClient(Uri endpoint, string key, HttpMessageHandler? handler = null)
    {
        ValidateEndpoint(endpoint);
        if (key.Length < 32 || key.Any(char.IsWhiteSpace)) throw new ArgumentException("Clave de laboratorio incompleta.");
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = new Uri(endpoint, "/api/native/v1/"), Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }
    public static void ValidateEndpoint(Uri endpoint)
    {
        // Local-only until device identity and local TLS are implemented. No TLS validation bypass.
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1"
            || endpoint.Port < 1024 || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0
            || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/")
            throw new ArgumentException("Este ensayo solo admite http://127.0.0.1:PUERTO, sin rutas ni credenciales.");
    }
    public async Task<T> GetAsync<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SafePath(path));
        using var response = await http.SendAsync(request);
        await Check(response);
        var text = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new JsonException("Respuesta vacía.");
    }
    public async Task<JsonElement> SendAsync<T>(string path, T body, string description)
    {
        if (!await mutation.WaitAsync(0)) throw new InvalidOperationException("Ya hay una operación en curso.");
        try
        {
            if (Pending is not null) throw new InvalidOperationException("Resuelve primero la orden sin confirmar.");
            Pending = new(Guid.NewGuid().ToString("N"), SafePath(path), JsonSerializer.Serialize(body, Json), description);
            store?.Save(Pending); // durable ANTES del primer intento: un cierre forzado no la pierde
            return await Attempt();
        }
        finally { mutation.Release(); }
    }
    public async Task<JsonElement> RetryAsync()
    {
        if (!await mutation.WaitAsync(0)) throw new InvalidOperationException("Ya hay una operación en curso.");
        try
        {
            if (Pending is null) throw new InvalidOperationException("No hay ninguna orden pendiente.");
            return await Attempt();
        }
        finally { mutation.Release(); }
    }
    private async Task<JsonElement> Attempt()
    {
        var command = Pending!;
        using var request = new HttpRequestMessage(HttpMethod.Post, command.Path);
        request.Headers.Add("Idempotency-Key", command.Key);
        request.Content = new StringContent(command.Body, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        // Only explicit rejection statuses from the application resolve the uncertainty.
        // Authentication changes, redirects, gateways and malformed responses retain the exact command.
        if ((int)response.StatusCode is 403 or 404 or 409 or 422) { Pending = null; store?.Clear(); }
        await Check(response);
        var data = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);
        if (data.ValueKind != JsonValueKind.Object || (!data.TryGetProperty("version", out _) && !data.TryGetProperty("serviceId", out _)))
            throw new JsonException("Respuesta de comando no reconocida; conserva el mismo reintento.");
        Pending = null; store?.Clear();
        return data;
    }
    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string code = "request_failed";
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("error", out var error)) code = error.GetString() ?? code;
        }
        catch (JsonException) { }
        throw new ApiError((int)response.StatusCode, code);
    }
    private static string SafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal)
            || path.Contains(':') || path.Contains('?') || path.Contains('#') || path.Contains('\\'))
            throw new ArgumentException("Ruta de API no válida.");
        return path;
    }
    public static string Segment(string id) => Uri.EscapeDataString(id);
    public void Dispose() { http.Dispose(); mutation.Dispose(); }
}
