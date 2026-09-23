using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E3: acceso al CATALOGO Y TARIFAS (core.taxes, categories, products, presentations, tariffs, prices) sobre la Unit del
// nucleo: misma transaccion, mismas guardas de ambito. Los modulos venden por aqui (Sellable + PriceFor), nunca por SQL propio.
public static class CatalogUnit
{
    public const string TaxColumns = "id,name,rate,active", CategoryColumns = "id,name,parent_id,color,sort,active",
        ProductColumns = "id,name,category_id,tax_id,reference,sort,active", PresentationColumns = "product_id,id,name,sort,active",
        TariffColumns = "id,name,sort,active", PriceColumns = "tariff_id,product_id,presentation_id,valid_from,price_cents";
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    public static TaxDefinition TaxRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetBoolean(3));
    public static CategoryDefinition CategoryRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), Text(r, 2), Text(r, 3), r.GetInt32(4), r.GetBoolean(5));
    public static ProductDefinition ProductRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), Text(r, 2), r.GetString(3), Text(r, 4), r.GetInt32(5), r.GetBoolean(6));
    public static PresentationDefinition PresentationRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetBoolean(4));
    public static TariffDefinition TariffRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetBoolean(3));
    public static PriceDefinition PriceRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetFieldValue<DateOnly>(3), r.GetInt64(4));

    public static Task<List<TaxDefinition>> Taxes(this Unit unit) => unit.Rows($"SELECT {TaxColumns} FROM core.taxes WHERE {Unit.ScopeWhere} ORDER BY rate DESC,id", [], TaxRow);
    public static Task<List<CategoryDefinition>> Categories(this Unit unit) => unit.Rows($"SELECT {CategoryColumns} FROM core.categories WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], CategoryRow);
    public static Task<List<ProductDefinition>> Products(this Unit unit) => unit.Rows($"SELECT {ProductColumns} FROM core.products WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], ProductRow);
    public static Task<List<PresentationDefinition>> Presentations(this Unit unit, string productId)
        => unit.Rows($"SELECT {PresentationColumns} FROM core.presentations WHERE {Unit.ScopeWhere} AND product_id=@product ORDER BY sort,id", [("product", productId)], PresentationRow);
    public static Task<List<TariffDefinition>> Tariffs(this Unit unit) => unit.Rows($"SELECT {TariffColumns} FROM core.tariffs WHERE {Unit.ScopeWhere} ORDER BY sort,id", [], TariffRow);
    public static Task<List<PriceDefinition>> Prices(this Unit unit, string productId, string presentationId)
        => unit.Rows($"SELECT {PriceColumns} FROM core.prices WHERE {Unit.ScopeWhere} AND product_id=@product AND presentation_id=@presentation ORDER BY tariff_id,valid_from",
            [("product", productId), ("presentation", presentationId)], PriceRow);

    private static async Task<T> One<T>(Task<List<T>> rows) => (await rows).FirstOrDefault() ?? throw new StoreNotFound();
    public static Task<TaxDefinition> Tax(this Unit unit, string id) => One(unit.Rows($"SELECT {TaxColumns} FROM core.taxes WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], TaxRow));
    public static Task<CategoryDefinition> Category(this Unit unit, string id) => One(unit.Rows($"SELECT {CategoryColumns} FROM core.categories WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], CategoryRow));
    public static Task<ProductDefinition> Product(this Unit unit, string id) => One(unit.Rows($"SELECT {ProductColumns} FROM core.products WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], ProductRow));
    public static Task<PresentationDefinition> Presentation(this Unit unit, string productId, string id)
        => One(unit.Rows($"SELECT {PresentationColumns} FROM core.presentations WHERE {Unit.ScopeWhere} AND product_id=@product AND id=@id", [("product", productId), ("id", id)], PresentationRow));
    public static Task<TariffDefinition> Tariff(this Unit unit, string id) => One(unit.Rows($"SELECT {TariffColumns} FROM core.tariffs WHERE {Unit.ScopeWhere} AND id=@id", [("id", id)], TariffRow));

    // Lo VENDIBLE: producto activo y una presentacion activa. Sin presentacion indicada vale la unica activa; con varias hay que elegir.
    public static async Task<(ProductDefinition Product, PresentationDefinition Presentation)> Sellable(this Unit unit, string productId, string? presentationId)
    {
        var product = await unit.Product(productId);
        if (!product.Active) throw new RuleViolation("product_unavailable", "Product unavailable.");
        var presentations = (await unit.Presentations(productId)).Where(p => p.Active).ToList();
        PresentationDefinition presentation;
        if (string.IsNullOrWhiteSpace(presentationId))
        {
            if (presentations.Count != 1) throw new ArgumentException(presentations.Count == 0 ? "Product has no active presentation." : "presentationId is required: this product has several presentations.");
            presentation = presentations[0];
        }
        else presentation = presentations.FirstOrDefault(p => p.Id == presentationId) ?? throw new RuleViolation("product_unavailable", "Presentation unavailable.");
        return (product, presentation);
    }

    // Precio vigente en una tarifa (fila de mayor valid_from no posterior a hoy), con la tarifa general como respaldo. Sin precio no se vende.
    public static async Task<(long Cents, string TariffId)> PriceFor(this Unit unit, string tariffId, string productId, string presentationId, DateOnly on)
    {
        foreach (var tariff in tariffId == Catalog.GeneralTariff ? [tariffId] : new[] { tariffId, Catalog.GeneralTariff })
        {
            var rows = await unit.Rows("SELECT price_cents FROM core.prices WHERE " + Unit.ScopeWhere + " AND tariff_id=@tariff AND product_id=@product AND presentation_id=@presentation AND valid_from<=@on ORDER BY valid_from DESC LIMIT 1",
                [("tariff", tariff), ("product", productId), ("presentation", presentationId), ("on", on)], r => r.GetInt64(0));
            if (rows.Count == 1) return (rows[0], tariff);
        }
        throw new RuleViolation("price_missing", "This product has no price in force: set one in the catalog first.");
    }

    // Tarifa de una cuenta: la de la sala de su mesa (core.zones.tariff_id) o la general. La cuenta guarda la mesa de origen (E1b).
    public static async Task<string> TariffForService(this Unit unit, string serviceId)
    {
        var rows = await unit.Rows("SELECT coalesce(z.tariff_id,'general') FROM core.accounts a LEFT JOIN core.tables t ON t.tenant=a.tenant AND t.company=a.company AND t.location=a.location AND t.id=a.table_id " +
            "LEFT JOIN core.zones z ON z.tenant=t.tenant AND z.company=t.company AND z.location=t.location AND z.id=t.zone_id AND z.active " +
            "WHERE a.tenant=@tenant AND a.company=@company AND a.location=@location AND a.service_id=@service",
            [("service", serviceId)], r => r.GetString(0));
        return rows.Count == 1 ? rows[0] : Catalog.GeneralTariff;
    }

    private static async Task Exactly(Task<int> work) { if (await work != 1) throw new StoreNotFound(); }
    private static async Task Insert(Unit unit, string sql, (string, object)[] values, string code)
    {
        try { await unit.Sql(sql, values); }
        catch (PostgresException e) when (e.SqlState == "23505") { throw new StoreConflict("duplicate_code", $"Code {code} already exists."); }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    private static object Db(string? value) => (object?)value ?? DBNull.Value;

    public static Task InsertTax(this Unit unit, TaxDefinition t) => Insert(unit,
        "INSERT INTO core.taxes (tenant,company,location,id,name,rate,active) VALUES (@tenant,@company,@location,@id,@name,@rate,@active)",
        [("id", t.Id), ("name", t.Name), ("rate", t.Rate), ("active", t.Active)], t.Id);
    public static Task UpdateTax(this Unit unit, TaxDefinition t) => Exactly(unit.Sql(
        $"UPDATE core.taxes SET name=@name,rate=@rate,active=@active WHERE {Unit.ScopeWhere} AND id=@id", [("id", t.Id), ("name", t.Name), ("rate", t.Rate), ("active", t.Active)]));
    public static Task InsertCategory(this Unit unit, CategoryDefinition c) => Insert(unit,
        "INSERT INTO core.categories (tenant,company,location,id,name,parent_id,color,sort,active) VALUES (@tenant,@company,@location,@id,@name,@parent,@color,@sort,@active)",
        [("id", c.Id), ("name", c.Name), ("parent", Db(c.ParentId)), ("color", Db(c.Color)), ("sort", c.Sort), ("active", c.Active)], c.Id);
    public static async Task UpdateCategory(this Unit unit, CategoryDefinition c)
    {
        try
        {
            await Exactly(unit.Sql($"UPDATE core.categories SET name=@name,parent_id=@parent,color=@color,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id",
                [("id", c.Id), ("name", c.Name), ("parent", Db(c.ParentId)), ("color", Db(c.Color)), ("sort", c.Sort), ("active", c.Active)]));
        }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    public static Task InsertProduct(this Unit unit, ProductDefinition p) => Insert(unit,
        "INSERT INTO core.products (tenant,company,location,id,name,category_id,tax_id,reference,sort,active) VALUES (@tenant,@company,@location,@id,@name,@category,@tax,@reference,@sort,@active)",
        [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active)], p.Id);
    public static async Task UpdateProduct(this Unit unit, ProductDefinition p)
    {
        try
        {
            await Exactly(unit.Sql($"UPDATE core.products SET name=@name,category_id=@category,tax_id=@tax,reference=@reference,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id",
                [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active)]));
        }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    public static Task InsertPresentation(this Unit unit, PresentationDefinition p) => Insert(unit,
        "INSERT INTO core.presentations (tenant,company,location,product_id,id,name,sort,active) VALUES (@tenant,@company,@location,@product,@id,@name,@sort,@active)",
        [("product", p.ProductId), ("id", p.Id), ("name", p.Name), ("sort", p.Sort), ("active", p.Active)], p.ProductId + "/" + p.Id);
    public static Task UpdatePresentation(this Unit unit, PresentationDefinition p) => Exactly(unit.Sql(
        $"UPDATE core.presentations SET name=@name,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND product_id=@product AND id=@id",
        [("product", p.ProductId), ("id", p.Id), ("name", p.Name), ("sort", p.Sort), ("active", p.Active)]));
    public static Task InsertTariff(this Unit unit, TariffDefinition t) => Insert(unit,
        "INSERT INTO core.tariffs (tenant,company,location,id,name,sort,active) VALUES (@tenant,@company,@location,@id,@name,@sort,@active)",
        [("id", t.Id), ("name", t.Name), ("sort", t.Sort), ("active", t.Active)], t.Id);
    public static Task UpdateTariff(this Unit unit, TariffDefinition t) => Exactly(unit.Sql(
        $"UPDATE core.tariffs SET name=@name,sort=@sort,active=@active WHERE {Unit.ScopeWhere} AND id=@id", [("id", t.Id), ("name", t.Name), ("sort", t.Sort), ("active", t.Active)]));
    // Un precio por (tarifa, presentacion, fecha de vigencia): fijarlo de nuevo sobre la misma fecha lo corrige. Devuelve si cambio algo.
    public static async Task<bool> SetPrice(this Unit unit, PriceDefinition p)
    {
        try
        {
            var count = await unit.Sql("INSERT INTO core.prices (tenant,company,location,tariff_id,product_id,presentation_id,valid_from,price_cents) VALUES (@tenant,@company,@location,@tariff,@product,@presentation,@from,@cents) " +
                "ON CONFLICT (tenant,company,location,tariff_id,product_id,presentation_id,valid_from) DO UPDATE SET price_cents=EXCLUDED.price_cents WHERE core.prices.price_cents<>EXCLUDED.price_cents",
                [("tariff", p.TariffId), ("product", p.ProductId), ("presentation", p.PresentationId), ("from", p.ValidFrom), ("cents", p.PriceCents)]);
            return count == 1;
        }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    // Siembra idempotente (fixtures): no pisa lo que ya exista.
    public static Task<int> SeedCategory(this Unit unit, CategoryDefinition c) => unit.Sql(
        "INSERT INTO core.categories (tenant,company,location,id,name,parent_id,color,sort,active) VALUES (@tenant,@company,@location,@id,@name,@parent,@color,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", c.Id), ("name", c.Name), ("parent", Db(c.ParentId)), ("color", Db(c.Color)), ("sort", c.Sort), ("active", c.Active)]);
    public static Task<int> SeedProduct(this Unit unit, ProductDefinition p) => unit.Sql(
        "INSERT INTO core.products (tenant,company,location,id,name,category_id,tax_id,reference,sort,active) VALUES (@tenant,@company,@location,@id,@name,@category,@tax,@reference,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active)]);
    public static Task<int> SeedPresentation(this Unit unit, PresentationDefinition p) => unit.Sql(
        "INSERT INTO core.presentations (tenant,company,location,product_id,id,name,sort,active) VALUES (@tenant,@company,@location,@product,@id,@name,@sort,@active) ON CONFLICT DO NOTHING",
        [("product", p.ProductId), ("id", p.Id), ("name", p.Name), ("sort", p.Sort), ("active", p.Active)]);
    public static Task<int> SeedPrice(this Unit unit, PriceDefinition p) => unit.Sql(
        "INSERT INTO core.prices (tenant,company,location,tariff_id,product_id,presentation_id,valid_from,price_cents) VALUES (@tenant,@company,@location,@tariff,@product,@presentation,@from,@cents) ON CONFLICT DO NOTHING",
        [("tariff", p.TariffId), ("product", p.ProductId), ("presentation", p.PresentationId), ("from", p.ValidFrom), ("cents", p.PriceCents)]);
}

// Lecturas del catalogo para el puesto principal (edicion) y para los catalogos operativos de los clientes.
public sealed record PriceView(string TariffId, string ValidFrom, long PriceCents);
public sealed record PresentationView(string Id, string Name, int Sort, bool Active, PriceView[] Prices);
public sealed record ProductView(string Id, string Name, string? CategoryId, string TaxId, string? Reference, int Sort, bool Active, PresentationView[] Presentations);
public sealed record CatalogView(TaxDefinition[] Taxes, CategoryDefinition[] Categories, ProductView[] Products, TariffDefinition[] Tariffs, string Today);
// Vendible tal como lo ven los clientes: sala sin dinero (SellableView); caja con el precio de la tarifa general (PricedSellableView).
public sealed record SellableView(string Id, string PresentationId, string Name, string Presentation, string? CategoryId, string? CategoryName);
public sealed record PricedSellableView(string Id, string PresentationId, string Name, string Presentation, string? CategoryId, string? CategoryName, long PriceCents, string TariffId);

public static class CatalogReads
{
    private const string Scope = "tenant=@tenant AND company=@company AND location=@location";
    public static async Task<CatalogView> FullCatalog(this DesktopReadRepository reads, BusinessScope scope, DateOnly today, CancellationToken ct)
    {
        var taxes = await reads.Query(scope, $"SELECT {CatalogUnit.TaxColumns} FROM core.taxes WHERE {Scope} ORDER BY rate DESC,id", CatalogUnit.TaxRow, ct);
        var categories = await reads.Query(scope, $"SELECT {CatalogUnit.CategoryColumns} FROM core.categories WHERE {Scope} ORDER BY sort,id", CatalogUnit.CategoryRow, ct);
        var products = await reads.Query(scope, $"SELECT {CatalogUnit.ProductColumns} FROM core.products WHERE {Scope} ORDER BY sort,id", CatalogUnit.ProductRow, ct);
        var presentations = await reads.Query(scope, $"SELECT {CatalogUnit.PresentationColumns} FROM core.presentations WHERE {Scope} ORDER BY product_id,sort,id", CatalogUnit.PresentationRow, ct);
        var prices = await reads.Query(scope, $"SELECT {CatalogUnit.PriceColumns} FROM core.prices WHERE {Scope} ORDER BY tariff_id,valid_from", CatalogUnit.PriceRow, ct);
        var tariffs = await reads.Query(scope, $"SELECT {CatalogUnit.TariffColumns} FROM core.tariffs WHERE {Scope} ORDER BY sort,id", CatalogUnit.TariffRow, ct);
        var byProduct = presentations.ToLookup(p => p.ProductId);
        var byPresentation = prices.ToLookup(p => (p.ProductId, p.PresentationId));
        return new(taxes.ToArray(), categories.ToArray(), products.Select(p => new ProductView(p.Id, p.Name, p.CategoryId, p.TaxId, p.Reference, p.Sort, p.Active,
            byProduct[p.Id].Select(s => new PresentationView(s.Id, s.Name, s.Sort, s.Active,
                byPresentation[(p.Id, s.Id)].Select(x => new PriceView(x.TariffId, x.ValidFrom.ToString("yyyy-MM-dd"), x.PriceCents)).ToArray())).ToArray())).ToArray(),
            tariffs.ToArray(), today.ToString("yyyy-MM-dd"));
    }

    // Vendibles con precio vigente en la tarifa GENERAL hoy: producto y presentacion activos, con su categoria. Orden estable.
    private const string SellableSql =
        "SELECT p.id,s.id,p.name,s.name,c.id,c.name,x.price_cents FROM core.products p " +
        "JOIN core.presentations s ON s.tenant=p.tenant AND s.company=p.company AND s.location=p.location AND s.product_id=p.id AND s.active " +
        "LEFT JOIN core.categories c ON c.tenant=p.tenant AND c.company=p.company AND c.location=p.location AND c.id=p.category_id " +
        "JOIN LATERAL (SELECT price_cents FROM core.prices x WHERE x.tenant=p.tenant AND x.company=p.company AND x.location=p.location AND x.tariff_id='general' " +
        " AND x.product_id=p.id AND x.presentation_id=s.id AND x.valid_from<=@today ORDER BY x.valid_from DESC LIMIT 1) x ON true " +
        // E4a: los productos-menu (categoria 'menus') se venden abriendo la mesa con su oferta, no como consumo suelto.
        "WHERE p.tenant=@tenant AND p.company=@company AND p.location=@location AND p.active AND coalesce(p.category_id,'')<>'menus' ORDER BY c.sort,c.id,p.sort,p.id,s.sort,s.id LIMIT 5000";
    public static Task<List<SellableView>> Sellables(this DesktopReadRepository reads, BusinessScope scope, DateOnly today, CancellationToken ct)
        => reads.Query(scope, SellableSql, r => new SellableView(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)), ct, ("today", today));
    public static Task<List<PricedSellableView>> PricedSellables(this DesktopReadRepository reads, BusinessScope scope, DateOnly today, CancellationToken ct)
        => reads.Query(scope, SellableSql, r => new PricedSellableView(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetInt64(6), Domain.Catalog.GeneralTariff), ct, ("today", today));
}
