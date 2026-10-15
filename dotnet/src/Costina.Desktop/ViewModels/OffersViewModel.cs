using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E4a: vista "ERP › Oferta: cartas y menús" (solo main): ofertas (degustación / menú cerrado / carta) con sus pases y platos.
// Patron lista + ficha de E2/E3; cada guardado es UN comando por la tuberia habitual y despues se relee todo.
// C1: tipo, estado y estacion en castellano y por su nombre (Labels, Names); el codigo solo en el campo "Código" de cada ficha.
// C2b (plan 2026-10-14): patron del Comedor. A la izquierda, las ofertas con buscador y chips de tipo (con su recuento); cada fila con su
// tipo, servicio, pases y vigencia, y la palabra "Desactivada" si lo esta. A la derecha, la ficha de la oferta elegida: sus datos, la nota
// de su producto-menu y los pases NUMERADOS (como los del Comedor); el pase elegido se despliega en su sitio con sus platos (o los platos
// de la carta), sus botones de anadir y sus datos. Nada de eso decide: cada orden conserva su cuerpo y su CanExecute; la eleccion de
// oferta, pase, plato y plato de la carta es lo que cada orden lleva (F01): buscar o filtrar conserva la oferta elegida por su codigo
// mientras se vea y, si la deja fuera, la deselecciona del todo. Sin nada elegido la ficha dice que elegir y no muestra campos.
public sealed record OfferRow(OfferDto Offer)
{
    public string KindLabel => Labels.OfferKind(Offer.Kind);
    public string StateLabel => Labels.Active(Offer.Active, feminine: true);
    // C2b: segunda linea de la fila: tipo, servicio y pases activos. Revision: la vigencia va en su propia linea (si no, la segunda se
    // partia tras un "·" colgado y las filas con fechas median mas que las demas).
    public string Detail { get; init; } = "";
    public string Validity { get; init; } = "";
    public bool Inactive => !Offer.Active;
    public override string ToString() => $"{Offer.Name} · {KindLabel} · {StateLabel}";
}
// C2b: fila de un pase con su numero (orden de la oferta), sus platos activos por su nombre (en un menu cerrado, "A elegir: ... o ...")
// y cuantos tiene.
public sealed record OfferCourseRow(OfferCourseDto Course)
{
    public string StateLabel => Labels.Active(Course.Active, feminine: false);
    public int Number { get; init; }
    public string Detail { get; init; } = "";
    public string Count { get; init; } = "";
    public bool Inactive => !Course.Active;
    public string Description => $"Pase {Number}: {Course.Name} · {Count} · {StateLabel}";
    // En una carta los platos del grupo son sus items del catalogo.
    public override string ToString() => $"{Course.Name} · {Labels.Plural(Course.Dishes.Count(d => d.Active) + (Course.Items ?? []).Count(i => i.Active), "plato", "platos")} · {StateLabel}";
}
public sealed record OfferDishRow(OfferDishDto Dish, string StationName)
{
    public string StateLabel => Labels.Active(Dish.Active, feminine: false);
    public bool Inactive => !Dish.Active;
    public override string ToString() => $"{Dish.Name} · {StationName} · {StateLabel}";
}

public sealed record OfferItemRow(OfferItemDto Item, string StationName)
{
    public string StateLabel => Labels.Active(Item.Active, feminine: false);
    public bool Inactive => !Item.Active;
    public string Key => Item.ProductId + "/" + Item.PresentationId;
    public string Label => Item.Name + " · " + Item.Presentation;   // C2b: el plato de la carta con su presentacion
    public override string ToString() => $"{Item.Name} · {Item.Presentation} · {StationName} · {StateLabel}";
}

public sealed partial class OffersViewModel(ShellViewModel shell) : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool rendering;
    // C2b: ofertas leidas (null = sin leer todavia); la lista es su parte visible con el buscador y el chip de tipo.
    private OfferDto[]? read;
    // C2b: lo recien creado (confirmado por el servidor) queda elegido tras releer; "Cancelar" vuelve a lo elegido antes del alta.
    private string? createdOffer, createdCourse, createdDish, createdItem;
    private string? offerBeforeNew, courseBeforeNew, dishBeforeNew;
    // E4b: items de un grupo de carta y catalogo (vendibles de caja) para elegir el producto + presentacion.
    [ObservableProperty] private OfferItemRow[] items = [];
    [ObservableProperty] private OfferItemRow? selectedItem;
    [ObservableProperty] private ProductChoice? selectedItemChoice;
    [ObservableProperty] private OfferRow[] offers = [];
    [ObservableProperty] private OfferRow? selectedOffer;
    [ObservableProperty] private OfferCourseRow[] courses = [];
    [ObservableProperty] private OfferCourseRow? selectedCourse;
    [ObservableProperty] private OfferDishRow[] dishes = [];
    [ObservableProperty] private OfferDishRow? selectedDish;
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NewMenuOffer))] [NotifyPropertyChangedFor(nameof(OfferProductText))] private bool newOffer;
    [ObservableProperty] private string offerId = ""; [ObservableProperty] private string offerName = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NewMenuOffer))] [NotifyPropertyChangedFor(nameof(OfferProductText))] [NotifyPropertyChangedFor(nameof(KindHelp))] private string offerKind = "tasting";
    [ObservableProperty] private string offerService = "any"; [ObservableProperty] private string offerValidFrom = ""; [ObservableProperty] private string offerValidTo = "";
    [ObservableProperty] private string offerSort = "0"; [ObservableProperty] private string offerPrice = ""; [ObservableProperty] private string offerProductId = "";
    [ObservableProperty] private bool[] offerDays = [true, true, true, true, true, true, true];
    [ObservableProperty] private bool newCourse; [ObservableProperty] private string courseId = ""; [ObservableProperty] private string courseName = ""; [ObservableProperty] private string courseSort = "0";
    [ObservableProperty] private bool newDish; [ObservableProperty] private string dishId = ""; [ObservableProperty] private string dishName = ""; [ObservableProperty] private string dishStationId = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DishProductText))] private string dishProductId = ""; [ObservableProperty] private string dishSort = "0";
    [ObservableProperty] private string summary = "Sin datos de oferta todavía.";

    // ---- C2b: lista (buscador, chips de tipo con su recuento y vacio explicado) ----
    // Chips ESTABLES (su texto cambia en el sitio, como los filtros del Comedor): "Todas" y un tipo de oferta cada uno.
    public FilterChip[] KindFilters { get; } = [new("all", "Todas"), new("tasting", "Degustación"), new("set-menu", "Menú cerrado"), new("a-la-carte", "Carta")];
    private string kindFilter = "all";
    // Codigo del tipo que filtra la lista ("all" = todas). Null o un codigo desconocido (la lista de chips perdiendo su eleccion) no cambian el filtro.
    public string? KindFilter
    {
        get => kindFilter;
        set
        {
            if (value is null || value == kindFilter || !KindFilters.Any(k => k.Id == value)) return;
            kindFilter = value; OnPropertyChanged();
            if (!rendering) Refilter();
        }
    }
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool showListTools;
    [ObservableProperty] private bool showListEmpty;
    [ObservableProperty] private string listEmptyHeading = "";
    [ObservableProperty] private string listEmptyText = "";
    [ObservableProperty] private bool showCreateOffer;
    [ObservableProperty] private bool showShowAll;

    // ---- C2b: ficha de la oferta (solo de lo leido y de lo elegido) ----
    [ObservableProperty] private bool hasOfferContext;
    [ObservableProperty] private string emptyHeading = "";
    [ObservableProperty] private string emptyText = "";
    [ObservableProperty] private string offerContext = "";
    [ObservableProperty] private string offerTitle = "";
    [ObservableProperty] private string offerStateText = "";
    [ObservableProperty] private string offerStateTone = "other";
    [ObservableProperty] private string offerSaveText = "Guardar oferta";
    [ObservableProperty] private bool offerDeactivates;
    [ObservableProperty] private bool offerReactivates;
    // Pases numerados: los de antes del elegido, el elegido (desplegado en su sitio) y los de despues; con un alta de pase, ninguno.
    [ObservableProperty] private bool showCourses;
    [ObservableProperty] private string coursesTitle = "";
    [ObservableProperty] private bool coursesEmpty;
    [ObservableProperty] private OfferCourseRow[] coursesBefore = [];
    [ObservableProperty] private OfferCourseRow[] coursesAfter = [];
    [ObservableProperty] private bool hasSelectedCourse;
    [ObservableProperty] private bool courseDeactivates;
    [ObservableProperty] private bool courseReactivates;
    // Platos del pase elegido (o platos de la carta) y la ficha del plato elegido o nuevo.
    [ObservableProperty] private string dishesTitle = "";
    [ObservableProperty] private bool dishesEmpty;
    [ObservableProperty] private string dishesEmptyText = "";
    [ObservableProperty] private bool hasDishEditor;
    [ObservableProperty] private string dishSaveText = "Guardar plato";
    [ObservableProperty] private bool dishDeactivates;
    [ObservableProperty] private bool dishReactivates;
    [ObservableProperty] private bool itemDeactivates;
    [ObservableProperty] private bool itemReactivates;

    public bool Visible { get; set; }
    public bool HasOffers => Offers.Length > 0;
    public bool HasCourses => Courses.Length > 0;
    public bool IsCarte => SelectedOffer?.Offer.Kind == "a-la-carte";
    public bool IsNotCarte => !IsCarte;
    public bool ItemEditable => Main && IsCarte && SelectedCourse is not null && !NewCourse && SelectedItemChoice is not null;
    public string OfferFormTitle => NewOffer ? "Nueva oferta" : SelectedOffer is null ? "Selecciona una oferta" : "Oferta: " + SelectedOffer.Offer.Name;
    public string CourseFormTitle => NewCourse ? "Nuevo pase" : SelectedCourse is null ? "Selecciona un pase" : "Pase: " + SelectedCourse.Course.Name;
    public string DishFormTitle => NewDish ? "Nuevo plato" : SelectedDish is null ? "Selecciona un plato" : "Plato: " + SelectedDish.Dish.Name;
    // Una oferta nueva que no es carta se vende como producto-menu: solo entonces se piden precio o producto existente.
    public bool NewMenuOffer => NewOffer && OfferKind != "a-la-carte";
    // C2b: el producto-menu por su nombre si es un vendible leido; si no (los productos-menu no se venden sueltos en caja), por su codigo,
    // dicho como codigo: es lo que encuentra el buscador del catalogo.
    public string OfferProductText => (NewOffer ? OfferKind == "a-la-carte" : IsCarte) ? "Carta: sin producto-menú; cada plato se cobra al pedirlo, al precio del catálogo."
        : !NewOffer && SelectedOffer?.Offer.ProductId is { Length: > 0 } p ? ((shell.Catalog.NameOfProduct(p) ?? ItemChoices.FirstOrDefault(c => c.Id == p)?.Name) is { } product
            ? $"Se vende por persona como el producto «{product}»: su precio se fija en ERP › Catálogo y tarifas."
            : $"Se vende por persona con su producto-menú del catálogo (código «{p}»): su precio se fija en ERP › Catálogo y tarifas.")
        : "Al crearla se crea también su producto-menú en el catálogo (categoría Menús).";
    // C2b: lo que significa el tipo elegido en la ficha (antes, un parrafo fijo en la cabecera de la vista).
    public string KindHelp => OfferKind switch
    {
        "tasting" => "Degustación: se sirven todos sus platos, pase a pase.",
        "set-menu" => "Menú cerrado: cada comensal elige un plato por pase antes de enviarlo a cocina.",
        "a-la-carte" => "Carta: cada pase agrupa platos del catálogo que se cobran al pedirlos.",
        _ => ""
    };
    // C2b: el producto del plato (codigo tecleado) por su nombre cuando es un vendible leido.
    public string DishProductText => DishProductId?.Trim() is not { Length: > 0 } code ? "Opcional: el producto del catálogo que corresponde al plato (para el stock)."
        : ItemChoices.FirstOrDefault(c => c.Id == code) is { } product ? "Producto del catálogo: " + product.Name : "";
    private bool Main => shell.Writable && shell.IsMain;
    public bool OfferEditable => Main && (NewOffer || SelectedOffer is not null);
    public bool CourseEditable => Main && SelectedOffer is not null && !NewOffer && (NewCourse || SelectedCourse is not null);
    public bool DishEditable => Main && SelectedCourse is not null && !NewCourse && (NewDish || SelectedDish is not null);
    public StationDto[] ActiveStations => Stations.Where(s => s.Active).ToArray();

    // E4b: vendibles de caja (producto + presentacion) para anadir a un grupo de carta. C2b: lista ESTABLE: solo se sustituye si cambia
    // su contenido y, al sustituirla, la eleccion se vuelve a dar (un desplegable cuya lista cambia pierde su eleccion y no la recupera si
    // el valor enlazado sigue igual: WPF no avisa de un valor que no cambia).
    private ProductChoice[] itemChoices = [];
    public ProductChoice[] ItemChoices
    {
        get => itemChoices;
        set
        {
            if (itemChoices.SequenceEqual(value)) return;
            var keep = SelectedItemChoice is { } chosen && value.Contains(chosen) ? chosen : value.FirstOrDefault();
            itemChoices = value; OnPropertyChanged();
            var was = rendering; rendering = true;
            try { SelectedItemChoice = null; SelectedItemChoice = keep; }
            finally { rendering = was; }
            OnPropertyChanged(nameof(OfferProductText)); OnPropertyChanged(nameof(DishProductText));
        }
    }
    // C2b: estaciones del desplegable del plato, ESTABLES por la misma razon: las activas y, solo si el plato elegido usa una desactivada,
    // esa tambien, marcada (antes la lista se rehacia en cada Sync y el desplegable salia vacio).
    private StationDto[] stationChoices = [];
    public StationDto[] StationChoices => stationChoices;
    private void RefreshStations()
    {
        var dish = NewDish ? null : SelectedDish?.Dish;
        StationDto[] fresh = [.. Stations.Where(s => s.Active || s.Id == dish?.StationId).Select(s => s.Active ? s : s with { Name = s.Name + " (desactivada)" })];
        if (stationChoices.SequenceEqual(fresh)) return;
        var value = DishStationId;   // el de antes: al cambiar la lista el desplegable puede escribir null
        stationChoices = fresh; OnPropertyChanged(nameof(StationChoices));
        DishStationId = null!; DishStationId = value;
    }
    private string DefaultStation() => ActiveStations.FirstOrDefault(s => s.Kind == "Kitchen")?.Id ?? "";

    internal void Sync()
    {
        foreach (var name in new[] { nameof(OfferEditable), nameof(CourseEditable), nameof(DishEditable), nameof(OfferFormTitle), nameof(CourseFormTitle), nameof(DishFormTitle), nameof(HasOffers), nameof(HasCourses), nameof(OfferProductText), nameof(IsCarte), nameof(IsNotCarte), nameof(ItemEditable), nameof(DishProductText) })
            OnPropertyChanged(name);
        foreach (var command in new IRelayCommand[] { StartOfferCommand, SaveOfferCommand, ToggleOfferCommand, StartCourseCommand, SaveCourseCommand, ToggleCourseCommand, StartDishCommand, SaveDishCommand, ToggleDishCommand, AddItemCommand, ToggleItemCommand,
            CancelOfferCommand, CancelCourseCommand, CancelDishCommand })
            command.NotifyCanExecuteChanged();
        Compose();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            read = null; Offers = []; Courses = []; Dishes = []; Items = []; ItemChoices = []; Stations = []; SelectedOffer = null; SelectedCourse = null; SelectedDish = null; SelectedItem = null; SelectedItemChoice = null;
            NewOffer = NewCourse = NewDish = false; Summary = "Sin datos de oferta todavía."; Search = ""; kindFilter = "all"; OnPropertyChanged(nameof(KindFilter));
            createdOffer = createdCourse = createdDish = createdItem = offerBeforeNew = courseBeforeNew = dishBeforeNew = null;
            LabelFilters();
        }
        finally { rendering = false; }
        RefreshStations();
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var offers = await shell.Api.GetAsync<OfferDto[]>("erp/offers");
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        ItemChoices = await shell.Api.GetAsync<ProductChoice[]>("checkout/catalog");   // E4b: producto + presentacion para los items de una carta
        Render(offers, organization.Stations);
    }

    // Tambien lo usan los checks del WPF sin servidor: la pantalla se compone solo de lo leido.
    public void Render(OfferDto[] value, StationDto[] stations)
    {
        rendering = true;
        try
        {
            var offer = Take(ref createdOffer) ?? SelectedOffer?.Offer.Id; var course = Take(ref createdCourse) ?? SelectedCourse?.Course.Id;
            var dish = Take(ref createdDish) ?? SelectedDish?.Dish.Id; var item = Take(ref createdItem) ?? SelectedItem?.Key;
            read = value; Stations = stations;
            LabelFilters();
            // Una lista rehecha pierde su eleccion y no la recupera si la fila nueva es igual a la de antes (los registros comparan por valor
            // y la propiedad no avisa): la eleccion se vuelve a dar, primero vacia y despues la de siempre. Sin eleccion previa (y sin alta en
            // curso) se elige la primera oferta, como siempre.
            Offers = Rows();
            SelectedOffer = null; SelectedOffer = Offers.FirstOrDefault(o => o.Offer.Id == offer) ?? (NewOffer ? null : Offers.FirstOrDefault());
            // El pase, el plato y el plato de la carta se conservan por su codigo solo dentro de la MISMA oferta y el mismo pase (F01).
            var sameOffer = offer is not null && SelectedOffer?.Offer.Id == offer;
            RenderCourses();
            SelectedCourse = NewCourse ? null : Courses.FirstOrDefault(c => sameOffer && c.Course.Id == course) ?? Courses.FirstOrDefault();
            var sameCourse = sameOffer && course is not null && SelectedCourse?.Course.Id == course;
            RenderDishes();
            SelectedDish = null; SelectedDish = NewDish || !sameCourse ? null : Dishes.FirstOrDefault(d => d.Dish.Id == dish);
            SelectedItem = null; SelectedItem = sameCourse ? Items.FirstOrDefault(i => i.Key == item) : null;
            var active = value.Count(o => o.Active); var inactive = value.Length - active;
            Summary = value.Length == 0 ? "Aún no hay ofertas: crea la primera con «Nueva oferta»."
                : $"{Labels.Plural(active, "oferta activa", "ofertas activas")}{(inactive > 0 ? " · " + Labels.Plural(inactive, "desactivada", "desactivadas") : "")} · los menús se cobran por persona y la carta, plato a plato";
        }
        finally { rendering = false; }
        if (!NewOffer) FillOfferForm(); if (!NewCourse) FillCourseForm(); if (!NewDish) FillDishForm(); else RefreshStations();
        Sync();
    }
    private static string? Take(ref string? created) { var value = created; created = null; return value; }
    private void RenderCourses()
    {
        var offer = SelectedOffer?.Offer;
        Courses = offer is null ? [] : offer.Courses.Select((c, i) => new OfferCourseRow(c) { Number = i + 1, Detail = CourseDetail(offer, c), Count = CourseCount(offer, c) }).ToArray();
    }
    private void RenderDishes()
    {
        // Estacion por su nombre (tambien las desactivadas); sin nombre conocido, el codigo.
        var names = new Names(stations: Stations);
        Dishes = SelectedCourse is null ? [] : SelectedCourse.Course.Dishes.Select(d => new OfferDishRow(d, names.Station(d.StationId))).ToArray();
        Items = SelectedCourse is null ? [] : (SelectedCourse.Course.Items ?? []).Select(i => new OfferItemRow(i, i.StationId is null ? "sin estación" : names.Station(i.StationId))).ToArray();
    }

    // ---- C2b: buscador y chips de tipo ----
    // Busca por nombre (sin distinguir mayusculas ni tildes: "costina" encuentra la Degustacion Costina) o por codigo.
    private bool Shown(OfferDto offer, bool byKind = true) => (!byKind || kindFilter == "all" || offer.Kind == kindFilter) && Matches(offer, Search.Trim());
    private static bool Matches(OfferDto offer, string text) => TextSearch.Contains(offer.Name, text) || offer.Id.Contains(text, StringComparison.OrdinalIgnoreCase);
    private OfferRow[] Rows() => read is null ? [] : read.Where(o => Shown(o)).Select(o => new OfferRow(o) { Detail = OfferDetail(o), Validity = Capital(Validity(o.ValidFrom, o.ValidTo)) }).ToArray();
    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    // Recuento de cada chip dentro de lo que deja ver la busqueda.
    private void LabelFilters()
    {
        var found = read?.Where(o => Shown(o, byKind: false)).ToArray() ?? [];
        foreach (var chip in KindFilters) chip.Label = read is null ? chip.Name : $"{chip.Name} · {found.Count(o => chip.Id == "all" || o.Kind == chip.Id)}";
    }
    partial void OnSearchChanged(string value) { if (!rendering) Refilter(); }
    // E3.2/C2b: buscar o filtrar vuelve a elegir la MISMA oferta por su codigo (pases y platos siguen como estaban); si la deja fuera, se
    // deselecciona del todo: ficha, pases y platos limpios, nunca una ficha de una oferta que ya no esta elegida.
    private void Refilter()
    {
        var offer = SelectedOffer?.Offer.Id;
        rendering = true;
        try
        {
            LabelFilters();
            Offers = Rows();
            SelectedOffer = null; SelectedOffer = Offers.FirstOrDefault(o => o.Offer.Id == offer);
            if (offer is not null && SelectedOffer is null) { RenderCourses(); SelectedCourse = null; RenderDishes(); SelectedDish = null; SelectedItem = null; }
        }
        finally { rendering = false; }
        if (offer is not null && SelectedOffer is null)
        {
            if (!NewOffer) FillOfferForm();
            NewCourse = NewDish = false; FillCourseForm(); FillDishForm();
        }
        Sync();
    }
    // "Ver todas las ofertas": sin busqueda ni tipo (la eleccion se conserva).
    [RelayCommand]
    private void ShowAll()
    {
        rendering = true;
        try { Search = ""; kindFilter = "all"; OnPropertyChanged(nameof(KindFilter)); }
        finally { rendering = false; }
        Refilter();
    }

    partial void OnSelectedOfferChanged(OfferRow? value)
    {
        if (rendering) return;
        NewOffer = false; FillOfferForm();
        rendering = true; try { RenderCourses(); SelectedCourse = Courses.FirstOrDefault(); RenderDishes(); SelectedDish = null; SelectedItem = null; } finally { rendering = false; }
        NewCourse = false; FillCourseForm(); NewDish = false; FillDishForm(); Sync();
    }
    partial void OnSelectedCourseChanged(OfferCourseRow? value)
    {
        if (rendering) return;
        NewCourse = false; FillCourseForm();
        rendering = true; try { RenderDishes(); SelectedDish = null; SelectedItem = null; } finally { rendering = false; }
        NewDish = false; FillDishForm(); Sync();
    }
    partial void OnSelectedDishChanged(OfferDishRow? value) { if (rendering) return; NewDish = false; FillDishForm(); Sync(); }
    partial void OnSelectedItemChanged(OfferItemRow? value) { if (!rendering) Sync(); }
    partial void OnSelectedItemChoiceChanged(ProductChoice? value) { if (!rendering) Sync(); }
    // C2b: pulsar la fila de un pase lo elige (solo la seleccion, como una tarjeta del Comedor): se despliega con sus platos.
    [RelayCommand]
    private void PickCourse(OfferCourseRow? row) { if (row is not null) SelectedCourse = row; }

    private void FillOfferForm()
    {
        var o = SelectedOffer?.Offer;
        OfferId = o?.Id ?? ""; OfferName = o?.Name ?? ""; OfferKind = o?.Kind ?? "tasting"; OfferService = o?.Service ?? "any";
        OfferValidFrom = DayText(o?.ValidFrom); OfferValidTo = DayText(o?.ValidTo); OfferSort = (o?.Sort ?? 0).ToString(); OfferProductId = o?.ProductId ?? ""; OfferPrice = "";
        var days = o?.Weekdays ?? 127; OfferDays = Enumerable.Range(0, 7).Select(i => (days & (1 << i)) != 0).ToArray();
    }
    private void FillCourseForm() { var c = SelectedCourse?.Course; CourseId = c?.Id ?? ""; CourseName = c?.Name ?? ""; CourseSort = (c?.Sort ?? 0).ToString(); }
    private void FillDishForm()
    {
        var d = SelectedDish?.Dish;
        RefreshStations();   // antes que el valor: la lista ya lleva la estacion de lo leido
        DishId = d?.Id ?? ""; DishName = d?.Name ?? ""; DishStationId = d?.StationId ?? DefaultStation(); DishProductId = d?.ProductId ?? ""; DishSort = (d?.Sort ?? 0).ToString();
    }

    // ---- C2b: composicion de la lista y la ficha (solo presentacion: nada de esto habilita ni envia) ----
    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];
    private static string Day(string? date) => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : date ?? "";
    // Vigencia de una oferta: sin fechas, desde, hasta o entre dos fechas (una fecha que no se entiende, tal cual).
    public static string Validity(string? from, string? to) => (string.IsNullOrWhiteSpace(from), string.IsNullOrWhiteSpace(to)) switch
    {
        (true, true) => "sin fechas",
        (false, false) => $"del {Day(from)} al {Day(to)}",
        (false, true) => $"desde el {Day(from)}",
        _ => $"hasta el {Day(to)}"
    };
    private static readonly string[] DayNames = ["lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];
    // Dias de la semana de una oferta (bit 0 = lunes): "todos los dias", "de lunes a viernes" o la lista ("sabado y domingo").
    public static string DaysText(int weekdays)
    {
        var on = Enumerable.Range(0, 7).Where(i => (weekdays & (1 << i)) != 0).ToArray();
        if (on.Length == 7) return "todos los días";
        if (on.Length == 0) return "ningún día";
        if (on.Length >= 3 && on[^1] - on[0] == on.Length - 1) return $"de {DayNames[on[0]]} a {DayNames[on[^1]]}";
        var names = on.Select(i => DayNames[i]).ToArray();
        return names.Length == 1 ? "solo " + names[0] : string.Join(", ", names[..^1]) + " y " + names[^1];
    }
    private static string OfferDetail(OfferDto o) => $"{Labels.OfferKind(o.Kind)} · {Labels.OfferService(o.Service)} · {Labels.Plural(o.Courses.Count(c => c.Active), "pase", "pases")}";
    private static string[] ActiveNames(OfferDto offer, OfferCourseDto course) => offer.Kind == "a-la-carte"
        ? [.. (course.Items ?? []).Where(i => i.Active).Select(i => i.Name).Distinct()] : [.. course.Dishes.Where(d => d.Active).Select(d => d.Name)];
    private static string CourseDetail(OfferDto offer, OfferCourseDto course)
    {
        var names = ActiveNames(offer, course);
        if (names.Length == 0) return "Sin platos activos";
        if (offer.Kind == "set-menu" && names.Length > 1) return "A elegir: " + string.Join(", ", names[..^1]) + " o " + names[^1];
        return string.Join(", ", names);
    }
    private static string CourseCount(OfferDto offer, OfferCourseDto course)
    {
        var carte = offer.Kind == "a-la-carte";
        var active = carte ? (course.Items ?? []).Count(i => i.Active) : course.Dishes.Count(d => d.Active);
        var inactive = carte ? (course.Items ?? []).Count(i => !i.Active) : course.Dishes.Count(d => !d.Active);
        // Revision de C2b: en una carta el plato que ya no se ofrece es "retirado" (su chip y sus botones dicen eso), no "desactivado".
        return Labels.Plural(active, "plato", "platos") + (inactive > 0 ? " · " + (carte ? Labels.Plural(inactive, "retirado", "retirados") : Labels.Plural(inactive, "desactivado", "desactivados")) : "");
    }

    private void Compose()
    {
        var total = read?.Length ?? 0; var text = Search.Trim(); var kind = KindFilters.FirstOrDefault(k => k.Id == kindFilter);
        var filtered = text.Length > 0 || kindFilter != "all";
        ShowListTools = total > 0;
        ShowListEmpty = Offers.Length == 0;
        (ListEmptyHeading, ListEmptyText) = read is null ? ("Sin leer todavía", "Las ofertas aparecerán aquí al leerlas del servidor.")
            : total == 0 ? ("Aún no hay ofertas", "Crea la primera: una degustación, un menú cerrado o una carta.")
            : text.Length > 0 ? ("Ninguna oferta coincide", $"Ninguna oferta{(kindFilter == "all" ? "" : " de tipo " + kind?.Name)} coincide con «{text}».")
            : ("Ninguna oferta coincide", $"No hay ofertas de tipo {kind?.Name}.");
        ShowCreateOffer = read is not null && total == 0;
        ShowShowAll = ShowListEmpty && total > 0 && filtered;

        var o = NewOffer ? null : SelectedOffer?.Offer;
        HasOfferContext = NewOffer || o is not null;
        (EmptyHeading, EmptyText) = read is null ? ("Sin leer todavía", "La oferta aparecerá aquí al leerla del servidor.")
            : total == 0 ? ("Sin ofertas todavía", "Cuando crees una oferta, elígela en la lista para ver sus datos, sus pases y sus platos.")
            : ("Elige una oferta", "Pulsa una oferta de la lista para ver sus datos, sus pases y sus platos.");
        OfferContext = NewOffer ? "Alta de oferta" : o is null ? "" : $"{Labels.OfferKind(o.Kind)} · {Labels.OfferService(o.Service)} · {DaysText(o.Weekdays)}";
        OfferTitle = NewOffer ? "Nueva oferta" : o?.Name ?? "";
        OfferStateText = o is null ? "" : Cap(Labels.Active(o.Active, feminine: true));
        OfferStateTone = o?.Active == true ? "ok" : "other";
        OfferSaveText = NewOffer ? "Crear oferta" : "Guardar oferta";
        OfferDeactivates = o?.Active == true;
        OfferReactivates = o?.Active == false;

        ShowCourses = o is not null;
        CoursesTitle = $"Pases · {Courses.Length}";
        CoursesEmpty = ShowCourses && Courses.Length == 0 && !NewCourse;
        var course = NewCourse || !ShowCourses ? null : SelectedCourse;
        HasSelectedCourse = course is not null;
        var at = course is null ? -1 : Array.IndexOf(Courses, course);
        OfferCourseRow[] before = !ShowCourses ? [] : at < 0 ? Courses : Courses[..at], after = at < 0 ? [] : Courses[(at + 1)..];
        if (!CoursesBefore.SequenceEqual(before)) CoursesBefore = before;   // sin rehacer las filas en cada Sync: no pierden el foco
        if (!CoursesAfter.SequenceEqual(after)) CoursesAfter = after;
        CourseDeactivates = course?.Course.Active == true;
        CourseReactivates = course?.Course.Active == false;

        DishesTitle = IsCarte ? $"Platos de la carta · {Items.Length}" : $"Platos del pase · {Dishes.Length}";
        DishesEmpty = HasSelectedCourse && (IsCarte ? Items.Length == 0 : Dishes.Length == 0);
        DishesEmptyText = IsCarte ? "Este pase aún no tiene platos de la carta: añádelos del catálogo aquí debajo." : "Este pase aún no tiene platos: añade el primero con «Plato».";
        HasDishEditor = HasSelectedCourse && !IsCarte && (NewDish || SelectedDish is not null);
        DishSaveText = NewDish ? "Crear plato" : "Guardar plato";
        DishDeactivates = !NewDish && SelectedDish?.Dish.Active == true;
        DishReactivates = !NewDish && SelectedDish?.Dish.Active == false;
        ItemDeactivates = HasSelectedCourse && SelectedItem?.Item.Active == true;
        ItemReactivates = HasSelectedCourse && SelectedItem?.Item.Active == false;
    }

    private static int Number(string text, string what, int min, int max)
    {
        if (!int.TryParse(text.Trim(), out var value) || value < min || value > max) throw new ArgumentException($"{what}: introduce un número entre {min} y {max}.");
        return value;
    }
    private static string Code(string text) => Codes.Typed(text);
    private static string Name(string text) => text.Trim().Length == 0 ? throw new ArgumentException("El nombre es obligatorio.") : text.Trim();
    // C2b: lo elegido en un desplegable. Null = el desplegable perdio su eleccion: no se envia nada en lugar de cambiar sin querer la estacion.
    private static string Chosen(string? value, string what) => value ?? throw new ArgumentException($"Vuelve a elegir {what}: el desplegable se quedó sin elección.");
    // Revision de C2b: las fechas se escriben como se leen (dd/mm/aaaa; tambien AAAA-MM-DD); en la orden viajan como AAAA-MM-DD.
    private static string DateText(string text) => CatalogViewModel.ParseDay(text, "Fechas: dd/mm/aaaa (vacío = sin límite).");
    private static string DayText(string? iso) => iso is { Length: > 0 } ? Day(iso) : "";
    public int Weekdays => OfferDays.Select((on, i) => on ? 1 << i : 0).Sum();

    // C2b: confirmed se ejecuta solo cuando el servidor confirma la orden (antes de releer): lo recien creado queda elegido.
    private Task Command(string action, object body, string description, Action? confirmed = null) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("erp/offers/commands/" + action, body, description);
        confirmed?.Invoke();
        NewOffer = NewCourse = NewDish = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    // C2b: empezar un alta deja sin eleccion lo que se va a crear (oferta, pase o plato: la ficha es la del alta); "Cancelar" vuelve a lo
    // elegido antes. Nada se envia hasta "Crear".
    private bool CanStart() => Main;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartOffer()
    {
        offerBeforeNew = SelectedOffer?.Offer.Id;
        rendering = true; try { SelectedOffer = null; RenderCourses(); SelectedCourse = null; RenderDishes(); SelectedDish = null; SelectedItem = null; } finally { rendering = false; }
        NewOffer = true; NewCourse = NewDish = false; OfferId = ""; OfferName = ""; OfferKind = "tasting"; OfferService = "any"; OfferValidFrom = ""; OfferValidTo = ""; OfferSort = (read?.Length ?? Offers.Length).ToString();
        OfferPrice = ""; OfferProductId = ""; OfferDays = [true, true, true, true, true, true, true]; FillCourseForm(); FillDishForm(); Sync();
    }
    private bool CanStartCourse() => Main && SelectedOffer is not null && !NewOffer;
    [RelayCommand(CanExecute = nameof(CanStartCourse))]
    private void StartCourse()
    {
        courseBeforeNew = SelectedCourse?.Course.Id;
        rendering = true; try { SelectedCourse = null; RenderDishes(); SelectedDish = null; SelectedItem = null; } finally { rendering = false; }
        NewCourse = true; NewDish = false; CourseId = ""; CourseName = ""; CourseSort = Courses.Length.ToString(); FillDishForm(); Sync();
    }
    private bool CanStartDish() => Main && SelectedCourse is not null && !NewCourse;
    [RelayCommand(CanExecute = nameof(CanStartDish))]
    private void StartDish()
    {
        dishBeforeNew = SelectedDish?.Dish.Id;
        rendering = true; try { SelectedDish = null; } finally { rendering = false; }
        NewDish = true; RefreshStations(); DishId = ""; DishName = ""; DishStationId = DefaultStation(); DishProductId = ""; DishSort = Dishes.Length.ToString(); Sync();
    }
    private bool CanCancelOffer() => NewOffer;
    [RelayCommand(CanExecute = nameof(CanCancelOffer))]
    private void CancelOffer()
    {
        NewOffer = false;
        if (Offers.FirstOrDefault(o => o.Offer.Id == offerBeforeNew) is { } before) SelectedOffer = before;
        else { FillOfferForm(); Sync(); }
    }
    private bool CanCancelCourse() => NewCourse;
    [RelayCommand(CanExecute = nameof(CanCancelCourse))]
    private void CancelCourse()
    {
        NewCourse = false;
        if (Courses.FirstOrDefault(c => c.Course.Id == courseBeforeNew) is { } before) SelectedCourse = before;
        else { FillCourseForm(); Sync(); }
    }
    private bool CanCancelDish() => NewDish;
    [RelayCommand(CanExecute = nameof(CanCancelDish))]
    private void CancelDish()
    {
        NewDish = false;
        if (Dishes.FirstOrDefault(d => d.Dish.Id == dishBeforeNew) is { } before) SelectedDish = before;
        else { FillDishForm(); Sync(); }
    }

    private bool CanSaveOffer() => OfferEditable;
    [RelayCommand(CanExecute = nameof(CanSaveOffer))]
    // C2b: los Guardar van en shell.Attempt (una comprobacion fallida se cuenta en la barra, no cierra el programa) y un codigo vacio se
    // genera del nombre (Codes): el de la oferta a 27 caracteres, el del pase y el del plato a 32, unicos entre sus hermanos.
    private Task SaveOffer() => shell.Attempt(() =>
    {
        var name = Name(OfferName); var id = NewOffer ? Codes.Of(OfferId, name, read?.Select(o => o.Id) ?? [], Codes.OfferMax) : SelectedOffer!.Offer.Id; var sort = Number(OfferSort, "Orden", 0, 9999);
        var validFrom = DateText(OfferValidFrom); var validTo = DateText(OfferValidTo); var weekdays = Weekdays;
        if (weekdays == 0) throw new ArgumentException("Elige al menos un día de la semana.");
        if (NewOffer)
        {
            // Una carta no tiene producto-menu (el servidor lo rechazaria): precio y producto solo viajan con un menu.
            long? priceCents = !NewMenuOffer || OfferPrice.Trim().Length == 0 ? null : CatalogViewModel.Cents(OfferPrice);
            var productId = NewMenuOffer ? OfferProductId.Trim() : "";
            // La oferta creada queda elegida y a la vista (sin busqueda ni tipo que la escondan).
            return Command("offer-create", new { id, name, kind = OfferKind, service = OfferService, validFrom, validTo, weekdays, sort, priceCents, productId }, "Crear oferta " + name,
                () => { createdOffer = id; rendering = true; try { Search = ""; kindFilter = "all"; OnPropertyChanged(nameof(KindFilter)); } finally { rendering = false; } });
        }
        return Command("offer-update", new { id, name, kind = OfferKind, service = OfferService, validFrom, validTo, weekdays, sort }, "Guardar oferta " + name);
    });
    private bool CanToggleOffer() => Main && SelectedOffer is not null && !NewOffer;
    [RelayCommand(CanExecute = nameof(CanToggleOffer))]
    private Task ToggleOffer() => Command(SelectedOffer!.Offer.Active ? "offer-deactivate" : "offer-reactivate", new { id = SelectedOffer.Offer.Id }, (SelectedOffer.Offer.Active ? "Desactivar oferta " : "Reactivar oferta ") + SelectedOffer.Offer.Name);

    private bool CanSaveCourse() => CourseEditable;
    [RelayCommand(CanExecute = nameof(CanSaveCourse))]
    private Task SaveCourse() => shell.Attempt(() =>
    {
        var offerId = SelectedOffer!.Offer.Id; var name = Name(CourseName); var id = NewCourse ? Codes.Of(CourseId, name, SelectedOffer.Offer.Courses.Select(c => c.Id)) : SelectedCourse!.Course.Id; var sort = Number(CourseSort, "Orden", 0, 9999);
        return Command(NewCourse ? "course-create" : "course-update", new { offerId, id, name, sort }, (NewCourse ? "Crear pase " : "Guardar pase ") + name, NewCourse ? () => { createdCourse = id; } : null);
    });
    private bool CanToggleCourse() => Main && SelectedCourse is not null && !NewCourse;
    [RelayCommand(CanExecute = nameof(CanToggleCourse))]
    private Task ToggleCourse() => Command(SelectedCourse!.Course.Active ? "course-deactivate" : "course-reactivate", new { offerId = SelectedOffer!.Offer.Id, id = SelectedCourse.Course.Id },
        (SelectedCourse.Course.Active ? "Desactivar pase " : "Reactivar pase ") + SelectedCourse.Course.Name);

    // E4b: items de una carta: anadir un vendible del catalogo al grupo; desactivar/reactivar.
    private bool CanAddItem() => ItemEditable;
    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItem()
    {
        var choice = SelectedItemChoice!;
        return Command("item-create", new { offerId = SelectedOffer!.Offer.Id, courseId = SelectedCourse!.Course.Id, productId = choice.Id, presentationId = choice.PresentationId, sort = Items.Length },
            $"Añadir {choice.Name} a {SelectedCourse.Course.Name}", () => { createdItem = choice.Id + "/" + choice.PresentationId; });
    }
    private bool CanToggleItem() => Main && IsCarte && SelectedCourse is not null && SelectedItem is not null;
    [RelayCommand(CanExecute = nameof(CanToggleItem))]
    private Task ToggleItem() => Command(SelectedItem!.Item.Active ? "item-deactivate" : "item-reactivate",
        new { offerId = SelectedOffer!.Offer.Id, courseId = SelectedCourse!.Course.Id, productId = SelectedItem.Item.ProductId, presentationId = SelectedItem.Item.PresentationId },
        (SelectedItem.Item.Active ? "Retirar de la carta " : "Volver a la carta ") + SelectedItem.Item.Name);

    private bool CanSaveDish() => DishEditable;
    [RelayCommand(CanExecute = nameof(CanSaveDish))]
    private Task SaveDish() => shell.Attempt(() =>
    {
        var offerId = SelectedOffer!.Offer.Id; var courseId = SelectedCourse!.Course.Id; var name = Name(DishName);
        var id = NewDish ? Codes.Of(DishId, name, SelectedCourse.Course.Dishes.Select(d => d.Id)) : SelectedDish!.Dish.Id;
        var stationId = Code(Chosen(DishStationId, "la estación de cocina")); var sort = Number(DishSort, "Orden", 0, 9999); var productId = DishProductId.Trim();
        return Command(NewDish ? "dish-create" : "dish-update", new { offerId, courseId, id, name, stationId, productId, sort }, (NewDish ? "Crear plato " : "Guardar plato ") + name, NewDish ? () => { createdDish = id; } : null);
    });
    private bool CanToggleDish() => Main && SelectedDish is not null && !NewDish;
    [RelayCommand(CanExecute = nameof(CanToggleDish))]
    private Task ToggleDish() => Command(SelectedDish!.Dish.Active ? "dish-deactivate" : "dish-reactivate", new { offerId = SelectedOffer!.Offer.Id, courseId = SelectedCourse!.Course.Id, id = SelectedDish.Dish.Id },
        (SelectedDish.Dish.Active ? "Desactivar plato " : "Reactivar plato ") + SelectedDish.Dish.Name);
}
