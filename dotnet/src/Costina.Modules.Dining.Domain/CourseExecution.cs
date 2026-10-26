using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

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
    private readonly int pax;
    public string Id { get; }
    // E4a: pase de menu cerrado; las elaboraciones nacen vacias y cada comensal elige la suya (Choose) antes de disparar.
    public bool ChoiceRequired { get; }
    // E4b: grupo de carta libre; los platos se anaden al pedir (AddDish) y un grupo vacio no se dispara.
    public bool Optional { get; }
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
        ChoiceRequired = definition.ChoiceRequired; Optional = definition.Optional; this.pax = pax;
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

    // Comensales sin plato elegido en un pase de menu cerrado (vacio si no exige eleccion).
    public IReadOnlyList<int> MissingChoices() => !ChoiceRequired ? []
        : Enumerable.Range(1, pax).Where(g => preparations.All(p => p.Definition.GuestPosition != g)).ToArray();
    // E4e (ADR-016): reviewPending = al comensal le aplica alguna restriccion: el plato nuevo nace por revisar (tambien al cambiar de idea,
    // porque sustituye la elaboracion y su decision anterior era de otro plato).
    internal void Choose(int guest, PreparationDefinition dish, bool reviewPending = false)
    {
        Guard.Rule(ChoiceRequired, "no_choice", "This course has no choice to make.");
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Choices close once the course is fired.");
        Guard.Rule(guest >= 1 && guest <= pax, "invalid_guest", "Guest outside this service.");
        preparations.RemoveAll(p => p.Definition.GuestPosition == guest);
        preparations.Add(new Preparation(dish with { Id = Guard.Text(dish.Id, nameof(dish.Id)) + "-" + guest, Name = Guard.Text(dish.Name, nameof(dish.Name)),
            StationId = Guard.Text(dish.StationId, nameof(dish.StationId)), GuestPosition = guest, Quantity = 1 }) { ReviewPending = reviewPending });
    }

    internal void AddDish(PreparationDefinition dish, bool reviewPending = false)
    {
        Guard.Rule(Optional, "no_dishes", "Dishes are added only to the groups of an a la carte offer.");
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Dishes are added while the course is pending; fire the next one.");
        Guard.Rule(dish.GuestPosition is null || (dish.GuestPosition >= 1 && dish.GuestPosition <= pax), "invalid_guest", "Guest outside this service.");
        Guard.Rule(dish.Quantity > 0, "invalid_quantity", "Quantity must be positive.");
        var id = Guard.Text(dish.Id, nameof(dish.Id));
        Guard.Rule(preparations.All(p => p.Definition.Id != id), "duplicate_preparation", "Preparation IDs must be unique within a course.");
        preparations.Add(new Preparation(dish with { Id = id, Name = Guard.Text(dish.Name, nameof(dish.Name)), StationId = Guard.Text(dish.StationId, nameof(dish.StationId)) })
            { ReviewPending = reviewPending });
    }

    public void Fire(CommandStamp stamp)
    {
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Only pending courses can be fired.");
        Guard.Rule(MissingChoices().Count == 0, "choice_missing", "Every guest must choose a dish before this course is fired.");
        Guard.Rule(!Optional || preparations.Count > 0, "course_empty", "This group has no dishes: add some or skip it.");
        State = CourseState.Fired;
        FiredAt = stamp.At;
        foreach (var p in preparations) p.State = PreparationState.Fired;
    }

    // E4e: marcas de estacion para una o varias elaboraciones del pase (la individual es el grupo de una). Se comprueban TODAS antes de
    // tocar ninguna: una mala no deja nada a medias. Lo que comprueba cada una no depende de las demas, asi que equivale a marcarlas
    // una detras de otra.
    internal void StartPreparations(IReadOnlyList<string> ids)
    {
        EnsurePreparing();
        var items = ids.Select(Find).ToArray();
        foreach (var item in items) Guard.Rule(item.State == PreparationState.Fired, "preparation_not_fired", "Preparation was not fired.");
        foreach (var item in items) item.State = PreparationState.Preparing;
        State = CourseState.Preparing;
    }

    internal void ReadyPreparations(IReadOnlyList<string> ids)
    {
        EnsurePreparing();
        var items = ids.Select(Find).ToArray();
        foreach (var item in items)
            Guard.Rule(item.State is PreparationState.Fired or PreparationState.Preparing, "preparation_not_active", "Preparation is not active.");
        foreach (var item in items) item.State = PreparationState.Ready;
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

    // Devuelve cuantas revisiones pendientes retira (E4e): lo que no se va a cocinar no se revisa.
    public int Skip(string reason)
    {
        reason = Guard.Text(reason, nameof(reason));
        // D0 deliberately does not recall a fired kitchen command. That needs acknowledgement.
        Guard.Rule(State == CourseState.Pending, "course_not_pending", "Only unsent courses can be skipped in D0.");
        State = CourseState.Skipped;
        SkipReason = reason;
        return WithdrawReviews();
    }

    // E4e (ADR-016): retira las revisiones pendientes de un pase que ya no se va a cocinar (omitido, o la mesa cancelada sin empezar).
    // Las decisiones ya tomadas (Review) se quedan; el evento de quien lo pide cuenta las retiradas.
    internal int WithdrawReviews()
    {
        var count = 0;
        foreach (var p in preparations.Where(p => p.ReviewPending)) { p.ReviewPending = false; count++; }
        return count;
    }

    // E4e (ADR-016): marca para revision las elaboraciones a las que aplica el cambio en TODO pase que aun se va a cocinar: sin enviar,
    // en cocina o listo sin servir. Antes (D3.5) solo lo enviado, y un pase sin enviar salia sin ninguna decision de cocina registrada.
    // Lo servido u omitido no se marca. La compatibilidad con D3.2 (Snapshots) sigue marcando solo los pases activos.
    internal int FlagForReview(Func<int?, bool> applies)
    {
        if (Terminal) return 0;
        var count = 0;
        foreach (var p in preparations.Where(p => applies(p.Definition.GuestPosition)))
        { p.ReviewPending = true; count++; }
        return count;
    }

    // E4e: revisar es COMPROBAR (sin tocar nada) y despues APLICAR, para que review-preparations compruebe todas sus decisiones antes de
    // aplicar ninguna. Lo que comprueba una no lo cambia la otra (rehacer deja el pase activo y no toca las demas elaboraciones).
    internal void CheckReview(string id, ReviewDecision decision)
    {
        // E4e: tambien en un pase sin enviar (se decide antes de cocinar); nunca en uno servido u omitido.
        Guard.Rule(!Terminal, "course_not_preparing", "Course is not available for review.");
        var item = Find(id);
        Guard.Rule(item.ReviewPending, "nothing_to_review", "This preparation has no pending restriction review.");
        // Rehacer es solo para lo que ya esta en cocina: un plato sin enviar se adapta o no le afecta.
        Guard.Rule(decision != ReviewDecision.Remake || Active, "nothing_to_remake",
            "Only work already sent to the kitchen can be remade: adapt it or mark it unaffected.");
    }

    internal void ApplyReview(string id, ReviewDecision decision, string note, CommandStamp stamp)
    {
        var item = Find(id);
        item.ReviewPending = false;
        item.Review = new(decision, note, stamp.At);
        if (decision != ReviewDecision.Remake) return;
        // Rehacer: la elaboracion vuelve a enviada y el pase pierde su validacion de salida.
        item.State = PreparationState.Fired;
        if (State == CourseState.Ready) { State = CourseState.Preparing; ReadyAt = null; }
    }

    public CourseView View() => new(Id, Name, State, FiredAt, ReadyAt, ServedAt, SkipReason,
        Array.AsReadOnly(preparations.Select(p => p.View()).ToArray()), ChoiceRequired: ChoiceRequired, Optional: Optional);
    private Preparation Find(string id) => preparations.FirstOrDefault(p => p.Definition.Id == id)
        ?? throw new RuleViolation("preparation_not_found", "Preparation not found.");
    private void EnsurePreparing() => Guard.Rule(State is CourseState.Fired or CourseState.Preparing,
        "course_not_preparing", "Course is not available for preparation.");
}
