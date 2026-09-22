namespace Costina.Core.Domain;

// Frontera de persistencia de confianza (no se acepta como comando HTTP).
// Refunds es aditivo (D3.6): un payload anterior restaura sin devoluciones.
public sealed record AccountSnapshot(string Id, BusinessScope Scope, string ServiceId,
    AccountState State, IReadOnlyList<ChargeLine> Charges, IReadOnlyList<PaymentEntry> Payments,
    IReadOnlyList<RefundEntry>? Refunds = null);

public sealed partial class SettlementAccount
{
    public AccountSnapshot Snapshot() => new(Id, Scope, ServiceId, State,
        Array.AsReadOnly(charges.ToArray()), Array.AsReadOnly(payments.ToArray()), Array.AsReadOnly(refunds.ToArray()));

    public static SettlementAccount Restore(AccountSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Guard.Rule(Enum.IsDefined(value.State), "invalid_snapshot", "Unknown account state.");
        var result = new SettlementAccount(value.Id, value.Scope, value.ServiceId);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in value.Charges)
        {
            Guard.Text(line.Id, nameof(line.Id)); Guard.Text(line.Description, nameof(line.Description));
            Guard.Rule(ids.Add(line.Id) && line.Quantity > 0 && line.UnitPriceCents >= 0,
                "invalid_snapshot", "Invalid or duplicate charge.");
            Guard.Rule(line.Voided == !string.IsNullOrWhiteSpace(line.VoidReason),
                "invalid_snapshot", "Invalid charge cancellation.");
            _ = checked(line.Quantity * line.UnitPriceCents);
            result.charges.Add(line);
        }
        ids.Clear();
        foreach (var payment in value.Payments)
        {
            Guard.Text(payment.Id, nameof(payment.Id)); Guard.Text(payment.Method, nameof(payment.Method));
            Guard.Rule(ids.Add(payment.Id) && payment.AmountCents > 0,
                "invalid_snapshot", "Invalid or duplicate payment.");
            result.payments.Add(payment);
        }
        foreach (var refund in value.Refunds ?? [])
        {
            Guard.Text(refund.Id, nameof(refund.Id)); Guard.Text(refund.Method, nameof(refund.Method));
            Guard.Rule(ids.Add(refund.Id) && refund.AmountCents > 0 && !string.IsNullOrWhiteSpace(refund.Reason),
                "invalid_snapshot", "Invalid or duplicate refund.");
            result.refunds.Add(refund);
        }
        _ = result.TotalCents; _ = result.PaidCents;
        Guard.Rule(result.PaidCents >= 0, "invalid_snapshot", "Refunds exceed recorded payments.");
        Guard.Rule(value.State != AccountState.Closed || (result.BalanceCents == 0 && result.CreditCents == 0),
            "invalid_snapshot", "Closed account is not balanced.");
        result.State = value.State;
        return result;
    }
}
