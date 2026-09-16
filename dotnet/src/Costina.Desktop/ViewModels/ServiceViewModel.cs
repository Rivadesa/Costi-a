using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie de comedor y cocina. La habilitacion de CADA accion es un mapeo directo de las
// affordances del DTO del servidor (Allowed): este viewmodel no reconstruye ninguna regla.
public sealed partial class ServiceViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? serviceId;

    [ObservableProperty] private BoardEntry[] board = [];
    [ObservableProperty] private BoardEntry? selectedEntry;
    [ObservableProperty] private Versioned<DiningDto>? dining;
    [ObservableProperty] private CourseDto[] courses = [];
    [ObservableProperty] private CourseDto? selectedCourse;
    [ObservableProperty] private PreparationDto[] preparations = [];
    [ObservableProperty] private PreparationDto? selectedPreparation;
    [ObservableProperty] private TableChoice[] tables = [];
    [ObservableProperty] private TableChoice? selectedTable;
    [ObservableProperty] private MenuChoice[] menus = [];
    [ObservableProperty] private MenuChoice? selectedMenu;
    [ObservableProperty] private string pax = "2";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private string serviceTitle = "Selecciona una mesa";

    // Unico punto de verdad para la habilitacion: la accion esta en la lista que envio el servidor.
    private bool Allowed(string action) => action switch
    {
        "start" or "fire-next" or "pause" or "resume" or "complete" or "cancel-unstarted"
            => Dining?.Data.Actions?.Contains(action) == true,
        "ready" or "serve" or "skip" => SelectedCourse?.Actions?.Contains(action) == true,
        "preparation-start" or "preparation-ready" => SelectedPreparation?.Actions?.Contains(action) == true,
        "release" => SelectedEntry?.Occupancy.Actions?.Contains(action) == true,
        _ => false
    };
    public bool CanAction(string action) => shell.Writable && Allowed(action);

    internal void Sync()
    {
        ActCommand.NotifyCanExecuteChanged(); OpenCommand.NotifyCanExecuteChanged();
        ReleaseCommand.NotifyCanExecuteChanged();
    }

    internal void ApplyConfiguration(Configuration config)
    {
        rendering = true;
        try { Tables = config.Tables; SelectedTable = Tables.FirstOrDefault(); Menus = config.Menus; SelectedMenu = Menus.FirstOrDefault(); }
        finally { rendering = false; }
    }

    internal void SelectService(string? id) => serviceId = id;

    internal void Clear()
    {
        rendering = true;
        try
        {
            Board = []; SelectedEntry = null; Dining = null; Courses = []; SelectedCourse = null;
            Preparations = []; SelectedPreparation = null; Tables = []; Menus = [];
            serviceId = null; Reason = ""; ServiceTitle = "Selecciona una mesa";
        }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null) return;
        var fresh = await shell.Api.GetAsync<BoardEntry[]>("board");
        if (!fresh.Any(b => b.Service.Id == serviceId)) serviceId = fresh.FirstOrDefault()?.Service.Id;
        var current = serviceId is null ? null
            : await shell.Api.GetAsync<Versioned<DiningDto>>("services/" + ApiClient.Segment(serviceId));
        var previousCourse = SelectedCourse?.Id;
        rendering = true;
        try
        {
            Board = fresh; SelectedEntry = fresh.FirstOrDefault(b => b.Service.Id == serviceId);
            Dining = current;
            ServiceTitle = current is null ? "Sin mesas ocupadas. Puedes abrir una mesa."
                : $"{current.Data.TableId} · {current.Data.Pax} personas · {current.Data.State}";
            Courses = current?.Data.Courses ?? [];
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
    }

    partial void OnSelectedEntryChanged(BoardEntry? value)
    {
        if (rendering || shell.Busy || value is null) return;
        serviceId = value.Service.Id;
        _ = shell.Run(LoadAsync);
    }
    partial void OnSelectedCourseChanged(CourseDto? value) { if (!rendering) { UpdatePreparations(); Sync(); } }
    partial void OnSelectedPreparationChanged(PreparationDto? value) { if (!rendering) Sync(); }

    private bool CanAct(string? action) => action is not null && CanAction(action);
    [RelayCommand(CanExecute = nameof(CanAct))]
    private Task Act(string action) => shell.Run(async () =>
    {
        if (action is "pause" or "skip" && string.IsNullOrWhiteSpace(Reason))
            throw new ArgumentException("Introduce el motivo.");
        var result = await shell.Api!.SendAsync(
            "services/" + ApiClient.Segment(serviceId!) + "/commands/" + action,
            new { expectedVersion = Dining!.Version, courseId = SelectedCourse?.Id, itemId = SelectedPreparation?.Id, reason = Reason },
            action);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: " + action;
    });

    private bool CanOpenTable() => shell.Writable && shell.CanOpen && SelectedTable is not null && SelectedMenu is not null;
    [RelayCommand(CanExecute = nameof(CanOpenTable))]
    private Task Open() => shell.Run(async () =>
    {
        if (!int.TryParse(Pax, out var people)) throw new ArgumentException("Introduce un número de personas.");
        var result = await shell.Api!.SendAsync("services",
            new { tableId = SelectedTable!.Id, pax = people, menuId = SelectedMenu!.Id }, "Abrir " + SelectedTable.Name);
        serviceId = result.GetProperty("serviceId").GetString();
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: abrir " + SelectedTable.Name;
    });

    private bool CanRelease() => shell.Writable && Allowed("release");
    [RelayCommand(CanExecute = nameof(CanRelease))]
    private Task Release() => shell.Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(Reason)) throw new ArgumentException("Introduce el motivo de liberación.");
        var entry = SelectedEntry!;
        await shell.Api!.SendAsync("occupancy/" + ApiClient.Segment(entry.Service.Id) + "/release",
            new { expectedVersion = entry.OccupancyVersion, reason = Reason }, "Liberar mesa sin cambiar su cuenta");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: liberar mesa";
    });
}
