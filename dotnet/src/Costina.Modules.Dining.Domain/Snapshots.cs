using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

// Trusted persistence boundary. These records are not accepted as HTTP commands.
// Restrictions/RestrictionsPendingAck son aditivos opcionales: un payload D1 sin ellos
// restaura con lista vacia y sin acuse pendiente; payload_version sigue siendo 1.
public sealed record DiningSnapshot(string Id, BusinessScope Scope, string TableId, int Pax,
    DiningState State, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<CourseView> Courses,
    IReadOnlyList<GuestRestriction>? Restrictions = null, bool RestrictionsPendingAck = false, string? OfferId = null);
public sealed record OccupancySnapshot(string Id, BusinessScope Scope, string TableId,
    string ServiceId, OccupancyState State, DateTimeOffset? ReleasedAt);

public sealed partial class DiningService
{
    public DiningSnapshot Snapshot() => new(Id, Scope, TableId, Pax, State, StartedAt,
        CompletedAt, Array.AsReadOnly(courses.Select(c => c.View()).ToArray()),
        restrictions.AsReadOnly(), RestrictionsPendingAck, OfferId);

    public static DiningService Restore(DiningSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Guard.Rule(Enum.IsDefined(value.State), "invalid_snapshot", "Unknown dining state.");
        var result = new DiningService(value.Id, value.Scope, value.TableId, value.Pax,
            value.Courses.Select(c => new CourseDefinition(c.Id, c.Name,
                c.Preparations.Select(p => new PreparationDefinition(p.Id, p.Name, p.StationId,
                    p.Quantity, p.GuestPosition, p.Mandatory)).ToArray(), c.ChoiceRequired, c.Optional)).ToArray(), value.OfferId);
        result.courses.Clear();
        result.courses.AddRange(value.Courses.Select(c => CourseExecution.Restore(c, value.Pax)));
        Guard.Rule(result.courses.Count(c => c.Active) <= 1, "invalid_snapshot", "Multiple active courses.");
        Guard.Rule(value.State != DiningState.Completed || result.courses.All(c => c.Terminal),
            "invalid_snapshot", "Completed service has unfinished courses.");
        Guard.Rule(value.State is not (DiningState.Open or DiningState.Cancelled) ||
            result.courses.All(c => c.State is CourseState.Pending or CourseState.Skipped),
            "invalid_snapshot", "Unstarted service has kitchen activity.");
        Guard.Rule((value.State is DiningState.InService or DiningState.Paused or DiningState.Completed)
            == value.StartedAt.HasValue, "invalid_snapshot", "Start timestamp does not match dining state.");
        Guard.Rule((value.State == DiningState.Completed) == value.CompletedAt.HasValue,
            "invalid_snapshot", "Completion timestamp does not match dining state.");
        Guard.Rule(value.CompletedAt is null || value.CompletedAt >= value.StartedAt,
            "invalid_snapshot", "Invalid dining timestamps.");
        foreach (var restriction in value.Restrictions ?? [])
        {
            Guard.Rule(!string.IsNullOrWhiteSpace(restriction.Id) && !string.IsNullOrWhiteSpace(restriction.Substance)
                && Enum.IsDefined(restriction.Kind) && Enum.IsDefined(restriction.Severity),
                "invalid_snapshot", "Malformed persisted restriction.");
            Guard.Rule(restriction.GuestPosition is null
                || (restriction.GuestPosition >= 1 && restriction.GuestPosition <= value.Pax),
                "invalid_snapshot", "Persisted restriction refers to a guest outside the service.");
            Guard.Rule(result.restrictions.All(r => r.Id != restriction.Id),
                "invalid_snapshot", "Duplicate persisted restriction identity.");
            result.restrictions.Add(restriction);
        }
        Guard.Rule(!value.RestrictionsPendingAck
            || value.State is DiningState.InService or DiningState.Paused,
            "invalid_snapshot", "Pending acknowledgement requires an active service.");
        // Compatibilidad D3.2: un payload con acuse global pendiente pero sin marcas por elaboracion
        // (anterior a D3.5) deja pendientes TODAS las elaboraciones del pase enviado: revisar de mas
        // es seguro; dar por revisado lo que nadie decidio, no.
        if (value.RestrictionsPendingAck && !result.courses.Any(c => c.ReviewPending))
            foreach (var course in result.courses) course.FlagForReview(_ => true);
        result.State = value.State; result.StartedAt = value.StartedAt; result.CompletedAt = value.CompletedAt;
        return result;
    }
}

internal sealed partial class CourseExecution
{
    internal static CourseExecution Restore(CourseView value, int pax)
    {
        Guard.Rule(Enum.IsDefined(value.State), "invalid_snapshot", "Unknown course state.");
        var result = new CourseExecution(new CourseDefinition(value.Id, value.Name,
            value.Preparations.Select(p => new PreparationDefinition(p.Id, p.Name, p.StationId,
                p.Quantity, p.GuestPosition, p.Mandatory)).ToArray(), value.ChoiceRequired, value.Optional), pax);
        var fired = value.State is CourseState.Fired or CourseState.Preparing or CourseState.Ready or CourseState.Served;
        foreach (var item in value.Preparations)
        {
            Guard.Rule(Enum.IsDefined(item.State), "invalid_snapshot", "Unknown preparation state.");
            Guard.Rule(!item.ReviewPending || (fired && value.State != CourseState.Served),
                "invalid_snapshot", "Review pending on a preparation that is not in the kitchen.");
            Guard.Rule(item.Review is null || (Enum.IsDefined(item.Review.Decision) && !string.IsNullOrWhiteSpace(item.Review.Note)),
                "invalid_snapshot", "Malformed persisted preparation review.");
            var preparation = result.Find(item.Id);
            preparation.State = item.State; preparation.ReviewPending = item.ReviewPending; preparation.Review = item.Review;
        }
        var ready = value.State is CourseState.Ready or CourseState.Served;
        Guard.Rule(fired == value.FiredAt.HasValue && ready == value.ReadyAt.HasValue &&
            (value.State == CourseState.Served) == value.ServedAt.HasValue,
            "invalid_snapshot", "Course timestamps do not match state.");
        Guard.Rule(value.State != CourseState.Skipped || !string.IsNullOrWhiteSpace(value.SkipReason),
            "invalid_snapshot", "Skipped course needs a reason.");
        Guard.Rule(value.State == CourseState.Skipped || value.SkipReason is null,
            "invalid_snapshot", "Unexpected skip reason.");
        Guard.Rule(!ready || value.Preparations.All(p => !p.Mandatory || p.State == PreparationState.Ready),
            "invalid_snapshot", "Mandatory preparation is not ready.");
        Guard.Rule(fired || value.Preparations.All(p => p.State == PreparationState.Pending),
            "invalid_snapshot", "Unsent course has preparation activity.");
        Guard.Rule(!fired || value.Preparations.All(p => p.State != PreparationState.Pending),
            "invalid_snapshot", "Sent course has unsent preparations.");
        Guard.Rule(value.State != CourseState.Fired || value.Preparations.All(p => p.State == PreparationState.Fired),
            "invalid_snapshot", "Fired course has unexpected preparation state.");
        Guard.Rule(value.ReadyAt is null || value.ReadyAt >= value.FiredAt,
            "invalid_snapshot", "Invalid readiness timestamp.");
        Guard.Rule(value.ServedAt is null || value.ServedAt >= value.ReadyAt,
            "invalid_snapshot", "Invalid served timestamp.");
        result.State = value.State; result.FiredAt = value.FiredAt; result.ReadyAt = value.ReadyAt;
        result.ServedAt = value.ServedAt; result.SkipReason = value.SkipReason;
        return result;
    }
}

public sealed partial class TableOccupancy
{
    public OccupancySnapshot Snapshot() => new(Id, Scope, TableId, ServiceId, State, ReleasedAt);
    public static TableOccupancy Restore(OccupancySnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Guard.Rule(Enum.IsDefined(value.State) &&
            (value.State == OccupancyState.Released) == value.ReleasedAt.HasValue,
            "invalid_snapshot", "Invalid occupancy state/timestamp.");
        return new TableOccupancy(value.Id, value.Scope, value.TableId, value.ServiceId)
            { State = value.State, ReleasedAt = value.ReleasedAt };
    }
}
