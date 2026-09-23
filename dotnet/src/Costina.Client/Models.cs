using System.Globalization;
using System.Text.Json;
namespace Costina.Client;

public sealed record Versioned<T>(long Version, T Data);
public sealed record SessionInfo(string Role, string TenantId, string CompanyId, string LocationId,
    string[]? Actions = null, string? InstallationId = null, string? ServerVersion = null,
    string? Actor = null, string? Station = null, bool Demo = false,
    // ADR-012: modulos activos en la instalacion; las pestanas se montan solo para ellos.
    string[]? Modules = null);
// Administracion de puestos (D4.2/D4.3b, solo main): espejo de las filas que devuelve el servidor.
public sealed record PairingPendingDto(string PairingId, string DeviceName, DateTimeOffset ClaimedAt)
{ public override string ToString() => $"{DeviceName} · solicitado a las {ClaimedAt.ToLocalTime():HH:mm:ss}"; }
public sealed record DeviceRowDto(string Id, string Name, string Role, string Station,
    string ApprovedBy, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt)
{
    public override string ToString() => $"{Name} · {Role}/{Station} · aprobado por {ApprovedBy}"
        + (RevokedAt is null ? "" : $" · REVOCADO {RevokedAt.Value.ToLocalTime():dd/MM HH:mm}");
}
// Resultado de conciliar una clave de idempotencia: si el servidor la aplico, devuelve su respuesta guardada.
public sealed record CommandLookup(string Key, bool Found, JsonElement? Response = null);
// E2: la mesa operativa lleva su sala (ZoneId/ZoneName son opcionales: un servidor anterior no los envia).
public sealed record TableChoice(string Id, string Name, int Capacity, string? ZoneId = null, string? ZoneName = null)
{ public override string ToString() => ZoneName is { Length: > 0 } ? $"{ZoneName} · {Name}" : Name; }
public sealed record MenuChoice(string Id, string Name) { public override string ToString() => Name; }
// E2: organizacion editable (solo main): espejo de GET /organization. Kind viene como texto (Kitchen, Pass, Bar, Room).
public sealed record TableDto(string Id, string Name, int Capacity, string ZoneId, int Sort, bool Active)
{ public string StateLabel => Active ? "activa" : "desactivada"; public override string ToString() => $"{Id} · {Name} · {Capacity} pax · {StateLabel}"; }
public sealed record ZoneDto(string Id, string Name, int Sort, bool Active, TableDto[] Tables, string? TariffId = null)
{ public string StateLabel => Active ? "activa" : "desactivada"; public override string ToString() => $"{Name} · {Tables.Count(t => t.Active)} mesas activas · {StateLabel}"; }
public sealed record StationDto(string Id, string Name, string Kind, int Sort, bool Active)
{
    public string KindLabel => Kind switch { "Kitchen" => "Cocina", "Pass" => "Pase", "Bar" => "Barra", "Room" => "Sala", _ => Kind };
    public string StateLabel => Active ? "activa" : "desactivada";
    public override string ToString() => $"{Name} ({Id}) · {KindLabel} · {StateLabel}";
}
public sealed record OrganizationDto(ZoneDto[] Zones, StationDto[] Stations);
public sealed record Configuration(TableChoice[] Tables, MenuChoice[]? Menus = null);   // menus: solo con el modulo Dining
// E3: vendible de caja = producto + presentacion con el precio de la tarifa general como referencia (el servidor fija el real).
// PresentationId/CategoryName/TariffId son opcionales: un servidor anterior no los envia.
public sealed record ProductChoice(string Id, string Name, string Presentation, long PriceCents, string? PresentationId = null, string? CategoryName = null, string? TariffId = null)
{ public override string ToString() => (CategoryName is { Length: > 0 } ? CategoryName + " · " : "") + $"{Name} · {Presentation} · {Money.Format(PriceCents)}"; }
// E3: catalogo completo (solo main): espejo de GET /erp/catalog. Precios con IVA incluido; ValidFrom como AAAA-MM-DD.
public sealed record TaxDto(string Id, string Name, decimal Rate, bool Active)
{ public string StateLabel => Active ? "activo" : "desactivado"; public override string ToString() => $"{Name} ({Id}) · {Rate:0.##} % · {StateLabel}"; }
public sealed record CategoryDto(string Id, string Name, string? ParentId, string? Color, int Sort, bool Active);
public sealed record PriceDto(string TariffId, string ValidFrom, long PriceCents);
public sealed record PresentationDto(string Id, string Name, int Sort, bool Active, PriceDto[] Prices);
public sealed record ProductDto(string Id, string Name, string? CategoryId, string TaxId, string? Reference, int Sort, bool Active, PresentationDto[] Presentations);
public sealed record TariffDto(string Id, string Name, int Sort, bool Active)
{ public string StateLabel => Active ? "activa" : "desactivada"; public override string ToString() => $"{Name} ({Id}) · {StateLabel}"; }
public sealed record CatalogDto(TaxDto[] Taxes, CategoryDto[] Categories, ProductDto[] Products, TariffDto[] Tariffs, string Today);
public sealed record AccountChoice(string ServiceId, string TableId, string State)
{ public override string ToString() => $"{TableId} · {State} · {ServiceId[..Math.Min(8, ServiceId.Length)]}"; }
// Etiquetas de restriccion SIEMPRE con texto y severidad: nunca depender solo del color.
public sealed record GuestRestrictionDto(string Id, int? GuestPosition, string Kind, string Substance, string Severity)
{
    public string KindLabel => Kind switch { "Allergy" => "ALERGIA", "Intolerance" => "Intolerancia", _ => "Preferencia" };
    public string SeverityLabel => Severity switch { "Severe" => "grave", "Moderate" => "moderada", _ => "leve" };
    public string GuestLabel => GuestPosition is null ? "Mesa" : "Comensal " + GuestPosition;
    public override string ToString() => $"{GuestLabel} · {KindLabel} {Substance} ({SeverityLabel})";
}
// Decision de cocina sobre una elaboracion tras un cambio de restriccion (D3.5): siempre con texto.
public sealed record PreparationReviewDto(string Decision, string Note, DateTimeOffset At)
{
    public string Label => Decision switch { "Unaffected" => "no afecta", "Adapt" => "adaptada", "Remake" => "rehecha", _ => Decision };
}
public sealed record PreparationDto(string Id, string Name, string StationId, int Quantity, int? GuestPosition, bool Mandatory, string State,
    string[]? Actions = null, GuestRestrictionDto[]? Restrictions = null, bool ReviewPending = false, PreparationReviewDto? Review = null)
{
    public string RestrictionsText => Restrictions is null or [] ? ""
        : string.Join("  ·  ", Restrictions.Select(r => $"⚠ {r.KindLabel} {r.Substance} ({r.SeverityLabel})"));
    public string ReviewText => ReviewPending ? "⚠ PENDIENTE DE REVISIÓN"
        : Review is null ? "" : $"{Review.Label}: {Review.Note}";
}
public sealed record CourseDto(string Id, string Name, string State, DateTimeOffset? FiredAt,
    DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt, string? SkipReason, PreparationDto[] Preparations,
    string[]? Actions = null);
public sealed record DiningDto(string Id, string TableId, int Pax, string State, CourseDto[] Courses,
    string[]? Actions = null, GuestRestrictionDto[]? Restrictions = null, bool RestrictionsPendingAck = false);
public sealed record OccupancyDto(string Id, string TableId, string ServiceId, string State, DateTimeOffset? ReleasedAt,
    string[]? Actions = null);
public sealed record BoardEntry(long Version, DiningDto Service, OccupancyDto Occupancy, long OccupancyVersion)
{ public override string ToString() => $"{Service.TableId} · {Service.Pax} personas · {Service.State}"; }
public sealed record ChargeDto(string Id, string Description, int Quantity, long UnitPriceCents,
    bool Voided, string? VoidReason, long TotalCents)
{ public string Amount => Money.Format(TotalCents); }
public sealed record PaymentDto(string Id, string Method, long AmountCents, DateTimeOffset At);
public sealed record RefundDto(string Id, string Method, long AmountCents, string Reason, DateTimeOffset At);
public sealed record AccountDto(string Id, string ServiceId, string State, long TotalCents, long PaidCents,
    long BalanceCents, long CreditCents, string Coverage, ChargeDto[] Charges, PaymentDto[] Payments,
    string[]? Actions = null, string[]? VoidableChargeIds = null, long RefundedCents = 0, RefundDto[]? Refunds = null);
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
