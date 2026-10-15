using System.Globalization;
namespace Costina.Client;

// C2 (plan 2026-10-14): plano del comedor. Todo lo de aqui es PRESENTACION calculada de lo leido (mesas de /configuration cruzadas
// con el tablero): el estado de la tarjeta, el aviso de restricciones, el avance de los pases y el tiempo. Nada habilita ni decide
// una accion: los botones siguen saliendo solo de lo que anuncia el servidor (affordances). Un valor desconocido se muestra tal cual
// (Labels), nunca se rebaja a un estado conocido.
public static class FloorPlan
{
    // Codigos de estado de la tarjeta (Labels.TableState los rotula). Un estado del servidor que no se conoce viaja crudo.
    public const string Free = "free", Seated = "seated", Kitchen = "kitchen", Ready = "ready", Eating = "eating",
        Paused = "paused", Finished = "finished", Cancelled = "cancelled";
    private static readonly string[] KnownCourseStates = ["Pending", "Fired", "Preparing", "Ready", "Served", "Skipped"];

    // Estado de una mesa: libre sin servicio en el tablero; despues manda el estado del servicio y, en servicio, el de sus pases
    // (listo para servir antes que en cocina; comiendo cuando ya se sirvio algo y no hay nada en cocina; sentados si aun no salio nada).
    public static string State(DiningDto? service)
    {
        if (service is null) return Free;
        if (Same(service.State, "Completed")) return Finished;
        if (Same(service.State, "Cancelled")) return Cancelled;
        if (Same(service.State, "Paused")) return Paused;
        if (Same(service.State, "Open")) return Seated;
        if (!Same(service.State, "InService")) return service.State;
        if (service.Courses.FirstOrDefault(c => !KnownCourseStates.Contains(c.State, StringComparer.OrdinalIgnoreCase)) is { } odd) return odd.State;
        if (service.Courses.Any(c => Same(c.State, "Ready"))) return Ready;
        if (service.Courses.Any(InKitchen)) return Kitchen;
        return service.Courses.Any(c => Same(c.State, "Served")) ? Eating : Seated;
    }

    // Desde cuando dura lo que muestra la tarjeta, si la lectura lo dice: en cocina desde que se envio el pase, listo desde que cocina
    // lo valido, comiendo desde el ultimo pase servido. Sentados, en pausa o terminado: el servidor no da la hora, no se inventa.
    public static DateTimeOffset? Since(DiningDto? service) => State(service) switch
    {
        Kitchen => service!.Courses.Where(InKitchen).Select(c => c.FiredAt).Min(),
        Ready => service!.Courses.Where(c => Same(c.State, "Ready")).Select(c => c.ReadyAt).Min(),
        Eating => service!.Courses.Select(c => c.ServedAt).Max(),
        _ => null
    };

    // Avance de los pases: cada uno con su tono (hecho, listo, en cocina, pendiente) y cuantos estan servidos u omitidos.
    public static string[] Steps(DiningDto? service) => service is null ? [] : service.Courses.Select(Step).ToArray();
    public static string Step(CourseDto course) =>
        Same(course.State, "Served") || Same(course.State, "Skipped") ? "done"
        : Same(course.State, "Ready") ? "ready"
        : InKitchen(course) ? "kitchen"
        : Same(course.State, "Pending") ? "pending" : "other";
    public static int Done(DiningDto? service) => service is null ? 0 : service.Courses.Count(c => Step(c) == "done");

    // Aviso de restricciones de una mesa (siempre con texto): "Alergia" si hay alguna alergia, "Restriccion" si hay otras, y
    // "· por revisar" mientras cocina no haya revisado un cambio. Alarming dice si va en rojo (alergia, grave, desconocido o por revisar).
    public static string Alert(DiningDto? service)
    {
        if (service is null) return "";
        var list = service.Restrictions ?? [];
        var word = list.Any(r => Same(r.Kind, "Allergy")) ? "Alergia" : list.Length > 0 || service.RestrictionsPendingAck ? "Restricción" : "";
        return service.RestrictionsPendingAck ? word + " · por revisar" : word;
    }
    public static bool AlertIsAlarming(DiningDto? service) =>
        service is not null && (service.RestrictionsPendingAck || (service.Restrictions ?? []).Any(Alarming));
    // Una restriccion pide la maxima atencion salvo que sea, CON SEGURIDAD, una intolerancia o preferencia moderada o leve: una alergia,
    // una gravedad grave o cualquier valor que no se conoce cuentan como alarma (nunca se rebajan).
    public static bool Alarming(GuestRestrictionDto restriction) =>
        !(Same(restriction.Kind, "Intolerance") || Same(restriction.Kind, "Preference"))
        || !(Same(restriction.Severity, "Moderate") || Same(restriction.Severity, "Mild"));

    // Linea de detalle de un pase en la ficha: cuando se sirvio, desde cuando esta en cocina y cuanto lleva hecho, quien falta por
    // elegir... Sin el numero de personas no se sabe cuantos faltan por elegir.
    public static string CourseDetail(CourseDto course, int pax)
    {
        var preparations = course.Preparations;
        if (Same(course.State, "Served")) return course.ServedAt is { } served ? "Servido a las " + Labels.Clock(served) : "";
        if (Same(course.State, "Skipped")) return course.SkipReason is { Length: > 0 } reason ? "Motivo: " + reason : "";
        if (Same(course.State, "Ready")) return course.ReadyAt is { } ready ? "Listo desde las " + Labels.Clock(ready) : "";
        if (InKitchen(course))
        {
            var since = course.FiredAt is { } fired ? "En cocina desde las " + Labels.Clock(fired) : "En cocina";
            return preparations.Length == 0 ? since
                : $"{since} · {preparations.Count(p => Same(p.State, "Ready")).ToString(CultureInfo.InvariantCulture)} de {Labels.Plural(preparations.Length, "elaboración lista", "elaboraciones listas")}";
        }
        if (!Same(course.State, "Pending")) return "";
        if (course.Optional) return preparations.Length == 0 ? "Se piden platos de la carta" : Labels.Plural(preparations.Length, "plato pedido", "platos pedidos");
        if (course.ChoiceRequired)
        {
            var chosen = preparations.Where(p => p.GuestPosition is not null).Select(p => p.GuestPosition).Distinct().Count();
            return $"Han elegido {chosen.ToString(CultureInfo.InvariantCulture)} de {Labels.Plural(pax, "persona", "personas")}";
        }
        return Labels.Plural(preparations.Length, "elaboración", "elaboraciones");
    }

    private static bool InKitchen(CourseDto course) => Same(course.State, "Fired") || Same(course.State, "Preparing");
    private static bool Same(string value, string code) => string.Equals(value, code, StringComparison.OrdinalIgnoreCase);
}
