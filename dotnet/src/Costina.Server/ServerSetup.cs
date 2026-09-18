using System.Diagnostics;
using System.Security.Cryptography;

namespace Costina.Server;

// D5.5 (#27, ADR-011): lo que ejecuta el INSTALADOR en el equipo servidor. El instalador solo copia
// ficheros y llama a estos comandos: toda la logica vive aqui, donde se prueba en CI.
//   setup-server   instalacion nueva: cluster PostgreSQL propio bajo la raiz de datos (solo loopback,
//                  scram, puerto propio) registrado como servicio, provision -> init -> provision-tls
//                  -> install-service -> arranque (con COSTINA_RESTORE_FILE, restore verificado en
//                  lugar de init). Sobre una raiz de datos EXISTENTE (reinstalacion o
//                  actualizacion): vuelve a registrar los servicios, hace COPIA DE SEGURIDAD y despues
//                  upgrade. Nunca inicializa dos veces ni toca datos sin copia previa.
//   stop-services  para motor y base antes de sustituir binarios.
//   remove-server  retira ambos servicios. JAMAS toca la raiz de datos.
// Cada paso reutiliza el comando ya existente lanzando este mismo ejecutable como proceso hijo.
public static class ServerSetup
{
    public const string DatabaseService="CostinaPostgres";
    private const string NetworkService="*S-1-5-20", LocalSystem="*S-1-5-18", Administrators="*S-1-5-32-544";
    private static string Cluster => Path.Combine(ServerSettings.DataRoot,"pg");
    private static string Self => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the engine executable.");

    private static string PgBin(ServerSettings settings)
    {
        var folder=settings.Get("COSTINA_PG_BIN") ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Self)!,"..","pgsql","bin"));
        if(!File.Exists(Path.Combine(folder,"pg_ctl.exe"))) throw new InvalidOperationException($"Bundled PostgreSQL not found under {folder}.");
        return folder;
    }

    public static async Task InstallAsync(ServerSettings settings)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("setup-server is the Windows installer backend.");
        var bin=PgBin(settings);
        var port=settings.Get("COSTINA_PG_PORT") ?? "5544";
        if(!int.TryParse(port,out var numeric) || numeric is < 1024 or > 65535) throw new ArgumentException("Invalid COSTINA_PG_PORT.");
        var hasCluster=File.Exists(Path.Combine(Cluster,"PG_VERSION"));
        var provisioned=File.Exists(ServerSettings.ServerFile);
        if(hasCluster!=provisioned)
            throw new InvalidOperationException($"The data root {ServerSettings.DataRoot} is half-installed (cluster: {hasCluster}, configuration: {provisioned}). Nothing was changed; inspect it before retrying.");
        var fresh=!hasCluster;
        string? bootstrapPassword=null;
        try
        {
            if(fresh)
            {
                foreach(var required in new[]{"COSTINA_TENANT","COSTINA_COMPANY","COSTINA_LOCATION"}) settings.Required(required);
                Step("Creating the PostgreSQL cluster");
                // El directorio de configuracion nace cerrado: ahi vive, unos segundos, la contrasena del superusuario.
                Directory.CreateDirectory(ServerSettings.ConfigDirectory);
                await Tool("icacls.exe",[ServerSettings.ConfigDirectory,"/inheritance:r","/grant:r",LocalSystem+":(OI)(CI)F",Administrators+":(OI)(CI)F"]);
                bootstrapPassword=Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
                var passwordFile=Path.Combine(ServerSettings.ConfigDirectory,"bootstrap.tmp");
                File.WriteAllText(passwordFile,bootstrapPassword);
                try
                {
                    await Tool(Path.Combine(bin,"initdb.exe"),["-D",Cluster,"-U","postgres","--auth=scram-sha-256","--encoding=UTF8",
                        "--locale=C","--data-checksums","--pwfile="+passwordFile]);
                }
                finally { File.Delete(passwordFile); }
                File.AppendAllText(Path.Combine(Cluster,"postgresql.conf"),
                    $"{Environment.NewLine}# Costina: base local del motor. Solo loopback; la LAN habla con el motor por HTTPS, nunca con la base.{Environment.NewLine}"
                    +$"port = {port}{Environment.NewLine}listen_addresses = '127.0.0.1'{Environment.NewLine}");
                await Tool("icacls.exe",[Cluster,"/inheritance:r","/grant:r",LocalSystem+":(OI)(CI)F",Administrators+":(OI)(CI)F",NetworkService+":(OI)(CI)M"]);
            }
            if(!await Registered(DatabaseService))
            {
                Step("Registering the PostgreSQL service");
                await Tool(Path.Combine(bin,"pg_ctl.exe"),["register","-N",DatabaseService,"-D",Cluster,"-S","auto","-w"]);
                // Cuenta integrada de minimos privilegios (la que usa el instalador oficial); sin contrasena que custodiar.
                await Tool("sc.exe",["config",DatabaseService,"obj=",@"NT AUTHORITY\NetworkService","password=",""]);
                await Tool("sc.exe",["description",DatabaseService,"Base de datos local de Costina. Solo escucha en este equipo."]);
                await Tool("sc.exe",["failure",DatabaseService,"reset=","86400","actions=","restart/5000/restart/30000/restart/60000"]);
            }
            Step("Starting PostgreSQL");
            await Tool("sc.exe",["start",DatabaseService],tolerateFailure:true);
            for(var attempt=0;;attempt++)
            {
                if(await Tool(Path.Combine(bin,"pg_isready.exe"),["-h","127.0.0.1","-p",port],tolerateFailure:true)==0) break;
                if(attempt>=90) throw new InvalidOperationException("PostgreSQL did not accept connections; see the Windows event log and "+Path.Combine(Cluster,"log"));
                await Task.Delay(1000);
            }

            var environment=new Dictionary<string,string?>{["COSTINA_PG_BIN"]=bin,["COSTINA_PG_SERVICE"]=DatabaseService};
            if(fresh)
            {
                environment["COSTINA_DB_BOOTSTRAP"]=$"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={bootstrapPassword}";
                environment["COSTINA_DB_NAME"]=settings.Get("COSTINA_DB_NAME") ?? "costina";
                await Child("provision",environment);
                environment.Remove("COSTINA_DB_BOOTSTRAP");
                // Equipo nuevo a partir de una copia: en lugar de un esquema vacio, restauracion VERIFICADA (D5.4).
                if(settings.Get("COSTINA_RESTORE_FILE") is {Length:>0} backupFile) await Child("restore",environment,backupFile);
                else await Child("init",environment);
                if(settings.Get("COSTINA_NO_TLS")!="true") await Child("provision-tls",environment);
            }
            else
            {
                if(await Registered(ServiceInstaller.Name)) await StopAsync(ServiceInstaller.Name);
                // Actualizacion EXPLICITA: primero una copia verificable, despues el esquema. Si la copia falla, no se toca nada.
                await Child("backup",environment);
                await Child("upgrade",environment);
            }
            if(!await Registered(ServiceInstaller.Name)) await Child("install-service",environment);
            Step("Starting the engine");
            await Tool("sc.exe",["start",ServiceInstaller.Name],tolerateFailure:true);
            var enginePort=new ServerSettings(false).Get("COSTINA_PORT") ?? "5088";
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(2)};
            for(var attempt=0;;attempt++)
            {
                try { if((await http.GetAsync($"http://127.0.0.1:{enginePort}/health")).IsSuccessStatusCode) break; }
                catch(Exception e) when (e is HttpRequestException or TaskCanceledException) { }
                if(attempt>=90) throw new InvalidOperationException("The engine service did not answer /health; see "+Path.Combine(ServerSettings.DataRoot,"logs"));
                await Task.Delay(1000);
            }
            Console.WriteLine(fresh
                ? "Costina server installed. Create the first administrator with: Costina.Server.exe first-user"
                : "Costina server updated: backup taken, schema upgraded, services running. Existing data was kept.");
        }
        catch when (fresh)
        {
            // Solo se deshace lo creado EN ESTA ejecucion sobre una raiz que estaba vacia: jamas datos previos.
            Console.WriteLine("Setup failed on a fresh data root: removing the partial installation it created.");
            await RemoveAsync(settings,quiet:true);
            foreach(var partial in new[]{Cluster,ServerSettings.ConfigDirectory,LocalTls.Folder})
                try { if(Directory.Exists(partial)) Directory.Delete(partial,true); } catch(Exception e) when (e is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public static async Task StopServicesAsync()
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if(await Registered(ServiceInstaller.Name)) await StopAsync(ServiceInstaller.Name);
        if(await Registered(DatabaseService)) await StopAsync(DatabaseService);
    }

    public static async Task RemoveAsync(ServerSettings settings,bool quiet=false)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if(await Registered(ServiceInstaller.Name)) await ServiceInstaller.UninstallAsync();
        if(await Registered(DatabaseService))
        {
            await StopAsync(DatabaseService);
            await Tool(Path.Combine(PgBin(settings),"pg_ctl.exe"),["unregister","-N",DatabaseService],tolerateFailure:quiet);
        }
        if(!quiet) Console.WriteLine($"Costina services removed. The data root {ServerSettings.DataRoot} (database, configuration, certificates, backups) was not touched.");
    }

    private static async Task<bool> Registered(string service) => await Tool("sc.exe",["query",service],tolerateFailure:true,silent:true)==0;

    private static async Task StopAsync(string service)
    {
        await Tool("sc.exe",["stop",service],tolerateFailure:true,silent:true);
        for(var attempt=0;attempt<60;attempt++)
        {
            var (code,output)=await Capture("sc.exe",["query",service]);
            if(code!=0 || output.Contains("STOPPED",StringComparison.Ordinal)) return;
            await Task.Delay(1000);
        }
        throw new InvalidOperationException($"Service {service} did not stop.");
    }

    private static void Step(string text) => Console.WriteLine("== "+text);

    private static async Task Child(string command,Dictionary<string,string?> environment,string? argument=null)
    {
        Step(command);
        var info=new ProcessStartInfo(Self){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        info.ArgumentList.Add(command);
        if(argument is not null) info.ArgumentList.Add(argument);
        foreach(var (name,value) in environment) info.Environment[name]=value;
        using var process=Process.Start(info) ?? throw new InvalidOperationException("Cannot start "+command);
        var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Console.Write(await output);
        if(process.ExitCode!=0) throw new InvalidOperationException($"{command} failed with code {process.ExitCode}: {FirstLines(await error)}");
    }

    private static async Task<int> Tool(string tool,IEnumerable<string> arguments,bool tolerateFailure=false,bool silent=false)
    {
        var (code,output)=await Capture(tool,arguments);
        if(code!=0 && !tolerateFailure)
            throw new InvalidOperationException($"{Path.GetFileName(tool)} failed with code {code}"+(code==5 ? " (access denied: run elevated)" : "")+": "+FirstLines(output));
        if(!silent && code!=0) Console.WriteLine($"   ({Path.GetFileName(tool)} returned {code})");
        return code;
    }

    private static async Task<(int Code,string Output)> Capture(string tool,IEnumerable<string> arguments)
    {
        var path=Path.IsPathRooted(tool) ? tool : Path.Combine(Environment.SystemDirectory,tool);
        var info=new ProcessStartInfo(path){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var argument in arguments) info.ArgumentList.Add(argument);
        using var process=Process.Start(info) ?? throw new InvalidOperationException("Cannot start "+tool);
        var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode,await output+await error);
    }

    // Los mensajes de las herramientas no llevan secretos (van por entorno o fichero), pero se acotan igualmente.
    private static string FirstLines(string text) => string.Join(" | ",text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Take(6));
}
