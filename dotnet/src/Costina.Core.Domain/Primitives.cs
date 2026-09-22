using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

// El modulo Dining usa las mismas guardas que el nucleo (ADR-012): visibilidad explicita, no duplicacion.
[assembly: InternalsVisibleTo("Costina.Modules.Dining.Domain")]

namespace Costina.Core.Domain;

public sealed class RuleViolation(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class Guard
{
    public static string Text(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} cannot be blank.", name);
        return value.Trim();
    }

    public static void Rule(bool condition, string code, string message)
    {
        if (!condition) throw new RuleViolation(code, message);
    }
}

// Keep legacy ULIDs unchanged. No dependency on a provider's ID scheme or Guid parsing.
public sealed record BusinessScope
{
    public string TenantId { get; }
    public string CompanyId { get; }
    public string LocationId { get; }
    public BusinessScope(string tenantId, string companyId, string locationId)
    {
        TenantId = Guard.Text(tenantId, nameof(tenantId));
        CompanyId = Guard.Text(companyId, nameof(companyId));
        LocationId = Guard.Text(locationId, nameof(locationId));
    }
}

public sealed record CommandStamp
{
    public BusinessScope Scope { get; }
    public string ActorId { get; }
    public DateTimeOffset At { get; }
    public CommandStamp(BusinessScope scope, string actorId, DateTimeOffset at)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ActorId = Guard.Text(actorId, nameof(actorId));
        At = at.ToUniversalTime();
    }
}

public sealed record DomainEvent(Guid Id, string Type, string AggregateId,
    BusinessScope Scope, string ActorId, DateTimeOffset At,
    IReadOnlyDictionary<string, string> Data);

public abstract class Aggregate
{
    private readonly List<DomainEvent> events = [];
    public string Id { get; }
    public BusinessScope Scope { get; }
    protected Aggregate(string id, BusinessScope scope)
    {
        Id = Guard.Text(id, nameof(id));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    protected void Check(CommandStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        Guard.Rule(stamp.Scope == Scope, "scope_mismatch", "Command belongs to another business scope.");
    }

    protected void Emit(string type, CommandStamp stamp, params (string Key, string Value)[] data)
    {
        var payload = new ReadOnlyDictionary<string, string>(data.ToDictionary(x => x.Key, x => x.Value));
        events.Add(new DomainEvent(Guid.NewGuid(), type, Id, Scope, stamp.ActorId, stamp.At, payload));
    }

    // Draining is only safe after the Application layer has persisted an outbox transaction.
    // D0 is in-memory domain code. It is NOT a durable delivery implementation.
    public IReadOnlyList<DomainEvent> PendingEvents => events.AsReadOnly();
    public void ClearPendingEvents() => events.Clear();
}
