using System.Collections.Concurrent;
using Costina.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace Costina.Server;

// El hub no expone metodos invocables: es un canal de solo-notificacion.
// La pertenencia a grupos se decide con el rol autenticado por el middleware,
// nunca con datos elegidos por el cliente.
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
