using CommunityToolkit.Mvvm.ComponentModel;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Inicio (corte A, plan 2026-09-24): la primera vista del ERP al entrar. Estado del servidor, del tiempo real y de quien esta
// conectado, accesos grandes a cada apartado y un resumen de los dispositivos. Se compone SOLO de lo leido: el diagnostico
// (solo principal, D5.2) y la lista de dispositivos. Ninguna regla de negocio; ningun secreto en pantalla.
public sealed partial class HomeViewModel(ShellViewModel shell) : ObservableObject
{
    public static readonly TimeSpan CertificateWarningWindow = TimeSpan.FromDays(30);

    // C1 (revision): una linea corta para quien trabaja (version y donde esta el servidor); la direccion, el modo y los puertos van en
    // el detalle (ayuda emergente de esa linea).
    [ObservableProperty] private string serverText = "";
    [ObservableProperty] private string serverDetail = "";
    [ObservableProperty] private string whoText = "";
    [ObservableProperty] private string backupText = "";
    [ObservableProperty] private string devicesSummary = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasCertificateWarning))] private string certificateWarning = "";
    public bool HasCertificateWarning => CertificateWarning.Length > 0;
    public bool Visible { get; set; }

    internal void Sync()
    {
        var session = shell.Session;
        // C1: rol y actor con el vocabulario comun (Labels): "Responsable: jefa", "Puesto de sala pc-barra · Sala 1"; sin user:/device:.
        var actor = session is null ? "" : Labels.Actor(session.Actor);
        var station = session?.StationName is { Length: > 0 } s ? " · " + s : "";
        WhoText = session is null ? "" : session.Role switch
        {
            "main" when shell.IsStation => $"{shell.RoleText("main")} {actor}".TrimEnd() + station + ": dispositivo antiguo con rol principal. Conviene quitarlo y conectar este PC de nuevo desde Dispositivos.",
            "main" => (actor.Length > 0 ? $"{Labels.SessionRole("main")}: {actor}" : Labels.SessionRole("main")) + ". Acceso a todos los apartados.",
            "service" => $"{Labels.SessionRole("service")} {actor}".TrimEnd() + station + ": toma comandas. Cobrar, configurar o conectar dispositivos pide la contraseña del responsable.",
            "kitchen" => $"{Labels.SessionRole("kitchen")} {actor}".TrimEnd() + station + ".",
            var role => Labels.SessionRole(role)
        };
        if (session is null) { ServerText = ""; ServerDetail = ""; CertificateWarning = ""; BackupText = ""; DevicesSummary = ""; }
        else if (ServerText.Length == 0)
        {
            ServerText = $"Servidor {session.ServerVersion} {shell.ServerPlace}" + (session.Demo ? " · DEMOSTRACIÓN · DATOS FICTICIOS" : "");
            ServerDetail = $"Servidor {session.ServerVersion} en {shell.Endpoint}";
        }
    }

    internal void Clear() { ServerText = ""; ServerDetail = ""; WhoText = ""; BackupText = ""; DevicesSummary = ""; CertificateWarning = ""; }

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
        ServerText = $"Servidor {diagnostics.Version} {shell.ServerPlace}" + (diagnostics.Mode == "installation" ? "" : " · laboratorio")
            + (shell.Session?.Demo == true ? " · DEMOSTRACIÓN · DATOS FICTICIOS" : "");
        ServerDetail = $"Servidor {diagnostics.Version} ({(diagnostics.Mode == "installation" ? "instalación" : "laboratorio")}{(diagnostics.RunningAsService ? ", servicio de Windows" : "")}) en {shell.Endpoint}"
            + (diagnostics.Tls is { Enabled: true } tls ? $" · HTTPS en la red local por el puerto {tls.Port}" + (tls.Names is { Length: > 0 } ? $" ({tls.Names})" : "") : " · solo en este equipo (sin HTTPS en la red)");
        BackupText = diagnostics.Backup is null ? "" : diagnostics.Backup.Automatic
            ? (diagnostics.Backup.LastSuccessAt is { } at ? $"Última copia de seguridad: {at.ToLocalTime():dd/MM HH:mm}." : "Todavía no hay ninguna copia de seguridad.")
              + (diagnostics.Backup.LastError is { Length: > 0 } e ? $" ÚLTIMO INTENTO FALLIDO: {e}" : "")
            : "Copias de seguridad automáticas desactivadas (laboratorio).";
        var expires = diagnostics.Tls?.ServerCertificateExpiresAt;
        CertificateWarning = expires is null ? "" : expires.Value <= moment
            ? $"EL CERTIFICADO DEL SERVIDOR CADUCÓ el {expires.Value.ToLocalTime():dd/MM/yyyy}: tablets y puestos adicionales no pueden conectar. Hay que renovarlo en el equipo servidor (LÉEME de la instalación)."
            : expires.Value - moment < CertificateWarningWindow
            ? $"El certificado del servidor caduca el {expires.Value.ToLocalTime():dd/MM/yyyy} (en {Labels.Plural((int)(expires.Value - moment).TotalDays, "día", "días")}). Renuévalo antes en el equipo servidor (LÉEME de la instalación): si caduca, tablets y puestos adicionales dejan de conectar."
            : "";
    }

    public void ApplyDevices(DeviceRowDto[] devices)
    {
        var active = devices.Where(d => d.Active).ToArray();
        var waiters = active.Where(d => d.Role == "service").ToArray();
        var screens = active.Where(d => d.Role == "kitchen").ToArray();
        var legacy = active.Count(d => d.Role == "main");
        DevicesSummary = active.Length == 0 ? "Ningún dispositivo conectado todavía. Conecta comanderos y pantallas de cocina desde «Dispositivos»."
            : $"{Labels.Plural(waiters.Length, "comandero", "comanderos")} ({waiters.Count(d => d.Online)} con conexión ahora) · {Labels.Plural(screens.Length, "pantalla de cocina", "pantallas de cocina")} ({screens.Count(d => d.Online)} con conexión ahora)"
              + (legacy > 0 ? $" · {Labels.Plural(legacy, "dispositivo antiguo con rol principal", "dispositivos antiguos con rol principal")}: conviene quitarlos y conectarlos de nuevo" : "");
    }
}
