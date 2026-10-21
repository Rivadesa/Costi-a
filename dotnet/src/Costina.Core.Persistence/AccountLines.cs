using System.Globalization;
using Costina.Core.Domain;

namespace Costina.Core.Persistence;

// C3b: linea de una cuenta SIN importes ni tarifa, para las proyecciones OPERATIVAS de los modulos («Lo que lleva la mesa»,
// ADR-007, apartado C3b). Nunca es ChargeLine, que lleva precio unitario, total y tarifa. ProductName y PresentationName son los
// nombres vigentes del catalogo (null en un cargo sin producto, anterior a E3); MenuProduct = el producto es un producto-menu
// (categoria 'menus' o producto de una oferta). Origin, GuestPosition y At son null en las lineas anteriores a C3b.
public sealed record AccountLine(string Id, string Description, int Quantity, bool Voided, string? ProductId, string? PresentationId,
    string? ProductName, string? PresentationName, bool MenuProduct, int? GuestPosition, string? Origin, DateTimeOffset? At);

public static class AccountLines
{
    // Las lineas de las cuentas de VARIOS servicios en UNA sola consulta (el tablero no lee cuenta a cuenta). Solo extrae del JSON de
    // la cuenta los campos sin dinero: el importe nunca sale de la base por aqui. Orden: el de la cuenta.
    private const string Sql =
        "SELECT a.service_id,l.value->>'id',l.value->>'description',(l.value->>'quantity')::int,coalesce((l.value->>'voided')::boolean,false)," +
        "l.value->>'productId',l.value->>'presentationId',p.name,s.name," +
        "coalesce(p.category_id='" + Offers.MenusCategory + "' OR EXISTS (SELECT 1 FROM core.offers o WHERE o.tenant=a.tenant AND o.company=a.company AND o.location=a.location AND o.product_id=p.id),false)," +
        "(l.value->>'guestPosition')::int,l.value->>'origin',l.value->>'at' " +
        "FROM core.accounts a CROSS JOIN LATERAL jsonb_array_elements(a.payload->'charges') WITH ORDINALITY AS l(value,n) " +
        "LEFT JOIN core.products p ON p.tenant=a.tenant AND p.company=a.company AND p.location=a.location AND p.id=l.value->>'productId' " +
        "LEFT JOIN core.presentations s ON s.tenant=a.tenant AND s.company=a.company AND s.location=a.location AND s.product_id=p.id AND s.id=l.value->>'presentationId' " +
        "WHERE a.tenant=@tenant AND a.company=@company AND a.location=@location AND a.service_id=ANY(@services) ORDER BY a.service_id,l.n";

    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<AccountLine>>> AccountLinesOf(this Unit unit, IReadOnlyCollection<string> serviceIds)
    {
        if (serviceIds.Count == 0) return new Dictionary<string, IReadOnlyList<AccountLine>>(StringComparer.Ordinal);
        string? Text(Npgsql.NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        var rows = await unit.Rows(Sql, [("services", serviceIds.ToArray())], r => (Service: r.GetString(0), Line: new AccountLine(r.GetString(1), r.GetString(2),
            r.GetInt32(3), r.GetBoolean(4), Text(r, 5), Text(r, 6), Text(r, 7), Text(r, 8), r.GetBoolean(9), r.IsDBNull(10) ? null : r.GetInt32(10), Text(r, 11),
            Text(r, 12) is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null)));
        return rows.GroupBy(x => x.Service, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AccountLine>)g.Select(x => x.Line).ToArray(), StringComparer.Ordinal);
    }
}
