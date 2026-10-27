using System.Globalization;
namespace Costina.Client;

// E4e (plan 2026-10-26, seccion 9; plan 2026-10-15, seccion 4.5): los platos de un pase AGRUPADOS («Ostra × 7»), en el Comedor del
// puesto. Todo es PRESENTACION de lo leido:
//  - un plato es el mismo nombre en la misma estacion (comparacion exacta), en el orden en que aparece por primera vez: PreparationView
//    no lleva el identificador del plato del menu, y su sufijo cambia segun la oferta. Su cantidad suma las de sus elaboraciones;
//  - las EXCEPCIONES van aparte, debajo del plato, escritas (comensal, tipo, sustancia, gravedad y la decision), nunca escondidas en el
//    grupo ni solo con color (IsException);
//  - nada habilita nada: un boton de plato existe porque el servidor anuncia la orden de grupo en el pase, y solo lleva las
//    elaboraciones que anuncian la individual (Announcing).
// C3d (plan 2026-10-27; plan 2026-10-22, seccion 4.3): Costina Cocina agrupa con la MISMA regla, comprobada por las dos partes con la
// tabla comun dotnet/tests/fixtures/kitchen-cases.json (ClientChecks y frontend/pwa/tests/cocina.test.ts, contra cocina.ts). Una
// restriccion de TODA LA MESA sale una sola vez arriba (en el PC, la franja de restricciones de la ficha) y nunca se repite en cada
// linea: si no, «Merluza × 4» serian cuatro lineas rojas iguales.
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

    // Va aparte (C3d, tabla comun): lo que esta por revisar, una decision que cambia el plato (todo lo que no es «No le afecta», tambien
    // una decision que no se conoce: nunca se esconde lo que no se entiende) o una restriccion de un comensal concreto. Un «No le afecta»
    // bajo una restriccion de toda la mesa no cambia el plato y la restriccion ya esta arriba: no es una excepcion.
    public static bool IsException(PreparationDto preparation) =>
        preparation.ReviewPending || preparation.Review is { } review && !Same(review.Decision, "Unaffected") || LineRestrictions(preparation).Length > 0;

    // Las restricciones que se escriben en la linea del comensal: las de un comensal. Las de toda la mesa salen una vez arriba.
    public static GuestRestrictionDto[] LineRestrictions(PreparationDto preparation) =>
        (preparation.Restrictions ?? []).Where(r => r.GuestPosition is not null).ToArray();

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
    // «· Por revisar en cocina» mientras cocina no decide. C3d: solo las restricciones de un comensal (LineRestrictions) y, en un plato
    // para compartir, de quien son: «Tabla · Toda la mesa · ⚠ ALERGIA marisco (grave) del Comensal 3».
    public static string ExceptionLine(PreparationDto preparation) => string.Join(" · ",
        new[] { preparation.Name, Labels.Guest(preparation.GuestPosition), LineText(preparation), Labels.ReviewOutcome(preparation) }.Where(p => p.Length > 0));

    private static string LineText(PreparationDto preparation) => string.Join("  ·  ", LineRestrictions(preparation).Select(r =>
        $"⚠ {r.KindLabel} {r.Substance} ({r.SeverityLabel})" + (r.GuestPosition == preparation.GuestPosition ? "" : " del " + Labels.Guest(r.GuestPosition))));

    // E4e (revision): estas elaboraciones llevan las MISMAS restricciones? Por tipo, sustancia y gravedad, sin distinguir mayusculas ni
    // el orden; nunca por el identificador (dos comensales con su propia intolerancia leve a la lactosa son lo mismo). El PC solo marca
    // por defecto las lineas por revisar de un plato cuando es asi: una nota pensada para una restriccion no sale marcada para otra.
    public static bool SameRestrictions(IEnumerable<PreparationDto> preparations) =>
        preparations.Select(RestrictionsKey).Distinct(StringComparer.Ordinal).Count() <= 1;

    private static string RestrictionsKey(PreparationDto preparation) => string.Join("\n", (preparation.Restrictions ?? [])
        .Select(r => string.Join("\t", r.Kind.Trim(), r.Substance.Trim(), r.Severity.Trim()).ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    // Va en rojo lo que pide la maxima atencion: un plato por revisar o una restriccion alarmante (FloorPlan.Alarming). El resto, en ambar.
    public static bool ExceptionAlarming(PreparationDto preparation) =>
        preparation.ReviewPending || (preparation.Restrictions ?? []).Any(FloorPlan.Alarming);

    internal static bool Same(string value, string code) => string.Equals(value, code, StringComparison.OrdinalIgnoreCase);
}
