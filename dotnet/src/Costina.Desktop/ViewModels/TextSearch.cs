using System.Globalization;

namespace Costina.Desktop.ViewModels;

// C2b: busqueda de las listas de gestion (Caja, Catalogo, Oferta): sin distinguir mayusculas, tildes ni la virgulilla de la ene
// ("costina" encuentra la Degustacion Costina; "senorans", el Pazo de Senorans). Se compara con la cultura INVARIANTE: en la espanola (la
// de un PC en castellano, el de cualquier cliente) la ene es otra letra y "albarino" no encontraba el Albarino por su nombre.
public static class TextSearch
{
    public static bool Contains(string? text, string part) =>
        part.Length == 0 || text is not null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, part, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
