namespace Costina.Client;

// C3b (plan 2026-10-15, seccion 4.4): el selector «Bodega y bebidas» sin WPF, para «Añadir consumicion» en la ficha de la mesa del
// Comedor. Espejo de frontend/pwa/src/drinks.ts (el comandero): las dos implementaciones recorren la tabla de casos comun
// dotnet/tests/fixtures/drinks-cases.json (ClientChecks y vitest). Solo PRESENTA lo que dice el servidor: que es bebida lo dice su
// seccion (`bodega`, E3.3) y que es la copa lo dice `glass`. Lee GET /catalog, SIN precios (tambien para el responsable): nunca
// /checkout/catalog.
//  - Una fila por producto (las filas del catalogo agrupadas por id), de la A a la Z, con un boton por presentacion: la copa primero y
//    despues el orden del servidor; con una sola presentacion, un unico «Añadir».
//  - Detalle: familia › region (la ruta de categorias, sin la seccion); con una sola presentacion que no es la de por defecto
//    («Unidad», que no se escribe), tambien su nombre.
//  - Accesos rapidos: «En esta mesa» (lo ya enviado, para repetir con +1), «Por copa» y una ficha por familia con sus subfamilias.

// Fila de GET /catalog (SellableView del servidor): sin dinero. Los campos de E3.2/E3.3 son opcionales: un servidor anterior no los envia.
public sealed record CatalogItem(string Id, string? PresentationId, string Name, string Presentation, string? CategoryId = null, string? CategoryName = null,
    string? ImageSha = null, string? Section = null, string? Family = null, string[]? CategoryPath = null, string? Reference = null, bool Glass = false);

public sealed record DrinkButton(string PresentationId, string Presentation, string Label, bool Glass);
public sealed record DrinkProduct(string Id, string Name, string? ImageSha, string? Reference, string? Family, string? Subfamily, IReadOnlyList<string> Path,
    string Detail, bool Glass, IReadOnlyList<DrinkButton> Buttons, IReadOnlyList<string> SearchFields) : ISearchable;
public sealed record FamilyChip(string? Family, string Label, int Count);
public sealed record SubfamilyChip(string Name, int Count);
public sealed record TableRepeat(string ProductId, string PresentationId, string Name, string Detail, string? ImageSha, DrinkProduct Product, DrinkButton Button);

public static class DrinksPicker
{
    public const string Section = "bodega";
    public const string DefaultPresentation = "unit";
    public const string OtherFamily = "Otros";
    public const string Add = "Añadir";

    // «Unidad» (la presentacion por defecto) no se escribe, como en la linea del servidor (Catalog.LineText).
    public static string PresentationText(string presentationId, string presentation) => presentationId == DefaultPresentation ? "" : presentation;
    public static string LineText(string name, string presentationId, string presentation)
        => PresentationText(presentationId, presentation) is { Length: > 0 } text ? name + " · " + text : name;

    private static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);

    // La ruta de categorias sin la seccion; un servidor que no la envia: la familia y la categoria del producto.
    public static string[] PathOf(string? family, string[]? categoryPath, string? categoryName)
    {
        if (categoryPath is { } path) return path.Where(Present).ToArray();
        var places = new List<string>();
        foreach (var place in new[] { family, categoryName })
            if (Present(place) && !places.Contains(place!, StringComparer.Ordinal)) places.Add(place!);
        return places.ToArray();
    }
    private static string? FamilyOf(string? family, string[] path) => Present(family) ? family : path.FirstOrDefault();
    // Donde se busca un vendible: nombre, referencia, familia y ruta. Tambien lo usa el buscador de Caja (E3.3), con sus filas con precio.
    public static string[] SearchFields(string name, string? reference, string? family, string[]? categoryPath, string? categoryName)
    {
        var path = PathOf(family, categoryPath, categoryName);
        return CatalogSearch.Fields(name, Present(reference) ? reference : null, new[] { FamilyOf(family, path) }.Concat(path));
    }

    public static DrinkProduct[] Products(IEnumerable<CatalogItem> rows)
    {
        var groups = new List<List<CatalogItem>>();
        var byId = new Dictionary<string, List<CatalogItem>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.Section != Section || !Present(row.PresentationId)) continue;
            if (!byId.TryGetValue(row.Id, out var group)) { byId[row.Id] = group = []; groups.Add(group); }
            if (!group.Any(other => other.PresentationId == row.PresentationId)) group.Add(row);
        }
        var products = groups.Select(group =>
        {
            var first = group[0];
            var path = PathOf(first.Family, first.CategoryPath, first.CategoryName);
            var family = FamilyOf(first.Family, path);
            var ordered = group.Where(r => r.Glass).Concat(group.Where(r => !r.Glass)).ToArray();
            var single = ordered.Length == 1;
            var buttons = ordered.Select(r => new DrinkButton(r.PresentationId!, r.Presentation, single ? Add : r.Presentation, r.Glass)).ToArray();
            var only = single ? PresentationText(buttons[0].PresentationId, buttons[0].Presentation) : "";
            var reference = Present(first.Reference) ? first.Reference : null;
            var detail = string.Join(" · ", new[] { string.Join(" › ", path), only }.Where(part => part.Length > 0));
            return new DrinkProduct(first.Id, first.Name, Present(first.ImageSha) ? first.ImageSha : null, reference, family, path.Length > 1 ? path[1] : null,
                path, detail, buttons.Any(b => b.Glass), buttons, SearchFields(first.Name, reference, first.Family, first.CategoryPath, first.CategoryName));
        }).ToList();
        products.Sort(CatalogSearch.CompareByName);
        return products.ToArray();
    }

    // Las familias, de la A a la Z; lo que no tiene familia, al final como «Otros».
    public static FamilyChip[] Families(IEnumerable<DrinkProduct> products)
    {
        var list = products.ToArray();
        var named = list.Where(p => p.Family is not null).GroupBy(p => p.Family!, StringComparer.Ordinal)
            .Select(g => new FamilyChip(g.Key, g.Key, g.Count())).ToList();
        named.Sort((a, b) => CatalogSearch.CompareByName(a.Family!, a.Label, b.Family!, b.Label));
        var other = list.Count(p => p.Family is null);
        return other == 0 ? named.ToArray() : [.. named, new FamilyChip(null, OtherFamily, other)];
    }

    // Subfamilias (la region en los vinos) de una familia; con menos de dos no hay nada que elegir y no se ofrecen.
    public static SubfamilyChip[] Subfamilies(IEnumerable<DrinkProduct> products, string? family)
    {
        var chips = products.Where(p => p.Family == family && p.Subfamily is not null).GroupBy(p => p.Subfamily!, StringComparer.Ordinal)
            .Select(g => new SubfamilyChip(g.Key, g.Count())).ToList();
        if (chips.Count < 2) return [];
        chips.Sort((a, b) => CatalogSearch.CompareByName(a.Name, a.Name, b.Name, b.Name));
        return chips.ToArray();
    }

    public static DrinkProduct[] InFamily(IEnumerable<DrinkProduct> products, string? family, string? subfamily = null)
        => products.Where(p => p.Family == family && (subfamily is null || p.Subfamily == subfamily)).ToArray();

    public static DrinkProduct[] ByGlass(IEnumerable<DrinkProduct> products) => products.Where(p => p.Glass).ToArray();

    public static DrinkProduct[]? Search(IEnumerable<DrinkProduct> products, string? query) => CatalogSearch.Search(products, query);

    // Detalle de la fila con una busqueda: si lo escrito encuentra la referencia, se ensena al final.
    public static string Detail(DrinkProduct product, string? query)
        => CatalogSearch.ReferenceHit(query, product.Reference)
            ? string.Join(" · ", new[] { product.Detail, "Ref. " + product.Reference }.Where(part => part.Length > 0)) : product.Detail;

    // «En esta mesa»: lo ya enviado y no anulado, una vez por producto y presentacion, en el orden en que se pidio; solo lo que sigue a la
    // venta en Bodega y bebidas (un vino retirado, una linea sin producto o un plato de cocina no se ofrecen para repetir).
    public static TableRepeat[] AtThisTable(IEnumerable<ConsumptionDto> consumptions, IEnumerable<DrinkProduct> products)
    {
        var sold = new Dictionary<(string, string), (DrinkProduct Product, DrinkButton Button)>();
        foreach (var product in products)
            foreach (var button in product.Buttons) sold.TryAdd((product.Id, button.PresentationId), (product, button));
        var seen = new HashSet<(string, string)>();
        var repeats = new List<TableRepeat>();
        foreach (var line in consumptions)
        {
            if (line.Voided || !Present(line.ProductId) || !Present(line.PresentationId)) continue;
            var key = (line.ProductId!, line.PresentationId!);
            if (!sold.TryGetValue(key, out var found) || !seen.Add(key)) continue;
            var detail = PresentationText(found.Button.PresentationId, found.Button.Presentation) is { Length: > 0 } text ? text : found.Product.Family ?? "";
            repeats.Add(new(found.Product.Id, found.Button.PresentationId, found.Product.Name, detail, found.Product.ImageSha, found.Product, found.Button));
        }
        return repeats.ToArray();
    }

    public static RoundItem Item(DrinkProduct product, DrinkButton button) => new(product.Id, button.PresentationId, product.Name, button.Presentation);
}
