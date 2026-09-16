namespace Costina.Domain;

// Affordances: el dominio es la unica fuente de que acciones admite cada agregado AHORA.
// Los clientes solo mapean accion->control; no reconstruyen reglas. El filtro por ROL es
// una decision de autorizacion y pertenece al servidor, no a este calculo.
// Estas listas se calculan al leer y nunca se persisten (ver ServiceModels: WhenWritingNull).
public sealed partial class DiningService
{
    public DiningView View(bool withActions)
    {
        if (!withActions) return View();
        bool active = State is DiningState.InService or DiningState.Paused;
        var pending = RestrictionsPendingAck;
        var actions = new List<string>();
        if (State == DiningState.Open && courses.Count > 0) actions.Add("start");
        if (State == DiningState.InService && !pending && !courses.Any(c => c.Active)
            && courses.Any(c => c.State == CourseState.Pending)) actions.Add("fire-next");
        if (State == DiningState.InService) actions.Add("pause");
        if (State == DiningState.Paused) actions.Add("resume");
        if (active && courses.All(c => c.Terminal)) actions.Add("complete");
        if (State == DiningState.Open && courses.All(c => !c.Active)) actions.Add("cancel-unstarted");
        if (State is not (DiningState.Completed or DiningState.Cancelled)) actions.Add("declare-restriction");
        if (State is not (DiningState.Completed or DiningState.Cancelled) && restrictions.Count > 0)
            actions.Add("remove-restriction");
        return new(Id, TableId, Pax, State,
            Array.AsReadOnly(courses.Select(c => c.ViewWithActions(State, restrictions)).ToArray()),
            actions.AsReadOnly(), restrictions.AsReadOnly(), pending);
    }
}

internal sealed partial class CourseExecution
{
    internal CourseView ViewWithActions(DiningState serviceState, IReadOnlyList<GuestRestriction> restrictions)
    {
        bool kitchenActive = serviceState is DiningState.InService or DiningState.Paused;
        bool preparing = State is CourseState.Fired or CourseState.Preparing;
        var actions = new List<string>();
        // La revision pendiente de cualquier elaboracion del pase retira validar y servir (F05).
        if (kitchenActive && preparing && !ReviewPending
            && preparations.All(p => !p.Definition.Mandatory || p.State == PreparationState.Ready)) actions.Add("ready");
        if (kitchenActive && !ReviewPending && State == CourseState.Ready) actions.Add("serve");
        if (serviceState is DiningState.Open or DiningState.InService or DiningState.Paused
            && State == CourseState.Pending) actions.Add("skip");
        var items = preparations.Select(p =>
        {
            var itemActions = new List<string>();
            if (kitchenActive && preparing && p.State == PreparationState.Fired) itemActions.Add("preparation-start");
            if (kitchenActive && preparing && p.State is PreparationState.Fired or PreparationState.Preparing)
                itemActions.Add("preparation-ready");
            if (kitchenActive && Active && p.ReviewPending) itemActions.Add("review-preparation");
            return p.View() with { Actions = itemActions.AsReadOnly(),
                Restrictions = restrictions.Where(r => DiningService.Applies(r, p.Definition.GuestPosition)).ToArray() };
        }).ToArray();
        return View() with { Preparations = Array.AsReadOnly(items), Actions = actions.AsReadOnly() };
    }
}

public sealed partial class TableOccupancy
{
    // La liberacion depende del estado del servicio propietario; se calcula al componer la lectura.
    public OccupancyView View(DiningService service)
    {
        var actions = new List<string>();
        if (State == OccupancyState.Occupied && service.Id == ServiceId
            && service.State is DiningState.Completed or DiningState.Cancelled) actions.Add("release");
        return View() with { Actions = actions.AsReadOnly() };
    }
}

public sealed partial class SettlementAccount
{
    public AccountView ViewWithActions()
    {
        var actions = new List<string>();
        var voidable = new List<string>();
        if (State == AccountState.Open)
        {
            actions.Add("add-product"); actions.Add("payment");
            voidable.AddRange(charges.Where(c => !c.Voided).Select(c => c.Id));
            if (voidable.Count > 0) actions.Add("void-charge");
            if (BalanceCents == 0 && CreditCents == 0) actions.Add("close");
        }
        return View() with { Actions = actions.AsReadOnly(), VoidableChargeIds = voidable.AsReadOnly() };
    }
}
