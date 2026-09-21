using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Costina.Server;

// D5.6 (#27): instalacion de demostracion. Los fixtures ficticios dejan su propia huella en la base
// (la orden idempotente del inicializador), asi que la marca viaja con las copias y no necesita esquema.
// Se refresca en cada /health: cargar la demo con el servicio en marcha se refleja sin reiniciar.
public sealed class DemoState { public volatile bool IsDemo; }

// D5.6 (#27): "status" — resumen de SOLO LECTURA de la instalacion, para el guion fisico y para soporte.
// No pide credenciales ni imprime secretos: version, modo, servicios, /health, certificado y huella de
// la CA, y ultima copia. Devuelve codigo 1 si el motor no responde. En Windows conviene consola elevada
// (la configuracion solo es legible por administradores).
public static class StatusReport
{
    public static async Task RunAsync(ServerSettings settings)
    {
        void Line(string name,object? value) => Console.WriteLine($"{name,-28}{value}");
        Line("Costina",BuildInfo.Version);
        Line("Data root",ServerSettings.DataRoot);
        var provisioned=false;
        try { provisioned=File.Exists(ServerSettings.ServerFile); }
        catch(UnauthorizedAccessException) { }
        Line("Provisioned",provisioned ? "yes" : "no (or not readable: run from an elevated console)");
        Line("Mode",settings.Get("COSTINA_MODE") ?? (settings.Get("COSTINA_LAB_MODE")=="true" ? "laboratory" : "unknown"));
        Line("Scope",$"{settings.Get("COSTINA_TENANT")}/{settings.Get("COSTINA_COMPANY")}/{settings.Get("COSTINA_LOCATION")}");
        if(OperatingSystem.IsWindows())
            foreach(var service in new[]{ServerSetup.DatabaseService,ServiceInstaller.Name})
                Line("Service "+service,await ServiceState(service));

        var port=settings.Get("COSTINA_PORT") ?? "5088";
        var healthy=false;
        try
        {
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
            using var health=JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/health"));
            var root=health.RootElement;
            healthy=root.GetProperty("status").GetString()=="ready";
            Line("Engine /health",$"{root.GetProperty("status").GetString()} · {root.GetProperty("mode").GetString()} · {root.GetProperty("version").GetString()}"
                +(root.TryGetProperty("demo",out var demo) && demo.GetBoolean() ? " · DEMO (fictitious data)" : ""));
        }
        catch(Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        { Line("Engine /health",$"NOT ANSWERING on http://127.0.0.1:{port}"); }

        try
        {
            if(File.Exists(LocalTls.ServerCertificate) && File.Exists(LocalTls.CaCertificate))
            {
                using var server=X509CertificateLoader.LoadCertificateFromFile(LocalTls.ServerCertificate);
                using var authority=X509CertificateLoader.LoadCertificateFromFile(LocalTls.CaCertificate);
                var remaining=(int)(server.NotAfter.ToUniversalTime()-DateTime.UtcNow).TotalDays;
                Line("HTTPS (LAN)",$"port {settings.Get("COSTINA_TLS_PORT") ?? "5443"} · {server.GetNameInfo(X509NameType.DnsName,false)}");
                Line("Server certificate",$"valid until {server.NotAfter.ToUniversalTime():yyyy-MM-dd} ({remaining} days)"+(remaining<30 ? " — run renew-tls" : ""));
                Line("Local CA fingerprint",LocalTls.Fingerprint(authority));
            }
            else Line("HTTPS (LAN)","not provisioned (loopback only)");
        }
        catch(Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.Cryptography.CryptographicException)
        { Line("HTTPS (LAN)","certificates not readable from this console"); }

        try
        {
            var latest=BackupRunner.Latest();
            Line("Last backup",latest is null ? "NONE" : $"{latest.File} · {(int)(DateTimeOffset.UtcNow-latest.CreatedAt).TotalHours} h ago · {latest.Tables.Values.Sum(t=>t.Rows)} rows");
            Line("Second backup destination",settings.Get("COSTINA_BACKUP_COPY") is {Length:>0} copy ? copy : "NOT CONFIGURED (a copy on the same disk does not survive losing the disk)");
        }
        catch(UnauthorizedAccessException) { Line("Last backup","not readable from this console"); }
        if(!healthy) Environment.ExitCode=1;
    }

    private static async Task<string> ServiceState(string service)
    {
        var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"sc.exe")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add("query"); info.ArgumentList.Add(service);
        using var process=Process.Start(info);
        if(process is null) return "unknown";
        var output=await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if(process.ExitCode!=0) return "not installed";
        foreach(var state in new[]{"RUNNING","STOPPED","START_PENDING","STOP_PENDING","PAUSED"})
            if(output.Contains(state,StringComparison.Ordinal)) return state;
        return "unknown";
    }
}
