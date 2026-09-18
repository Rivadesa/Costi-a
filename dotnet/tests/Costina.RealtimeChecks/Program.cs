using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Costina.Client;
using Costina.Domain;
using Costina.Persistence;
using Npgsql;

// Comprobaciones D2: publicador del outbox contra PostgreSQL real (parte A) y flujo
// completo servidor+SignalR con procesos reales, filtrado economico y reconexion (parte B).
// Uso: dotnet run --project tests/Costina.RealtimeChecks -- <ruta Costina.Server.dll>

var results = new List<object>(); int passed = 0, failed = 0;
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; results.Add(new { name, passed = false, error = e.GetType().Name }); Console.WriteLine("FAIL " + name + ": " + e); }
}
void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
async Task WaitUntil(Func<bool> condition, int seconds, string what)
{
    for (var i = 0; i < seconds * 10; i++) { if (condition()) return; await Task.Delay(100); }
    throw new TimeoutException("Timed out waiting for: " + what);
}

var connectionString = Environment.GetEnvironmentVariable("COSTINA_DB")
    ?? throw new InvalidOperationException("COSTINA_DB is required.");
var database = new NpgsqlConnectionStringBuilder(connectionString).Database ?? "";
if (!database.EndsWith("_d1_lab", StringComparison.Ordinal) && !database.EndsWith("_d1_test", StringComparison.Ordinal))
    throw new InvalidOperationException("Realtime checks only run against an isolated *_d1_lab/*_d1_test database.");
await using var source = NpgsqlDataSource.Create(connectionString);
// D5.1: el esquema lo aplica el rol propietario; el resto de la suite usa la conexion de ejecucion.
await using (var ownerSource = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("COSTINA_DB_OWNER") ?? connectionString))
    await new PostgresStore(ownerSource).InitializeAsync();

async Task InsertEvent(Guid id, string tenant, string type, DateTimeOffset at)
{
    await using var command = source.CreateCommand(
        "INSERT INTO native_d1.outbox (id,tenant,company,location,aggregate_id,type,occurred_at,payload) " +
        "VALUES (@id,@tenant,'rt-company','rt-location','rt-aggregate',@type,@at,'{}'::jsonb)");
    command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("tenant", tenant);
    command.Parameters.AddWithValue("type", type); command.Parameters.AddWithValue("at", at);
    await command.ExecuteNonQueryAsync();
}
async Task<int> Unpublished(IEnumerable<Guid> ids)
{
    await using var command = source.CreateCommand(
        "SELECT count(*) FROM native_d1.outbox WHERE published_at IS NULL AND id = ANY(@ids)");
    command.Parameters.AddWithValue("ids", ids.ToArray());
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
OutboxPublisher Publisher(string tenant, FakeSink sink) => new(source, new BusinessScope(tenant, "rt-company", "rt-location"), sink);
async Task Drain(OutboxPublisher publisher) { while (await publisher.PublishPendingAsync() > 0) { } }

// ---------- Parte A: publicador con sink falso ----------
var now = DateTimeOffset.UtcNow;

await Check("A1 pending events publish in order and are marked", async () =>
{
    var sink = new FakeSink();
    var publisher = Publisher("rt-a1", sink);
    var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
    for (var i = 0; i < ids.Length; i++) await InsertEvent(ids[i], "rt-a1", "service.started", now.AddSeconds(i));
    await Drain(publisher);
    var mine = sink.Published.Where(p => ids.Contains(p.Notice.Id)).Select(p => p.Notice.Id).ToArray();
    Assert(mine.SequenceEqual(ids), "Publication order must follow occurred_at.");
    Assert(await Unpublished(ids) == 0, "Every published event must be marked.");
});

await Check("A2 financial types route to fin audience only", async () =>
{
    var sink = new FakeSink();
    var publisher = Publisher("rt-a2", sink);
    var cases = new (Guid Id, string Type, string Audience)[] {
        (Guid.NewGuid(), "account.charge_added", "fin"), (Guid.NewGuid(), "payment.recorded", "fin"),
        (Guid.NewGuid(), "account.opened", "fin"), (Guid.NewGuid(), "service.started", "ops"),
        (Guid.NewGuid(), "table.occupancy_released", "ops") };
    foreach (var c in cases) await InsertEvent(c.Id, "rt-a2", c.Type, now);
    await Drain(publisher);
    foreach (var c in cases)
        Assert(sink.Published.Single(p => p.Notice.Id == c.Id).Audience == c.Audience, "Wrong audience for " + c.Type);
});

await Check("A3 sink failure loses nothing and retries as duplicate", async () =>
{
    var first = Guid.NewGuid(); var second = Guid.NewGuid();
    await InsertEvent(first, "rt-a3", "service.started", now);
    await InsertEvent(second, "rt-a3", "course.fired", now.AddSeconds(1));
    var sink = new FakeSink { FailOnId = second };
    var publisher = Publisher("rt-a3", sink);
    try { await Drain(publisher); throw new Exception("Expected the injected sink failure."); }
    catch (Exception e) when (e.Message == "injected sink failure") { }
    Assert(await Unpublished([first, second]) == 2, "A batch failure must roll back every mark of the batch.");
    sink.FailOnId = null;
    await Drain(publisher);
    Assert(await Unpublished([first, second]) == 0, "Both events must publish after recovery.");
    Assert(sink.Published.Count(p => p.Notice.Id == first) >= 2, "At-least-once implies a bounded duplicate, never a loss.");
});

await Check("A4 already published events are never repeated", async () =>
{
    var id = Guid.NewGuid();
    await InsertEvent(id, "rt-a4", "service.started", now);
    var sink = new FakeSink();
    var publisher = Publisher("rt-a4", sink);
    await Drain(publisher); await Drain(publisher);
    Assert(sink.Published.Count(p => p.Notice.Id == id) == 1, "A marked event must not be republished.");
});

// ---------- Parte B: servidor real + SignalR ----------
var serverDll = args.Length == 1 ? args[0] : throw new ArgumentException("Pass the Costina.Server.dll path.");
// D4.3: sin claves de rol. Cada rol autentica como usuario lab-<rol>; keys guarda TOKENS de sesion.
var keys = new Dictionary<string, string> { ["MAIN"] = "", ["SERVICE"] = "", ["KITCHEN"] = "" };
const string LabPassword = "lab-password-ensayo-123";
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var endpoint = new Uri($"http://127.0.0.1:{port}");
var serverLog = new StringBuilder();

Process Spawn(string? argument)
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(serverDll); if (argument is not null) info.ArgumentList.Add(argument);
    info.Environment["COSTINA_LAB_MODE"] = "true"; info.Environment["COSTINA_DB"] = connectionString;
    info.Environment["COSTINA_TENANT"] = "rt-tenant"; info.Environment["COSTINA_COMPANY"] = "rt-company";
    info.Environment["COSTINA_LOCATION"] = "rt-location"; info.Environment["COSTINA_PORT"] = port.ToString();
    var process = Process.Start(info) ?? throw new Exception("Could not start the server process.");
    process.OutputDataReceived += (_, e) => { lock (serverLog) serverLog.AppendLine(e.Data); };
    process.ErrorDataReceived += (_, e) => { lock (serverLog) serverLog.AppendLine(e.Data); };
    process.BeginOutputReadLine(); process.BeginErrorReadLine();
    return process;
}
using var http = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(10) };
async Task WaitHealthy()
{
    await WaitUntil(() => {
        try { return http.GetAsync("/health").GetAwaiter().GetResult().IsSuccessStatusCode; }
        catch { return false; }
    }, 20, "server /health");
}
async Task<JsonElement> Post(string role, string path, object body)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, path)
    { Content = new StringContent(JsonSerializer.Serialize(body, Wire.Json), Encoding.UTF8, "application/json") };
    request.Headers.Add("Authorization", "Bearer " + keys[role]);
    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode) throw new Exception($"HTTP {(int)response.StatusCode}: {text}");
    return JsonSerializer.Deserialize<JsonElement>(text, Wire.Json);
}
async Task<int> RunCli(string input, params string[] arguments)
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(serverDll);
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    info.Environment["COSTINA_LAB_MODE"] = "true"; info.Environment["COSTINA_DB"] = connectionString;
    info.Environment["COSTINA_TENANT"] = "rt-tenant"; info.Environment["COSTINA_COMPANY"] = "rt-company";
    info.Environment["COSTINA_LOCATION"] = "rt-location";
    using var process = Process.Start(info) ?? throw new Exception("Could not start the CLI process.");
    await process.StandardInput.WriteLineAsync(input); process.StandardInput.Close();
    await process.WaitForExitAsync();
    return process.ExitCode;
}
async Task<JsonElement> Anon(string path, object body, string? bearer = null)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, path)
    { Content = new StringContent(JsonSerializer.Serialize(body, Wire.Json), Encoding.UTF8, "application/json") };
    if (bearer is not null) request.Headers.Add("Authorization", "Bearer " + bearer);
    var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode) throw new Exception($"HTTP {(int)response.StatusCode}: {text}");
    return JsonSerializer.Deserialize<JsonElement>(text, Wire.Json);
}
TimeSpan[] fastRetries = [.. Enumerable.Repeat(TimeSpan.FromMilliseconds(500), 60)];
RealtimeSubscription Subscribe(string role, ConcurrentQueue<Costina.Client.EventNotice> notices, ConcurrentQueue<string> states)
{
    var subscription = new RealtimeSubscription(endpoint, keys[role], fastRetries);
    subscription.Notified += notices.Enqueue;
    subscription.StateChanged += states.Enqueue;
    return subscription;
}

Process? server = null;
try
{
    using (var init = Spawn("init-lab")) { await init.WaitForExitAsync(); Assert(init.ExitCode == 0, "init-lab failed"); }
    server = Spawn(null); await WaitHealthy();
    foreach (var role in new[] { "main", "service", "kitchen" })
    {
        _ = await RunCli(LabPassword, "create-user", "lab-" + role, role);
        keys[role.ToUpperInvariant()] = (await Anon("/api/native/v1/auth/login",
            new { username = "lab-" + role, password = LabPassword })).GetProperty("token").GetString()!;
    }

    var mainNotices = new ConcurrentQueue<Costina.Client.EventNotice>(); var mainStates = new ConcurrentQueue<string>();
    var salaNotices = new ConcurrentQueue<Costina.Client.EventNotice>(); var salaStates = new ConcurrentQueue<string>();
    await using var main = Subscribe("MAIN", mainNotices, mainStates);
    await using var sala = Subscribe("SERVICE", salaNotices, salaStates);
    await main.StartAsync(); await sala.StartAsync();
    var reconnectedFired = false; main.Reconnected += () => reconnectedFired = true;

    string serviceId = "";
    await Check("B1 opening a service notifies every role without waiting for refresh", async () =>
    {
        var opened = await Post("MAIN", "/api/native/v1/services", new { tableId = "M1", pax = 2, menuId = "LAB-TASTING" });
        serviceId = opened.GetProperty("serviceId").GetString()!;
        await WaitUntil(() => mainNotices.Any(n => n.Type == "service.created") && mainNotices.Any(n => n.Type == "table.occupied"), 15, "main operational notices");
        await WaitUntil(() => salaNotices.Any(n => n.Type == "service.created"), 15, "service-role operational notice");
    });

    await Check("B2 financial notices reach main only", async () =>
    {
        await WaitUntil(() => mainNotices.Any(n => n.Type == "account.opened"), 15, "main financial notice");
        _ = await Post("MAIN", $"/api/native/v1/checkout/services/{serviceId}/commands/payment",
            new { expectedVersion = 1, paymentId = "rt-pay-1", method = "card", amountCents = 1000 });
        await WaitUntil(() => mainNotices.Any(n => n.Type == "payment.recorded"), 15, "payment notice on main");
        await Task.Delay(700); // margen: si sala fuera a recibirlo, ya lo tendria
        Assert(!salaNotices.Any(n => OutboxPublisher.IsFinancial(n.Type)),
            "Service role must never receive financial notice types.");
    });

    await Check("B3 every outbox event of this scope ends marked as published", async () =>
    {
        await WaitUntil(() => {
            using var command = source.CreateCommand("SELECT count(*) FROM native_d1.outbox WHERE tenant='rt-tenant' AND published_at IS NULL");
            return Convert.ToInt32(command.ExecuteScalar()) == 0;
        }, 15, "outbox drained for rt-tenant");
    });

    await Check("B4 notices carry identity only, never payload or money", () =>
    {
        Assert(mainNotices.All(n => n.Id != Guid.Empty && n.AggregateId.Length > 0 && n.OccurredAt != default));
        var properties = typeof(Costina.Client.EventNotice).GetProperties().Select(p => p.Name).ToArray();
        Assert(properties.Length == 4 && !properties.Any(p => p.Contains("Data") || p.Contains("Payload") || p.Contains("Cents")),
            "The notice contract must stay thin.");
        return Task.CompletedTask;
    });

    await Check("B5 restart reconnects and authoritative re-read works", async () =>
    {
        server!.Kill(entireProcessTree: true); await server.WaitForExitAsync();
        await WaitUntil(() => mainStates.Contains("reconnecting"), 15, "reconnecting state");
        server = Spawn(null); await WaitHealthy();
        await WaitUntil(() => reconnectedFired, 25, "automatic reconnection");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/native/v1/services/{serviceId}");
        request.Headers.Add("Authorization", "Bearer " + keys["MAIN"]);
        var response = await http.SendAsync(request);
        Assert(response.IsSuccessStatusCode, "Authoritative re-read after reconnection must succeed.");
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Wire.Json);
        Assert(body.GetProperty("version").GetInt64() >= 1, "Re-read must return a versioned state.");
    });

    await Check("B6 pairing issues a device identity and revocation aborts live realtime", async () =>
    {
        Assert(await RunCli("clave-de-ensayo-larga-1", "create-user", "gerente", "main") == 0, "create-user CLI must succeed");
        var admin = (await Anon("/api/native/v1/auth/login", new { username = "gerente", password = "clave-de-ensayo-larga-1" }))
            .GetProperty("token").GetString()!;
        var issued = await Anon("/api/native/v1/auth/pairings", new { }, admin);
        var claimed = await Anon("/api/native/v1/auth/pairings/claim",
            new { code = issued.GetProperty("code").GetString(), deviceName = "tablet-b6" });
        var pairingId = claimed.GetProperty("pairingId").GetString()!;
        _ = await Anon($"/api/native/v1/auth/pairings/{pairingId}/approve", new { role = "service", station = "sala-1" }, admin);
        var credentials = await Anon($"/api/native/v1/auth/pairings/{pairingId}/collect",
            new { pollSecret = claimed.GetProperty("pollSecret").GetString() });
        var deviceToken = credentials.GetProperty("deviceToken").GetString()!;
        var deviceStates = new ConcurrentQueue<string>();
        var deviceNotices = new ConcurrentQueue<Costina.Client.EventNotice>();
        // Sin reintentos de reconexion: el aborto del servidor se observa directamente como "closed".
        await using var deviceSub = new RealtimeSubscription(endpoint, deviceToken, []);
        deviceSub.StateChanged += deviceStates.Enqueue;
        deviceSub.Notified += deviceNotices.Enqueue;
        await deviceSub.StartAsync();
        _ = await Post("MAIN", "/api/native/v1/services", new { tableId = "M2", pax = 2, menuId = "LAB-TASTING" });
        await WaitUntil(() => deviceNotices.Any(n => n.Type == "service.created"), 15, "device realtime notice");
        _ = await Anon($"/api/native/v1/auth/devices/{credentials.GetProperty("deviceId").GetString()}/revoke", new { }, admin);
        await WaitUntil(() => deviceStates.Contains("closed"), 10, "revoked device connection aborted");
        using var probe = new HttpRequestMessage(HttpMethod.Get, "/api/native/v1/session");
        probe.Headers.Add("Authorization", "Bearer " + deviceToken);
        Assert((int)(await http.SendAsync(probe)).StatusCode == 401, "revoked device token must be rejected");
    });
}
finally
{
    try { server?.Kill(entireProcessTree: true); } catch { }
    Directory.CreateDirectory("artifacts/realtime");
    lock (serverLog) File.WriteAllText("artifacts/realtime/realtime-server.log", serverLog.ToString());
    File.WriteAllText("artifacts/realtime/realtime-checks.json",
        JsonSerializer.Serialize(new { passed, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"Realtime checks: {passed} passed; {failed} failed");
return failed == 0 ? 0 : 1;

sealed class FakeSink : IEventSink
{
    public Guid? FailOnId;
    public List<(string Audience, Costina.Persistence.EventNotice Notice)> Published { get; } = [];
    public Task PublishAsync(string audience, Costina.Persistence.EventNotice notice, CancellationToken ct)
    {
        if (notice.Id == FailOnId) throw new Exception("injected sink failure");
        Published.Add((audience, notice));
        return Task.CompletedTask;
    }
}
