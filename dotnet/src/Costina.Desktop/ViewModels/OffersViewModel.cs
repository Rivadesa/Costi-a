using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E4a: vista "ERP › Oferta: cartas y menús" (solo main): ofertas (degustación / menú cerrado) con sus pases y platos.
// Patron lista + ficha de E2/E3; cada guardado es UN comando por la tuberia habitual y despues se relee todo.
public sealed record OfferRow(OfferDto Offer)
{
    public string KindLabel => Offer.Kind switch { "tasting" => "degustación", "set-menu" => "menú cerrado", "a-la-carte" => "carta", var k => k };
    public string StateLabel => Offer.Active ? "activa" : "desactivada";
    public override string ToString() => $"{Offer.Name} ({Offer.Id}) · {KindLabel} · {StateLabel}";
}
public sealed record OfferCourseRow(OfferCourseDto Course)
{
    public string StateLabel => Course.Active ? "activo" : "desactivado";
    public override string ToString() => $"{Course.Name} ({Course.Id}) · {Course.Dishes.Count(d => d.Active)} platos · {StateLabel}";
}
public sealed record OfferDishRow(OfferDishDto Dish)
{
    public string StateLabel => Dish.Active ? "activo" : "desactivado";
    public override string ToString() => $"{Dish.Name} ({Dish.Id}) · estación {Dish.StationId} · {StateLabel}";
}

public sealed partial class OffersViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    [ObservableProperty] private OfferRow[] offers = [];
    [ObservableProperty] private OfferRow? selectedOffer;
    [ObservableProperty] private OfferCourseRow[] courses = [];
    [ObservableProperty] private OfferCourseRow? selectedCourse;
    [ObservableProperty] private OfferDishRow[] dishes = [];
    [ObservableProperty] private OfferDishRow? selectedDish;
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] private bool newOffer; [ObservableProperty] private string offerId = ""; [ObservableProperty] private string offerName = ""; [ObservableProperty] private string offerKind = "tasting";
    [ObservableProperty] private string offerService = "any"; [ObservableProperty] private string offerValidFrom = ""; [ObservableProperty] private string offerValidTo = "";
    [ObservableProperty] private string offerSort = "0"; [ObservableProperty] private string offerPrice = ""; [ObservableProperty] private string offerProductId = "";
    [ObservableProperty] private bool[] offerDays = [true, true, true, true, true, true, true];
    [ObservableProperty] private bool newCourse; [ObservableProperty] private string courseId = ""; [ObservableProperty] private string courseName = ""; [ObservableProperty] private string courseSort = "0";
    [ObservableProperty] private bool newDish; [ObservableProperty] private string dishId = ""; [ObservableProperty] private string dishName = ""; [ObservableProperty] private string dishStationId = ""; [ObservableProperty] private string dishProductId = ""; [ObservableProperty] private string dishSort = "0";
    [ObservableProperty] private string summary = "Sin datos de oferta todavía.";

    public bool Visible { get; set; }
    public bool HasOffers => Offers.Length > 0;
    public bool HasCourses => Courses.Length > 0;
    public string OfferFormTitle => NewOffer ? "Nueva oferta" : SelectedOffer is null ? "Selecciona una oferta" : "Oferta " + SelectedOffer.Offer.Id;
    public string CourseFormTitle => NewCourse ? "Nuevo pase" : SelectedCourse is null ? "Selecciona un pase" : "Pase " + SelectedCourse.Course.Id;
    public string DishFormTitle => NewDish ? "Nuevo plato" : SelectedDish is null ? "Selecciona un plato" : "Plato " + SelectedDish.Dish.Id;
    public string OfferProductText => SelectedOffer?.Offer.ProductId is { Length: > 0 } p ? $"Se vende como el producto «{p}» por persona: su precio se fija en ERP › Catálogo y tarifas." : "Al crearla se crea también su producto-menú en el catálogo (categoría Menús).";
    private bool Main => shell.Writable && shell.IsMain;
    public bool OfferEditable => Main && (NewOffer || SelectedOffer is not null);
    public bool CourseEditable => Main && SelectedOffer is not null && !NewOffer && (NewCourse || SelectedCourse is not null);
    public bool DishEditable => Main && SelectedCourse is not null && !NewCourse && (NewDish || SelectedDish is not null);
    public StationDto[] ActiveStations => Stations.Where(s => s.Active).ToArray();

    internal void Sync()
    {
        foreach (var name in new[] { nameof(OfferEditable), nameof(CourseEditable), nameof(DishEditable), nameof(OfferFormTitle), nameof(CourseFormTitle), nameof(DishFormTitle), nameof(HasOffers), nameof(HasCourses), nameof(OfferProductText), nameof(ActiveStations) })
            OnPropertyChanged(name);
        foreach (var command in new IRelayCommand[] { StartOfferCommand, SaveOfferCommand, ToggleOfferCommand, StartCourseCommand, SaveCourseCommand, ToggleCourseCommand, StartDishCommand, SaveDishCommand, ToggleDishCommand })
            command.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try { Offers = []; Courses = []; Dishes = []; Stations = []; SelectedOffer = null; SelectedCourse = null; SelectedDish = null; NewOffer = NewCourse = NewDish = false; Summary = "Sin datos de oferta todavía."; }
        finally { rendering = false; }
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var offers = await shell.Api.GetAsync<OfferDto[]>("erp/offers");
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        Render(offers, organization.Stations);
    }

    // Tambien lo usan los checks del WPF sin servidor: la pantalla se compone solo de lo leido.
    public void Render(OfferDto[] value, StationDto[] stations)
    {
        rendering = true;
        try
        {
            var offer = SelectedOffer?.Offer.Id; var course = SelectedCourse?.Course.Id; var dish = SelectedDish?.Dish.Id;
            Stations = stations;
            Offers = value.Select(o => new OfferRow(o)).ToArray();
            SelectedOffer = Offers.FirstOrDefault(o => o.Offer.Id == offer) ?? Offers.FirstOrDefault();
            RenderCourses(); SelectedCourse = Courses.FirstOrDefault(c => c.Course.Id == course) ?? Courses.FirstOrDefault();
            RenderDishes(); SelectedDish = Dishes.FirstOrDefault(d => d.Dish.Id == dish);
            Summary = $"{value.Count(o => o.Active)} ofertas activas · {value.Count(o => o.Active && o.Kind == "tasting")} degustaciones · {value.Count(o => o.Active && o.Kind == "set-menu")} menús cerrados · cada una se vende como producto del catálogo por persona";
        }
        finally { rendering = false; }
        if (!NewOffer) FillOfferForm(); if (!NewCourse) FillCourseForm(); if (!NewDish) FillDishForm();
        Sync();
    }
    private void RenderCourses() => Courses = SelectedOffer is null ? [] : SelectedOffer.Offer.Courses.Select(c => new OfferCourseRow(c)).ToArray();
    private void RenderDishes() => Dishes = SelectedCourse is null ? [] : SelectedCourse.Course.Dishes.Select(d => new OfferDishRow(d)).ToArray();

    partial void OnSelectedOfferChanged(OfferRow? value)
    {
        if (rendering) return;
        NewOffer = false; FillOfferForm();
        rendering = true; try { RenderCourses(); SelectedCourse = Courses.FirstOrDefault(); RenderDishes(); SelectedDish = null; } finally { rendering = false; }
        NewCourse = false; FillCourseForm(); NewDish = false; FillDishForm(); Sync();
    }
    partial void OnSelectedCourseChanged(OfferCourseRow? value)
    {
        if (rendering) return;
        NewCourse = false; FillCourseForm();
        rendering = true; try { RenderDishes(); SelectedDish = null; } finally { rendering = false; }
        NewDish = false; FillDishForm(); Sync();
    }
    partial void OnSelectedDishChanged(OfferDishRow? value) { if (rendering) return; NewDish = false; FillDishForm(); Sync(); }

    private void FillOfferForm()
    {
        var o = SelectedOffer?.Offer;
        OfferId = o?.Id ?? ""; OfferName = o?.Name ?? ""; OfferKind = o?.Kind ?? "tasting"; OfferService = o?.Service ?? "any";
        OfferValidFrom = o?.ValidFrom ?? ""; OfferValidTo = o?.ValidTo ?? ""; OfferSort = (o?.Sort ?? 0).ToString(); OfferProductId = o?.ProductId ?? ""; OfferPrice = "";
        var days = o?.Weekdays ?? 127; OfferDays = Enumerable.Range(0, 7).Select(i => (days & (1 << i)) != 0).ToArray();
    }
    private void FillCourseForm() { var c = SelectedCourse?.Course; CourseId = c?.Id ?? ""; CourseName = c?.Name ?? ""; CourseSort = (c?.Sort ?? 0).ToString(); }
    private void FillDishForm()
    {
        var d = SelectedDish?.Dish;
        DishId = d?.Id ?? ""; DishName = d?.Name ?? ""; DishStationId = d?.StationId ?? ActiveStations.FirstOrDefault(s => s.Kind == "Kitchen")?.Id ?? ""; DishProductId = d?.ProductId ?? ""; DishSort = (d?.Sort ?? 0).ToString();
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
    private static string Name(string text) => text.Trim().Length == 0 ? throw new ArgumentException("El nombre es obligatorio.") : text.Trim();
    private static string DateText(string text)
    {
        var t = text.Trim();
        if (t.Length > 0 && !DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Fechas: AAAA-MM-DD (vacío = sin límite).");
        return t;
    }
    public int Weekdays => OfferDays.Select((on, i) => on ? 1 << i : 0).Sum();

    private Task Command(string action, object body, string description) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("erp/offers/commands/" + action, body, description);
        NewOffer = NewCourse = NewDish = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    private bool CanStart() => Main;
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartOffer() { NewOffer = true; OfferId = ""; OfferName = ""; OfferKind = "tasting"; OfferService = "any"; OfferValidFrom = ""; OfferValidTo = ""; OfferSort = Offers.Length.ToString(); OfferPrice = ""; OfferProductId = ""; OfferDays = [true, true, true, true, true, true, true]; Sync(); }
    private bool CanStartCourse() => Main && SelectedOffer is not null && !NewOffer;
    [RelayCommand(CanExecute = nameof(CanStartCourse))] private void StartCourse() { NewCourse = true; CourseId = ""; CourseName = ""; CourseSort = Courses.Length.ToString(); Sync(); }
    private bool CanStartDish() => Main && SelectedCourse is not null && !NewCourse;
    [RelayCommand(CanExecute = nameof(CanStartDish))] private void StartDish() { NewDish = true; DishId = ""; DishName = ""; DishStationId = ActiveStations.FirstOrDefault(s => s.Kind == "Kitchen")?.Id ?? ""; DishProductId = ""; DishSort = Dishes.Length.ToString(); Sync(); }

    private bool CanSaveOffer() => OfferEditable;
    [RelayCommand(CanExecute = nameof(CanSaveOffer))]
    private Task SaveOffer()
    {
        var id = Code(NewOffer ? OfferId : SelectedOffer!.Offer.Id); var name = Name(OfferName); var sort = Number(OfferSort, "Orden", 0, 9999);
        var validFrom = DateText(OfferValidFrom); var validTo = DateText(OfferValidTo); var weekdays = Weekdays;
        if (weekdays == 0) throw new ArgumentException("Elige al menos un día de la semana.");
        if (NewOffer)
        {
            long? priceCents = OfferPrice.Trim().Length == 0 ? null : CatalogViewModel.Cents(OfferPrice);
            return Command("offer-create", new { id, name, kind = OfferKind, service = OfferService, validFrom, validTo, weekdays, sort, priceCents, productId = OfferProductId.Trim() }, "Crear oferta " + id);
        }
        return Command("offer-update", new { id, name, kind = OfferKind, service = OfferService, validFrom, validTo, weekdays, sort }, "Guardar oferta " + id);
    }
    private bool CanToggleOffer() => Main && SelectedOffer is not null && !NewOffer;
    [RelayCommand(CanExecute = nameof(CanToggleOffer))]
    private Task ToggleOffer() => Command(SelectedOffer!.Offer.Active ? "offer-deactivate" : "offer-reactivate", new { id = SelectedOffer.Offer.Id }, (SelectedOffer.Offer.Active ? "Desactivar oferta " : "Reactivar oferta ") + SelectedOffer.Offer.Id);

    private bool CanSaveCourse() => CourseEditable;
    [RelayCommand(CanExecute = nameof(CanSaveCourse))]
    private Task SaveCourse()
    {
        var offerId = SelectedOffer!.Offer.Id; var id = Code(NewCourse ? CourseId : SelectedCourse!.Course.Id); var name = Name(CourseName); var sort = Number(CourseSort, "Orden", 0, 9999);
        return Command(NewCourse ? "course-create" : "course-update", new { offerId, id, name, sort }, (NewCourse ? "Crear pase " : "Guardar pase ") + id);
    }
    private bool CanToggleCourse() => Main && SelectedCourse is not null && !NewCourse;
    [RelayCommand(CanExecute = nameof(CanToggleCourse))]
    private Task ToggleCourse() => Command(SelectedCourse!.Course.Active ? "course-deactivate" : "course-reactivate", new { offerId = SelectedOffer!.Offer.Id, id = SelectedCourse.Course.Id },
        (SelectedCourse.Course.Active ? "Desactivar pase " : "Reactivar pase ") + SelectedCourse.Course.Id);

    private bool CanSaveDish() => DishEditable;
    [RelayCommand(CanExecute = nameof(CanSaveDish))]
    private Task SaveDish()
    {
        var offerId = SelectedOffer!.Offer.Id; var courseId = SelectedCourse!.Course.Id; var id = Code(NewDish ? DishId : SelectedDish!.Dish.Id);
        var name = Name(DishName); var stationId = Code(DishStationId); var sort = Number(DishSort, "Orden", 0, 9999); var productId = DishProductId.Trim();
        return Command(NewDish ? "dish-create" : "dish-update", new { offerId, courseId, id, name, stationId, productId, sort }, (NewDish ? "Crear plato " : "Guardar plato ") + id);
    }
    private bool CanToggleDish() => Main && SelectedDish is not null && !NewDish;
    [RelayCommand(CanExecute = nameof(CanToggleDish))]
    private Task ToggleDish() => Command(SelectedDish!.Dish.Active ? "dish-deactivate" : "dish-reactivate", new { offerId = SelectedOffer!.Offer.Id, courseId = SelectedCourse!.Course.Id, id = SelectedDish.Dish.Id },
        (SelectedDish.Dish.Active ? "Desactivar plato " : "Reactivar plato ") + SelectedDish.Dish.Id);
}
