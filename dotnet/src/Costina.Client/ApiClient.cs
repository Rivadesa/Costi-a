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
        // D5.3: HTTP solo contra el propio equipo (loopback); en la red local UNICAMENTE https con la
        // validacion normal de la cadena (la raiz de la CA local se instala en el puesto). Nunca se
        // desactiva la validacion TLS ni se acepta http hacia otra maquina.
        if (!endpoint.IsAbsoluteUri) throw new ArgumentException("Dirección no válida.");
        var local = endpoint.Scheme == "http" && endpoint.Host == "127.0.0.1";
        var lan = endpoint.Scheme == "https" && endpoint.Host.Length > 0;
        if (!(local || lan) || endpoint.Port < 1024 || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0
            || endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/")
            throw new ArgumentException("Solo se admite http://127.0.0.1:PUERTO en el propio servidor o https://NOMBRE:PUERTO en la red local, sin rutas ni credenciales.");
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
        if ((status is 403 or 404 or 409 or 422) && IsDefinitiveRejection(response, text, command.Key))
        {
            // D6.6: el rechazo habla de ESTE intento. El servidor comprueba el permiso ANTES de mirar si la clave ya se
            // ejecuto, asi que un reintento puede recibir 403 de una orden que SI se aplico (puesto re-emparejado con
            // otro rol). Solo su registro lo dice: consta -> aplicada; no consta -> rechazada; no se sabe -> se conserva.
            var earlier = await LookupAsync(command.Key);
            if (earlier is { Found: true })
            {
                Resolve(command);
                return earlier.Response ?? JsonSerializer.Deserialize<JsonElement>("{}", Json);
            }
            if (earlier is not null) Resolve(command);
        }
        if (!response.IsSuccessStatusCode) throw new ApiError(status, ErrorCode(text) ?? "request_failed");
        var data = JsonSerializer.Deserialize<JsonElement>(text, Json);
        // Exito reconocible: un objeto que habla de ESTE comando, por el eco de su clave (todo el servidor la devuelve,
        // D3.4) o, para servidores anteriores, por la version o el identificador de servicio que devuelven los comandos de dominio.
        var echoed = response.Headers.TryGetValues("Idempotency-Key", out var keys) && keys.Contains(command.Key, StringComparer.Ordinal);
        if (data.ValueKind != JsonValueKind.Object || (!echoed && !data.TryGetProperty("version", out _) && !data.TryGetProperty("serviceId", out _)))
            throw new JsonException("Respuesta de comando no reconocida; conserva el mismo reintento.");
        Resolve(command);
        return data;
    }
    private void Resolve(PendingCommand command) { Pending = null; store?.Clear(command.Key); }
    // GET commands/{key}: solo el mismo actor; nunca ejecuta nada. null = no se pudo saber (red, 5xx, 401, cuerpo raro).
    private async Task<CommandLookup?> LookupAsync(string key)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "commands/" + Segment(key));
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var lookup = JsonSerializer.Deserialize<CommandLookup>(await response.Content.ReadAsStringAsync(), Json);
            return lookup is not null && string.Equals(lookup.Key, key, StringComparison.Ordinal) ? lookup : null;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException) { return null; }
    }
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
