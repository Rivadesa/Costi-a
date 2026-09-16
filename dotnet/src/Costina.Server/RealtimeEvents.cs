using Costina.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace Costina.Server;

// El hub no expone metodos invocables: es un canal de solo-notificacion.
// La pertenencia a grupos se decide con el rol autenticado por el middleware,
// nunca con datos elegidos por el cliente.
public sealed class EventsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.GetHttpContext()?.Items["role"] as string
            ?? throw new HubException("Missing authenticated role.");
        await Groups.AddToGroupAsync(Context.ConnectionId, "ops");
        if (role == "main") await Groups.AddToGroupAsync(Context.ConnectionId, "fin");
        await base.OnConnectedAsync();
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
