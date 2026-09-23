using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie de comedor y cocina. La habilitacion de CADA accion es un mapeo directo de las
// affordances del DTO del servidor (Allowed): este viewmodel no reconstruye ninguna regla.
// La seleccion solicitada y la entidad leida son estados distintos: un comando solo se construye
// desde un contexto leido correctamente que coincide con lo que el operador ve seleccionado.
public sealed partial class ServiceViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? serviceId;   // destino solicitado; nunca es por si mismo el contexto de un comando
    private int generation;      // invalida lecturas obsoletas cuando cambia el destino

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
    [ObservableProperty] private string pax = "2";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private GuestRestrictionDto[] restrictions = [];
    [ObservableProperty] private GuestRestrictionDto? selectedRestriction;
    [ObservableProperty] private bool restrictionsPendingAck;
    [ObservableProperty] private string restrictionGuest = "";
    [ObservableProperty] private string restrictionKind = "allergy";
    [ObservableProperty] private string restrictionSubstance = "";
    [ObservableProperty] private string restrictionSeverity = "severe";
    [ObservableProperty] private string reviewNote = "";
    [ObservableProperty] private string serviceTitle = "Selecciona una mesa";

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
        "ready" or "serve" or "skip" or "choose" => Context is not null && SelectedCourse?.Actions?.Contains(action) == true,
        "preparation-start" or "preparation-ready" or "review-preparation"
            => Context is not null && SelectedPreparation?.Actions?.Contains(action) == true,
        "release" => Context is not null && SelectedEntry?.Occupancy.Actions?.Contains(action) == true,
        _ => false
    };
    public bool CanAction(string action) => shell.Writable && Allowed(action);

    internal void Sync()
    {
        ActCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged(); DeclareRestrictionCommand.NotifyCanExecuteChanged();
        RemoveRestrictionCommand.NotifyCanExecuteChanged(); ReviewCommand.NotifyCanExecuteChanged(); ChooseCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanChoose));
    }

    public void ApplyConfiguration(Configuration config)
    {
        rendering = true;
        try
        {
            Tables = config.Tables; SelectedTable = Tables.FirstOrDefault();
            // Servidor anterior a E4a: solo 'menus' (id, name); se muestran como ofertas sin pases.
            Offers = config.Offers ?? (config.Menus ?? []).Select(m => new OfferChoice(m.Id, m.Name, "tasting", [])).ToArray();
            SelectedOffer = Offers.FirstOrDefault();
        }
        finally { rendering = false; }
    }

    internal void SelectService(string? id) => serviceId = id;

    internal void Clear()
    {
        rendering = true;
        try
        {
            Board = []; SelectedEntry = null; Tables = []; Offers = []; serviceId = null; Reason = "";
            Invalidate(); ServiceTitle = "Selecciona una mesa";
        }
        finally { rendering = false; }
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
        Sync();
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
            ServiceTitle = current is null ? "Sin mesas ocupadas. Puedes abrir una mesa."
                : $"{current.Data.TableId} · {current.Data.Pax} personas · {current.Data.State}";
            Courses = current?.Data.Courses ?? [];
            Restrictions = current?.Data.Restrictions ?? [];
            RestrictionsPendingAck = current?.Data.RestrictionsPendingAck ?? false;
            SelectedCourse = Courses.FirstOrDefault(c => c.Id == previousCourse)
                ?? Courses.FirstOrDefault(c => c.State is "Fired" or "Preparing" or "Ready")
                ?? Courses.FirstOrDefault(c => c.State == "Pending") ?? Courses.LastOrDefault();
            UpdatePreparations();
        }
        finally { rendering = false; }
        Sync();
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
        var pax = Dining?.Data.Pax ?? 0;
        ChoicesText = SelectedCourse?.ChoiceRequired != true ? ""
            : string.Join(" · ", Enumerable.Range(1, pax).Select(g => $"{g}: " + (Preparations.FirstOrDefault(p => p.GuestPosition == g)?.Name ?? "sin elegir")));
    }
    public bool CanChoose => shell.Writable && Allowed("choose");
    private bool CanChooseDish() => CanChoose && SelectedChoiceDish is not null;
    [RelayCommand(CanExecute = nameof(CanChooseDish))]
    private Task Choose() => shell.Run(async () =>
    {
        var context = Require();
        if (!int.TryParse(ChoiceGuest, out var guest) || guest < 1 || guest > context.Data.Pax) throw new ArgumentException("Comensal: un número entre 1 y " + context.Data.Pax + ".");
        await shell.Api!.SendAsync("dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/choose",
            new { expectedVersion = context.Version, courseId = SelectedCourse!.Id, guestPosition = guest, dishId = SelectedChoiceDish!.Id },
            $"Elegir {SelectedChoiceDish.Name} para el comensal {guest} · {context.Data.TableId}");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: elección registrada.";
    });

    partial void OnSelectedEntryChanged(BoardEntry? value)
    {
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
    private Task Act(string action) => shell.Run(async () =>
    {
        var context = Require();
        if (action is "pause" or "skip" && string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Introduce el motivo.");
        await shell.Api!.SendAsync(
            "dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/" + action,
            new { expectedVersion = context.Version, courseId = SelectedCourse?.Id, itemId = SelectedPreparation?.Id, reason = Reason },
            action + " · " + context.Data.TableId);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: " + action;
    });

    private bool CanOpenTable() => shell.Writable && shell.CanOpen && SelectedTable is not null && SelectedOffer is not null;
    [RelayCommand(CanExecute = nameof(CanOpenTable))]
    private Task Open() => shell.Run(async () =>
    {
        if (!int.TryParse(Pax, out var people)) throw new ArgumentException("Introduce un número de personas.");
        var result = await shell.Api!.SendAsync("dining/services",
            new { tableId = SelectedTable!.Id, pax = people, offerId = SelectedOffer!.Id, menuId = SelectedOffer.Id }, "Abrir " + SelectedTable.Name);   // menuId: alias para un servidor anterior
        serviceId = result.GetProperty("serviceId").GetString();
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
            "Declarar restricción de comensal · " + context.Data.TableId);
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
            "Retirar restricción con motivo · " + context.Data.TableId);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: restricción retirada.";
    });

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
            "Revisión de cocina (" + decision + ") · " + preparation.Name + " · " + context.Data.TableId);
        ReviewNote = "";
        await shell.RefreshAll();
        shell.Status = "Cocina ha registrado su decisión sobre " + preparation.Name + ": " + decision + ".";
    });

    private bool CanRelease() => shell.Writable && Allowed("release");
    [RelayCommand(CanExecute = nameof(CanRelease))]
    private Task Release() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(Reason)) throw new ArgumentException("Introduce el motivo de liberación.");
        var entry = SelectedEntry!;   // Context garantiza que es la misma mesa que el detalle leido
        await shell.Api!.SendAsync("dining/occupancy/" + ApiClient.Segment(context.Data.Id) + "/release",
            new { expectedVersion = entry.OccupancyVersion, reason = Reason }, "Liberar mesa sin cambiar su cuenta · " + context.Data.TableId);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: liberar mesa";
    });
}
