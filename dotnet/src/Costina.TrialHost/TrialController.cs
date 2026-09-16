using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Costina.Client;
using Npgsql;

namespace Costina.TrialHost;

// Visible, user-session trial supervisor. NOT the eventual Windows SCM production service.
// Owns only its packaged components and dedicated data directory; no elevation or network downloads.
public sealed class TrialController : IDisposable
{
    public const string Version="0.2.1-d1.3";
    public static string DefaultHome => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Xeitoso","CostinaTrialD13");
    public string Home {get;}
    public string Package {get;}
    public TrialSettings? Settings {get;private set;}
    public bool Initialized => Settings?.Ready==true;
    public bool Running => server is not null && !server.HasExited;
    public bool DatabaseStarted {get;private set;}
    public bool HasRunningComponents => Running || DatabaseStarted;
    public int OpenClients => clients.Count(p=>!p.HasExited);
    private readonly FileStream installationLock;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly List<Process> clients=[];
    private Process? server;
    private Task? outputTask,errorTask;
    private bool disposed;
    public Action<string>? Progress {get;set;}
    private string Data=>Path.Combine(Home,"data");
    private string Pg(string name)=>Path.Combine(Package,"pgsql","bin",name+".exe");
    private string Engine=>Path.Combine(Package,"server","Costina.Server.exe");

    public TrialController(string package,string? home=null)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Solo Windows en este corte.");
        Package=Path.GetFullPath(package); Home=Path.GetFullPath(home ?? DefaultHome);
        if(Home.TrimEnd(Path.DirectorySeparatorChar).Equals(Package.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)
            || Home.StartsWith(Package.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Los datos deben estar fuera de la carpeta del programa.");
        if(!File.Exists(Path.Combine(Package,"trial-package.json")) || !File.Exists(Engine) || !File.Exists(Pg("pg_ctl"))
            || !File.Exists(Path.Combine(Package,"desktop","Costina.Desktop.exe")))
            throw new InvalidOperationException("Paquete incompleto. Instala o extrae todos los componentes juntos.");
        using(var marker=JsonDocument.Parse(File.ReadAllText(Path.Combine(Package,"trial-package.json"))))
            if(marker.RootElement.GetProperty("version").GetString()!=Version || marker.RootElement.GetProperty("format").GetInt32()!=1)
                throw new InvalidOperationException("El asistente y el paquete no son de la misma versión.");
        if(Directory.Exists(Home) && !File.Exists(Path.Combine(Home,"installation.dat"))
            && Directory.EnumerateFileSystemEntries(Home).Any(x=>Path.GetFileName(x)!="host.lock"))
            throw new InvalidOperationException("La carpeta contiene datos sin configuración reconocida. No se sobrescribe ni se reinicializa.");
        SettingsVault.RestrictDirectory(Home);
        try {installationLock=new FileStream(Path.Combine(Home,"host.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException) {throw new InvalidOperationException("Ya hay un asistente usando esta instalación. Vuelve a la ventana abierta.");}
        try {
            if(File.Exists(Path.Combine(Home,"installation.dat"))) Settings=SettingsVault.Load(Home);
        } catch {installationLock.Dispose();throw;}
    }
    private void Report(string text)=>Progress?.Invoke(text);
    public async Task InitializeAsync()
    {
        await gate.WaitAsync();
        try {
            if(Initialized) {Report("Instalación existente: no se cambian usuarios, catálogo ni datos.");return;}
            Settings ??= TrialSettings.Create(); SettingsVault.Save(Home,Settings);
            Report("Preparando PostgreSQL nativo con autenticación SCRAM y directorio propio...");
            var version=await NativeProcess.Run(Pg("pg_ctl"),["--version"],Package);
            if(!version.Output.Contains("17.11",StringComparison.Ordinal)) throw new InvalidOperationException("Este paquete requiere PostgreSQL 17.11.");
            if(!File.Exists(Path.Combine(Data,"PG_VERSION"))) {
                if(Directory.Exists(Data) && Directory.EnumerateFileSystemEntries(Data).Any())
                    throw new InvalidOperationException("Inicialización interrumpida: se conservan los archivos; requiere revisión, no reset automático.");
                var passwordFile=Path.Combine(Home,"bootstrap-password.tmp");
                try {
                    await File.WriteAllTextAsync(passwordFile,Settings.BootstrapPassword+"\n",new UTF8Encoding(false));
                    await NativeProcess.Run(Pg("initdb"),["-D",Data,"-U","costina_bootstrap","--auth=scram-sha-256","--encoding=UTF8","--locale=C","--data-checksums","--pwfile="+passwordFile],Package);
                } finally {if(File.Exists(passwordFile)) File.Delete(passwordFile);}
                await File.AppendAllTextAsync(Path.Combine(Data,"postgresql.conf"),
                    $"\n# Costina user-owned trial: never expose to LAN\nlisten_addresses = '127.0.0.1'\nport = {Settings.DatabasePort}\nmax_connections = 30\nshared_buffers = '64MB'\npassword_encryption = 'scram-sha-256'\n",new UTF8Encoding(false));
            }
            await StartPostgres();
            Report("Creando base aislada y usuario de ejecución sin permisos administrativos...");
            await ProvisionDatabase();
            await NativeProcess.Run(Engine,["init-lab"],Path.GetDirectoryName(Engine)!,ServerEnvironment("owner"));
            await using(var owner=new NpgsqlConnection(Settings.Connection("owner"))) {
                await owner.OpenAsync();
                await using var grant=new NpgsqlCommand("""
                    REVOKE ALL ON SCHEMA native_d1 FROM PUBLIC;
                    GRANT USAGE ON SCHEMA native_d1 TO costina_runtime;
                    GRANT SELECT ON ALL TABLES IN SCHEMA native_d1 TO costina_runtime;
                    GRANT INSERT, UPDATE ON native_d1.services, native_d1.occupancies, native_d1.accounts TO costina_runtime;
                    GRANT INSERT ON native_d1.commands, native_d1.audit, native_d1.outbox TO costina_runtime;
                    """,owner);
                await grant.ExecuteNonQueryAsync();
            }
            Settings=Settings with {Ready=true}; SettingsVault.Save(Home,Settings);
            Report("Datos ficticios preparados. El inicio normal no vuelve a cargar semillas.");
        } finally {gate.Release();}
    }
    private async Task ProvisionDatabase()
    {
        var s=Settings!;
        await using var connection=new NpgsqlConnection(s.Connection("bootstrap","postgres"));await connection.OpenAsync();
        foreach(var (name,password) in new[]{("costina_owner",s.OwnerPassword),("costina_runtime",s.RuntimePassword)}) {
            await using var exists=new NpgsqlCommand("SELECT count(*) FROM pg_roles WHERE rolname=@name",connection);
            exists.Parameters.AddWithValue("name",name);
            if((long)(await exists.ExecuteScalarAsync())! == 0) {
                // Both names are constants; password is validated random hexadecimal, never user input.
                await using var create=new NpgsqlCommand($"CREATE ROLE {name} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'",connection);
                await create.ExecuteNonQueryAsync();
            }
        }
        await using var check=new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name",connection);
        check.Parameters.AddWithValue("name",TrialSettings.Database);
        if((long)(await check.ExecuteScalarAsync())! == 0) {
            await using var create=new NpgsqlCommand($"CREATE DATABASE {TrialSettings.Database} OWNER costina_owner",connection);await create.ExecuteNonQueryAsync();
        }
    }
    private async Task StartPostgres()
    {
        var status=await NativeProcess.Run(Pg("pg_ctl"),["status","-D",Data],Package,allowFailure:true);
        if(status.Code==3) {
            EnsurePortFree(Settings!.DatabasePort);
            await NativeProcess.Run(Pg("pg_ctl"),["start","-D",Data,"-l",Path.Combine(Home,"postgres.log"),"-w","-t","30"],Package);
        } else if(status.Code!=0) throw new InvalidOperationException("No se puede comprobar el PostgreSQL de esta instalación.");
        await VerifyDatabaseDirectory(); DatabaseStarted=true;
    }
    private async Task VerifyDatabaseDirectory()
    {
        await using var connection=new NpgsqlConnection(Settings!.Connection("bootstrap","postgres")); await connection.OpenAsync();
        await using var command=new NpgsqlCommand("SHOW data_directory",connection);
        var actual=Path.GetFullPath((string)(await command.ExecuteScalarAsync())!);
        if(!actual.TrimEnd(Path.DirectorySeparatorChar).Equals(Data.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El puerto pertenece a otra base. No se modifica ni se detiene.");
    }
    public async Task StartAsync()
    {
        await gate.WaitAsync();
        try {
            if(!Initialized) throw new InvalidOperationException("Pulsa primero Preparar datos ficticios.");
            if(Running) {Report("Servidor ya iniciado; sin reinicialización.");return;}
            EnsurePortFree(Settings!.ApiPort); await StartPostgres();
            var info=NativeProcess.Info(Engine,[],Path.GetDirectoryName(Engine)!,ServerEnvironment("runtime"));
            info.RedirectStandardInput=info.RedirectStandardOutput=info.RedirectStandardError=true;
            info.Environment["COSTINA_SUPERVISED_TRIAL"]="true";
            server=Process.Start(info) ?? throw new InvalidOperationException("No se pudo abrir el motor.");
            outputTask=Drain(server.StandardOutput,Path.Combine(Home,"server-output.log"));
            errorTask=Drain(server.StandardError,Path.Combine(Home,"server-error.log"));
            for(int n=0;n<100;n++) {
                if(server.HasExited) throw new InvalidOperationException("El motor no arrancó. Revisa el diagnóstico sin borrar datos.");
                try {
                    using var api=Client("main");var identity=await api.GetAsync<SessionInfo>("session");
                    if(identity.TenantId!=Settings.Tenant || identity.Role!="main") throw new InvalidOperationException("Identidad del servidor inesperada.");
                    Report("Servidor y PostgreSQL listos. Puedes abrir los puestos de prueba.");return;
                } catch(HttpRequestException) {await Task.Delay(150);}
            }
            throw new TimeoutException("El servidor no confirmó disponibilidad.");
        } finally {gate.Release();}
    }
    private static async Task Drain(StreamReader reader,string file)
    {
        await using var writer=new StreamWriter(new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
        while(await reader.ReadLineAsync() is {} line) {await writer.WriteLineAsync(line);await writer.FlushAsync();}
    }
    private Dictionary<string,string> ServerEnvironment(string role)
    {
        var s=Settings!;
        return new() { ["COSTINA_LAB_MODE"]="true",["COSTINA_DB"]=s.Connection(role),["COSTINA_TENANT"]=s.Tenant,
            ["COSTINA_COMPANY"]=s.Company,["COSTINA_LOCATION"]=s.Location,["COSTINA_PORT"]=s.ApiPort.ToString(),
            ["COSTINA_KEY_MAIN"]=s.MainKey,["COSTINA_KEY_SERVICE"]=s.ServiceKey,["COSTINA_KEY_KITCHEN"]=s.KitchenKey };
    }
    public ApiClient Client(string role) => new(Settings!.Endpoint,Settings.Key(role));
    public void LaunchDesktop(string role)
    {
        if(!Running) throw new InvalidOperationException("Inicia el servidor antes de abrir un puesto.");
        var info=NativeProcess.Info(Path.Combine(Package,"desktop","Costina.Desktop.exe"),[],Path.Combine(Package,"desktop"),
            new Dictionary<string,string>{["COSTINA_DESKTOP_URL"]=Settings!.Endpoint.ToString(),["COSTINA_DESKTOP_KEY"]=Settings.Key(role)});
        clients.Add(Process.Start(info) ?? throw new InvalidOperationException("No se pudo abrir el puesto."));
    }
    public async Task StopAsync()
    {
        await gate.WaitAsync();
        try {
            if(OpenClients>0) throw new InvalidOperationException("Cierra primero los puestos de prueba para no interrumpir una orden.");
            if(Running) {
                await server!.StandardInput.WriteLineAsync("STOP");await server.StandardInput.FlushAsync();
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await server.WaitForExitAsync(timeout.Token);
                if(outputTask is not null) await outputTask;if(errorTask is not null) await errorTask;
            }
            server?.Dispose();server=null;
            if(File.Exists(Path.Combine(Data,"PG_VERSION"))) {
                var status=await NativeProcess.Run(Pg("pg_ctl"),["status","-D",Data],Package,allowFailure:true);
                if(status.Code==0) {
                    await VerifyDatabaseDirectory();
                    await NativeProcess.Run(Pg("pg_ctl"),["stop","-D",Data,"-m","fast","-w","-t","30"],Package);
                } else if(status.Code!=3) throw new InvalidOperationException("Estado PostgreSQL desconocido; no se fuerza su parada.");
            }
            DatabaseStarted=false; Report("Instalación detenida. Datos conservados.");
        } finally {gate.Release();}
    }
    private static void EnsurePortFree(int port)
    {
        var listener=new TcpListener(IPAddress.Loopback,port);
        try {listener.Start();} catch(SocketException) {throw new InvalidOperationException($"El puerto {port} está ocupado. No se detiene otro programa ni se cambia la configuración a ciegas.");}
        finally {listener.Stop();}
    }
    public void Dispose()
    {
        if(disposed)return;
        if(HasRunningComponents) throw new InvalidOperationException("Detén la instalación antes de cerrar el asistente.");
        foreach(var client in clients)client.Dispose();installationLock.Dispose();gate.Dispose();disposed=true;
    }
}
