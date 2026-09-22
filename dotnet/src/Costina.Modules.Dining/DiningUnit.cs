using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;

namespace Costina.Modules.Dining;

public sealed record BoardRow(long Version, DiningView Service, OccupancyView Occupancy, long OccupancyVersion);
public sealed record StoredDining(long Version, DiningService Entity);
public sealed record StoredOccupancy(long Version, TableOccupancy Entity);

// Acceso a datos del modulo sobre la Unit del nucleo (misma transaccion, mismas guardas de ambito y version).
// Solo toca las tablas del modulo: services y occupancies. La cuenta (accounts) es del nucleo y se pide a la Unit.
public static class DiningUnit
{
    public static async Task<StoredDining> Dining(this Unit unit, string id)
    {
        var rows = await unit.Rows("SELECT version,state,payload::text,payload_version FROM native_d1.services WHERE " + Unit.ScopeWhere + " AND id=@id",
            [("id", id)], r => (r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (version, state, payload, payloadVersion) = rows[0];
        var entity = DiningService.Restore(Wire.Decode<DiningSnapshot>(payload));
        unit.ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        return new(version, entity);
    }

    public static async Task<StoredOccupancy> Occupancy(this Unit unit, string serviceId)
    {
        var rows = await unit.Rows("SELECT id,version,state,payload::text,payload_version,table_id FROM native_d1.occupancies WHERE " + Unit.ScopeWhere + " AND service_id=@id",
            [("id", serviceId)], r => (r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetString(5)));
        if (rows.Count == 0) throw new StoreNotFound();
        var (id, version, state, payload, payloadVersion, tableId) = rows[0];
        var entity = TableOccupancy.Restore(Wire.Decode<OccupancySnapshot>(payload));
        unit.ValidateStored(entity, id, state, entity.State.ToString(), payloadVersion);
        if (entity.ServiceId != serviceId || entity.TableId != tableId) throw new InvalidDataException("Occupancy reference mismatch.");
        return new(version, entity);
    }

    public static Task Save(this Unit unit, DiningService entity, long? expectedVersion)
        => unit.SaveAggregate("services", entity, entity.State.ToString(), Wire.Encode(entity.Snapshot()), expectedVersion, "table_id", entity.TableId);

    public static async Task Save(this Unit unit, TableOccupancy entity, long? expectedVersion)
    {
        if (expectedVersion is null)
        {
            unit.RequireScope(entity);
            await unit.Sql("INSERT INTO native_d1.occupancies (tenant,company,location,id,service_id,table_id,state,version,payload) VALUES (@tenant,@company,@location,@id,@service,@table,@state,1,@payload::jsonb)",
                [("id", entity.Id), ("service", entity.ServiceId), ("table", entity.TableId), ("state", entity.State.ToString()), ("payload", Wire.Encode(entity.Snapshot()))]);
            await unit.Events(entity.PendingEvents);
        }
        else await unit.SaveAggregate("occupancies", entity, entity.State.ToString(), Wire.Encode(entity.Snapshot()), expectedVersion, "service_id", entity.ServiceId);
    }

    public static async Task<IReadOnlyList<BoardRow>> Board(this Unit unit)
    {
        var ids = await unit.Rows("SELECT service_id FROM native_d1.occupancies WHERE " + Unit.ScopeWhere + " AND state='Occupied' ORDER BY table_id LIMIT 200", [], r => r.GetString(0));
        var result = new List<BoardRow>();
        foreach (var id in ids)
        {
            var d = await unit.Dining(id); var o = await unit.Occupancy(id);
            // Affordances calculadas al leer; el servidor las filtra por rol antes de responder.
            result.Add(new(d.Version, d.Entity.View(true), o.Entity.View(d.Entity), o.Version));
        }
        return result;
    }
}
