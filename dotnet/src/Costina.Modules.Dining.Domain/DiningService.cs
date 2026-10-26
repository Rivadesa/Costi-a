using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

// No reference to SettlementAccount: payments cannot change pacing by construction.
public sealed partial class DiningService : Aggregate
{
    private readonly List<CourseExecution> courses;
    public string TableId { get; }
    public int Pax { get; }
    // E4a: oferta del nucleo con la que se abrio (null en servicios anteriores a E4).
    public string? OfferId { get; }
    public DiningState State { get; private set; } = DiningState.Open;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public DiningService(string id, BusinessScope scope, string tableId, int pax,
        IReadOnlyList<CourseDefinition> definitions, string? offerId = null) : base(id, scope)
    {
        TableId = Guard.Text(tableId, nameof(tableId));
        OfferId = string.IsNullOrWhiteSpace(offerId) ? null : offerId;
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

    // E4e (ADR-016): el bloqueo por revisiones es del pase que VA A SALIR, no global: un pase revisado sale aunque otro posterior
    // siga esperando. Y desde «Sentados» (Open) la misma orden empieza el servicio y envia el primer pase, todo o nada: si el pase
    // no puede salir (revision, eleccion o grupo vacio), no cambia nada y la mesa sigue sentada. 'start' sigue existiendo.
    public string FireNext(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.InService or DiningState.Open, "service_not_running", "Service must be seated or running to fire a course.");
        var active = courses.FirstOrDefault(c => c.Active);
        // Con un pase en cocina que espera revision se dice eso, como en D3.5; sin revisiones, que hay otro pase en marcha.
        Guard.Rule(active is null || !active.ReviewPending, "restrictions_unreviewed", "Kitchen must review every affected preparation of the course in the kitchen first.");
        Guard.Rule(active is null, "active_course", "Another course is still active.");
        var course = NextCourse() ?? throw new RuleViolation("no_pending_course", "There are no pending courses.");
        Guard.Rule(!course.ReviewPending, "restrictions_unreviewed", "Kitchen must review every affected preparation of this course before it is sent.");
        var starting = State == DiningState.Open;
        course.Fire(stamp);   // choice_missing si un comensal no ha elegido (menu cerrado), course_empty en un grupo vacio: antes de cambiar nada
        if (starting) { State = DiningState.InService; StartedAt = stamp.At; Emit("service.started", stamp); }
        Emit("course.fired", stamp, ("course_id", course.Id));
        return course.Id;
    }

    // El pase que saldria con fire-next: el primero pendiente.
    private CourseExecution? NextCourse() => courses.FirstOrDefault(c => c.State == CourseState.Pending);

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
        Find(courseId).ValidateReady(stamp);
        Emit("course.ready", stamp, ("course_id", courseId));
    }

    public void Serve(string courseId, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        Find(courseId).Serve(stamp);
        Emit("course.served", stamp, ("course_id", courseId));
    }

    // E4a: eleccion de plato de un comensal en un pase de menu cerrado, solo mientras el pase no se ha disparado.
    public void Choose(string courseId, int guest, PreparationDefinition dish, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.Open or DiningState.InService or DiningState.Paused,
            "service_finished", "A finished service cannot be changed.");
        Find(courseId).Choose(guest, dish, NeedsReview(guest));   // E4e: para un comensal con restriccion nace por revisar
        Emit("course.chosen", stamp, ("course_id", courseId), ("guest", guest.ToString()), ("dish_id", dish.Id));
    }

    // E4b: plato pedido a la carta en un grupo pendiente (la cuenta se apunta fuera, por el contrato del nucleo).
    public void AddDish(string courseId, PreparationDefinition dish, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.Open or DiningState.InService or DiningState.Paused,
            "service_finished", "A finished service cannot be changed.");
        // E4e: un plato para un comensal con restriccion, o sin comensal (para compartir: le aplica cualquiera), nace por revisar.
        Find(courseId).AddDish(dish, NeedsReview(dish.GuestPosition));
        Emit("course.dish_added", stamp, ("course_id", courseId), ("item_id", dish.Id), ("quantity", dish.Quantity.ToString()));
    }

    public void Skip(string courseId, string reason, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.Open or DiningState.InService or DiningState.Paused,
            "service_finished", "A finished service cannot be changed.");
        // E4e: omitir retira las revisiones pendientes de ese pase; el evento las cuenta solo si habia alguna (lo de siempre no cambia).
        var withdrawn = Find(courseId).Skip(reason);
        Emit("course.skipped", stamp, WithWithdrawn([("course_id", courseId), ("reason", reason.Trim())], withdrawn));
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
        // E4e: nada se va a cocinar: las revisiones pendientes se retiran (si no, la mesa cancelada quedaria «por revisar» para siempre).
        var withdrawn = courses.Sum(c => c.WithdrawReviews());
        State = DiningState.Cancelled;
        Emit("service.cancelled", stamp, WithWithdrawn([("reason", reason)], withdrawn));
    }

    private static (string Key, string Value)[] WithWithdrawn((string Key, string Value)[] data, int withdrawn)
        => withdrawn > 0 ? [.. data, ("reviews_withdrawn", withdrawn.ToString(System.Globalization.CultureInfo.InvariantCulture))] : data;

    public DiningView View() => new(Id, TableId, Pax, State,
        Array.AsReadOnly(courses.Select(c => c.View()).ToArray()),
        Restrictions: restrictions.AsReadOnly(), RestrictionsPendingAck: RestrictionsPendingAck, OfferId: OfferId);
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
