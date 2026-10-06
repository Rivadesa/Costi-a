using CommunityToolkit.Mvvm.ComponentModel;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Inicio (corte A, plan 2026-09-24): la primera vista del ERP al entrar. Estado del servidor, del tiempo real y de quien esta
// conectado, accesos grandes a cada apartado y un resumen de los dispositivos. Se compone SOLO de lo leido: el diagnostico
// (solo principal, D5.2) y la lista de dispositivos. Ninguna regla de negocio; ningun secreto en pantalla.
public sealed partial class HomeViewModel(ShellViewModel shell) : ObservableObject
{
    public static readonly TimeSpan CertificateWarningWindow = TimeSpan.FromDays(30);

    [ObservableProperty] private string serverText = "";
    [ObservableProperty] private string whoText = "";
    [ObservableProperty] private string backupText = "";
    [ObservableProperty] private string devicesSummary = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCertificateWarning))] private string certificateWarning = "";
    public bool HasCertificateWarning => CertificateWarning.Length > 0;
    public bool Visible { get; set; }

    internal void Sync()
    {
        var session = shell.Session;
        WhoText = session is null ? "" : session.Role switch
        {
            "main" => $"Responsable: {session.Actor ?? session.Role}. Acceso a todos los apartados.",
            "service" => $"Puesto de sala {session.Actor ?? ""}{(session.StationName is { Length: > 0 } s ? " · " + s : "")}: toma comandas. Cobrar, configurar o conectar dispositivos pide la contraseña del responsable.",
            "kitchen" => $"Pantalla de cocina {session.Actor ?? ""}{(session.StationName is { Length: > 0 } s ? " · " + s : "")}.",
            var role => role
        };
        if (session is null) { ServerText = ""; CertificateWarning = ""; BackupText = ""; DevicesSummary = ""; }
        else if (ServerText.Length == 0)
            ServerText = $"Servidor {session.ServerVersion} en {shell.Endpoint}" + (session.Demo ? " · DEMOSTRACIÓN · DATOS FICTICIOS" : "");
    }

    internal void Clear() { ServerText = ""; WhoText = ""; BackupText = ""; DevicesSummary = ""; CertificateWarning = ""; }

    internal async Task LoadAsync()
    {
        if (shell.Api is null) return;
        Sync();
        if (!shell.IsMain) return;
        ApplyDiagnostics(await shell.Api.GetAsync<DiagnosticsDto>("diagnostics"));
        ApplyDevices(await shell.Api.GetAsync<DeviceRowDto[]>("auth/devices"));
    }

    // Pura (los checks la ejercitan sin servidor): el aviso de certificado aparece cuando quedan menos de 30 dias (F10).
    public void ApplyDiagnostics(DiagnosticsDto diagnostics, DateTimeOffset? now = null)
    {
        var moment = now ?? DateTimeOffset.UtcNow;
        ServerText = $"Servidor {diagnostics.Version} ({(diagnostics.Mode == "installation" ? "instalación" : "laboratorio")}{(diagnostics.RunningAsService ? ", servicio de Windows" : "")}) en {shell.Endpoint}"
            + (diagnostics.Tls is { Enabled: true } tls ? $" · HTTPS en la red local por el puerto {tls.Port}" + (tls.Names is { Length: > 0 } ? $" ({tls.Names})" : "") : " · solo en este equipo (sin HTTPS en la red)");
        BackupText = diagnostics.Backup is null ? "" : diagnostics.Backup.Automatic
            ? (diagnostics.Backup.LastSuccessAt is { } at ? $"Última copia de seguridad: {at.ToLocalTime():dd/MM HH:mm}." : "Todavía no hay ninguna copia de seguridad.")
              + (diagnostics.Backup.LastError is { Length: > 0 } e ? $" ÚLTIMO INTENTO FALLIDO: {e}" : "")
            : "Copias de seguridad automáticas desactivadas (laboratorio).";
        var expires = diagnostics.Tls?.ServerCertificateExpiresAt;
        CertificateWarning = expires is null ? "" : expires.Value <= moment
            ? $"EL CERTIFICADO DEL SERVIDOR CADUCÓ el {expires.Value.ToLocalTime():dd/MM/yyyy}: tablets y puestos adicionales no pueden conectar. Renuévalo con «Costina.Server.exe renew-tls» (D5.3)."
            : expires.Value - moment < CertificateWarningWindow
            ? $"El certificado del servidor caduca el {expires.Value.ToLocalTime():dd/MM/yyyy} (en {(int)(expires.Value - moment).TotalDays} días). Renuévalo antes con «Costina.Server.exe renew-tls»: si caduca, tablets y puestos adicionales dejan de conectar."
            : "";
    }

    public void ApplyDevices(DeviceRowDto[] devices)
    {
        var active = devices.Where(d => d.Active).ToArray();
        var waiters = active.Where(d => d.Role == "service").ToArray();
        var screens = active.Where(d => d.Role == "kitchen").ToArray();
        var legacy = active.Count(d => d.Role == "main");
        DevicesSummary = active.Length == 0 ? "Ningún dispositivo conectado todavía. Conecta comanderos y pantallas de cocina desde «Dispositivos»."
            : $"{Plural(waiters.Length, "comandero", "comanderos")} ({waiters.Count(d => d.Online)} con conexión ahora) · {Plural(screens.Length, "pantalla de cocina", "pantallas de cocina")} ({screens.Count(d => d.Online)} con conexión ahora)"
              + (legacy > 0 ? $" · {Plural(legacy, "dispositivo antiguo con rol principal", "dispositivos antiguos con rol principal")}: conviene quitarlos y conectarlos de nuevo" : "");
    }

    private static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";
}
