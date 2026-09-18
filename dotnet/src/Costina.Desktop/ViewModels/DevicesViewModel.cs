using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie "Puestos" (solo rol main; el servidor lo impone ademas por permisos): generar el
// codigo de emparejamiento, aprobar o denegar solicitudes con rol y estacion, y revocar puestos.
// El QR visual llegara con la PWA (#28): aqui el codigo se muestra para teclear o copiar.
public sealed partial class DevicesViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;

    [ObservableProperty] private string pairingCode = "";
    [ObservableProperty] private string pairingInfo = "Genera un código y tecléalo en el puesto nuevo. Caduca en 5 minutos y sirve una sola vez.";
    [ObservableProperty] private PairingPendingDto[] pending = [];
    [ObservableProperty] private PairingPendingDto? selectedPending;
    [ObservableProperty] private string approveRole = "service";
    [ObservableProperty] private string approveStation = "";
    [ObservableProperty] private DeviceRowDto[] devices = [];
    [ObservableProperty] private DeviceRowDto? selectedDevice;

    public bool Visible { get; set; }

    internal void Sync()
    {
        GenerateCodeCommand.NotifyCanExecuteChanged(); ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged(); RevokeCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try { PairingCode = ""; Pending = []; SelectedPending = null; Devices = []; SelectedDevice = null; ApproveStation = ""; }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var pendingRows = await shell.Api.GetAsync<PairingPendingDto[]>("auth/pairings/pending");
        var deviceRows = await shell.Api.GetAsync<DeviceRowDto[]>("auth/devices");
        rendering = true;
        try
        {
            var previous = SelectedPending?.PairingId;
            Pending = pendingRows; SelectedPending = pendingRows.FirstOrDefault(p => p.PairingId == previous) ?? pendingRows.FirstOrDefault();
            var device = SelectedDevice?.Id;
            Devices = deviceRows; SelectedDevice = deviceRows.FirstOrDefault(d => d.Id == device);
        }
        finally { rendering = false; }
        Sync();
    }

    partial void OnSelectedPendingChanged(PairingPendingDto? value) { if (!rendering) Sync(); }
    partial void OnSelectedDeviceChanged(DeviceRowDto? value) { if (!rendering) Sync(); }

    private bool CanManage() => shell.Writable && shell.IsMain;
    [RelayCommand(CanExecute = nameof(CanManage))]
    private Task GenerateCode() => shell.Run(async () =>
    {
        var issued = await shell.Api!.PostRawAsync("auth/pairings", new { });
        PairingCode = issued.GetProperty("code").GetString()!;
        PairingInfo = $"Código de un solo uso. Caduca a las {issued.GetProperty("expiresAt").GetDateTimeOffset().ToLocalTime():HH:mm:ss}.";
        await LoadAsync();
    });

    private bool CanApprove() => shell.Writable && shell.IsMain && SelectedPending is not null;
    [RelayCommand(CanExecute = nameof(CanApprove))]
    private Task Approve() => shell.Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(ApproveStation)) throw new ArgumentException("Indica la estación del puesto (p. ej. sala-1, cold, pase).");
        await shell.Api!.PostRawAsync("auth/pairings/" + ApiClient.Segment(SelectedPending!.PairingId) + "/approve",
            new { role = ApproveRole, station = ApproveStation.Trim() });
        shell.Status = $"Puesto \"{SelectedPending.DeviceName}\" aprobado como {ApproveRole}/{ApproveStation.Trim()}.";
        await LoadAsync();
    });

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private Task Deny() => shell.Run(async () =>
    {
        await shell.Api!.PostRawAsync("auth/pairings/" + ApiClient.Segment(SelectedPending!.PairingId) + "/deny", new { });
        shell.Status = $"Solicitud de \"{SelectedPending.DeviceName}\" denegada.";
        await LoadAsync();
    });

    private bool CanRevoke() => shell.Writable && shell.IsMain && SelectedDevice is { RevokedAt: null };
    [RelayCommand(CanExecute = nameof(CanRevoke))]
    private Task Revoke() => shell.Run(async () =>
    {
        await shell.Api!.PostRawAsync("auth/devices/" + ApiClient.Segment(SelectedDevice!.Id) + "/revoke", new { });
        shell.Status = $"Puesto \"{SelectedDevice.Name}\" revocado: expulsado también del tiempo real.";
        await LoadAsync();
    });
}
