using Costina.Core.Domain;
using Costina.Modules.Dining.Domain;

namespace Costina.Migration;

// An inspection contract, not an importer. It cannot modify a database or an aggregate.
public sealed record LegacyLifecycleInput(string ServiceId, string Status, bool HasStartedAt,
    bool HasClosedAt, IReadOnlyList<string> CourseStatuses, long TotalCents, long PaidCents);
public sealed record LifecycleMigrationPlan(string ServiceId, string OriginalStatus,
    DiningState? SuggestedDiningState, OccupancyState? SuggestedOccupancyState,
    AccountState? SuggestedAccountState, PaymentCoverage Coverage,
    bool RequiresReview, IReadOnlyList<string> Reasons);

public static class LegacyLifecyclePlanner
{
    private static readonly HashSet<string> KnownCourses =
        ["pending", "fired", "preparing", "ready", "served", "skipped", "cancelled"];

    public static LifecycleMigrationPlan Plan(LegacyLifecycleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.CourseStatuses);
        if (string.IsNullOrWhiteSpace(input.ServiceId)) throw new ArgumentException("Service ID is required.");
        if (input.TotalCents < 0 || input.PaidCents < 0) throw new ArgumentException("Negative legacy totals need separate reconciliation.");
        var reasons = new List<string>();
        var coverage = input.PaidCents > input.TotalCents ? PaymentCoverage.Credit
            : input.PaidCents == input.TotalCents ? PaymentCoverage.Paid
            : input.PaidCents == 0 ? PaymentCoverage.Unpaid : PaymentCoverage.PartiallyPaid;
        DiningState? state = input.Status switch
        {
            "open" => DiningState.Open,
            "in_service" => DiningState.InService,
            "paused" => DiningState.Paused,
            "cancelled" => DiningState.Cancelled,
            "closed" when input.HasClosedAt && input.CourseStatuses.Count > 0
                && input.CourseStatuses.All(c => c is "served" or "skipped") => DiningState.Completed,
            _ => null
        };
        if (input.Status is "paid" or "pending_payment")
            reasons.Add("financial_status_overwrote_dining_state");
        else if (state is null)
            reasons.Add("ambiguous_or_unknown_dining_state");
        if (input.Status == "open" && (input.HasStartedAt || input.CourseStatuses.Any(c => c != "pending")))
        {
            state = null;
            reasons.Add("open_status_has_progress_evidence");
        }
        if (input.CourseStatuses.Any(c => !KnownCourses.Contains(c)))
        {
            state = null;
            reasons.Add("unknown_course_status");
        }
        if (input.Status == "cancelled" && input.CourseStatuses.Any(c => c is "fired" or "preparing" or "ready"))
        {
            state = null;
            reasons.Add("cancelled_service_has_live_kitchen_work");
        }
        // Legacy closed was a combined checkout operation, not evidence diners vacated a table.
        // Even a paid account is not proof of an explicit account-close decision.
        reasons.Add("occupancy_requires_explicit_cutover_confirmation");
        reasons.Add("account_closure_requires_explicit_cutover_confirmation");
        return new(input.ServiceId, input.Status, state, null, null, coverage, true, reasons.AsReadOnly());
    }
}
