using System.Data;
using System.Text.RegularExpressions;
using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E1b (ADR-012): lo que un modulo aporta al esquema. Name es su esquema PostgreSQL; Schema y Grants son sus
// schema.sql y grants.sql (recursos embebidos en el ensamblado del modulo). El nucleo los aplica tras los suyos.
public sealed record ModuleSchema(string Name, string Schema, string Grants)
{
    public static ModuleSchema FromAssembly(System.Reflection.Assembly assembly, string name)
        => new(name, Resource(assembly, assembly.GetName().Name + ".schema.sql"), Resource(assembly, assembly.GetName().Name + ".grants.sql"));
    internal static string Resource(System.Reflection.Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Resource missing: " + resource);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

// Current-state persistence, not event sourcing. SQL identifiers are constants, never user input.
public sealed partial class PostgresStore(NpgsqlDataSource dataSource)
{
    public const int SchemaVersion = 5;
    [GeneratedRegex("^[a-z][a-z0-9_]{0,30}$")] private static partial Regex SchemaName();
    public static string CheckedSchema(string name)
        => SchemaName().IsMatch(name) && name != "public" ? name : throw new ArgumentException("Invalid schema name: " + name);

    // init / upgrade / restore (rol PROPIETARIO). Una sola transaccion: migracion v1->v2 si la base sigue en native_d1,
    // esquema del nucleo, esquema de cada modulo COMPILADO (activo o no: sus datos nunca desaparecen) y, si "provision"
    // creo el rol de ejecucion, las concesiones del nucleo y de cada modulo. O todo o nada.
    public async Task<bool> InitializeAsync(IReadOnlyList<ModuleSchema> modules, CancellationToken ct = default)
    {
        foreach (var module in modules) CheckedSchema(module.Name);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        async Task Run(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync(ct);
        }
        async Task<bool> Exists(string relation)
        {
            await using var command = new NpgsqlCommand($"SELECT to_regclass('{relation}') IS NOT NULL", connection, transaction);
            return Equals(await command.ExecuteScalarAsync(ct), true);
        }
        await Run("SELECT pg_advisory_xact_lock(748215091501)");
        var assembly = typeof(PostgresStore).Assembly;
        var upgraded = false;
        if (await Exists("native_d1.schema_version") && !await Exists("core.schema_version"))
        {
            await Run(ModuleSchema.Resource(assembly, "Costina.Core.Persistence.upgrade-v2.sql"));
            upgraded = true;
        }
        // Migraciones encadenadas: de la version guardada hasta la de estos binarios, una a una (upgrade-v{n}.sql).
        if (await Exists("core.schema_version"))
        {
            int current;
            await using (var version = new NpgsqlCommand("SELECT version FROM core.schema_version", connection, transaction))
                current = (int)(await version.ExecuteScalarAsync(ct) ?? throw new InvalidDataException("Schema version row missing."));
            if (current > SchemaVersion) throw new InvalidDataException($"The database schema is version {current}, newer than these binaries ({SchemaVersion}).");
            for (var next = current + 1; next <= SchemaVersion; next++)
            {
                await Run(ModuleSchema.Resource(assembly, $"Costina.Core.Persistence.upgrade-v{next}.sql"));
                upgraded = true;
            }
        }
        await Run(ModuleSchema.Resource(assembly, "Costina.Core.Persistence.schema.sql"));
        foreach (var module in modules) await Run(module.Schema);
        // D5.1: privilegios minimos del rol de ejecucion, solo si "provision" lo creo. En un laboratorio
        // de un solo rol no hay nada que conceder y el esquema queda como siempre.
        bool runtimeRole;
        await using (var role = new NpgsqlCommand("SELECT 1 FROM pg_roles WHERE rolname='costina_runtime'", connection, transaction))
            runtimeRole = await role.ExecuteScalarAsync(ct) is not null;
        if (runtimeRole)
        {
            await Run(ModuleSchema.Resource(assembly, "Costina.Core.Persistence.grants.sql"));
            foreach (var module in modules) await Run(module.Grants);
        }
        await using (var version = new NpgsqlCommand("SELECT version FROM core.schema_version", connection, transaction))
            if (!Equals(await version.ExecuteScalarAsync(ct), SchemaVersion)) throw new InvalidDataException("Schema version mismatch after initialization.");
        await transaction.CommitAsync(ct);
        return upgraded;
    }

    // E2/E3: lo que existe SIEMPRE en un ambito: la estacion de PASE (validacion y revision de pases, D4.3), la tarifa GENERAL
    // y los impuestos por defecto (IVA de hosteleria en Espana; editables). Idempotente; rol propietario.
    public async Task EnsureOrganizationAsync(BusinessScope scope, CancellationToken ct = default)
    {
        foreach (var sql in new[] {
            "INSERT INTO core.stations (tenant,company,location,id,name,kind,sort,active) VALUES (@tenant,@company,@location,'pase','Pase','pass',0,true) ON CONFLICT DO NOTHING",
            "INSERT INTO core.tariffs (tenant,company,location,id,name,sort,active) VALUES (@tenant,@company,@location,'general','General',0,true) ON CONFLICT DO NOTHING",
            "INSERT INTO core.taxes (tenant,company,location,id,name,rate,active) VALUES (@tenant,@company,@location,'iva-10','IVA 10 %',10.00,true)," +
            "(@tenant,@company,@location,'iva-21','IVA 21 %',21.00,true),(@tenant,@company,@location,'iva-4','IVA 4 %',4.00,true),(@tenant,@company,@location,'iva-0','Exento',0.00,true) ON CONFLICT DO NOTHING" })
        {
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("tenant", scope.TenantId); command.Parameters.AddWithValue("company", scope.CompanyId);
            command.Parameters.AddWithValue("location", scope.LocationId);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    // D5.6: instalacion de demostracion = los fixtures ficticios dejaron su orden idempotente en este ambito.
    // La marca viaja con las copias y no necesita esquema propio.
    public async Task<bool> IsDemoAsync(BusinessScope scope, CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM core.commands WHERE tenant=@tenant AND company=@company AND location=@location AND actor='lab-initializer' AND key='lab-fixtures-v1')");
        command.Parameters.AddWithValue("tenant", scope.TenantId); command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }

    public async Task<bool> HasConfigurationAsync(BusinessScope scope, CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM core.products WHERE tenant=@tenant AND company=@company AND location=@location) OR " +
            "EXISTS (SELECT 1 FROM core.tables WHERE tenant=@tenant AND company=@company AND location=@location)");
        command.Parameters.AddWithValue("tenant", scope.TenantId); command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }

    // D5.2: cola de avisos sin publicar del ambito, para el diagnostico. Solo cuenta y antiguedad.
    public async Task<(long Pending, DateTimeOffset? OldestAt)> OutboxBacklogAsync(BusinessScope scope, CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT count(*), min(occurred_at) FROM core.outbox WHERE tenant=@tenant AND company=@company AND location=@location AND published_at IS NULL");
        command.Parameters.AddWithValue("tenant", scope.TenantId); command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1));
    }

    // D5.1: "init" solo actua sobre una base sin esquema y "upgrade" solo sobre una que ya lo tiene (v2 "core" o v1 "native_d1").
    public async Task<bool> SchemaExistsAsync(CancellationToken ct = default)
    {
        // Por el catalogo (no exige privilegios sobre el esquema): el rol de ejecucion tambien la usa para explicar por que no arranca.
        await using var command = dataSource.CreateCommand("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname IN ('core','native_d1'))");
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }

    // D5.1: el motor en marcha no debe poder cambiar el esquema ni borrar auditoria. Verdadero si la conexion de
    // ejecucion es superusuario, puede crear en el esquema del nucleo o en el de cualquier modulo, o puede borrar/reescribir auditoria.
    public async Task<bool> RuntimeOverprivilegedAsync(CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT (SELECT rolsuper FROM pg_roles WHERE rolname=current_user) " +
            "OR EXISTS (SELECT 1 FROM pg_namespace WHERE nspname NOT IN ('public','information_schema') AND nspname NOT LIKE 'pg\\_%' AND has_schema_privilege(current_user,nspname,'CREATE')) " +
            "OR has_table_privilege(current_user,'core.audit','DELETE') " +
            "OR has_table_privilege(current_user,'core.audit','UPDATE')");
        return Equals(await command.ExecuteScalarAsync(ct), true);
    }

    // Arranque normal: la base debe estar en la version de estos binarios y tener el esquema de cada modulo compilado.
    public async Task CheckAsync(IEnumerable<string> moduleSchemas, CancellationToken ct = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT version FROM core.schema_version");
            if (!Equals(await command.ExecuteScalarAsync(ct), SchemaVersion))
                throw new InvalidDataException($"The database schema is not version {SchemaVersion}: run upgrade with these binaries.");
        }
        catch (PostgresException e) when (e.SqlState == "42P01")
        {
            throw new InvalidDataException(await SchemaExistsAsync(ct)
                ? "This build splits the schema into core and modules (E1b): run upgrade (the installer does it after a backup)."
                : "Run the explicit schema initialization first (init, or init-lab in the laboratory).");
        }
        foreach (var schema in moduleSchemas)
        {
            await using var command = dataSource.CreateCommand("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname=@name)");
            command.Parameters.AddWithValue("name", CheckedSchema(schema));
            if (!Equals(await command.ExecuteScalarAsync(ct), true)) throw new InvalidDataException($"Schema of module {schema} is missing: run upgrade.");
        }
        await InstallationAsync(ct);
    }

    // Identidad estable de esta instalacion, creada una sola vez por init; no cambia al reiniciar.
    public async Task<Guid> InstallationAsync(CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand("SELECT id FROM core.installation");
        return await command.ExecuteScalarAsync(ct) is Guid id ? id
            : throw new InvalidDataException("Installation identity missing; run init once more.");
    }

    // E1b: modulos ACTIVOS de esta instalacion (core.installation.modules). NULL = todos los compilados en el binario.
    public async Task<IReadOnlyList<string>?> ActiveModulesAsync(CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand("SELECT modules FROM core.installation");
        return await command.ExecuteScalarAsync(ct) is string[] modules ? modules : null;
    }

    public async Task<T> ReadAsync<T>(ExecutionIdentity identity, Func<Unit, Task<T>> read, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await command.ExecuteNonQueryAsync(ct);
        var result = await read(new Unit(connection, transaction, identity, "", ct));
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<string> ExecuteAsync(ExecutionIdentity identity, string key, string fingerprint,
        Func<Unit, Task<object>> work, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl))
            throw new ArgumentException("A nonempty Idempotency-Key of at most 128 characters is required.");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var unit = new Unit(connection, transaction, identity, key, ct);
        await unit.Sql("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '15s'", []);
        await unit.Sql("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))",
            [("key", Wire.Encode(new { identity.Scope, identity.ActorId, key }))]);
        var replay = await unit.Rows("SELECT fingerprint, response FROM core.commands WHERE " + Unit.ScopeWhere + " AND actor=@actor AND key=@key",
            [("actor", identity.ActorId), ("key", key)], r => (r.GetString(0), r.GetString(1)));
        if (replay.Count > 0)
        {
            if (replay[0].Item1 != fingerprint) throw new StoreConflict("idempotency_conflict", "The key was already used with a different command.");
            await transaction.CommitAsync(ct);
            return replay[0].Item2;
        }
        var response = Wire.Encode(await work(unit));
        await unit.Sql("INSERT INTO core.commands (tenant,company,location,actor,key,fingerprint,response) VALUES (@tenant,@company,@location,@actor,@key,@fingerprint,@response)",
            [("actor", identity.ActorId), ("key", key), ("fingerprint", fingerprint), ("response", response)]);
        await transaction.CommitAsync(ct);
        return response;
    }
}

public sealed record StoredAccount(long Version, SettlementAccount Entity);

public sealed partial class Unit(NpgsqlConnection connection, NpgsqlTransaction transaction,
    ExecutionIdentity identity, string key, CancellationToken ct)
{
    public const string ScopeWhere = "tenant=@tenant AND company=@company AND location=@location";
    [GeneratedRegex("^[a-z][a-z0-9_]{0,30}\\.[a-z][a-z0-9_]{0,62}$")] private static partial Regex QualifiedTable();
    private readonly BusinessScope scope = identity.Scope;

    private NpgsqlCommand Command(string sql, (string Name, object Value)[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("tenant", scope.TenantId);
        command.Parameters.AddWithValue("company", scope.CompanyId);
        command.Parameters.AddWithValue("location", scope.LocationId);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return command;
    }
    public async Task<int> Sql(string sql, (string Name, object Value)[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteNonQueryAsync(ct);
    }
    public async Task<List<T>> Rows<T>(string sql, (string Name, object Value)[] values, Func<NpgsqlDataReader,T> project)
    {
        await using var command = Command(sql, values);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(project(reader));
        return result;
    }
    public async Task<StoredAccount> Account(string serviceId)
    {
        var rows = await Rows("SELECT id,version,state,payload::text,payload_version FROM core.accounts WHERE " + ScopeWhere + " AND service_id=@id",
            [("id",serviceId)], r => (r.GetString(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetInt32(4)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (id,version,state,payload,payloadVersion) = rows[0];
        var entity = SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(payload));
        ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        if (entity.ServiceId != serviceId) throw new InvalidDataException("Account reference mismatch.");
        return new(version,entity);
    }
    public void ValidateStored(Aggregate entity,string id,string storedState,string actualState,int payloadVersion)
    {
        if (payloadVersion != 1 || entity.Scope != scope || entity.Id != id || storedState != actualState)
            throw new InvalidDataException("Persisted identity, state or payload version mismatch.");
    }
    // Cuenta del nucleo. Al abrirla, el modulo que la origina indica la mesa (organizacion del nucleo) o '' si no tiene:
    // con ella el puesto principal lista las cuentas abiertas sin leer ninguna tabla de modulo (E1b).
    public async Task Save(SettlementAccount entity,long? expectedVersion,string tableId="")
    {
        if (expectedVersion is null)
        {
            RequireScope(entity);
            var count = await Sql("INSERT INTO core.accounts (tenant,company,location,id,service_id,table_id,state,version,payload) VALUES (@tenant,@company,@location,@id,@service,@table,@state,1,@payload::jsonb)",
                [("id",entity.Id),("service",entity.ServiceId),("table",tableId),("state",entity.State.ToString()),("payload",Wire.Encode(entity.Snapshot()))]);
            if (count != 1) throw new StoreConflict("version_conflict","Another command changed this record; reload before deciding.");
            await Events(entity.PendingEvents);
        }
        else await SaveAggregate("core.accounts",entity,entity.State.ToString(),Wire.Encode(entity.Snapshot()),expectedVersion,"service_id",entity.ServiceId);
    }
    public void RequireScope(Aggregate entity)
    {
        if (entity.Scope != scope) throw new RuleViolation("scope_mismatch","Wrong persistence scope.");
    }
    // Guardado generico de un agregado en una tabla del nucleo o de un modulo (ADR-012), siempre CUALIFICADA con su
    // esquema (core.accounts, dining.services): misma guarda de ambito y version. Solo constantes del codigo.
    public async Task SaveAggregate(string table,Aggregate entity,string state,string payload,long? expectedVersion,string referenceColumn,string reference)
    {
        if (!QualifiedTable().IsMatch(table)) throw new ArgumentException("Table must be schema-qualified: " + table);
        RequireScope(entity);
        int count;
        if (expectedVersion is null)
            count = await Sql($"INSERT INTO {table} (tenant,company,location,id,{referenceColumn},state,version,payload) VALUES (@tenant,@company,@location,@id,@reference,@state,1,@payload::jsonb)",
                [("id",entity.Id),("reference",reference),("state",state),("payload",payload)]);
        else
            count = await Sql($"UPDATE {table} SET state=@state,payload=@payload::jsonb,version=version+1 WHERE {ScopeWhere} AND id=@id AND version=@version",
                [("id",entity.Id),("state",state),("payload",payload),("version",expectedVersion.Value)]);
        if (count != 1) throw new StoreConflict("version_conflict","Another command changed this record; reload before deciding.");
        await Events(entity.PendingEvents);
    }
    public async Task Events(IEnumerable<DomainEvent> events)
    {
        foreach (var e in events)
        {
            if (e.Scope != scope || e.ActorId != identity.ActorId) throw new InvalidDataException("Event identity mismatch.");
            var payload = Wire.Encode(e);
            await Sql("INSERT INTO core.outbox (id,tenant,company,location,aggregate_id,type,occurred_at,payload) VALUES (@id,@tenant,@company,@location,@aggregate,@type,@at,@payload::jsonb)",
                [("id",e.Id),("aggregate",e.AggregateId),("type",e.Type),("at",e.At),("payload",payload)]);
            await Sql("INSERT INTO core.audit (id,event_id,tenant,company,location,actor,aggregate_id,action,command_key,occurred_at,payload) VALUES (@id,@event,@tenant,@company,@location,@actor,@aggregate,@action,@key,@at,@payload::jsonb)",
                [("id",Guid.NewGuid()),("event",e.Id),("actor",identity.ActorId),("aggregate",e.AggregateId),("action",e.Type),("key",key),("at",e.At),("payload",payload)]);
        }
    }
    // Configuracion de un MODULO (<modulo>.configuration, JSONB por kind). El nucleo ya no tiene configuracion JSONB (E2/E3).
    public Task<T> ModuleConfiguration<T>(string module, string kind, string id) => ReadConfiguration<T>(PostgresStore.CheckedSchema(module), kind, id);
    private async Task<T> ReadConfiguration<T>(string schema, string kind, string id)
    {
        var rows = await Rows($"SELECT payload::text FROM {schema}.configuration WHERE " + ScopeWhere + " AND kind=@kind AND id=@id",
            [("kind",kind),("id",id)], r => r.GetString(0));
        if (rows.Count == 0) throw new StoreNotFound();
        return Wire.Decode<T>(rows[0]);
    }
    // Conciliacion: solo el MISMO actor y ambito ven el resultado guardado de su clave de idempotencia.
    public async Task<string?> CommandResponse(string commandKey)
    {
        if (string.IsNullOrWhiteSpace(commandKey) || commandKey.Length > 128 || commandKey.Any(char.IsControl))
            throw new ArgumentException("Invalid command key.");
        var rows = await Rows("SELECT response FROM core.commands WHERE " + ScopeWhere + " AND actor=@actor AND key=@key",
            [("actor", identity.ActorId), ("key", commandKey)], r => r.GetString(0));
        return rows.Count == 0 ? null : rows[0];
    }
    public Task<int> SeedModuleConfiguration<T>(string module,string kind,string id,T value) => Seed(PostgresStore.CheckedSchema(module),kind,id,value);
    private Task<int> Seed<T>(string schema,string kind,string id,T value) => Sql(
        $"INSERT INTO {schema}.configuration (tenant,company,location,kind,id,payload) VALUES (@tenant,@company,@location,@kind,@id,@payload::jsonb) ON CONFLICT DO NOTHING",
        [("kind",kind),("id",id),("payload",Wire.Encode(value))]);
}
