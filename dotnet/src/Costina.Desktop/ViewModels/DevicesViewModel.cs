using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie "Puestos" (solo rol main; el servidor lo impone ademas por permisos): generar el
// codigo de emparejamiento, aprobar o denegar solicitudes con rol y estacion, y revocar puestos.
// D6.5: el codigo se muestra ademas como QR con el enlace https://EQUIPO:PUERTO/app/#pair=CODIGO, que lee la camara
// del dispositivo (sin escaner propio). El QR solo lleva el codigo de un uso: nunca un token ni una credencial.
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

    private const string QrIdle = "Al generar un código aparece aquí su QR: el dispositivo lo lee con la cámara y abre la aplicación con el código ya puesto.";
    private DateTimeOffset? codeExpiresAt;
    private bool addressSuggested;
    // Direccion con la que los DISPOSITIVOS llegan al servidor (https://NOMBRE:PUERTO, la del certificado de D5.3).
    // Se propone la de este cliente si ya habla por la LAN; por loopback la escribe el administrador y se recuerda.
    [ObservableProperty] private string deviceAddress = "";
    [ObservableProperty] private string pairingUrl = "";
    [ObservableProperty] private string qrInfo = QrIdle;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasQr))] private ImageSource? pairingQr;
    public bool HasQr => PairingQr is not null;
    // Carpeta del recordatorio (no es un secreto: solo la direccion). Los checks la apuntan a una carpeta temporal.
    public string? MemoryFolder { get; set; }

    public bool Visible { get; set; }

    internal void Sync()
    {
        GenerateCodeCommand.NotifyCanExecuteChanged(); ApproveCommand.NotifyCanExecuteChanged();
        DenyCommand.NotifyCanExecuteChanged(); RevokeCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            PairingCode = ""; Pending = []; SelectedPending = null; Devices = []; SelectedDevice = null; ApproveStation = "";
            codeExpiresAt = null; addressSuggested = false; DeviceAddress = ""; PairingQr = null; PairingUrl = ""; QrInfo = QrIdle;
        }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        SuggestAddress();
        // Un QR de un codigo caducado solo confunde: se retira en la siguiente lectura.
        if (codeExpiresAt is { } expires && DateTimeOffset.UtcNow >= expires)
        {
            codeExpiresAt = null; PairingCode = "";
            PairingInfo = "El código anterior caducó. Genera otro: caduca en 5 minutos y sirve una sola vez.";
        }
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

    // Una vez por conexion: la direccion de este cliente si ya habla por la LAN; si no, la ultima que se uso aqui.
    public void SuggestAddress()
    {
        if (addressSuggested) return;
        addressSuggested = true;
        if (DeviceAddress.Length > 0) return;
        var own = Uri.TryCreate(shell.Endpoint.Trim(), UriKind.Absolute, out var endpoint) ? PairingLink.SuggestDeviceAddress(endpoint) : "";
        DeviceAddress = own.Length > 0 ? own : Recall();
    }

    partial void OnPairingCodeChanged(string value) => RefreshQr();
    partial void OnDeviceAddressChanged(string value) => RefreshQr();

    private void RefreshQr()
    {
        if (rendering) return;
        if (PairingCode.Length == 0) { PairingQr = null; PairingUrl = ""; QrInfo = QrIdle; return; }
        try
        {
            var address = PairingLink.ParseDeviceAddress(DeviceAddress);
            var url = PairingLink.Build(address, PairingCode);
            PairingQr = QrImage.Render(url); PairingUrl = url;
            QrInfo = "Apunta la cámara del dispositivo a este QR: abre la aplicación con el código ya puesto. El dispositivo debe tener instalada la CA de este servidor. Después, aprueba aquí la solicitud.";
            Remember(address);
        }
        catch (ArgumentException e)
        {
            PairingQr = null; PairingUrl = "";
            QrInfo = "Sin QR: " + e.Message + " El código se puede teclear igualmente en el dispositivo.";
        }
    }

    private string MemoryPath => System.IO.Path.Combine(MemoryFolder ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Costina"), "device-address.txt");
    private string Recall()
    {
        try { return PairingLink.ParseDeviceAddress(File.ReadAllText(MemoryPath)).GetLeftPart(UriPartial.Authority); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return ""; }
    }
    private void Remember(Uri address)
    {
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MemoryPath)!); File.WriteAllText(MemoryPath, address.GetLeftPart(UriPartial.Authority)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    partial void OnSelectedPendingChanged(PairingPendingDto? value) { if (!rendering) Sync(); }
    partial void OnSelectedDeviceChanged(DeviceRowDto? value) { if (!rendering) Sync(); }

    private bool CanManage() => shell.Writable && shell.IsMain;
    [RelayCommand(CanExecute = nameof(CanManage))]
    private Task GenerateCode() => shell.Run(async () =>
    {
        var issued = await shell.Api!.PostRawAsync("auth/pairings", new { });
        codeExpiresAt = issued.GetProperty("expiresAt").GetDateTimeOffset();
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
