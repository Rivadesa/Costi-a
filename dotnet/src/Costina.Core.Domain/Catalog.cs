using System.Globalization;

namespace Costina.Core.Domain;

// E3 (Hito 6, ADR-012): CATALOGO Y TARIFAS del nucleo. Impuestos, categorias (jerarquicas), productos con presentaciones
// vendibles, tarifas y precios con vigencia. Datos maestros relacionales editables desde el puesto principal e importables.
// Nada se borra: se desactiva. El precio se congela en la cuenta al consumir (ChargeLine lleva producto, presentacion y tarifa).
public sealed record TaxDefinition(string Id, string Name, decimal Rate, bool Active);
public sealed record CategoryDefinition(string Id, string Name, string? ParentId, string? Color, int Sort, bool Active);
// E4b: StationId = estacion de cocina de un plato (a la carta); null = bebida o consumo directo, sin pase.
public sealed record ProductDefinition(string Id, string Name, string? CategoryId, string TaxId, string? Reference, int Sort, bool Active, string? StationId = null);
public sealed record PresentationDefinition(string ProductId, string Id, string Name, int Sort, bool Active);
public sealed record TariffDefinition(string Id, string Name, int Sort, bool Active);
public sealed record PriceDefinition(string TariffId, string ProductId, string PresentationId, DateOnly ValidFrom, long PriceCents);

public static class Catalog
{
    public const long MaxPriceCents = 99_999_999, MaxReferenceLength = 64;
    public const int MaxCategoryDepth = 4;
    // La tarifa GENERAL existe siempre en cada ambito y no se desactiva: es la que se aplica cuando la sala no fija otra.
    public const string GeneralTariff = "general";
    // Presentacion por defecto de la importacion y de la migracion (lo que antes era el unico "producto vendible").
    public const string DefaultPresentation = "unit";

    public static TaxDefinition Tax(string? id, string? name, decimal? rate, bool active = true)
    {
        Guard.Rule(rate is >= 0 and <= 100 && decimal.Round(rate.Value, 2) == rate.Value, "invalid_rate", "Tax rate must be between 0 and 100 with at most two decimals.");
        return new(Organization.Code(id, "tax id"), Organization.Name(name), rate!.Value, active);
    }

    public static CategoryDefinition Category(string? id, string? name, string? parentId, string? color, int? sort, bool active = true)
    {
        var code = Organization.Code(id, "category id");
        var parent = string.IsNullOrWhiteSpace(parentId) ? null : Organization.Code(parentId, "parent id");
        Guard.Rule(parent != code, "invalid_parent", "A category cannot be its own parent.");
        return new(code, Organization.Name(name), parent, Color(color), Organization.Sort(sort), active);
    }

    // Color de familia para los botones del TPV: #RRGGBB o nada.
    public static string? Color(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().ToUpperInvariant();
        Guard.Rule(text.Length == 7 && text[0] == '#' && text.Skip(1).All(char.IsAsciiHexDigit), "invalid_color", "Color must be #RRGGBB.");
        return text;
    }

    public static ProductDefinition Product(string? id, string? name, string? categoryId, string? taxId, string? reference, int? sort, bool active = true, string? stationId = null)
    {
        var category = string.IsNullOrWhiteSpace(categoryId) ? null : Organization.Code(categoryId, "category id");
        var station = string.IsNullOrWhiteSpace(stationId) ? null : Organization.Code(stationId, "station id");
        return new(Organization.Code(id, "product id"), Organization.Name(name), category, Organization.Code(taxId, "tax id"), Reference(reference), Organization.Sort(sort), active, station);
    }

    // Referencia externa (codigo de articulo del ERP anterior, SKU de la tienda web): texto corto sin control.
    public static string? Reference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        Guard.Rule(text.Length <= MaxReferenceLength && !text.Any(char.IsControl), "invalid_reference", $"Reference must be 1-{MaxReferenceLength} printable characters.");
        return text;
    }

    public static PresentationDefinition Presentation(string? productId, string? id, string? name, int? sort, bool active = true)
        => new(Organization.Code(productId, "product id"), Organization.Code(id, "presentation id"), Organization.Name(name), Organization.Sort(sort), active);

    public static TariffDefinition Tariff(string? id, string? name, int? sort, bool active = true)
    {
        var code = Organization.Code(id, "tariff id");
        Guard.Rule(code != GeneralTariff || active, "reserved_tariff", "The general tariff cannot be deactivated.");
        return new(code, Organization.Name(name), Organization.Sort(sort), active);
    }

    public static PriceDefinition Price(string? tariffId, string? productId, string? presentationId, DateOnly validFrom, long? priceCents)
    {
        Guard.Rule(priceCents is >= 0 and <= MaxPriceCents, "invalid_price", $"Price must be between 0 and {MaxPriceCents} cents.");
        return new(Organization.Code(tariffId, "tariff id"), Organization.Code(productId, "product id"), Organization.Code(presentationId, "presentation id"), validFrom, priceCents!.Value);
    }

    // Fecha de vigencia tal como viaja en comandos y ficheros: AAAA-MM-DD.
    public static DateOnly ValidFrom(string? value, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value)) return today;
        Guard.Rule(DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date), "invalid_date", "validFrom must be YYYY-MM-DD.");
        return date;
    }

    // El arbol de categorias no admite ciclos ni mas de MaxCategoryDepth niveles. parentOf devuelve el padre de un codigo (null en la raiz).
    public static int Depth(string id, string? parentId, Func<string, string?> parentOf)
    {
        var depth = 1; var current = parentId;
        while (current is not null)
        {
            Guard.Rule(current != id, "category_cycle", "A category cannot descend from itself.");
            Guard.Rule(++depth <= MaxCategoryDepth, "category_depth", $"Categories nest at most {MaxCategoryDepth} levels.");
            current = parentOf(current);
        }
        return depth;
    }

    // Precio vigente: la fila de mayor validFrom no posterior a la fecha; null si no hay ninguna.
    public static PriceDefinition? Current(IEnumerable<PriceDefinition> prices, DateOnly on)
        => prices.Where(p => p.ValidFrom <= on).OrderByDescending(p => p.ValidFrom).FirstOrDefault();

    // Codigo derivado de un texto libre (importacion): minusculas ASCII, digitos y guiones; nunca vacio.
    public static string Slug(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = new List<char>(); var dash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c)) { chars.Add(c); dash = false; }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            else if (!dash && chars.Count > 0) { chars.Add('-'); dash = true; }
        }
        while (chars.Count > 0 && chars[^1] == '-') chars.RemoveAt(chars.Count - 1);
        var slug = new string(chars.ToArray());
        if (slug.Length > Organization.MaxCodeLength) slug = slug[..Organization.MaxCodeLength].TrimEnd('-');
        return slug.Length == 0 ? "x" : slug;
    }
}
