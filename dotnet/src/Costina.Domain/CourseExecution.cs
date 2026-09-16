namespace Costina.Domain;

// Only DiningService can invoke mutations. Callers receive immutable snapshots.
internal sealed partial class CourseExecution
{
    private sealed class Preparation(PreparationDefinition definition)
    {
        public PreparationDefinition Definition { get; } = definition;
        public PreparationState State { get; set; } = PreparationState.Pending;
        public bool ReviewPending { get; set; }
        public PreparationReview? Review { get; set; }
        public PreparationView View() => new(Definition.Id, Definition.Name, Definition.StationId,
            Definition.Quantity, Definition.GuestPosition, Definition.Mandatory, State,
            ReviewPending: ReviewPending, Review: Review);
    }

    private readonly List<Preparation> preparations;
    public string Id { get; }
    public string Name { get; }
    public CourseState State { get; private set; } = CourseState.Pending;
    public DateTimeOffset? FiredAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ServedAt { get; private set; }
    public string? SkipReason { get; private set; }
    public bool Active => State is CourseState.Fired or CourseState.Preparing or CourseState.Ready;
    public bool Terminal => State is CourseState.Served or CourseState.Skipped;
    public bool ReviewPending => preparations.Any(p => p.ReviewPending);

    public CourseExecution(CourseDefinition definition, int pax)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Id = Guard.Text(definition.Id, nameof(definition.Id));
        Name = Guard.Text(definition.Name, nameof(definition.Name));
        ArgumentNullException.ThrowIfNull(definition.Preparations);
        preparations = [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in definition.Preparations)
        {
            ArgumentNullException.ThrowIfNull(item);
            var id = Guard.Text(item.Id, nameof(item.Id));
            Guard.Rule(ids.Add(id), "duplicate_preparation", "Preparation IDs must be unique within a course.");
            Guard.Rule(item.Quantity > 0, "invalid_quantity", "Quantity must be positive.");
            Guard.Rule(item.GuestPosition is null || (item.GuestPosition >= 1 && item.GuestPosition <= pax),
                "invalid_guest", "Preparation refers to a guest outside this service.");
            preparations.Add(new Preparation(item with { Id = id,
                Name = Guard.Text(item.Name, nameof(item.Name)),
                StationId = Guard.Text(item.StationId, nameof(item.StationId)) }));
        }
    }

    public void Fire(CommandStamp stamp)
    {
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Only pending courses can be fired.");
        State = CourseState.Fired;
        FiredAt = stamp.At;
        foreach (var p in preparations) p.State = PreparationState.Fired;
    }

    public void StartPreparation(string id)
    {
        EnsurePreparing();
        var item = Find(id);
        Guard.Rule(item.State == PreparationState.Fired, "preparation_not_fired", "Preparation was not fired.");
        item.State = PreparationState.Preparing;
        State = CourseState.Preparing;
    }

    public void ReadyPreparation(string id)
    {
        EnsurePreparing();
        var item = Find(id);
        Guard.Rule(item.State is PreparationState.Fired or PreparationState.Preparing,
            "preparation_not_active", "Preparation is not active.");
        item.State = PreparationState.Ready;
        State = CourseState.Preparing;
    }

    public void ValidateReady(CommandStamp stamp)
    {
        EnsurePreparing();
        Guard.Rule(!ReviewPending, "restrictions_unreviewed", "Review every preparation affected by the restriction change before validating.");
        Guard.Rule(preparations.All(p => !p.Definition.Mandatory || p.State == PreparationState.Ready),
            "mandatory_preparation_pending", "Every mandatory preparation must be ready.");
        State = CourseState.Ready;
        ReadyAt = stamp.At;
    }

    public void Serve(CommandStamp stamp)
    {
        Guard.Rule(State == CourseState.Ready, "course_not_ready", "Course is not ready.");
        Guard.Rule(!ReviewPending, "restrictions_unreviewed", "Review every preparation affected by the restriction change before serving.");
        State = CourseState.Served;
        ServedAt = stamp.At;
    }

    public void Skip(string reason)
    {
        reason = Guard.Text(reason, nameof(reason));
        // D0 deliberately does not recall a fired kitchen command. That needs acknowledgement.
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Only unsent courses can be skipped in D0.");
        State = CourseState.Skipped;
        SkipReason = reason;
    }

    // Marca para revision las elaboraciones YA enviadas a las que aplica el cambio. Un pase no
    // enviado no necesita revision: al dispararse, cada elaboracion proyecta las restricciones vigentes.
    internal int FlagForReview(Func<int?, bool> applies)
    {
        if (!Active) return 0;
        var count = 0;
        foreach (var p in preparations.Where(p => applies(p.Definition.GuestPosition)))
        { p.ReviewPending = true; count++; }
        return count;
    }

    internal void Review(string id, ReviewDecision decision, string note, CommandStamp stamp)
    {
        Guard.Rule(Active, "course_not_preparing", "Course is not available for review.");
        var item = Find(id);
        Guard.Rule(item.ReviewPending, "nothing_to_review", "This preparation has no pending restriction review.");
        item.ReviewPending = false;
        item.Review = new(decision, note, stamp.At);
        if (decision != ReviewDecision.Remake) return;
        // Rehacer: la elaboracion vuelve a enviada y el pase pierde su validacion de salida.
        item.State = PreparationState.Fired;
        if (State == CourseState.Ready) { State = CourseState.Preparing; ReadyAt = null; }
    }

    public CourseView View() => new(Id, Name, State, FiredAt, ReadyAt, ServedAt, SkipReason,
        Array.AsReadOnly(preparations.Select(p => p.View()).ToArray()));
    private Preparation Find(string id) => preparations.FirstOrDefault(p => p.Definition.Id == id)
        ?? throw new RuleViolation("preparation_not_found", "Preparation not found.");
    private void EnsurePreparing() => Guard.Rule(State is CourseState.Fired or CourseState.Preparing,
        "course_not_preparing", "Course is not available for preparation.");
}
