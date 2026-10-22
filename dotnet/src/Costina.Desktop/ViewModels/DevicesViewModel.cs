using System.IO;
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
// C3c (plan 2026-10-22, seccion 4.1): los dos asistentes son «Conectar Costiña Sala · <estacion>» (el movil de cada camarero) y
// «Conectar Costiña Cocina · <estacion>» (la tablet fija de cada estacion), cada uno con su desplegable de estaciones. El QR abre la
// aplicacion de la intencion del asistente (PairingLink.Build con AppFor(IntentRole): /app/sala/ o /app/cocina/), que se conoce antes
// de emitir el codigo. El asistente pide SIEMPRE el codigo corto ({typed:true}), que se ensena en dos grupos junto al QR para teclearlo
// en el icono de un iPhone o un iPad; un motor anterior no lee el cuerpo y devuelve el largo: se ensena el que llegue, tal cual. Junto
// al QR, los dos caminos: Android (el QR abre la aplicacion, se empareja alli y se anade el icono) e iPhone y iPad (Safari no empareja:
// se anade el icono, se abre y se teclea el codigo). La lista agrupa los dispositivos por aplicacion.
public sealed partial class DevicesViewModel(ShellViewModel shell) : ObservableObject
{
    // Fila de la lista: el dispositivo, su tipo con la estacion por su nombre y si es este mismo PC (corte B).
    // C2b: la fila de Inicio en tarjeta (icono, nombre, tipo y estacion; a la derecha, el punto y la palabra de la conexion) y una tercera
    // linea con quien lo aprobo y cuando. Text es la fila entera en una linea (nombre accesible de la tarjeta).
    // C3c: la lista se agrupa por aplicacion (Group: «Costiña Sala», «Costiña Cocina», el principal antiguo o un tipo desconocido tal
    // cual) y, bajo su grupo, la tarjeta dice solo la estacion por su nombre (Place); Kind sigue entero en Text y en Inicio.
    public sealed class DeviceRow(DeviceRowDto device, string kind, string place, bool isThisPc)
    {
        public DeviceRowDto Device { get; } = device;
        public string Kind { get; } = kind;
        public string Group => Labels.DeviceRole(Device.Role);
        public string Place { get; } = place;
        public bool IsThisPc { get; } = isThisPc;
        public string Name => Device.Name;
        public string State => Device.StateLabel;
        public string Approved => (Labels.Actor(Device.ApprovedBy) is { Length: > 0 } actor ? "Aprobado por " + actor + " el " : "Aprobado el ")
            + Device.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        public string Text => (IsThisPc ? "Este PC · " : "") + $"{Device.Name} · {Kind} · {Device.StateLabel} · " + char.ToLowerInvariant(Approved[0]) + Approved[1..];
        // C1 (revision): punto de presencia junto al texto (que ya lo dice con palabras): verde con conexion, gris sin ella o quitado.
        public string PresenceDot => Device.Active && Device.Online ? "ok" : "off";
        public bool Screen => Device.Role == "kitchen";   // icono de pantalla; Costina Sala (y cualquier otro), el de movil
        public bool Removed => !Device.Active;
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
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NeedsRoomStation), nameof(ChoosingText))] private StationDto[] roomStations = [];   // sala o barra: donde va Costina Sala
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NeedsKitchenStation), nameof(ChoosingText), nameof(KitchenPurpose))] private StationDto[] kitchenStations = [];   // cocina, pase o barra: Costina Cocina
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmptyConnect), nameof(EmptyConnectText), nameof(WaiterConnectText))] private StationDto? selectedRoomStation;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(KitchenConnectText))] private StationDto? selectedKitchenStation;
    [ObservableProperty] private PairingPendingDto[] pending = [];
    [ObservableProperty] private PairingPendingDto? selectedPending;
    [ObservableProperty] private DeviceRowDto[] devices = [];
    [ObservableProperty] private DeviceRow[] rows = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RemoveText))] private DeviceRow? selectedRow;
    [ObservableProperty] private bool showRevoked;
    public bool NeedsRoomStation => RoomStations.Length == 0;
    public bool NeedsKitchenStation => KitchenStations.Length == 0;

    // C3c: las dos aplicaciones de los dispositivos, con su nombre y su boton, que dice la estacion elegida en su desplegable.
    public static readonly string SalaName = Labels.DeviceRole("service"), CocinaName = Labels.DeviceRole("kitchen");
    public string WaiterConnectText => ConnectText(SalaName, SelectedRoomStation);
    public string KitchenConnectText => ConnectText(CocinaName, SelectedKitchenStation);
    private static string ConnectText(string app, StationDto? station) => "Conectar " + app + (station is null ? "" : " · " + station.Name);
    public string KitchenPurpose => KitchenStations.Length == 0 ? "Una tablet fija en cada estación de cocina, pase o barra."
        : "Una tablet fija en cada estación: " + Listed([.. KitchenStations.Select(s => s.Name)]) + ".";
    private static string Listed(string[] items) => items.Length <= 1 ? string.Concat(items) : string.Join(", ", items[..^1]) + " y " + items[^1];
    // C2b: cabecera de pagina, cabecera de la lista y vacio explicado (patron comun de las vistas de gestion). Sin leer, nada se supone.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmptyConnect))] private bool devicesRead;
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string onlineText = "";
    [ObservableProperty] private string revokedToggleText = "";
    [ObservableProperty] private bool hasRemoved;
    [ObservableProperty] private string legacyWarning = "";
    [ObservableProperty] private bool showListEmpty = true;
    [ObservableProperty] private string listEmptyHeading = "Sin leer todavía";
    [ObservableProperty] private string listEmptyText = "Los dispositivos aparecerán aquí al leerlos del servidor.";
    // El boton del vacio abre el asistente de siempre (StartWaiter) con la estacion de sala elegida a la izquierda, que dice su nombre.
    public bool ShowEmptyConnect => DevicesRead && SelectedRoomStation is not null;
    public string EmptyConnectText => SelectedRoomStation is not null ? WaiterConnectText : "";
    // El boton de quitar dice a quien quita; sin nada elegido, que elegir.
    public string RemoveText => SelectedRow is not { } row ? "Elige un dispositivo para quitarlo"
        : row.Device.Active ? $"Quitar «{row.Name}»" : $"«{row.Name}» ya está quitado";

    // Asistente en curso (intencion ligada al pairingId emitido)
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAssistant), nameof(ShowWaiterSection), nameof(ShowKitchenSection))] private string assistantTitle = "";
    [ObservableProperty] private string assistantText = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IntentAppName), nameof(AndroidSteps), nameof(AppleSteps))] private string intentRole = "";
    [ObservableProperty] private string intentStationId = "";
    [ObservableProperty] private string intentStationName = "";
    [ObservableProperty] private string pairingId = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PairingCodeText), nameof(ShortCodeShown), nameof(HasCode))] private string pairingCode = "";
    // C3c: el codigo como se teclea: el corto en dos grupos («K7M2Q 9XR4T»); el largo de un motor anterior, tal cual (PairingLink).
    public string PairingCodeText => PairingLink.DisplayCode(PairingCode);
    public bool ShortCodeShown => PairingLink.IsShortCode(PairingCode);
    public bool HasCode => PairingCode.Length > 0;
    // C3c: la aplicacion del asistente abierto, la que abre su QR (sin intencion conocida, ninguna: el enlace va al enrutador /app/).
    public PwaApp? IntentApp => PairingLink.AppFor(IntentRole);
    public string IntentAppName => IntentApp is null ? "la aplicación" : Labels.DeviceRole(IntentRole);
    // C3c: los dos caminos de instalacion junto al QR, con las palabras de los botones de la PWA. Android: el QR abre la aplicacion, que
    // empareja alli mismo, y despues se anade su icono. iPhone y iPad: la camara abre Safari, que NO empareja (lo que guarda Safari no lo
    // ve el icono): se anade el icono, se abre y se teclea el codigo corto.
    // Revision C3c: en Android el icono sale del navegador que abrio el QR (en un Samsung puede ser Samsung Internet): es donde quedo la
    // credencial. En iOS 26 Compartir puede estar dentro de «•••», y la hoja trae «Abrir como app web», que tiene que quedar activado.
    public string AndroidSteps => $"Lee el QR con la cámara → «Solicitar emparejamiento» → «Aprobar» aquí. Después, en el menú del navegador que abrió el QR (Chrome o Samsung Internet), «Añadir a pantalla de inicio»: queda el icono de {IntentAppName}.";
    public string AppleSteps => $"Lee el QR: Safari no empareja y explica cómo añadir el icono (Compartir, o ••• › Compartir › Añadir a pantalla de inicio, con «Abrir como app web» activado). Abre el icono de {IntentAppName} → teclea el código → «Solicitar emparejamiento» → «Aprobar» aquí.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasRecognised))] [NotifyPropertyChangedFor(nameof(RecognisedText))] private PairingPendingDto? recognised;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))] private string duplicateWarning = "";
    public bool HasAssistant => AssistantTitle.Length > 0;
    public bool HasRecognised => Recognised is not null;

    // C2: Inicio pide abrir un asistente concreto ("Conectar Costina Sala" / "Conectar Costina Cocina"). La peticion se guarda y se atiende en la
    // siguiente lectura de esta vista (al abrirla): con UNA sola estacion posible el asistente arranca y emite su codigo; con varias (o
    // ninguna) se abre en su primer paso, elegir la estacion, con la otra seccion recogida. La estacion la elige siempre el responsable.
    private string? requestedIntent;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChoosingStation), nameof(ChoosingText), nameof(ShowWaiterSection), nameof(ShowKitchenSection))]
    private string choosingIntent = "";
    public bool ChoosingStation => ChoosingIntent.Length > 0;
    public bool ShowWaiterSection => !HasAssistant && ChoosingIntent != "kitchen";
    public bool ShowKitchenSection => !HasAssistant && ChoosingIntent != "service";
    public string ChoosingText => ChoosingIntent switch
    {
        "service" when NeedsRoomStation => $"Conectar {SalaName}: no hay ninguna estación de sala donde ponerla. Crea la estación «Sala» y conecta el móvil después.",
        "service" => $"Conectar {SalaName}: elige la estación de sala del móvil y pulsa «Conectar {SalaName}».",
        "kitchen" when NeedsKitchenStation => $"Conectar {CocinaName}: no hay ninguna estación de cocina, pase o barra activa. Créala en Salas, mesas y estaciones.",
        "kitchen" => $"Conectar {CocinaName}: elige la estación donde va a estar la tablet y pulsa «Conectar {CocinaName}».",
        _ => ""
    };
    internal void Request(string? role) => requestedIntent = role;   // null = olvidar la peticion (se cancelo la entrada del responsable)
    public bool HasDuplicateWarning => DuplicateWarning.Length > 0;
    public string IntentLabel => $"{IntentAppName} · {IntentStationName}";
    public string RecognisedText => Recognised is null ? "" : $"«{Recognised.DeviceName}» pide conectarse como {IntentLabel}. Comprueba que es el dispositivo que tienes delante y aprueba.";

    // QR del enlace https://EQUIPO:PUERTO/app/<aplicacion>/#pair=CODIGO (D6.5; C3c: la aplicacion del asistente)
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
    public string RemovalText => RemovalCandidate is null ? "" : $"¿Quitar «{RemovalCandidate.Device.Name}»? Dejará de funcionar en el acto y habrá que conectarlo de nuevo"
        + (RemovalCandidate.IsThisPc ? ". ES ESTE PC: tendrás que volver a conectarlo como responsable" : "") + ".";

    public bool Visible { get; set; }

    internal void Sync()
    {
        StartWaiterCommand.NotifyCanExecuteChanged(); StartKitchenCommand.NotifyCanExecuteChanged(); CreateRoomStationCommand.NotifyCanExecuteChanged();
        NewCodeCommand.NotifyCanExecuteChanged(); ApproveCommand.NotifyCanExecuteChanged(); DenyCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        AskRemoveCommand.NotifyCanExecuteChanged(); ConfirmRemoveCommand.NotifyCanExecuteChanged(); CancelRemoveCommand.NotifyCanExecuteChanged();
        KeepThisPcConnectedCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(ThisPcText));
        AskEnableSimpleCommand.NotifyCanExecuteChanged(); ConfirmEnableSimpleCommand.NotifyCanExecuteChanged(); CancelEnableSimpleCommand.NotifyCanExecuteChanged();
        DisableSimpleCommand.NotifyCanExecuteChanged(); CancelChoosingCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        poll?.Stop();
        rendering = true;
        try
        {
            Stations = []; knownStations = []; stationNames = Names.Empty; RoomStations = []; KitchenStations = []; SelectedRoomStation = null; SelectedKitchenStation = null;
            Pending = []; SelectedPending = null;
            Devices = []; DevicesRead = false; Rows = []; SelectedRow = null; RemovalCandidate = null; RebuildRows();
            AssistantTitle = ""; AssistantText = ""; IntentRole = ""; IntentStationId = ""; IntentStationName = ""; PairingId = ""; PairingCode = "";
            Recognised = null; DuplicateWarning = ""; codeExpiresAt = null; addressSuggested = false; DeviceAddress = ""; PairingQr = null; PairingUrl = ""; QrInfo = QrIdle;
            Network = null; ConfirmingSimple = false; requestedIntent = null; ChoosingIntent = "";
        }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        // C2: el asistente pedido desde Inicio se consume ANTES de leer: si una lectura falla no queda para despues (otra visita o una
        // actualizacion lo abririan solo y emitirian un codigo que nadie ha pedido en ese momento); se vuelve a pulsar.
        var intent = requestedIntent; requestedIntent = null;
        await ReadNetworkAsync();
        if (!addressSuggested) await SuggestAddressAsync();
        var pendingRows = await shell.Api.GetAsync<PairingPendingDto[]>("auth/pairings/pending");
        var deviceRows = await shell.Api.GetAsync<DeviceRowDto[]>("auth/devices");
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        Render(organization.Stations, pendingRows, deviceRows);
        if (intent is { } role) await OpenRequested(role);
    }

    // C2: el asistente que pidio Inicio, con las estaciones recien leidas. Corre dentro de la lectura (shell.Run): no pasa otra vez por
    // Run, asi que repite aqui las mismas condiciones que la habilitacion de los botones (responsable, sin orden pendiente ni ilegible).
    private async Task OpenRequested(string role)
    {
        if (HasAssistant) { shell.Status = "Ya hay un asistente abierto: termínalo o ciérralo antes de conectar otro dispositivo."; return; }
        var choices = role == "service" ? RoomStations : KitchenStations;
        if (choices.Length == 1 && shell.IsMain && shell.Pending is null && shell.Blocked is null)
        {
            if (role == "service") SelectedRoomStation = choices[0];
            BeginAssistant(role, choices[0]);
            await IssueCode();
            return;
        }
        ChoosingIntent = role;
        Sync();
    }
    private bool CanCancelChoosing() => ChoosingStation;
    [RelayCommand(CanExecute = nameof(CanCancelChoosing))]
    private void CancelChoosing() { ChoosingIntent = ""; Sync(); }

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
            // C3c: la estacion de Costina Cocina se elige en su desplegable, como la de sala, con la misma regla.
            if (SelectedKitchenStation is null || KitchenStations.All(s => s.Id != SelectedKitchenStation.Id)) SelectedKitchenStation = KitchenStations.FirstOrDefault();
            ExpireCode();
            var previous = SelectedPending?.PairingId;
            Pending = pendingRows; SelectedPending = pendingRows.FirstOrDefault(p => p.PairingId == previous) ?? pendingRows.FirstOrDefault();
            Devices = deviceRows; DevicesRead = true;
            // Solo la solicitud del asistente abierto se reconoce (y se puede aprobar): la intencion vive aqui, ligada al pairingId.
            Recognised = PairingId.Length == 0 ? null : pendingRows.FirstOrDefault(p => p.PairingId == PairingId);
            DuplicateWarning = Recognised is { } request && deviceRows.Any(d => d.Active && string.Equals(d.Name, request.DeviceName, StringComparison.OrdinalIgnoreCase))
                ? $"Ya hay un dispositivo activo llamado «{request.DeviceName}». Dos con el mismo nombre comparten identidad en la auditoría: quita el antiguo antes o pide otro nombre en el dispositivo."
                : "";
            RebuildRows();
        }
        finally { rendering = false; }
        Sync();
    }

    // C2b: cada tarjeta dice su tipo y su estacion por su nombre (configuracion leida; sin nombre, su codigo; un tipo desconocido, tal
    // cual), como la lista de Inicio. Orden, el de Inicio: Costina Sala, Costina Cocina y el resto; dentro, por nombre. La eleccion se
    // conserva por id (las filas son clases: una fila nueva siempre avisa a la lista). C3c: la vista agrupa las filas por aplicacion
    // (DeviceRow.Group) en ese mismo orden; bajo su grupo, la tarjeta dice la estacion (Place).
    public const string NoStation = "Sin estación asignada";   // como la cabecera de Costina Cocina (frontend/pwa/src/apps.ts, NO_STATION)
    private void RebuildRows()
    {
        var me = shell.Session?.Actor;   // "device:<nombre>" cuando este PC trabaja como puesto (corte B)
        string Kind(DeviceRowDto d) => d.Role is "service" or "kitchen" && d.Station.Length > 0
            ? Labels.DeviceRole(d.Role) + " · " + stationNames.Station(d.Station)
            : Labels.DeviceRole(d.Role);
        string Place(DeviceRowDto d) => d.Station.Length > 0 ? stationNames.Station(d.Station) : NoStation;
        var selected = SelectedRow?.Device.Id;
        Rows = Devices.Where(d => ShowRevoked || d.Active)
            .OrderBy(d => d.Role switch { "service" => 0, "kitchen" => 1, _ => 2 }).ThenBy(d => Labels.DeviceRole(d.Role), StringComparer.Ordinal)
            .ThenBy(d => d.Name, StringComparer.Ordinal).ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => new DeviceRow(d, Kind(d), Place(d), me is not null && me == "device:" + d.Name)).ToArray();
        SelectedRow = Rows.FirstOrDefault(r => r.Device.Id == selected);
        if (RemovalCandidate is { } candidate && Rows.All(r => r.Device.Id != candidate.Device.Id)) RemovalCandidate = null;
        var active = Devices.Where(d => d.Active).ToArray();
        var removed = Devices.Length - active.Length;
        int service = active.Count(d => d.Role == "service"), kitchen = active.Count(d => d.Role == "kitchen"), legacy = active.Count(d => d.Role == "main");
        var other = active.Length - service - kitchen - legacy;
        static string Count(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] kinds = [.. new[] { (service, Count(service) + " en " + SalaName), (kitchen, Count(kitchen) + " en " + CocinaName),
                (legacy, Labels.Plural(legacy, "principal (antiguo)", "principales (antiguos)")), (other, Count(other) + " de otro tipo") }
            .Where(k => k.Item1 > 0).Select(k => k.Item2)];
        var kindsText = Listed(kinds);
        var removedText = removed == 0 ? "" : Labels.Plural(removed, "quitado", "quitados");
        Summary = !DevicesRead ? ""
            : Devices.Length == 0 ? "Aún no hay dispositivos: conecta el primero con el asistente de «Conectar un dispositivo»."
            : active.Length == 0 ? "Ningún dispositivo activo · " + removedText
            : $"{Labels.Plural(active.Length, "dispositivo activo", "dispositivos activos")}: {kindsText}" + (removed > 0 ? " · " + removedText : "");
        OnlineText = active.Length == 0 ? "" : $"{active.Count(d => d.Online)} de {active.Length} con conexión";
        HasRemoved = removed > 0;
        RevokedToggleText = $"Mostrar también los quitados ({removed.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        LegacyWarning = legacy == 0 ? ""
            : legacy == 1 ? $"1 dispositivo antiguo con rol principal: conviene quitarlo y conectarlo de nuevo con {SalaName} o {CocinaName}."
            : $"{legacy} dispositivos antiguos con rol principal: conviene quitarlos y conectarlos de nuevo con {SalaName} o {CocinaName}.";
        ShowListEmpty = Rows.Length == 0;
        (ListEmptyHeading, ListEmptyText) = !DevicesRead ? ("Sin leer todavía", "Los dispositivos aparecerán aquí al leerlos del servidor.")
            : Devices.Length == 0 ? ("Aún no hay dispositivos", $"Conecta el móvil de cada camarero con {SalaName} y la tablet de cada estación con {CocinaName}: cada uno, con un código de un solo uso.")
            : ("Ningún dispositivo activo", removed == 1 ? "El único dispositivo está quitado: marca «Mostrar también los quitados» para verlo."
                : $"Los {removed.ToString(System.Globalization.CultureInfo.InvariantCulture)} dispositivos están quitados: marca «Mostrar también los quitados» para verlos.");
    }
    partial void OnShowRevokedChanged(bool value) { if (!rendering) RebuildRows(); }
    partial void OnSelectedRowChanged(DeviceRow? value) { if (!rendering) Sync(); }
    partial void OnSelectedPendingChanged(PairingPendingDto? value) { if (!rendering) Sync(); }
    partial void OnSelectedRoomStationChanged(StationDto? value) { if (!rendering) Sync(); }
    partial void OnSelectedKitchenStationChanged(StationDto? value) { if (!rendering) Sync(); }

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
    public bool SimpleAvailable => SimpleAvailableIn(Network);
    public bool SimpleOn => SimpleOnIn(Network);
    public bool SimpleOff => SimpleAvailable && !Network!.LanHttp.Enabled;
    public bool OfferSimple => SimpleOff && !ConfirmingSimple;   // mientras se confirma, solo la barra de confirmacion
    public string SimpleText => SimpleConnectionText(Network);
    public string NetworkWarning => NetworkWarningText(Network);
    public bool HasNetworkWarning => NetworkWarning.Length > 0;
    private string? SimpleAddressNow() => SimpleAddress(Network);

    // C2: textos puros de la red del servidor, los mismos aqui y en los avisos de Inicio. Sin conexion sencilla en el servidor (lectura
    // nula, servidor anterior o puerto sin escuchar) no se dice nada de ella.
    public static bool SimpleAvailableIn(NetworkDto? network) => network?.LanHttp is { Listening: true, Port: not null };
    public static bool SimpleOnIn(NetworkDto? network) => SimpleAvailableIn(network) && network!.LanHttp.Enabled;
    public static string? SimpleAddress(NetworkDto? network) =>
        SimpleOnIn(network) && network!.SuggestedIp is { } ip ? PairingLink.SimpleAddress(ip, network.LanHttp.Port!.Value) : null;
    public static string SimpleConnectionText(NetworkDto? network) => !SimpleAvailableIn(network) ? ""
        : SimpleOnIn(network) ? (SimpleAddress(network) is { } address
            ? $"Conexión sencilla activa: móviles y tablets entran por {address} con solo leer el QR, sin ningún certificado. El tráfico viaja sin cifrar por la red del local: úsala solo con el Wi-Fi del personal, nunca con el de los clientes."
            : "Conexión sencilla activa, pero no se encuentra la IP de este servidor en la red del local: comprueba que está conectado por cable o Wi-Fi.")
        : "Para conectar móviles y tablets sin instalar ningún certificado, activa la conexión sencilla: entran por la IP de este PC con solo leer el QR.";
    public static string NetworkWarningText(NetworkDto? network)
    {
        var exposed = network?.PublicNetworks ?? [];
        if (exposed.Length == 0) return "";
        var names = string.Join(", ", exposed.Select(n => "«" + n.Name + "»"));
        return $"La red {names} del servidor está marcada como «Pública» en Windows: así ningún móvil ni tablet puede llegar a él. En el PC servidor, abre Configuración › Red e Internet, entra en la conexión y cambia «Tipo de perfil de red» a «Privada» (solo si es la red de confianza del local).";
    }

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
    partial void OnIntentRoleChanged(string value) => RefreshQr();

    // C3c: el QR abre la aplicacion del asistente (IntentApp). El texto ya no dice «No hay que instalar nada»: en los dos caminos se
    // anade el icono de la aplicacion (AndroidSteps y AppleSteps, junto al QR); aqui solo lo que depende de la direccion.
    private void RefreshQr()
    {
        if (rendering) return;
        if (PairingCode.Length == 0) { PairingQr = null; PairingUrl = ""; QrInfo = QrIdle; return; }
        try
        {
            var address = PairingLink.ParseDeviceAddress(DeviceAddress, SimpleOn);
            var url = PairingLink.Build(address, PairingCode, IntentApp);
            PairingQr = QrImage.Render(url); PairingUrl = url;
            QrInfo = address.Scheme == "http"
                ? "Conexión sencilla: el dispositivo no necesita ningún certificado. En Android, el icono abre una pestaña del navegador con su nombre."
                : "El dispositivo tiene que tener instalado el certificado de este servidor.";
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
        shell.Status = $"Este PC queda conectado como puesto «{name}» en {station.Name}. Al encenderlo entrará solo; al salir del modo responsable seguirá operativo.";
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

    // C3c: como Costina Sala, con la estacion elegida en su desplegable (antes, un boton por estacion).
    private bool CanStartKitchen() => CanStart() && SelectedKitchenStation is not null;
    [RelayCommand(CanExecute = nameof(CanStartKitchen))]
    private Task StartKitchen() => Start("kitchen", SelectedKitchenStation!);

    // Sin ninguna estacion de sala no hay donde poner Costina Sala: se crea "Sala" (tipo sala, auditada al responsable, E2).
    private bool CanCreateRoomStation() => shell.Writable && shell.IsMain && NeedsRoomStation;
    [RelayCommand(CanExecute = nameof(CanCreateRoomStation))]
    private Task CreateRoomStation() => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("organization/commands/station-create", new { id = "sala", name = "Sala", kind = "room", sort = 1 }, "Crear estación Sala");
        shell.Status = $"Estación «Sala» creada (Configuración › Salas, mesas y estaciones). Ya puedes conectar {SalaName}.";
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

    // C3c: el asistente pide SIEMPRE el codigo corto ({typed:true}); el QR lleva ese mismo codigo. Un motor anterior no lee el cuerpo y
    // devuelve el largo: se ensena y se usa el que llegue.
    private async Task IssueCode()
    {
        var issued = await shell.Api!.PostRawAsync("auth/pairings", new { typed = true });
        Issued(issued.GetProperty("pairingId").GetString()!, issued.GetProperty("code").GetString()!, issued.GetProperty("expiresAt").GetDateTimeOffset());
        StartPolling();
    }

    // Sin red (los checks): la intencion del asistente y, despues, el codigo emitido con su pairingId.
    public void BeginAssistant(string role, StationDto station)
    {
        IntentRole = role; IntentStationId = station.Id; IntentStationName = station.Name; ChoosingIntent = "";   // C2: el paso de elegir estacion termina aqui
        PairingId = ""; PairingCode = ""; codeExpiresAt = null; Recognised = null; DuplicateWarning = "";
        AssistantTitle = ConnectText(IntentAppName, station);
        AssistantText = "Generando el código…";
        Sync();
    }
    public void Issued(string issuedPairingId, string code, DateTimeOffset expiresAt)
    {
        PairingId = issuedPairingId; codeExpiresAt = expiresAt; PairingCode = code;
        AssistantText = $"Su solicitud aparece aquí sola: compruébala y apruébala. El código sirve una vez y caduca a las {expiresAt.ToLocalTime():HH:mm:ss}.";
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
        shell.Status = $"«{request.DeviceName}» conectado como {IntentLabel}. En unos segundos entra solo en su pantalla.";
        CloseAssistant();
        await LoadAsync();
    });

    private bool CanDeny() => shell.Writable && shell.IsMain && SelectedPending is not null;
    [RelayCommand(CanExecute = nameof(CanDeny))]
    private Task Deny() => shell.Run(async () =>
    {
        await shell.Api!.PostRawAsync("auth/pairings/" + ApiClient.Segment(SelectedPending!.PairingId) + "/deny", new { });
        shell.Status = $"Solicitud de «{SelectedPending.DeviceName}» denegada.";
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
        shell.Status = $"«{row.Device.Name}» quitado: expulsado también del tiempo real. Para volver a usarlo, conéctalo de nuevo.";
        await LoadAsync();
    });

    private bool CanCancelRemove() => ConfirmingRemoval;
    [RelayCommand(CanExecute = nameof(CanCancelRemove))]
    private void CancelRemove() { RemovalCandidate = null; Sync(); }
}
