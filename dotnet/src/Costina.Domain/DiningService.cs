namespace Costina.Domain;

// No reference to SettlementAccount: payments cannot change pacing by construction.
public sealed partial class DiningService : Aggregate
{
    private readonly List<CourseExecution> courses;
    public string TableId { get; }
    public int Pax { get; }
    public DiningState State { get; private set; } = DiningState.Open;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public DiningService(string id, BusinessScope scope, string tableId, int pax,
        IReadOnlyList<CourseDefinition> definitions) : base(id, scope)
    {
        TableId = Guard.Text(tableId, nameof(tableId));
        Guard.Rule(pax > 0, "invalid_pax", "Pax must be positive.");
        Pax = pax;
        ArgumentNullException.ThrowIfNull(definitions);
        courses = definitions.Select(d => new CourseExecution(d, pax)).ToList();
        Guard.Rule(courses.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() == courses.Count,
            "duplicate_course", "Course IDs must be unique.");
    }

    public void Start(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State == DiningState.Open, "service_not_open", "Only an open service can start.");
        Guard.Rule(courses.Count > 0, "menu_required", "A service needs at least one course.");
        State = DiningState.InService;
        StartedAt = stamp.At;
        Emit("service.started", stamp);
    }

    public string FireNext(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State == DiningState.InService, "service_not_running", "Service must be running to fire a course.");
        Guard.Rule(!restrictionsPendingAck, "restrictions_unacknowledged", "Kitchen must acknowledge the restriction change before firing more work.");
        Guard.Rule(!courses.Any(c => c.Active), "active_course", "Another course is still active.");
        var course = courses.FirstOrDefault(c => c.State == CourseState.Pending)
            ?? throw new RuleViolation("no_pending_course", "There are no pending courses.");
        course.Fire(stamp);
        Emit("course.fired", stamp, ("course_id", course.Id));
        return course.Id;
    }

    public void StartPreparation(string courseId, string itemId, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Find(courseId).StartPreparation(itemId);
        Emit("course_item.started", stamp, ("course_id", courseId), ("item_id", itemId));
    }

    public void ReadyPreparation(string courseId, string itemId, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Find(courseId).ReadyPreparation(itemId);
        Emit("course_item.ready", stamp, ("course_id", courseId), ("item_id", itemId));
    }

    public void ValidateReady(string courseId, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Guard.Rule(!restrictionsPendingAck, "restrictions_unacknowledged", "Kitchen must acknowledge the restriction change before validating a course.");
        Find(courseId).ValidateReady(stamp);
        Emit("course.ready", stamp, ("course_id", courseId));
    }

    public void Serve(string courseId, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Guard.Rule(!restrictionsPendingAck, "restrictions_unacknowledged", "Kitchen must acknowledge the restriction change before serving.");
        Find(courseId).Serve(stamp);
        Emit("course.served", stamp, ("course_id", courseId));
    }

    public void Skip(string courseId, string reason, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.Open or DiningState.InService or DiningState.Paused,
            "service_finished", "A finished service cannot be changed.");
        Find(courseId).Skip(reason);
        Emit("course.skipped", stamp, ("course_id", courseId), ("reason", reason.Trim()));
    }

    public void Pause(string reason, CommandStamp stamp)
    {
        Check(stamp);
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(State == DiningState.InService, "service_not_running", "Only a running service can pause.");
        State = DiningState.Paused;
        Emit("service.paused", stamp, ("reason", reason));
    }

    public void Resume(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State == DiningState.Paused, "service_not_paused", "Only a paused service can resume.");
        State = DiningState.InService;
        Emit("service.resumed", stamp);
    }

    public void Complete(CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Guard.Rule(courses.All(c => c.Terminal), "unfinished_courses",
            "Serve or explicitly skip all courses before completing the dining service.");
        State = DiningState.Completed;
        CompletedAt = stamp.At;
        Emit("service.completed", stamp);
    }

    public void CancelUnstarted(string reason, CommandStamp stamp)
    {
        Check(stamp);
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(State == DiningState.Open && courses.All(c => !c.Active),
            "service_already_started", "Cancellation of an active kitchen service needs a separate recall workflow.");
        State = DiningState.Cancelled;
        Emit("service.cancelled", stamp, ("reason", reason));
    }

    public DiningView View() => new(Id, TableId, Pax, State,
        Array.AsReadOnly(courses.Select(c => c.View()).ToArray()),
        Restrictions: restrictions.AsReadOnly(), RestrictionsPendingAck: restrictionsPendingAck);
    private CourseExecution Find(string id) => courses.FirstOrDefault(c => c.Id == id)
        ?? throw new RuleViolation("course_not_found", "Course not found.");
    private void KitchenAllowed(CommandStamp stamp)
    {
        Check(stamp);
        // Pause blocks future firing, not acknowledgements of work already sent.
        Guard.Rule(State is DiningState.InService or DiningState.Paused,
            "service_not_active", "Kitchen operations require an active or paused service.");
    }
}
