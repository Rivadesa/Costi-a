using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E2: superficie "Configuracion" (solo rol main; el servidor lo impone ademas por permisos): salas y mesas, y estaciones.
// Patron lista + ficha. Cada guardado es UN comando por la tuberia habitual (orden incierta persistida antes de enviar) y
// despues se relee la organizacion completa: la pantalla nunca muestra un estado que el servidor no haya confirmado.
// Nada se borra: se desactiva y se puede reactivar. La validacion local es solo de formato; las reglas las aplica el servidor.
public sealed partial class OrganizationViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;

    [ObservableProperty] private ZoneDto[] zones = [];
    [ObservableProperty] private ZoneDto? selectedZone;
    [ObservableProperty] private TableDto[] tables = [];
    [ObservableProperty] private TableDto? selectedTable;
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] private StationDto? selectedStation;
    // Fichas (formularios). NewX = alta: el codigo se teclea; en edicion el codigo es inmutable.
    [ObservableProperty] private bool newZone; [ObservableProperty] private string zoneId = ""; [ObservableProperty] private string zoneName = ""; [ObservableProperty] private string zoneSort = "0";
    // E3.2: tarifa de la sala. "" = la General (la sala no tiene tarifa propia); null = el desplegable perdio su eleccion al releer
    // y entonces la orden no la toca (el servidor conserva la que tenia). Las opciones son las de la sala que se edita: General, las
    // tarifas activas y, solo si ESA sala usa una desactivada (o una que el servidor no lista), esa tambien, marcada, para no
    // cambiarla sin querer. Otra sala nunca la ofrece: el servidor la rechazaria (tariff_inactive).
    [ObservableProperty] private string? zoneTariffId = "";
    [ObservableProperty] private TariffDto[] tariffChoices = [GeneralChoice];
    private static readonly TariffDto GeneralChoice = new("", "General", -1, true);
    private TariffDto[] tariffs = [];
    [ObservableProperty] private bool newTable; [ObservableProperty] private string tableId = ""; [ObservableProperty] private string tableName = "";
    [ObservableProperty] private string tableCapacity = "4"; [ObservableProperty] private string tableZoneId = ""; [ObservableProperty] private string tableSort = "0";
    [ObservableProperty] private bool newStation; [ObservableProperty] private string stationId = ""; [ObservableProperty] private string stationName = "";
    [ObservableProperty] private string stationKind = "kitchen"; [ObservableProperty] private string stationSort = "0";
    [ObservableProperty] private string summary = "Sin datos de organización todavía.";

    public bool Visible { get; set; }
    public ZoneDto[] ActiveZones => Zones.Where(z => z.Active).ToArray();
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

    internal void Sync()
    {
        OnPropertyChanged(nameof(ZoneEditable)); OnPropertyChanged(nameof(TableEditable)); OnPropertyChanged(nameof(StationEditable));
        OnPropertyChanged(nameof(ZoneFormTitle)); OnPropertyChanged(nameof(TableFormTitle)); OnPropertyChanged(nameof(StationFormTitle));
        OnPropertyChanged(nameof(HasZones)); OnPropertyChanged(nameof(HasTables)); OnPropertyChanged(nameof(HasStations)); OnPropertyChanged(nameof(ActiveZones));
        SaveZoneCommand.NotifyCanExecuteChanged(); ToggleZoneCommand.NotifyCanExecuteChanged(); StartZoneCommand.NotifyCanExecuteChanged();
        SaveTableCommand.NotifyCanExecuteChanged(); ToggleTableCommand.NotifyCanExecuteChanged(); StartTableCommand.NotifyCanExecuteChanged();
        SaveStationCommand.NotifyCanExecuteChanged(); ToggleStationCommand.NotifyCanExecuteChanged(); StartStationCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try { Zones = []; Tables = []; Stations = []; tariffs = []; TariffChoices = [GeneralChoice]; SelectedZone = null; SelectedTable = null; SelectedStation = null; NewZone = NewTable = NewStation = false; Summary = "Sin datos de organización todavía."; }
        finally { rendering = false; }
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
            var zone = SelectedZone?.Id; var table = SelectedTable?.Id; var station = SelectedStation?.Id;
            // E3.2: la lista de salas nombra la tarifa propia de cada sala (la General no se nombra: es la de siempre).
            tariffs = organization.Tariffs ?? [];
            Zones = organization.Zones.Select(z => z with { TariffName = TariffOf(z.TariffId) is { Length: > 0 } own ? tariffs.FirstOrDefault(t => t.Id == own)?.Name ?? own : null }).ToArray();
            Stations = organization.Stations;
            SelectedZone = Zones.FirstOrDefault(z => z.Id == zone) ?? Zones.FirstOrDefault();
            RenderTables();
            SelectedTable = Tables.FirstOrDefault(t => t.Id == table);
            SelectedStation = Stations.FirstOrDefault(s => s.Id == station);
            var activeTables = Zones.Sum(z => z.Tables.Count(t => t.Active));
            Summary = $"{Labels.Plural(Zones.Count(z => z.Active), "sala activa", "salas activas")} · {Labels.Plural(activeTables, "mesa activa", "mesas activas")} · {Labels.Plural(Stations.Count(s => s.Active), "estación activa", "estaciones activas")}";
        }
        finally { rendering = false; }
        if (!NewZone) FillZoneForm(); else RenderTariffChoices();
        if (!NewTable) FillTableForm(); if (!NewStation) FillStationForm();
        Sync();
    }

    // Opciones del desplegable para la sala que se edita (una sala nueva: General y las activas). Se fijan ANTES que ZoneTariffId:
    // al cambiar la lista el desplegable puede escribir null si su eleccion ya no esta, y despues se pone la buena.
    private void RenderTariffChoices()
    {
        var own = NewZone ? "" : TariffOf(SelectedZone?.TariffId);
        TariffDto[] active = [GeneralChoice, .. tariffs.Where(t => t.Id != "general" && t.Active)];
        TariffChoices = own.Length == 0 || active.Any(t => t.Id == own) ? active
            : [.. active, tariffs.FirstOrDefault(t => t.Id == own) is { } off ? off with { Name = off.Name + " (desactivada)" } : new TariffDto(own, own, int.MaxValue, false)];
    }

    private void RenderTables() => Tables = SelectedZone is null ? [] : SelectedZone.Tables;

    partial void OnSelectedZoneChanged(ZoneDto? value)
    {
        if (rendering) return;
        NewZone = false; FillZoneForm();
        rendering = true; try { RenderTables(); SelectedTable = null; } finally { rendering = false; }
        NewTable = false; FillTableForm(); Sync();
    }
    partial void OnSelectedTableChanged(TableDto? value) { if (rendering) return; NewTable = false; FillTableForm(); Sync(); }
    partial void OnSelectedStationChanged(StationDto? value) { if (rendering) return; NewStation = false; FillStationForm(); Sync(); }

    private void FillZoneForm() { ZoneId = SelectedZone?.Id ?? ""; ZoneName = SelectedZone?.Name ?? ""; ZoneSort = (SelectedZone?.Sort ?? 0).ToString(); RenderTariffChoices(); ZoneTariffId = TariffOf(SelectedZone?.TariffId); }
    // Tarifa propia de una sala como codigo; sin tarifa (o la general escrita a mano) = "" (la General).
    private static string TariffOf(string? tariffId) => tariffId is null or "" or "general" ? "" : tariffId;
    private void FillTableForm()
    {
        TableId = SelectedTable?.Id ?? ""; TableName = SelectedTable?.Name ?? ""; TableCapacity = (SelectedTable?.Capacity ?? 4).ToString();
        TableZoneId = SelectedTable?.ZoneId ?? SelectedZone?.Id ?? ""; TableSort = (SelectedTable?.Sort ?? 0).ToString();
    }
    private void FillStationForm()
    {
        StationId = SelectedStation?.Id ?? ""; StationName = SelectedStation?.Name ?? ""; StationSort = (SelectedStation?.Sort ?? 0).ToString();
        StationKind = SelectedStation?.Kind.ToLowerInvariant() ?? "kitchen";
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

    private Task Command(string action, object body, string description) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("organization/commands/" + action, body, description);
        NewZone = NewTable = NewStation = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    private bool CanStart() => shell.Writable && shell.IsMain;
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartZone() { NewZone = true; ZoneId = ""; ZoneName = ""; ZoneSort = Zones.Length.ToString(); RenderTariffChoices(); ZoneTariffId = ""; Sync(); }
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartTable() { NewTable = true; TableId = ""; TableName = ""; TableCapacity = "4"; TableZoneId = SelectedZone?.Id ?? ""; TableSort = Tables.Length.ToString(); Sync(); }
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartStation() { NewStation = true; StationId = ""; StationName = ""; StationKind = "kitchen"; StationSort = Stations.Length.ToString(); Sync(); }

    private bool CanSaveZone() => ZoneEditable;
    [RelayCommand(CanExecute = nameof(CanSaveZone))]
    private Task SaveZone()
    {
        var id = Code(NewZone ? ZoneId : SelectedZone!.Id); var name = ZoneName.Trim(); var sort = Number(ZoneSort, "Orden", 0, 9999);
        if (name.Length == 0) throw new ArgumentException("El nombre de la sala es obligatorio.");
        // E3.2: tariffId "" = la General, un codigo = esa tarifa, null = sin cambio (zone-update y zone-create ya lo admitian).
        var tariffId = ZoneTariffId;
        return Command(NewZone ? "zone-create" : "zone-update", new { id, name, sort, tariffId }, (NewZone ? "Crear sala " : "Guardar sala ") + name);
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
        var capacity = Number(TableCapacity, "Plazas", 1, 60); var sort = Number(TableSort, "Orden", 0, 9999); var zoneId = Code(TableZoneId);
        if (name.Length == 0) throw new ArgumentException("El nombre de la mesa es obligatorio.");
        return Command(NewTable ? "table-create" : "table-update", new { id, name, capacity, zoneId, sort }, (NewTable ? "Crear mesa " : "Guardar mesa ") + name);
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
        return Command(NewStation ? "station-create" : "station-update", new { id, name, kind = StationKind, sort }, (NewStation ? "Crear estación " : "Guardar estación ") + name);
    }
    private bool CanToggleStation() => shell.Writable && shell.IsMain && SelectedStation is not null && !NewStation && SelectedStation.Id != "pase";
    [RelayCommand(CanExecute = nameof(CanToggleStation))]
    private Task ToggleStation() => Command(SelectedStation!.Active ? "station-deactivate" : "station-reactivate", new { id = SelectedStation.Id },
        (SelectedStation.Active ? "Desactivar estación " : "Reactivar estación ") + SelectedStation.Name);
}
