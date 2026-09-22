using Costina.Core.Domain;

namespace Costina.Modules.Dining.Domain;

public enum DiningState { Open, InService, Paused, Completed, Cancelled }
public enum CourseState { Pending, Fired, Preparing, Ready, Served, Skipped }
public enum PreparationState { Pending, Fired, Preparing, Ready }
public enum OccupancyState { Occupied, Released }

public sealed record PreparationDefinition(string Id, string Name, string StationId,
    int Quantity = 1, int? GuestPosition = null, bool Mandatory = true);
public sealed record CourseDefinition(string Id, string Name, IReadOnlyList<PreparationDefinition> Preparations);

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
    IReadOnlyList<string>? Actions = null);

// These DTOs contain no account, payment, price, balance or fiscal fields.
// Restrictions/RestrictionsPendingAck son ESTADO del servicio (no affordances) y viajan siempre.
public sealed record DiningView(string Id, string TableId, int Pax, DiningState State,
    IReadOnlyList<CourseView> Courses,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null,
    IReadOnlyList<GuestRestriction>? Restrictions = null,
    bool RestrictionsPendingAck = false);
public sealed record OccupancyView(string Id, string TableId, string ServiceId,
    OccupancyState State, DateTimeOffset? ReleasedAt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Actions = null);
public sealed record ServiceBoardView(DiningView Service, OccupancyView Occupancy);
