using Costina.Domain;
using Npgsql;

namespace Costina.Persistence;

// Notificacion fina: identidad del evento, nunca su payload. El estado autoritativo
// se relee siempre por HTTP; ningun consumidor debe reconstruir estado desde el canal.
public sealed record EventNotice(Guid Id, string Type, string AggregateId, DateTimeOffset OccurredAt);

public interface IEventSink
{
    // audience: "ops" (operacional, todos los roles) o "fin" (economico, solo principal).
    Task PublishAsync(string audience, EventNotice notice, CancellationToken ct);
}

// Relevo at-least-once del outbox transaccional hacia los clientes conectados.
// Publica ANTES de marcar published_at: una caida entre ambos produce un duplicado,
// nunca una perdida. Los duplicados son inofensivos porque las notificaciones solo
// disparan relecturas idempotentes.
public sealed class OutboxPublisher(NpgsqlDataSource dataSource, BusinessScope scope, IEventSink sink)
{
    // La separacion economica de ADR-007 tambien aplica al canal: ni siquiera el TIPO
    // de un evento de cuenta/pago llega a conexiones de sala o cocina.
    public static bool IsFinancial(string type) =>
        type.StartsWith("account.", StringComparison.Ordinal) || type.StartsWith("payment.", StringComparison.Ordinal);

    public async Task<int> PublishPendingAsync(CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var pending = new List<EventNotice>();
        // Publicacion por ambito: este proceso solo releva los eventos de SU tenant/empresa/local.
        // Eventos de otro ambito en la misma base los publica el servidor de ese ambito.
        await using (var select = new NpgsqlCommand(
            "SELECT id,type,aggregate_id,occurred_at FROM native_d1.outbox WHERE published_at IS NULL " +
            "AND tenant=@tenant AND company=@company AND location=@location " +
            "ORDER BY occurred_at,id LIMIT 100 FOR UPDATE SKIP LOCKED", connection, transaction))
        {
        select.Parameters.AddWithValue("tenant", scope.TenantId);
        select.Parameters.AddWithValue("company", scope.CompanyId);
        select.Parameters.AddWithValue("location", scope.LocationId);
        await using (var reader = await select.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                pending.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3)));
        }
        foreach (var notice in pending)
        {
            await sink.PublishAsync(IsFinancial(notice.Type) ? "fin" : "ops", notice, ct);
            await using var mark = new NpgsqlCommand(
                "UPDATE native_d1.outbox SET published_at=now() WHERE id=@id", connection, transaction);
            mark.Parameters.AddWithValue("id", notice.Id);
            await mark.ExecuteNonQueryAsync(ct);
        }
        // Un fallo del sink a mitad de lote revierte todo el marcado del lote: los eventos ya
        // emitidos se reemiten en el siguiente ciclo (duplicado acotado al lote, sin perdida).
        await transaction.CommitAsync(ct);
        return pending.Count;
    }
}
