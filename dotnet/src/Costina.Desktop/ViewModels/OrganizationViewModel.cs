using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E2: superficie "Configuracion" (solo rol main; el servidor lo impone ademas por permisos): salas y mesas, y estaciones.
// Patron lista + ficha. Cada guardado es UN comando por la tuberia habitual (orden incierta persistida antes de enviar) y
// despues se relee la organizacion completa: la pantalla nunca muestra un estado que el servidor no haya confirmado.
// Nada se borra: se desactiva y se puede reactivar. La validacion local es solo de formato; las reglas las aplica el servidor.
// C2b (plan 2026-10-14): pestanas "Salas y mesas" · "Estaciones" con el patron del Comedor. Salas y mesas: las salas a la izquierda
// (tarjetas con sus mesas activas, sus plazas y su tarifa propia), en el centro las mesas de la sala elegida como tarjetas (nombre, plazas
// y estado con palabra; una desactivada, con borde discontinuo) y a la derecha la ficha de la sala o de la mesa elegida. Estaciones: lista
// y ficha (la estacion Pase, marcada como reservada). Nada de eso decide: cada boton sigue con su orden, su cuerpo y su CanExecute de
// siempre; lo que no se permite lo rechaza el servidor (una mesa en uso, una sala con mesas activas) y su texto sale en el aviso.
public sealed record ZoneRow(ZoneDto Zone, string Detail)
{
    // Tarifa propia de la sala (la General no se nombra: es la de siempre).
    public string? Tariff => Zone.TariffName is { Length: > 0 } tariff ? "Tarifa " + tariff : null;
    public bool Inactive => !Zone.Active;
    public override string ToString() => Zone.ToString();
}
// C2b: tarjeta de una mesa de la sala elegida (la eleccion sigue siendo el DTO: SelectedTable).
public sealed record TableCard(TableDto Table)
{
    public string Seats => Labels.Plural(Table.Capacity, "plaza", "plazas");
    public string StateText => Table.Active ? "Activa" : "Desactivada";
    public string Tone => Table.Active ? "ok" : "other";
    public bool Inactive => !Table.Active;
    public override string ToString() => $"{Table.Name} · {Seats} · {StateText}";
}
// C2b: fila de la lista de estaciones. Reserved = la estacion Pase (existe siempre: ni se desactiva ni cambia de tipo).
public sealed record StationRow(StationDto Station, bool Reserved)
{
    public string Detail => Station.KindLabel + (Reserved ? " · existe siempre" : "");
    public bool Inactive => !Station.Active;
    public override string ToString() => $"{Station.Name} · {Detail}{(Reserved ? " · reservada" : "")}{(Inactive ? " · desactivada" : "")}";
}
// C2b: una sala en el desplegable de la ficha de una mesa (las activas y, si la mesa esta en una desactivada, esa, marcada).
public sealed record ZoneChoice(string Id, string Name);

public sealed partial class OrganizationViewModel(ShellViewModel shell) : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool rendering;
    private OrganizationDto? read;   // null = sin leer
    // C2b: lo recien creado (confirmado por el servidor) queda elegido tras releer; "Cancelar" vuelve a lo elegido antes del alta.
    private string? createdZone, createdTable, createdStation, zoneBeforeNew, tableBeforeNew, stationBeforeNew;

    [ObservableProperty] private ZoneDto[] zones = [];
    [ObservableProperty] private ZoneDto? selectedZone;
    [ObservableProperty] private TableDto[] tables = [];
    [ObservableProperty] private TableDto? selectedTable;
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] private StationDto? selectedStation;
    // C2b: lo que pintan las listas (la eleccion sigue siendo el DTO de arriba).
    [ObservableProperty] private ZoneRow[] zoneItems = [];
    [ObservableProperty] private TableCard[] tableItems = [];
    [ObservableProperty] private StationRow[] stationItems = [];
    // Fichas (formularios). NewX = alta: el codigo se teclea; en edicion el codigo es inmutable.
    [ObservableProperty] private bool newZone; [ObservableProperty] private string zoneId = ""; [ObservableProperty] private string zoneName = ""; [ObservableProperty] private string zoneSort = "0";
    // E3.2: tarifa de la sala. "" = la General (la sala no tiene tarifa propia); null = el desplegable perdio su eleccion al releer
    // y entonces la orden no la toca (el servidor conserva la que tenia). Las opciones son las de la sala que se edita: General, las
    // tarifas activas y, solo si ESA sala usa una desactivada (o una que el servidor no lista), esa tambien, marcada, para no
    // cambiarla sin querer. Otra sala nunca la ofrece: el servidor la rechazaria (tariff_inactive).
    [ObservableProperty] private string? zoneTariffId = "";
    private static readonly TariffDto GeneralChoice = new("", "General", -1, true);
    private TariffDto[] tariffs = [];
    [ObservableProperty] private bool newTable; [ObservableProperty] private string tableId = ""; [ObservableProperty] private string tableName = "";
    // C2b: TableZoneId null = el desplegable de la sala perdio su eleccion: guardar no envia nada (antes reventaba al leer el codigo).
    [ObservableProperty] private string tableCapacity = "4"; [ObservableProperty] private string? tableZoneId = ""; [ObservableProperty] private string tableSort = "0";
    [ObservableProperty] private bool newStation; [ObservableProperty] private string stationId = ""; [ObservableProperty] private string stationName = "";
    [ObservableProperty] private string stationKind = "kitchen"; [ObservableProperty] private string stationSort = "0";
    [ObservableProperty] private string summary = "Sin datos de organización todavía.";

    // ---- C2b: pestanas internas (Segmented); la elegida es Section y Mark la pinta ----
    public SegmentChoice[] Sections { get; } = SectionChoices();
    private static SegmentChoice[] SectionChoices()
    {
        var choices = new[] { new SegmentChoice("zones", "Salas y mesas"), new SegmentChoice("stations", "Estaciones") };
        SegmentChoice.Mark(choices, "zones");
        return choices;
    }
    private string section = "zones";
    // Null o un codigo desconocido (la lista de pestanas perdiendo su eleccion) no cambian de pestana.
    public string? Section
    {
        get => section;
        set
        {
            if (value is null || value == section || !Sections.Any(s => s.Id == value)) return;
            section = value; SegmentChoice.Mark(Sections, value);
            foreach (var name in new[] { nameof(Section), nameof(ShowZones), nameof(ShowStations) }) OnPropertyChanged(name);
        }
    }
    public bool ShowZones => section == "zones";
    public bool ShowStations => section == "stations";

    // ---- C2b: lista de salas ----
    [ObservableProperty] private bool showZoneListEmpty;
    [ObservableProperty] private string zoneListHeading = "";
    [ObservableProperty] private string zoneListText = "";
    [ObservableProperty] private bool showCreateZone;
    // ---- C2b: mesas de la sala elegida (centro) ----
    [ObservableProperty] private bool hasZoneContext;
    [ObservableProperty] private string tablesTitle = "";
    [ObservableProperty] private string tablesSummary = "";
    [ObservableProperty] private bool showTablesEmpty;
    [ObservableProperty] private string tablesEmptyHeading = "";
    [ObservableProperty] private string tablesEmptyText = "";
    [ObservableProperty] private bool showCreateTable;
    // ---- C2b: ficha de la sala o de la mesa elegida ----
    [ObservableProperty] private bool showZoneFicha;
    [ObservableProperty] private bool showTableFicha;
    [ObservableProperty] private bool showZoneEmpty;
    [ObservableProperty] private string zoneEmptyHeading = "";
    [ObservableProperty] private string zoneEmptyText = "";
    [ObservableProperty] private string zoneContext = "";
    [ObservableProperty] private string zoneTitle = "";
    [ObservableProperty] private string zoneStateText = "";
    [ObservableProperty] private string zoneStateTone = "other";
    [ObservableProperty] private string zoneSaveText = "Guardar cambios";
    [ObservableProperty] private bool zoneDeactivates;
    [ObservableProperty] private bool zoneReactivates;
    [ObservableProperty] private string zoneTablesFigure = "";
    [ObservableProperty] private string zoneSeatsFigure = "";
    [ObservableProperty] private string zoneTablesText = "";
    [ObservableProperty] private string tableContext = "";
    [ObservableProperty] private string tableTitle = "";
    [ObservableProperty] private string tableStateText = "";
    [ObservableProperty] private string tableStateTone = "other";
    [ObservableProperty] private string tableSaveText = "Guardar cambios";
    [ObservableProperty] private bool tableDeactivates;
    [ObservableProperty] private bool tableReactivates;
    // ---- C2b: estaciones ----
    [ObservableProperty] private bool showStationListEmpty;
    [ObservableProperty] private string stationListHeading = "";
    [ObservableProperty] private string stationListText = "";
    [ObservableProperty] private bool hasStationContext;
    [ObservableProperty] private string stationEmptyHeading = "";
    [ObservableProperty] private string stationEmptyText = "";
    [ObservableProperty] private string stationContext = "";
    [ObservableProperty] private string stationTitle = "";
    [ObservableProperty] private string stationStateText = "";
    [ObservableProperty] private string stationStateTone = "other";
    [ObservableProperty] private bool isReservedStation;
    [ObservableProperty] private string stationSaveText = "Guardar cambios";
    [ObservableProperty] private bool stationDeactivates;
    [ObservableProperty] private bool stationReactivates;
    [ObservableProperty] private string stationNote = "";

    public bool Visible { get; set; }
    // Desplegables de las fichas. Las listas son ESTABLES (RefreshChoices solo las rehace si cambia su contenido) y, al rehacerlas, el
    // valor elegido se vuelve a dar: un desplegable cuya lista cambia pierde su eleccion (y puede escribir null) y no la recupera si el
    // valor enlazado sigue igual, porque WPF no avisa de un valor que no cambia (la leccion del Catalogo y la Oferta de C2b).
    private TariffDto[] tariffChoices = [GeneralChoice];
    private ZoneChoice[] zoneChoices = [];
    public TariffDto[] TariffChoices => tariffChoices;
    public ZoneChoice[] ZoneChoices => zoneChoices;
    public bool HasZones => Zones.Length > 0;
    public bool HasTables => Tables.Length > 0;
    public bool HasStations => Stations.Length > 0;
    // C1: titulos por el nombre; el codigo solo en el campo "Código" de la ficha.
    public string ZoneFormTitle => NewZone ? "Nueva sala" : SelectedZone is null ? "Selecciona una sala" : "Sala: " + SelectedZone.Name;
    public string TableFormTitle => NewTable ? "Nueva mesa" : SelectedTable is null ? "Selecciona una mesa" : "Mesa: " + SelectedTable.Name;
    public string StationFormTitle => NewStation ? "Nueva estación" : SelectedStation is null ? "Selecciona una estación" : "Estación: " + SelectedStation.Name;
    public bool ZoneEditable => shell.Writable && shell.IsMain && (NewZone || SelectedZone is not null);
    public bool TableEditable => shell.Writable && shell.IsMain && (NewTable || SelectedTable is not null);
    public bool StationEditable => shell.Writable && shell.IsMain && (NewStation || SelectedStation is not null);
    // C2b: lo que hace cada tipo de estacion (ayuda bajo el desplegable Tipo; un tipo que no se conoce sale tal cual).
    public string StationKindHelp => StationKind?.ToLowerInvariant() switch
    {
        "kitchen" => "Cocina: prepara los platos que la llevan; sus elaboraciones salen en su pantalla de cocina.",
        "pass" => "Pase: aquí se validan los pases y se revisan los cambios de restricciones antes de servir.",
        "bar" => "Barra: prepara bebidas y lo que se pide en barra; admite pantallas de cocina y comanderos.",
        "room" => "Sala: la de los comanderos que toman las comandas en una zona del local.",
        null or "" => "",
        var other => "Tipo «" + other + "» del servidor."
    };

    internal void Sync()
    {
        foreach (var name in new[] { nameof(ZoneEditable), nameof(TableEditable), nameof(StationEditable), nameof(ZoneFormTitle), nameof(TableFormTitle), nameof(StationFormTitle),
            nameof(HasZones), nameof(HasTables), nameof(HasStations) })
            OnPropertyChanged(name);
        foreach (var command in new IRelayCommand[] { StartZoneCommand, SaveZoneCommand, ToggleZoneCommand, CancelZoneCommand, StartTableCommand, SaveTableCommand, ToggleTableCommand,
            CancelTableCommand, ShowZoneCommand, StartStationCommand, SaveStationCommand, ToggleStationCommand, CancelStationCommand })
            command.NotifyCanExecuteChanged();
        Compose();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            read = null; Zones = []; Tables = []; Stations = []; ZoneItems = []; TableItems = []; StationItems = []; tariffs = [];
            SelectedZone = null; SelectedTable = null; SelectedStation = null; NewZone = NewTable = NewStation = false;
            createdZone = createdTable = createdStation = zoneBeforeNew = tableBeforeNew = stationBeforeNew = null;
            Summary = "Sin datos de organización todavía."; Section = "zones";
        }
        finally { rendering = false; }
        // C2b: las fichas tambien se vacian (antes quedaba la ultima sala leida en el formulario junto a unas listas vacias).
        FillZoneForm(); FillTableForm(); FillStationForm();
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        Render(organization);
    }

    // Tambien lo usan los checks del WPF sin servidor: la pantalla se compone solo de lo leido.
    public void Render(OrganizationDto organization)
    {
        rendering = true;
        try
        {
            var zone = Take(ref createdZone) ?? SelectedZone?.Id; var table = Take(ref createdTable) ?? SelectedTable?.Id; var station = Take(ref createdStation) ?? SelectedStation?.Id;
            read = organization;
            // E3.2: la lista de salas nombra la tarifa propia de cada sala (la General no se nombra: es la de siempre).
            tariffs = organization.Tariffs ?? [];
            Zones = organization.Zones.Select(z => z with { TariffName = TariffOf(z.TariffId) is { Length: > 0 } own ? tariffs.FirstOrDefault(t => t.Id == own)?.Name ?? own : null }).ToArray();
            Stations = organization.Stations;
            // C2b (F01): la mesa elegida manda en su sala. Si una orden la ha llevado a otra sala (o se acaba de crear en otra), la vista la
            // sigue alli: la ficha nunca queda con una mesa que no es de la sala elegida.
            if (table is not null && Zones.FirstOrDefault(z => z.Tables.Any(t => t.Id == table)) is { } home) zone = home.Id;
            // Una lista rehecha pierde su eleccion y no la recupera si la fila nueva es igual a la de antes: se vuelve a dar, primero vacia.
            ZoneItems = Zones.Select(z => new ZoneRow(z, ZoneDetail(z))).ToArray();
            SelectedZone = null;
            SelectedZone = NewZone ? null : Zones.FirstOrDefault(z => z.Id == zone) ?? Zones.FirstOrDefault(z => z.Active) ?? Zones.FirstOrDefault();
            RenderTables();
            SelectedTable = null;
            SelectedTable = NewTable ? null : Tables.FirstOrDefault(t => t.Id == table);
            StationItems = Stations.Select(s => new StationRow(s, IsReserved(s))).ToArray();
            SelectedStation = null;
            SelectedStation = NewStation ? null : Stations.FirstOrDefault(s => s.Id == station);
            var active = Zones.Where(z => z.Active).SelectMany(z => z.Tables.Where(t => t.Active)).ToArray();
            Summary = Zones.Length == 0 ? "Aún no hay salas: crea la primera con «Nueva sala»."
                : $"{Labels.Plural(Zones.Count(z => z.Active), "sala activa", "salas activas")} · {Labels.Plural(active.Length, "mesa activa", "mesas activas")} ({Labels.Plural(active.Sum(t => t.Capacity), "plaza", "plazas")}) · {Labels.Plural(Stations.Count(s => s.Active), "estación activa", "estaciones activas")}";
        }
        finally { rendering = false; }
        RefreshChoices();
        if (!NewZone) FillZoneForm(); if (!NewTable) FillTableForm(); if (!NewStation) FillStationForm();
        Sync();
    }
    private static string? Take(ref string? created) { var value = created; created = null; return value; }
    private static bool IsReserved(StationDto station) => station.Id == "pase";

    // Opciones de los desplegables para lo que se edita: la tarifa de la sala (una sala nueva: General y las activas) y la sala de la
    // mesa. Se fijan ANTES que el valor: al cambiar la lista el desplegable puede escribir null si su eleccion ya no esta, y despues se pone
    // la buena.
    private void RefreshChoices()
    {
        var own = NewZone ? "" : TariffOf(SelectedZone?.TariffId);
        TariffDto[] active = [GeneralChoice, .. tariffs.Where(t => t.Id != "general" && t.Active)];
        TariffDto[] tariffsFresh = own.Length == 0 || active.Any(t => t.Id == own) ? active
            : [.. active, tariffs.FirstOrDefault(t => t.Id == own) is { } off ? off with { Name = off.Name + " (desactivada)" } : new TariffDto(own, own, int.MaxValue, false)];
        Choices(ref tariffChoices, tariffsFresh, nameof(TariffChoices), () => ZoneTariffId, v => ZoneTariffId = v);
        var home = NewTable ? SelectedZone?.Id : SelectedTable?.ZoneId;
        ZoneChoice[] zonesFresh = [.. Zones.Where(z => z.Active || z.Id == home).Select(z => new ZoneChoice(z.Id, z.Active ? z.Name : z.Name + " (desactivada)"))];
        if (home is { Length: > 0 } && Zones.All(z => z.Id != home)) zonesFresh = [.. zonesFresh, new ZoneChoice(home, home)];   // una sala que no se lista: su codigo, tal cual
        Choices(ref zoneChoices, zonesFresh, nameof(ZoneChoices), () => TableZoneId, v => TableZoneId = v);
    }
    private void Choices<T>(ref T[] field, T[] fresh, string list, Func<string?> get, Action<string?> set)
    {
        if (field.SequenceEqual(fresh)) return;
        var value = get();   // el de antes: al cambiar la lista el desplegable puede escribir null
        field = fresh; OnPropertyChanged(list);
        var was = rendering; rendering = true;
        try { set(null); set(value); }
        finally { rendering = was; }
    }

    private void RenderTables()
    {
        Tables = SelectedZone is null ? [] : SelectedZone.Tables;
        TableItems = Tables.Select(t => new TableCard(t)).ToArray();
    }

    partial void OnSelectedZoneChanged(ZoneDto? value)
    {
        if (rendering) return;
        NewZone = false; NewTable = false;
        rendering = true; try { RenderTables(); SelectedTable = null; } finally { rendering = false; }
        FillZoneForm(); FillTableForm(); Sync();
    }
    partial void OnSelectedTableChanged(TableDto? value) { if (rendering) return; NewTable = false; FillTableForm(); Sync(); }
    partial void OnSelectedStationChanged(StationDto? value) { if (rendering) return; NewStation = false; FillStationForm(); Sync(); }
    partial void OnStationKindChanged(string value) => OnPropertyChanged(nameof(StationKindHelp));

    private void FillZoneForm()
    {
        var z = NewZone ? null : SelectedZone;
        RefreshChoices();   // antes que los valores: la lista ya lleva la eleccion de lo leido
        ZoneId = z?.Id ?? ""; ZoneName = z?.Name ?? ""; ZoneSort = (z?.Sort ?? 0).ToString(); ZoneTariffId = TariffOf(z?.TariffId);
    }
    // Tarifa propia de una sala como codigo; sin tarifa (o la general escrita a mano) = "" (la General).
    private static string TariffOf(string? tariffId) => tariffId is null or "" or "general" ? "" : tariffId;
    private void FillTableForm()
    {
        var t = NewTable ? null : SelectedTable;
        RefreshChoices();
        TableId = t?.Id ?? ""; TableName = t?.Name ?? ""; TableCapacity = (t?.Capacity ?? 4).ToString();
        TableZoneId = t?.ZoneId ?? SelectedZone?.Id ?? ""; TableSort = (t?.Sort ?? 0).ToString();
    }
    private void FillStationForm()
    {
        var s = NewStation ? null : SelectedStation;
        StationId = s?.Id ?? ""; StationName = s?.Name ?? ""; StationSort = (s?.Sort ?? 0).ToString();
        StationKind = s?.Kind.ToLowerInvariant() ?? "kitchen";
    }

    // ---- C2b: composicion de las listas y las fichas (solo presentacion: nada de esto habilita ni envia) ----
    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];
    private static string ZoneDetail(ZoneDto zone)
    {
        var active = zone.Tables.Where(t => t.Active).ToArray();
        return active.Length == 0 ? "Sin mesas activas" : $"{Labels.Plural(active.Length, "mesa activa", "mesas activas")} · {Labels.Plural(active.Sum(t => t.Capacity), "plaza", "plazas")}";
    }
    private string ZoneNameOf(string? id) => Zones.FirstOrDefault(z => z.Id == id)?.Name ?? id ?? "";

    private void Compose()
    {
        var none = read is null;
        var zone = NewZone ? null : SelectedZone;
        var table = NewTable ? null : SelectedTable;
        // Salas.
        ShowZoneListEmpty = ZoneItems.Length == 0;
        (ZoneListHeading, ZoneListText) = none ? ("Sin leer todavía", "Las salas aparecerán aquí al leer la organización del servidor.")
            : ("Aún no hay salas", "Crea la primera sala del local (Sala principal, Terraza…) y después sus mesas.");
        ShowCreateZone = !none && ZoneItems.Length == 0;
        // Mesas de la sala elegida.
        HasZoneContext = zone is not null;
        TablesTitle = zone is null ? "" : "Mesas de " + zone.Name;
        var activeTables = zone?.Tables.Where(t => t.Active).ToArray() ?? [];
        var inactiveTables = zone?.Tables.Where(t => !t.Active).ToArray() ?? [];
        TablesSummary = zone is null ? "" : (activeTables.Length == 0 ? "Ninguna mesa activa" : $"{Labels.Plural(activeTables.Length, "mesa activa", "mesas activas")} · {Labels.Plural(activeTables.Sum(t => t.Capacity), "plaza", "plazas")}")
            + (inactiveTables.Length == 0 ? "" : " · " + Labels.Plural(inactiveTables.Length, "desactivada", "desactivadas"));
        // Sin leer, el centro calla (la lista y la ficha ya lo dicen); sin ninguna sala, dice que pondra (la lista ya ofrece crear la primera).
        ShowTablesEmpty = TableItems.Length == 0 && !none;
        (TablesEmptyHeading, TablesEmptyText) = NewZone ? ("Sala nueva", "Cuando el servidor confirme la sala, podrás añadirle sus mesas.")
            : Zones.Length == 0 ? ("Sin mesas todavía", "Las mesas de cada sala aparecerán aquí, como en el plano del comedor.")
            : zone is null ? ("Elige una sala", "Sus mesas aparecerán aquí, como en el plano del comedor.")
            : ("Sala sin mesas", zone.Name + " aún no tiene mesas: añade la primera.");
        ShowCreateTable = zone is not null && TableItems.Length == 0;
        // Ficha: la mesa elegida (o la nueva) manda; si no, la sala elegida (o la nueva); si no, que elegir.
        ShowTableFicha = NewTable || table is not null;
        ShowZoneFicha = !ShowTableFicha && (NewZone || zone is not null);
        ShowZoneEmpty = !ShowTableFicha && !ShowZoneFicha;
        (ZoneEmptyHeading, ZoneEmptyText) = none ? ("Sin leer todavía", "La organización aparecerá aquí al leerla del servidor.")
            : Zones.Length == 0 ? ("Sin salas todavía", "Cuando crees una sala, elígela para ver sus datos, su tarifa y sus mesas.")
            : ("Elige una sala", "Pulsa una sala de la lista para ver sus datos, su tarifa y sus mesas.");
        ZoneContext = NewZone ? "Alta de sala" : zone is null ? "" : "Sala · tarifa " + (zone.TariffName is { Length: > 0 } own ? own : "General");
        ZoneTitle = NewZone ? "Nueva sala" : zone?.Name ?? "";
        ZoneStateText = zone is null ? "" : Cap(Labels.Active(zone.Active, feminine: true));
        ZoneStateTone = zone?.Active == true ? "ok" : "other";
        ZoneSaveText = NewZone ? "Crear sala" : "Guardar cambios";
        ZoneDeactivates = zone is { Active: true };
        ZoneReactivates = zone is { Active: false };
        ZoneTablesFigure = zone is null ? "" : activeTables.Length.ToString(CultureInfo.InvariantCulture);
        ZoneSeatsFigure = zone is null ? "" : activeTables.Sum(t => t.Capacity).ToString(CultureInfo.InvariantCulture);
        ZoneTablesText = zone is null ? "" : inactiveTables.Length > 0
            ? $"{Cap(Labels.Plural(inactiveTables.Length, "mesa desactivada", "mesas desactivadas"))}: {string.Join(", ", inactiveTables.Select(t => t.Name))}."
            : activeTables.Length == 0 ? "Aún no tiene mesas: añádelas con «Mesa»." : "Todas sus mesas están activas.";
        TableContext = NewTable ? (SelectedZone is { } home ? "Alta de mesa en " + home.Name : "Alta de mesa") : table is null ? "" : "Mesa de " + ZoneNameOf(table.ZoneId);
        TableTitle = NewTable ? "Nueva mesa" : table?.Name ?? "";
        TableStateText = table is null ? "" : Cap(Labels.Active(table.Active, feminine: true));
        TableStateTone = table?.Active == true ? "ok" : "other";
        TableSaveText = NewTable ? "Crear mesa" : "Guardar cambios";
        TableDeactivates = table is { Active: true };
        TableReactivates = table is { Active: false };
        // Estaciones.
        var station = NewStation ? null : SelectedStation;
        ShowStationListEmpty = StationItems.Length == 0;
        (StationListHeading, StationListText) = none ? ("Sin leer todavía", "Las estaciones aparecerán aquí al leer la organización del servidor.")
            : ("Aún no hay estaciones", "Crea la primera: una cocina, una barra o la sala de los comanderos.");
        HasStationContext = NewStation || station is not null;
        (StationEmptyHeading, StationEmptyText) = none ? ("Sin leer todavía", "Las estaciones aparecerán aquí al leer la organización del servidor.")
            : ("Elige una estación", "Pulsa una estación para ver sus datos, su tipo y qué puestos admite.");
        StationContext = NewStation ? "Alta de estación" : station is null ? "" : "Estación · " + station.KindLabel;
        StationTitle = NewStation ? "Nueva estación" : station?.Name ?? "";
        StationStateText = station is null ? "" : Cap(Labels.Active(station.Active, feminine: true));
        StationStateTone = station?.Active == true ? "ok" : "other";
        IsReservedStation = station is not null && IsReserved(station);
        StationSaveText = NewStation ? "Crear estación" : "Guardar cambios";
        StationDeactivates = station is { Active: true } && !IsReservedStation;
        StationReactivates = station is { Active: false };
        StationNote = IsReservedStation ? "La estación Pase existe siempre: no se puede desactivar ni cambiar de tipo."
            : "Nada se borra: una estación desactivada deja de ofrecerse para platos y puestos nuevos y conserva su historia; se puede reactivar.";
    }

    private static int Number(string text, string what, int min, int max)
    {
        if (!int.TryParse(text.Trim(), out var value) || value < min || value > max) throw new ArgumentException($"{what}: introduce un número entre {min} y {max}.");
        return value;
    }
    private static string Code(string text)
    {
        var code = text.Trim();
        if (code.Length is 0 or > 32 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("El código es obligatorio: letras, números, '-' o '_', sin espacios ni acentos (máximo 32).");
        return code;
    }
    // C2b: lo elegido en un desplegable. Null = el desplegable perdio su eleccion (su lista cambio): no se envia nada en lugar de mover
    // la mesa sin querer.
    private static string Chosen(string? value, string what) => value ?? throw new ArgumentException($"Vuelve a elegir {what}: el desplegable se quedó sin elección.");

    // C2b: confirmed se ejecuta solo cuando el servidor confirma la orden (antes de releer): lo recien creado queda elegido.
    private Task Command(string action, object body, string description, Action? confirmed = null) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("organization/commands/" + action, body, description);
        confirmed?.Invoke();
        NewZone = NewTable = NewStation = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    // C2b: empezar un alta deja sin eleccion lo que se va a crear (la ficha es la del alta) y abre su pestana; "Cancelar" vuelve a lo
    // elegido antes. Nada se envia hasta "Crear".
    private bool CanStart() => shell.Writable && shell.IsMain;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartZone()
    {
        zoneBeforeNew = SelectedZone?.Id; tableBeforeNew = SelectedTable?.Id;
        rendering = true; try { SelectedZone = null; RenderTables(); SelectedTable = null; } finally { rendering = false; }
        NewZone = true; NewTable = false; Section = "zones";
        ZoneId = ""; ZoneName = ""; ZoneSort = Zones.Length.ToString(); RefreshChoices(); ZoneTariffId = ""; FillTableForm(); Sync();
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartTable()
    {
        tableBeforeNew = SelectedTable?.Id;
        rendering = true; try { SelectedTable = null; } finally { rendering = false; }
        NewTable = true; NewZone = false; Section = "zones";
        RefreshChoices();
        TableId = ""; TableName = ""; TableCapacity = "4"; TableZoneId = SelectedZone?.Id ?? ""; TableSort = Tables.Length.ToString(); Sync();
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartStation()
    {
        stationBeforeNew = SelectedStation?.Id;
        rendering = true; try { SelectedStation = null; } finally { rendering = false; }
        NewStation = true; Section = "stations";
        StationId = ""; StationName = ""; StationKind = "kitchen"; StationSort = Stations.Length.ToString(); Sync();
    }
    private bool CanCancelZone() => NewZone;
    [RelayCommand(CanExecute = nameof(CanCancelZone))]
    private void CancelZone()
    {
        NewZone = false;
        rendering = true;
        try
        {
            SelectedZone = Zones.FirstOrDefault(z => z.Id == zoneBeforeNew) ?? Zones.FirstOrDefault(z => z.Active) ?? Zones.FirstOrDefault();
            RenderTables(); SelectedTable = Tables.FirstOrDefault(t => t.Id == tableBeforeNew);
        }
        finally { rendering = false; }
        FillZoneForm(); FillTableForm(); Sync();
    }
    private bool CanCancelTable() => NewTable;
    [RelayCommand(CanExecute = nameof(CanCancelTable))]
    private void CancelTable()
    {
        NewTable = false;
        rendering = true; try { SelectedTable = Tables.FirstOrDefault(t => t.Id == tableBeforeNew); } finally { rendering = false; }
        FillTableForm(); Sync();
    }
    private bool CanCancelStation() => NewStation;
    [RelayCommand(CanExecute = nameof(CanCancelStation))]
    private void CancelStation()
    {
        NewStation = false;
        rendering = true; try { SelectedStation = Stations.FirstOrDefault(s => s.Id == stationBeforeNew); } finally { rendering = false; }
        FillStationForm(); Sync();
    }
    // C2b: "Ver la sala" (o pulsar otra vez la sala elegida): la ficha vuelve a la sala; solo cambia la eleccion, nada se envia.
    private bool CanShowZone() => SelectedZone is not null && !NewZone && (SelectedTable is not null || NewTable);
    [RelayCommand(CanExecute = nameof(CanShowZone))]
    private void ShowZone()
    {
        NewTable = false;
        rendering = true; try { SelectedTable = null; } finally { rendering = false; }
        FillTableForm(); Sync();
    }

    private bool CanSaveZone() => ZoneEditable;
    [RelayCommand(CanExecute = nameof(CanSaveZone))]
    private Task SaveZone()
    {
        var id = Code(NewZone ? ZoneId : SelectedZone!.Id); var name = ZoneName.Trim(); var sort = Number(ZoneSort, "Orden", 0, 9999);
        if (name.Length == 0) throw new ArgumentException("El nombre de la sala es obligatorio.");
        // E3.2: tariffId "" = la General, un codigo = esa tarifa, null = sin cambio (zone-update y zone-create ya lo admitian).
        var tariffId = ZoneTariffId;
        return Command(NewZone ? "zone-create" : "zone-update", new { id, name, sort, tariffId }, (NewZone ? "Crear sala " : "Guardar sala ") + name,
            NewZone ? () => { createdZone = id; } : null);
    }
    private bool CanToggleZone() => shell.Writable && shell.IsMain && SelectedZone is not null && !NewZone;
    [RelayCommand(CanExecute = nameof(CanToggleZone))]
    private Task ToggleZone() => Command(SelectedZone!.Active ? "zone-deactivate" : "zone-reactivate", new { id = SelectedZone.Id },
        (SelectedZone.Active ? "Desactivar sala " : "Reactivar sala ") + SelectedZone.Name);

    private bool CanSaveTable() => TableEditable;
    [RelayCommand(CanExecute = nameof(CanSaveTable))]
    private Task SaveTable()
    {
        var id = Code(NewTable ? TableId : SelectedTable!.Id); var name = TableName.Trim();
        var capacity = Number(TableCapacity, "Plazas", 1, 60); var sort = Number(TableSort, "Orden", 0, 9999); var zoneId = Code(Chosen(TableZoneId, "la sala"));
        if (name.Length == 0) throw new ArgumentException("El nombre de la mesa es obligatorio.");
        return Command(NewTable ? "table-create" : "table-update", new { id, name, capacity, zoneId, sort }, (NewTable ? "Crear mesa " : "Guardar mesa ") + name,
            NewTable ? () => { createdTable = id; } : null);
    }
    private bool CanToggleTable() => shell.Writable && shell.IsMain && SelectedTable is not null && !NewTable;
    [RelayCommand(CanExecute = nameof(CanToggleTable))]
    private Task ToggleTable() => Command(SelectedTable!.Active ? "table-deactivate" : "table-reactivate", new { id = SelectedTable.Id },
        (SelectedTable.Active ? "Desactivar mesa " : "Reactivar mesa ") + SelectedTable.Name);

    private bool CanSaveStation() => StationEditable;
    [RelayCommand(CanExecute = nameof(CanSaveStation))]
    private Task SaveStation()
    {
        var id = Code(NewStation ? StationId : SelectedStation!.Id); var name = StationName.Trim(); var sort = Number(StationSort, "Orden", 0, 9999);
        if (name.Length == 0) throw new ArgumentException("El nombre de la estación es obligatorio.");
        return Command(NewStation ? "station-create" : "station-update", new { id, name, kind = StationKind, sort }, (NewStation ? "Crear estación " : "Guardar estación ") + name,
            NewStation ? () => { createdStation = id; } : null);
    }
    private bool CanToggleStation() => shell.Writable && shell.IsMain && SelectedStation is not null && !NewStation && SelectedStation.Id != "pase";
    [RelayCommand(CanExecute = nameof(CanToggleStation))]
    private Task ToggleStation() => Command(SelectedStation!.Active ? "station-deactivate" : "station-reactivate", new { id = SelectedStation.Id },
        (SelectedStation.Active ? "Desactivar estación " : "Reactivar estación ") + SelectedStation.Name);
}
