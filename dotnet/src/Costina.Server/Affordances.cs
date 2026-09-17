using Costina.Domain;
using Costina.Persistence;

namespace Costina.Server;

// Filtro de affordances por ROL. Debe reflejar exactamente lo que el middleware autoriza:
// un cliente no debe ver como disponible una accion que el servidor rechazaria con 403.
// El dominio calcula QUE admite el agregado; aqui se decide QUIEN puede verlo/hacerlo.
public static class Affordances
{
    private static readonly string[] KitchenAllowed = ["preparation-start", "preparation-ready", "ready", "review-preparation"];

    public static bool Allows(string role, string action) => role switch
    {
        "main" => true,
        "service" => action is not ("complete" or "cancel-unstarted" or "release" or "review-preparation"),
        "kitchen" => KitchenAllowed.Contains(action, StringComparer.Ordinal),
        _ => false
    };

    private static IReadOnlyList<string>? Keep(IReadOnlyList<string>? actions, string role)
        => actions?.Where(a => Allows(role, a)).ToArray();

    // Espejo de la matriz de estacion (D4.3): lo que un dispositivo no puede hacer no se anuncia.
    private static bool StationKeeps(string action, string? station, string prepStation) => station is null
        || (action is "preparation-start" or "preparation-ready"
            ? string.Equals(prepStation, station, StringComparison.Ordinal)
            : true);
    private static bool PassKeeps(string action, string? station) => station is null
        || (action is "ready" or "review-preparation" ? string.Equals(station, "pase", StringComparison.Ordinal) : true);

    public static DiningView Filter(DiningView view, string role, string? station = null) => view with
    {
        Actions = Keep(view.Actions, role),
        Courses = view.Courses.Select(c => c with
        {
            Actions = Keep(c.Actions, role)?.Where(a => PassKeeps(a, station)).ToArray(),
            Preparations = c.Preparations.Select(p => p with
            {
                Actions = Keep(p.Actions, role)?.Where(a => StationKeeps(a, station, p.StationId)).ToArray()
            }).ToArray()
        }).ToArray()
    };

    public static OccupancyView Filter(OccupancyView view, string role)
        => view with { Actions = Keep(view.Actions, role) };

    public static BoardRow Filter(BoardRow row, string role, string? station = null)
        => row with { Service = Filter(row.Service, role, station), Occupancy = Filter(row.Occupancy, role) };
}
