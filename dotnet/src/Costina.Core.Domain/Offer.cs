namespace Costina.Core.Domain;

// E4 (Hito 6, ADR-012): OFERTA del negocio, nucleo. Lo que se vende en sala: degustaciones/experiencias (todos los platos),
// menus cerrados (el comensal elige un plato por pase) y, en E4b, cartas libres. Una oferta cerrada SE VENDE como un producto
// del catalogo (presentacion "person") a las tarifas de E3. Los pases y platos son la definicion que el modulo Dining ejecuta.
// Nada se borra: se desactiva.
public enum OfferKind { Tasting, SetMenu, ALaCarte }
public enum OfferService { Any, Lunch, Dinner }

public sealed record OfferDefinition(string Id, string Name, OfferKind Kind, string? ProductId, OfferService Service,
    DateOnly? ValidFrom, DateOnly? ValidTo, int Weekdays, int Sort, bool Active);
public sealed record OfferCourseDefinition(string OfferId, string Id, string Name, int Sort, bool Active);
public sealed record OfferDishDefinition(string OfferId, string CourseId, string Id, string Name, string StationId, string? ProductId, int Sort, bool Active);
// E4b: item de una carta libre: lo que se puede pedir en un grupo (producto + presentacion del catalogo).
public sealed record OfferItemDefinition(string OfferId, string CourseId, string ProductId, string PresentationId, int Sort, bool Active);

public static class Offers
{
    // Presentacion con la que se vende una oferta cerrada (por persona) y categoria de sus productos-menu.
    public const string PersonPresentation = "person", MenusCategory = "menus";
    public const int AllWeekdays = 127, LunchUntilHour = 17;

    public static OfferDefinition Offer(string? id, string? name, OfferKind kind, string? productId, OfferService service,
        DateOnly? validFrom, DateOnly? validTo, int? weekdays, int? sort, bool active = true)
    {
        var code = Organization.Code(id, "offer id");
        var product = string.IsNullOrWhiteSpace(productId) ? null : Organization.Code(productId, "product id");
        Guard.Rule(kind == OfferKind.ALaCarte || product is not null, "product_required", "A tasting or set menu is sold as a product: productId is required.");
        // E4b: una carta no se vende como producto: cada plato se cobra al pedirlo.
        Guard.Rule(kind != OfferKind.ALaCarte || product is null, "product_not_allowed", "An a la carte offer has no menu product: dishes are charged as they are ordered.");
        Guard.Rule(validFrom is null || validTo is null || validFrom <= validTo, "invalid_validity", "validTo must not precede validFrom.");
        var days = weekdays ?? AllWeekdays;
        Guard.Rule(days is >= 1 and <= AllWeekdays, "invalid_weekdays", "weekdays is a 7-bit mask (Monday = 1 ... Sunday = 64) with at least one day.");
        return new(code, Organization.Name(name), kind, product, service, validFrom, validTo, days, Organization.Sort(sort), active);
    }

    public static OfferCourseDefinition Course(string? offerId, string? id, string? name, int? sort, bool active = true)
        => new(Organization.Code(offerId, "offer id"), Organization.Code(id, "course id"), Organization.Name(name), Organization.Sort(sort), active);

    public static OfferDishDefinition Dish(string? offerId, string? courseId, string? id, string? name, string? stationId, string? productId, int? sort, bool active = true)
        => new(Organization.Code(offerId, "offer id"), Organization.Code(courseId, "course id"), Organization.Code(id, "dish id"), Organization.Name(name),
            Organization.Code(stationId, "station id"), string.IsNullOrWhiteSpace(productId) ? null : Organization.Code(productId, "product id"), Organization.Sort(sort), active);

    public static OfferItemDefinition Item(string? offerId, string? courseId, string? productId, string? presentationId, int? sort, bool active = true)
        => new(Organization.Code(offerId, "offer id"), Organization.Code(courseId, "course id"), Organization.Code(productId, "product id"), Organization.Code(presentationId, "presentation id"), Organization.Sort(sort), active);

    // Bit del dia de la semana: lunes = 1 ... domingo = 64.
    public static int WeekdayBit(DayOfWeek day) => 1 << (((int)day + 6) % 7);

    // Vigente en un momento dado: activa, dentro de fechas, dia de la semana y servicio (comida hasta las 17:00 hora local).
    public static bool Available(OfferDefinition offer, DateTime at)
    {
        if (!offer.Active) return false;
        var date = DateOnly.FromDateTime(at);
        if (offer.ValidFrom is { } from && date < from) return false;
        if (offer.ValidTo is { } to && date > to) return false;
        if ((offer.Weekdays & WeekdayBit(at.DayOfWeek)) == 0) return false;
        return offer.Service == OfferService.Any || (offer.Service == OfferService.Lunch) == (at.Hour < LunchUntilHour);
    }
}
