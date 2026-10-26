using System.Globalization;
namespace Costina.Client;

// E4e (plan 2026-10-26, seccion 9; plan 2026-10-15, seccion 4.5): los platos de un pase AGRUPADOS («Ostra × 7»), en el Comedor del
// puesto. Todo es PRESENTACION de lo leido:
//  - un plato es el mismo nombre en la misma estacion (comparacion exacta), en el orden en que aparece por primera vez: PreparationView
//    no lleva el identificador del plato del menu, y su sufijo cambia segun la oferta. Su cantidad suma las de sus elaboraciones;
//  - las elaboraciones de un comensal con restriccion, o con una decision de cocina, son las EXCEPCIONES: van aparte, debajo del plato,
//    escritas (comensal, tipo, sustancia, gravedad y la decision), nunca escondidas en el grupo ni solo con color;
//  - nada habilita nada: un boton de plato existe porque el servidor anuncia la orden de grupo en el pase, y solo lleva las
//    elaboraciones que anuncian la individual (Announcing). La cocina agrupara igual en C3d (tabla comun kitchen-cases.json).
public sealed record DishGroup(string Name, string StationId, int Quantity, PreparationDto[] Preparations)
{
    // Un plato esta listo cuando lo estan todas sus elaboraciones («3 de 5 platos listos», FloorPlan.CourseDetail).
    public bool AllReady => Preparations.All(p => DishGroups.Same(p.State, "Ready"));
    public PreparationDto[] Exceptions => Preparations.Where(DishGroups.IsException).ToArray();
    // Las elaboraciones de este plato que anuncian la accion individual: lo que lleva su orden de grupo.
    public string[] Announcing(string action) => Preparations.Where(p => p.Actions?.Contains(action, StringComparer.Ordinal) == true).Select(p => p.Id).ToArray();
}

public static class DishGroups
{
    public static DishGroup[] Of(CourseDto course) => course.Preparations
        .GroupBy(p => (p.Name, p.StationId))
        .Select(g => new DishGroup(g.Key.Name, g.Key.StationId, g.Sum(p => p.Quantity), g.ToArray()))
        .ToArray();

    public static bool IsException(PreparationDto preparation) =>
        preparation.ReviewPending || preparation.Review is not null || preparation.Restrictions is { Length: > 0 };

    // «Ostra × 7»; con una sola, el nombre.
    public static string Title(DishGroup group) =>
        group.Quantity > 1 ? group.Name + " × " + group.Quantity.ToString(CultureInfo.InvariantCulture) : group.Name;

    // Estado del plato con las palabras de siempre: el de todas sus elaboraciones si coinciden; si no, cuantas estan listas.
    public static string State(DishGroup group)
    {
        var states = group.Preparations.Select(p => p.State).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return states.Length == 1 ? Labels.Preparation(states[0])
            : group.Preparations.Count(p => Same(p.State, "Ready")).ToString(CultureInfo.InvariantCulture) + " de "
              + Labels.Plural(group.Preparations.Length, "lista", "listas");
    }

    // La linea del comensal (plan 2026-10-15, seccion 4.5): «Ostra · Comensal 3 · ⚠ ALERGIA marisco (grave) · Adaptado: vieira», o
    // «· Por revisar en cocina» mientras cocina no decide.
    public static string ExceptionLine(PreparationDto preparation) => string.Join(" · ",
        new[] { preparation.Name, Labels.Guest(preparation.GuestPosition), preparation.RestrictionsText, Labels.ReviewOutcome(preparation) }.Where(p => p.Length > 0));

    // Va en rojo lo que pide la maxima atencion: un plato por revisar o una restriccion alarmante (FloorPlan.Alarming). El resto, en ambar.
    public static bool ExceptionAlarming(PreparationDto preparation) =>
        preparation.ReviewPending || (preparation.Restrictions ?? []).Any(FloorPlan.Alarming);

    internal static bool Same(string value, string code) => string.Equals(value, code, StringComparison.OrdinalIgnoreCase);
}
