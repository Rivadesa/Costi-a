using Costina.Core.Domain;
using Npgsql;

namespace Costina.Core.Persistence;

// E3: acceso al CATALOGO Y TARIFAS (core.taxes, categories, products, presentations, tariffs, prices) sobre la Unit del
// nucleo: misma transaccion, mismas guardas de ambito. Los modulos venden por aqui (Sellable + PriceFor), nunca por SQL propio.
public static class CatalogUnit
{
    public const string TaxColumns = "id,name,rate,active", CategoryColumns = "id,name,parent_id,color,sort,active",
        ProductColumns = "id,name,category_id,tax_id,reference,sort,active,station_id", PresentationColumns = "product_id,id,name,sort,active",
        TariffColumns = "id,name,sort,active", PriceColumns = "tariff_id,product_id,presentation_id,valid_from,price_cents";
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    public static TaxDefinition TaxRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetBoolean(3));
    public static CategoryDefinition CategoryRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), Text(r, 2), Text(r, 3), r.GetInt32(4), r.GetBoolean(5));
    public static ProductDefinition ProductRow(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), Text(r, 2), r.GetString(3), Text(r, 4), r.GetInt32(5), r.GetBoolean(6), Text(r, 7));
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
        "INSERT INTO core.products (tenant,company,location,id,name,category_id,tax_id,reference,sort,active,station_id) VALUES (@tenant,@company,@location,@id,@name,@category,@tax,@reference,@sort,@active,@station)",
        [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active), ("station", Db(p.StationId))], p.Id);
    public static async Task UpdateProduct(this Unit unit, ProductDefinition p)
    {
        try
        {
            await Exactly(unit.Sql($"UPDATE core.products SET name=@name,category_id=@category,tax_id=@tax,reference=@reference,sort=@sort,active=@active,station_id=@station WHERE {Unit.ScopeWhere} AND id=@id",
                [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active), ("station", Db(p.StationId))]));
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
    // E3.2: imagen vigente de un producto. ImageOf da sus datos SIN los bytes (null si nunca tuvo); la lectura de los bytes es
    // CatalogReads.ActiveImage. SetImage inserta o sustituye (y reactiva) y devuelve si cambio algo: la misma imagen ya vigente
    // no se reescribe. RemoveImage la marca inactiva: nada se borra.
    public static async Task<ImageInfo?> ImageOf(this Unit unit, string productId)
        => (await unit.Rows($"SELECT product_id,content_type,sha256,bytes,active FROM core.product_images WHERE {Unit.ScopeWhere} AND product_id=@product",
            [("product", productId)], r => new ImageInfo(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetBoolean(4)))).FirstOrDefault();
    public static async Task<bool> SetImage(this Unit unit, ProductImage image)
    {
        try
        {
            var count = await unit.Sql("INSERT INTO core.product_images (tenant,company,location,product_id,content,content_type,sha256,bytes,active,updated_at,updated_by) " +
                "VALUES (@tenant,@company,@location,@product,@content,@type,@sha,@bytes,true,now(),@actor) " +
                "ON CONFLICT (tenant,company,location,product_id) DO UPDATE SET content=EXCLUDED.content,content_type=EXCLUDED.content_type,sha256=EXCLUDED.sha256," +
                "bytes=EXCLUDED.bytes,active=true,updated_at=EXCLUDED.updated_at,updated_by=EXCLUDED.updated_by " +
                "WHERE NOT (core.product_images.active AND core.product_images.sha256=EXCLUDED.sha256)",
                [("product", image.ProductId), ("content", image.Content), ("type", image.ContentType), ("sha", image.Sha256), ("bytes", image.Bytes), ("actor", unit.Actor)]);
            return count == 1;
        }
        catch (PostgresException e) when (e.SqlState == "23503") { throw new StoreNotFound(); }
    }
    public static async Task<bool> RemoveImage(this Unit unit, string productId)
        => await unit.Sql($"UPDATE core.product_images SET active=false,updated_at=now(),updated_by=@actor WHERE {Unit.ScopeWhere} AND product_id=@product AND active",
            [("product", productId), ("actor", unit.Actor)]) == 1;
    // Siembra idempotente (fixtures): no pisa lo que ya exista.
    public static Task<int> SeedCategory(this Unit unit, CategoryDefinition c) => unit.Sql(
        "INSERT INTO core.categories (tenant,company,location,id,name,parent_id,color,sort,active) VALUES (@tenant,@company,@location,@id,@name,@parent,@color,@sort,@active) ON CONFLICT DO NOTHING",
        [("id", c.Id), ("name", c.Name), ("parent", Db(c.ParentId)), ("color", Db(c.Color)), ("sort", c.Sort), ("active", c.Active)]);
    public static Task<int> SeedProduct(this Unit unit, ProductDefinition p) => unit.Sql(
        "INSERT INTO core.products (tenant,company,location,id,name,category_id,tax_id,reference,sort,active,station_id) VALUES (@tenant,@company,@location,@id,@name,@category,@tax,@reference,@sort,@active,@station) ON CONFLICT DO NOTHING",
        [("id", p.Id), ("name", p.Name), ("category", Db(p.CategoryId)), ("tax", p.TaxId), ("reference", Db(p.Reference)), ("sort", p.Sort), ("active", p.Active), ("station", Db(p.StationId))]);
    public static Task<int> SeedPresentation(this Unit unit, PresentationDefinition p) => unit.Sql(
        "INSERT INTO core.presentations (tenant,company,location,product_id,id,name,sort,active) VALUES (@tenant,@company,@location,@product,@id,@name,@sort,@active) ON CONFLICT DO NOTHING",
        [("product", p.ProductId), ("id", p.Id), ("name", p.Name), ("sort", p.Sort), ("active", p.Active)]);
    public static Task<int> SeedPrice(this Unit unit, PriceDefinition p) => unit.Sql(
        "INSERT INTO core.prices (tenant,company,location,tariff_id,product_id,presentation_id,valid_from,price_cents) VALUES (@tenant,@company,@location,@tariff,@product,@presentation,@from,@cents) ON CONFLICT DO NOTHING",
        [("tariff", p.TariffId), ("product", p.ProductId), ("presentation", p.PresentationId), ("from", p.ValidFrom), ("cents", p.PriceCents)]);
}

// Lecturas del catalogo para el puesto principal (edicion) y para los catalogos operativos de los clientes.
public sealed record PriceView(string TariffId, string ValidFrom, long PriceCents);
// E3.3: Glass = es la copa (lo decide el servidor: presentacion 'copa' o la 'glass' de ensayo).
public sealed record PresentationView(string Id, string Name, int Sort, bool Active, PriceView[] Prices, bool Glass = false);
// E3.2: ImageSha = huella de la imagen VIGENTE del producto (null sin imagen o quitada); campo anadido al final: solo anade.
// E3.3: Section ('cocina' | 'bodega' | null para un producto-menu), Family (la familia bajo la seccion) y CategoryPath (de la raiz a
// la hoja, sin la seccion): campos anadidos al final, sin dinero; un cliente anterior los ignora.
public sealed record ProductView(string Id, string Name, string? CategoryId, string TaxId, string? Reference, int Sort, bool Active, PresentationView[] Presentations, string? StationId = null, string? ImageSha = null,
    string? Section = null, string? Family = null, string[]? CategoryPath = null);
public sealed record CatalogView(TaxDefinition[] Taxes, CategoryDefinition[] Categories, ProductView[] Products, TariffDefinition[] Tariffs, string Today);
// Vendible tal como lo ven los clientes: sala sin dinero (SellableView); caja con el precio de la tarifa general (PricedSellableView).
// E3.3: Section, Family, CategoryPath, Reference y Glass al final, sin vocabulario de dinero (la guarda de la PWA los acepta).
public sealed record SellableView(string Id, string PresentationId, string Name, string Presentation, string? CategoryId, string? CategoryName, string? ImageSha = null,
    string? Section = null, string? Family = null, string[]? CategoryPath = null, string? Reference = null, bool Glass = false);
public sealed record PricedSellableView(string Id, string PresentationId, string Name, string Presentation, string? CategoryId, string? CategoryName, long PriceCents, string TariffId, string? ImageSha = null,
    string? Section = null, string? Family = null, string[]? CategoryPath = null, string? Reference = null, bool Glass = false);
// E3.2: datos de la imagen de un producto sin sus bytes (ordenes, importacion) y la imagen vigente con sus bytes (lectura).
public sealed record ImageInfo(string ProductId, string ContentType, string Sha256, int Bytes, bool Active);
public sealed record StoredImage(byte[] Content, string ContentType, string Sha256);

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
        // E3.2: solo la huella de las imagenes vigentes; los bytes se piden aparte (GET /catalog/images/{id}) y el cliente los guarda por huella.
        var images = (await reads.Query(scope, $"SELECT product_id,sha256 FROM core.product_images WHERE {Scope} AND active", r => (Product: r.GetString(0), Sha: r.GetString(1)), ct))
            .ToDictionary(i => i.Product, i => i.Sha, StringComparer.Ordinal);
        var byProduct = presentations.ToLookup(p => p.ProductId);
        var byPresentation = prices.ToLookup(p => (p.ProductId, p.PresentationId));
        var index = new SectionIndex(categories, await StationKinds(reads, scope, ct));
        return new(taxes.ToArray(), categories.ToArray(), products.Select(p => new ProductView(p.Id, p.Name, p.CategoryId, p.TaxId, p.Reference, p.Sort, p.Active,
            byProduct[p.Id].Select(s => new PresentationView(s.Id, s.Name, s.Sort, s.Active,
                byPresentation[(p.Id, s.Id)].Select(x => new PriceView(x.TariffId, x.ValidFrom.ToString("yyyy-MM-dd"), x.PriceCents)).ToArray(), Domain.Catalog.IsGlass(s.Id))).ToArray(), p.StationId,
                images.GetValueOrDefault(p.Id), index.Section(p.CategoryId, p.StationId), index.Family(p.CategoryId), index.Path(p.CategoryId))).ToArray(),
            tariffs.ToArray(), today.ToString("yyyy-MM-dd"));
    }

    // E3.3: seccion, familia y ruta de cada producto, calculadas al leer con las categorias y los tipos de estacion del ambito.
    private static async Task<Dictionary<string, string>> StationKinds(DesktopReadRepository reads, BusinessScope scope, CancellationToken ct)
        => (await reads.Query(scope, $"SELECT id,kind FROM core.stations WHERE {Scope}", r => (Id: r.GetString(0), Kind: r.GetString(1)), ct)).ToDictionary(s => s.Id, s => s.Kind, StringComparer.Ordinal);
    private sealed class SectionIndex(IReadOnlyList<CategoryDefinition> categories, IReadOnlyDictionary<string, string> stationKinds)
    {
        private readonly Dictionary<string, CategoryDefinition> byId = categories.ToDictionary(c => c.Id, StringComparer.Ordinal);
        private string? ParentOf(string id) => byId.GetValueOrDefault(id)?.ParentId;
        private string? NameOf(string id) => byId.GetValueOrDefault(id)?.Name;
        public string? Section(string? categoryId, string? stationId) => Domain.Catalog.SectionOf(categoryId, ParentOf, stationId is null ? null : stationKinds.GetValueOrDefault(stationId));
        public string[] Path(string? categoryId) => Domain.Catalog.CategoryPath(categoryId, ParentOf, NameOf);
        public string? Family(string? categoryId) => Path(categoryId) is { Length: > 0 } path ? path[0] : null;
    }

    // E3.2: imagen VIGENTE de un producto con sus bytes, o null (sin imagen, quitada o de otro ambito).
    public static async Task<StoredImage?> ActiveImage(this DesktopReadRepository reads, BusinessScope scope, string productId, CancellationToken ct)
        => (await reads.Query(scope, $"SELECT content,content_type,sha256 FROM core.product_images WHERE {Scope} AND product_id=@product AND active",
            r => new StoredImage(r.GetFieldValue<byte[]>(0), r.GetString(1), r.GetString(2)), ct, ("product", productId))).FirstOrDefault();

    // Vendibles con precio vigente en la tarifa GENERAL hoy: producto y presentacion activos, con su categoria. Orden estable.
    private const string SellableSql =
        "SELECT p.id,s.id,p.name,s.name,c.id,c.name,x.price_cents,i.sha256,p.reference,p.station_id FROM core.products p " +
        "JOIN core.presentations s ON s.tenant=p.tenant AND s.company=p.company AND s.location=p.location AND s.product_id=p.id AND s.active " +
        "LEFT JOIN core.categories c ON c.tenant=p.tenant AND c.company=p.company AND c.location=p.location AND c.id=p.category_id " +
        // E3.2: huella de la imagen vigente (null sin imagen o quitada).
        "LEFT JOIN core.product_images i ON i.tenant=p.tenant AND i.company=p.company AND i.location=p.location AND i.product_id=p.id AND i.active " +
        "JOIN LATERAL (SELECT price_cents FROM core.prices x WHERE x.tenant=p.tenant AND x.company=p.company AND x.location=p.location AND x.tariff_id='general' " +
        " AND x.product_id=p.id AND x.presentation_id=s.id AND x.valid_from<=@today ORDER BY x.valid_from DESC LIMIT 1) x ON true " +
        // E4a: los productos-menu (categoria 'menus') se venden abriendo la mesa con su oferta, no como consumo suelto.
        "WHERE p.tenant=@tenant AND p.company=@company AND p.location=@location AND p.active AND coalesce(p.category_id,'')<>'menus' ORDER BY c.sort,c.id,p.sort,p.id,s.sort,s.id LIMIT 5000";
    // E3.3: las filas se completan con seccion, familia, ruta, referencia y copa a partir de las categorias y estaciones del ambito.
    private static async Task<SectionIndex> Index(DesktopReadRepository reads, BusinessScope scope, CancellationToken ct)
        => new(await reads.Query(scope, $"SELECT {CatalogUnit.CategoryColumns} FROM core.categories WHERE {Scope}", CatalogUnit.CategoryRow, ct), await StationKinds(reads, scope, ct));
    public static async Task<List<SellableView>> Sellables(this DesktopReadRepository reads, BusinessScope scope, DateOnly today, CancellationToken ct)
    {
        var index = await Index(reads, scope, ct);
        return await reads.Query(scope, SellableSql, r => new SellableView(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(7) ? null : r.GetString(7), index.Section(Text(r, 4), Text(r, 9)), index.Family(Text(r, 4)), index.Path(Text(r, 4)), Text(r, 8), Domain.Catalog.IsGlass(r.GetString(1))), ct, ("today", today));
    }
    public static async Task<List<PricedSellableView>> PricedSellables(this DesktopReadRepository reads, BusinessScope scope, DateOnly today, CancellationToken ct)
    {
        var index = await Index(reads, scope, ct);
        return await reads.Query(scope, SellableSql, r => new PricedSellableView(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetInt64(6), Domain.Catalog.GeneralTariff,
            r.IsDBNull(7) ? null : r.GetString(7), index.Section(Text(r, 4), Text(r, 9)), index.Family(Text(r, 4)), index.Path(Text(r, 4)), Text(r, 8), Domain.Catalog.IsGlass(r.GetString(1))), ct, ("today", today));
    }
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
