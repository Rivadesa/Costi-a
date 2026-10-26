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
    public static readonly string[] Codes = [Free, Seated, Kitchen, Ready, Eating, Paused, Finished, Cancelled];
    private static readonly string[] KnownCourseStates = ["Pending", "Fired", "Preparing", "Ready", "Served", "Skipped"];

    // Texto del estado de una tarjeta. Solo se rotulan los codigos de FloorPlan (comparacion EXACTA): un estado del servidor que no se
    // conoce sale tal cual aunque se parezca a uno de ellos (Labels no distingue mayusculas, y un pase "Cancelled" no es una mesa
    // cancelada ni un servicio "Ready" una mesa lista para servir).
    public static bool IsCode(string state) => Codes.Contains(state, StringComparer.Ordinal);
    public static string Label(string state) => IsCode(state) ? Labels.TableState(state) : state;

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

    // C3a: lo que pinta la tarjeta con Since: los minutos del estado que se ve ("12 min"); sin hora del servidor, nada.
    public static string Elapsed(DiningDto? service, DateTimeOffset now) => Since(service) is { } since ? Labels.Minutes(now - since) : "";

    // Avance de los pases: cada uno con su tono (hecho, listo, en cocina, pendiente) y cuantos estan servidos u omitidos.
    public static string[] Steps(DiningDto? service) => service is null ? [] : service.Courses.Select(Step).ToArray();
    // C3a: el avance en corto de la tarjeta, "2 de 5" (servidos u omitidos de todos); sin pases, nada.
    public static string Progress(DiningDto? service) => Steps(service) is { Length: > 0 } steps
        ? Done(service).ToString(CultureInfo.InvariantCulture) + " de " + steps.Length.ToString(CultureInfo.InvariantCulture) : "";
    public static string Step(CourseDto course) =>
        Same(course.State, "Served") || Same(course.State, "Skipped") ? "done"
        : Same(course.State, "Ready") ? "ready"
        : InKitchen(course) ? "kitchen"
        : Same(course.State, "Pending") ? "pending" : "other";
    public static int Done(DiningDto? service) => service is null ? 0 : service.Courses.Count(c => Step(c) == "done");
    // E4e (ADR-016): el pase que importa ahora (el primero sin servir ni omitir: el que esta en cocina o, si no hay, el que va a salir)
    // tiene platos por revisar (reviewPending, lo marca el servidor). Solo rotula el boton grande; el servidor ya no anuncia enviarlo.
    public static bool WaitsForReview(DiningDto? service) =>
        service?.Courses.FirstOrDefault(c => Step(c) != "done")?.Preparations.Any(p => p.ReviewPending) == true;

    // Aviso de restricciones de una mesa (siempre con texto): la palabra nombra lo mas alarmante y "· por revisar" se anade mientras
    // cocina no haya revisado un cambio. Alarming dice si va en rojo (alergia, grave, desconocido o por revisar).
    //  - "Alergia" si hay alguna alergia (con su gravedad tal cual si no se conoce: "Alergia (Critical)");
    //  - un tipo que no se conoce, TAL CUAL y con su gravedad ("Anaphylaxis (Critical)"), aunque haya tambien una alergia;
    //  - sin nada de eso, una intolerancia o preferencia grave o de gravedad desconocida con su tipo y su gravedad ("Intolerancia (grave)");
    //  - "Restriccion" solo para las moderadas o leves (y para un cambio sin revisar sin restricciones a la vista).
    // Asi el rojo y el ambar nunca llevan la misma palabra y un valor desconocido nunca se rebaja a una palabra generica.
    public const string PendingReview = "por revisar";
    public static string Alert(DiningDto? service)
    {
        if (service is null) return "";
        var word = AlertWord(service);
        return service.RestrictionsPendingAck ? word + " · " + PendingReview : word;
    }
    public static string AlertWord(DiningDto? service)
    {
        if (service is null) return "";
        var list = service.Restrictions ?? [];
        var words = new List<string>();
        var allergies = list.Where(r => Same(r.Kind, "Allergy")).ToArray();
        if (allergies.Length > 0)
            words.Add(allergies.FirstOrDefault(r => !KnownSeverity(r.Severity)) is { } odd ? $"Alergia ({Labels.Severity(odd.Severity)})" : "Alergia");
        words.AddRange(list.Where(r => !KnownKind(r.Kind)).Select(r => $"{Labels.RestrictionKind(r.Kind)} ({Labels.Severity(r.Severity)})"));
        if (words.Count == 0)
            words.AddRange(list.Where(Alarming).OrderBy(r => KnownSeverity(r.Severity)).Select(r => $"{Labels.RestrictionKind(r.Kind)} ({Labels.Severity(r.Severity)})"));
        if (words.Count == 0 && (list.Length > 0 || service.RestrictionsPendingAck)) words.Add("Restricción");
        return string.Join(", ", words.Distinct(StringComparer.Ordinal));
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
    // E4e (plan 2026-10-15, seccion 4.5): cuenta PLATOS, no lineas por comensal: «3 de 5 platos listos» (DishGroups: mismo nombre en la
    // misma estacion; un plato esta listo cuando lo estan todas sus elaboraciones). Un menu de 8 personas y 3 platos son 3 platos, no 24.
    public static string CourseDetail(CourseDto course, int pax)
    {
        var preparations = course.Preparations;
        if (Same(course.State, "Served")) return course.ServedAt is { } served ? "Servido a las " + Labels.Clock(served) : "";
        if (Same(course.State, "Skipped")) return course.SkipReason is { Length: > 0 } reason ? "Motivo: " + reason : "";
        if (Same(course.State, "Ready")) return course.ReadyAt is { } ready ? "Listo desde las " + Labels.Clock(ready) : "";
        var dishes = DishGroups.Of(course);
        if (InKitchen(course))
        {
            var since = course.FiredAt is { } fired ? "En cocina desde las " + Labels.Clock(fired) : "En cocina";
            return dishes.Length == 0 ? since
                : $"{since} · {dishes.Count(d => d.AllReady).ToString(CultureInfo.InvariantCulture)} de {Labels.Plural(dishes.Length, "plato listo", "platos listos")}";
        }
        if (!Same(course.State, "Pending")) return "";
        if (course.Optional) return preparations.Length == 0 ? "Se piden platos de la carta" : Labels.Plural(preparations.Length, "plato pedido", "platos pedidos");
        if (course.ChoiceRequired)
        {
            var chosen = preparations.Where(p => p.GuestPosition is not null).Select(p => p.GuestPosition).Distinct().Count();
            return $"Han elegido {chosen.ToString(CultureInfo.InvariantCulture)} de {Labels.Plural(pax, "persona", "personas")}";
        }
        return Labels.Plural(dishes.Length, "plato", "platos");
    }

    private static bool KnownKind(string kind) => Same(kind, "Allergy") || Same(kind, "Intolerance") || Same(kind, "Preference");
    private static bool KnownSeverity(string severity) => Same(severity, "Severe") || Same(severity, "Moderate") || Same(severity, "Mild");
    private static bool InKitchen(CourseDto course) => Same(course.State, "Fired") || Same(course.State, "Preparing");
    private static bool Same(string value, string code) => string.Equals(value, code, StringComparison.OrdinalIgnoreCase);
}
