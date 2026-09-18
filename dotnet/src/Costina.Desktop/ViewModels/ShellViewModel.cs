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
    public DevicesViewModel Devices { get; }

    [ObservableProperty] private string endpoint = "http://127.0.0.1:5088";
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string pairName = "";
    [ObservableProperty] private string pairingStatus = "";
    [ObservableProperty] private bool pairingBusy;
    private string? sessionToken;   // solo en memoria; nunca a disco
    [ObservableProperty] private SessionInfo? session;
    [ObservableProperty] private bool busy;
    [ObservableProperty] private PendingCommand? pending;
    [ObservableProperty] private PendingLoad? blocked;
    [ObservableProperty] private string status = "Entra con tu usuario y contraseña o conecta este puesto emparejado. La contraseña no se guarda en disco.";
    [ObservableProperty] private string realtimeState = "Tiempo real inactivo.";
    [ObservableProperty] private string readTime = "Sin lectura actual del servidor. El botón Actualizar sigue disponible como respaldo.";

    public ShellViewModel() { Service = new(this); Checkout = new(this); Devices = new(this); }

    public bool Connected => Session is not null;
    public bool IsMain => Session?.Role == "main";
    // Fallo cerrado: con una orden sin confirmar o una orden anterior ilegible no se admite nada nuevo.
    public bool Writable => Connected && !Busy && Pending is null && Blocked is null;
    public bool HasPending => Pending is not null;
    public bool HasBlocked => Blocked is not null;
    public bool CanOpen => Session?.Actions?.Contains("open") == true;
    public bool TabsEnabled => Connected && !Busy;
    // Perfil de pantalla (UX, no autorizacion): el servidor sigue decidiendo cada accion.
    public bool IsKitchen => Session?.Role is "main" or "kitchen";
    public string PendingText => Pending is null ? "" :
        "SIN CONFIRMAR: " + Pending.Description + ". Conservamos el mismo identificador. No repitas la acción por otro medio sin comprobarla.";
    public string BlockedText => Blocked is null ? "" : Blocked.Outcome switch
    {
        PendingOutcome.Unavailable => "ORDEN ANTERIOR INACCESIBLE: " + Blocked.Detail
            + " No se admiten órdenes nuevas hasta poder leerla. Cierra otras ventanas o revisa el disco y vuelve a conectar.",
        _ => "ORDEN ANTERIOR ILEGIBLE" + (Blocked.Key is null ? " (sin identificador)" : " (identificador " + Blocked.Key + ")") + ": "
            + Blocked.Detail + " Pudo haberse aplicado. Consulta al servidor antes de operar; la evidencia se conserva en cuarentena al descartar."
    };

    internal void Sync()
    {
        OnPropertyChanged(nameof(Connected)); OnPropertyChanged(nameof(IsMain));
        OnPropertyChanged(nameof(Writable)); OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(HasBlocked)); OnPropertyChanged(nameof(BlockedText));
        OnPropertyChanged(nameof(CanOpen)); OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(ConnectEnabled)); OnPropertyChanged(nameof(TabsEnabled));
        OnPropertyChanged(nameof(IsKitchen)); OnPropertyChanged(nameof(HasPairedDevice));
        ConnectPairedCommand.NotifyCanExecuteChanged(); PairDeviceCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged(); RetryCommand.NotifyCanExecuteChanged();
        ReconcileCommand.NotifyCanExecuteChanged(); DiscardCommand.NotifyCanExecuteChanged();
        Service.Sync(); Checkout.Sync(); Devices.Sync();
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
            Busy = false; Pending = Api?.Pending; Blocked = Api?.Blocked; Sync();
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
        if (IsMain && Devices.Visible) await Devices.LoadAsync();
        ReadTime = $"Datos leídos a las {DateTimeOffset.Now:HH:mm:ss} (lectura autoritativa del servidor).";
    }

    private Uri ParseEndpoint()
    {
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri)) throw new ArgumentException("Dirección no válida.");
        return uri;
    }

    // D4.3b: sin claves de laboratorio. Entrada por usuario+contraseña (la contraseña no pasa
    // por bindings ni se guarda; el token de sesion solo vive en memoria) o por puesto emparejado.
    public Task LoginWithPassword(string password) => Run(async () =>
    {
        var uri = ParseEndpoint();
        using var auth = new AuthClient(uri);
        LoginResult login;
        try { login = await auth.LoginAsync(Username, password); }
        catch (ApiError error) when (error.Status == 401)
        { throw new InvalidOperationException("Usuario o contraseña incorrectos."); } // generico: sin pistas
        await ConnectWithToken(uri, login.Token, isUserSession: true);
    });

    private bool CanConnectPaired() => ConnectEnabled && HasPairedDevice;
    [RelayCommand(CanExecute = nameof(CanConnectPaired))]
    private Task ConnectPaired() => Run(async () =>
    {
        var uri = ParseEndpoint();
        var store = new DpapiDeviceStore(uri);
        var token = store.Load() ?? throw new InvalidOperationException("Este puesto no está emparejado con ese servidor.");
        try { await ConnectWithToken(uri, token, isUserSession: false); }
        catch (ApiError error) when (error.Status == 401)
        {
            store.Clear(); Sync();
            throw new InvalidOperationException("El emparejamiento ya no es válido (revocado). Vuelve a emparejar este puesto.");
        }
    });

    // Emparejar: reclama el codigo, espera la aprobacion del administrador (sondeo cada 2 s
    // mientras el codigo vive) y guarda el token del puesto cifrado con DPAPI.
    private bool CanPairDevice() => ConnectEnabled;
    [RelayCommand(CanExecute = nameof(CanPairDevice))]
    private async Task PairDevice(string code)
    {
        if (PairingBusy) return;
        try
        {
            PairingBusy = true; Sync();
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(PairName))
                throw new ArgumentException("Introduce el código del administrador y un nombre para este puesto.");
            var uri = ParseEndpoint();
            using var auth = new AuthClient(uri);
            var claim = await auth.ClaimAsync(code, PairName);
            PairingStatus = "Solicitud enviada. Esperando la aprobación del administrador…";
            for (var attempt = 0; attempt < 150; attempt++)
            {
                await Task.Delay(2000);
                var collect = await auth.CollectAsync(claim.PairingId, claim.PollSecret);
                if (collect.Status == "denied") { PairingStatus = "Solicitud denegada por el administrador."; return; }
                if (collect.Status == "approved")
                {
                    new DpapiDeviceStore(uri).Save(collect.DeviceToken!);
                    PairingStatus = $"Puesto emparejado como {collect.Role}/{collect.Station}.";
                    Sync();
                    await Run(() => ConnectWithToken(uri, collect.DeviceToken!, isUserSession: false));
                    return;
                }
            }
            PairingStatus = "La aprobación no llegó a tiempo. Pide un código nuevo.";
        }
        catch (Exception error) when (error is ApiError or ArgumentException or InvalidOperationException)
        { PairingStatus = error is ApiError ? "Código no válido o caducado. Pide uno nuevo." : error.Message; }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        { PairingStatus = "Servidor sin respuesta. Comprueba la dirección y vuelve a solicitar el emparejamiento."; }
        catch (Exception error) when (error is System.Text.Json.JsonException or KeyNotFoundException or System.Security.Cryptography.CryptographicException or System.IO.IOException)
        { PairingStatus = "No se pudo completar el emparejamiento en este puesto. Pide un código nuevo."; }
        finally { PairingBusy = false; Sync(); }
    }

    private async Task ConnectWithToken(Uri uri, string token, bool isUserSession)
    {
        var candidate = new ApiClient(uri, token);
        try
        {
            var identity = await candidate.GetAsync<SessionInfo>("session");
            if (string.IsNullOrEmpty(identity.InstallationId))
                throw new InvalidOperationException("El servidor no identifica su instalación: actualiza el motor a la misma versión que este cliente.");
            var config = await candidate.GetAsync<Configuration>("configuration");
            Api = candidate; Session = identity; sessionToken = isUserSession ? token : null;
            // Cola durable: con la identidad ya autenticada se engancha el almacen cifrado, aislado por
            // instalacion, ambito, rol y ventana. Una orden sin confirmar de otra sesion se restaura y
            // bloquea todo hasta reintentarla identica; una ilegible bloquea hasta conciliar con el servidor.
            candidate.AttachPendingStore(new DpapiPendingStore(identity));
            Pending = candidate.Pending; Blocked = candidate.Blocked;
            Service.ApplyConfiguration(config);
            await StartRealtime(uri, token);
            await RefreshAll();
            Status = Blocked is not null
                ? "Conectado. ORDEN ANTERIOR NO LEGIBLE: no se admiten órdenes nuevas hasta consultarla al servidor o descartarla conservando la evidencia."
                : Pending is not null
                ? "Conectado. ORDEN SIN CONFIRMAR recuperada de una sesión anterior: \"" + Pending.Description
                    + "\". Reintenta la misma orden antes de operar."
                : $"Conectado al servidor real {identity.ServerVersion} · {identity.Actor ?? identity.Role} · rol {identity.Role}" +
                  (identity.Station is null ? "" : $" · estación {identity.Station}") +
                  $" · {identity.CompanyId}/{identity.LocationId}";
        }
        catch { if (Api == candidate) Reset(); else candidate.Dispose(); throw; }
    }

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
        if (sessionToken is not null)
        {
            var token = sessionToken; sessionToken = null;
            try { using var auth = new AuthClient(ParseEndpoint()); _ = auth.LogoutAsync(token); } catch (ArgumentException) { }
        }
        ResetInternal();
    }

    private void ResetInternal()
    {
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null; refreshQueued = false; sessionToken = null;
        RealtimeState = "Tiempo real inactivo.";
        Api?.Dispose(); Api = null; Session = null; Pending = null; Blocked = null;
        Service.Clear(); Checkout.Clear(); Devices.Clear();
        Status = "Desconectado. La contraseña no se guarda en disco.";
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
    public bool ConnectEnabled => !Connected && !Busy && !PairingBusy;
    public bool HasPairedDevice
    {
        get { try { return new DpapiDeviceStore(ParseEndpoint()).Load() is not null; } catch (ArgumentException) { return false; } }
    }

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

    // Conciliacion asistida de una orden ilegible: el servidor dice si consta como aplicada.
    private bool CanReconcile() => Connected && !Busy && Blocked is { Outcome: PendingOutcome.Unreadable, Key: not null };
    [RelayCommand(CanExecute = nameof(CanReconcile))]
    private Task Reconcile() => Run(async () =>
    {
        var outcome = await Api!.ReconcileAsync();
        await RefreshAll();
        Status = outcome == "confirmed"
            ? "El servidor confirma que la orden anterior SÍ se aplicó. Bloqueo retirado y datos releídos; la evidencia queda en cuarentena."
            : "El servidor NO tiene constancia de la orden anterior. Comprueba el estado antes de repetirla; ahora puedes descartarla (la evidencia se conserva).";
    });

    // Descartar solo tras consultar (o si no hay identificador que consultar); nunca destruye el fichero.
    private bool CanDiscard() => Connected && !Busy && Blocked is { Outcome: PendingOutcome.Unreadable } load
        && (load.Key is null || Api?.LastReconciliation == "unknown");
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private Task Discard() => Run(() =>
    {
        Api!.DiscardBlocked();
        Status = "Orden ilegible descartada. El fichero queda en cuarentena en la carpeta de la aplicación para revisión.";
        return Task.CompletedTask;
    });
}
