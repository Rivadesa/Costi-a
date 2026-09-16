using System.Globalization;
namespace Costina.Client;

public sealed record Versioned<T>(long Version, T Data);
public sealed record SessionInfo(string Role, string TenantId, string CompanyId, string LocationId);
public sealed record TableChoice(string Id, string Name, int Capacity) { public override string ToString() => Name; }
public sealed record MenuChoice(string Id, string Name) { public override string ToString() => Name; }
public sealed record Configuration(TableChoice[] Tables, MenuChoice[] Menus);
public sealed record ProductChoice(string Id, string Name, string Presentation, long PriceCents)
{ public override string ToString() => $"{Name} · {Presentation} · {Money.Format(PriceCents)}"; }
public sealed record AccountChoice(string ServiceId, string TableId, string State)
{ public override string ToString() => $"{TableId} · {State} · {ServiceId[..Math.Min(8, ServiceId.Length)]}"; }
public sealed record PreparationDto(string Id, string Name, string StationId, int Quantity, int? GuestPosition, bool Mandatory, string State);
public sealed record CourseDto(string Id, string Name, string State, DateTimeOffset? FiredAt,
    DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt, string? SkipReason, PreparationDto[] Preparations);
public sealed record DiningDto(string Id, string TableId, int Pax, string State, CourseDto[] Courses);
public sealed record OccupancyDto(string Id, string TableId, string ServiceId, string State, DateTimeOffset? ReleasedAt);
public sealed record BoardEntry(long Version, DiningDto Service, OccupancyDto Occupancy, long OccupancyVersion)
{ public override string ToString() => $"{Service.TableId} · {Service.Pax} personas · {Service.State}"; }
public sealed record ChargeDto(string Id, string Description, int Quantity, long UnitPriceCents,
    bool Voided, string? VoidReason, long TotalCents)
{ public string Amount => Money.Format(TotalCents); }
public sealed record PaymentDto(string Id, string Method, long AmountCents, DateTimeOffset At);
public sealed record AccountDto(string Id, string ServiceId, string State, long TotalCents, long PaidCents,
    long BalanceCents, long CreditCents, string Coverage, ChargeDto[] Charges, PaymentDto[] Payments);
public sealed record OpenResult(string ServiceId, long Version, long OccupancyVersion);

public static class Money
{
    public static string Format(long cents) => (cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("es-ES")) + " €";
    public static long Parse(string input)
    {
        var value = input.Trim();
        if (value.Length == 0 || value.Count(c => c is ',' or '.') > 1 || value.Any(c => !char.IsAsciiDigit(c) && c is not ',' and not '.'))
            throw new ArgumentException("Introduce un importe positivo, sin separadores de miles.");
        var parts = value.Replace(',', '.').Split('.');
        if (parts[0].Length == 0 || parts.Length > 1 && (parts[1].Length == 0 || parts[1].Length > 2))
            throw new ArgumentException("El importe admite un máximo de dos decimales.");
        if (!decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0 || amount > long.MaxValue / 100m)
            throw new ArgumentException("Importe no válido.");
        return checked((long)(amount * 100m));
    }
}
