using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace Costina.Server;

// D5.1 (#27, ADR-011): separacion programa/datos y configuracion por fichero. El directorio del
// programa nunca se escribe en marcha; todo lo mutable vive bajo la raiz de datos
// (%ProgramData%\Costina en Windows, o COSTINA_DATA). Las variables de entorno siguen mandando
// (laboratorio y CI); lo que falte se lee de config\server.json. Las credenciales del propietario
// del esquema (config\owner.json) solo se cargan para los comandos administrativos explicitos:
// un arranque normal ni siquiera las lee.
public sealed class ServerSettings
{
    private readonly Dictionary<string,string> file=new(StringComparer.Ordinal);

    // Raiz de una instalacion: la que se usa sin COSTINA_DATA.
    public static string InstallationRoot => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Costina") : "/var/lib/costina";

    public static string DataRoot
    {
        get
        {
            var root=Environment.GetEnvironmentVariable("COSTINA_DATA") is {Length:>0} custom ? Path.GetFullPath(custom) : InstallationRoot;
            var program=Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if((root+Path.DirectorySeparatorChar).StartsWith(program+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The data root must live outside the program directory.");
            return root;
        }
    }
    public static string ConfigDirectory => Path.Combine(DataRoot,"config");
    public static string ServerFile => Path.Combine(ConfigDirectory,"server.json");
    public static string OwnerFile => Path.Combine(ConfigDirectory,"owner.json");

    // C2: una configuracion que existe pero este usuario no puede leer (consola sin administrador en una instalacion: config\ es
    // solo de SYSTEM y Administradores) detiene cualquier orden con un mensaje claro. 'status' la tolera: resume lo que puede leer.
    // Si el ENTORNO define el modo (laboratorio, CI) la ejecucion no depende del fichero y uno ilegible se ignora, como hasta ahora:
    // las variables de entorno mandan. Desde C2.1 el laboratorio solo puede tratarse de su propia carpeta (ver abajo).
    public ServerSettings(bool administrative,bool tolerateUnreadable=false)
    {
        // C2.1: el laboratorio (y con el todas las pruebas) nunca lee la configuracion de una instalacion. Sin carpeta propia su raiz
        // seria la de la instalacion real del PC: desde una consola de administrador mezclaria su server.json (COSTINA_MODE=installation
        // gana a COSTINA_LAB_MODE: registros, copias, certificado y puertos reales) y, en init-lab, las credenciales de owner.json.
        // Se comprueba ANTES de abrir ningun fichero; la mezcla, antes de leer owner.json.
        var laboratory=LaboratoryFromEnvironment;
        if(laboratory) RequireOwnDataRoot();
        Load(ServerFile);
        if(laboratory && file.TryGetValue("COSTINA_MODE",out var fileMode) && fileMode!="laboratory")
            throw new InvalidOperationException(MixedMessage(fileMode));
        if(administrative) Load(OwnerFile);
        var environmentDefinesMode=Environment.GetEnvironmentVariable("COSTINA_MODE") is {Length:>0} || Environment.GetEnvironmentVariable("COSTINA_LAB_MODE") is {Length:>0};
        if(Unreadable is not null && !tolerateUnreadable && !environmentDefinesMode) throw new InvalidOperationException(UnreadableMessage(Unreadable));
    }

    // El ENTORNO pide el laboratorio, con la misma precedencia que Program.cs: COSTINA_MODE manda y, sin el, COSTINA_LAB_MODE=true.
    public static bool LaboratoryFromEnvironment => Environment.GetEnvironmentVariable("COSTINA_MODE") is {Length:>0} mode
        ? mode=="laboratory" : Environment.GetEnvironmentVariable("COSTINA_LAB_MODE")=="true";

    // Una carpeta propia: COSTINA_DATA definida y fuera de la raiz de una instalacion (ni ella ni nada dentro).
    private static void RequireOwnDataRoot()
    {
        var installation=Path.TrimEndingDirectorySeparator(Path.GetFullPath(InstallationRoot));
        var comparison=OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var own=Environment.GetEnvironmentVariable("COSTINA_DATA") is {Length:>0} custom
            && !(Path.TrimEndingDirectorySeparator(Path.GetFullPath(custom))+Path.DirectorySeparatorChar).StartsWith(installation+Path.DirectorySeparatorChar,comparison);
        if(!own) throw new InvalidOperationException(
            $"El laboratorio y las pruebas necesitan su propia carpeta de datos: define COSTINA_DATA con una carpeta que no sea la de una instalacion ({installation}). Nunca leen la configuracion de Costina instalado."
            + Environment.NewLine + $"The laboratory and the tests need their own data folder: set COSTINA_DATA to a folder outside an installation's ({installation}). They never read an installed Costina's configuration.");
    }

    private static string MixedMessage(string fileMode) =>
        $"COSTINA_DATA ({DataRoot}) tiene la configuracion de una instalacion (server.json dice COSTINA_MODE={fileMode}): el laboratorio y las pruebas nunca la usan. Define COSTINA_DATA con una carpeta propia."
        + Environment.NewLine + $"COSTINA_DATA ({DataRoot}) holds an installation's configuration (server.json says COSTINA_MODE={fileMode}): the laboratory and the tests never use it. Set COSTINA_DATA to a folder of their own.";

    // Primer fichero de configuracion que existe y no se pudo leer por permisos (null si todo se leyo o no hay configuracion).
    public string? Unreadable { get; private set; }

    public static string UnreadableMessage(string path) => OperatingSystem.IsWindows()
        ? $"No se puede leer la configuracion de Costina ({path}): abre PowerShell como administrador (clic derecho > Ejecutar como administrador) y repite la orden."
            + Environment.NewLine + $"Costina's configuration ({path}) is not readable by this user: open PowerShell as administrator (right click > Run as administrator) and run the command again."
        : $"No se puede leer la configuracion de Costina ({path}): repite la orden con sudo o como el usuario de la instalacion."
            + Environment.NewLine + $"Costina's configuration ({path}) is not readable by this user: run the command again with sudo or as the installation user.";

    private void Load(string path)
    {
        // C2: se intenta leer directamente. File.Exists da FALSO ante un acceso denegado (sin administrador, server.json parecia no
        // existir y el motor acababa pidiendo COSTINA_MODE); solo 'no existe' (fichero o carpeta) cuenta como configuracion ausente.
        string text;
        try { text=File.ReadAllText(path); }
        catch(Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return; }
        catch(UnauthorizedAccessException) { Unreadable??=path; return; }
        using var document=JsonDocument.Parse(text);
        foreach(var property in document.RootElement.EnumerateObject())
            if(property.Name.StartsWith("COSTINA_",StringComparison.Ordinal) && property.Value.ValueKind==JsonValueKind.String)
                file[property.Name]=property.Value.GetString()!;
    }

    public string? Get(string name) =>
        Environment.GetEnvironmentVariable(name) is {Length:>0} fromEnvironment ? fromEnvironment
        : file.TryGetValue(name,out var fromFile) ? fromFile : null;
    public string Required(string name) => Get(name)
        ?? throw new InvalidOperationException($"Missing {name}; no default credentials are provided.");
}

// "provision": unica operacion que usa el superusuario de PostgreSQL. Crea (o re-clavea, si la
// configuracion aun no existe) los roles costina_owner y costina_runtime, la base y sus permisos
// de conexion, y escribe config\owner.json y config\server.json. Se niega a sobrescribir una
// configuracion existente. Nunca imprime contrasenas. Una instalacion por cluster de PostgreSQL.
public static partial class Provisioner
{
    public const string OwnerRole="costina_owner", RuntimeRole="costina_runtime";
    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$")] private static partial Regex DatabaseName();

    public static async Task RunAsync(ServerSettings settings)
    {
        var bootstrap=new NpgsqlConnectionStringBuilder(settings.Required("COSTINA_DB_BOOTSTRAP"));
        var database=settings.Required("COSTINA_DB_NAME");
        if(!DatabaseName().IsMatch(database)) throw new ArgumentException("COSTINA_DB_NAME must be a plain lowercase identifier.");
        var mode=settings.Get("COSTINA_MODE") ?? "installation";
        if(mode is not ("installation" or "laboratory")) throw new ArgumentException("COSTINA_MODE must be installation or laboratory.");
        var tenant=settings.Required("COSTINA_TENANT"); var company=settings.Required("COSTINA_COMPANY");
        var location=settings.Required("COSTINA_LOCATION"); var port=settings.Get("COSTINA_PORT") ?? "5088";
        if(File.Exists(ServerSettings.ServerFile) || File.Exists(ServerSettings.OwnerFile))
            throw new InvalidOperationException("This data root is already provisioned; refusing to overwrite its configuration.");

        var ownerPassword=Secret(); var runtimePassword=Secret();
        await using(var cluster=NpgsqlDataSource.Create(bootstrap.ConnectionString))
        {
            if(!Equals(await Scalar(cluster,"SELECT rolsuper FROM pg_roles WHERE rolname=current_user"),true))
                throw new InvalidOperationException("COSTINA_DB_BOOTSTRAP must be a PostgreSQL superuser; it is used only by provision.");
            foreach(var (role,password) in new[]{(OwnerRole,ownerPassword),(RuntimeRole,runtimePassword)})
            {
                var exists=await Scalar(cluster,"SELECT 1 FROM pg_roles WHERE rolname=@name",("name",role)) is not null;
                // Identificadores constantes y secretos hexadecimales generados aqui: nada procede del usuario.
                await Execute(cluster,(exists?"ALTER":"CREATE")+$" ROLE {role} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'");
            }
            if(await Scalar(cluster,"SELECT 1 FROM pg_database WHERE datname=@name",("name",database)) is null)
                await Execute(cluster,$"CREATE DATABASE {database} OWNER {OwnerRole}");
        }
        var target=new NpgsqlConnectionStringBuilder(bootstrap.ConnectionString){Database=database};
        await using(var inside=NpgsqlDataSource.Create(target.ConnectionString))
        {
            // E1b: el esquema del nucleo es "core" (v2); "native_d1" es una base v1 aun sin upgrade. Ninguno se adopta si es de otro.
            var schemaOwner=await Scalar(inside,"SELECT nspowner::regrole::text FROM pg_namespace WHERE nspname IN ('core','native_d1') ORDER BY nspname LIMIT 1") as string;
            if(schemaOwner is not null && schemaOwner!=OwnerRole)
                throw new InvalidOperationException($"Database {database} already holds a Costina schema owned by {schemaOwner}; provision never takes over existing data.");
            await Execute(inside,$"ALTER DATABASE {database} OWNER TO {OwnerRole}");
            await Execute(inside,$"REVOKE ALL ON DATABASE {database} FROM PUBLIC");
            await Execute(inside,$"GRANT CONNECT ON DATABASE {database} TO {RuntimeRole}");
        }

        string Connection(string role,string password) => new NpgsqlConnectionStringBuilder
            {Host=bootstrap.Host,Port=bootstrap.Port,Database=database,Username=role,Password=password}.ConnectionString;
        Directory.CreateDirectory(ServerSettings.ConfigDirectory);
        // owner.json primero y server.json al final: server.json marca la provision como completa.
        WritePrivate(ServerSettings.OwnerFile,new Dictionary<string,string>{["COSTINA_DB_OWNER"]=Connection(OwnerRole,ownerPassword)});
        WritePrivate(ServerSettings.ServerFile,new Dictionary<string,string>{
            ["COSTINA_MODE"]=mode,["COSTINA_DB"]=Connection(RuntimeRole,runtimePassword),
            ["COSTINA_TENANT"]=tenant,["COSTINA_COMPANY"]=company,["COSTINA_LOCATION"]=location,["COSTINA_PORT"]=port}
            // Ajustes opcionales (no secretos) que el servicio necesitara y que su entorno no hereda de esta consola.
            .Concat(new[]{"COSTINA_TLS_PORT","COSTINA_PG_BIN","COSTINA_BACKUP_COPY","COSTINA_BACKUP_HOUR","COSTINA_BACKUP_KEEP"}
                .Where(name=>settings.Get(name) is not null).Select(name=>KeyValuePair.Create(name,settings.Get(name)!)))
            .ToDictionary(pair=>pair.Key,pair=>pair.Value));
        Console.WriteLine($"Provisioned roles and database {database}. Configuration written under {ServerSettings.ConfigDirectory}. Next: init.");
    }

    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static void WritePrivate(string path,Dictionary<string,string> values)
    {
        var temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(values,new JsonSerializerOptions{WriteIndented=true}));
        // En Windows la ACL del directorio de configuracion la fija la instalacion del servicio (D5.2).
        if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        File.Move(temporary,path,overwrite:false);
    }

    private static async Task<object?> Scalar(NpgsqlDataSource source,string sql,params (string Name,object Value)[] values)
    {
        await using var command=source.CreateCommand(sql);
        foreach(var (name,value) in values) command.Parameters.AddWithValue(name,value);
        var result=await command.ExecuteScalarAsync();
        return result is DBNull ? null : result;
    }

    private static async Task Execute(NpgsqlDataSource source,string sql)
    {
        await using var command=source.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }
}
