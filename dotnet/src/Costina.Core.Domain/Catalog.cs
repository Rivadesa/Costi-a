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
// E3.2: imagen vigente de un articulo (una por producto). ContentType sale de la FIRMA de los bytes, nunca de la extension ni de
// lo que diga quien la envia; Sha256 (hex en minusculas) es su huella: ETag de la lectura y clave de cache de los clientes.
public sealed record ProductImage(string ProductId, string ContentType, byte[] Content, string Sha256)
{
    public int Bytes => Content.Length;
}

public static class Catalog
{
    public const long MaxPriceCents = 99_999_999, MaxReferenceLength = 64;
    public const int MaxCategoryDepth = 4;
    // La tarifa GENERAL existe siempre en cada ambito y no se desactiva: es la que se aplica cuando la sala no fija otra.
    public const string GeneralTariff = "general";
    // Presentacion por defecto de la importacion y de la migracion (lo que antes era el unico "producto vendible").
    public const string DefaultPresentation = "unit";
    // E3.3: dos SECCIONES fijas del catalogo, como categorias raiz reservadas (existen siempre en cada ambito, como 'general' y 'pase'):
    // no se desactivan, no se cuelgan de otra categoria y su codigo no se puede dar de alta a mano (Slug nunca produce un guion bajo).
    // Su nombre si se puede cambiar. La presentacion de la COPA es 'copa' (la de los datos de ensayo, 'glass', cuenta igual).
    public const string KitchenSection = "_cocina", CellarSection = "_bodega", GlassPresentation = "copa";
    public static bool IsSection(string? id) => id is KitchenSection or CellarSection;
    public static string SectionName(string id) => id == KitchenSection ? "Cocina" : "Bodega y bebidas";
    public static bool IsGlass(string? presentationId) => presentationId is GlassPresentation or "glass";
    // E3.3.1: LA copa de un vino entre sus presentaciones (null si no tiene ninguna): la activa primero; entre varias, 'copa'; despues,
    // por codigo. glass-sale-set reprecia o reactiva esta (solo crea 'copa' si no hay ninguna) y el puesto pinta la misma.
    public static PresentationDefinition? GlassOf(IEnumerable<PresentationDefinition> presentations) => presentations.Where(p => IsGlass(p.Id))
        .OrderByDescending(p => p.Active).ThenBy(p => p.Id == GlassPresentation ? 0 : 1).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
    // Raiz de una categoria (null sin categoria). Se protege de un ciclo con un tope de pasos.
    public static string? RootOf(string? categoryId, Func<string, string?> parentOf)
    {
        var current = categoryId;
        for (var steps = 0; current is not null && steps < 16; steps++)
        {
            var parent = parentOf(current);
            if (parent is null) return current;
            current = parent;
        }
        return current;
    }
    // Seccion de un producto al leer: la de su raiz si es una seccion; los productos-menu (categoria 'menus'), ninguna: se editan en
    // Menus y cartas; si no, por la estacion (cocina o pase = cocina; sin estacion o barra = bodega: "sin estacion, bebida o consumo
    // directo"). Asi un catalogo importado sale en Bodega antes de mover nada. Devuelve "cocina", "bodega" o null.
    public static string? SectionOf(string? categoryId, Func<string, string?> parentOf, string? stationKind)
    {
        if (categoryId == Offers.MenusCategory) return null;
        var root = RootOf(categoryId, parentOf);
        if (root == KitchenSection) return "cocina";
        if (root == CellarSection) return "bodega";
        return stationKind is { } kind && (kind.Equals("kitchen", StringComparison.OrdinalIgnoreCase) || kind.Equals("pass", StringComparison.OrdinalIgnoreCase)) ? "cocina" : "bodega";
    }
    // Nombres de la raiz a la hoja, sin la seccion; la familia es el primero (en una raiz sin seccion, la propia raiz).
    public static string[] CategoryPath(string? categoryId, Func<string, string?> parentOf, Func<string, string?> nameOf)
    {
        var names = new List<string>(); var current = categoryId;
        for (var steps = 0; current is not null && steps < 16; steps++)
        {
            if (!IsSection(current) && nameOf(current) is { } name) names.Insert(0, name);
            current = parentOf(current);
        }
        return [.. names];
    }

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

    // E3.2: solo imagenes SEGURAS. JPEG, PNG y WebP reconocidos por su firma de bytes, hasta 2 MB; nunca SVG (puede llevar
    // codigo) ni nada que no empiece por una de esas firmas. El tipo declarado tiene que coincidir con el de los bytes.
    // El servidor no las transforma: no hace falta ninguna libreria de imagen.
    public const int MaxImageBytes = 2 * 1024 * 1024;
    public static readonly IReadOnlyList<string> ImageTypes = ["image/jpeg", "image/png", "image/webp"];

    // Tipo por la firma (magic bytes): JPEG FF D8 FF; PNG 89 50 4E 47 0D 0A 1A 0A; WebP "RIFF" <tamano> "WEBP". null = otro.
    public static string? ImageType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF) return "image/jpeg";
        if (content.Length >= 8 && content[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "image/png";
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static void ImageSize(long bytes)
        => Guard.Rule(bytes <= MaxImageBytes, "image_too_large", $"The image is larger than {MaxImageBytes / (1024 * 1024)} MB.");

    public static ProductImage Image(string? productId, string? contentType, byte[]? content)
    {
        var product = Organization.Code(productId, "product id");
        Guard.Rule(content is { Length: > 0 }, "image_invalid", "The image is empty.");
        ImageSize(content!.Length);
        var detected = ImageType(content) ?? throw new RuleViolation("image_invalid", "Only JPEG, PNG and WebP images are accepted (recognised by their bytes).");
        Guard.Rule(string.Equals(contentType?.Trim(), detected, StringComparison.OrdinalIgnoreCase), "image_invalid",
            $"The declared type does not match the image bytes ({detected}).");
        return new(product, detected, content, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)));
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
    public static string Slug(string text) => Slug(text, Organization.MaxCodeLength);
    // E4d: el mismo codigo recortado a `max` (el de una oferta deja sitio al prefijo de su producto-menu).
    public static string Slug(string text, int max)
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
        if (slug.Length > max) slug = slug[..max].TrimEnd('-');
        return slug.Length == 0 ? "x" : slug;
    }

    // E4d: codigo generado de un nombre que no choque con los que ya existen en su ambito: el slug y, si esta usado, un sufijo -2, -3...
    // que sigue cabiendo en `max` (la regla de la importacion de categorias, sacada aqui para todas las altas sin codigo).
    public static string FreeCode(string name, Func<string, bool> taken, int max = Organization.MaxCodeLength)
    {
        var id = Slug(name, max);
        if (!taken(id)) return id;
        var stem = id.Length > max - 3 ? id[..(max - 3)].TrimEnd('-') : id;
        for (var n = 2; ; n++)
        {
            var candidate = stem + "-" + n;
            if (!taken(candidate)) return candidate;
        }
    }
    // E4d: un alta sin orden va al final de sus hermanos (el mayor + 10, con el tope de Organization.Sort); sin hermanos, 0.
    public static int NextSort(IEnumerable<int> siblings) => siblings.DefaultIfEmpty(-10).Max() is var top && top < 0 ? 0 : Math.Min(9999, top + 10);
}
