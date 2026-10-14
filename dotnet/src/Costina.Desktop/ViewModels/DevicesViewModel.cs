using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Centro de dispositivos (corte A, plan 2026-09-24; antes "Puestos y emparejamiento", D4.3b/D6.5). Solo rol principal (el
// servidor lo impone ademas por permisos). Dos asistentes CON INTENCION: "Conectar comandero" (estacion de sala o barra) y
// "Conectar pantalla de cocina" (una estacion de cocina, pase o barra). Cada asistente recuerda SU pairingId, consulta las
// solicitudes cada 2 s mientras vive el codigo y solo aprueba la suya (las ajenas solo se deniegan). No hay desplegable de rol
// ni opcion "Principal" (el servidor la veta: A1) y la coherencia rol x tipo de estacion la comprueba el servidor (A2): aqui
// solo se ofrece lo que encaja. El QR lleva SOLO el codigo de un uso, en el fragmento: nunca un token ni una credencial (D6.5).
public sealed partial class DevicesViewModel(ShellViewModel shell) : ObservableObject
{
    // Fila de la lista: el dispositivo, su grupo (comanderos / pantalla por estacion) y si es este mismo PC (corte B).
    public sealed record DeviceRow(DeviceRowDto Device, string Group, bool IsThisPc)
    {
        public string Text => (IsThisPc ? "ESTE PC · " : "") + $"{Device.Name} · {Device.StateLabel} · aprobado por {Labels.Actor(Device.ApprovedBy)} el {Device.CreatedAt.ToLocalTime():dd/MM/yyyy}";
        // C1 (revision): punto de presencia junto al texto (que ya lo dice con palabras): verde con conexion, gris sin ella o quitado.
        public string PresenceDot => Device.Active && Device.Online ? "ok" : "off";
    }

    private bool rendering;
    // C1: nombres de TODAS las estaciones leidas (tambien las desactivadas) para rotular los grupos; sin nombre, el codigo.
    private StationDto[] knownStations = [];   // todas las leidas, tambien las desactivadas (el sondeo vuelve a pintar con ellas)
    private Names stationNames = Names.Empty;
    private DispatcherTimer? poll;
    private DateTimeOffset? codeExpiresAt;
    private bool addressSuggested;

    // Lo leido
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NeedsRoomStation))] private StationDto[] roomStations = [];   // sala o barra: donde va un comandero
    [ObservableProperty] private StationDto[] kitchenStations = [];                                                     // cocina, pase o barra: donde va una pantalla
    [ObservableProperty] private StationDto? selectedRoomStation;
    [ObservableProperty] private PairingPendingDto[] pending = [];
    [ObservableProperty] private PairingPendingDto? selectedPending;
    [ObservableProperty] private DeviceRowDto[] devices = [];
    [ObservableProperty] private DeviceRow[] rows = [];
    [ObservableProperty] private DeviceRow? selectedRow;
    [ObservableProperty] private bool showRevoked;
    [ObservableProperty] private string listSummary = "";
    public bool NeedsRoomStation => RoomStations.Length == 0;

    // Asistente en curso (intencion ligada al pairingId emitido)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAssistant))] private string assistantTitle = "";
    [ObservableProperty] private string assistantText = "";
    [ObservableProperty] private string intentRole = "";
    [ObservableProperty] private string intentStationId = "";
    [ObservableProperty] private string intentStationName = "";
    [ObservableProperty] private string pairingId = "";
    [ObservableProperty] private string pairingCode = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasRecognised))] [NotifyPropertyChangedFor(nameof(RecognisedText))] private PairingPendingDto? recognised;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))] private string duplicateWarning = "";
    public bool HasAssistant => AssistantTitle.Length > 0;
    public bool HasRecognised => Recognised is not null;
    public bool HasDuplicateWarning => DuplicateWarning.Length > 0;
    public string IntentLabel => IntentRole == "service" ? $"comandero en {IntentStationName}" : $"pantalla de cocina de {IntentStationName}";
    public string RecognisedText => Recognised is null ? "" : $"\"{Recognised.DeviceName}\" pide ser {IntentLabel}. Comprueba que es el dispositivo que tienes delante y aprueba.";

    // QR del enlace https://EQUIPO:PUERTO/app/#pair=CODIGO (D6.5)
    private const string QrIdle = "Al generar el código aparece aquí su QR: el dispositivo lo lee con la cámara y abre la aplicación con el código ya puesto.";
    [ObservableProperty] private string deviceAddress = "";
    [ObservableProperty] private string pairingUrl = "";
    [ObservableProperty] private string qrInfo = QrIdle;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasQr))] private ImageSource? pairingQr;
    public bool HasQr => PairingQr is not null;
    // Corte B: la direccion para los dispositivos vive en desktop.json (DesktopSettings). Los checks apuntan la carpeta de
    // preferencias a un directorio temporal a traves de esta propiedad.
    public string? MemoryFolder { get => DesktopSettings.Folder; set { DesktopSettings.Folder = value; shell.ReloadSettings(); } }
    public string ThisPcText => shell.HasPairedDevice ? "Este PC ya está conectado como puesto: al encenderlo entra solo." : "";

    // Quitar en dos pasos (F9: nada irreversible con un solo toque)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ConfirmingRemoval))] [NotifyPropertyChangedFor(nameof(RemovalText))] private DeviceRow? removalCandidate;
    public bool ConfirmingRemoval => RemovalCandidate is not null;
    public string RemovalText => RemovalCandidate is null ? "" : $"¿Quitar \"{RemovalCandidate.Device.Name}\"? Dejará de funcionar en el acto y habrá que conectarlo de nuevo"
        + (RemovalCandidate.IsThisPc ? ". ES ESTE PC: tendrás que volver a conectarlo como responsable" : "") + ".";

    public bool Visible { get; set; }

    internal void Sync()
    {
        StartWaiterCommand.NotifyCanExecuteChanged(); StartKitchenCommand.NotifyCanExecuteChanged(); CreateRoomStationCommand.NotifyCanExecuteChanged();
        NewCodeCommand.NotifyCanExecuteChanged(); ApproveCommand.NotifyCanExecuteChanged(); DenyCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        AskRemoveCommand.NotifyCanExecuteChanged(); ConfirmRemoveCommand.NotifyCanExecuteChanged(); CancelRemoveCommand.NotifyCanExecuteChanged();
        KeepThisPcConnectedCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(ThisPcText));
        AskEnableSimpleCommand.NotifyCanExecuteChanged(); ConfirmEnableSimpleCommand.NotifyCanExecuteChanged(); CancelEnableSimpleCommand.NotifyCanExecuteChanged();
        DisableSimpleCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        poll?.Stop();
        rendering = true;
        try
        {
            Stations = []; knownStations = []; stationNames = Names.Empty; RoomStations = []; KitchenStations = []; SelectedRoomStation = null; Pending = []; SelectedPending = null;
            Devices = []; Rows = []; SelectedRow = null; ListSummary = ""; RemovalCandidate = null;
            AssistantTitle = ""; AssistantText = ""; IntentRole = ""; IntentStationId = ""; IntentStationName = ""; PairingId = ""; PairingCode = "";
            Recognised = null; DuplicateWarning = ""; codeExpiresAt = null; addressSuggested = false; DeviceAddress = ""; PairingQr = null; PairingUrl = ""; QrInfo = QrIdle;
            Network = null; ConfirmingSimple = false;
        }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        await ReadNetworkAsync();
        if (!addressSuggested) await SuggestAddressAsync();
        var pendingRows = await shell.Api.GetAsync<PairingPendingDto[]>("auth/pairings/pending");
        var deviceRows = await shell.Api.GetAsync<DeviceRowDto[]>("auth/devices");
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        Render(organization.Stations, pendingRows, deviceRows);
    }

    // Composicion pura de lo leido (los checks la ejercitan sin servidor). El tipo de estacion decide que asistente la ofrece;
    // la regla de verdad vive en el servidor (A2).
    public void Render(StationDto[] allStations, PairingPendingDto[] pendingRows, DeviceRowDto[] deviceRows)
    {
        rendering = true;
        try
        {
            knownStations = allStations; Stations = allStations.Where(s => s.Active).ToArray(); stationNames = new Names(stations: allStations);
            RoomStations = Stations.Where(s => s.Kind is "Room" or "Bar").ToArray();
            KitchenStations = Stations.Where(s => s.Kind is "Kitchen" or "Pass" or "Bar").ToArray();
            if (SelectedRoomStation is null || RoomStations.All(s => s.Id != SelectedRoomStation.Id)) SelectedRoomStation = RoomStations.FirstOrDefault();
            ExpireCode();
            var previous = SelectedPending?.PairingId;
            Pending = pendingRows; SelectedPending = pendingRows.FirstOrDefault(p => p.PairingId == previous) ?? pendingRows.FirstOrDefault();
            Devices = deviceRows;
            // Solo la solicitud del asistente abierto se reconoce (y se puede aprobar): la intencion vive aqui, ligada al pairingId.
            Recognised = PairingId.Length == 0 ? null : pendingRows.FirstOrDefault(p => p.PairingId == PairingId);
            DuplicateWarning = Recognised is { } request && deviceRows.Any(d => d.Active && string.Equals(d.Name, request.DeviceName, StringComparison.OrdinalIgnoreCase))
                ? $"Ya hay un dispositivo activo llamado \"{request.DeviceName}\". Dos con el mismo nombre comparten identidad en la auditoría: quita el antiguo antes o pide otro nombre en el dispositivo."
                : "";
            RebuildRows();
        }
        finally { rendering = false; }
        Sync();
    }

    private void RebuildRows()
    {
        var me = shell.Session?.Actor;   // "device:<nombre>" cuando este PC trabaja como puesto (corte B)
        string Group(DeviceRowDto d) => d.Role switch
        {
            "service" => "Comanderos",
            "kitchen" => "Pantalla de cocina · " + stationNames.Station(d.Station),
            "main" => "Dispositivos antiguos con rol principal (quítalos y conéctalos de nuevo)",
            var other => other
        };
        Rows = Devices.Where(d => ShowRevoked || d.Active)
            .OrderBy(d => d.Role switch { "service" => 0, "kitchen" => 1, _ => 2 }).ThenBy(d => d.Station, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal)
            .Select(d => new DeviceRow(d, Group(d), me is not null && me == "device:" + d.Name)).ToArray();
        var view = CollectionViewSource.GetDefaultView(Rows);
        view.GroupDescriptions.Clear(); view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DeviceRow.Group)));
        var selected = SelectedRow?.Device.Id; SelectedRow = Rows.FirstOrDefault(r => r.Device.Id == selected);
        if (RemovalCandidate is { } candidate && Rows.All(r => r.Device.Id != candidate.Device.Id)) RemovalCandidate = null;
        var active = Devices.Where(d => d.Active).ToArray();
        var removed = Devices.Length - active.Length;
        ListSummary = active.Length == 0 ? "Todavía no hay dispositivos conectados."
            : $"Activos: {Labels.Plural(active.Count(d => d.Role == "service"), "comandero", "comanderos")} y {Labels.Plural(active.Count(d => d.Role == "kitchen"), "pantalla de cocina", "pantallas de cocina")}; {active.Count(d => d.Online)} con conexión ahora."
              + (removed > 0 && !ShowRevoked ? " " + Labels.Plural(removed, "quitado (oculto)", "quitados (ocultos)") + "." : "");
    }
    partial void OnShowRevokedChanged(bool value) { if (!rendering) RebuildRows(); }
    partial void OnSelectedRowChanged(DeviceRow? value) { if (!rendering) Sync(); }
    partial void OnSelectedPendingChanged(PairingPendingDto? value) { if (!rendering) Sync(); }
    partial void OnSelectedRoomStationChanged(StationDto? value) { if (!rendering) Sync(); }

    // ---- Direccion con la que los DISPOSITIVOS llegan al servidor (https://NOMBRE:PUERTO, la del certificado de D5.3) ----
    // Se propone la de este cliente si ya habla por la LAN; por loopback, la que el servidor anuncia en su diagnostico (A7) o la
    // ultima usada en este equipo; y se recuerda.
    private async Task SuggestAddressAsync()
    {
        addressSuggested = true;
        if (DeviceAddress.Length > 0) return;
        var own = OwnAddress();
        if (own.Length == 0)
            try
            {
                var diagnostics = await shell.Api!.GetAsync<DiagnosticsDto>("diagnostics");
                if (diagnostics.Tls is { Enabled: true, Port: { } port, Names: { Length: > 0 } name }) own = $"https://{name}:{port}";
            }
            catch (Exception e) when (e is ApiError or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
        DeviceAddress = own.Length > 0 ? own : Recall();
    }
    public void SuggestAddress()   // sin servidor (checks): la propia direccion o la recordada
    {
        if (addressSuggested) return;
        addressSuggested = true;
        if (DeviceAddress.Length > 0) return;
        var own = OwnAddress();
        DeviceAddress = own.Length > 0 ? own : Recall();
    }
    private string OwnAddress() => Uri.TryCreate(shell.Endpoint.Trim(), UriKind.Absolute, out var endpoint) ? PairingLink.SuggestDeviceAddress(endpoint) : "";

    // ---- B.2 (ADR-014): conexion sencilla por la red del local (HTTP sin cifrar, con la IP de este PC) ----
    // El servidor dice si la tiene, si esta encendida, su puerto, la IP de la red del local y el tipo de cada red de Windows. Con la
    // conexion sencilla encendida el QR lleva http://IP:PUERTO y el dispositivo no necesita ningun certificado. La enciende y la apaga
    // el responsable (la vista ya lo exige y el servidor responde 403 a cualquier otro); encenderla se confirma en dos pasos.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimpleAvailable))] [NotifyPropertyChangedFor(nameof(SimpleOn))] [NotifyPropertyChangedFor(nameof(SimpleOff))] [NotifyPropertyChangedFor(nameof(OfferSimple))]
    [NotifyPropertyChangedFor(nameof(SimpleText))] [NotifyPropertyChangedFor(nameof(NetworkWarning))] [NotifyPropertyChangedFor(nameof(HasNetworkWarning))]
    private NetworkDto? network;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(OfferSimple))] private bool confirmingSimple;
    public bool SimpleAvailable => Network?.LanHttp is { Listening: true, Port: not null };
    public bool SimpleOn => SimpleAvailable && Network!.LanHttp.Enabled;
    public bool SimpleOff => SimpleAvailable && !Network!.LanHttp.Enabled;
    public bool OfferSimple => SimpleOff && !ConfirmingSimple;   // mientras se confirma, solo la barra de confirmacion
    public string SimpleText => !SimpleAvailable ? ""
        : SimpleOn ? (SimpleAddressNow() is { } address
            ? $"Conexión sencilla activa: móviles y tablets entran por {address} con solo leer el QR, sin instalar nada. El tráfico viaja sin cifrar por la red del local: úsala solo con el Wi-Fi del personal, nunca con el de los clientes."
            : "Conexión sencilla activa, pero no se encuentra la IP de este servidor en la red del local: comprueba que está conectado por cable o Wi-Fi.")
        : "Para conectar móviles y tablets sin instalar ningún certificado, activa la conexión sencilla: entran por la IP de este PC con solo leer el QR.";
    public string NetworkWarning
    {
        get
        {
            var exposed = Network?.PublicNetworks ?? [];
            if (exposed.Length == 0) return "";
            var names = string.Join(", ", exposed.Select(n => "«" + n.Name + "»"));
            return $"La red {names} del servidor está marcada como «Pública» en Windows: así ningún móvil ni tablet puede llegar a él. En el PC servidor, abre Configuración › Red e Internet, entra en la conexión y cambia «Tipo de perfil de red» a «Privada» (solo si es la red de confianza del local).";
        }
    }
    public bool HasNetworkWarning => NetworkWarning.Length > 0;
    private string? SimpleAddressNow() => SimpleOn && Network!.SuggestedIp is { } ip ? PairingLink.SimpleAddress(ip, Network.LanHttp.Port!.Value) : null;

    private async Task ReadNetworkAsync()
    {
        try { ApplyNetwork(await shell.Api!.GetAsync<NetworkDto>("installation/network")); }
        catch (Exception e) when (e is ApiError or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { ApplyNetwork(null); }
    }

    // Composicion pura (los checks la ejercitan sin servidor): con la conexion sencilla encendida la direccion para los dispositivos
    // pasa a ser http://IP:PUERTO; al apagarla, una direccion http deja de valer y se vuelve a proponer la https de siempre.
    public void ApplyNetwork(NetworkDto? value)
    {
        Network = value;
        if (SimpleAddressNow() is { } simple) { if (!DeviceAddress.StartsWith("http://", StringComparison.Ordinal)) DeviceAddress = simple; }
        else if (DeviceAddress.StartsWith("http://", StringComparison.Ordinal)) { DeviceAddress = ""; addressSuggested = false; }   // LoadAsync vuelve a proponer la https
        RefreshQr();
        Sync();
    }

    private bool CanAskEnableSimple() => shell.Writable && shell.IsMain && SimpleOff && !ConfirmingSimple;
    [RelayCommand(CanExecute = nameof(CanAskEnableSimple))]
    private void AskEnableSimple() { ConfirmingSimple = true; Sync(); }

    private bool CanCancelEnableSimple() => ConfirmingSimple;
    [RelayCommand(CanExecute = nameof(CanCancelEnableSimple))]
    private void CancelEnableSimple() { ConfirmingSimple = false; Sync(); }

    private bool CanConfirmEnableSimple() => shell.Writable && shell.IsMain && SimpleOff && ConfirmingSimple;
    [RelayCommand(CanExecute = nameof(CanConfirmEnableSimple))]
    private Task ConfirmEnableSimple() => SetSimple(true);

    private bool CanDisableSimple() => shell.Writable && shell.IsMain && SimpleOn;
    [RelayCommand(CanExecute = nameof(CanDisableSimple))]
    private Task DisableSimple() => SetSimple(false);

    private Task SetSimple(bool enabled) => shell.Run(async () =>
    {
        ConfirmingSimple = false;
        await shell.Api!.SendAsync("installation/commands/lan-http-set", new { enabled }, enabled ? "Activar la conexión sencilla" : "Desactivar la conexión sencilla");
        shell.Status = enabled
            ? "Conexión sencilla activada: el QR lleva ya la IP de este servidor. Úsala solo con el Wi-Fi del personal."
            : "Conexión sencilla desactivada: los dispositivos que entraban por ella dejan de llegar hasta que se active de nuevo.";
        await LoadAsync();
    });

    partial void OnPairingCodeChanged(string value) => RefreshQr();
    partial void OnDeviceAddressChanged(string value) => RefreshQr();

    private void RefreshQr()
    {
        if (rendering) return;
        if (PairingCode.Length == 0) { PairingQr = null; PairingUrl = ""; QrInfo = QrIdle; return; }
        try
        {
            var address = PairingLink.ParseDeviceAddress(DeviceAddress, SimpleOn);
            var url = PairingLink.Build(address, PairingCode);
            PairingQr = QrImage.Render(url); PairingUrl = url;
            QrInfo = address.Scheme == "http"
                ? "Apunta la cámara del dispositivo a este QR y abre el enlace en el navegador (Chrome o Safari): la aplicación se abre con el código ya puesto. No hay que instalar nada (conexión sencilla)."
                : "Apunta la cámara del dispositivo a este QR: abre la aplicación con el código ya puesto (el dispositivo debe tener instalado el certificado de este servidor).";
            Remember(address);
        }
        catch (ArgumentException e)
        {
            PairingQr = null; PairingUrl = "";
            QrInfo = "Sin QR: " + e.Message + (SimpleOn ? " Escribe en «Dirección para los dispositivos» http://IP:PUERTO de este servidor." : " Escribe en «Dirección para los dispositivos» la dirección https del servidor, o activa la conexión sencilla.") + " El código se puede teclear igualmente.";
        }
    }

    private string Recall()
    {
        try { return shell.Settings.DeviceAddress.Length == 0 ? "" : PairingLink.ParseDeviceAddress(shell.Settings.DeviceAddress).GetLeftPart(UriPartial.Authority); }
        catch (ArgumentException) { return ""; }
    }
    private void Remember(Uri address)
    {
        var authority = address.GetLeftPart(UriPartial.Authority);
        if (shell.Settings.DeviceAddress == authority) return;
        shell.Settings.DeviceAddress = authority; shell.Settings.Save();
    }

    // ---- B6: dejar ESTE PC conectado como puesto de sala, con una sola sesion del responsable ----
    // Emite el codigo, lo reclama este mismo programa, comprueba que la solicitud reclamada es la emitida, la aprueba con la
    // estacion elegida y recoge el token, que se guarda cifrado con DPAPI (el unico secreto en disco). Auditado al responsable.
    public bool CanKeepThisPc => shell.Writable && shell.IsMain && shell.IsResponsible && SelectedRoomStation is not null && !shell.HasPairedDevice;
    [RelayCommand(CanExecute = nameof(CanKeepThisPc))]
    private Task KeepThisPcConnected() => shell.Run(async () =>
    {
        var uri = shell.ParseEndpoint();
        var station = SelectedRoomStation!;
        var name = ThisPcName();
        var issued = await shell.Api!.PostRawAsync("auth/pairings", new { });
        using var auth = new AuthClient(uri);
        var claim = await auth.ClaimAsync(issued.GetProperty("code").GetString()!, name);
        if (claim.PairingId != issued.GetProperty("pairingId").GetString()) throw new InvalidOperationException("La solicitud reclamada no es la que se acaba de emitir.");
        await shell.Api.PostRawAsync("auth/pairings/" + ApiClient.Segment(claim.PairingId) + "/approve", new { role = "service", station = station.Id });
        var collect = await auth.CollectAsync(claim.PairingId, claim.PollSecret);
        if (collect.Status != "approved" || collect.DeviceToken is null) throw new InvalidOperationException("No se pudo recoger la credencial del puesto.");
        new DpapiDeviceStore(uri).Save(collect.DeviceToken);
        shell.Status = $"Este PC queda conectado como puesto \"{name}\" en {station.Name}. Al encenderlo entrará solo; al salir del modo responsable seguirá operativo.";
        OnPropertyChanged(nameof(ThisPcText)); shell.Sync();
        await LoadAsync();
    });
    public static string ThisPcName()
    {
        var machine = new string(Environment.MachineName.ToLowerInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        var name = "pc-" + (machine.Length > 0 ? machine : "principal");
        return name.Length > 60 ? name[..60] : name;
    }

    // ---- Asistentes ----
    private bool CanStart() => shell.Writable && shell.IsMain && !HasAssistant;
    private bool CanStartWaiter() => CanStart() && SelectedRoomStation is not null;
    [RelayCommand(CanExecute = nameof(CanStartWaiter))]
    private Task StartWaiter() => Start("service", SelectedRoomStation!);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartKitchen(StationDto station) => Start("kitchen", station);

    // Sin ninguna estacion de sala no hay donde poner un comandero: se crea "Sala" (tipo sala, auditada al responsable, E2).
    private bool CanCreateRoomStation() => shell.Writable && shell.IsMain && NeedsRoomStation;
    [RelayCommand(CanExecute = nameof(CanCreateRoomStation))]
    private Task CreateRoomStation() => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("organization/commands/station-create", new { id = "sala", name = "Sala", kind = "room", sort = 1 }, "Crear estación Sala");
        shell.Status = "Estación «Sala» creada (Configuración › Salas, mesas y estaciones). Ya puedes conectar comanderos.";
        await LoadAsync();
    });

    private Task Start(string role, StationDto station) => shell.Run(async () =>
    {
        BeginAssistant(role, station);
        await IssueCode();
        await LoadAsync();
    });

    // Solo con el codigo caducado o sin solicitud reconocida: el mismo asistente, otro codigo.
    private bool CanNewCode() => shell.Writable && shell.IsMain && HasAssistant && Recognised is null;
    [RelayCommand(CanExecute = nameof(CanNewCode))]
    private Task NewCode() => shell.Run(async () => { await IssueCode(); await LoadAsync(); });

    private async Task IssueCode()
    {
        var issued = await shell.Api!.PostRawAsync("auth/pairings", new { });
        Issued(issued.GetProperty("pairingId").GetString()!, issued.GetProperty("code").GetString()!, issued.GetProperty("expiresAt").GetDateTimeOffset());
        StartPolling();
    }

    // Sin red (los checks): la intencion del asistente y, despues, el codigo emitido con su pairingId.
    public void BeginAssistant(string role, StationDto station)
    {
        IntentRole = role; IntentStationId = station.Id; IntentStationName = station.Name;
        PairingId = ""; PairingCode = ""; codeExpiresAt = null; Recognised = null; DuplicateWarning = "";
        AssistantTitle = role == "service" ? $"Conectar comandero · {station.Name}" : $"Conectar pantalla de cocina · {station.Name}";
        AssistantText = "Generando el código…";
        Sync();
    }
    public void Issued(string issuedPairingId, string code, DateTimeOffset expiresAt)
    {
        PairingId = issuedPairingId; codeExpiresAt = expiresAt; PairingCode = code;
        AssistantText = $"Lee el QR con la cámara del dispositivo (o teclea el código), escribe su nombre y pulsa «Solicitar». La solicitud aparecerá aquí sola. El código caduca a las {expiresAt.ToLocalTime():HH:mm:ss} y sirve una vez.";
        Sync();
    }

    private void StartPolling()
    {
        poll ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        poll.Tick -= Tick; poll.Tick += Tick; poll.Start();
    }

    // F7: la solicitud de la tablet aparece sola, sin pulsar Actualizar. Lectura ligera, fuera de Run (no bloquea la ventana).
    private async void Tick(object? sender, EventArgs e)
    {
        if (shell.Api is null || !HasAssistant) { poll?.Stop(); return; }
        if (shell.Busy) return;
        if (codeExpiresAt is { } expires && DateTimeOffset.UtcNow >= expires && Recognised is null) { ExpireCode(); return; }
        try
        {
            var pendingRows = await shell.Api.GetAsync<PairingPendingDto[]>("auth/pairings/pending");
            if (shell.Busy || !HasAssistant) return;
            Render(knownStations, pendingRows, Devices);
        }
        catch (Exception error) when (error is ApiError or System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
    }

    private void ExpireCode()
    {
        if (codeExpiresAt is not { } expires || DateTimeOffset.UtcNow < expires) return;
        codeExpiresAt = null; PairingCode = "";
        if (HasAssistant && Recognised is null) AssistantText = "El código caducó sin que ningún dispositivo lo usara. Genera otro código o cierra el asistente.";
    }

    private bool CanApprove() => shell.Writable && shell.IsMain && Recognised is not null;
    [RelayCommand(CanExecute = nameof(CanApprove))]
    private Task Approve() => shell.Run(async () =>
    {
        var request = Recognised!;
        await shell.Api!.PostRawAsync("auth/pairings/" + ApiClient.Segment(request.PairingId) + "/approve", new { role = IntentRole, station = IntentStationId });
        shell.Status = $"\"{request.DeviceName}\" conectado como {IntentLabel}. En unos segundos entra solo en su pantalla.";
        CloseAssistant();
        await LoadAsync();
    });

    private bool CanDeny() => shell.Writable && shell.IsMain && SelectedPending is not null;
    [RelayCommand(CanExecute = nameof(CanDeny))]
    private Task Deny() => shell.Run(async () =>
    {
        await shell.Api!.PostRawAsync("auth/pairings/" + ApiClient.Segment(SelectedPending!.PairingId) + "/deny", new { });
        shell.Status = $"Solicitud de \"{SelectedPending.DeviceName}\" denegada.";
        await LoadAsync();
    });

    private bool CanCancel() => HasAssistant && !shell.Busy;
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CloseAssistant();
        shell.Status = "Asistente cerrado. Si el dispositivo ya envió su solicitud, aparece en «Solicitudes recibidas» y solo puede denegarse: abre el asistente y genera otro código.";
    }

    private void CloseAssistant()
    {
        poll?.Stop();
        AssistantTitle = ""; AssistantText = ""; IntentRole = ""; IntentStationId = ""; IntentStationName = "";
        PairingId = ""; PairingCode = ""; codeExpiresAt = null; Recognised = null; DuplicateWarning = "";
        Sync();
    }

    // ---- Quitar (revocacion con expulsion inmediata, D4.2) en dos pasos ----
    private bool CanAskRemove() => shell.Writable && shell.IsMain && SelectedRow is { Device.Active: true } && !ConfirmingRemoval;
    [RelayCommand(CanExecute = nameof(CanAskRemove))]
    private void AskRemove() { RemovalCandidate = SelectedRow; Sync(); }

    private bool CanConfirmRemove() => shell.Writable && shell.IsMain && RemovalCandidate is not null;
    [RelayCommand(CanExecute = nameof(CanConfirmRemove))]
    private Task ConfirmRemove() => shell.Run(async () =>
    {
        var row = RemovalCandidate!; RemovalCandidate = null;
        await shell.Api!.PostRawAsync("auth/devices/" + ApiClient.Segment(row.Device.Id) + "/revoke", new { });
        shell.Status = $"\"{row.Device.Name}\" quitado: expulsado también del tiempo real. Para volver a usarlo, conéctalo de nuevo.";
        await LoadAsync();
    });

    private bool CanCancelRemove() => ConfirmingRemoval;
    [RelayCommand(CanExecute = nameof(CanCancelRemove))]
    private void CancelRemove() { RemovalCandidate = null; Sync(); }
}
