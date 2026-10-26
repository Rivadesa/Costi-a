using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

// Restricciones por comensal: datos ESTRUCTURADOS, nunca notas libres (AGENTS).
// GuestPosition null = toda la mesa. La severidad viaja siempre con la restriccion
// para que ninguna pantalla dependa solo del color.
public enum RestrictionKind { Allergy, Intolerance, Preference }
public enum RestrictionSeverity { Severe, Moderate, Mild }
public sealed record GuestRestriction(string Id, int? GuestPosition, RestrictionKind Kind,
    string Substance, RestrictionSeverity Severity);

// Decision de cocina sobre UNA elaboracion afectada por un cambio de restriccion (D3.5, F05):
// no afecta / se adapta (con el cambio aplicado) / se rehace. Siempre con nota y responsable.
public enum ReviewDecision { Unaffected, Adapt, Remake }
public sealed record PreparationReview(ReviewDecision Decision, string Note, DateTimeOffset At);
public sealed record PreparationDecision(string CourseId, string ItemId, ReviewDecision Decision, string Note);

public sealed partial class DiningService
{
    private readonly List<GuestRestriction> restrictions = [];

    public IReadOnlyList<GuestRestriction> Restrictions => restrictions.AsReadOnly();
    // Derivado, nunca un booleano suelto: hay elaboraciones sin revisar (desde E4e, tambien de pases sin enviar).
    public bool RestrictionsPendingAck => courses.Any(c => c.ReviewPending);

    // Una restriccion aplica a una elaboracion si cualquiera de las dos es de mesa entera
    // o si coinciden en posicion de comensal.
    internal static bool Applies(GuestRestriction restriction, int? guestPosition)
        => restriction.GuestPosition is null || guestPosition is null || restriction.GuestPosition == guestPosition;

    public string DeclareRestriction(int? guestPosition, RestrictionKind kind, string substance,
        RestrictionSeverity severity, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is not (DiningState.Completed or DiningState.Cancelled),
            "service_finished", "A finished service cannot change restrictions.");
        substance = Guard.Text(substance, nameof(substance));
        Guard.Rule(Enum.IsDefined(kind) && Enum.IsDefined(severity), "invalid_restriction", "Unknown kind or severity.");
        Guard.Rule(guestPosition is null || (guestPosition >= 1 && guestPosition <= Pax),
            "invalid_guest", "Restriction refers to a guest outside this service.");
        Guard.Rule(!restrictions.Any(r => r.GuestPosition == guestPosition && r.Kind == kind
            && string.Equals(r.Substance, substance, StringComparison.OrdinalIgnoreCase)),
            "duplicate_restriction", "This restriction is already declared for that guest.");
        var id = Guid.NewGuid().ToString("N");
        var restriction = new GuestRestriction(id, guestPosition, kind, substance, severity);
        restrictions.Add(restriction);
        // Protocolo de revision (ADR-009, F05; ADR-016 desde E4e): cada elaboracion a la que aplica el cambio y que aun se va a
        // cocinar, ENVIADA O NO, queda pendiente de una decision explicita de cocina antes de enviar su pase, validarlo o servirlo.
        var affected = FlagAffected(restriction);
        Emit("restriction.declared", stamp, ("restriction_id", id), ("kind", kind.ToString()),
            ("substance", substance), ("severity", severity.ToString()),
            ("guest_position", guestPosition?.ToString() ?? "all"),
            ("affected_preparations", affected.ToString()));
        return id;
    }

    public void RemoveRestriction(string restrictionId, string reason, CommandStamp stamp)
    {
        Check(stamp);
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(State is not (DiningState.Completed or DiningState.Cancelled),
            "service_finished", "A finished service cannot change restrictions.");
        var index = restrictions.FindIndex(r => r.Id == restrictionId);
        Guard.Rule(index >= 0, "restriction_not_found", "Restriction not found.");
        var removed = restrictions[index];
        restrictions.RemoveAt(index);
        // La historia queda en outbox/auditoria: retirar del estado actual no es un borrado silencioso.
        // Las elaboraciones que se adaptaron a la restriccion retirada tambien exigen decision.
        var affected = FlagAffected(removed);
        Emit("restriction.removed", stamp, ("restriction_id", removed.Id), ("reason", reason),
            ("substance", removed.Substance), ("affected_preparations", affected.ToString()));
    }

    // Acto de cocina por elaboracion: decide que pasa con ESE plato tras el cambio. Permitido en pausa,
    // como el resto de reconocimientos de trabajo ya enviado. "Rehacer" devuelve la elaboracion a
    // enviada y retira la validacion del pase si la tenia: nada sale sin una nueva validacion.
    // E4e (ADR-016): tambien con la mesa «Sentados» y en pases sin enviar (cocina decide antes de cocinar); rehacer, solo lo enviado.
    // E4e: la individual es el grupo de una (mismas reglas, mismo evento).
    public void ReviewPreparation(string courseId, string itemId, ReviewDecision decision, string note, CommandStamp stamp)
        => ReviewPreparations([new(courseId, itemId, decision, note)], stamp);

    // E4e: ORDEN DE GRUPO review-preparations: varias decisiones (de uno o varios pases: el menu entero de un comensal), cada una con su
    // decision y su nota obligatoria, en UNA orden. Todo o nada: se comprueban todas con las mismas reglas que la individual antes de
    // aplicar ninguna, y deja un restriction.reviewed por elaboracion, en el orden pedido. Vacia o con una elaboracion repetida: 422.
    public void ReviewPreparations(IReadOnlyList<PreparationDecision> decisions, CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.Open or DiningState.InService or DiningState.Paused,
            "service_not_active", "Reviews require a seated, running or paused service.");
        ArgumentNullException.ThrowIfNull(decisions);
        if (decisions.Count == 0) throw new ArgumentException("At least one review is required.", nameof(decisions));
        if (decisions.Any(d => d is null)) throw new ArgumentException("A review cannot be null.", nameof(decisions));
        if (decisions.Select(d => (d.CourseId, d.ItemId)).Distinct().Count() != decisions.Count)
            throw new ArgumentException("A preparation can be reviewed only once per command.", nameof(decisions));
        var checkedDecisions = decisions.Select(d =>
        {
            var note = Guard.Text(d.Note, nameof(d.Note));
            Guard.Rule(Enum.IsDefined(d.Decision), "invalid_review", "Unknown review decision.");
            var course = Find(d.CourseId);
            course.CheckReview(d.ItemId, d.Decision);
            return (Course: course, Decision: d, Note: note);
        }).ToArray();
        foreach (var (course, decision, note) in checkedDecisions)
        {
            course.ApplyReview(decision.ItemId, decision.Decision, note, stamp);
            Emit("restriction.reviewed", stamp, ("course_id", decision.CourseId), ("item_id", decision.ItemId),
                ("decision", decision.Decision.ToString()), ("note", note));
        }
    }

    private int FlagAffected(GuestRestriction restriction)
        => courses.Sum(c => c.FlagForReview(position => Applies(restriction, position)));

    // E4e: una elaboracion que nace para ese comensal (o sin comensal) necesita decision de cocina si le aplica alguna restriccion.
    private bool NeedsReview(int? guestPosition) => restrictions.Any(r => Applies(r, guestPosition));
}
