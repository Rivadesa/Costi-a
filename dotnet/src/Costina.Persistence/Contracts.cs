using System.Text.Json;
using System.Text.Json.Serialization;
using Costina.Domain;

namespace Costina.Persistence;

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
