using System.Text.Json;
using System.Text.Json.Serialization;
using Costina.Core.Domain;

namespace Costina.Core.Persistence;

public static class Wire
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Decode<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)
        ?? throw new InvalidDataException("Missing persisted value.");
}
public sealed record Versioned<T>(long Version, T Data);
public sealed class StoreConflict(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
public sealed class StoreNotFound() : Exception("Record not found in the authorized scope.");
// Station: solo dispositivos emparejados. null = usuario con sesion (sin restriccion de estacion).
public sealed record ExecutionIdentity(BusinessScope Scope, string ActorId, string? Station = null);

// Guardas de comando compartidas por el nucleo y los modulos: campo obligatorio, enum reconocible y version esperada.
public static class CommandGuards
{
    public static string Required(string? value) => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A required command field is missing.");
    public static T ParseEnum<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(Required(value), true, out var parsed) && Enum.IsDefined(parsed) ? parsed
            : throw new ArgumentException($"Unsupported {typeof(T).Name} value.");
    public static void Version(long actual, long expected)
    {
        if (expected < 1) throw new ArgumentException("expectedVersion must be positive.");
        if (actual != expected) throw new StoreConflict("version_conflict", "Record changed; reload before deciding.");
    }
}
