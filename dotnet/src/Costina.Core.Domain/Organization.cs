namespace Costina.Core.Domain;

// E2 (Hito 6, ADR-012): ORGANIZACION del negocio, nucleo. Salas (zonas), mesas y estaciones de trabajo son datos
// maestros RELACIONALES editables desde el puesto principal. Nunca se borran: se desactivan, para que los servicios
// y cuentas antiguos sigan siendo legibles. El codigo (Id) es estable e inmutable; el nombre se edita.
public enum StationKind { Kitchen, Pass, Bar, Room }

public sealed record ZoneDefinition(string Id, string Name, int Sort, bool Active);
public sealed record TableDefinition(string Id, string Name, int Capacity, string ZoneId, int Sort, bool Active);
public sealed record StationDefinition(string Id, string Name, StationKind Kind, int Sort, bool Active);

public static class Organization
{
    public const int MaxCapacity = 60, MaxNameLength = 60, MaxCodeLength = 32;
    // La estacion de PASE (validacion y revision de pases, D4.3) tiene codigo reservado: existe siempre y no se desactiva.
    public const string PassStation = "pase";

    // Codigo: letras, digitos, guion y guion bajo; sin espacios ni acentos (viaja en rutas, QR y ficheros).
    public static string Code(string? value, string name)
    {
        var code = Guard.Text(value, name);
        Guard.Rule(code.Length <= MaxCodeLength && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'),
            "invalid_code", $"{name} must be 1-{MaxCodeLength} letters, digits, '-' or '_'.");
        return code;
    }

    public static string Name(string? value)
    {
        var text = Guard.Text(value, "name");
        Guard.Rule(text.Length <= MaxNameLength && !text.Any(char.IsControl), "invalid_name", $"Name must be 1-{MaxNameLength} printable characters.");
        return text;
    }

    public static int Sort(int? value)
    {
        var sort = value ?? 0;
        Guard.Rule(sort is >= 0 and <= 9999, "invalid_sort", "Sort must be between 0 and 9999.");
        return sort;
    }

    public static ZoneDefinition Zone(string? id, string? name, int? sort, bool active = true)
        => new(Code(id, "zone id"), Name(name), Sort(sort), active);

    public static TableDefinition Table(string? id, string? name, int? capacity, string? zoneId, int? sort, bool active = true)
    {
        Guard.Rule(capacity is >= 1 and <= MaxCapacity, "invalid_capacity", $"Capacity must be between 1 and {MaxCapacity}.");
        return new(Code(id, "table id"), Name(name), capacity!.Value, Code(zoneId, "zone id"), Sort(sort), active);
    }

    public static StationDefinition Station(string? id, string? name, StationKind kind, int? sort, bool active = true)
    {
        var code = Code(id, "station id");
        Guard.Rule(code != PassStation || kind == StationKind.Pass, "reserved_station", "The 'pase' station is the pass station.");
        Guard.Rule(code != PassStation || active, "reserved_station", "The pass station cannot be deactivated.");
        return new(code, Name(name), kind, Sort(sort), active);
    }
}
