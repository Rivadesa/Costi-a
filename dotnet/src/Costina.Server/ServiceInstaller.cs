using System.Diagnostics;

namespace Costina.Server;

// D5.2 (#27, ADR-011): registro del motor como servicio de Windows. Se ejecuta elevado y una vez,
// despues de provision/init. El servicio corre con la cuenta VIRTUAL NT SERVICE\Costina (sin
// contrasena, sin privilegios de administrador), arranca con el sistema sin sesion de usuario y se
// recupera solo si el proceso muere. La ACL deja a esa cuenta leer UNICAMENTE server.json: las
// credenciales del propietario del esquema (owner.json) quedan fuera de su alcance. Desinstalar el
// servicio nunca toca la raiz de datos. Solo usa herramientas del sistema (sc, icacls, reg).
public static class ServiceInstaller
{
    public const string Name="Costina";
    public const string Account=@"NT SERVICE\Costina";
    private const string LocalSystem="*S-1-5-18", Administrators="*S-1-5-32-544";

    public static async Task InstallAsync(ServerSettings settings)
    {
        RequireWindows();
        if(!File.Exists(ServerSettings.ServerFile))
            throw new InvalidOperationException("This data root is not provisioned; run provision and init before install-service.");
        var executable=Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the engine executable.");
        if(!Path.GetFileName(executable).Equals("Costina.Server.exe",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("install-service must run from the published Costina.Server.exe, not through the dotnet host.");

        // Ruta SIEMPRE entrecomillada: una ruta de servicio sin comillas y con espacios es secuestrable.
        var create=new List<string>{"create",Name,"binPath=","\""+executable+"\"","start=","delayed-auto","obj=",Account,
            "DisplayName=","Costina - motor del restaurante"};
        if(settings.Get("COSTINA_PG_SERVICE") is {Length:>0} database) { create.Add("depend="); create.Add(database); }
        await Run("sc.exe",create);
        await Run("sc.exe",["description",Name,"Motor local del restaurante (API y tiempo real). Los datos viven en la raiz de datos, no en el programa."]);
        await Run("sc.exe",["failure",Name,"reset=","86400","actions=","restart/5000/restart/30000/restart/60000"]);
        await Run("sc.exe",["failureflag",Name,"1"]);
        // Una raiz de datos no estandar debe acompanar al servicio: el SCM no hereda el entorno de esta consola.
        if(Environment.GetEnvironmentVariable("COSTINA_DATA") is {Length:>0})
            await Run("reg.exe",["add",@"HKLM\SYSTEM\CurrentControlSet\Services\"+Name,"/v","Environment","/t","REG_MULTI_SZ",
                "/d","COSTINA_DATA="+ServerSettings.DataRoot,"/f"]);

        var logs=Path.Combine(ServerSettings.DataRoot,"logs");
        Directory.CreateDirectory(logs);
        await Run("icacls.exe",[ServerSettings.ConfigDirectory,"/inheritance:r","/grant:r",LocalSystem+":(OI)(CI)F",Administrators+":(OI)(CI)F"]);
        await Run("icacls.exe",[ServerSettings.ServerFile,"/grant",Account+":R"]);
        await Run("icacls.exe",[logs,"/grant",Account+":(OI)(CI)M"]);
        Console.WriteLine($"Service {Name} registered as {Account} (delayed automatic start, restart on failure). Start it with: sc start {Name}");
    }

    public static async Task UninstallAsync()
    {
        RequireWindows();
        await Run("sc.exe",["stop",Name],tolerateFailure:true);
        for(var i=0;i<30 && (await Run("sc.exe",["query",Name],tolerateFailure:true)).Contains("STOP_PENDING",StringComparison.Ordinal);i++)
            await Task.Delay(1000);
        await Run("sc.exe",["delete",Name]);
        Console.WriteLine($"Service {Name} removed. The data root {ServerSettings.DataRoot} was not touched.");
    }

    private static void RequireWindows()
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows services exist only on Windows.");
    }

    private static async Task<string> Run(string tool,IEnumerable<string> arguments,bool tolerateFailure=false)
    {
        var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,tool))
            {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var argument in arguments) info.ArgumentList.Add(argument);
        using var process=Process.Start(info) ?? throw new InvalidOperationException("Cannot start "+tool);
        var output=await process.StandardOutput.ReadToEndAsync()+await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if(process.ExitCode!=0 && !tolerateFailure)
            throw new InvalidOperationException($"{tool} failed with code {process.ExitCode}"
                +(process.ExitCode==5 ? " (access denied: run this command from an elevated console)" : "")+": "+output.Trim());
        return output;
    }
}
