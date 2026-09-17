using System.Collections.Concurrent;
using Costina.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace Costina.Server;

// El hub no expone metodos invocables: es un canal de solo-notificacion.
// La pertenencia a grupos se decide con el rol autenticado por el middleware,
// nunca con datos elegidos por el cliente.
// D4.3 (F06, preparacion PWA): billete efimero para el hub. Un navegador no puede enviar
// cabeceras arbitrarias en WebSockets, asi que el hub acepta ?access_token= SOLO con un billete
// de un solo uso y 60 segundos emitido a una identidad ya autenticada. Vive en memoria del
// proceso (el hub es el mismo proceso), nunca se registra en logs y se consume al usarse.
public sealed record HubTicket(string Role, string Actor, string? DeviceId, string? Station);
public sealed class HubTicketStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,(HubTicket Ticket,DateTimeOffset Expires)> tickets = new();
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private static string HashOf(string value) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    public (string Token, DateTimeOffset ExpiresAt) Issue(HubTicket ticket)
    {
        foreach (var (key, value) in tickets) if (value.Expires < DateTimeOffset.UtcNow) tickets.TryRemove(key, out _);
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = DateTimeOffset.UtcNow + Lifetime;
        tickets[HashOf(token)] = (ticket, expires);
        return (token, expires);
    }

    public HubTicket? Consume(string token)
        => tickets.TryRemove(HashOf(token), out var entry) && entry.Expires > DateTimeOffset.UtcNow ? entry.Ticket : null;
}

// D4.2: registro de conexiones vivas por dispositivo. La revocacion no espera a la proxima
// peticion: aborta en el acto las conexiones SignalR del dispositivo revocado.
public sealed class DeviceConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HubCallerContext>> byDevice = new();
    public void Register(string deviceId, HubCallerContext context)
        => byDevice.GetOrAdd(deviceId, _ => new()).TryAdd(context.ConnectionId, context);
    public void Unregister(string deviceId, string connectionId)
    { if (byDevice.TryGetValue(deviceId, out var map)) map.TryRemove(connectionId, out _); }
    public void AbortAll(string deviceId)
    { if (byDevice.TryRemove(deviceId, out var map)) foreach (var context in map.Values) context.Abort(); }
}

public sealed class EventsHub(DeviceConnectionRegistry devices) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var role = http?.Items["role"] as string
            ?? throw new HubException("Missing authenticated role.");
        if (http?.Items["deviceId"] is string deviceId) devices.Register(deviceId, Context);
        await Groups.AddToGroupAsync(Context.ConnectionId, "ops");
        if (role == "main") await Groups.AddToGroupAsync(Context.ConnectionId, "fin");
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.GetHttpContext()?.Items["deviceId"] is string deviceId)
            devices.Unregister(deviceId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class HubEventSink(IHubContext<EventsHub> hub) : IEventSink
{
    public Task PublishAsync(string audience, EventNotice notice, CancellationToken ct)
        => hub.Clients.Group(audience).SendAsync("event", notice, ct);
}

// Bucle supervisado dentro del proceso del servidor: un fallo registra el tipo de error
// y reintenta con espera mayor; el bucle no muere y no publica secretos en el log.
public sealed class OutboxPublisherService(OutboxPublisher publisher, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromMilliseconds(250);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var published = await publisher.PublishPendingAsync(ct);
                delay = published > 0 ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromMilliseconds(250);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogError("Outbox publish failed: {ErrorType}", e.GetType().Name);
                delay = TimeSpan.FromSeconds(5);
            }
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { break; }
        }
    }
}
