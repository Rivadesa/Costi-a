using System.Globalization;
using System.Text;
namespace Costina.Client;

// C3b (plan 2026-10-15, seccion 4.4): la busqueda de «Bodega y bebidas», sin WPF. La usan el selector «Añadir consumicion» del Comedor
// (DrinksPicker) y el buscador de consumos de Caja (E3.3), y es la MISMA regla que la del comandero (frontend/pwa/src/search.ts): las
// dos implementaciones recorren la tabla de casos comun dotnet/tests/fixtures/drinks-cases.json (ClientChecks y vitest).
//  - Sin tildes ni mayusculas: se compara el texto plegado (descompuesto, sin marcas combinantes y en minusculas). La ene cuenta como n.
//  - Por nombre, referencia, familia y region (la ruta de categorias). La uva, la anada o la bodega solo si estan en el nombre.
//  - Desde 2 letras (sin contar los espacios de los extremos). Cada palabra escrita tiene que estar en algun campo.
//  - Orden: primero lo que EMPIEZA por la primera palabra (o cuya referencia es exactamente lo escrito), despues lo que la lleva al
//    empezar una palabra del nombre y al final el resto; dentro de cada grupo, por nombre plegado en orden ordinal (unidades UTF-16,
//    el mismo en los dos clientes: nada de reglas de idioma que dependan de la version de ICU de cada uno).
public interface ISearchable
{
    string Id { get; }
    string Name { get; }
    string? Reference { get; }
    IReadOnlyList<string> SearchFields { get; }
}

public static class CatalogSearch
{
    public const int MinLetters = 2;

    // Plegado: NFD, sin marcas combinantes (categoria Mn: tildes, dieresis, la virgulilla de la ene, la cedilla) y en minusculas.
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().ToLowerInvariant();
    }

    public static string[] Tokens(string? query) => Fold(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static bool IsSearching(string? query) => Fold(query).Trim().Length >= MinLetters;

    // Campos en los que se busca, ya plegados y sin repetir: nombre, referencia y lugares (familia y ruta de categorias).
    public static string[] Fields(string name, string? reference, IEnumerable<string?> places)
    {
        var fields = new List<string>();
        foreach (var value in new[] { name, reference }.Concat(places))
        {
            var folded = Fold(value);
            if (folded.Length > 0 && !fields.Contains(folded, StringComparer.Ordinal)) fields.Add(folded);
        }
        return fields.ToArray();
    }

    public static bool Matches(IReadOnlyList<string> words, IReadOnlyList<string> fields)
        => words.All(word => fields.Any(field => field.Contains(word, StringComparison.Ordinal)));

    // Palabras de un texto plegado: tramos de letras y cifras (como Labels.Vocabulary.Key).
    private static IEnumerable<string> Words(string folded)
    {
        var start = -1;
        for (var i = 0; i <= folded.Length; i++)
        {
            var inside = i < folded.Length && char.IsLetterOrDigit(folded[i]);
            if (inside && start < 0) start = i;
            else if (!inside && start >= 0) { yield return folded[start..i]; start = -1; }
        }
    }

    // 0: el nombre empieza por la primera palabra, o la referencia es exactamente lo escrito; 1: una palabra del nombre empieza por
    // ella; 2: el resto (familia, region, dentro de una palabra o parte de la referencia).
    public static int Rank(IReadOnlyList<string> written, string name, string? reference)
    {
        var first = written.Count > 0 ? written[0] : "";
        var folded = Fold(name);
        if (folded.StartsWith(first, StringComparison.Ordinal) || written.Count == 1 && Fold(reference) == first) return 0;
        return Words(folded).Any(word => word.StartsWith(first, StringComparison.Ordinal)) ? 1 : 2;
    }

    // De la A a la Z sin tildes ni mayusculas; empate por el nombre tal cual y por el identificador (orden total y estable).
    public static int CompareByName(string idA, string nameA, string idB, string nameB)
    {
        var byFolded = Math.Sign(string.CompareOrdinal(Fold(nameA), Fold(nameB)));
        if (byFolded != 0) return byFolded;
        var byName = Math.Sign(string.CompareOrdinal(nameA, nameB));
        return byName != 0 ? byName : Math.Sign(string.CompareOrdinal(idA, idB));
    }
    public static int CompareByName(ISearchable a, ISearchable b) => CompareByName(a.Id, a.Name, b.Id, b.Name);

    // null = aun no se busca (menos de 2 letras): la pantalla sigue con los accesos rapidos y las familias.
    public static T[]? Search<T>(IEnumerable<T> items, string? query) where T : ISearchable
    {
        if (!IsSearching(query)) return null;
        var written = Tokens(query);
        var found = items.Where(item => Matches(written, item.SearchFields)).Select(item => (Item: item, Rank: Rank(written, item.Name, item.Reference))).ToList();
        found.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : CompareByName(a.Item, b.Item));
        return found.Select(entry => entry.Item).ToArray();
    }

    // La referencia se ensena en la fila cuando lo escrito la encuentra (el camarero la ha tecleado y espera verla).
    public static bool ReferenceHit(string? query, string? reference)
        => IsSearching(query) && reference is not null && Tokens(query).Any(word => Fold(reference).Contains(word, StringComparison.Ordinal));
}
