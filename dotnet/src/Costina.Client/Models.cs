using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Costina.Client;

public sealed record Versioned<T>(long Version, T Data);
public sealed record SessionInfo(string Role, string TenantId, string CompanyId, string LocationId,
    string[]? Actions = null, string? InstallationId = null, string? ServerVersion = null,
    string? Actor = null, string? Station = null, bool Demo = false,
    // ADR-012: modulos activos en la instalacion; las pestanas se montan solo para ellos.
    string[]? Modules = null,
    // A4 (Inicio y dispositivos): nombre legible de la estacion de un puesto emparejado (un servidor anterior no lo envia).
    string? StationName = null);
// Administracion de puestos (D4.2/D4.3b, solo main): espejo de las filas que devuelve el servidor.
public sealed record PairingPendingDto(string PairingId, string DeviceName, DateTimeOffset ClaimedAt)
{ public override string ToString() => $"{DeviceName} · solicitado a las {ClaimedAt.ToLocalTime():HH:mm:ss}"; }
// A3: Online = canal de tiempo real abierto ahora mismo (un servidor anterior no lo envia: false).
public sealed record DeviceRowDto(string Id, string Name, string Role, string Station,
    string ApprovedBy, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt, bool Online = false)
{
    public bool Active => RevokedAt is null;
    // C1: textos del vocabulario comun (Labels); el actor que aprobo, sin el prefijo user:/device:.
    public string RoleLabel => Labels.DeviceRole(Role);
    public string StateLabel => Labels.Presence(Online, RevokedAt);
    public override string ToString() => $"{Name} · {RoleLabel} · {StateLabel} · aprobado por {Labels.Actor(ApprovedBy)}";
}
// A8: diagnostico (solo main, D5.2): lo que Inicio muestra del servidor. Sin datos de negocio ni secretos; campos opcionales.
public sealed record TlsInfo(bool Enabled, int? Port = null, DateTimeOffset? ServerCertificateExpiresAt = null, string? Names = null, string? CaFingerprintSha256 = null);
public sealed record BackupInfo(bool Automatic, DateTimeOffset? LastSuccessAt = null, string? LastFile = null, string? LastError = null);
public sealed record DiagnosticsDto(string Mode, string Version, DateTimeOffset StartedAt, TlsInfo? Tls = null, BackupInfo? Backup = null, bool RunningAsService = false);
// Resultado de conciliar una clave de idempotencia: si el servidor la aplico, devuelve su respuesta guardada.
public sealed record CommandLookup(string Key, bool Found, JsonElement? Response = null);
// E2: la mesa operativa lleva su sala (ZoneId/ZoneName son opcionales: un servidor anterior no los envia).
public sealed record TableChoice(string Id, string Name, int Capacity, string? ZoneId = null, string? ZoneName = null)
{ public override string ToString() => ZoneName is { Length: > 0 } ? $"{ZoneName} · {Name}" : Name; }
public sealed record MenuChoice(string Id, string Name) { public override string ToString() => Name; }
// E2: organizacion editable (solo main): espejo de GET /organization. Kind viene como texto (Kitchen, Pass, Bar, Room).
public sealed record TableDto(string Id, string Name, int Capacity, string ZoneId, int Sort, bool Active)
{ public string StateLabel => Labels.Active(Active, feminine: true); public override string ToString() => $"{Name} · {Labels.Plural(Capacity, "plaza", "plazas")} · {StateLabel}"; }
// E3.2: TariffName no viaja: la pantalla lo resuelve con las tarifas leidas (OrganizationDto.Tariffs) y la lista de salas nombra la
// tarifa de la sala cuando no es la General (sin tarifa propia la sala cobra con la General).
public sealed record ZoneDto(string Id, string Name, int Sort, bool Active, TableDto[] Tables, string? TariffId = null)
{
    [JsonIgnore] public string? TariffName { get; init; }
    public string StateLabel => Labels.Active(Active, feminine: true);
    public override string ToString() => $"{Name}{(TariffName is { Length: > 0 } tariff ? " · tarifa " + tariff : "")} · {Labels.Plural(Tables.Count(t => t.Active), "mesa activa", "mesas activas")} · {StateLabel}";
}
public sealed record StationDto(string Id, string Name, string Kind, int Sort, bool Active)
{
    public string KindLabel => Labels.StationKind(Kind);
    public string StateLabel => Labels.Active(Active, feminine: true);
    public override string ToString() => $"{Name} · {KindLabel} · {StateLabel}";
}
// E3.2: Tariffs = tarifas del catalogo (codigo, nombre, orden, activa; nunca precios) para elegir la de cada sala. Opcional: un
// servidor anterior no las envia.
public sealed record OrganizationDto(ZoneDto[] Zones, StationDto[] Stations, TariffDto[]? Tariffs = null);
// E4a: ofertas VIGENTES (degustacion / menu cerrado / carta) con sus pases y platos; Menus sigue como alias de un servidor anterior.
public sealed record OfferDishChoice(string Id, string Name) { public override string ToString() => Name; }
// E4b: Items = lo pedible en un grupo de carta (producto + presentacion); Dishes = platos de un menu cerrado.
public sealed record OfferItemChoice(string ProductId, string PresentationId, string Name, string Presentation)
{ public string Key => ProductId + "/" + PresentationId; public override string ToString() => Name + " · " + Presentation; }
public sealed record OfferCourseChoice(string Id, string Name, OfferDishChoice[] Dishes, OfferItemChoice[]? Items = null);
public sealed record OfferChoice(string Id, string Name, string Kind, OfferCourseChoice[] Courses)
{ public string KindLabel => Labels.OfferKind(Kind); public override string ToString() => $"{Name} · {KindLabel}"; }
public sealed record Configuration(TableChoice[] Tables, MenuChoice[]? Menus = null, OfferChoice[]? Offers = null);
// E4a: oferta completa (solo main): espejo de GET /erp/offers.
public sealed record OfferDishDto(string OfferId, string CourseId, string Id, string Name, string StationId, string? ProductId, int Sort, bool Active);
public sealed record OfferItemDto(string ProductId, string PresentationId, string Name, string Presentation, string? StationId, int Sort, bool Active);
public sealed record OfferCourseDto(string Id, string Name, int Sort, bool Active, OfferDishDto[] Dishes, OfferItemDto[]? Items = null);
public sealed record OfferDto(string Id, string Name, string Kind, string? ProductId, string Service, string? ValidFrom, string? ValidTo, int Weekdays, int Sort, bool Active, OfferCourseDto[] Courses);
// E3: vendible de caja = producto + presentacion con el precio de la tarifa general como referencia (el servidor fija el real).
// PresentationId/CategoryName/TariffId son opcionales: un servidor anterior no los envia. E3.2: ImageSha = huella de la imagen
// vigente del producto (null sin imagen o con un servidor anterior); los bytes se piden aparte (ApiClient.GetProductImageAsync).
public sealed record ProductChoice(string Id, string Name, string Presentation, long PriceCents, string? PresentationId = null, string? CategoryName = null, string? TariffId = null,
    string? ImageSha = null)
{ public override string ToString() => (CategoryName is { Length: > 0 } ? CategoryName + " · " : "") + $"{Name} · {Presentation} · {Money.Format(PriceCents)}"; }
// E3: catalogo completo (solo main): espejo de GET /erp/catalog. Precios con IVA incluido; ValidFrom como AAAA-MM-DD.
public sealed record TaxDto(string Id, string Name, decimal Rate, bool Active)
{ public string StateLabel => Labels.Active(Active, feminine: false); public override string ToString() => $"{Name} · {Rate:0.##} % · {StateLabel}"; }
public sealed record CategoryDto(string Id, string Name, string? ParentId, string? Color, int Sort, bool Active);
public sealed record PriceDto(string TariffId, string ValidFrom, long PriceCents);
public sealed record PresentationDto(string Id, string Name, int Sort, bool Active, PriceDto[] Prices);
// E3.2: ImageSha = huella de la imagen vigente (null sin imagen, quitada o con un servidor anterior).
public sealed record ProductDto(string Id, string Name, string? CategoryId, string TaxId, string? Reference, int Sort, bool Active, PresentationDto[] Presentations, string? StationId = null,
    string? ImageSha = null);
public sealed record TariffDto(string Id, string Name, int Sort, bool Active)
{ public string StateLabel => Labels.Active(Active, feminine: true); public override string ToString() => $"{Name} · {StateLabel}"; }
public sealed record CatalogDto(TaxDto[] Taxes, CategoryDto[] Categories, ProductDto[] Products, TariffDto[] Tariffs, string Today);
// C1: la pantalla muestra el nombre de la mesa (Names) y el estado en castellano; el ToString queda de respaldo con el codigo.
public sealed record AccountChoice(string ServiceId, string TableId, string State)
{ public string StateLabel => Labels.Account(State); public override string ToString() => $"{TableId} · {StateLabel}"; }
// Etiquetas de restriccion SIEMPRE con texto y severidad: nunca depender solo del color. C1: un tipo o una gravedad desconocidos
// salen tal cual (Labels); antes caian en "Preferencia" y "leve", lo mas suave, y una alergia nueva podia pasar por preferencia.
public sealed record GuestRestrictionDto(string Id, int? GuestPosition, string Kind, string Substance, string Severity)
{
    public string KindLabel => Labels.RestrictionKind(Kind);
    public string SeverityLabel => Labels.Severity(Severity);
    public string GuestLabel => Labels.Guest(GuestPosition);
    public override string ToString() => $"{GuestLabel} · {KindLabel} {Substance} ({SeverityLabel})";
}
// Decision de cocina sobre una elaboracion tras un cambio de restriccion (D3.5): siempre con texto.
public sealed record PreparationReviewDto(string Decision, string Note, DateTimeOffset At)
{
    public string Label => Labels.Review(Decision);
}
public sealed record PreparationDto(string Id, string Name, string StationId, int Quantity, int? GuestPosition, bool Mandatory, string State,
    string[]? Actions = null, GuestRestrictionDto[]? Restrictions = null, bool ReviewPending = false, PreparationReviewDto? Review = null)
{
    public string StateLabel => Labels.Preparation(State);
    public string GuestLabel => Labels.Guest(GuestPosition);
    public string RestrictionsText => Restrictions is null or [] ? ""
        : string.Join("  ·  ", Restrictions.Select(r => $"⚠ {r.KindLabel} {r.Substance} ({r.SeverityLabel})"));
    public string ReviewText => ReviewPending ? "⚠ PENDIENTE DE REVISIÓN"
        : Review is null ? "" : $"{Review.Label}: {Review.Note}";
}
public sealed record CourseDto(string Id, string Name, string State, DateTimeOffset? FiredAt,
    DateTimeOffset? ReadyAt, DateTimeOffset? ServedAt, string? SkipReason, PreparationDto[] Preparations,
    string[]? Actions = null, bool ChoiceRequired = false, bool Optional = false)
{
    public string StateLabel => Labels.Course(this);
    // El servidor sella la hora en UTC (+00:00): se muestra la hora local del puesto.
    public string ServedAtLabel => ServedAt is { } at ? at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) : "";
}
public sealed record DiningDto(string Id, string TableId, int Pax, string State, CourseDto[] Courses,
    string[]? Actions = null, GuestRestrictionDto[]? Restrictions = null, bool RestrictionsPendingAck = false, string? OfferId = null)
{ public string StateLabel => Labels.Dining(State); }
public sealed record OccupancyDto(string Id, string TableId, string ServiceId, string State, DateTimeOffset? ReleasedAt,
    string[]? Actions = null)
{ public string StateLabel => Labels.Occupancy(State); }
public sealed record BoardEntry(long Version, DiningDto Service, OccupancyDto Occupancy, long OccupancyVersion)
{ public override string ToString() => $"{Service.TableId} · {Labels.Plural(Service.Pax, "persona", "personas")} · {Service.StateLabel}"; }
public sealed record ChargeDto(string Id, string Description, int Quantity, long UnitPriceCents,
    bool Voided, string? VoidReason, long TotalCents)
{ public string Amount => Money.Format(TotalCents); }
public sealed record PaymentDto(string Id, string Method, long AmountCents, DateTimeOffset At)
{ public string MethodLabel => Labels.PaymentMethod(Method); }
public sealed record RefundDto(string Id, string Method, long AmountCents, string Reason, DateTimeOffset At)
{ public string MethodLabel => Labels.PaymentMethod(Method); }
public sealed record AccountDto(string Id, string ServiceId, string State, long TotalCents, long PaidCents,
    long BalanceCents, long CreditCents, string Coverage, ChargeDto[] Charges, PaymentDto[] Payments,
    string[]? Actions = null, string[]? VoidableChargeIds = null, long RefundedCents = 0, RefundDto[]? Refunds = null)
{
    public string StateLabel => Labels.Account(State);
    public string CoverageLabel => Labels.Coverage(Coverage);
}
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
