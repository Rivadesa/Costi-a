using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

public enum DiningState { Open, InService, Paused, Completed, Cancelled }
public enum CourseState { Pending, Fired, Preparing, Ready, Served, Skipped }
public enum PreparationState { Pending, Fired, Preparing, Ready }
public enum OccupancyState { Occupied, Released }

public sealed record PreparationDefinition(string Id, string Name, string StationId,
    int Quantity = 1, int? GuestPosition = null, bool Mandatory = true);
// E4a: ChoiceRequired = pase de menu cerrado: cada comensal elige su plato (Choose) antes de poder dispararlo.
// E4b: Optional = grupo de una carta libre: puede quedar vacio (no se dispara vacio: se anaden platos o se omite).
public sealed record CourseDefinition(string Id, string Name, IReadOnlyList<PreparationDefinition> Preparations, bool ChoiceRequired = false, bool Optional = false);

// Actions: affordances calculadas por el dominio en el momento de la lectura, NUNCA persistidas.
// Con valor null la propiedad no se serializa: los payloads de snapshot quedan identicos a D1.
// Restrictions (por elaboracion): proyeccion calculada al leer desde la lista del servicio;
// nunca persistida (la fuente unica vive en el agregado y su snapshot).
public sealed record PreparationView(string Id, string Name, string StationId, int Quantity,
    int? GuestPosition, bool Mandatory, PreparationState State,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<GuestRestriction>? Restrictions = null,
    // D3.5 (aditivos, SI persisten): revision pendiente y ultima decision de cocina sobre esta elaboracion.
    bool ReviewPending = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    PreparationReview? Review = null);
public sealed record CourseView(string Id, string Name, CourseState State,
    DateTimeOffset? FiredAt, DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt,
    string? SkipReason, IReadOnlyList<PreparationView> Preparations,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null,
    // E4a (aditivo, SI persiste): el pase exige una eleccion por comensal; un payload anterior restaura con false.
    bool ChoiceRequired = false,
    // E4b (aditivo, SI persiste): grupo de carta libre, puede quedar vacio.
    bool Optional = false);

// These DTOs contain no account, payment, price, balance or fiscal fields.
// Restrictions/RestrictionsPendingAck son ESTADO del servicio (no affordances) y viajan siempre.
public sealed record DiningView(string Id, string TableId, int Pax, DiningState State,
    IReadOnlyList<CourseView> Courses,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null,
    IReadOnlyList<GuestRestriction>? Restrictions = null,
    bool RestrictionsPendingAck = false,
    // E4a: oferta del nucleo con la que se abrio la mesa (null en servicios anteriores).
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? OfferId = null);
public sealed record OccupancyView(string Id, string TableId, string ServiceId,
    OccupancyState State, DateTimeOffset? ReleasedAt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null);
public sealed record ServiceBoardView(DiningView Service, OccupancyView Occupancy);
