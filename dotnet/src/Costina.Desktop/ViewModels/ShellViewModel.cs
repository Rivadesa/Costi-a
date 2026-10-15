using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Coordinador de conexion, ocupacion y realtime. Ninguna regla de negocio:
// la habilitacion visible sale exclusivamente de las affordances que envia el servidor.
//
// Corte B (ADR-013, plan 2026-09-24): el PC principal lleva DOS identidades. Nivel PUESTO = el dispositivo de este PC
// (token cifrado con DPAPI, como un comandero): arranca solo y toma comandas. Nivel RESPONSABLE = la sesion de una persona
// (solo en memoria, 30 min deslizantes / 14 h): abre caja, configuracion, ERP y dispositivos. Todo se ve siempre; lo sensible
// pide la contrasena del responsable en el momento y, tras 15 min sin tocar nada o si la sesion caduca, se vuelve a Puesto.
public sealed partial class ShellViewModel : ObservableObject
{
    public static readonly TimeSpan Inactivity = TimeSpan.FromMinutes(15), BootPatience = TimeSpan.FromMinutes(5);
    internal ApiClient? Api;
    private RealtimeSubscription? realtime;
    private DispatcherTimer? realtimeRetry;
    private bool refreshQueued;
    private string? sessionToken;   // solo en memoria; nunca a disco
    private DateTimeOffset lastActivity;
    // Reloj inyectable (los checks simulan la inactividad sin esperar).
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;
    public DesktopSettings Settings { get; private set; }
    internal void ReloadSettings() => Settings = DesktopSettings.Load();
    // La ventana selecciona la vista pedida cuando el responsable ya ha entrado (y al conectar, la primera visible).
    public event Action<string>? ViewRequested;

    public HomeViewModel Home { get; }                   // corte A: Inicio (estado, accesos, dispositivos); C2: panel con cifras y mini plano
    public ServiceViewModel Service { get; }
    public CheckoutViewModel Checkout { get; }
    public DevicesViewModel Devices { get; }
    public OrganizationViewModel Organization { get; }   // E2: Configuracion (solo main)
    public CatalogViewModel Catalog { get; }             // E3: ERP > Catalogo y tarifas (solo main)
    public OffersViewModel Offers { get; }               // E4a: ERP > Oferta: cartas y menus (solo main)
    // C1: nombres de mesa de la configuracion leida al conectar (Service.ApplyConfiguration); el codigo queda de respaldo.
    public Names Names => Service.Names;

    [ObservableProperty] private string endpoint = "http://127.0.0.1:5088";
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string pairName = "";
    [ObservableProperty] private string pairingStatus = "";
    [ObservableProperty] private bool pairingBusy;
    [ObservableProperty] private SessionInfo? session;
    [ObservableProperty] private bool busy;
    [ObservableProperty] private PendingCommand? pending;
    [ObservableProperty] private PendingLoad? blocked;
    // C1: vacio al arrancar (la capa Primeros pasos ya explica que hacer); el aviso de estado solo aparece cuando hay algo que decir.
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string realtimeState = "Tiempo real inactivo.";
    // C1: estado corto del tiempo real (connected, reconnecting, closed, unavailable, inactive): punto y palabra de la barra de estado.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RealtimeShort), nameof(RealtimeDot))] private string realtimeLink = "inactive";
    [ObservableProperty] private string readTime = "Sin lectura del servidor todavía (F5 para leer).";
    // Arranque (B1): mientras el servidor levanta tras encender el equipo, la ventana lo dice y espera.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LoginCardVisible), nameof(ShowFirstRun), nameof(ShowBoot), nameof(ServerDot), nameof(ServerStateText))] private bool booting;
    [ObservableProperty] private string bootText = "";
    // Elevacion (B3): la vista sensible pedida mientras se es Puesto; se abre sola al entrar el responsable.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LoginCardVisible))] [NotifyPropertyChangedFor(nameof(LoginTitle))] [NotifyPropertyChangedFor(nameof(ElevationRequested), nameof(ElevationMode))] private string? requestedView;
    public bool ElevationRequested => RequestedView is not null;
    // C1: vista abierta en el area de trabajo (nombre del TabItem). La fija la ventana al cambiar de vista; la barra lateral resalta
    // ESTA y no el boton pulsado: pedir Caja en el nivel Puesto abre la entrada del responsable y deja Inicio resaltado.
    [ObservableProperty] private string? currentView;
    // C1: barra lateral plegada a iconos (preferencia de pantalla de esta ventana; no se guarda).
    [ObservableProperty] private bool sideNavCompact;
    // C1: lo que se sabe del servidor antes de entrar: null = sin comprobar, true = responde, false = no responde o no es de fiar
    // (el motivo, en Status). Lo fija el arranque (StartAsync) y una conexion correcta; cambiar la direccion lo olvida.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ServerDot), nameof(ServerStateText), nameof(FirstRunServerText))] private bool? serverReady;

    public ShellViewModel()
    {
        Settings = DesktopSettings.Load();
        lastActivity = Clock();
        Home = new(this); Service = new(this); Checkout = new(this); Devices = new(this); Organization = new(this); Catalog = new(this); Offers = new(this);
        Home.Watch(Service);   // C2: las cifras y el mini plano de Inicio siguen a las tarjetas del Comedor (sin lecturas propias)
        // C1 (arreglo): la direccion guardada se asigna DESPUES de crear las vistas. Si es distinta de la de por defecto (todo
        // puesto adicional: https://NOMBRE:PUERTO), OnEndpointChanged -> Sync() recorre las vistas y antes reventaba (NullReference).
        Endpoint = Settings.Endpoint; Username = Settings.Username;
    }

    // Cambiar la sesion (conectar, desconectar, otro nivel) refresca todas las propiedades derivadas que enlazan menus y vistas.
    partial void OnSessionChanged(SessionInfo? value) => Sync();
    public bool Connected => Session is not null;
    public bool IsMain => Session?.Role == "main";
    // Niveles del corte B. IsResponsible = persona con sesion; IsStation = este PC como dispositivo.
    public bool IsResponsible => Connected && sessionToken is not null;
    public bool IsStation => Connected && sessionToken is null;
    public string LevelText => !Connected ? "" : IsResponsible ? "Responsable" : "Puesto";
    // ADR-012: la pestana de comedor y cocina existe solo si el modulo Dining esta activo en la instalacion.
    public bool HasDining => Session?.Modules?.Contains("dining", StringComparer.Ordinal) == true;
    // Fallo cerrado: con una orden sin confirmar o una orden anterior ilegible no se admite nada nuevo.
    public bool Writable => Connected && !Busy && Pending is null && Blocked is null;
    public bool HasPending => Pending is not null;
    public bool HasBlocked => Blocked is not null;
    public bool CanOpen => Session?.Actions?.Contains("open") == true;
    public bool TabsEnabled => Connected && !Busy;
    // Barra de estado: quien esta conectado, con que rol y contra que servidor (nunca credenciales). C1: el actor sin user:/device:
    // y el rol con el vocabulario comun (Responsable, Puesto de sala, Pantalla de cocina), sin repetir el nivel.
    public string ConnectionText
    {
        get
        {
            if (Session is null) return "Nadie ha entrado en este PC";   // C1: como el lienzo (antes "Sin conexion" junto a "Servidor listo": se contradecia)
            var role = RoleText(Session.Role); var actor = Labels.Actor(Session.Actor);
            string?[] parts = [$"{LevelText}: {(actor.Length > 0 ? actor : role)}", actor.Length > 0 && role != LevelText ? role : null,
                Session.StationName, Endpoint, Session.Demo ? "DEMO" : null];
            return string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }
    }
    // C1 (revision): rol de la sesion con el vocabulario comun. Un dispositivo antiguo con rol principal (nivel Puesto) no es una
    // persona: se rotula como dispositivo, "Principal (antiguo)", nunca "Responsable".
    public string RoleText(string role) => IsStation && role == "main" ? Labels.DeviceRole(role) : Labels.SessionRole(role);
    // Perfil de pantalla (UX, no autorizacion): el servidor sigue decidiendo cada accion.
    public bool IsKitchen => Session?.Role is "main" or "kitchen";
    // Tarjeta de entrada: sin conexion (y sin arrancar), o cuando una vista sensible pide al responsable.
    public bool LoginCardVisible => (!Connected && !Booting) || RequestedView is not null;
    // C1: Primeros pasos = este PC sin sesion y sin esperar al servidor; mientras espera, la capa de arranque. Con sesion (puesto) y un
    // apartado sensible pedido, la misma tarjeta de entrada se abre centrada sobre un velo (ElevationMode).
    public bool ShowFirstRun => !Connected && !Booting;
    public bool ShowBoot => !Connected && Booting;
    public bool ElevationMode => Connected && RequestedView is not null;
    // C1: barra de estado y Primeros pasos: punto (ok, warn, danger, off) y palabra; el detalle de un fallo va en Status.
    // Mientras el arranque espera al servidor la barra lo dice igual que la capa de arranque (no "sin comprobar").
    public string ServerDot => Connected || ServerReady == true ? "ok" : Booting ? "warn" : ServerReady == false ? "danger" : "off";
    public string ServerStateText => Connected ? "Servidor conectado" : ServerReady == true ? "Servidor listo" : Booting ? "Esperando al servidor"
        : ServerReady == false ? "Servidor sin respuesta" : "Servidor sin comprobar";
    public string FirstRunServerText => ServerReady switch
    {
        true => $"El servidor está listo {ServerPlace}",
        false => $"No se pudo conectar con el servidor {ServerPlace}",
        null => $"Servidor configurado {ServerPlace}"
    };
    internal string ServerPlace => Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) ? uri.IsLoopback ? "en este equipo" : $"en {uri.Host}" : "(dirección no válida)";
    public string RealtimeShort => Labels.Realtime(RealtimeLink);
    public string RealtimeDot => RealtimeLink switch { "connected" => "ok", "reconnecting" or "closed" => "warn", _ => "off" };
    // C1: pastilla de la barra superior: nivel y quien (la estacion para un puesto). Vacia sin sesion.
    public string LevelBadge
    {
        get
        {
            if (Session is null) return "";
            var actor = Labels.Actor(Session.Actor);
            var who = IsResponsible ? actor : Session.StationName is { Length: > 0 } station ? station : actor;
            return who.Length > 0 ? $"{LevelText} · {who}" : LevelText;
        }
    }
    // C1: version del paquete leida del ensamblado (Version del csproj), sin el commit que el build anade tras '+'. Nunca escrita a mano.
    public static string AppVersion { get; } = ReadVersion();
    public string VersionText => "Costiña " + AppVersion;
    private static string ReadVersion()
    {
        var informational = typeof(ShellViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
    public string LoginTitle => RequestedView is null ? "Entrar como responsable" : $"Entrar como responsable para abrir {ViewTitle(RequestedView)}";
    // C1: el nombre de cada vista sale del vocabulario comun (el mismo de la barra lateral y de los accesos de Inicio).
    public static string ViewTitle(string view) => Labels.View(view);
    // Lo sensible (caja, configuracion, ERP, dispositivos) exige a una persona identificada; el servidor lo impone ademas (403).
    public static bool RequiresResponsible(string view) => view is "CheckoutTab" or "OrganizationTab" or "CatalogTab" or "OffersTab" or "DevicesTab";
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
        OnPropertyChanged(nameof(Connected)); OnPropertyChanged(nameof(IsMain)); OnPropertyChanged(nameof(HasDining));
        OnPropertyChanged(nameof(IsResponsible)); OnPropertyChanged(nameof(IsStation)); OnPropertyChanged(nameof(LevelText));
        OnPropertyChanged(nameof(Writable)); OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(HasBlocked)); OnPropertyChanged(nameof(BlockedText));
        OnPropertyChanged(nameof(CanOpen)); OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(ConnectEnabled)); OnPropertyChanged(nameof(TabsEnabled)); OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(IsKitchen)); OnPropertyChanged(nameof(HasPairedDevice)); OnPropertyChanged(nameof(LoginCardVisible)); OnPropertyChanged(nameof(LoginTitle));
        OnPropertyChanged(nameof(ShowFirstRun)); OnPropertyChanged(nameof(ShowBoot)); OnPropertyChanged(nameof(ElevationMode));
        OnPropertyChanged(nameof(ServerDot)); OnPropertyChanged(nameof(ServerStateText)); OnPropertyChanged(nameof(LevelBadge));
        ConnectPairedCommand.NotifyCanExecuteChanged(); PairDeviceCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged(); LeaveResponsibleCommand.NotifyCanExecuteChanged(); CancelElevationCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged(); RetryCommand.NotifyCanExecuteChanged();
        ReconcileCommand.NotifyCanExecuteChanged(); DiscardCommand.NotifyCanExecuteChanged();
        Home.Sync(); Service.Sync(); Checkout.Sync(); Devices.Sync(); Organization.Sync(); Catalog.Sync(); Offers.Sync();
    }

    // Coalescencia: los avisos que llegan durante una operacion no la interrumpen; se relee al terminar.
    internal async Task Run(Func<Task> work)
    {
        if (Busy) return;
        Busy = true; Sync();
        try { await work(); }
        catch (ApiError unauthorized) when (unauthorized.Status == 401) { await OnUnauthorized(unauthorized); }
        catch (Exception e)
        {
            Status = e switch
            {
                // C1: el codigo del rechazo en una frase clara; uno desconocido sale con su codigo entre parentesis y una respuesta
                // sin codigo reconocible (request_failed: proxy, error interno) con su estado HTTP. Con la orden aun pendiente no hay
                // rechazo que contar: no se sabe si se aplico (el aviso de la orden sin confirmar dice como seguir).
                ApiError a when Api?.Pending is not null => Labels.Failure(a.Code, a.Status, stillPending: true),
                ApiError a => Labels.Failure(a.Code, a.Status, stillPending: false) + " Actualiza antes de una nueva acción.",
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

    // B4 (F1): un 401 ya no es un error generico que deja la ventana "conectada". Sesion del responsable caducada o revocada:
    // se vuelve al nivel Puesto (si este PC lo tiene) o a la tarjeta de entrada; la orden sin confirmar sigue en disco y
    // reaparece al volver a entrar con el mismo rol. 401 del puesto: revocado; se olvida la credencial y se explica.
    public async Task OnUnauthorized(ApiError? error = null)
    {
        if (!Connected) { Status = "No autorizado: la credencial ya no vale."; return; }
        if (IsResponsible)
        {
            sessionToken = null;   // el servidor ya no la reconoce: no hay nada que cerrar
            var hadPending = Pending is not null;
            ResetInternal();
            Status = "Tu sesión de responsable ha caducado o fue cerrada." + (hadPending ? " La ORDEN SIN CONFIRMAR sigue guardada y reaparecerá al volver a entrar como responsable." : "")
                + (HasPairedDevice ? " Este PC sigue conectado como puesto." : " Entra de nuevo para continuar.");
            if (HasPairedDevice) await TryConnectStation();
            return;
        }
        try { new DpapiDeviceStore(ParseEndpoint()).Clear(); } catch (ArgumentException) { }
        ResetInternal();
        Status = "Este PC ya no está emparejado como puesto (lo quitaron desde Dispositivos, o el servidor se restauró desde una copia anterior). Entra como responsable y vuelve a dejarlo conectado.";
    }

    private void QueueRefresh()
    {
        if (Api is null) return;
        if (Busy) { refreshQueued = true; return; }
        _ = Run(RefreshAll);
    }

    internal async Task RefreshAll()
    {
        if (Home.Visible) await Home.LoadAsync();
        // C2: la configuracion (mesas activas, ofertas vigentes, estaciones) se relee con el tablero: una mesa creada en Configuracion
        // aparece en el plano sin reconectar y la oferta de la comida deja paso a la de la cena.
        if (HasDining) { Service.ApplyConfiguration(await Api!.GetAsync<Configuration>("configuration")); await Service.LoadAsync(); }
        if (IsMain && Checkout.Visible) await Checkout.LoadAsync();
        if (IsMain && Devices.Visible) await Devices.LoadAsync();
        if (IsMain && Organization.Visible) await Organization.LoadAsync();
        if (IsMain && Catalog.Visible) await Catalog.LoadAsync();
        if (IsMain && Offers.Visible) await Offers.LoadAsync();
        ReadTime = $"Datos leídos del servidor a las {DateTimeOffset.Now:HH:mm:ss}.";
    }

    internal Uri ParseEndpoint()
    {
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri)) throw new ArgumentException("Dirección no válida.");
        return uri;
    }
    // F4: 'Conectar puesto emparejado' se reevalua al teclear. C1: otra direccion es otro servidor: lo comprobado del anterior no vale.
    // C1 (revision): el lugar del servidor de Primeros pasos depende de la direccion, no solo de ServerReady (que solo cambia con la
    // primera tecla): se avisa en cada cambio.
    partial void OnEndpointChanged(string value) { ServerReady = null; OnPropertyChanged(nameof(FirstRunServerText)); Sync(); }

    // ---- Arranque (B1): esperar al servidor sin rendirse en frio, distinguir red de certificado, conectar solo como puesto ----
    public async Task StartAsync(CancellationToken ct = default)
    {
        Booting = true; BootText = "Buscando el servidor…"; Sync();
        try
        {
            Uri uri;
            try { uri = ParseEndpoint(); } catch (ArgumentException e) { Status = e.Message; return; }
            var started = Clock();
            while (!ct.IsCancellationRequested)
            {
                try { using var auth = new AuthClient(uri); await auth.ProbeAsync(ct); ServerReady = true; break; }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ApiError)
                {
                    switch (AuthClient.Classify(e))
                    {
                        case StartupProblem.Certificate:
                            ServerReady = false;
                            Status = $"No se puede confiar en el certificado del servidor {uri.Host}. Instala en este equipo el certificado de la instalación (LÉEME del servidor) o revisa la dirección. No se reintenta.";
                            return;
                        case StartupProblem.Other:
                            ServerReady = false;
                            Status = $"En {uri} responde algo que no es el motor de Costiña. Revisa la dirección del servidor.";
                            return;
                    }
                    var waited = Clock() - started;
                    if (waited > BootPatience) { ServerReady = false; Status = $"El servidor {uri.Host} no responde tras {(int)BootPatience.TotalMinutes} minutos. Comprueba que el equipo servidor está encendido y conectado, y pulsa Actualizar."; return; }
                    BootText = $"Arrancando el servidor ({(int)waited.TotalSeconds} s). Tras encender el equipo puede tardar de uno a tres minutos.";
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
            }
            if (HasPairedDevice) await TryConnectStation();
        }
        catch (OperationCanceledException) { }
        finally { Booting = false; BootText = ""; Sync(); }
    }

    private async Task TryConnectStation()
    {
        try { await Run(ConnectPairedCore); } catch (InvalidOperationException e) { Status = e.Message; }
    }

    // D4.3b: sin claves de laboratorio. Entrada por usuario+contrasena (la contrasena no pasa
    // por bindings ni se guarda; el token de sesion solo vive en memoria) o por puesto emparejado.
    public Task LoginWithPassword(string password) => Run(async () =>
    {
        var uri = ParseEndpoint();
        using var auth = new AuthClient(uri);
        LoginResult login;
        try { login = await auth.LoginAsync(Username, password); }
        catch (ApiError error) when (error.Status == 401)
        { throw new InvalidOperationException("Usuario o contraseña incorrectos."); } // generico: sin pistas
        catch (ApiError error) when (error.Status == 429)
        { throw new InvalidOperationException("Demasiados intentos desde este equipo. Espera un minuto."); }
        var requested = RequestedView;
        await ConnectWithToken(uri, login.Token, isUserSession: true);
        Settings.Endpoint = Endpoint.Trim(); Settings.Username = Username.Trim(); Settings.Save();
        NoteActivity();
        RequestedView = null;
        if (requested is not null) ViewRequested?.Invoke(requested);
    });

    // B3: una vista sensible pedida en el nivel Puesto abre la tarjeta de entrada; al entrar se abre esa vista.
    public void RequestElevation(string view)
    {
        if (IsMain) { ViewRequested?.Invoke(view); return; }
        RequestedView = view;
        Status = $"{ViewTitle(view)} es solo para el responsable: entra con tu usuario y contraseña. El puesto sigue operativo.";
        Sync();
    }
    // C2: una vista pedida desde un viewmodel (Inicio: abrir mesa, una mesa del mini plano, un asistente de Dispositivos) sigue la logica
    // del menu: un apartado sensible en el nivel Puesto pide antes al responsable; el resto se abre sin mas.
    public void OpenView(string view)
    {
        if (RequiresResponsible(view)) RequestElevation(view);
        else ViewRequested?.Invoke(view);
    }
    private bool CanCancelElevation() => RequestedView is not null && !Busy;
    [RelayCommand(CanExecute = nameof(CanCancelElevation))]
    private void CancelElevation()
    {
        RequestedView = null;
        Devices.Request(null);   // C2: un asistente pedido desde Inicio no se abre solo mas tarde, cuando nadie lo espera
        Status = "Sigues en el nivel Puesto."; Sync();
    }

    private bool CanConnectPaired() => ConnectEnabled && HasPairedDevice;
    [RelayCommand(CanExecute = nameof(CanConnectPaired))]
    private Task ConnectPaired() => Run(ConnectPairedCore);
    private async Task ConnectPairedCore()
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
    }

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
                    Settings.Endpoint = Endpoint.Trim(); Settings.Save();
                    PairingStatus = $"Emparejado como {Labels.SessionRole(collect.Role ?? "")}.";   // la estacion, por su nombre, al conectar
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
            // Cambiar de nivel cierra la conexion y el tiempo real anteriores (B3): nunca dos canales vivos de la misma ventana.
            await StopRealtime();
            var previous = Api;
            Api = candidate; ServerReady = true; Session = identity; sessionToken = isUserSession ? token : null;
            previous?.Dispose();
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
                : (identity.Demo ? "DEMOSTRACIÓN · DATOS FICTICIOS · " : "") +
                  (isUserSession ? $"Responsable {Labels.Actor(identity.Actor)}".TrimEnd() + $": acceso a todos los apartados. Tras {(int)Inactivity.TotalMinutes} minutos sin actividad se vuelve al nivel Puesto."
                                 : $"Puesto {Labels.Actor(identity.Actor)}".TrimEnd() + (identity.StationName is { Length: > 0 } s ? $" · {s}" : "") + ": toma comandas. Caja, configuración y dispositivos piden la contraseña del responsable.")
                  + $" · servidor {identity.ServerVersion}";
        }
        catch { if (Api == candidate) Reset(); else candidate.Dispose(); throw; }
    }

    // Fallo del canal = degradacion, no error: el refresco manual sigue siendo el respaldo.
    // B5 (F2): cuando la reconexion automatica se agota ("closed"), se reintenta cada 30 s sin limite, con estado visible.
    private async Task StartRealtime(Uri uri, string key)
    {
        try
        {
            realtime = new RealtimeSubscription(uri, key);
            realtime.Notified += _ => Application.Current.Dispatcher.BeginInvoke(QueueRefresh);
            realtime.Reconnected += () => Application.Current.Dispatcher.BeginInvoke(QueueRefresh);
            realtime.StateChanged += state => Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // C1: empieza por la etiqueta corta del vocabulario comun; la explicacion va detras. La barra de estado pinta la corta.
                RealtimeLink = state is "connected" or "reconnecting" ? state : "closed";
                RealtimeState = state switch
                {
                    "connected" => Labels.Realtime(state) + ": los cambios de otros puestos aparecen sin pulsar Actualizar.",
                    "reconnecting" => Labels.Realtime(state) + " Los datos pueden estar desactualizados.",
                    _ => Labels.Realtime("closed") + " cada 30 s. Mientras tanto usa Actualizar; los datos no se refrescan solos."
                };
                if (state == "closed") ScheduleRealtimeRetry(); else realtimeRetry?.Stop();
            });
            await realtime.StartAsync();
        }
        catch
        {
            await StopRealtime();
            RealtimeLink = "unavailable";
            RealtimeState = Labels.Realtime("unavailable") + " en este servidor. Usa el botón Actualizar.";
        }
    }

    private void ScheduleRealtimeRetry()
    {
        realtimeRetry ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
        realtimeRetry.Tick -= RealtimeRetryTick; realtimeRetry.Tick += RealtimeRetryTick; realtimeRetry.Start();
    }
    private async void RealtimeRetryTick(object? sender, EventArgs e)
    {
        if (realtime is null) { realtimeRetry?.Stop(); return; }
        if (await realtime.TryRestartAsync()) realtimeRetry?.Stop();
    }

    private async Task StopRealtime()
    {
        realtimeRetry?.Stop();
        if (realtime is { } previous) { realtime = null; try { await previous.DisposeAsync(); } catch (Exception e) when (e is HttpRequestException or ObjectDisposedException or InvalidOperationException) { } }
    }

    // ---- Niveles (B3): salir del modo responsable vuelve al puesto; la inactividad lo hace sola ----
    public void NoteActivity() => lastActivity = Clock();
    // Solo para los checks del WPF: simula haber entrado como responsable sin servidor (el token nunca se usa).
    public void AssumeResponsible(SessionInfo identity) { sessionToken = "checks-only"; Session = identity; NoteActivity(); }
    // Devuelve true si ha habido que salir del modo responsable. Lo llama la ventana cada minuto; los checks lo llaman a mano.
    public bool CheckInactivity()
    {
        if (!IsResponsible || Busy || Pending is not null || Clock() - lastActivity < Inactivity) return false;
        _ = LeaveResponsible("Sin actividad durante " + (int)Inactivity.TotalMinutes + " minutos: ");
        return true;
    }
    private bool CanLeaveResponsible() => IsResponsible && !Busy && Pending is null;
    [RelayCommand(CanExecute = nameof(CanLeaveResponsible))]
    private Task LeaveResponsibleAsync() => LeaveResponsible("");
    private async Task LeaveResponsible(string reason)
    {
        Reset();   // cierra la sesion en el servidor (mejor esfuerzo) y limpia la ventana
        if (HasPairedDevice) { await TryConnectStation(); Status = reason + "Has salido del modo responsable. " + Status; }
        else Status = reason + "Has salido del modo responsable. Este PC no está conectado como puesto: entra de nuevo o déjalo conectado desde Dispositivos.";
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
        realtimeRetry?.Stop();
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null; refreshQueued = false; sessionToken = null; RequestedView = null;
        RealtimeState = "Tiempo real inactivo."; RealtimeLink = "inactive";
        Api?.Dispose(); Api = null; Session = null; Pending = null; Blocked = null;
        Home.Clear(); Service.Clear(); Checkout.Clear(); Devices.Clear(); Organization.Clear(); Catalog.Clear(); Offers.Clear();
        Status = "Desconectado. La contraseña no se guarda en disco.";
        ReadTime = "Sin lectura del servidor todavía (F5 para leer).";
        Sync();
    }

    internal void DisposeConnections()
    {
        realtimeRetry?.Stop();
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask);
        Api?.Dispose();
    }

    // La clave viaja por code-behind (PasswordBox no es enlazable a proposito); el boton
    // Entrar usa Click + esta habilitacion. No hay comando para no simular uno vacio.
    // Con una vista sensible pedida, el puesto (ya conectado) tambien puede entrar como responsable.
    public bool ConnectEnabled => (!Connected || RequestedView is not null) && !Busy && !PairingBusy && !Booting;
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
        if (path == "dining/services") Service.SelectService(result.GetProperty("serviceId").GetString());
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
