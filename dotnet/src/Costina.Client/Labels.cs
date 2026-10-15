using System.Globalization;
namespace Costina.Client;

// C1 (plan 2026-10-14, secciones 2.9 y 4): el vocabulario de las pantallas en UNA sola clase. Funciones puras por familia:
// valor del servidor -> texto en castellano para quien trabaja en el restaurante. Los DTO exponen StateLabel/KindLabel que
// llaman aqui y las vistas enlazan esas propiedades, nunca el valor crudo. La PWA copiara esta misma tabla en C3 (labels.ts).
// Reglas:
//  - Un valor DESCONOCIDO se muestra tal cual: nunca se rebaja a una etiqueta mas suave ni se inventa. Una restriccion de un
//    tipo o de una gravedad nuevos sale cruda (antes salia "Preferencia (leve)": fallo de seguridad).
//  - Los codigos que viajan al servidor (Tag/SelectedValue de los desplegables, cuerpos de las ordenes) siguen siendo codigos:
//    aqui solo se rotula.
//  - La busqueda no distingue mayusculas ni guiones: las lecturas llegan en PascalCase (InService, ALaCarte) y las ordenes en
//    minusculas con guiones (fire-next, a-la-carte); las dos formas dan el mismo texto.
public static class Labels
{
    // Tabla de una familia: codigo -> texto. Lo que no esta en la tabla vuelve sin tocar.
    private sealed class Vocabulary
    {
        private readonly Dictionary<string, string> labels;
        public Vocabulary(params (string Code, string Label)[] rows) => labels = rows.ToDictionary(r => Key(r.Code), r => r.Label, StringComparer.Ordinal);
        public string this[string? value] => value is not null && labels.TryGetValue(Key(value), out var label) ? label : value ?? "";
        private static string Key(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static readonly Vocabulary DiningStates = new(("Open", "Sentados"), ("InService", "En servicio"), ("Paused", "En pausa"),
        ("Completed", "Terminado"), ("Cancelled", "Cancelado"));
    private static readonly Vocabulary CourseStates = new(("Pending", "Sin enviar"), ("Fired", "En cocina"), ("Preparing", "En preparación"),
        ("Ready", "Listo para servir"), ("Served", "Servido"), ("Skipped", "Omitido"));
    private static readonly Vocabulary PreparationStates = new(("Pending", "Sin enviar"), ("Fired", "Por empezar"), ("Preparing", "En preparación"),
        ("Ready", "Lista"));
    private static readonly Vocabulary OccupancyStates = new(("Occupied", "Ocupada"), ("Released", "Libre"));
    private static readonly Vocabulary AccountStates = new(("Open", "Abierta"), ("Closed", "Cerrada"));
    private static readonly Vocabulary Coverages = new(("Unpaid", "Sin pagar"), ("PartiallyPaid", "Pago parcial"), ("Paid", "Pagada"),
        ("Credit", "Pagado de más"));
    private static readonly Vocabulary PaymentMethods = new(("card", "Tarjeta"), ("cash", "Efectivo"), ("other", "Otro"));
    // E4d: el tipo tasting se llama "Menu degustacion" (el nombre del menu del promotor); set-menu, "Menu con eleccion" (cada persona elige).
    private static readonly Vocabulary OfferKinds = new(("tasting", "Menú degustación"), ("set-menu", "Menú con elección"), ("a-la-carte", "Carta"));
    private static readonly Vocabulary OfferServices = new(("any", "Comida y cena"), ("lunch", "Comida"), ("dinner", "Cena"));
    private static readonly Vocabulary StationKinds = new(("Kitchen", "Cocina"), ("Pass", "Pase"), ("Bar", "Barra"), ("Room", "Sala"));
    private static readonly Vocabulary DeviceRoles = new(("service", "Comandero"), ("kitchen", "Pantalla de cocina"), ("main", "Principal (antiguo)"));
    private static readonly Vocabulary SessionRoles = new(("main", "Responsable"), ("service", "Puesto de sala"), ("kitchen", "Pantalla de cocina"));
    // Restricciones (D3.2): SIEMPRE con texto; la alergia en mayusculas para que se vea sin depender del color.
    private static readonly Vocabulary RestrictionKinds = new(("Allergy", "ALERGIA"), ("Intolerance", "Intolerancia"), ("Preference", "Preferencia"));
    private static readonly Vocabulary Severities = new(("Severe", "grave"), ("Moderate", "moderada"), ("Mild", "leve"));
    // Decision de cocina tras un cambio de restricciones (D3.5): la lectura (Unaffected...) y la orden (unaffected...) dan lo mismo.
    private static readonly Vocabulary Reviews = new(("Unaffected", "no afecta"), ("Adapt", "adaptada"), ("Remake", "rehecha"));
    // C2: estado de una mesa en el plano del comedor (codigos de FloorPlan.State). Solo presentacion: nunca habilita nada.
    private static readonly Vocabulary TableStates = new(("free", "Libre"), ("seated", "Sentados"), ("kitchen", "En cocina"),
        ("ready", "Listo para servir"), ("eating", "Comiendo"), ("paused", "En pausa"), ("finished", "Terminado, por liberar"),
        ("cancelled", "Cancelado, por liberar"));
    private static readonly Vocabulary RealtimeStates = new(("connected", "Tiempo real activo"), ("reconnecting", "Reconectando…"),
        ("closed", "Sin tiempo real, reintentando"), ("unavailable", "Tiempo real no disponible"), ("inactive", "Tiempo real inactivo"));
    // Acciones que anuncia el servidor (affordances) y que el puesto envia como orden: textos de los avisos y de la orden pendiente.
    private static readonly Vocabulary Actions = new(
        ("open", "Abrir mesa"), ("start", "Iniciar servicio"), ("fire-next", "Enviar siguiente pase a cocina"), ("serve", "Marcar servido"),
        ("pause", "Pausar servicio"), ("resume", "Reanudar servicio"), ("complete", "Terminar servicio"), ("cancel-unstarted", "Cancelar mesa sin empezar"),
        ("skip", "Omitir pase"), ("ready", "Validar pase listo"), ("choose", "Elegir plato"), ("add-dish", "Pedir plato"),
        ("preparation-start", "Empezar elaboración"), ("preparation-ready", "Marcar elaboración lista"), ("review-preparation", "Revisar elaboración"),
        ("declare-restriction", "Declarar restricción"), ("remove-restriction", "Retirar restricción"), ("release", "Liberar mesa"),
        ("add-product", "Añadir consumo"), ("add-consumption", "Añadir consumo"), ("payment", "Registrar pago"), ("void-charge", "Anular cargo"),
        ("refund", "Devolver lo pagado de más"), ("close", "Cerrar cuenta"), ("reopen", "Reabrir cuenta"));
    // Vistas del area de trabajo (nombre del TabItem): mismo nombre en la barra lateral, los accesos de Inicio y la entrada.
    private static readonly Vocabulary Views = new(("HomeTab", "Inicio"), ("ServiceTab", "Comedor y cocina"), ("CheckoutTab", "Cuenta / Caja"),
        ("CatalogTab", "Catálogo y tarifas"), ("OffersTab", "Menús y cartas"), ("OrganizationTab", "Salas, mesas y estaciones"),
        ("DevicesTab", "Dispositivos"));
    // Rechazos del servidor (codigo de ApiError) en frases claras. El resto sale con su codigo para poder consultarlo.
    private static readonly Vocabulary Rejections = new(
        ("version_conflict", "Otro puesto ha cambiado estos datos a la vez."),
        ("storage_conflict", "El servidor tuvo un conflicto al guardar: puede haberse aplicado o no."),
        ("idempotency_conflict", "El servidor ya tenía esa misma orden con otro contenido."),
        ("operation_unconfirmed", "El servidor no pudo confirmar la operación: puede haberse aplicado o no."),
        ("request_failed", "El servidor no dio una respuesta reconocible."),
        ("forbidden", "Este acceso no tiene permiso para esa acción."),
        ("unauthorized", "La sesión ya no vale: vuelve a entrar."),
        ("invalid_credentials", "Usuario o contraseña incorrectos."),
        ("too_many_attempts", "Demasiados intentos desde este equipo. Espera un minuto."),
        ("not_found", "Ya no existe: puede que otro puesto lo haya cambiado."),
        ("invalid_request", "El servidor no aceptó los datos de la petición."),
        ("scope_mismatch", "Eso pertenece a otro negocio."),
        ("duplicate_code", "Ya existe otro con ese código."),
        ("table_occupied", "Esa mesa ya está ocupada."),
        ("table_inactive", "Esa mesa está desactivada."),
        ("table_already_released", "La mesa ya estaba liberada."),
        ("table_in_use", "La mesa está en uso: no se puede desactivar."),
        ("zone_in_use", "La sala está en uso: no se puede desactivar."),
        ("zone_inactive", "Esa sala está desactivada."),
        ("station_inactive", "Esa estación está desactivada."),
        ("reserved_station", "La estación Pase existe siempre: no se puede desactivar ni cambiar de tipo."),
        ("station_kind_mismatch", "Ese tipo de dispositivo no corresponde al tipo de estación."),
        ("device_role_not_allowed", "Un dispositivo nuevo no puede tener el papel de principal."),
        ("wrong_station", "Esta acción no corresponde a la estación de este puesto."),
        ("category_in_use", "La categoría está en uso: no se puede desactivar."),
        ("category_inactive", "Esa categoría está desactivada."),
        ("category_cycle", "Una categoría no puede quedar dentro de sí misma."),
        ("category_depth", "Las categorías admiten como mucho cuatro niveles."),
        ("tariff_in_use", "La tarifa está en uso: no se puede desactivar."),
        ("tariff_inactive", "Esa tarifa está desactivada."),
        ("reserved_tariff", "La tarifa General existe siempre: no se puede desactivar."),
        ("tax_in_use", "El impuesto está en uso: no se puede desactivar."),
        ("tax_inactive", "Ese impuesto está desactivado."),
        ("product_unavailable", "Ese producto está desactivado."),
        ("price_missing", "Ese producto no tiene precio vigente."),
        // E3.2: imagenes de los articulos (el servidor las reconoce por sus bytes).
        ("image_invalid", "La imagen no es válida: solo se admiten JPEG, PNG o WebP (nunca SVG)."),
        ("image_too_large", "La imagen ocupa más de 2 MB: redúcela y vuelve a intentarlo."),
        ("presentation_required", "Falta la presentación del producto (botella, copa, por persona…)."),
        ("product_required", "Un menú necesita su precio por persona."),
        ("product_not_allowed", "Una carta no tiene producto-menú: cada plato se cobra al pedirlo."),
        ("station_required", "El producto necesita una estación de cocina para pedirse en la carta."),
        ("not_a_la_carte", "Solo una carta lleva platos del catálogo por grupos."),
        ("offer_unavailable", "Ese menú o carta no se ofrece ahora."),
        ("offer_incomplete", "Ese menú o carta no tiene pases con platos activos."),
        ("offer_unpriced", "Ese menú no tiene precio por persona vigente."),
        ("invalid_validity", "La fecha de fin no puede ser anterior a la de inicio."),
        ("invalid_weekdays", "Elige al menos un día de la semana."),
        ("invalid_pax", "Número de personas no válido."),
        ("invalid_guest", "Comensal no válido."),
        ("invalid_quantity", "Cantidad no válida."),
        ("invalid_capacity", "Plazas no válidas: de 1 a 60."),
        ("invalid_price", "Precio no válido."),
        ("invalid_payment", "Importe del pago no válido."),
        ("invalid_refund", "Importe de la devolución no válido."),
        ("invalid_name", "Falta el nombre o no es válido."),
        ("invalid_color", "Color no válido: usa #RRGGBB."),
        ("menu_required", "La mesa necesita al menos un pase."),
        ("service_not_open", "Solo se inicia una mesa recién sentada."),
        ("service_not_running", "La mesa no está en servicio."),
        ("service_not_paused", "La mesa no está en pausa."),
        ("service_finished", "La mesa ya está terminada: no admite cambios."),
        ("active_course", "Hay otro pase en cocina: sírvelo antes de enviar el siguiente."),
        ("no_pending_course", "No quedan pases por enviar."),
        ("unfinished_courses", "Quedan pases sin servir ni omitir."),
        ("course_not_pending", "Ese pase ya se envió a cocina."),
        ("course_not_ready", "Ese pase aún no está listo."),
        ("course_not_preparing", "Ese pase no está en cocina."),
        ("course_not_found", "Ese pase ya no existe."),
        ("course_empty", "Ese grupo no tiene platos pedidos: pide alguno u omítelo."),
        ("choice_missing", "Falta que algún comensal elija plato."),
        ("no_choice", "En este pase no hay plato que elegir."),
        ("no_dishes", "En este pase no se piden platos de la carta."),
        ("dish_unavailable", "Ese plato está desactivado."),
        ("item_unavailable", "Ese plato no está en este grupo de la carta."),
        ("preparation_not_found", "Esa elaboración ya no existe."),
        ("preparation_not_fired", "Esa elaboración no está por empezar."),
        ("nothing_to_review", "Esa elaboración no tiene revisión pendiente."),
        ("restrictions_unreviewed", "Cocina aún no ha revisado el cambio de restricciones."),
        ("restrictions_unacknowledged", "Cocina aún no ha revisado el cambio de restricciones."),
        ("restriction_not_found", "Esa restricción ya no existe."),
        ("account_closed", "La cuenta está cerrada: reábrela con motivo para cambiarla."),
        ("account_not_balanced", "La cuenta aún no está saldada."),
        ("account_not_closed", "Solo se reabre una cuenta cerrada."),
        ("refund_exceeds_credit", "No se puede devolver más de lo pagado de más."),
        ("charge_not_found", "Ese cargo no está en la cuenta."),
        ("charge_already_voided", "Ese cargo ya estaba anulado."),
        ("duplicate_payment", "Ese pago ya estaba registrado."),
        ("duplicate_charge", "Ese cargo ya estaba registrado."));

    public static string Dining(string state) => DiningStates[state];
    public static string Course(string state) => CourseStates[state];
    // Estado de un pase con su matiz: el motivo de la omision, la eleccion pendiente de un menu cerrado y el grupo de carta vacio.
    // Sin el numero de personas (pax) solo se sabe que falta elegir cuando nadie ha elegido todavia.
    public static string Course(CourseDto course, int? pax = null)
    {
        if (Same(course.State, "Skipped") && course.SkipReason is { Length: > 0 } reason) return Course(course.State) + ": " + reason;
        if (Same(course.State, "Pending"))
        {
            if (course.Optional && course.Preparations.Length == 0) return "Sin platos pedidos";
            var chosen = course.Preparations.Where(p => p.GuestPosition is not null).Select(p => p.GuestPosition).Distinct().Count();
            if (course.ChoiceRequired && (pax is { } people ? chosen < people : chosen == 0)) return "Falta elegir plato";
        }
        return Course(course.State);
    }
    public static string Preparation(string state) => PreparationStates[state];
    public static string Guest(int? position) => position is null ? "Toda la mesa" : "Comensal " + position.Value.ToString(CultureInfo.InvariantCulture);
    public static string Occupancy(string state) => OccupancyStates[state];
    public static string Account(string state) => AccountStates[state];
    public static string Coverage(string coverage) => Coverages[coverage];
    public static string PaymentMethod(string method) => PaymentMethods[method];
    public static string OfferKind(string kind) => OfferKinds[kind];
    public static string OfferService(string service) => OfferServices[service];
    public static string StationKind(string kind) => StationKinds[kind];
    public static string DeviceRole(string role) => DeviceRoles[role];
    public static string SessionRole(string role) => SessionRoles[role];
    public static string RestrictionKind(string kind) => RestrictionKinds[kind];
    public static string Severity(string severity) => Severities[severity];
    public static string Review(string decision) => Reviews[decision];
    public static string TableState(string state) => TableStates[state];
    public static string Realtime(string state) => RealtimeStates[state];
    // C2: hora local del puesto (el servidor sella en UTC) y minutos transcurridos ("12 min", "1 h 05 min"; nunca negativos si el
    // reloj del PC va por detras del servidor).
    public static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string Minutes(TimeSpan elapsed)
    {
        var minutes = Math.Max(0, (int)elapsed.TotalMinutes);
        return minutes < 60 ? minutes.ToString(CultureInfo.InvariantCulture) + " min"
            : (minutes / 60).ToString(CultureInfo.InvariantCulture) + " h " + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + " min";
    }
    public static string Action(string action) => Actions[action];
    public static string View(string view) => Views[view];
    public static string Rejection(string code)
    {
        var text = Rejections[code];
        return text != code ? text : $"El servidor rechazó la operación (código {code}).";
    }
    // C1 (revision): aviso de una operacion fallida. Si la orden SIGUE PENDIENTE (5xx, conflicto de almacenamiento, respuesta sin el
    // eco de su clave...) no se sabe si se aplico: nunca se dice que el servidor la rechazo. Solo un rechazo definitivo, que ya cerro
    // la orden, se cuenta como rechazo; una respuesta sin codigo reconocible lleva ademas su estado HTTP.
    public static string Failure(string code, int status, bool stillPending) => stillPending
        ? $"El servidor no confirmó la operación: puede haberse aplicado o no (código {code})."
        : Rejection(code) + (code == "request_failed" ? $" (HTTP {status.ToString(CultureInfo.InvariantCulture)})" : "");

    // Actor de auditoria (D4.1): "user:jefa" y "device:pc-barra" se muestran como "jefa" y "pc-barra"; el actor de laboratorio
    // ("lab-main") como "laboratorio". Cualquier otra forma, tal cual.
    public static string Actor(string? actor) => actor switch
    {
        null or "" => "",
        _ when actor.StartsWith("user:", StringComparison.Ordinal) => actor["user:".Length..],
        _ when actor.StartsWith("device:", StringComparison.Ordinal) => actor["device:".Length..],
        _ when actor.StartsWith("lab-", StringComparison.Ordinal) => "laboratorio",
        _ => actor
    };

    // Presencia de un dispositivo (A3): quitado (con fecha local), conectado ahora o sin conexion.
    public static string Presence(bool online, DateTimeOffset? revokedAt)
        => revokedAt is { } at ? "Quitado el " + at.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)
            : online ? "Conectado" : "Sin conexión";

    public static string Active(bool active, bool feminine) => active ? feminine ? "activa" : "activo" : feminine ? "desactivada" : "desactivado";
    public static string Plural(int count, string one, string many) => count == 1 ? "1 " + one : count.ToString(CultureInfo.InvariantCulture) + " " + many;

    private static bool Same(string value, string code) => string.Equals(value, code, StringComparison.OrdinalIgnoreCase);
}
