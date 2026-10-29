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
        StateLabel = FloorPlan.Label(State);   // un estado del servidor que no se conoce, tal cual (nunca rotulado como uno parecido)
        Tone = ServiceViewModel.ToneOf(State);
        PeopleText = service is null ? Labels.Plural(table.Capacity, "plaza", "plazas") : Labels.Plural(service.Pax, "persona", "personas");
        // Libre: sus plazas; ocupada: sus personas y la oferta con la que se abrio.
        Detail = service is null || offerName.Length == 0 ? PeopleText : PeopleText + " · " + offerName;
        Elapsed = FloorPlan.Elapsed(service, now);
        Alert = FloorPlan.Alert(service);
        // El mini plano de Inicio parte el aviso solo por su sentido: la palabra en una linea y "por revisar" en la siguiente.
        AlertWord = FloorPlan.AlertWord(service);
        AlertNote = service?.RestrictionsPendingAck == true ? FloorPlan.PendingReview : "";
        AlertAlarming = FloorPlan.AlertIsAlarming(service);
        Steps = FloorPlan.Steps(service);
        Progress = FloorPlan.Progress(service);
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
    public string AlertWord { get; }
    public string AlertNote { get; }
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
// La pestana pintada como elegida sale de IsCurrent (= SelectedZone), no de la seleccion de la lista: al rehacer las pestanas (salir del
// modo responsable vacia el plano) la lista pierde su seleccion y no la recupera, y ninguna sala se veia elegida.
public sealed partial class ZoneTab(string id) : ObservableObject
{ public string Id { get; } = id; [ObservableProperty] private string label = ""; [ObservableProperty] private bool isCurrent; }
public sealed partial class FilterChip(string id, string name) : ObservableObject
{ public string Id { get; } = id; public string Name { get; } = name; [ObservableProperty] private string label = name; }
// Grupo de tarjetas por sala, fila de pase y boton de accion.
public sealed record TileGroup(string Name, string Summary, TableTile[] Tiles);
// E4e: Alert = cuantos platos (agrupados, DishGroups) del pase tienen algo por revisar en cocina (rojo y escrito; vacio si ninguno).
public sealed record CourseRow(CourseDto Course, int Number, string State, string Tone, string Detail, string Alert = "")
{ public bool HasAlert => Alert.Length > 0; }
public sealed record ActionButton(string Action, string Text, bool Danger);
// E4e (plan 2026-10-15, seccion 4.5): un plato AGRUPADO del pase elegido («Ostra × 7», DishGroups) con, debajo, la linea de cada comensal
// con restriccion o con decision de cocina («Ostra · Comensal 3 · ⚠ ALERGIA marisco (grave) · Adaptado: vieira» o «· Por revisar en
// cocina»). Alarm = en rojo (por revisar o restriccion alarmante); si no, en ambar. Solo presentacion.
public sealed record DishLine(PreparationDto Preparation, string Text, bool Alarm);
public sealed record DishRow(DishGroup Group, string Title, string Station, string State, DishLine[] Lines)
{ public bool HasLines => Lines.Length > 0; }
// E4e: una linea por revisar del plato elegido. Se marcan las que reciben la decision: UNA orden review-preparations con la decision y la
// nota en cada una. Por defecto, todas marcadas si llevan las mismas restricciones y ninguna si no (revision; UpdateReviewLines).
public sealed partial class ReviewLine(PreparationDto preparation, string text) : ObservableObject
{
    public PreparationDto Preparation { get; } = preparation;
    public string Text { get; } = text;
    [ObservableProperty] private bool isChecked = true;
}
// E4e (plan 2026-10-15, seccion 4.5): una fila de «¿Alergias o intolerancias?» al abrir mesa. El comensal es EXPLICITO: «Toda la mesa» es
// una opcion mas y nunca la de por defecto (sin elegir, la mesa no se abre); Alergia y Grave por defecto. Viajan en la MISMA orden open.
// C3e.1 (usabilidad 28-10, H51): la fila de anotar una restriccion en una mesa YA abierta (DeclareDraft) es una de estas, con las mismas
// reglas: nada se hereda de la anotacion anterior ni de otra mesa.
public sealed partial class AllergyDraft : ObservableObject
{
    [ObservableProperty] private string? guest;   // null = sin elegir; "all" = toda la mesa; "1".."n" = ese comensal
    [ObservableProperty] private string kind = "allergy";
    [ObservableProperty] private string substance = "";
    [ObservableProperty] private string severity = "severe";
}

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
    // E4e: los platos agrupados del pase elegido; el elegido es el que reciben «Empezar plato», «Marcar plato listo» y la revision.
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasDishes))] private DishRow[] dishRows = [];
    [ObservableProperty] private DishRow? selectedDish;
    [ObservableProperty] private ReviewLine[] reviewLines = [];
    // E4e: el apartado «¿Alergias o intolerancias?» de abrir mesa (filas en memoria, viajan en la orden open) y los comensales que se ofrecen.
    public System.Collections.ObjectModel.ObservableCollection<AllergyDraft> OpenAllergies { get; } = [];
    [ObservableProperty] private GuestChoice[] guestChoices = GuestsFor("2");
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
    // C3e.1 (usabilidad 28-10, H51): «Declarar una restricción» en la mesa leida es la MISMA fila que el alta (AllergyDraft): «¿Para quién?»
    // sin elegir, Alergia y Grave de partida. Antes conservaba comensal, tipo y gravedad de la anotacion anterior (tambien de otra mesa)
    // y un comensal vacio contaba como toda la mesa. Se rehace entera tras una declaracion confirmada o que queda sin confirmar (la orden
    // pendiente ya la lleva, como en Sala), al cambiar de mesa y en Invalidate; solo un rechazo la conserva para corregirla. Los comensales
    // que ofrece son los de la mesa leida (1..personas y «Toda la mesa» al final).
    [ObservableProperty] private AllergyDraft declareDraft = new();
    [ObservableProperty] private GuestChoice[] declareGuestChoices = [];
    [ObservableProperty] private string reviewNote = "";
    [ObservableProperty] private string serviceTitle = "Selecciona una mesa";

    // ---- C2: plano de mesas ----
    // Si el tablero se ha leido con esta sesion: distingue "sin leer todavia" de "leido y vacio" (Inicio no presenta un comedor sin leer
    // como un comedor vacio ni dice "nada por revisar" sin haberlo leido). Una lectura fallida despues conserva la ultima buena.
    [ObservableProperty] private bool boardRead;
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
    // C3b: «Añadir consumicion» y «Lo que lleva la mesa» (DrinksViewModel) usan el MISMO contexto leido (F01) y la entrada del tablero
    // de esa mesa (sus consumos). Se crea al pedirlo: el cascaron construye sus vistas antes de conectar.
    private DrinksViewModel? drinks;
    public DrinksViewModel Drinks => drinks ??= new(shell, this);
    internal Versioned<DiningDto>? ReadContext => Context;
    internal BoardEntry? ReadEntry => Context is not null ? SelectedEntry : null;
    internal string TableName(Versioned<DiningDto> context) => TableOf(context);

    // Unico punto de verdad para la habilitacion: la accion esta en la lista que envio el servidor.
    // E4e: las acciones de cocina van por PLATO AGRUPADO con las ordenes de grupo, que el servidor anuncia en el pase (ya filtradas por rol
    // y estacion) y que llevan solo las elaboraciones del plato que anuncian la individual: sin ninguna, no hay boton.
    private bool Allowed(string action) => action switch
    {
        "start" or "fire-next" or "pause" or "resume" or "complete" or "cancel-unstarted"
        or "declare-restriction" or "remove-restriction"
            => Context?.Data.Actions?.Contains(action) == true,
        "ready" or "serve" or "skip" or "choose" or "add-dish" => Context is not null && SelectedCourse?.Actions?.Contains(action) == true,
        "preparations-start" or "preparations-ready"
            => Context is not null && SelectedCourse?.Actions?.Contains(action) == true && SelectedDish?.Group.Announcing(Single(action)).Length > 0,
        "review-preparations" => Context is not null && SelectedCourse?.Actions?.Contains(action) == true && ReviewLines.Length > 0,
        "release" => Context is not null && SelectedEntry?.Occupancy.Actions?.Contains(action) == true,
        _ => false
    };
    // La accion individual de cada orden de grupo (la que anuncia cada elaboracion).
    private static string Single(string group) => group switch
    {
        "preparations-start" => "preparation-start", "preparations-ready" => "preparation-ready", "review-preparations" => "review-preparation", _ => group
    };
    public bool CanAction(string action) => shell.Writable && Allowed(action);

    // ---- C2: acciones de la ficha. Cual va en grande es presentacion: la primera ANUNCIADA de esta lista (servir lo que espera en el
    // pase, validarlo, enviar el siguiente, empezar...). El resto de las anunciadas, debajo. Ninguna se habilita aqui: ActCommand.
    private static readonly string[] PrimaryOrder = ["serve", "ready", "fire-next", "start", "resume", "complete", "release"];
    private static readonly string[] SecondaryOrder = ["serve", "ready", "fire-next", "start", "resume", "pause", "skip", "complete", "cancel-unstarted", "release"];
    private static readonly string[] ReasonActions = ["pause", "skip", "cancel-unstarted", "release", "remove-restriction"];
    // E4e (ADR-016): mientras el pase que va a salir espera la revision de cocina, «Iniciar servicio» no va en grande aunque el servidor
    // lo anuncie con la mesa Sentados (queda debajo): el boton grande se apaga y dice «Esperando la revisión de cocina». Presentacion.
    public string? PrimaryAction => PrimaryOrder.Where(a => !(a == "start" && WaitsForReview)).FirstOrDefault(Allowed);
    public string PrimaryText => PrimaryAction is { } action ? ActionText(action) : WaitingText();
    private bool WaitsForReview => FloorPlan.WaitsForReview(Context?.Data);
    [ObservableProperty] private ActionButton[] secondaryActions = [];
    public bool ReasonNeeded => ReasonActions.Any(Allowed);
    public bool PreparationActionsAnnounced => Allowed("preparations-start") || Allowed("preparations-ready");
    public bool ReviewAnnounced => Allowed("review-preparations");
    public bool HasContext => Context is not null;
    private string ActionText(string action) => action switch
    {
        "serve" or "ready" or "skip" when SelectedCourse is { } course => Labels.Action(action) + " · " + course.Name,
        "fire-next" when Courses.FirstOrDefault(c => c.State == "Pending") is { } next => Labels.Action(action) + " · " + next.Name,
        _ => Labels.Action(action)
    };
    // Sin ninguna accion principal anunciada se dice que se espera (el boton grande queda deshabilitado). E4e: la revision de cocina solo se
    // espera si es del pase que importa ahora (con un pase en cocina y una alergia en el postre, se espera a cocina); un servidor anterior
    // a D3.5, sin marcas por elaboracion, solo da el aviso global.
    private string WaitingText() => Context?.Data is not { } dining ? ""
        : FloorPlan.WaitsForReview(dining) || (dining.RestrictionsPendingAck && !dining.Courses.Any(c => c.Preparations.Any(p => p.ReviewPending)))
            ? "Esperando la revisión de cocina"
        : FloorPlan.State(dining) == FloorPlan.Kitchen ? "Esperando a cocina"
        : "Sin acciones para esta mesa ahora";

    internal void Sync()
    {
        ActCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged(); DeclareRestrictionCommand.NotifyCanExecuteChanged();
        RemoveRestrictionCommand.NotifyCanExecuteChanged(); ReviewCommand.NotifyCanExecuteChanged(); ChooseCommand.NotifyCanExecuteChanged(); AddDishCommand.NotifyCanExecuteChanged();
        StartOpenCommand.NotifyCanExecuteChanged(); ViewAccountCommand.NotifyCanExecuteChanged(); AddAllergyCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanChoose)); OnPropertyChanged(nameof(CanAddDish));
        OnPropertyChanged(nameof(PrimaryAction)); OnPropertyChanged(nameof(PrimaryText)); OnPropertyChanged(nameof(ReasonNeeded));
        OnPropertyChanged(nameof(PreparationActionsAnnounced)); OnPropertyChanged(nameof(ReviewAnnounced)); OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(ReviewTitle));
        var secondary = SecondaryOrder.Where(a => a != PrimaryAction && Allowed(a)).Select(a => new ActionButton(a, ActionText(a), a is "release" or "cancel-unstarted")).ToArray();
        if (!secondary.SequenceEqual(SecondaryActions)) SecondaryActions = secondary;   // sin rehacer los botones en cada cambio de "ocupado"
        Drinks.Sync();
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
            UpdateDishRows();   // los nombres de las estaciones; el plato elegido se conserva (acciones de cocina)
        }
        finally { rendering = false; }
    }

    internal void SelectService(string? id) => serviceId = id;

    internal void Clear()
    {
        rendering = true;
        try
        {
            BoardRead = false;
            Board = []; SelectedEntry = null; Tables = []; Offers = []; serviceId = null; Reason = ""; stations = null;
            OpenForm = false; SelectedZone = ""; Filter = "all"; OpenAllergies.Clear(); chosenLines.Clear();
            Invalidate(); ServiceTitle = "Selecciona una mesa";
        }
        finally { rendering = false; }
        Drinks.Clear();   // C3b: la ronda sin enviar se pierde con la sesion (nunca se guarda ni se recupera como enviada, D6.1)
        UpdatePanel();
    }

    // Nada heredado de otra lectura queda accionable: detalle, pases, elaboraciones y restricciones.
    private void Invalidate()
    {
        var wasRendering = rendering; rendering = true;
        try
        {
            Dining = null; Courses = []; SelectedCourse = null; DishRows = []; SelectedDish = null; ReviewLines = [];
            Restrictions = []; SelectedRestriction = null; RestrictionsPendingAck = false; ReviewNote = "";
            ResetDeclare(); DeclareGuestChoices = [];   // C3e.1 (H51): la fila de anotar entera, no solo la sustancia
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
        var previousService = Dining?.Data.Id;
        rendering = true;
        try
        {
            BoardRead = true;
            Board = fresh; SelectedEntry = fresh.FirstOrDefault(b => b.Service.Id == serviceId);
            // C3e.1 (usabilidad 28-10, H51): la fila de anotar es de UNA mesa. Otra mesa (tambien la que se lee porque la pedida ya no esta en
            // el tablero, sin pasar por Invalidate) empieza de cero; una relectura de la misma (tiempo real) no toca lo que se esta escribiendo.
            if (current?.Data.Id != previousService) ResetDeclare();
            var guests = GuestsFor(current?.Data.Pax ?? 0);
            if (!guests.Select(c => c.Key).SequenceEqual(DeclareGuestChoices.Select(c => c.Key))) DeclareGuestChoices = guests;
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
        Drinks.OnRead();   // C3b: lo que lleva la mesa y para quien puede ser cada linea; la ronda solo se pierde si su mesa ya no es esta
    }

    private void UpdatePreparations()
    {
        UpdateDishRows();
        // E4a: platos elegibles del pase (de la oferta con la que se abrio la mesa) y resumen de las elecciones hechas.
        var course = Dining?.Data.OfferId is { } offerId ? Offers.FirstOrDefault(o => o.Id == offerId)?.Courses.FirstOrDefault(c => c.Id == SelectedCourse?.Id) : null;
        ChoiceDishes = course?.Dishes ?? []; SelectedChoiceDish = ChoiceDishes.FirstOrDefault();
        DishItems = course?.Items ?? []; SelectedDishItem = DishItems.FirstOrDefault();
        var pax = Dining?.Data.Pax ?? 0;
        var preparations = SelectedCourse?.Preparations ?? [];
        ChoicesText = SelectedCourse?.ChoiceRequired != true ? ""
            : string.Join(" · ", Enumerable.Range(1, pax).Select(g => $"{g}: " + (preparations.FirstOrDefault(p => p.GuestPosition == g)?.Name ?? "sin elegir")));
        OnPropertyChanged(nameof(PreparationsTitle));
    }

    // E4e: los platos agrupados del pase elegido. Se conserva el plato elegido (por nombre y estacion); si no, el primero con algo por
    // revisar que se pueda revisar desde aqui, el primero con trabajo de cocina anunciado o el primero.
    private void UpdateDishRows()
    {
        var previous = SelectedDish?.Group is { } chosen ? (chosen.Name, chosen.StationId) : default((string, string)?);
        DishRows = SelectedCourse is { } course ? DishGroups.Of(course).Select(group => new DishRow(group, DishGroups.Title(group), Names.Station(group.StationId),
            DishGroups.State(group), group.Exceptions.Select(p => new DishLine(p, DishGroups.ExceptionLine(p), DishGroups.ExceptionAlarming(p))).ToArray())).ToArray() : [];
        SelectedDish = DishRows.FirstOrDefault(d => previous is { } key && d.Group.Name == key.Item1 && d.Group.StationId == key.Item2)
            ?? DishRows.FirstOrDefault(d => d.Group.Announcing("review-preparation").Length > 0)
            ?? DishRows.FirstOrDefault(d => d.Group.Announcing("preparation-start").Length + d.Group.Announcing("preparation-ready").Length > 0)
            ?? DishRows.FirstOrDefault();
        UpdateReviewLines();
    }
    // Las lineas que el servidor pide revisar en el plato elegido (y anuncia a este puesto). E4e (revision): salen marcadas solo si todas
    // llevan las MISMAS restricciones (DishGroups.SameRestrictions); con restricciones distintas (una alergia grave y una intolerancia leve)
    // ninguna sale marcada y cada comensal se marca a mano, para que la nota pensada para uno no quede como decision de cocina del otro.
    // Lo marcado o desmarcado a mano se respeta en cada relectura (otro puesto, el tiempo real). Tras una revision confirmada, lo que queda
    // vuelve a su marca por defecto.
    private readonly Dictionary<string, bool> chosenLines = new(StringComparer.Ordinal);
    private void UpdateReviewLines()
    {
        var group = SelectedDish?.Group;
        var pending = group is null ? [] : group.Preparations.Where(p => p.ReviewPending && p.Actions?.Contains("review-preparation", StringComparer.Ordinal) == true).ToArray();
        var checkedByDefault = DishGroups.SameRestrictions(pending);
        ReviewLines = pending.Select(p => new ReviewLine(p, string.Join(" · ", new[] { Labels.Guest(p.GuestPosition), p.RestrictionsText }.Where(x => x.Length > 0)))
            { IsChecked = chosenLines.TryGetValue(p.Id, out var chosen) ? chosen : checkedByDefault }).ToArray();
        foreach (var line in ReviewLines)
            line.PropertyChanged += (_, _) =>
            {
                chosenLines[line.Preparation.Id] = line.IsChecked;
                ReviewCommand.NotifyCanExecuteChanged();
            };
        ReviewCommand.NotifyCanExecuteChanged();
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
    partial void OnSelectedDishChanged(DishRow? value) { if (!rendering) { UpdateReviewLines(); Sync(); } }

    private Versioned<DiningDto> Require() => Context
        ?? throw new InvalidOperationException("No hay una mesa leída correctamente que coincida con la seleccionada.");

    private bool CanAct(string? action) => action is not null && CanAction(action);
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Act(string action) => action == "release" ? Release() : shell.Run(async () =>
    {
        var context = Require();
        if (action is "pause" or "skip" or "cancel-unstarted" && string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Introduce el motivo.");
        // E4e: «Empezar plato» y «Marcar plato listo» son UNA orden de grupo con las elaboraciones del plato elegido que anuncian la
        // individual (lo que el servidor dejaria hacer una a una), con la version leida (F01).
        var dish = action is "preparations-start" or "preparations-ready" ? SelectedDish ?? throw new InvalidOperationException("Elige el plato.") : null;
        object body = dish is not null
            ? new { expectedVersion = context.Version, courseId = SelectedCourse!.Id, itemIds = dish.Group.Announcing(Single(action)) }
            : new { expectedVersion = context.Version, courseId = SelectedCourse?.Id, reason = Reason };
        await shell.Api!.SendAsync(
            "dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/" + action, body,
            Labels.Action(action) + (dish is not null ? " · " + dish.Title : "") + " · " + TableOf(context));   // C1: la orden pendiente se describe en castellano y con el nombre de la mesa
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: " + Labels.Action(action) + ".";
    });

    private bool CanOpenTable() => shell.Writable && shell.CanOpen && SelectedTable is not null && SelectedOffer is not null;
    [RelayCommand(CanExecute = nameof(CanOpenTable))]
    private Task Open() => shell.Run(async () =>
    {
        if (!int.TryParse(Pax, out var people)) throw new ArgumentException("Introduce un número de personas.");
        var restrictions = OpenRestrictions(people);
        // E4e: las alergias van en la MISMA orden open (una sola orden incierta). Sin ninguna, el cuerpo de siempre (un servidor anterior
        // rechaza el campo nuevo). menuId: alias para un servidor anterior a E4a.
        object body = restrictions.Length == 0
            ? new { tableId = SelectedTable!.Id, pax = people, offerId = SelectedOffer!.Id, menuId = SelectedOffer.Id }
            : new { tableId = SelectedTable!.Id, pax = people, offerId = SelectedOffer!.Id, menuId = SelectedOffer.Id, restrictions };
        var result = await shell.Api!.SendAsync("dining/services", body,
            "Abrir " + SelectedTable.Name + (restrictions.Length == 0 ? "" : " con " + Labels.Plural(restrictions.Length, "alergia o intolerancia", "alergias o intolerancias")));
        serviceId = result.GetProperty("serviceId").GetString();
        OpenForm = false;   // C2: la ficha pasa a la mesa recien abierta (la lectura la elige por serviceId)
        OpenAllergies.Clear();
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: abrir " + SelectedTable.Name;
    });
    // E4e: las filas de «¿Alergias o intolerancias?» como restricciones de la orden open. Solo el FORMATO de lo tecleado se comprueba aqui
    // (quien, a que); tipo y gravedad salen de sus listas y el servidor valida el resto (invalid_restriction, invalid_guest, duplicadas).
    private object[] OpenRestrictions(int people)
    {
        var rows = OpenAllergies.Where(a => a.Guest is not null || a.Substance.Trim().Length > 0).ToArray();
        foreach (var row in rows) CheckDraft(row, people);
        return rows.Select(row => (object)new { guestPosition = GuestPosition(row), kind = row.Kind, substance = row.Substance.Trim(), severity = row.Severity }).ToArray();
    }
    // El formato de UNA fila (quien, a que), con los mismos textos en el alta y al anotar en una mesa abierta (C3e.1, H51): sin elegir
    // para quien no hay orden; «Toda la mesa» solo si se elige.
    private static void CheckDraft(AllergyDraft row, int people)
    {
        if (row.Guest is null) throw new ArgumentException("Elige para quién es cada alergia o intolerancia: un comensal o toda la mesa.");
        if (row.Substance.Trim().Length < 2) throw new ArgumentException("Escribe a qué es cada alergia o intolerancia (2 caracteres o más).");
        if (row.Guest != "all" && (!int.TryParse(row.Guest, out var guest) || guest < 1 || guest > people))
            throw new ArgumentException("El comensal de una alergia tiene que estar entre 1 y " + people + ".");
    }
    // «Toda la mesa» viaja como guestPosition null; un comensal, con su numero (CheckDraft ya lo ha comprobado).
    private static int? GuestPosition(AllergyDraft row) => row.Guest == "all" ? null : int.Parse(row.Guest!, System.Globalization.CultureInfo.InvariantCulture);
    // E4e: anadir y quitar filas de alergias del formulario de abrir mesa (solo memoria: nada se envia hasta «Abrir»).
    private bool CanAddAllergy() => shell.Writable && shell.CanOpen && OpenAllergies.Count < 30;
    [RelayCommand(CanExecute = nameof(CanAddAllergy))]
    private void AddAllergy() => OpenAllergies.Add(new AllergyDraft());
    [RelayCommand]
    private void RemoveAllergy(AllergyDraft? row) { if (row is not null) OpenAllergies.Remove(row); }
    // Los comensales que se pueden elegir: 1..personas y «Toda la mesa» al final (nunca preseleccionada). Las personas se teclean: hasta
    // que son un numero valido solo queda «Toda la mesa».
    partial void OnPaxChanged(string value)
    {
        var choices = GuestsFor(value);
        if (!choices.Select(c => c.Key).SequenceEqual(GuestChoices.Select(c => c.Key))) GuestChoices = choices;
    }
    private static GuestChoice[] GuestsFor(string pax) => GuestsFor(int.TryParse(pax, out var n) ? n : 0);
    private static GuestChoice[] GuestsFor(int pax)
    {
        var people = pax is >= 1 and <= 60 ? pax : 0;
        return [..Enumerable.Range(1, people).Select(g => new GuestChoice(g.ToString(System.Globalization.CultureInfo.InvariantCulture), Labels.Guest(g))),
            new GuestChoice("all", Labels.Guest(null))];
    }

    // C3e.1 (usabilidad 28-10, H51): la fila de anotar vuelve a empezar: sin comensal elegido, Alergia, Grave y sin sustancia.
    private void ResetDeclare() => DeclareDraft = new AllergyDraft();
    private bool CanDeclareRestriction() => shell.Writable && Allowed("declare-restriction");
    [RelayCommand(CanExecute = nameof(CanDeclareRestriction))]
    private Task DeclareRestriction() => shell.Run(async () =>
    {
        var context = Require();
        // C3e.1 (usabilidad 28-10, H51): las comprobaciones y los textos de cada fila del alta (CheckDraft). Sin elegir para quien no sale
        // ninguna orden: nunca «toda la mesa» por omision.
        var draft = DeclareDraft;
        CheckDraft(draft, context.Data.Pax);
        var api = shell.Api!;
        var path = "dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/declare-restriction";
        var request = new { expectedVersion = context.Version, guestPosition = GuestPosition(draft), kind = draft.Kind,
                            substance = draft.Substance.Trim(), severity = draft.Severity };
        var body = System.Text.Json.JsonSerializer.Serialize(request, ApiClient.Json);
        try { await api.SendAsync(path, request, "Declarar restricción de comensal · " + TableOf(context)); }
        catch (Exception) when (api.Pending is { } pending && pending.Path == path && pending.Body == body)
        {
            // Revision de C3e.1 (H51): sin confirmar, lo escrito YA es la orden pendiente (su aviso la muestra y «Reintentar» la manda
            // identica): la fila vuelve a empezar, como la hoja de Sala (H03). Si se quedara escrita, la siguiente heredaria comensal, tipo y
            // gravedad tras «Reintentar», justo el fallo de H51, o la misma volveria a salir como duplicada.
            if (ReferenceEquals(DeclareDraft, draft)) ResetDeclare();
            throw;
        }
        // Confirmada por el servidor: la fila entera vuelve a empezar (la siguiente puede ser de otro comensal y otra gravedad). Un rechazo
        // (o una orden que no llego a salir) no llega aqui: la fila se conserva para corregirla.
        if (ReferenceEquals(DeclareDraft, draft)) ResetDeclare();
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

    // Revision por elaboracion (D3.5): cocina decide que pasa con ESE plato; la nota es obligatoria. E4e (ADR-016): por PLATO AGRUPADO y
    // tambien antes de enviar: la decision y la nota van a las lineas marcadas del plato elegido en UNA orden review-preparations (como
    // mucho las que admite el servidor; las demas siguen por revisar). «Rehacer» solo vale para lo enviado: el servidor lo dice
    // (nothing_to_remake) en un pase sin enviar.
    public const int MaxGroupReviews = 60;
    private bool CanReview(string? decision) => decision is not null && CanAction("review-preparations") && ReviewLines.Any(l => l.IsChecked);
    [RelayCommand(CanExecute = nameof(CanReview))]
    private Task Review(string decision) => shell.Run(async () =>
    {
        var context = Require();
        var dish = SelectedDish ?? throw new InvalidOperationException("Elige el plato a revisar.");
        var lines = ReviewLines.Where(l => l.IsChecked).Take(MaxGroupReviews).ToArray();
        if (lines.Length == 0) throw new ArgumentException("Marca al menos un comensal.");
        if (string.IsNullOrWhiteSpace(ReviewNote)) throw new ArgumentException("Indica qué se ha revisado o cambiado (nota obligatoria).");
        var note = ReviewNote.Trim();
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/review-preparations",
            new { expectedVersion = context.Version, items = lines.Select(l => new { courseId = SelectedCourse!.Id, itemId = l.Preparation.Id, decision, note }).ToArray() },
            "Revisión de cocina (" + Labels.Review(decision) + ") · " + dish.Title + " · " + Labels.Plural(lines.Length, "comensal", "comensales") + " · " + TableOf(context));
        ReviewNote = ""; chosenLines.Clear();
        await shell.RefreshAll();
        shell.Status = "Cocina ha registrado su decisión sobre " + dish.Group.Name + " (" + Labels.Plural(lines.Length, "comensal", "comensales") + "): " + Labels.Review(decision) + ".";
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
        // C3b: salir de la ficha (otra mesa, o una libre para abrirla) con una ronda sin enviar pide confirmacion; sin ronda, el selector de
        // la mesa anterior se cierra (la ronda es de la ficha leida al abrirlo, F01).
        if (tile.Entry?.Service.Id is not { } target || target != SelectedEntry?.Service.Id || OpenForm)
        {
            if (Drinks.AskLeave("cambias de mesa", () => PickTile(tile))) return;
            Drinks.Discard();
        }
        if (tile.Entry is { } entry)
        {
            OpenForm = false;
            SelectedEntry = Board.FirstOrDefault(b => b.Service.Id == entry.Service.Id) ?? entry;   // dispara la lectura de esa mesa
        }
        else
        {
            SelectedTable = Tables.FirstOrDefault(t => t.Id == tile.Table.Id) ?? SelectedTable;
            ProposeDaily(); OpenAllergies.Clear(); OpenForm = true;
        }
    }

    // Boton "Abrir mesa" de la cabecera: abre el formulario de siempre (mesa, oferta, personas) con una mesa libre ya elegida.
    private bool CanStartOpen() => shell.CanOpen && !shell.Busy;
    [RelayCommand(CanExecute = nameof(CanStartOpen))]
    private void StartOpen()
    {
        if (Drinks.AskLeave("abres otra mesa", StartOpen)) return;   // C3b: con una ronda sin enviar, primero se pregunta
        Drinks.Discard();
        if (SelectedTable is null || Board.Any(b => b.Service.TableId == SelectedTable.Id))
        {
            var free = Groups.SelectMany(g => g.Tiles).Concat(Tiles).FirstOrDefault(t => !t.Occupied);
            if (free is not null) SelectedTable = Tables.FirstOrDefault(t => t.Id == free.Table.Id) ?? SelectedTable;
        }
        ProposeDaily(); OpenAllergies.Clear(); OpenForm = true;
    }
    // E4d: al abrir mesa se propone SIEMPRE el menu del dia (la primera oferta vigente ahora, en el orden de Menus y cartas), no el
    // elegido la vez anterior. Mientras el formulario esta abierto, una relectura no toca la eleccion.
    private void ProposeDaily() { if (Offers.Length > 0) SelectedOffer = Offers[0]; }
    [RelayCommand] private void CancelOpen() { OpenForm = false; OpenAllergies.Clear(); }

    // "Ver cuenta": la cuenta de ESTA mesa en Caja (apartado del responsable: en el nivel Puesto pide la contrasena, como el menu). Si
    // Caja no la lista (cerrada y cobrada, o sin cuenta), Caja lo dice y no elige otra.
    private bool CanViewAccount() => Context is not null && shell.TabsEnabled;
    [RelayCommand(CanExecute = nameof(CanViewAccount))]
    private void ViewAccount()
    {
        if (Context is not { } context) return;
        shell.Checkout.Request(context.Data.Id, TableOf(context));
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
        // "Todas las salas" (no "Todas"): justo debajo esta el filtro "Todas" de los estados y no deben parecer el mismo control.
        foreach (var tab in ZoneTabs)
        {
            tab.Label = tab.Id.Length == 0 ? $"Todas las salas · {Tiles.Length}" : zones.First(z => z.Id == tab.Id) is var zone ? $"{zone.Name} · {zone.Count}" : "";
            tab.IsCurrent = tab.Id == SelectedZone;
        }
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
    public string DetailState => Context?.Data is { } d ? FloorPlan.Label(FloorPlan.State(d)) : "";
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
    public string PreparationsTitle => SelectedCourse is { } course ? "Platos · " + course.Name : "Platos";
    public string ReviewTitle => SelectedDish is { } dish ? "Revisión de cocina · " + dish.Title : "Revisión de cocina";
    public bool HasDishes => DishRows.Length > 0;
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
    internal static string ToneOf(string state) => FloorPlan.IsCode(state) ? state : "other";

    private void UpdatePanel()
    {
        foreach (var name in new[] { nameof(ShowOpen), nameof(ShowDetail), nameof(ShowEmpty), nameof(EmptyText), nameof(DetailTitle), nameof(DetailZoneOffer),
                     nameof(DetailState), nameof(DetailTone), nameof(DetailInfo), nameof(CoursesTitle), nameof(OpenTitle), nameof(OpenInfo), nameof(OpenState),
                     nameof(OpenTone), nameof(OpenWarning), nameof(OpenButtonText), nameof(BackText), nameof(HasContext), nameof(PrimaryText) })
            OnPropertyChanged(name);
        ViewAccountCommand.NotifyCanExecuteChanged();
        drinks?.Sync();
    }

    private void UpdateCourseRows()
    {
        var pax = Dining?.Data.Pax ?? SelectedEntry?.Service.Pax ?? 0;
        CourseRows = Courses.Select((course, index) =>
        {
            var tone = FloorPlan.Step(course);
            var state = course.State == "Skipped" ? Labels.Course(course.State) : Labels.Course(course, pax);
            if (tone == "pending" && state == "Falta elegir plato") tone = "choose";
            // E4e: lo que cocina tiene por revisar en este pase, tambien en uno sin enviar, escrito en su fila (nunca solo color). Revision:
            // cuenta PLATOS agrupados con algo por revisar (DishGroups), como el detalle («1 plato»), nunca lineas por comensal.
            var review = DishGroups.Of(course).Count(group => group.Preparations.Any(p => p.ReviewPending));
            return new CourseRow(course, index + 1, state, tone, FloorPlan.CourseDetail(course, pax),
                review == 0 ? "" : "⚠ " + Labels.Plural(review, "plato por revisar en cocina", "platos por revisar en cocina"));
        }).ToArray();
        OnPropertyChanged(nameof(CoursesTitle));
    }
}
