using System.Globalization;
using Costina.Core.Domain;
using Costina.Core.Persistence;
using Costina.Modules.Dining.Domain;

namespace Costina.Modules.Dining;

// C3b: una entrada de «Lo que lleva la mesa»: lo ya enviado de bebida y extras, con nombre, formato, cantidad, comensal, hora y si se
// anulo en Caja. SIN importes, totales ni tarifa (ADR-007, apartado C3b: no es la cuenta provisional). Presentation es null para la
// presentacion por defecto ('unit', que no se escribe); GuestPosition null = toda la mesa; At null en una linea anterior a C3b.
public sealed record ConsumptionView(string Id, string? ProductId, string? PresentationId, string Name, string? Presentation, int Quantity,
    int? GuestPosition, DateTimeOffset? At, bool Voided);

public static class TableConsumptions
{
    // Que lineas de la cuenta se ensenan, en su orden:
    // - origen consumption (sala) o checkout (Caja): si; dish (plato de la carta) o menu: no; un origen que no se conoce: si (mejor verlo);
    // - sin origen (anterior a C3b), por su producto: fuera el producto-menu y los platos que tienen su elaboracion (add-dish crea
    //   'producto-presentacion-n' en un pase); un plato apuntado por error como consumo SI se ve, para que alguien lo note.
    public static IReadOnlyList<ConsumptionView> Of(DiningView service, IReadOnlyList<AccountLine> lines)
    {
        var preparations = service.Courses.SelectMany(c => c.Preparations).Select(p => p.Id).ToArray();
        // Elaboraciones de plato de cada producto y presentacion que aun no tienen su linea de cobro: las primeras lineas sin origen de
        // ese producto son esos platos; las demas, consumos.
        var dishes = new Dictionary<(string, string), int>();
        int Unmatched(string product, string presentation)
        {
            if (dishes.TryGetValue((product, presentation), out var left)) return left;
            var prefix = product + "-" + presentation + "-";
            left = preparations.Count(id => id.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                - lines.Count(l => l.Origin == ChargeOrigin.Dish && l.ProductId == product && l.PresentationId == presentation);
            return dishes[(product, presentation)] = left;
        }
        bool Shown(AccountLine line)
        {
            if (line.Origin is not null) return line.Origin is not (ChargeOrigin.Dish or ChargeOrigin.Menu);
            if (line.MenuProduct) return false;
            if (line.ProductId is { } product && line.PresentationId is { } presentation && Unmatched(product, presentation) > 0)
            {
                dishes[(product, presentation)]--;
                return false;
            }
            return true;
        }
        return lines.Where(Shown).Select(l => new ConsumptionView(l.Id, l.ProductId, l.PresentationId, l.ProductName ?? l.Description,
            l.PresentationId == Catalog.DefaultPresentation ? null : l.PresentationName, l.Quantity, l.GuestPosition, l.At, l.Voided)).ToArray();
    }
}
