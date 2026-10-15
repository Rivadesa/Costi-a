using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie de comedor y cocina. La habilitacion de CADA accion es un mapeo directo de las
// affordances del DTO del servidor (Allowed): este viewmodel no reconstruye ninguna regla.
// La seleccion solicitada y la entidad leida son estados distintos: un comando solo se construye
// desde un contexto leido correctamente que coincide con lo que el operador ve seleccionado.
//
// C2 (plan 2026-10-14): plano de mesas y ficha. Las tarjetas son las mesas ACTIVAS de /configuration cruzadas con el tablero
// (una mesa ocupada que la configuracion ya no lista sale igual: nunca se esconde una mesa con gente). Todo lo de la tarjeta es
// PRESENTACION (FloorPlan, Labels): una tarjeta solo mueve la seleccion de siempre, SelectedEntry para una mesa ocupada (la lectura
// y el contexto de las ordenes no cambian, F01) o SelectedTable para una libre (formulario de abrir mesa). En la ficha, la accion
// grande es la mas relevante de las ANUNCIADAS y las demas van debajo: todas siguen siendo ActCommand con su CanExecute.

// Tarjeta de una mesa del plano. Inmutable salvo la marca de seleccion (se rehace en cada lectura y cada minuto).
public sealed partial class TableTile : ObservableObject
{
    public TableTile(TableChoice table, BoardEntry? entry, string zoneId, string zoneName, string name, string offerName, DateTimeOffset now)
    {
        Table = table; Entry = entry; ZoneId = zoneId; ZoneName = zoneName; Name = name;
        var service = entry?.Service;
        State = FloorPlan.State(service);
        StateLabel = Labels.TableState(State);
        Tone = ServiceViewModel.ToneOf(State);
        PeopleText = service is null ? Labels.Plural(table.Capacity, "plaza", "plazas") : Labels.Plural(service.Pax, "persona", "personas");
        // Libre: sus plazas; ocupada: sus personas y la oferta con la que se abrio.
        Detail = service is null || offerName.Length == 0 ? PeopleText : PeopleText + " · " + offerName;
        Elapsed = FloorPlan.Since(service) is { } since ? Labels.Minutes(now - since) : "";
        Alert = FloorPlan.Alert(service);
        AlertAlarming = FloorPlan.AlertIsAlarming(service);
        Steps = FloorPlan.Steps(service);
        Progress = Steps.Length == 0 ? "" : $"{FloorPlan.Done(service)} de {Steps.Length}";
        Description = string.Join(", ", new[] { name, StateLabel, PeopleText, Alert, Progress.Length > 0 ? Progress + " pases" : "" }.Where(p => p.Length > 0));
    }
    public TableChoice Table { get; }
    public BoardEntry? Entry { get; }
    public bool Occupied => Entry is not null;
    public string ZoneId { get; }
    public string ZoneName { get; }
    public string Name { get; }
    public string State { get; }
    public string StateLabel { get; }
    public string Tone { get; }
    public string PeopleText { get; }
    public string Detail { get; }
    public string Elapsed { get; }
    public string Alert { get; }
    public bool HasAlert => Alert.Length > 0;
    public bool AlertAlarming { get; }
    public string[] Steps { get; }
    public bool HasSteps => Steps.Length > 0;
    public string Progress { get; }
    public string Description { get; }
    [ObservableProperty] private bool isSelected;
}
// Pestana de sala (Id "" = todas) y filtro del plano: objetos ESTABLES cuyo texto (con su cifra) cambia en el sitio. Si la lista se
// sustituyera en cada lectura, la seleccion de la lista (SelectedValue, en los dos sentidos) se perderia y volveria vacia.
public sealed partial class ZoneTab(string id) : ObservableObject { public string Id { get; } = id; [ObservableProperty] private string label = ""; }
public sealed partial class FilterChip(string id, string name) : ObservableObject
{ public string Id { get; } = id; public string Name { get; } = name; [ObservableProperty] private string label = name; }
// Grupo de tarjetas por sala, fila de pase y de elaboracion, y boton de accion.
public sealed record TileGroup(string Name, string Summary, TableTile[] Tiles);
public sealed record CourseRow(CourseDto Course, int Number, string State, string Tone, string Detail);
public sealed record PreparationRow(PreparationDto Preparation, string Station, string Line);
public sealed record ActionButton(string Action, string Text, bool Danger);

public sealed partial class ServiceViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? serviceId;   // destino solicitado; nunca es por si mismo el contexto de un comando
    private int generation;      // invalida lecturas obsoletas cuando cambia el destino
    private StationChoice[]? stations;   // C2: estaciones de la ultima configuracion leida (nombres de las elaboraciones)

    [ObservableProperty] private BoardEntry[] board = [];
    [ObservableProperty] private BoardEntry? selectedEntry;
    [ObservableProperty] private Versioned<DiningDto>? dining;
    [ObservableProperty] private CourseDto[] courses = [];
    [ObservableProperty] private CourseDto? selectedCourse;
    [ObservableProperty] private PreparationDto[] preparations = [];
    [ObservableProperty] private PreparationDto? selectedPreparation;
    [ObservableProperty] private TableChoice[] tables = [];
    [ObservableProperty] private TableChoice? selectedTable;
    [ObservableProperty] private OfferChoice[] offers = [];
    [ObservableProperty] private OfferChoice? selectedOffer;
    // E4a: eleccion de plato por comensal en un pase de menu cerrado (solo cuando el servidor anuncia 'choose' en el pase).
    [ObservableProperty] private string choiceGuest = "1";
    [ObservableProperty] private OfferDishChoice[] choiceDishes = [];
    [ObservableProperty] private OfferDishChoice? selectedChoiceDish;
    [ObservableProperty] private string choicesText = "";
    // E4b: pedir un plato de la carta en un grupo pendiente (solo cuando el servidor anuncia 'add-dish' en el pase).
    [ObservableProperty] private OfferItemChoice[] dishItems = [];
    [ObservableProperty] private OfferItemChoice? selectedDishItem;
    [ObservableProperty] private string dishGuest = "";
    [ObservableProperty] private string dishQuantity = "1";
    [ObservableProperty] private string pax = "2";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasRestrictions), nameof(RestrictionsTitle), nameof(RestrictionsAlarming))] private GuestRestrictionDto[] restrictions = [];
    public bool HasRestrictions => Restrictions.Length > 0;
    [ObservableProperty] private GuestRestrictionDto? selectedRestriction;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RestrictionsAlarming))] private bool restrictionsPendingAck;
    [ObservableProperty] private string restrictionGuest = "";
    [ObservableProperty] private string restrictionKind = "allergy";
    [ObservableProperty] private string restrictionSubstance = "";
    [ObservableProperty] private string restrictionSeverity = "severe";
    [ObservableProperty] private string reviewNote = "";
    [ObservableProperty] private string serviceTitle = "Selecciona una mesa";

    // ---- C2: plano de mesas ----
    [ObservableProperty] private TableTile[] tiles = [];
    [ObservableProperty] private TileGroup[] groups = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasZoneTabs))] private ZoneTab[] zoneTabs = [];
    public bool HasZoneTabs => ZoneTabs.Length > 0;   // con una sola sala no hay pestanas (su nombre va en el grupo)
    public FilterChip[] Filters { get; } = [new("all", "Todas"), new("busy", "Ocupadas"), new("kitchen", "En cocina"), new("ready", "Para servir"), new("free", "Libres")];
    [ObservableProperty] private string selectedZone = "";   // "" = todas las salas
    [ObservableProperty] private string filter = "all";      // all, busy, kitchen, ready, free
    [ObservableProperty] private string floorSummary = "";
    [ObservableProperty] private string floorEmpty = "";
    // Ficha: formulario de abrir mesa (mesa libre elegida o boton "Abrir mesa") o detalle de la mesa ocupada elegida.
    [ObservableProperty] private bool openForm;
    [ObservableProperty] private CourseRow[] courseRows = [];
    [ObservableProperty] private PreparationRow[] preparationRows = [];

    // C1: nombres de mesa de la configuracion leida (GET /configuration); sin nombre conocido queda el codigo. C2: tambien los de las
    // estaciones (configuration.stations); con un servidor anterior las elaboraciones siguen nombrando la estacion por su codigo.
    public Names Names { get; private set; } = Names.Empty;
    partial void OnTablesChanged(TableChoice[] value)
    {
        Names = new Names(new Configuration(value, Stations: stations));
        if (Dining is not null) ServiceTitle = Title(Dining.Data);
        RebuildFloor(); UpdatePanel();
    }
    partial void OnBoardChanged(BoardEntry[] value) { RebuildFloor(); UpdatePanel(); }
    partial void OnOffersChanged(OfferChoice[] value) { RebuildFloor(); UpdatePanel(); }
    // El titulo sale siempre de la mesa leida: nunca se escribe a mano (los checks lo comprueban asi).
    partial void OnDiningChanged(Versioned<DiningDto>? value) { if (value is not null) ServiceTitle = Title(value.Data); UpdatePanel(); }
    partial void OnCoursesChanged(CourseDto[] value) => UpdateCourseRows();
    partial void OnPreparationsChanged(PreparationDto[] value) => UpdatePreparationRows();
    // Una lista que pierde su seleccion escribe null: se vuelve a "todas" (sala) o a "todas" (filtro), nunca a un estado sin sentido.
    partial void OnSelectedZoneChanged(string value) { if (value is null) SelectedZone = ""; else RebuildFloor(); }
    partial void OnFilterChanged(string value) { if (value is null) Filter = "all"; else RebuildFloor(); }
    partial void OnSelectedTableChanged(TableChoice? value) { MarkSelection(); UpdatePanel(); OpenCommand.NotifyCanExecuteChanged(); }
    partial void OnOpenFormChanged(bool value) { MarkSelection(); UpdatePanel(); }
    private string Title(DiningDto dining) => $"{Names.Table(dining.TableId)} · {Labels.Plural(dining.Pax, "persona", "personas")} · {dining.StateLabel}";
    private string TableOf(Versioned<DiningDto> context) => Names.Table(context.Data.TableId);

    // Contexto inmutable de un comando: identidad y version de la MISMA entidad leida correctamente,
    // y solo si coincide con la fila seleccionada. Null = no hay nada accionable.
    private Versioned<DiningDto>? Context =>
        Dining is not null && SelectedEntry?.Service.Id == Dining.Data.Id ? Dining : null;

    // Unico punto de verdad para la habilitacion: la accion esta en la lista que envio el servidor.
    private bool Allowed(string action) => action switch
    {
        "start" or "fire-next" or "pause" or "resume" or "complete" or "cancel-unstarted"
        or "declare-restriction" or "remove-restriction"
            => Context?.Data.Actions?.Contains(action) == true,
        "ready" or "serve" or "skip" or "choose" or "add-dish" => Context is not null && SelectedCourse?.Actions?.Contains(action) == true,
        "preparation-start" or "preparation-ready" or "review-preparation"
            => Context is not null && SelectedPreparation?.Actions?.Contains(action) == true,
        "release" => Context is not null && SelectedEntry?.Occupancy.Actions?.Contains(action) == true,
        _ => false
    };
    public bool CanAction(string action) => shell.Writable && Allowed(action);

    // ---- C2: acciones de la ficha. Cual va en grande es presentacion: la primera ANUNCIADA de esta lista (servir lo que espera en el
    // pase, validarlo, enviar el siguiente, empezar...). El resto de las anunciadas, debajo. Ninguna se habilita aqui: ActCommand.
    private static readonly string[] PrimaryOrder = ["serve", "ready", "fire-next", "start", "resume", "complete", "release"];
    private static readonly string[] SecondaryOrder = ["serve", "ready", "fire-next", "start", "resume", "pause", "skip", "complete", "cancel-unstarted", "release"];
    private static readonly string[] ReasonActions = ["pause", "skip", "cancel-unstarted", "release", "remove-restriction"];
    public string? PrimaryAction => PrimaryOrder.FirstOrDefault(Allowed);
    public string PrimaryText => PrimaryAction is { } action ? ActionText(action) : WaitingText();
    [ObservableProperty] private ActionButton[] secondaryActions = [];
    public bool ReasonNeeded => ReasonActions.Any(Allowed);
    public bool PreparationActionsAnnounced => Allowed("preparation-start") || Allowed("preparation-ready");
    public bool ReviewAnnounced => Allowed("review-preparation");
    public bool HasContext => Context is not null;
    private string ActionText(string action) => action switch
    {
        "serve" or "ready" or "skip" when SelectedCourse is { } course => Labels.Action(action) + " · " + course.Name,
        "fire-next" when Courses.FirstOrDefault(c => c.State == "Pending") is { } next => Labels.Action(action) + " · " + next.Name,
        _ => Labels.Action(action)
    };
    // Sin ninguna accion principal anunciada se dice que se espera (el boton grande queda deshabilitado).
    private string WaitingText() => Context?.Data is not { } dining ? ""
        : dining.RestrictionsPendingAck ? "Esperando la revisión de cocina"
        : FloorPlan.State(dining) == FloorPlan.Kitchen ? "Esperando a cocina"
        : "Sin acciones para esta mesa ahora";

    internal void Sync()
    {
        ActCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged(); DeclareRestrictionCommand.NotifyCanExecuteChanged();
        RemoveRestrictionCommand.NotifyCanExecuteChanged(); ReviewCommand.NotifyCanExecuteChanged(); ChooseCommand.NotifyCanExecuteChanged(); AddDishCommand.NotifyCanExecuteChanged();
        StartOpenCommand.NotifyCanExecuteChanged(); ViewAccountCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanChoose)); OnPropertyChanged(nameof(CanAddDish));
        OnPropertyChanged(nameof(PrimaryAction)); OnPropertyChanged(nameof(PrimaryText)); OnPropertyChanged(nameof(ReasonNeeded));
        OnPropertyChanged(nameof(PreparationActionsAnnounced)); OnPropertyChanged(nameof(ReviewAnnounced)); OnPropertyChanged(nameof(HasContext));
        var secondary = SecondaryOrder.Where(a => a != PrimaryAction && Allowed(a)).Select(a => new ActionButton(a, ActionText(a), a is "release" or "cancel-unstarted")).ToArray();
        if (!secondary.SequenceEqual(SecondaryActions)) SecondaryActions = secondary;   // sin rehacer los botones en cada cambio de "ocupado"
    }

    // C2: la configuracion se relee en cada actualizacion (RefreshAll): una mesa creada aparece sin reconectar. La mesa y la oferta
    // elegidas se conservan por su codigo.
    public void ApplyConfiguration(Configuration config)
    {
        rendering = true;
        try
        {
            var table = SelectedTable?.Id; var offer = SelectedOffer?.Id;
            stations = config.Stations;
            Tables = config.Tables; SelectedTable = Tables.FirstOrDefault(t => t.Id == table) ?? Tables.FirstOrDefault();
            // Servidor anterior a E4a: solo 'menus' (id, name); se muestran como ofertas sin pases.
            Offers = config.Offers ?? (config.Menus ?? []).Select(m => new OfferChoice(m.Id, m.Name, "tasting", [])).ToArray();
            SelectedOffer = Offers.FirstOrDefault(o => o.Id == offer) ?? Offers.FirstOrDefault();
            Names = new Names(config);
            var preparation = SelectedPreparation;   // rehacer las filas no debe perder la elaboracion elegida (acciones de cocina)
            UpdatePreparationRows();
            SelectedPreparation = preparation;
        }
        finally { rendering = false; }
    }

    internal void SelectService(string? id) => serviceId = id;

    internal void Clear()
    {
        rendering = true;
        try
        {
            Board = []; SelectedEntry = null; Tables = []; Offers = []; serviceId = null; Reason = ""; stations = null;
            OpenForm = false; SelectedZone = ""; Filter = "all";
            Invalidate(); ServiceTitle = "Selecciona una mesa";
        }
        finally { rendering = false; }
        UpdatePanel();
    }

    // Nada heredado de otra lectura queda accionable: detalle, pases, elaboraciones y restricciones.
    private void Invalidate()
    {
        var wasRendering = rendering; rendering = true;
        try
        {
            Dining = null; Courses = []; SelectedCourse = null; Preparations = []; SelectedPreparation = null;
            Restrictions = []; SelectedRestriction = null; RestrictionsPendingAck = false; RestrictionSubstance = ""; ReviewNote = "";
            ServiceTitle = "Lectura pendiente: sin datos fiables de la mesa seleccionada.";
        }
        finally { rendering = wasRendering; }
        Sync(); UpdatePanel();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null) return;
        var attempt = ++generation;
        var target = serviceId;
        if (Dining is not null && Dining.Data.Id != target) Invalidate();
        try
        {
            var fresh = await shell.Api.GetAsync<BoardEntry[]>("dining/board");
            if (!fresh.Any(b => b.Service.Id == target)) target = fresh.FirstOrDefault()?.Service.Id;
            var current = target is null ? null
                : await shell.Api.GetAsync<Versioned<DiningDto>>("dining/services/" + ApiClient.Segment(target));
            if (attempt != generation) return;   // otra lectura mas reciente manda
            if (current is not null && current.Data.Id != target)
                throw new InvalidOperationException("El servidor devolvió una mesa distinta de la solicitada; lectura descartada.");
            serviceId = target;
            Render(fresh, current);
        }
        catch
        {
            // Una lectura fallida no deja datos de otra mesa a la vista ni accionables.
            if (attempt == generation) Invalidate();
            throw;
        }
    }

    private void Render(BoardEntry[] fresh, Versioned<DiningDto>? current)
    {
        var previousCourse = SelectedCourse?.Id;
        rendering = true;
        try
        {
            Board = fresh; SelectedEntry = fresh.FirstOrDefault(b => b.Service.Id == serviceId);
            Dining = current;
            ServiceTitle = current is null ? "Sin mesas ocupadas. Puedes abrir una mesa." : Title(current.Data);
            Courses = current?.Data.Courses ?? [];
            Restrictions = current?.Data.Restrictions ?? [];
            RestrictionsPendingAck = current?.Data.RestrictionsPendingAck ?? false;
            SelectedCourse = Courses.FirstOrDefault(c => c.Id == previousCourse)
                ?? Courses.FirstOrDefault(c => c.State is "Fired" or "Preparing" or "Ready")
                ?? Courses.FirstOrDefault(c => c.State == "Pending") ?? Courses.LastOrDefault();
            UpdatePreparations();
        }
        finally { rendering = false; }
        MarkSelection(); UpdatePanel(); Sync();
    }

    private void UpdatePreparations()
    {
        var previous = SelectedPreparation?.Id;
        Preparations = SelectedCourse?.Preparations ?? [];
        SelectedPreparation = Preparations.FirstOrDefault(p => p.Id == previous)
            ?? Preparations.FirstOrDefault(p => p.State is "Fired" or "Preparing") ?? Preparations.FirstOrDefault();
        // E4a: platos elegibles del pase (de la oferta con la que se abrio la mesa) y resumen de las elecciones hechas.
        var course = Dining?.Data.OfferId is { } offerId ? Offers.FirstOrDefault(o => o.Id == offerId)?.Courses.FirstOrDefault(c => c.Id == SelectedCourse?.Id) : null;
        ChoiceDishes = course?.Dishes ?? []; SelectedChoiceDish = ChoiceDishes.FirstOrDefault();
        DishItems = course?.Items ?? []; SelectedDishItem = DishItems.FirstOrDefault();
        var pax = Dining?.Data.Pax ?? 0;
        ChoicesText = SelectedCourse?.ChoiceRequired != true ? ""
            : string.Join(" · ", Enumerable.Range(1, pax).Select(g => $"{g}: " + (Preparations.FirstOrDefault(p => p.GuestPosition == g)?.Name ?? "sin elegir")));
        OnPropertyChanged(nameof(PreparationsTitle));
    }
    public bool CanAddDish => shell.Writable && Allowed("add-dish");
    private bool CanAddDishItem() => CanAddDish && SelectedDishItem is not null;
    [RelayCommand(CanExecute = nameof(CanAddDishItem))]
    private Task AddDish() => shell.Run(async () =>
    {
        var context = Require();
        int? guest = null;
        if (DishGuest.Trim().Length > 0) { if (!int.TryParse(DishGuest, out var g) || g < 1 || g > context.Data.Pax) throw new ArgumentException("Comensal: vacío (toda la mesa) o un número entre 1 y " + context.Data.Pax + "."); guest = g; }
        if (!int.TryParse(DishQuantity, out var quantity) || quantity < 1 || quantity > 20) throw new ArgumentException("Cantidad: entre 1 y 20.");
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/add-dish",
            new { expectedVersion = context.Version, courseId = SelectedCourse!.Id, productId = SelectedDishItem!.ProductId, presentationId = SelectedDishItem.PresentationId, guestPosition = guest, quantity },
            $"Pedir {quantity} × {SelectedDishItem.Name} en {SelectedCourse.Name} · {TableOf(context)}");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: plato pedido y apuntado en la cuenta.";
    });
    public bool CanChoose => shell.Writable && Allowed("choose");
    private bool CanChooseDish() => CanChoose && SelectedChoiceDish is not null;
    [RelayCommand(CanExecute = nameof(CanChooseDish))]
    private Task Choose() => shell.Run(async () =>
    {
        var context = Require();
        if (!int.TryParse(ChoiceGuest, out var guest) || guest < 1 || guest > context.Data.Pax) throw new ArgumentException("Comensal: un número entre 1 y " + context.Data.Pax + ".");
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/choose",
            new { expectedVersion = context.Version, courseId = SelectedCourse!.Id, guestPosition = guest, dishId = SelectedChoiceDish!.Id },
            $"Elegir {SelectedChoiceDish.Name} para el comensal {guest} · {TableOf(context)}");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: elección registrada.";
    });

    partial void OnSelectedEntryChanged(BoardEntry? value)
    {
        MarkSelection(); UpdatePanel();
        if (rendering || shell.Busy || value is null) return;
        serviceId = value.Service.Id;
        Sync();   // hasta que llegue la lectura del nuevo destino no hay contexto accionable
        _ = shell.Run(LoadAsync);
    }
    partial void OnSelectedCourseChanged(CourseDto? value) { if (!rendering) { UpdatePreparations(); Sync(); } }
    partial void OnSelectedPreparationChanged(PreparationDto? value) { if (!rendering) Sync(); }

    private Versioned<DiningDto> Require() => Context
        ?? throw new InvalidOperationException("No hay una mesa leída correctamente que coincida con la seleccionada.");

    private bool CanAct(string? action) => action is not null && CanAction(action);
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Act(string action) => action == "release" ? Release() : shell.Run(async () =>
    {
        var context = Require();
        if (action is "pause" or "skip" or "cancel-unstarted" && string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Introduce el motivo.");
        await shell.Api!.SendAsync(
            "dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/" + action,
            new { expectedVersion = context.Version, courseId = SelectedCourse?.Id, itemId = SelectedPreparation?.Id, reason = Reason },
            Labels.Action(action) + " · " + TableOf(context));   // C1: la orden pendiente se describe en castellano y con el nombre de la mesa
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: " + Labels.Action(action) + ".";
    });

    private bool CanOpenTable() => shell.Writable && shell.CanOpen && SelectedTable is not null && SelectedOffer is not null;
    [RelayCommand(CanExecute = nameof(CanOpenTable))]
    private Task Open() => shell.Run(async () =>
    {
        if (!int.TryParse(Pax, out var people)) throw new ArgumentException("Introduce un número de personas.");
        var result = await shell.Api!.SendAsync("dining/services",
            new { tableId = SelectedTable!.Id, pax = people, offerId = SelectedOffer!.Id, menuId = SelectedOffer.Id }, "Abrir " + SelectedTable.Name);   // menuId: alias para un servidor anterior
        serviceId = result.GetProperty("serviceId").GetString();
        OpenForm = false;   // C2: la ficha pasa a la mesa recien abierta (la lectura la elige por serviceId)
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: abrir " + SelectedTable.Name;
    });

    private bool CanDeclareRestriction() => shell.Writable && Allowed("declare-restriction");
    [RelayCommand(CanExecute = nameof(CanDeclareRestriction))]
    private Task DeclareRestriction() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(RestrictionSubstance)) throw new ArgumentException("Indica la sustancia o preferencia.");
        int? guest = null;
        if (!string.IsNullOrWhiteSpace(RestrictionGuest))
        {
            if (!int.TryParse(RestrictionGuest, out var position)) throw new ArgumentException("Comensal no válido (vacío = toda la mesa).");
            guest = position;
        }
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/declare-restriction",
            new { expectedVersion = context.Version, guestPosition = guest, kind = RestrictionKind,
                  substance = RestrictionSubstance.Trim(), severity = RestrictionSeverity },
            "Declarar restricción de comensal · " + TableOf(context));
        RestrictionSubstance = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: restricción declarada.";
    });

    private bool CanRemoveRestriction() => shell.Writable && Allowed("remove-restriction") && SelectedRestriction is not null;
    [RelayCommand(CanExecute = nameof(CanRemoveRestriction))]
    private Task RemoveRestriction() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(Reason)) throw new ArgumentException("Introduce el motivo de la retirada.");
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/remove-restriction",
            new { expectedVersion = context.Version, restrictionId = SelectedRestriction!.Id, reason = Reason },
            "Retirar restricción con motivo · " + TableOf(context));
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: restricción retirada.";
    });
    partial void OnSelectedRestrictionChanged(GuestRestrictionDto? value) => RemoveRestrictionCommand.NotifyCanExecuteChanged();

    // Revision por elaboracion (D3.5): cocina decide que pasa con ESE plato; la nota es obligatoria.
    private bool CanReview(string? decision) => decision is not null && CanAction("review-preparation");
    [RelayCommand(CanExecute = nameof(CanReview))]
    private Task Review(string decision) => shell.Run(async () =>
    {
        var context = Require();
        var preparation = SelectedPreparation ?? throw new InvalidOperationException("Selecciona la elaboración a revisar.");
        if (string.IsNullOrWhiteSpace(ReviewNote)) throw new ArgumentException("Indica qué se ha revisado o cambiado (nota obligatoria).");
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/review-preparation",
            new { expectedVersion = context.Version, courseId = SelectedCourse?.Id, itemId = preparation.Id, decision, note = ReviewNote.Trim() },
            "Revisión de cocina (" + Labels.Review(decision) + ") · " + preparation.Name + " · " + TableOf(context));
        ReviewNote = "";
        await shell.RefreshAll();
        shell.Status = "Cocina ha registrado su decisión sobre " + preparation.Name + ": " + Labels.Review(decision) + ".";
    });

    private bool CanRelease() => shell.Writable && Allowed("release");
    [RelayCommand(CanExecute = nameof(CanRelease))]
    private Task Release() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(Reason)) throw new ArgumentException("Introduce el motivo de liberación.");
        var entry = SelectedEntry!;   // Context garantiza que es la misma mesa que el detalle leido
        await shell.Api!.SendAsync("dining/occupancy/" + ApiClient.Segment(context.Data.Id) + "/release",
            new { expectedVersion = entry.OccupancyVersion, reason = Reason }, "Liberar mesa sin cambiar su cuenta · " + TableOf(context));
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: " + Labels.Action("release") + ".";
    });

    // ---- C2: plano. Una tarjeta solo mueve la seleccion de siempre (F01): nunca construye una orden ni se salta la lectura. ----
    [RelayCommand]
    private void PickTile(TableTile? tile)
    {
        if (tile is null || shell.Busy) return;
        if (tile.Entry is { } entry)
        {
            OpenForm = false;
            SelectedEntry = Board.FirstOrDefault(b => b.Service.Id == entry.Service.Id) ?? entry;   // dispara la lectura de esa mesa
        }
        else
        {
            SelectedTable = Tables.FirstOrDefault(t => t.Id == tile.Table.Id) ?? SelectedTable;
            OpenForm = true;
        }
    }

    // Boton "Abrir mesa" de la cabecera: abre el formulario de siempre (mesa, oferta, personas) con una mesa libre ya elegida.
    private bool CanStartOpen() => shell.CanOpen && !shell.Busy;
    [RelayCommand(CanExecute = nameof(CanStartOpen))]
    private void StartOpen()
    {
        if (SelectedTable is null || Board.Any(b => b.Service.TableId == SelectedTable.Id))
        {
            var free = Groups.SelectMany(g => g.Tiles).Concat(Tiles).FirstOrDefault(t => !t.Occupied);
            if (free is not null) SelectedTable = Tables.FirstOrDefault(t => t.Id == free.Table.Id) ?? SelectedTable;
        }
        OpenForm = true;
    }
    [RelayCommand] private void CancelOpen() => OpenForm = false;

    // "Ver cuenta": la cuenta de ESTA mesa en Caja (apartado del responsable: en el nivel Puesto pide la contrasena, como el menu).
    private bool CanViewAccount() => Context is not null && shell.TabsEnabled;
    [RelayCommand(CanExecute = nameof(CanViewAccount))]
    private void ViewAccount()
    {
        if (Context is not { } context) return;
        shell.Checkout.Request(context.Data.Id);
        shell.RequestElevation("CheckoutTab");
    }

    // El tiempo de las tarjetas avanza solo (la ventana lo llama cada minuto); no relee nada.
    internal void Tick() { RebuildFloor(); UpdatePanel(); }

    private void RebuildFloor()
    {
        var now = shell.Clock();
        var offers = Offers;
        string OfferName(DiningDto service) => service.OfferId is not { } id ? "" : offers.FirstOrDefault(o => o.Id == id)?.Name ?? id;
        var tiles = new List<TableTile>();
        foreach (var table in Tables)
        {
            var (zoneId, zoneName) = (table.ZoneId ?? "", table.ZoneName is { Length: > 0 } zone ? zone : "Mesas");
            var entries = Board.Where(b => b.Service.TableId == table.Id).ToArray();
            if (entries.Length == 0) tiles.Add(new TableTile(table, null, zoneId, zoneName, table.Name, "", now));
            foreach (var entry in entries) tiles.Add(new TableTile(table, entry, zoneId, zoneName, table.Name, OfferName(entry.Service), now));
        }
        // Una mesa ocupada que la configuracion no lista (desactivada despues, o una lectura de configuracion antigua) sale igual.
        foreach (var entry in Board.Where(b => Tables.All(t => t.Id != b.Service.TableId)))
            tiles.Add(new TableTile(new TableChoice(entry.Service.TableId, Names.Table(entry.Service.TableId), 0), entry, "~otras", "Otras mesas ocupadas",
                Names.Table(entry.Service.TableId), OfferName(entry.Service), now));
        Tiles = tiles.ToArray();

        var zones = Tiles.GroupBy(t => t.ZoneId).Select(g => (Id: g.Key, Name: g.First().ZoneName, Count: g.Count())).ToArray();
        if (SelectedZone.Length > 0 && zones.All(z => z.Id != SelectedZone)) { SelectedZone = ""; return; }   // OnSelectedZoneChanged rehace
        var tabIds = zones.Length < 2 ? [] : new[] { "" }.Concat(zones.Select(z => z.Id)).ToArray();
        if (!tabIds.SequenceEqual(ZoneTabs.Select(t => t.Id))) ZoneTabs = tabIds.Select(id => new ZoneTab(id)).ToArray();
        foreach (var tab in ZoneTabs)
            tab.Label = tab.Id.Length == 0 ? $"Todas · {Tiles.Length}" : zones.First(z => z.Id == tab.Id) is var zone ? $"{zone.Name} · {zone.Count}" : "";
        var inZone = Tiles.Where(t => SelectedZone.Length == 0 || t.ZoneId == SelectedZone).ToArray();
        bool Matches(TableTile t, string f) => f switch
        {
            "busy" => t.Occupied, "kitchen" => t.State == FloorPlan.Kitchen, "ready" => t.State == FloorPlan.Ready, "free" => !t.Occupied, _ => true
        };
        foreach (var chip in Filters) chip.Label = $"{chip.Name} · {inZone.Count(t => Matches(t, chip.Id))}";
        Groups = inZone.Where(t => Matches(t, Filter)).GroupBy(t => t.ZoneId)
            .Select(g => new TileGroup(g.First().ZoneName, ZoneSummary(Tiles.Where(t => t.ZoneId == g.Key).ToArray()), g.ToArray())).ToArray();
        var occupied = Tiles.Where(t => t.Occupied).ToArray();
        FloorSummary = Tiles.Length == 0 ? "" : $"{occupied.Length} de {Labels.Plural(Tiles.Length, "mesa ocupada", "mesas ocupadas")} · {Labels.Plural(occupied.Sum(t => t.Entry!.Service.Pax), "persona", "personas")}";
        FloorEmpty = Groups.Length > 0 ? "" : Tiles.Length == 0 ? "No hay mesas activas. Créalas en Salas, mesas y estaciones." : "Ninguna mesa coincide con este filtro.";
        MarkSelection();
    }
    private static string ZoneSummary(TableTile[] zone) => $"{Labels.Plural(zone.Length, "mesa", "mesas")} · {Labels.Plural(zone.Count(t => t.Occupied), "ocupada", "ocupadas")}";

    private void MarkSelection()
    {
        foreach (var tile in Tiles)
            tile.IsSelected = OpenForm ? tile.Table.Id == SelectedTable?.Id && (!tile.Occupied || Tiles.Count(t => t.Table.Id == tile.Table.Id) == 1)
                : tile.Entry is not null && tile.Entry.Service.Id == SelectedEntry?.Service.Id;
    }

    // ---- C2: ficha (detalle de la mesa ocupada elegida, o formulario de abrir mesa) ----
    public bool ShowOpen => OpenForm;
    public bool ShowDetail => !OpenForm && SelectedEntry is not null;
    public bool ShowEmpty => !OpenForm && SelectedEntry is null;
    public string EmptyText => Board.Length == 0 ? "Ninguna mesa ocupada. Elige una mesa libre en el plano para abrirla." : "Elige una mesa en el plano.";
    public string DetailTitle => Names.Table((Context?.Data ?? SelectedEntry?.Service)?.TableId);
    public string DetailZoneOffer
    {
        get
        {
            if (Context?.Data is not { } d) return "";
            var zone = Tables.FirstOrDefault(t => t.Id == d.TableId)?.ZoneName ?? "";
            var offer = d.OfferId is not { } id ? "" : Offers.FirstOrDefault(o => o.Id == id)?.Name ?? id;
            return string.Join(" · ", new[] { zone, offer }.Where(p => p.Length > 0));
        }
    }
    public string DetailState => Context?.Data is { } d ? Labels.TableState(FloorPlan.State(d)) : "";
    public string DetailTone => Context?.Data is { } d ? ToneOf(FloorPlan.State(d)) : "other";
    public string DetailInfo
    {
        get
        {
            if (Context?.Data is not { } d) return "";
            var people = Labels.Plural(d.Pax, "persona", "personas");
            var since = FloorPlan.Since(d);
            var elapsed = since is { } start ? $" ({Labels.Minutes(shell.Clock() - start)})" : "";
            return FloorPlan.State(d) switch
            {
                FloorPlan.Kitchen when since is { } at => $"{people} · en cocina desde las {Labels.Clock(at)}{elapsed}",
                FloorPlan.Ready when since is { } at => $"{people} · listo para servir desde las {Labels.Clock(at)}{elapsed}",
                FloorPlan.Eating when since is { } at => $"{people} · último pase servido a las {Labels.Clock(at)}{elapsed}",
                _ => people
            };
        }
    }
    public string RestrictionsTitle => HasRestrictions ? "Restricciones de los comensales" : "Sin restricciones declaradas";
    public bool RestrictionsAlarming => RestrictionsPendingAck || Restrictions.Any(r => r.Alarming);
    public string CoursesTitle => Courses.Length == 0 ? "Pases" : $"Pases · {Courses.Count(c => FloorPlan.Step(c) == "done")} de {Courses.Length} hechos";
    public string PreparationsTitle => SelectedCourse is { } course ? "Elaboraciones · " + course.Name : "Elaboraciones";
    public bool HasPreparations => PreparationRows.Length > 0;
    // Formulario de abrir mesa: la mesa elegida con su sala, sus plazas y su estado en la ultima lectura (el servidor decide).
    public string OpenTitle => SelectedTable?.Name ?? "Abrir mesa";
    public string OpenInfo => SelectedTable is { } t ? string.Join(" · ", new[] { t.ZoneName ?? "", Labels.Plural(t.Capacity, "plaza", "plazas") }.Where(p => p.Length > 0)) : "";
    private TableTile? OpenTile => SelectedTable is { } t ? Tiles.FirstOrDefault(x => x.Table.Id == t.Id) : null;
    public string OpenState => OpenTile?.StateLabel ?? Labels.TableState(FloorPlan.Free);
    public string OpenTone => OpenTile?.Tone ?? FloorPlan.Free;
    public string OpenWarning => OpenTile is { Occupied: true } ? "Según la última lectura esta mesa está ocupada: el servidor no la abrirá otra vez." : "";
    public string OpenButtonText => SelectedTable is { } table ? "Abrir " + table.Name : "Abrir mesa";
    // Salir del formulario vuelve a la ficha de la mesa ocupada que estaba elegida (la lectura de siempre sigue ahi).
    public string BackText => SelectedEntry is { } entry ? "Volver a " + Names.Table(entry.Service.TableId) : "";
    // Tono de color de un estado de mesa (Tag de los chips); un estado que no se conoce queda neutro, con su texto tal cual.
    internal static string ToneOf(string state) => state is FloorPlan.Free or FloorPlan.Seated or FloorPlan.Kitchen or FloorPlan.Ready or FloorPlan.Eating
        or FloorPlan.Paused or FloorPlan.Finished or FloorPlan.Cancelled ? state : "other";

    private void UpdatePanel()
    {
        foreach (var name in new[] { nameof(ShowOpen), nameof(ShowDetail), nameof(ShowEmpty), nameof(EmptyText), nameof(DetailTitle), nameof(DetailZoneOffer),
                     nameof(DetailState), nameof(DetailTone), nameof(DetailInfo), nameof(CoursesTitle), nameof(OpenTitle), nameof(OpenInfo), nameof(OpenState),
                     nameof(OpenTone), nameof(OpenWarning), nameof(OpenButtonText), nameof(BackText), nameof(HasContext), nameof(PrimaryText) })
            OnPropertyChanged(name);
        ViewAccountCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCourseRows()
    {
        var pax = Dining?.Data.Pax ?? SelectedEntry?.Service.Pax ?? 0;
        CourseRows = Courses.Select((course, index) =>
        {
            var tone = FloorPlan.Step(course);
            var state = course.State == "Skipped" ? Labels.Course(course.State) : Labels.Course(course, pax);
            if (tone == "pending" && state == "Falta elegir plato") tone = "choose";
            return new CourseRow(course, index + 1, state, tone, FloorPlan.CourseDetail(course, pax));
        }).ToArray();
        OnPropertyChanged(nameof(CoursesTitle));
    }
    private void UpdatePreparationRows()
    {
        PreparationRows = Preparations.Select(p => new PreparationRow(p, Names.Station(p.StationId),
            string.Join(" · ", new[] { Names.Station(p.StationId), p.GuestLabel, p.Quantity > 1 ? "× " + p.Quantity : "" }.Where(x => x.Length > 0)))).ToArray();
        OnPropertyChanged(nameof(HasPreparations));
    }
}
