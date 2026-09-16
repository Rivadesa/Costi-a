namespace Costina.Domain;

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

public sealed partial class DiningService
{
    private readonly List<GuestRestriction> restrictions = [];

    public IReadOnlyList<GuestRestriction> Restrictions => restrictions.AsReadOnly();
    // Derivado, nunca un booleano suelto: hay trabajo enviado con elaboraciones sin revisar.
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
        // Protocolo de revision (ADR-009, F05): cada elaboracion ya enviada a la que aplica el cambio
        // queda pendiente de una decision explicita de cocina antes de validar, servir o disparar mas.
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
    public void ReviewPreparation(string courseId, string itemId, ReviewDecision decision, string note, CommandStamp stamp)
    {
        KitchenAllowed(stamp);
        note = Guard.Text(note, nameof(note));
        Guard.Rule(Enum.IsDefined(decision), "invalid_review", "Unknown review decision.");
        Find(courseId).Review(itemId, decision, note, stamp);
        Emit("restriction.reviewed", stamp, ("course_id", courseId), ("item_id", itemId),
            ("decision", decision.ToString()), ("note", note));
    }

    private int FlagAffected(GuestRestriction restriction)
        => courses.Sum(c => c.FlagForReview(position => Applies(restriction, position)));
}
