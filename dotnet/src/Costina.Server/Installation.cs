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

    public static string DataRoot
    {
        get
        {
            var root=Environment.GetEnvironmentVariable("COSTINA_DATA") is {Length:>0} custom ? Path.GetFullPath(custom)
                : OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Costina")
                : "/var/lib/costina";
            var program=Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if((root+Path.DirectorySeparatorChar).StartsWith(program+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The data root must live outside the program directory.");
            return root;
        }
    }
    public static string ConfigDirectory => Path.Combine(DataRoot,"config");
    public static string ServerFile => Path.Combine(ConfigDirectory,"server.json");
    public static string OwnerFile => Path.Combine(ConfigDirectory,"owner.json");

    public ServerSettings(bool administrative)
    {
        Load(ServerFile);
        if(administrative) Load(OwnerFile);
    }

    private void Load(string path)
    {
        if(!File.Exists(path)) return;
        using var document=JsonDocument.Parse(File.ReadAllText(path));
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
            var schemaOwner=await Scalar(inside,"SELECT nspowner::regrole::text FROM pg_namespace WHERE nspname='native_d1'") as string;
            if(schemaOwner is not null && schemaOwner!=OwnerRole)
                throw new InvalidOperationException($"Database {database} already holds a native_d1 schema owned by {schemaOwner}; provision never takes over existing data.");
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
