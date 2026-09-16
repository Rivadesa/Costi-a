namespace Costina.Domain;

// Restricciones por comensal: datos ESTRUCTURADOS, nunca notas libres (AGENTS).
// GuestPosition null = toda la mesa. La severidad viaja siempre con la restriccion
// para que ninguna pantalla dependa solo del color.
public enum RestrictionKind { Allergy, Intolerance, Preference }
public enum RestrictionSeverity { Severe, Moderate, Mild }
public sealed record GuestRestriction(string Id, int? GuestPosition, RestrictionKind Kind,
    string Substance, RestrictionSeverity Severity);

public sealed partial class DiningService
{
    private readonly List<GuestRestriction> restrictions = [];
    private bool restrictionsPendingAck;

    public IReadOnlyList<GuestRestriction> Restrictions => restrictions.AsReadOnly();
    public bool RestrictionsPendingAck => restrictionsPendingAck;
    private bool KitchenWorkSent => courses.Any(c => c.Active);

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
        restrictions.Add(new(id, guestPosition, kind, substance, severity));
        // Protocolo de acuse: un cambio con trabajo ya enviado a cocina exige reconocimiento
        // explicito antes de validar, servir o disparar mas pases (ADR-009).
        if (KitchenWorkSent) restrictionsPendingAck = true;
        Emit("restriction.declared", stamp, ("restriction_id", id), ("kind", kind.ToString()),
            ("substance", substance), ("severity", severity.ToString()),
            ("guest_position", guestPosition?.ToString() ?? "all"),
            ("kitchen_ack_required", restrictionsPendingAck.ToString()));
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
        if (KitchenWorkSent) restrictionsPendingAck = true;
        Emit("restriction.removed", stamp, ("restriction_id", removed.Id), ("reason", reason),
            ("substance", removed.Substance), ("kitchen_ack_required", restrictionsPendingAck.ToString()));
    }

    // Acto de cocina: confirma que el cambio se ha visto. Permitido tambien en pausa,
    // como el resto de reconocimientos de trabajo ya enviado.
    public void AcknowledgeRestrictions(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State is DiningState.InService or DiningState.Paused,
            "service_not_active", "Acknowledgement requires an active or paused service.");
        Guard.Rule(restrictionsPendingAck, "nothing_to_acknowledge", "There is no unacknowledged restriction change.");
        restrictionsPendingAck = false;
        Emit("restriction.acknowledged", stamp);
    }
}
