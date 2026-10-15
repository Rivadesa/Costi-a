using System.Globalization;
using System.Text;

namespace Costina.Desktop;

// C2b: codigos de las altas del puesto principal. Un codigo tecleado se comprueba como siempre (letras, numeros, '-' o '_', hasta 32).
// Uno VACIO se genera del nombre con la regla de la importacion del catalogo (Catalog.Slug del motor, copiada aqui porque el puesto no
// referencia el dominio): minusculas ASCII sin tildes, digitos y guiones, recortado al maximo y unico en su lista (sufijos -2, -3...).
// El de una oferta se recorta a 27 porque su producto-menu se llama "menu-<codigo>" (OfferOperations) y un codigo admite 32.
// El servidor sigue comprobando cada codigo y su unicidad: esto solo ahorra inventarlo (el programa se cerraba al guardar sin codigo).
public static class Codes
{
    public const int Max = 32, OfferMax = 27;

    public static string Typed(string text)
    {
        var code = text.Trim();
        if (code.Length is 0 or > Max || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("El código es obligatorio: letras, números, '-' o '_', sin espacios ni acentos (máximo 32).");
        return code;
    }

    // El tecleado o, si esta vacio, uno generado del nombre (ya comprobado: no vacio) que no este en la lista.
    public static string Of(string typed, string name, IEnumerable<string> taken, int max = Max)
        => typed.Trim().Length == 0 ? Generated(name, taken, max) : Typed(typed);

    public static string Generated(string name, IEnumerable<string> taken, int max = Max)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var slug = Slug(name, max);
        if (!used.Contains(slug)) return slug;
        for (var n = 2; ; n++)
        {
            var suffix = "-" + n;
            var candidate = Slug(name, max - suffix.Length) + suffix;
            if (!used.Contains(candidate)) return candidate;
        }
    }

    public static string Slug(string text, int max = Max)
    {
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = new StringBuilder(); var dash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c)) { chars.Append(c); dash = false; }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            else if (!dash && chars.Length > 0) { chars.Append('-'); dash = true; }
        }
        var slug = chars.ToString().TrimEnd('-');
        if (slug.Length > max) slug = slug[..max].TrimEnd('-');
        return slug.Length == 0 ? "x" : slug;
    }
}
