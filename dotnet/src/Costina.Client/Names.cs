namespace Costina.Client;

// C1: nombres legibles de mesas y estaciones a partir de lo leido (mesas de GET /configuration; las estaciones llegaran a
// /configuration en C2, hasta entonces solo las conoce quien lee /organization). Cuando el nombre no se conoce queda el codigo:
// nunca se inventa un nombre ni se oculta de que mesa o estacion se habla.
public sealed class Names(IEnumerable<TableChoice>? tables = null, IEnumerable<StationDto>? stations = null)
{
    public static readonly Names Empty = new();
    private readonly Dictionary<string, string> tableNames = Index((tables ?? []).Select(t => (t.Id, t.Name)));
    private readonly Dictionary<string, string> stationNames = Index((stations ?? []).Select(s => (s.Id, s.Name)));

    public string Table(string? id) => Lookup(tableNames, id);
    public string Station(string? id) => Lookup(stationNames, id);

    private static Dictionary<string, string> Index(IEnumerable<(string Id, string Name)> rows)
    {
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, name) in rows) if (name.Trim().Length > 0) index.TryAdd(id, name.Trim());
        return index;
    }
    private static string Lookup(Dictionary<string, string> index, string? id) => id is not null && index.TryGetValue(id, out var name) ? name : id ?? "";
}
