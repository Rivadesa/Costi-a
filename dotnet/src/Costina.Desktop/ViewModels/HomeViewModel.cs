using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Inicio (corte A, plan 2026-09-24): la primera vista del ERP al entrar. Se compone SOLO de lo leido: el diagnostico (solo
// principal, D5.2), la lista de dispositivos y la red del servidor (B.2, solo principal). Ninguna regla de negocio; ningun secreto
// en pantalla.
//
// C2 (plan 2026-10-14): Inicio como panel, como el lienzo aprobado (Main.dc.html). Saludo con la fecha y el servicio del dia; cifras
// del comedor (mesas ocupadas, comensales, pases en cocina y alergias por revisar) calculadas de Service.Tiles, es decir, de las MISMAS
// tarjetas del plano del Comedor (FloorPlan): ni una lectura mas ni un cambio en el servidor; "Sala ahora", el plano en pequeno; los
// dispositivos con su conexion (solo el responsable); accesos y avisos. Todo es PRESENTACION: una mesa del mini plano solo abre el
// Comedor y mueve la seleccion de siempre (Service.PickTile: SelectedEntry para una ocupada, con su lectura y su contexto F01, o
// SelectedTable para una libre); "Abrir mesa" es el boton de cabecera del Comedor (Service.StartOpen, con la affordance 'open'); los
// asistentes de Dispositivos emiten su codigo con la orden de siempre y en su vista.

// Fila de la lista de dispositivos de Inicio: nombre, tipo con su estacion y la conexion con su palabra (el punto solo acompana).
public sealed record HomeDevice(string Name, string Kind, string State, string Dot, bool Screen);
// Muestra de la leyenda del mini plano: el tono de las tarjetas (codigo de FloorPlan, o "alert") y su palabra.
public sealed record LegendItem(string Tone, string Label);

public sealed partial class HomeViewModel(ShellViewModel shell) : ObservableObject
{
    public static readonly TimeSpan CertificateWarningWindow = TimeSpan.FromDays(30);
    // Servicio del dia con la regla de las ofertas del motor (Offers.LunchUntilHour): comida hasta las 17:00 hora local, cena despues.
    public const int LunchUntilHour = 17;
    // Dispositivos que caben en la tarjeta de Inicio; los demas se cuentan y se ven en Dispositivos.
    public const int DevicesShown = 5;
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    // Leyenda del mini plano: los estados de una mesa en el orden del servicio (solo los que hay en el plano).
    private static readonly string[] LegendOrder = [FloorPlan.Free, FloorPlan.Seated, FloorPlan.Kitchen, FloorPlan.Ready, FloorPlan.Eating,
        FloorPlan.Paused, FloorPlan.Finished, FloorPlan.Cancelled];

    // C1 (revision): una linea corta para quien trabaja (version y donde esta el servidor); la direccion, el modo y los puertos van en
    // el detalle (ayuda emergente de esa linea).
    [ObservableProperty] private string serverText = "";
    [ObservableProperty] private string serverDetail = "";
    [ObservableProperty] private string whoText = "";
    [ObservableProperty] private string backupText = "";
    [ObservableProperty] private bool backupFailed;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCertificateWarning))] private string certificateWarning = "";
    [ObservableProperty] private bool certificateExpired;
    public bool HasCertificateWarning => CertificateWarning.Length > 0;
    public bool Visible { get; set; }

    // ---- C2: cabecera. El saludo nombra a la persona (responsable) o al puesto; la fecha lleva el servicio del dia con comedor. ----
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string dateLine = "";
    // "Abrir mesa" solo existe con comedor y si la sesion trae la affordance 'open' (la misma condicion que en el Comedor).
    public bool CanOpenTables => shell.HasDining && shell.CanOpen;

    // ---- C2: cifras del comedor (de las tarjetas del plano) ----
    [ObservableProperty] private string occupiedText = "0";
    [ObservableProperty] private string tablesText = "de 0";
    [ObservableProperty] private GridLength occupiedShare = new(0, GridUnitType.Star);
    [ObservableProperty] private GridLength freeShare = new(1, GridUnitType.Star);
    [ObservableProperty] private string dinersText = "0";
    [ObservableProperty] private string dinersDetail = "";
    [ObservableProperty] private string kitchenText = "0";
    [ObservableProperty] private string readyText = "";
    [ObservableProperty] private bool hasReady;
    [ObservableProperty] private string reviewText = "0";
    [ObservableProperty] private string reviewDetail = "";
    [ObservableProperty] private bool reviewAlarm;

    // ---- C2: "Sala ahora" (mini plano): las mismas tarjetas del Comedor, con su estado, su palabra y su aviso ----
    [ObservableProperty] private TableTile[] tiles = [];
    [ObservableProperty] private string zonesText = "";
    [ObservableProperty] private LegendItem[] legend = [];
    [ObservableProperty] private string floorEmpty = "";

    // ---- C2: dispositivos con su conexion (solo el responsable: la lista es de /auth/devices) y la conexion sencilla (B.2) ----
    [ObservableProperty] private HomeDevice[] deviceRows = [];
    [ObservableProperty] private string devicesSummary = "";
    [ObservableProperty] private string devicesEmpty = "";
    [ObservableProperty] private string devicesMore = "";
    [ObservableProperty] private string devicesWarning = "";
    [ObservableProperty] private string simpleState = "";
    [ObservableProperty] private string simpleDot = "off";
    [ObservableProperty] private string simpleNotice = "";
    [ObservableProperty] private string networkNotice = "";

    // Las cifras y el mini plano siguen a las tarjetas del Comedor: se rehacen cada vez que el plano se rehace (cada lectura del
    // tablero o de la configuracion y cada minuto) y cuando el tablero pasa a leido o deja de estarlo. La primera pintura ya dice
    // "sin leer" (nunca los ceros de los valores iniciales).
    internal void Watch(ServiceViewModel service)
    {
        service.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(ServiceViewModel.Tiles) or nameof(ServiceViewModel.BoardRead)) RebuildFloor(); };
        RebuildFloor();
    }

    internal void Sync()
    {
        var session = shell.Session;
        // C1: rol y actor con el vocabulario comun (Labels): "Responsable: jefa", "Puesto de sala pc-barra · Sala 1"; sin user:/device:.
        var actor = session is null ? "" : Labels.Actor(session.Actor);
        var station = session?.StationName is { Length: > 0 } s ? " · " + s : "";
        WhoText = session is null ? "" : session.Role switch
        {
            "main" when shell.IsStation => $"{shell.RoleText("main")} {actor}".TrimEnd() + station + ": dispositivo antiguo con rol principal. Conviene quitarlo y conectar este PC de nuevo desde Dispositivos.",
            "main" => (actor.Length > 0 ? $"{Labels.SessionRole("main")}: {actor}" : Labels.SessionRole("main")) + ". Acceso a todos los apartados.",
            "service" => $"{Labels.SessionRole("service")} {actor}".TrimEnd() + station + ": toma comandas. Cobrar, configurar o conectar dispositivos pide la contraseña del responsable.",
            "kitchen" => $"{Labels.SessionRole("kitchen")} {actor}".TrimEnd() + station + ".",
            var role => Labels.SessionRole(role)
        };
        if (session is null) { ServerText = ""; ServerDetail = ""; ClearReads(); }
        else if (ServerText.Length == 0)
        {
            ServerText = $"Servidor {session.ServerVersion} {shell.ServerPlace}" + (session.Demo ? " · DEMOSTRACIÓN · DATOS FICTICIOS" : "");
            ServerDetail = $"Servidor {session.ServerVersion} en {shell.Endpoint}";
        }
        UpdateHeader();
        OnPropertyChanged(nameof(CanOpenTables));
        OpenTableCommand.NotifyCanExecuteChanged(); OpenTileCommand.NotifyCanExecuteChanged(); ConnectDeviceCommand.NotifyCanExecuteChanged();
    }

    internal void Clear() { ServerText = ""; ServerDetail = ""; WhoText = ""; ClearReads(); }

    // Lo leido solo por el responsable (diagnostico, dispositivos y red): fuera al cerrar la sesion o al cambiar de nivel.
    private void ClearReads()
    {
        BackupText = ""; BackupFailed = false; CertificateWarning = ""; CertificateExpired = false;
        DeviceRows = []; DevicesSummary = ""; DevicesEmpty = ""; DevicesMore = ""; DevicesWarning = "";
        ApplyNetwork(null);
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null) return;
        Sync();
        if (!shell.IsMain) return;
        ApplyDiagnostics(await shell.Api.GetAsync<DiagnosticsDto>("diagnostics"));
        ApplyDevices(await shell.Api.GetAsync<DeviceRowDto[]>("auth/devices"));
        // B.2: un servidor anterior no tiene la ruta; sin ella no se dice nada de la conexion sencilla (como en Dispositivos). Una sesion
        // caducada (401) sigue su camino de siempre.
        try { ApplyNetwork(await shell.Api.GetAsync<NetworkDto>("installation/network")); }
        catch (Exception e) when (e is ApiError { Status: not 401 } or HttpRequestException or TaskCanceledException or JsonException) { ApplyNetwork(null); }
    }

    // Pura (los checks la ejercitan sin servidor): el aviso de certificado aparece cuando quedan menos de 30 dias (F10).
    public void ApplyDiagnostics(DiagnosticsDto diagnostics, DateTimeOffset? now = null)
    {
        var moment = now ?? DateTimeOffset.UtcNow;
        ServerText = $"Servidor {diagnostics.Version} {shell.ServerPlace}" + (diagnostics.Mode == "installation" ? "" : " · laboratorio")
            + (shell.Session?.Demo == true ? " · DEMOSTRACIÓN · DATOS FICTICIOS" : "");
        ServerDetail = $"Servidor {diagnostics.Version} ({(diagnostics.Mode == "installation" ? "instalación" : "laboratorio")}{(diagnostics.RunningAsService ? ", servicio de Windows" : "")}) en {shell.Endpoint}"
            + (diagnostics.Tls is { Enabled: true } tls ? $" · HTTPS en la red local por el puerto {tls.Port}" + (tls.Names is { Length: > 0 } ? $" ({tls.Names})" : "") : " · solo en este equipo (sin HTTPS en la red)");
        BackupText = diagnostics.Backup is null ? "" : diagnostics.Backup.Automatic
            ? (diagnostics.Backup.LastSuccessAt is { } at ? $"Última copia de seguridad: {at.ToLocalTime():dd/MM HH:mm}." : "Todavía no hay ninguna copia de seguridad.")
              + (diagnostics.Backup.LastError is { Length: > 0 } e ? $" ÚLTIMO INTENTO FALLIDO: {e}" : "")
            : "Copias de seguridad automáticas desactivadas (laboratorio).";
        BackupFailed = diagnostics.Backup?.LastError is { Length: > 0 };
        var expires = diagnostics.Tls?.ServerCertificateExpiresAt;
        CertificateExpired = expires is { } end && end <= moment;
        CertificateWarning = expires is null ? "" : expires.Value <= moment
            ? $"EL CERTIFICADO DEL SERVIDOR CADUCÓ el {expires.Value.ToLocalTime():dd/MM/yyyy}: tablets y puestos adicionales no pueden conectar. Hay que renovarlo en el equipo servidor (LÉEME de la instalación)."
            : expires.Value - moment < CertificateWarningWindow
            ? $"El certificado del servidor caduca el {expires.Value.ToLocalTime():dd/MM/yyyy} (en {Labels.Plural((int)(expires.Value - moment).TotalDays, "día", "días")}). Renuévalo antes en el equipo servidor (LÉEME de la instalación): si caduca, tablets y puestos adicionales dejan de conectar."
            : "";
    }

    // Pura: la lista de Inicio. Solo los dispositivos activos (los quitados se ven en Dispositivos), comanderos primero; cada uno con su
    // tipo, la estacion por su nombre (configuracion leida; sin nombre, su codigo) y la conexion con palabra.
    public void ApplyDevices(DeviceRowDto[] devices)
    {
        var active = devices.Where(d => d.Active)
            .OrderBy(d => d.Role switch { "service" => 0, "kitchen" => 1, _ => 2 }).ThenBy(d => d.Name, StringComparer.Ordinal).ToArray();
        var legacy = active.Count(d => d.Role == "main");
        DeviceRows = active.Take(DevicesShown).Select(d => new HomeDevice(d.Name, DeviceKind(d), d.StateLabel, d.Online ? "ok" : "off", d.Role == "kitchen")).ToArray();
        DevicesMore = active.Length > DevicesShown ? $"Y {Labels.Plural(active.Length - DevicesShown, "dispositivo más", "dispositivos más")} en Dispositivos." : "";
        DevicesSummary = active.Length == 0 ? "" : $"{active.Count(d => d.Online)} de {active.Length} con conexión";
        DevicesEmpty = active.Length == 0 ? "Ningún dispositivo conectado todavía: conecta comanderos y pantallas de cocina con los botones de abajo." : "";
        DevicesWarning = legacy == 0 ? ""
            : $"{Labels.Plural(legacy, "dispositivo antiguo con rol principal", "dispositivos antiguos con rol principal")}: conviene quitarlos y conectarlos de nuevo desde Dispositivos.";
    }
    private string DeviceKind(DeviceRowDto device) => device.Role is "service" or "kitchen" && device.Station.Length > 0
        ? Labels.DeviceRole(device.Role) + " · " + shell.Names.Station(device.Station)
        : Labels.DeviceRole(device.Role);

    // Pura: la conexion sencilla (B.2) en la tarjeta de dispositivos y en los avisos, con los textos de Dispositivos.
    public void ApplyNetwork(NetworkDto? network)
    {
        var on = DevicesViewModel.SimpleOnIn(network);
        SimpleState = !DevicesViewModel.SimpleAvailableIn(network) ? ""
            : on ? "Conexión sencilla activa" + (DevicesViewModel.SimpleAddress(network) is { } address ? " · " + address : "")
            : "Conexión sencilla desactivada";
        SimpleDot = on ? "warn" : "off";
        SimpleNotice = on ? DevicesViewModel.SimpleConnectionText(network) : "";
        NetworkNotice = DevicesViewModel.NetworkWarningText(network);
    }

    // ---- C2: saludo y fecha (puros: los checks los prueban con horas fijas) ----
    public static string Greeting(DateTime local) => local.Hour switch { < 6 => "Buenas noches", < 14 => "Buenos días", < 21 => "Buenas tardes", _ => "Buenas noches" };
    public static string ServiceOfDay(DateTime local) => local.Hour < LunchUntilHour ? "Servicio de comida" : "Servicio de cena";
    public static string DateText(DateTime local, bool withService)
    {
        var day = local.ToString("dddd, d 'de' MMMM", Spanish);
        return char.ToUpper(day[0], Spanish) + day[1..] + (withService ? " · " + ServiceOfDay(local) : "");
    }
    // Una persona (responsable) se saluda por su nombre con mayuscula inicial; un puesto, por el nombre del dispositivo tal cual.
    private string GreetingName()
    {
        var actor = Labels.Actor(shell.Session?.Actor);
        if (actor.Length == 0) return "";
        return shell.IsResponsible ? char.ToUpper(actor[0], Spanish) + actor[1..] : actor;
    }
    private void UpdateHeader()
    {
        var local = shell.Clock().ToLocalTime().DateTime;
        if (shell.Session is null) { Title = "Inicio"; DateLine = ""; return; }
        var name = GreetingName();
        Title = name.Length > 0 ? $"{Greeting(local)}, {name}" : Greeting(local);
        DateLine = DateText(local, shell.HasDining);
    }

    // ---- C2: cifras y mini plano, de las tarjetas del Comedor ----
    // Sin el tablero leido (antes de la primera lectura, al cambiar de nivel o al desconectar) no se presenta un comedor vacio: las cifras
    // dicen que no hay lectura (nunca "0 de 0" ni "Nada por revisar", que seria un falso negativo sobre alergias).
    public const string NotRead = "—", NotReadDetail = "Sin leer todavía";
    private void RebuildFloor()
    {
        UpdateHeader();   // el saludo y el servicio cambian con la hora (el plano se rehace cada minuto)
        if (!shell.Service.BoardRead)
        {
            OccupiedText = NotRead; TablesText = ""; OccupiedShare = new(0, GridUnitType.Star); FreeShare = new(1, GridUnitType.Star);
            DinersText = NotRead; DinersDetail = ""; KitchenText = NotRead; ReadyText = ""; HasReady = false;
            ReviewText = NotRead; ReviewAlarm = false; ReviewDetail = NotReadDetail;
            Tiles = []; ZonesText = ""; Legend = []; FloorEmpty = NotReadDetail + ": pulsa F5 para leer el comedor.";
            return;
        }
        var all = shell.Service.Tiles;
        var occupied = all.Where(t => t.Occupied).ToArray();
        var dinings = occupied.Select(t => t.Entry!.Service).ToArray();
        Tiles = all;
        OccupiedText = dinings.Length.ToString(CultureInfo.InvariantCulture);
        TablesText = "de " + all.Length.ToString(CultureInfo.InvariantCulture);
        OccupiedShare = new(dinings.Length, GridUnitType.Star);
        FreeShare = new(all.Length - dinings.Length, GridUnitType.Star);
        DinersText = dinings.Sum(d => d.Pax).ToString(CultureInfo.InvariantCulture);
        DinersDetail = dinings.Length == 0 ? "Ninguna mesa ocupada" : OfferMix(dinings, shell.Service.Offers);
        // Pases: en cocina (enviados o en preparacion) y listos para servir, con el mismo criterio que la barra de avance de las tarjetas.
        var courses = dinings.SelectMany(d => d.Courses).ToArray();
        KitchenText = courses.Count(c => FloorPlan.Step(c) == "kitchen").ToString(CultureInfo.InvariantCulture);
        var ready = courses.Count(c => FloorPlan.Step(c) == "ready");
        HasReady = ready > 0;
        ReadyText = ready == 0 ? "Ninguno listo para servir" : Labels.Plural(ready, "listo para servir", "listos para servir");
        // Alergias por revisar: mesas con un cambio de restricciones que cocina aun no ha revisado. Siempre con texto: la primera mesa y
        // su restriccion mas grave (desconocida = alarma, tal cual).
        var review = occupied.Where(t => t.Entry!.Service.RestrictionsPendingAck).ToArray();
        ReviewText = review.Length.ToString(CultureInfo.InvariantCulture);
        ReviewAlarm = review.Length > 0;
        ReviewDetail = ReviewSummary(review, occupied);
        ZonesText = string.Join(" · ", all.Select(t => t.ZoneName).Distinct(StringComparer.Ordinal));
        var tones = all.Select(t => t.Tone).ToHashSet(StringComparer.Ordinal);
        var legend = LegendOrder.Where(tones.Contains).Select(s => new LegendItem(s, Labels.TableState(s))).ToList();
        // El borde rojo solo lo llevan las mesas con aviso ALARMANTE (FloorPlan.AlertIsAlarming); una restriccion leve o moderada lleva su
        // pastilla ambar con su palabra y ningun borde: la leyenda no promete un rojo que no se pinta.
        if (all.Any(t => t.AlertAlarming)) legend.Add(new("alert", "Alergia, restricción grave o por revisar"));
        if (!legend.SequenceEqual(Legend)) Legend = [..legend];
        FloorEmpty = all.Length == 0 ? "No hay mesas activas. Créalas en Salas, mesas y estaciones." : "";
    }

    // Ofertas con las que estan abiertas las mesas: "2 degustaciones · 2 menús cerrados · 3 a la carta". Una mesa sin oferta (servidor
    // anterior) o con una que ya no esta vigente se cuenta aparte; un tipo nuevo sale tal cual.
    private static string OfferMix(DiningDto[] dinings, OfferChoice[] offers)
    {
        string KindOf(DiningDto d) => d.OfferId is { } id && offers.FirstOrDefault(o => o.Id == id) is { } offer ? offer.Kind : "";
        static string Key(string kind) => string.Concat(kind.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        static int Order(string kind) => Key(kind) switch { "tasting" => 0, "setmenu" => 1, "alacarte" => 2, "" => 4, _ => 3 };
        static string Count(string kind, int n) => Key(kind) switch
        {
            "tasting" => Labels.Plural(n, "degustación", "degustaciones"),
            "setmenu" => Labels.Plural(n, "menú cerrado", "menús cerrados"),
            "alacarte" => n.ToString(CultureInfo.InvariantCulture) + " a la carta",
            "" => Labels.Plural(n, "mesa con otra oferta", "mesas con otra oferta"),
            _ => n.ToString(CultureInfo.InvariantCulture) + " " + Labels.OfferKind(kind)
        };
        return string.Join(" · ", dinings.GroupBy(KindOf).OrderBy(g => Order(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => Count(g.Key, g.Count())));
    }
    private static string ReviewSummary(TableTile[] review, TableTile[] occupied)
    {
        if (review.Length == 0)
        {
            var reviewed = occupied.Count(t => (t.Entry!.Service.Restrictions ?? []).Length > 0);
            return reviewed == 0 ? "Nada por revisar" : Labels.Plural(reviewed, "mesa con restricciones, ya revisadas", "mesas con restricciones, ya revisadas");
        }
        var first = review[0];
        var restriction = (first.Entry!.Service.Restrictions ?? []).OrderByDescending(r => r.Alarming).FirstOrDefault();
        var what = restriction is null ? "cambio de restricciones sin revisar" : $"{restriction.KindLabel} {restriction.Substance} ({restriction.SeverityLabel})";
        var more = review.Length > 1 ? " · y " + Labels.Plural(review.Length - 1, "mesa más", "mesas más") : "";
        return $"{first.Name} · {what}{more}";
    }

    // ---- C2: acciones de Inicio. Ninguna decide nada: abren la vista y usan la orden o la seleccion de siempre. ----
    // "Abrir mesa": el Comedor con el formulario de abrir mesa (Service.StartOpen, habilitado por la affordance 'open').
    private bool CanOpenTable() => shell.HasDining && shell.TabsEnabled && shell.Service.StartOpenCommand.CanExecute(null);
    [RelayCommand(CanExecute = nameof(CanOpenTable))]
    private void OpenTable()
    {
        shell.OpenView("ServiceTab");
        shell.Service.StartOpenCommand.Execute(null);
    }

    // Una mesa del mini plano abre el Comedor con ESA mesa elegida por el camino de siempre (PickTile): una ocupada se lee y su contexto
    // es el de la lectura (F01); una libre lleva al formulario de abrir mesa.
    private bool CanOpenTile() => shell.HasDining && shell.TabsEnabled;
    [RelayCommand(CanExecute = nameof(CanOpenTile))]
    private void OpenTile(TableTile? tile)
    {
        if (tile is null) return;
        shell.OpenView("ServiceTab");
        shell.Service.PickTileCommand.Execute(tile);
    }

    // "Conectar comandero" / "Pantalla de cocina": Dispositivos con ese asistente (Devices.Request). Es un apartado del responsable: en el
    // nivel Puesto pide antes la contrasena, como el menu.
    private bool CanConnectDevice() => shell.TabsEnabled;
    [RelayCommand(CanExecute = nameof(CanConnectDevice))]
    private void ConnectDevice(string? role)
    {
        if (role is not ("service" or "kitchen")) return;
        shell.Devices.Request(role);
        shell.OpenView("DevicesTab");
    }
}
