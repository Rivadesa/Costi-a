using System.Data;
using Costina.Domain;
using Npgsql;

namespace Costina.Persistence;

// Current-state persistence, not event sourcing. SQL identifiers are constants, never user input.
public sealed class PostgresStore(NpgsqlDataSource dataSource)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(748215091501)", connection, transaction))
            await gate.ExecuteNonQueryAsync(ct);
        using var stream = typeof(PostgresStore).Assembly.GetManifestResourceStream("Costina.Persistence.schema.sql")
            ?? throw new InvalidOperationException("Schema resource missing.");
        using var reader = new StreamReader(stream);
        await using var command = new NpgsqlCommand(await reader.ReadToEndAsync(ct), connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        await using var command = dataSource.CreateCommand("SELECT version FROM native_d1.schema_version");
        if (!Equals(await command.ExecuteScalarAsync(ct), 1)) throw new InvalidDataException("Run the explicit D1 schema initialization first.");
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
        var replay = await unit.Rows("SELECT fingerprint, response FROM native_d1.commands WHERE " + Unit.ScopeWhere + " AND actor=@actor AND key=@key",
            [("actor", identity.ActorId), ("key", key)], r => (r.GetString(0), r.GetString(1)));
        if (replay.Count > 0)
        {
            if (replay[0].Item1 != fingerprint) throw new StoreConflict("idempotency_conflict", "The key was already used with a different command.");
            await transaction.CommitAsync(ct);
            return replay[0].Item2;
        }
        var response = Wire.Encode(await work(unit));
        await unit.Sql("INSERT INTO native_d1.commands (tenant,company,location,actor,key,fingerprint,response) VALUES (@tenant,@company,@location,@actor,@key,@fingerprint,@response)",
            [("actor", identity.ActorId), ("key", key), ("fingerprint", fingerprint), ("response", response)]);
        await transaction.CommitAsync(ct);
        return response;
    }
}

public sealed record BoardRow(long Version, DiningView Service, OccupancyView Occupancy, long OccupancyVersion);
public sealed record StoredDining(long Version, DiningService Entity);
public sealed record StoredAccount(long Version, SettlementAccount Entity);
public sealed record StoredOccupancy(long Version, TableOccupancy Entity);

public sealed class Unit(NpgsqlConnection connection, NpgsqlTransaction transaction,
    ExecutionIdentity identity, string key, CancellationToken ct)
{
    internal const string ScopeWhere = "tenant=@tenant AND company=@company AND location=@location";
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
    internal async Task<int> Sql(string sql, (string Name, object Value)[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteNonQueryAsync(ct);
    }
    internal async Task<List<T>> Rows<T>(string sql, (string Name, object Value)[] values, Func<NpgsqlDataReader,T> project)
    {
        await using var command = Command(sql, values);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<T>();
        while (await reader.ReadAsync(ct)) result.Add(project(reader));
        return result;
    }
    public async Task<StoredDining> Dining(string id)
    {
        var rows = await Rows("SELECT version,state,payload::text,payload_version FROM native_d1.services WHERE " + ScopeWhere + " AND id=@id",
            [("id",id)], r => (r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt32(3)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (version,state,payload,payloadVersion) = rows[0];
        var entity = DiningService.Restore(Wire.Decode<DiningSnapshot>(payload));
        ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        return new(version,entity);
    }
    public async Task<StoredAccount> Account(string serviceId)
    {
        var rows = await Rows("SELECT id,version,state,payload::text,payload_version FROM native_d1.accounts WHERE " + ScopeWhere + " AND service_id=@id",
            [("id",serviceId)], r => (r.GetString(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetInt32(4)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (id,version,state,payload,payloadVersion) = rows[0];
        var entity = SettlementAccount.Restore(Wire.Decode<AccountSnapshot>(payload));
        ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        if (entity.ServiceId != serviceId) throw new InvalidDataException("Account reference mismatch.");
        return new(version,entity);
    }
    public async Task<StoredOccupancy> Occupancy(string serviceId)
    {
        var rows = await Rows("SELECT id,version,state,payload::text,payload_version,table_id FROM native_d1.occupancies WHERE " + ScopeWhere + " AND service_id=@id",
            [("id",serviceId)], r => (r.GetString(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (id,version,state,payload,payloadVersion,tableId) = rows[0];
        var entity = TableOccupancy.Restore(Wire.Decode<OccupancySnapshot>(payload));
        ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        if (entity.ServiceId != serviceId || entity.TableId != tableId) throw new InvalidDataException("Occupancy reference mismatch.");
        return new(version,entity);
    }
    private void ValidateStored(Aggregate entity,string id,string storedState,string actualState,int payloadVersion)
    {
        if (payloadVersion != 1 || entity.Scope != scope || entity.Id != id || storedState != actualState)
            throw new InvalidDataException("Persisted identity, state or payload version mismatch.");
    }
    public async Task Save(DiningService entity,long? expectedVersion)
    {
        await SaveCore("services",entity,entity.State.ToString(),Wire.Encode(entity.Snapshot()),expectedVersion,
            "table_id",entity.TableId);
    }
    public async Task Save(SettlementAccount entity,long? expectedVersion)
    {
        await SaveCore("accounts",entity,entity.State.ToString(),Wire.Encode(entity.Snapshot()),expectedVersion,
            "service_id",entity.ServiceId);
    }
    public async Task Save(TableOccupancy entity,long? expectedVersion)
    {
        if (expectedVersion is null)
        {
            RequireScope(entity);
            await Sql("INSERT INTO native_d1.occupancies (tenant,company,location,id,service_id,table_id,state,version,payload) VALUES (@tenant,@company,@location,@id,@service,@table,@state,1,@payload::jsonb)",
                [("id",entity.Id),("service",entity.ServiceId),("table",entity.TableId),("state",entity.State.ToString()),("payload",Wire.Encode(entity.Snapshot()))]);
            await Events(entity.PendingEvents);
        }
        else await SaveCore("occupancies",entity,entity.State.ToString(),Wire.Encode(entity.Snapshot()),expectedVersion,"service_id",entity.ServiceId);
    }
    private void RequireScope(Aggregate entity)
    {
        if (entity.Scope != scope) throw new RuleViolation("scope_mismatch","Wrong persistence scope.");
    }
    private async Task SaveCore(string table,Aggregate entity,string state,string payload,long? expectedVersion,string referenceColumn,string reference)
    {
        RequireScope(entity);
        int count;
        if (expectedVersion is null)
            count = await Sql($"INSERT INTO native_d1.{table} (tenant,company,location,id,{referenceColumn},state,version,payload) VALUES (@tenant,@company,@location,@id,@reference,@state,1,@payload::jsonb)",
                [("id",entity.Id),("reference",reference),("state",state),("payload",payload)]);
        else
            count = await Sql($"UPDATE native_d1.{table} SET state=@state,payload=@payload::jsonb,version=version+1 WHERE {ScopeWhere} AND id=@id AND version=@version",
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
            await Sql("INSERT INTO native_d1.outbox (id,tenant,company,location,aggregate_id,type,occurred_at,payload) VALUES (@id,@tenant,@company,@location,@aggregate,@type,@at,@payload::jsonb)",
                [("id",e.Id),("aggregate",e.AggregateId),("type",e.Type),("at",e.At),("payload",payload)]);
            await Sql("INSERT INTO native_d1.audit (id,event_id,tenant,company,location,actor,aggregate_id,action,command_key,occurred_at,payload) VALUES (@id,@event,@tenant,@company,@location,@actor,@aggregate,@action,@key,@at,@payload::jsonb)",
                [("id",Guid.NewGuid()),("event",e.Id),("actor",identity.ActorId),("aggregate",e.AggregateId),("action",e.Type),("key",key),("at",e.At),("payload",payload)]);
        }
    }
    public async Task<T> Configuration<T>(string kind, string id)
    {
        var rows = await Rows("SELECT payload::text FROM native_d1.configuration WHERE " + ScopeWhere + " AND kind=@kind AND id=@id",
            [("kind",kind),("id",id)], r => r.GetString(0));
        if (rows.Count == 0) throw new StoreNotFound();
        return Wire.Decode<T>(rows[0]);
    }
    public Task<int> SeedConfiguration<T>(string kind,string id,T value) => Sql(
        "INSERT INTO native_d1.configuration (tenant,company,location,kind,id,payload) VALUES (@tenant,@company,@location,@kind,@id,@payload::jsonb) ON CONFLICT DO NOTHING",
        [("kind",kind),("id",id),("payload",Wire.Encode(value))]);
    public async Task<IReadOnlyList<BoardRow>> Board()
    {
        var ids = await Rows("SELECT service_id FROM native_d1.occupancies WHERE " + ScopeWhere + " AND state='Occupied' ORDER BY table_id LIMIT 200",[],r => r.GetString(0));
        var result = new List<BoardRow>();
        foreach (var id in ids)
        {
            var d = await Dining(id); var o = await Occupancy(id);
            // Affordances calculadas al leer; el servidor las filtra por rol antes de responder.
            result.Add(new(d.Version, d.Entity.View(true), o.Entity.View(d.Entity), o.Version));
        }
        return result;
    }
}
