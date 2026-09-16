using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Coordinador de conexion, ocupacion y realtime. Ninguna regla de negocio:
// la habilitacion visible sale exclusivamente de las affordances que envia el servidor.
public sealed partial class ShellViewModel : ObservableObject
{
    internal ApiClient? Api;
    private RealtimeSubscription? realtime;
    private bool refreshQueued;

    public ServiceViewModel Service { get; }
    public CheckoutViewModel Checkout { get; }

    [ObservableProperty] private string endpoint = "http://127.0.0.1:5088";
    [ObservableProperty] private SessionInfo? session;
    [ObservableProperty] private bool busy;
    [ObservableProperty] private PendingCommand? pending;
    [ObservableProperty] private string status = "Introduce la clave del servidor. La aplicación no almacena la clave en disco.";
    [ObservableProperty] private string realtimeState = "Tiempo real inactivo.";
    [ObservableProperty] private string readTime = "Sin lectura actual del servidor. El botón Actualizar sigue disponible como respaldo.";

    public ShellViewModel() { Service = new(this); Checkout = new(this); }

    public bool Connected => Session is not null;
    public bool IsMain => Session?.Role == "main";
    public bool Writable => Connected && !Busy && Pending is null;
    public bool HasPending => Pending is not null;
    public bool CanOpen => Session?.Actions?.Contains("open") == true;
    public bool TabsEnabled => Connected && !Busy;
    // Perfil de pantalla (UX, no autorizacion): el servidor sigue decidiendo cada accion.
    public bool IsKitchen => Session?.Role is "main" or "kitchen";
    public string PendingText => Pending is null ? "" :
        "SIN CONFIRMAR: " + Pending.Description + ". Conservamos el mismo identificador. No repitas la acción por otro medio sin comprobarla.";

    internal void Sync()
    {
        OnPropertyChanged(nameof(Connected)); OnPropertyChanged(nameof(IsMain));
        OnPropertyChanged(nameof(Writable)); OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(CanOpen)); OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(ConnectEnabled)); OnPropertyChanged(nameof(TabsEnabled));
        OnPropertyChanged(nameof(IsKitchen));
        DisconnectCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged(); RetryCommand.NotifyCanExecuteChanged();
        Service.Sync(); Checkout.Sync();
    }

    // Coalescencia: los avisos que llegan durante una operacion no la interrumpen; se relee al terminar.
    internal async Task Run(Func<Task> work)
    {
        if (Busy) return;
        Busy = true; Sync();
        try { await work(); }
        catch (Exception e)
        {
            Status = e switch
            {
                ApiError a => $"El servidor rechazó o no confirmó la petición: {a.Message}. Actualiza antes de una nueva acción.",
                HttpRequestException or TaskCanceledException => "Servidor sin respuesta. Los datos pueden estar desactualizados. Una orden pendiente NO está confirmada.",
                ArgumentException or InvalidOperationException => e.Message,
                _ => "No se pudo interpretar la respuesta. Comprueba la versión y actualiza; no dupliques la orden."
            };
        }
        finally
        {
            Busy = false; Pending = Api?.Pending; Sync();
            if (refreshQueued && Api is not null)
            {
                refreshQueued = false;
                _ = Application.Current.Dispatcher.BeginInvoke(async () => await Run(RefreshAll));
            }
        }
    }

    private void QueueRefresh()
    {
        if (Api is null) return;
        if (Busy) { refreshQueued = true; return; }
        _ = Run(RefreshAll);
    }

    internal async Task RefreshAll()
    {
        await Service.LoadAsync();
        if (IsMain && Checkout.Visible) await Checkout.LoadAsync();
        ReadTime = $"Datos leídos a las {DateTimeOffset.Now:HH:mm:ss} (lectura autoritativa del servidor).";
    }

    public Task ConnectWithKey(string key) => Run(async () =>
    {
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri)) throw new ArgumentException("Dirección no válida.");
        var candidate = new ApiClient(uri, key);
        try
        {
            var identity = await candidate.GetAsync<SessionInfo>("session");
            var config = await candidate.GetAsync<Configuration>("configuration");
            Api = candidate; Session = identity;
            Service.ApplyConfiguration(config);
            await StartRealtime(uri, key);
            await RefreshAll();
            Status = $"Conectado al servidor real · rol {identity.Role} · {identity.CompanyId}/{identity.LocationId}";
        }
        catch { if (Api == candidate) Reset(); else candidate.Dispose(); throw; }
    });

    // Fallo del canal = degradacion, no error: el refresco manual sigue siendo el respaldo.
    private async Task StartRealtime(Uri uri, string key)
    {
        try
        {
            realtime = new RealtimeSubscription(uri, key);
            realtime.Notified += _ => Application.Current.Dispatcher.BeginInvoke(QueueRefresh);
            realtime.Reconnected += () => Application.Current.Dispatcher.BeginInvoke(QueueRefresh);
            realtime.StateChanged += state => Application.Current.Dispatcher.BeginInvoke(() => RealtimeState = state switch
            {
                "connected" => "Tiempo real activo: los cambios de otros puestos aparecen sin pulsar Actualizar.",
                "reconnecting" => "Tiempo real reconectando. Los datos pueden estar desactualizados.",
                _ => "Tiempo real desconectado. Usa Actualizar; los datos no se refrescan solos."
            });
            await realtime.StartAsync();
        }
        catch
        {
            _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null;
            RealtimeState = "Tiempo real no disponible en este servidor. Usa el botón Actualizar.";
        }
    }

    private void Reset()
    {
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null; refreshQueued = false;
        RealtimeState = "Tiempo real inactivo.";
        Api?.Dispose(); Api = null; Session = null; Pending = null;
        Service.Clear(); Checkout.Clear();
        Status = "Desconectado. La clave no se guarda en disco.";
        ReadTime = "Sin lectura actual del servidor.";
        Sync();
    }

    internal void DisposeConnections()
    {
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask);
        Api?.Dispose();
    }

    // La clave viaja por code-behind (PasswordBox no es enlazable a proposito); el boton
    // Conectar usa Click + esta habilitacion. No hay comando para no simular uno vacio.
    public bool ConnectEnabled => !Connected && !Busy;

    private bool CanDisconnect() => Connected && !Busy && Pending is null;
    [RelayCommand(CanExecute = nameof(CanDisconnect))] private void Disconnect() => Reset();

    private bool CanRefresh() => Connected && !Busy;
    [RelayCommand(CanExecute = nameof(CanRefresh))] private Task Refresh() => Run(RefreshAll);

    private bool CanRetry() => HasPending && !Busy;
    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task Retry() => Run(async () =>
    {
        var path = Api!.Pending!.Path;
        var result = await Api.RetryAsync();
        if (path == "services") Service.SelectService(result.GetProperty("serviceId").GetString());
        await RefreshAll();
        Status = "Reintento confirmado con el mismo identificador.";
    });
}
