using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E2: acceso a la ORGANIZACION (core.zones, core.tables, core.stations) sobre la Unit del nucleo: misma transaccion,
// mismas guardas de ambito. Los modulos leen mesas y estaciones por aqui (nunca por SQL propio).
public static class OrganizationUnit
{
    public static string KindText(StationKind kind) => kind.ToString().ToLowerInvariant();
    public static StationKind KindOf(string text) => Enum.Parse<StationKind>(text, ignoreCase: true);

    private static ZoneDefinition ZoneRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetBoolean(3));
    private static TableDefinition TableRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetInt32(4), r.GetBoolean(5));
    private static StationDefinition StationRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), KindOf(r.GetString(2)), r.GetInt32(3), r.GetBoolean(4));
    public const string ZoneColumns = "id,name,sort,active", TableColumns = "id,name,capacity,zone_id,sort,active", StationColumns = "id,name,kind,sort,active";

    public static Task<List<ZoneDefinition>> Zones(this Unit unit)
        => unit.Rows($"SELECT {ZoneColumns} FROM core.zones WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], ZoneRow);
    public static Task<List<TableDefinition>> Tables(this Unit unit)
        => unit.Rows($"SELECT {TableColumns} FROM core.tables WHERE {Unit.ScopeWhere} ORDER BY zone_id,sort,id", [], TableRow);
    public static Task<List<StationDefinition>> Stations(this Unit unit)
        => unit.Rows($"SELECT {StationColumns} FROM core.stations WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], StationRow);

    public static async Task<ZoneDefinition> Zone(this Unit unit, string id)
        => (await unit.Rows($"SELECT {ZoneColumns} FROM core.zones WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], ZoneRow)).FirstOrDefault() ?? throw new StoreNotFound();
    public static async Task<TableDefinition> Table(this Unit unit, string id)
        => (await unit.Rows($"SELECT {TableColumns} FROM core.tables WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], TableRow)).FirstOrDefault() ?? throw new StoreNotFound();
    public static async Task<StationDefinition> Station(this Unit unit, string id)
        => (await unit.Rows($"SELECT {StationColumns} FROM core.stations WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], StationRow)).FirstOrDefault() ?? throw new StoreNotFound();

    // Mesa sobre la que se puede ABRIR algo: existe y esta activa (una mesa desactivada conserva su historia, nada mas).
    public static async Task<TableDefinition> ActiveTable(this Unit unit, string id)
    {
        var table = await unit.Table(id);
        if (!table.Active) throw new RuleViolation("table_inactive", "This table is deactivated.");
        return table;
    }

    private static async Task Exactly(Task<int> work, string missing)
    {
        if (await work != 1) throw new StoreNotFound();
        _ = missing;
    }
    private static async Task Insert(Unit unit, string sql, (string, object)[] values, string code)
    {
        try { await unit.Sql(sql, values); }
        catch (PostgresException e) when (e.SqlState == "23505") { throw new StoreConflict("duplicate_code", $"Code {code} already exists."); }
    }

    public static Task InsertZone(this Unit unit, ZoneDefinition z) => Insert(unit,
        "INSERT INTO core.zones (tenant,company,location,id,name,sort,active) VALUES (@tenant,@company,@location,@id,@name,@sort,@active)",
        [("id", z.Id), ("name", z.Name), ("sort", z.Sort), ("active", z.Active)], z.Id);
    public static Task UpdateZone(this Unit unit, ZoneDefinition z) => Exactly(unit.Sql(
        $"UPDATE core.zones SET name=@name,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id",
        [("id", z.Id), ("name", z.Name), ("sort", z.Sort), ("active", z.Active)]), z.Id);
    public static Task InsertTable(this Unit unit, TableDefinition t) => Insert(unit,
        "INSERT INTO core.tables (tenant,company,location,id,name,capacity,zone_id,sort,active) VALUES (@tenant,@company,@location,@id,@name,@capacity,@zone,@sort,@active)",
        [("id", t.Id), ("name", t.Name), ("capacity", t.Capacity), ("zone", t.ZoneId), ("sort", t.Sort), ("active", t.Active)], t.Id);
    public static Task UpdateTable(this Unit unit, TableDefinition t) => Exactly(unit.Sql(
        $"UPDATE core.tables SET name=@name,capacity=@capacity,zone_id=@zone,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id",
        [("id", t.Id), ("name", t.Name), ("capacity", t.Capacity), ("zone", t.ZoneId), ("sort", t.Sort), ("active", t.Active)]), t.Id);
    public static Task InsertStation(this Unit unit, StationDefinition s) => Insert(unit,
        "INSERT INTO core.stations (tenant,company,location,id,name,kind,sort,active) VALUES (@tenant,@company,@location,@id,@name,@kind,@sort,@active)",
        [("id", s.Id), ("name", s.Name), ("kind", KindText(s.Kind)), ("sort", s.Sort), ("active", s.Active)], s.Id);
    public static Task UpdateStation(this Unit unit, StationDefinition s) => Exactly(unit.Sql(
        $"UPDATE core.stations SET name=@name,kind=@kind,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id",
        [("id", s.Id), ("name", s.Name), ("kind", KindText(s.Kind)), ("sort", s.Sort), ("active", s.Active)]), s.Id);
    // Siembra idempotente (fixtures, estacion de pase): no pisa lo que ya exista.
    public static Task<int> SeedZone(this Unit unit, ZoneDefinition z) => unit.Sql(
        "INSERT INTO core.zones (tenant,company,location,id,name,sort,active) VALUES (@tenant,@company,@location,@id,@name,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", z.Id), ("name", z.Name), ("sort", z.Sort), ("active", z.Active)]);
    public static Task<int> SeedTable(this Unit unit, TableDefinition t) => unit.Sql(
        "INSERT INTO core.tables (tenant,company,location,id,name,capacity,zone_id,sort,active) VALUES (@tenant,@company,@location,@id,@name,@capacity,@zone,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", t.Id), ("name", t.Name), ("capacity", t.Capacity), ("zone", t.ZoneId), ("sort", t.Sort), ("active", t.Active)]);
    public static Task<int> SeedStation(this Unit unit, StationDefinition s) => unit.Sql(
        "INSERT INTO core.stations (tenant,company,location,id,name,kind,sort,active) VALUES (@tenant,@company,@location,@id,@name,@kind,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", s.Id), ("name", s.Name), ("kind", KindText(s.Kind)), ("sort", s.Sort), ("active", s.Active)]);
}

// Lecturas de organizacion para el puesto principal y para la configuracion operativa de los clientes.
public sealed record ZoneView(string Id, string Name, int Sort, bool Active, TableDefinition[] Tables);
public sealed record OrganizationView(ZoneView[] Zones, StationDefinition[] Stations);
// Mesa tal como la ven los clientes en GET /configuration (activas; campos de E1 mas la sala).
public sealed record TableChoiceView(string Id, string Name, int Capacity, string ZoneId, string ZoneName);

public static class OrganizationReads
{
    public static async Task<OrganizationView> Organization(this DesktopReadRepository reads, BusinessScope scope, CancellationToken ct)
    {
        var zones = await reads.Query(scope, $"SELECT {OrganizationUnit.ZoneColumns} FROM core.zones WHERE tenant=@tenant AND company=@company AND location=@location ORDER BY sort,id",
            r => new ZoneDefinition(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetBoolean(3)), ct);
        var tables = await reads.Query(scope, $"SELECT {OrganizationUnit.TableColumns} FROM core.tables WHERE tenant=@tenant AND company=@company AND location=@location ORDER BY sort,id",
            r => new TableDefinition(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetInt32(4), r.GetBoolean(5)), ct);
        var stations = await reads.Query(scope, $"SELECT {OrganizationUnit.StationColumns} FROM core.stations WHERE tenant=@tenant AND company=@company AND location=@location ORDER BY sort,id",
            r => new StationDefinition(r.GetString(0), r.GetString(1), OrganizationUnit.KindOf(r.GetString(2)), r.GetInt32(3), r.GetBoolean(4)), ct);
        return new(zones.Select(z => new ZoneView(z.Id, z.Name, z.Sort, z.Active, tables.Where(t => t.ZoneId == z.Id).ToArray())).ToArray(), stations.ToArray());
    }

    public static Task<List<TableChoiceView>> ActiveTables(this DesktopReadRepository reads, BusinessScope scope, CancellationToken ct)
        => reads.Query(scope, "SELECT t.id,t.name,t.capacity,z.id,z.name FROM core.tables t JOIN core.zones z ON z.tenant=t.tenant AND z.company=t.company AND z.location=t.location AND z.id=t.zone_id " +
            "WHERE t.tenant=@tenant AND t.company=@company AND t.location=@location AND t.active AND z.active ORDER BY z.sort,z.id,t.sort,t.id LIMIT 500",
            r => new TableChoiceView(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4)), ct);

    public static async Task<bool> StationActive(this DesktopReadRepository reads, BusinessScope scope, string id, CancellationToken ct)
        => (await reads.Query(scope, "SELECT 1 FROM core.stations WHERE tenant=@tenant AND company=@company AND location=@location AND id=@id AND active",
            r => 1, ct, ("id", id))).Count == 1;
}
