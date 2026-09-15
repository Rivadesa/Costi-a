using System.Globalization;

namespace Costina.Domain;

public sealed record ChargeLine(string Id, string Description, int Quantity, long UnitPriceCents,
    bool Voided = false, string? VoidReason = null)
{
    public long TotalCents => Voided ? 0 : checked(Quantity * UnitPriceCents);
}
public sealed record PaymentEntry(string Id, string Method, long AmountCents, DateTimeOffset At);
public sealed record AccountView(string Id, string ServiceId, AccountState State,
    long TotalCents, long PaidCents, long BalanceCents, long CreditCents,
    PaymentCoverage Coverage, IReadOnlyList<ChargeLine> Charges, IReadOnlyList<PaymentEntry> Payments);

// An account can remain open after table release. No dependency on DiningService.
public sealed partial class SettlementAccount : Aggregate
{
    private readonly List<ChargeLine> charges = [];
    private readonly List<PaymentEntry> payments = [];
    public string ServiceId { get; }
    public AccountState State { get; private set; } = AccountState.Open;
    public long TotalCents => Sum(charges.Select(c => c.TotalCents));
    public long PaidCents => Sum(payments.Select(p => p.AmountCents));
    public long BalanceCents => Math.Max(0, checked(TotalCents - PaidCents));
    public long CreditCents => Math.Max(0, checked(PaidCents - TotalCents));
    public PaymentCoverage Coverage => PaidCents > TotalCents ? PaymentCoverage.Credit
        : PaidCents == TotalCents ? PaymentCoverage.Paid
        : PaidCents == 0 ? PaymentCoverage.Unpaid : PaymentCoverage.PartiallyPaid;

    public SettlementAccount(string id, BusinessScope scope, string serviceId) : base(id, scope)
        => ServiceId = Guard.Text(serviceId, nameof(serviceId));

    // Prices here are already authorized snapshots, not raw input from a waiter or browser.
    // The future catalog application service must be the only public path to normal charges.
    public void AddCharge(string lineId, string description, int quantity, long unitPriceCents, CommandStamp stamp)
    {
        Mutable(stamp);
        lineId = Guard.Text(lineId, nameof(lineId));
        description = Guard.Text(description, nameof(description));
        Guard.Rule(quantity > 0 && unitPriceCents >= 0, "invalid_charge", "Invalid quantity or price.");
        Guard.Rule(charges.All(c => c.Id != lineId), "duplicate_charge", "This charge ID already exists.");
        var line = new ChargeLine(lineId, description, quantity, unitPriceCents);
        _ = checked(TotalCents + line.TotalCents);
        charges.Add(line);
        Emit("account.charge_added", stamp, ("charge_id", lineId),
            ("amount_cents", line.TotalCents.ToString(CultureInfo.InvariantCulture)));
    }

    public void RecordPayment(string paymentId, string method, long amountCents, CommandStamp stamp)
    {
        Mutable(stamp);
        paymentId = Guard.Text(paymentId, nameof(paymentId));
        method = Guard.Text(method, nameof(method));
        Guard.Rule(amountCents > 0, "invalid_payment", "Payment must be positive.");
        Guard.Rule(payments.All(p => p.Id != paymentId), "duplicate_payment", "This payment ID already exists.");
        _ = checked(PaidCents + amountCents);
        payments.Add(new PaymentEntry(paymentId, method, amountCents, stamp.At));
        Emit("payment.recorded", stamp, ("payment_id", paymentId), ("method", method),
            ("amount_cents", amountCents.ToString(CultureInfo.InvariantCulture)));
    }

    public void VoidCharge(string lineId, string reason, CommandStamp stamp)
    {
        Mutable(stamp);
        reason = Guard.Text(reason, nameof(reason));
        var index = charges.FindIndex(c => c.Id == lineId);
        Guard.Rule(index >= 0, "charge_not_found", "Charge not found.");
        Guard.Rule(!charges[index].Voided, "charge_already_voided", "Charge already voided.");
        charges[index] = charges[index] with { Voided = true, VoidReason = reason };
        Emit("account.charge_voided", stamp, ("charge_id", lineId), ("reason", reason));
    }

    public void Close(CommandStamp stamp)
    {
        Mutable(stamp);
        Guard.Rule(BalanceCents == 0 && CreditCents == 0, "account_not_balanced",
            "Outstanding balance or credit must be resolved before closing the account.");
        State = AccountState.Closed;
        Emit("account.closed", stamp);
    }

    public AccountView View() => new(Id, ServiceId, State, TotalCents, PaidCents, BalanceCents,
        CreditCents, Coverage, Array.AsReadOnly(charges.ToArray()), Array.AsReadOnly(payments.ToArray()));
    private void Mutable(CommandStamp stamp)
    {
        Check(stamp);
        Guard.Rule(State == AccountState.Open, "account_closed", "Closed accounts cannot be edited.");
    }
    private static long Sum(IEnumerable<long> values)
    {
        long total = 0;
        foreach (var value in values) total = checked(total + value);
        return total;
    }
}
