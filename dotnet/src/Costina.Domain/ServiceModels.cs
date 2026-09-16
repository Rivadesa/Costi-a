namespace Costina.Domain;

public enum DiningState { Open, InService, Paused, Completed, Cancelled }
public enum CourseState { Pending, Fired, Preparing, Ready, Served, Skipped }
public enum PreparationState { Pending, Fired, Preparing, Ready }
public enum OccupancyState { Occupied, Released }
public enum AccountState { Open, Closed }
public enum PaymentCoverage { Unpaid, PartiallyPaid, Paid, Credit }

public sealed record PreparationDefinition(string Id, string Name, string StationId,
    int Quantity = 1, int? GuestPosition = null, bool Mandatory = true);
public sealed record CourseDefinition(string Id, string Name, IReadOnlyList<PreparationDefinition> Preparations);

public sealed record PreparationView(string Id, string Name, string StationId, int Quantity,
    int? GuestPosition, bool Mandatory, PreparationState State);
public sealed record CourseView(string Id, string Name, CourseState State,
    DateTimeOffset? FiredAt, DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt,
    string? SkipReason, IReadOnlyList<PreparationView> Preparations);

// These DTOs contain no account, payment, price, balance or fiscal fields.
public sealed record DiningView(string Id, string TableId, int Pax, DiningState State,
    IReadOnlyList<CourseView> Courses);
public sealed record OccupancyView(string Id, string TableId, string ServiceId,
    OccupancyState State, DateTimeOffset? ReleasedAt);
public sealed record ServiceBoardView(DiningView Service, OccupancyView Occupancy);
