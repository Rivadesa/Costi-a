using Costina.Modules.Dining.Domain;

namespace Costina.Modules.Dining;

// Filtro de affordances por ROL. Debe reflejar exactamente lo que el middleware autoriza:
// un cliente no debe ver como disponible una accion que el servidor rechazaria con 403.
// El dominio calcula QUE admite el agregado; aqui se decide QUIEN puede verlo/hacerlo.
public static class Affordances
{
    // Estacion reservada del pase (E2): valida y revisa; desde E4e (ADR-016) tambien marca por cualquier estacion.
    public const string PassStation = "pase";
    // E4e: cada orden de grupo con su individual. Por ROL valen lo mismo (Allows) y el anuncio del grupo se deduce de la individual (Filter).
    private static readonly (string Individual, string Group)[] Groups =
        [("preparation-start", "preparations-start"), ("preparation-ready", "preparations-ready"), ("review-preparation", "review-preparations")];
    private static readonly string[] KitchenAllowed = ["preparation-start", "preparation-ready", "ready", "review-preparation",
        "preparations-start", "preparations-ready", "review-preparations"];

    // C3b: add-consumptions (la ronda) es de principal y sala, como add-consumption: sala la tiene porque no esta en su lista de
    // excluidas y cocina no, porque no esta en KitchenAllowed. Es accion de SESION (DiningModule.SessionActions), sin estacion.
    // E4e: la sala queda fuera de review-preparations como de review-preparation, y conserva preparations-start|ready como conserva las
    // individuales: lo que impide a un comandero de sala marcar platos de cocina es la estacion (StationAllowed), plato a plato, y que
    // la excepcion del pase (PassMarksAll) nunca es de la sala.
    public static bool Allows(string role, string action) => role switch
    {
        "main" => true,
        "service" => action is not ("complete" or "cancel-unstarted" or "release" or "review-preparation" or "review-preparations"),
        "kitchen" => KitchenAllowed.Contains(action, StringComparer.Ordinal),
        _ => false
    };

    private static IReadOnlyList<string>? Keep(IReadOnlyList<string>? actions, string role)
        => actions?.Where(a => Allows(role, a)).ToArray();

    private static bool IsPass(string? station) => string.Equals(station, PassStation, StringComparison.Ordinal);

    // E4e (ADR-016; revision): marcar (preparation(s)-start|ready) por CUALQUIER estacion es de un dispositivo de COCINA (o un principal
    // antiguo) en el pase, nunca de la sala. Antes de A2 un comandero aprobado sin tocar nada quedaba en 'pase' con rol de sala, y A2 no
    // revisa los ya emparejados: por la estacion sola, ese movil marcaria el trabajo de todas las estaciones. Falla cerrado: un rol
    // desconocido no la tiene. La usan los dos lados de la matriz: este filtro y DiningOperations.StationAllowed.
    public static bool PassMarksAll(string role, string? station) => IsPass(station) && role is ("kitchen" or "main");

    // Espejo de la matriz de estacion (D4.3; DiningOperations.StationAllowed): lo que un dispositivo no puede hacer no se anuncia.
    // E4e (ADR-016): el pase de cocina marca por cualquier estacion (PassMarksAll); las demas, y la sala en el pase, solo lo suyo.
    private static bool StationKeeps(string action, string role, string? station, string prepStation) => station is null
        || (action is "preparation-start" or "preparation-ready"
            ? string.Equals(prepStation, station, StringComparison.Ordinal) || PassMarksAll(role, station)
            // D6.4: review-preparation se anuncia EN LA ELABORACION (AllowedActions) y el comando exige la estacion
            // 'pase' (DiningOperations.StationAllowed). Sin este filtro una estacion veia un boton que daba wrong_station.
            : action is not "review-preparation" || IsPass(station));
    private static bool PassKeeps(string action, string? station) => station is null
        || (action is "ready" or "review-preparation" or "review-preparations" ? IsPass(station) : true);

    public static DiningView Filter(DiningView view, string role, string? station = null) => view with
    {
        Actions = Keep(view.Actions, role),
        Courses = view.Courses.Select(c => Filter(c, role, station)).ToArray()
    };

    // E4e: es la PRIMERA ACCION QUE SE DEDUCE EN EL FILTRO (el dominio no la anuncia). Cada orden de grupo sale en el pase si alguna de
    // sus elaboraciones anuncia la individual DESPUES de filtrar por rol y estacion, asi que hereda la matriz entera: una estacion solo
    // ve el grupo de lo suyo, el pase el de todo y la sala nunca review-preparations. El cliente manda en el grupo SOLO las elaboraciones
    // que anuncian la individual (Costina.Acceptance comprueba que esa orden es ejecutable). Una vista sin affordances sigue sin ellas.
    private static CourseView Filter(CourseView course, string role, string? station)
    {
        var preparations = course.Preparations.Select(p => p with
        {
            Actions = Keep(p.Actions, role)?.Where(a => StationKeeps(a, role, station, p.StationId)).ToArray()
        }).ToArray();
        var groups = course.Actions is null ? []
            : Groups.Where(g => preparations.Any(p => p.Actions?.Contains(g.Individual, StringComparer.Ordinal) == true)).Select(g => g.Group);
        return course with
        {
            Actions = Keep(course.Actions?.Concat(groups).ToArray(), role)?.Where(a => PassKeeps(a, station)).ToArray(),
            Preparations = preparations
        };
    }

    public static OccupancyView Filter(OccupancyView view, string role)
        => view with { Actions = Keep(view.Actions, role) };

    public static BoardRow Filter(BoardRow row, string role, string? station = null)
        => row with { Service = Filter(row.Service, role, station), Occupancy = Filter(row.Occupancy, role) };
}
