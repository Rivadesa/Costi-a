namespace Costina.Domain;

public sealed class TableOccupancy : Aggregate
{
    public string TableId { get; }
    public string ServiceId { get; }
    public OccupancyState State { get; private set; } = OccupancyState.Occupied;
    public DateTimeOffset? ReleasedAt { get; private set; }

    public TableOccupancy(string id, BusinessScope scope, string tableId, string serviceId) : base(id, scope)
    {
        TableId = Guard.Text(tableId, nameof(tableId));
        ServiceId = Guard.Text(serviceId, nameof(serviceId));
    }

    public void Release(DiningService service, string reason, CommandStamp stamp)
    {
        Check(stamp);
        ArgumentNullException.ThrowIfNull(service);
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(service.Scope == Scope && service.Id == ServiceId && service.TableId == TableId,
            "occupancy_mismatch", "The service does not own this table occupancy.");
        Guard.Rule(State == OccupancyState.Occupied, "table_already_released", "Table is already released.");
        Guard.Rule(service.State is DiningState.Completed or DiningState.Cancelled,
            "service_not_finished", "Complete or cancel the service before releasing its occupancy.");
        State = OccupancyState.Released;
        ReleasedAt = stamp.At;
        Emit("table.occupancy_released", stamp, ("service_id", ServiceId), ("table_id", TableId), ("reason", reason));
    }

    public OccupancyView View() => new(Id, TableId, ServiceId, State, ReleasedAt);
}
