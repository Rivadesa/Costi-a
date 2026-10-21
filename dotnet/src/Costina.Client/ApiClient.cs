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
// D6.7: el servidor guarda la idempotencia POR ACTOR. Una orden de OTRO actor con el mismo rol (otra persona responsable en este
// PC, o un dispositivo antiguo con rol principal) y una SIN actor anotado (fichero de 0.40.0 o anterior) tambien BLOQUEAN y nunca
// se reenvian: con otra identidad el servidor la trataria como nueva y podria aplicarla dos veces. La de otro actor tampoco se
// consulta (GET commands/{clave} solo responde por las del actor que pregunta: diria que no consta); solo admite el descarte
// explicito. La de actor desconocido se consulta: si consta con esta sesion, se aplico (D6.6); si no, no se sabe.
public enum PendingOutcome { Absent, Restored, Unreadable, Unavailable, OtherActor, UnknownActor }
// Command: la orden leida; en un bloqueo solo sirve para describirla, nunca se envia. Actor: quien la hizo (OtherActor).
public sealed record PendingLoad(PendingOutcome Outcome, PendingCommand? Command, string? Key, string Detail, string? Actor = null);

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
    // Una orden persistida de una sesion anterior DEL MISMO ACTOR queda restaurada y bloquea nuevas mutaciones
    // hasta reintentarla identica; un fichero ilegible o inaccesible bloquea hasta conciliar, y uno de otro actor
    // o sin actor anotado bloquea sin reenviarse nunca (D6.7: lo decide el almacen, que conoce la identidad).
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
    // E3.2: bytes de las imagenes ya leidas por esta conexion, por huella (se olvidan al cerrarla).
    public ImageCache Images { get; } = new();
    private readonly Dictionary<string, Task<ProductImage?>> reading = new(StringComparer.Ordinal);   // lecturas en curso por huella

    // E3.2: imagen vigente de un articulo, con la credencial de esta conexion (la ruta exige identidad: un <img> no podria mandarla).
    // Con la huella que trae el catalogo se mira antes la cache y las lecturas en curso: cada imagen se pide UNA vez aunque la
    // pidan a la vez la lista y la ficha. Al recibirla se comprueban el tipo declarado, la firma, el tamano y que su huella sea la
    // del ETag; si algo no cuadra no se guarda ni se devuelve. null = sin imagen.
    public async Task<ProductImage?> GetProductImageAsync(string productId, string? sha256 = null)
    {
        if (sha256 is not null && Images.Get(sha256) is { } cached) return new(productId, cached.Type, cached.Content, sha256);
        Task<ProductImage?>? read = null;
        lock (reading)
            if (sha256 is null || !reading.TryGetValue(sha256, out read))
            {
                read = ReadImageAsync(productId);
                if (sha256 is not null) reading[sha256] = read;
            }
        try { return await read! is { } image ? image with { ProductId = productId } : null; }
        finally { if (sha256 is not null) lock (reading) if (reading.TryGetValue(sha256, out var current) && current == read) reading.Remove(sha256); }
    }
    private async Task<ProductImage?> ReadImageAsync(string productId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SafePath("catalog/images/" + Segment(productId)));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;   // sin imagen, quitada o producto desconocido
        await Check(response);
        if (response.Content.Headers.ContentLength is > ProductImages.MaxBytes) throw new InvalidDataException("La imagen recibida pasa de 2 MB.");
        var content = await ReadLimitedAsync(response.Content, ProductImages.MaxBytes);
        var type = ProductImages.TypeOf(content);
        if (type is null || !string.Equals(type, response.Content.Headers.ContentType?.MediaType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La respuesta no es una imagen " + ProductImages.Formats + ".");
        var sha = ProductImages.Sha256(content);
        if (response.Headers.ETag is { } tag && !string.Equals(tag.Tag.Trim('"'), sha, StringComparison.Ordinal))
            throw new InvalidDataException("La imagen recibida no corresponde a su huella.");
        Images.Put(sha, type, content);
        return new(productId, type, content, sha);
    }
    // E3.2: poner o cambiar la imagen de un articulo es una orden normal de la tuberia incierta (clave, persistida antes de enviar,
    // mismo reintento identico). El tipo sale de los bytes; lo que pasa de 2 MB o no es JPEG, PNG o WebP no se envia. Confirmada,
    // sus bytes quedan en la cache por su huella: la relectura del catalogo no la vuelve a descargar.
    public async Task<JsonElement> SetProductImageAsync(string productId, byte[] content, string description)
    {
        var contentType = ProductImages.Check(content);
        var result = await SendAsync("erp/catalog/commands/product-image-set", new { productId, contentType, data = Convert.ToBase64String(content) }, description);
        Images.Put(ProductImages.Sha256(content), contentType, content);
        return result;
    }
    // E3.2: quitar la imagen (el servidor la marca inactiva; nada se borra). Misma tuberia.
    public Task<JsonElement> RemoveProductImageAsync(string productId, string description)
        => SendAsync("erp/catalog/commands/product-image-remove", new { productId }, description);
    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, int limit)
    {
        await using var stream = await content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > limit) throw new InvalidDataException("La imagen recibida pasa de 2 MB.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
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
                throw new InvalidOperationException(Blocked.Outcome switch
                {
                    PendingOutcome.OtherActor => "Hay una orden sin confirmar de otra sesión en este equipo y no se reenvía desde la tuya. Compruébala y descártala antes de operar.",
                    PendingOutcome.UnknownActor => "Hay una orden anterior sin autor anotado y no se reenvía. Consúltala en el servidor o descártala conservando la evidencia antes de operar.",
                    _ => "Hay una orden anterior ilegible o inaccesible. Consúltala en el servidor o descártala conservando la evidencia antes de operar."
                });
            if (Pending is not null) throw new InvalidOperationException("Resuelve primero la orden sin confirmar.");
            var command = new PendingCommand(Guid.NewGuid().ToString("N"), SafePath(path), JsonSerializer.Serialize(body, Json), description);
            // Durable ANTES del primer intento: un cierre forzado no la pierde. C3b (diferido de D6.7): pasa a Pending SOLO despues de
            // guardarse. Si el almacen falla, la excepcion sale de aqui sin Pending: nada se envia, ni ahora ni con RetryAsync, porque
            // nunca se envia lo que no se pudo guardar (antes quedaba en memoria y "Reintentar" la mandaba sin estar guardada).
            store?.Save(command);
            Pending = command;
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
    // D6.7: la de otro actor no se consulta: el servidor solo responde por las ordenes de quien pregunta.
    public async Task<string> ReconcileAsync()
    {
        if (!await mutation.WaitAsync(0)) throw new InvalidOperationException("Ya hay una operación en curso.");
        try
        {
            if (Blocked?.Outcome == PendingOutcome.OtherActor)
                throw new InvalidOperationException("Esa orden la hizo otra sesión: el servidor solo responde por las órdenes de quien pregunta y diría que no le consta aunque se aplicara. Compruébala con quien la hizo y después descártala.");
            var key = Blocked?.Key ?? throw new InvalidOperationException("La orden ilegible no conserva identificador: no se puede consultar.");
            var lookup = await GetAsync<CommandLookup>("commands/" + Segment(key));
            var outcome = lookup.Found ? "confirmed" : "unknown";
            if (lookup.Found) { store?.Discard(); Blocked = null; }
            LastReconciliation = outcome;
            return outcome;
        }
        finally { mutation.Release(); }
    }
    // Descartar: a cuarentena, nunca se destruye. La orden de otro actor se descarta sin consultar (consultarla no sirve, D6.7);
    // una ilegible o sin actor anotado, solo tras preguntar al servidor si conserva su identificador.
    public void DiscardBlocked()
    {
        if (Blocked is null) return;
        if (Blocked.Outcome == PendingOutcome.Unavailable)
            throw new InvalidOperationException("La orden es inaccesible, no ilegible: no se descarta. Vuelve a conectar cuando el fichero esté disponible.");
        if (Blocked.Outcome != PendingOutcome.OtherActor && Blocked.Key is not null && LastReconciliation != "unknown")
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
    public void Dispose() { http.Dispose(); mutation.Dispose(); Images.Clear(); (store as IDisposable)?.Dispose(); }
}
