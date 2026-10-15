namespace Costina.Client;

// C1: nombres legibles de mesas y estaciones a partir de lo leido. Cuando el nombre no se conoce queda el codigo: nunca se inventa
// un nombre ni se oculta de que mesa o estacion se habla. C2: GET /configuration ya trae las estaciones para todos los roles (antes
// solo las conocia quien leia /organization); con un servidor anterior no llegan y se sigue viendo el codigo.
public sealed class Names
{
    public static readonly Names Empty = new();
    private readonly Dictionary<string, string> tableNames;
    private readonly Dictionary<string, string> stationNames;

    public Names(IEnumerable<TableChoice>? tables = null, IEnumerable<StationDto>? stations = null)
        : this((tables ?? []).Select(t => (t.Id, t.Name)), (stations ?? []).Select(s => (s.Id, s.Name))) { }
    // C2: mesas y estaciones de la configuracion operativa leida.
    public Names(Configuration configuration)
        : this(configuration.Tables.Select(t => (t.Id, t.Name)), (configuration.Stations ?? []).Select(s => (s.Id, s.Name))) { }
    private Names(IEnumerable<(string Id, string Name)> tables, IEnumerable<(string Id, string Name)> stations)
    {
        tableNames = Index(tables);
        stationNames = Index(stations);
    }

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
