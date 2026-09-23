using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E4a: acceso a la OFERTA (core.offers, offer_courses, offer_dishes) sobre la Unit del nucleo: misma transaccion, mismas
// guardas de ambito. El modulo Dining abre mesas por aqui (Offer + AvailableOffer), nunca por SQL propio.
// E4b: Items = lo pedible en un grupo de carta (producto + presentacion), con su nombre para las pantallas.
public sealed record OfferItemView(string ProductId, string PresentationId, string Name, string Presentation, string? StationId, int Sort, bool Active);
public sealed record OfferCourseView(string Id, string Name, int Sort, bool Active, OfferDishDefinition[] Dishes, OfferItemView[]? Items = null);
public sealed record OfferView(string Id, string Name, string Kind, string? ProductId, string Service, string? ValidFrom, string? ValidTo, int Weekdays, int Sort, bool Active, OfferCourseView[] Courses);

public static class OfferUnit
{
    public const string OfferColumns = "id,name,kind,product_id,service,valid_from,valid_to,weekdays,sort,active",
        CourseColumns = "offer_id,id,name,sort,active", DishColumns = "offer_id,course_id,id,name,station_id,product_id,sort,active",
        ItemColumns = "i.offer_id,i.course_id,i.product_id,i.presentation_id,p.name,s.name,p.station_id,i.sort,i.active",
        ItemFrom = "FROM core.offer_items i JOIN core.products p ON p.tenant=i.tenant AND p.company=i.company AND p.location=i.location AND p.id=i.product_id " +
            "JOIN core.presentations s ON s.tenant=i.tenant AND s.company=i.company AND s.location=i.location AND s.product_id=i.product_id AND s.id=i.presentation_id";
    public static string KindText(OfferKind kind) => kind switch { OfferKind.Tasting => "tasting", OfferKind.SetMenu => "set-menu", _ => "a-la-carte" };
    public static OfferKind KindOf(string text) => text switch { "tasting" => OfferKind.Tasting, "set-menu" => OfferKind.SetMenu, "a-la-carte" => OfferKind.ALaCarte, _ => throw new ArgumentException("Unknown offer kind.") };
    public static string ServiceText(OfferService service) => service.ToString().ToLowerInvariant();
    public static OfferService ServiceOf(string text) => Enum.Parse<OfferService>(text, ignoreCase: true);
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static DateOnly? Date(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetFieldValue<DateOnly>(i);
    private static object Db(object? value) => value ?? DBNull.Value;
    public static OfferDefinition OfferRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), KindOf(r.GetString(2)), Text(r, 3), ServiceOf(r.GetString(4)), Date(r, 5), Date(r, 6), r.GetInt32(7), r.GetInt32(8), r.GetBoolean(9));
    public static OfferCourseDefinition CourseRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetBoolean(4));
    public static OfferDishDefinition DishRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Text(r, 5), r.GetInt32(6), r.GetBoolean(7));
    public static (string OfferId, string CourseId, OfferItemView Item) ItemRow(NpgsqlDataReader r) => (r.GetString(0), r.GetString(1), new(r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), Text(r, 6), r.GetInt32(7), r.GetBoolean(8)));

    public static Task<List<OfferDefinition>> Offers(this Unit unit) => unit.Rows($"SELECT {OfferColumns} FROM core.offers WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], OfferRow);
    public static async Task<OfferDefinition> Offer(this Unit unit, string id)
        => (await unit.Rows($"SELECT {OfferColumns} FROM core.offers WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], OfferRow)).FirstOrDefault() ?? throw new StoreNotFound();
    public static Task<List<OfferCourseDefinition>> OfferCourses(this Unit unit, string offerId)
        => unit.Rows($"SELECT {CourseColumns} FROM core.offer_courses WHERE {Unit.ScopeWhere} AND offer_id=@offer ORDER BY sort,id", [("offer", offerId)], CourseRow);
    public static async Task<OfferCourseDefinition> OfferCourse(this Unit unit, string offerId, string id)
        => (await unit.Rows($"SELECT {CourseColumns} FROM core.offer_courses WHERE {Unit.ScopeWhere} AND offer_id=@offer AND id=@id", [("offer", offerId), ("id", id)], CourseRow)).FirstOrDefault() ?? throw new StoreNotFound();
    public static Task<List<OfferDishDefinition>> OfferDishes(this Unit unit, string offerId)
        => unit.Rows($"SELECT {DishColumns} FROM core.offer_dishes WHERE {Unit.ScopeWhere} AND offer_id=@offer ORDER BY course_id,sort,id", [("offer", offerId)], DishRow);
    public static async Task<OfferDishDefinition> OfferDish(this Unit unit, string offerId, string courseId, string id)
        => (await unit.Rows($"SELECT {DishColumns} FROM core.offer_dishes WHERE {Unit.ScopeWhere} AND offer_id=@offer AND course_id=@course AND id=@id", [("offer", offerId), ("course", courseId), ("id", id)], DishRow)).FirstOrDefault() ?? throw new StoreNotFound();

    public static Task<List<(string OfferId, string CourseId, OfferItemView Item)>> OfferItems(this Unit unit, string offerId)
        => unit.Rows($"SELECT {ItemColumns} {ItemFrom} WHERE i.tenant=@tenant AND i.company=@company AND i.location=@location AND i.offer_id=@offer ORDER BY i.course_id,i.sort,i.product_id,i.presentation_id", [("offer", offerId)], ItemRow);

    // Oferta con la que se puede ABRIR una mesa ahora: vigente y con al menos un pase activo con un plato activo (o, en una
    // carta, un grupo activo con un item activo). Devuelve sus pases activos con sus platos e items activos, en orden.
    public static async Task<(OfferDefinition Offer, OfferCourseView[] Courses)> AvailableOffer(this Unit unit, string id, DateTime at)
    {
        var offer = await unit.Offer(id);
        if (!Domain.Offers.Available(offer, at)) throw new RuleViolation("offer_unavailable", "This offer is not available now.");
        var dishes = (await unit.OfferDishes(id)).Where(d => d.Active).ToLookup(d => d.CourseId);
        var items = (await unit.OfferItems(id)).Where(i => i.Item.Active).ToLookup(i => i.CourseId, i => i.Item);
        var carte = offer.Kind == OfferKind.ALaCarte;
        var courses = (await unit.OfferCourses(id)).Where(c => c.Active).Select(c => new OfferCourseView(c.Id, c.Name, c.Sort, c.Active, dishes[c.Id].ToArray(), items[c.Id].ToArray()))
            .Where(c => carte ? c.Items!.Length > 0 : c.Dishes.Length > 0).ToArray();
        if (courses.Length == 0) throw new RuleViolation("offer_incomplete", carte ? "This offer has no group with items yet." : "This offer has no course with dishes yet.");
        return (offer, courses);
    }

    private static async Task Exactly(Task<int> work) { if (await work != 1) throw new StoreNotFound(); }
    private static async Task Insert(Unit unit, string sql, (string, object)[] values, string code)
    {
        try { await unit.Sql(sql, values); }
        catch (PostgresException e) when (e.SqlState == "23505") { throw new StoreConflict("duplicate_code", $"Code {code} already exists."); }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    private static (string, object)[] OfferValues(OfferDefinition o) => [("id", o.Id), ("name", o.Name), ("kind", KindText(o.Kind)), ("product", Db(o.ProductId)), ("service", ServiceText(o.Service)),
        ("from", Db(o.ValidFrom)), ("to", Db(o.ValidTo)), ("weekdays", o.Weekdays), ("sort", o.Sort), ("active", o.Active)];
    public static Task InsertOffer(this Unit unit, OfferDefinition o) => Insert(unit,
        "INSERT INTO core.offers (tenant,company,location,id,name,kind,product_id,service,valid_from,valid_to,weekdays,sort,active) VALUES (@tenant,@company,@location,@id,@name,@kind,@product,@service,@from,@to,@weekdays,@sort,@active)", OfferValues(o), o.Id);
    public static async Task UpdateOffer(this Unit unit, OfferDefinition o)
    {
        try { await Exactly(unit.Sql($"UPDATE core.offers SET name=@name,kind=@kind,product_id=@product,service=@service,valid_from=@from,valid_to=@to,weekdays=@weekdays,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id", OfferValues(o))); }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    public static Task InsertOfferCourse(this Unit unit, OfferCourseDefinition c) => Insert(unit,
        "INSERT INTO core.offer_courses (tenant,company,location,offer_id,id,name,sort,active) VALUES (@tenant,@company,@location,@offer,@id,@name,@sort,@active)",
        [("offer", c.OfferId), ("id", c.Id), ("name", c.Name), ("sort", c.Sort), ("active", c.Active)], c.OfferId + "/" + c.Id);
    public static Task UpdateOfferCourse(this Unit unit, OfferCourseDefinition c) => Exactly(unit.Sql(
        $"UPDATE core.offer_courses SET name=@name,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND offer_id=@offer AND id=@id",
        [("offer", c.OfferId), ("id", c.Id), ("name", c.Name), ("sort", c.Sort), ("active", c.Active)]));
    private static (string, object)[] DishValues(OfferDishDefinition d) => [("offer", d.OfferId), ("course", d.CourseId), ("id", d.Id), ("name", d.Name), ("station", d.StationId), ("product", Db(d.ProductId)), ("sort", d.Sort), ("active", d.Active)];
    public static Task InsertOfferDish(this Unit unit, OfferDishDefinition d) => Insert(unit,
        "INSERT INTO core.offer_dishes (tenant,company,location,offer_id,course_id,id,name,station_id,product_id,sort,active) VALUES (@tenant,@company,@location,@offer,@course,@id,@name,@station,@product,@sort,@active)", DishValues(d), d.OfferId + "/" + d.CourseId + "/" + d.Id);
    public static async Task UpdateOfferDish(this Unit unit, OfferDishDefinition d)
    {
        try { await Exactly(unit.Sql($"UPDATE core.offer_dishes SET name=@name,station_id=@station,product_id=@product,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND offer_id=@offer AND course_id=@course AND id=@id", DishValues(d))); }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    private static (string, object)[] ItemValues(OfferItemDefinition i) => [("offer", i.OfferId), ("course", i.CourseId), ("product", i.ProductId), ("presentation", i.PresentationId), ("sort", i.Sort), ("active", i.Active)];
    public static Task InsertOfferItem(this Unit unit, OfferItemDefinition i) => Insert(unit,
        "INSERT INTO core.offer_items (tenant,company,location,offer_id,course_id,product_id,presentation_id,sort,active) VALUES (@tenant,@company,@location,@offer,@course,@product,@presentation,@sort,@active)", ItemValues(i), i.ProductId + "/" + i.PresentationId);
    public static Task UpdateOfferItem(this Unit unit, OfferItemDefinition i) => Exactly(unit.Sql(
        $"UPDATE core.offer_items SET sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND offer_id=@offer AND course_id=@course AND product_id=@product AND presentation_id=@presentation", ItemValues(i)));
    public static Task<int> SeedOfferItem(this Unit unit, OfferItemDefinition i) => unit.Sql(
        "INSERT INTO core.offer_items (tenant,company,location,offer_id,course_id,product_id,presentation_id,sort,active) VALUES (@tenant,@company,@location,@offer,@course,@product,@presentation,@sort,@active) ON CONFLICT DO NOTHING", ItemValues(i));
    // Siembra idempotente (fixtures): no pisa lo que ya exista.
    public static Task<int> SeedOffer(this Unit unit, OfferDefinition o) => unit.Sql(
        "INSERT INTO core.offers (tenant,company,location,id,name,kind,product_id,service,valid_from,valid_to,weekdays,sort,active) VALUES (@tenant,@company,@location,@id,@name,@kind,@product,@service,@from,@to,@weekdays,@sort,@active) ON CONFLICT DO NOTHING", OfferValues(o));
    public static Task<int> SeedOfferCourse(this Unit unit, OfferCourseDefinition c) => unit.Sql(
        "INSERT INTO core.offer_courses (tenant,company,location,offer_id,id,name,sort,active) VALUES (@tenant,@company,@location,@offer,@id,@name,@sort,@active) ON CONFLICT DO NOTHING",
        [("offer", c.OfferId), ("id", c.Id), ("name", c.Name), ("sort", c.Sort), ("active", c.Active)]);
    public static Task<int> SeedOfferDish(this Unit unit, OfferDishDefinition d) => unit.Sql(
        "INSERT INTO core.offer_dishes (tenant,company,location,offer_id,course_id,id,name,station_id,product_id,sort,active) VALUES (@tenant,@company,@location,@offer,@course,@id,@name,@station,@product,@sort,@active) ON CONFLICT DO NOTHING", DishValues(d));
}

// Lecturas de la oferta: completa para el puesto principal (edicion) y VIGENTE para la configuracion operativa de los clientes.
public sealed record AvailableOfferView(string Id, string Name, string Kind, OfferCourseChoice[] Courses);
public sealed record OfferCourseChoice(string Id, string Name, OfferDishChoice[] Dishes, OfferItemChoice[]? Items = null);
public sealed record OfferItemChoice(string ProductId, string PresentationId, string Name, string Presentation);
public sealed record OfferDishChoice(string Id, string Name);

public static class OfferReads
{
    private const string Scope = "tenant=@tenant AND company=@company AND location=@location";
    public static async Task<OfferView[]> Offers(this DesktopReadRepository reads, BusinessScope scope, CancellationToken ct)
    {
        var offers = await reads.Query(scope, $"SELECT {OfferUnit.OfferColumns} FROM core.offers WHERE {Scope} ORDER BY sort,id", OfferUnit.OfferRow, ct);
        var courses = await reads.Query(scope, $"SELECT {OfferUnit.CourseColumns} FROM core.offer_courses WHERE {Scope} ORDER BY offer_id,sort,id", OfferUnit.CourseRow, ct);
        var dishes = (await reads.Query(scope, $"SELECT {OfferUnit.DishColumns} FROM core.offer_dishes WHERE {Scope} ORDER BY offer_id,course_id,sort,id", OfferUnit.DishRow, ct)).ToLookup(d => (d.OfferId, d.CourseId));
        var items = (await reads.Query(scope, $"SELECT {OfferUnit.ItemColumns} {OfferUnit.ItemFrom} WHERE i.tenant=@tenant AND i.company=@company AND i.location=@location ORDER BY i.offer_id,i.course_id,i.sort,i.product_id,i.presentation_id", OfferUnit.ItemRow, ct))
            .ToLookup(i => (i.OfferId, i.CourseId), i => i.Item);
        return offers.Select(o => new OfferView(o.Id, o.Name, OfferUnit.KindText(o.Kind), o.ProductId, OfferUnit.ServiceText(o.Service), o.ValidFrom?.ToString("yyyy-MM-dd"), o.ValidTo?.ToString("yyyy-MM-dd"), o.Weekdays, o.Sort, o.Active,
            courses.Where(c => c.OfferId == o.Id).Select(c => new OfferCourseView(c.Id, c.Name, c.Sort, c.Active, dishes[(o.Id, c.Id)].ToArray(), items[(o.Id, c.Id)].ToArray())).ToArray())).ToArray();
    }

    // Ofertas con las que se puede abrir una mesa AHORA, con sus pases y platos activos (sin dinero: es lo que ven sala y cocina).
    public static async Task<AvailableOfferView[]> AvailableOffers(this DesktopReadRepository reads, BusinessScope scope, DateTime at, CancellationToken ct)
        => (await reads.Offers(scope, ct)).Where(o => Domain.Offers.Available(new(o.Id, o.Name, OfferUnit.KindOf(o.Kind), o.ProductId, OfferUnit.ServiceOf(o.Service),
                o.ValidFrom is null ? null : DateOnly.Parse(o.ValidFrom), o.ValidTo is null ? null : DateOnly.Parse(o.ValidTo), o.Weekdays, o.Sort, o.Active), at))
            .Select(o => new AvailableOfferView(o.Id, o.Name, o.Kind, o.Courses.Where(c => c.Active).Select(c => new OfferCourseChoice(c.Id, c.Name, c.Dishes.Where(d => d.Active).Select(d => new OfferDishChoice(d.Id, d.Name)).ToArray(),
                    (c.Items ?? []).Where(i => i.Active).Select(i => new OfferItemChoice(i.ProductId, i.PresentationId, i.Name, i.Presentation)).ToArray()))
                .Where(c => o.Kind == "a-la-carte" ? c.Items!.Length > 0 : c.Dishes.Length > 0).ToArray()))
            .Where(o => o.Courses.Length > 0).ToArray();
}
