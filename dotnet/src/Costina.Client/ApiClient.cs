using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace Costina.Client;

public sealed class ApiError(int status, string code) : Exception($"HTTP {status}: {code}")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record PendingCommand(string Key, string Path, string Body, string Description);

// Resultado de leer el almacen durable. Ausente y restaurada son los casos normales; ilegible e
// inaccesible BLOQUEAN las mutaciones nuevas (fallo cerrado): la orden anterior pudo aplicarse y
// solo el servidor puede decirlo. Nunca se resuelve la duda destruyendo el fichero.
public enum PendingOutcome { Absent, Restored, Unreadable, Unavailable }
public sealed record PendingLoad(PendingOutcome Outcome, PendingCommand? Command, string? Key, string Detail);

// Cola durable minima: exactamente UNA orden incierta, persistida fuera del proceso.
// La implementacion decide cifrado, ubicacion y aislamiento entre ventanas; esta biblioteca
// solo define el contrato. Nunca se persisten la clave de acceso ni credenciales.
public interface IPendingStore
{
    void Save(PendingCommand command);
    PendingLoad Load();
    void Clear(string key);   // borra SOLO si lo guardado lleva esa clave, nunca un fichero generico
    void Discard();           // aparta a cuarentena lo que haya sin destruir la evidencia
}

// One uncertain command at a time; with an attached store it survives process shutdown.
// No financial/domain rules or database driver are present in this library.
public sealed class ApiClient : IDisposable
{
    // Codigos que NO cierran la incertidumbre: el servidor pudo aplicar (o aun aplicar) el comando.
    private static readonly string[] Inconclusive = ["storage_conflict", "idempotency_conflict", "operation_unconfirmed"];
    private readonly HttpClient http;
    private readonly SemaphoreSlim mutation = new(1, 1);
    private IPendingStore? store;
    public PendingCommand? Pending { get; private set; }
    public PendingLoad? Blocked { get; private set; }
    public string? LastReconciliation { get; private set; }

    // Se llama tras autenticar (cuando ya se conoce la identidad que aisla el almacen).
    // Una orden persistida de una sesion anterior queda restaurada y bloquea nuevas mutaciones
    // hasta reintentarla identica; un fichero ilegible o inaccesible bloquea hasta conciliar.
    public void AttachPendingStore(IPendingStore pendingStore)
    {
        store = pendingStore;
        if (Pending is not null) { store.Save(Pending); return; }
        var load = store.Load();
        if (load.Outcome == PendingOutcome.Restored)
            Pending = load.Command ?? throw new InvalidOperationException("Almacén de órdenes inconsistente.");
        else if (load.Outcome != PendingOutcome.Absent) Blocked = load;
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
    // POST autenticado directo para operaciones de IDENTIDAD (emparejamientos, revocaciones,
    // logout): sin Idempotency-Key ni orden incierta. Los comandos de dominio siguen en SendAsync.
    public async Task<JsonElement> PostRawAsync<T>(string path, T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SafePath(path))
        { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };
        using var response = await http.SendAsync(request);
        await Check(response);
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);
    }

    public async Task<JsonElement> SendAsync<T>(string path, T body, string description)
    {
        if (!await mutation.WaitAsync(0)) throw new InvalidOperationException("Ya hay una operación en curso.");
        try
        {
            if (Blocked is not null)
                throw new InvalidOperationException("Hay una orden anterior ilegible o inaccesible. Consúltala en el servidor o descártala conservando la evidencia antes de operar.");
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
    // Pregunta al servidor si la orden ilegible consta como aplicada. "confirmed" retira el bloqueo
    // (la evidencia va a cuarentena); "unknown" lo mantiene y solo entonces se permite descartar.
    public async Task<string> ReconcileAsync()
    {
        if (!await mutation.WaitAsync(0)) throw new InvalidOperationException("Ya hay una operación en curso.");
        try
        {
            var key = Blocked?.Key ?? throw new InvalidOperationException("La orden ilegible no conserva identificador: no se puede consultar.");
            var lookup = await GetAsync<CommandLookup>("commands/" + Segment(key));
            var outcome = lookup.Found ? "confirmed" : "unknown";
            if (lookup.Found) { store?.Discard(); Blocked = null; }
            LastReconciliation = outcome;
            return outcome;
        }
        finally { mutation.Release(); }
    }
    public void DiscardBlocked()
    {
        if (Blocked is null) return;
        if (Blocked.Outcome == PendingOutcome.Unavailable)
            throw new InvalidOperationException("La orden es inaccesible, no ilegible: no se descarta. Vuelve a conectar cuando el fichero esté disponible.");
        if (Blocked.Key is not null && LastReconciliation != "unknown")
            throw new InvalidOperationException("Consulta primero al servidor si la orden consta como aplicada.");
        store?.Discard(); Blocked = null;
    }
    private async Task<JsonElement> Attempt()
    {
        var command = Pending!;
        using var request = new HttpRequestMessage(HttpMethod.Post, command.Path);
        request.Headers.Add("Idempotency-Key", command.Key);
        request.Content = new StringContent(command.Body, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var status = (int)response.StatusCode;
        // Solo un rechazo reconocible de la aplicacion, con codigo definitivo y que identifica ESTE
        // comando por su clave, resuelve la incertidumbre. Autenticacion, redirecciones, pasarelas,
        // conflictos de almacenamiento y respuestas malformadas conservan el reintento exacto.
        if ((status is 403 or 404 or 409 or 422) && IsDefinitiveRejection(response, text, command.Key)) Resolve(command);
        if (!response.IsSuccessStatusCode) throw new ApiError(status, ErrorCode(text) ?? "request_failed");
        var data = JsonSerializer.Deserialize<JsonElement>(text, Json);
        if (data.ValueKind != JsonValueKind.Object || (!data.TryGetProperty("version", out _) && !data.TryGetProperty("serviceId", out _)))
            throw new JsonException("Respuesta de comando no reconocida; conserva el mismo reintento.");
        Resolve(command);
        return data;
    }
    private void Resolve(PendingCommand command) { Pending = null; store?.Clear(command.Key); }
    private static bool IsDefinitiveRejection(HttpResponseMessage response, string body, string key)
    {
        var code = ErrorCode(body);
        if (code is null || Inconclusive.Contains(code, StringComparer.Ordinal)) return false;
        return response.Headers.TryGetValues("Idempotency-Key", out var echoed) && echoed.Contains(key, StringComparer.Ordinal);
    }
    private static string? ErrorCode(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String ? error.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ApiError((int)response.StatusCode, ErrorCode(await response.Content.ReadAsStringAsync()) ?? "request_failed");
    }
    private static string SafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal)
            || path.Contains(':') || path.Contains('?') || path.Contains('#') || path.Contains('\\'))
            throw new ArgumentException("Ruta de API no válida.");
        return path;
    }
    public static string Segment(string id) => Uri.EscapeDataString(id);
    public void Dispose() { http.Dispose(); mutation.Dispose(); (store as IDisposable)?.Dispose(); }
}
