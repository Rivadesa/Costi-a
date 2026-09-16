using Microsoft.AspNetCore.SignalR.Client;

namespace Costina.Client;

public sealed record EventNotice(Guid Id, string Type, string AggregateId, DateTimeOffset OccurredAt);

// Suscripcion de solo-notificacion. Las notificaciones son finas y pueden llegar
// duplicadas: el consumidor NUNCA aplica estado desde el canal; tras cualquier aviso
// y, obligatoriamente, tras cada reconexion, relee el estado autoritativo por HTTP
// comparando versiones. Si la reconexion automatica se agota se emite "closed" y el
// refresco manual sigue siendo el respaldo.
public sealed class RealtimeSubscription : IAsyncDisposable
{
    private readonly HubConnection connection;
    public event Action<EventNotice>? Notified;
    public event Action? Reconnected;            // el consumidor debe relanzar la lectura autoritativa
    public event Action<string>? StateChanged;   // "connected" | "reconnecting" | "closed"

    public RealtimeSubscription(Uri endpoint, string key, TimeSpan[]? retryDelays = null)
    {
        ApiClient.ValidateEndpoint(endpoint);
        if (key.Length < 32 || key.Any(char.IsWhiteSpace)) throw new ArgumentException("Clave de laboratorio incompleta.");
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(endpoint, "/api/native/v1/events"),
                options => options.Headers["Authorization"] = "Bearer " + key)
            .WithAutomaticReconnect(retryDelays ?? [TimeSpan.Zero, TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)])
            .Build();
        connection.On<EventNotice>("event", notice => Notified?.Invoke(notice));
        connection.Reconnecting += _ => { StateChanged?.Invoke("reconnecting"); return Task.CompletedTask; };
        connection.Reconnected += _ => { StateChanged?.Invoke("connected"); Reconnected?.Invoke(); return Task.CompletedTask; };
        connection.Closed += _ => { StateChanged?.Invoke("closed"); return Task.CompletedTask; };
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await connection.StartAsync(ct);
        StateChanged?.Invoke("connected");
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
