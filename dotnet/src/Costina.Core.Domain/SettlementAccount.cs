using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Costina.Core.Domain;

// E3: el cargo CONGELA su origen (producto, presentacion y tarifa aplicada) ademas del importe; los tres son opcionales
// porque los cargos anteriores a E3 y los de menu no los tienen. Un cargo nunca vuelve a leer el catalogo.
// C3b (aditivos, opcionales): el comensal de la linea (null = toda la mesa), su ORIGEN (ChargeOrigin) y la hora en que se apunto.
// Las cuentas guardadas antes los restauran como null; un origen que este motor no conoce se conserva tal cual.
public sealed record ChargeLine(string Id, string Description, int Quantity, long UnitPriceCents,
    bool Voided = false, string? VoidReason = null, string? ProductId = null, string? PresentationId = null, string? TariffId = null,
    int? GuestPosition = null, string? Origin = null, DateTimeOffset? At = null)
{
    public long TotalCents => Voided ? 0 : checked(Quantity * UnitPriceCents);
}

// C3b: de donde sale una linea de la cuenta. Sirve para saber despues que se pidio como bebida o extra («Lo que lleva la mesa»):
// consumption = consumo de sala (add-consumption, add-consumptions); dish = plato de la carta (add-dish); menu = el menu al abrir;
// checkout = apuntado en Caja (add-product).
public static class ChargeOrigin
{
    public const string Consumption = "consumption", Dish = "dish", Menu = "menu", Checkout = "checkout";
    public static bool Known(string? origin) => origin is Consumption or Dish or Menu or Checkout;
}
public sealed record PaymentEntry(string Id, string Method, long AmountCents, DateTimeOffset At);
// Devolucion de credito (D3.6, F07): nunca borra un pago ni inventa un consumo; consume credito existente.
public sealed record RefundEntry(string Id, string Method, long AmountCents, string Reason, DateTimeOffset At);
// Actions/VoidableChargeIds: affordances de lectura (nunca persistidas; AccountView no va a snapshot).
public sealed record AccountView(string Id, string ServiceId, AccountState State,
    long TotalCents, long PaidCents, long BalanceCents, long CreditCents,
    PaymentCoverage Coverage, IReadOnlyList<ChargeLine> Charges, IReadOnlyList<PaymentEntry> Payments,
    IReadOnlyList<string>? Actions = null, IReadOnlyList<string>? VoidableChargeIds = null,
    long RefundedCents = 0, IReadOnlyList<RefundEntry>? Refunds = null);

// An account can remain open after table release. No dependency on DiningService.
// "Pagada" (saldo cero) y "cerrada" (liquidacion definitiva) son cosas distintas: una cuenta cerrada
// antes de tiempo se reabre con motivo auditado y vuelve a admitir cambios (decision de producto, #37).
public sealed partial class SettlementAccount : Aggregate
{
    private readonly List<ChargeLine> charges = [];
    private readonly List<PaymentEntry> payments = [];
    private readonly List<RefundEntry> refunds = [];
    public string ServiceId { get; }
    public AccountState State { get; private set; } = AccountState.Open;
    public long TotalCents => Sum(charges.Select(c => c.TotalCents));
    public long RefundedCents => Sum(refunds.Select(r => r.AmountCents));
    public long PaidCents => checked(Sum(payments.Select(p => p.AmountCents)) - RefundedCents);
    public long BalanceCents => Math.Max(0, checked(TotalCents - PaidCents));
    public long CreditCents => Math.Max(0, checked(PaidCents - TotalCents));
    public PaymentCoverage Coverage => PaidCents > TotalCents ? PaymentCoverage.Credit
        : PaidCents == TotalCents ? PaymentCoverage.Paid
        : PaidCents == 0 ? PaymentCoverage.Unpaid : PaymentCoverage.PartiallyPaid;

    public SettlementAccount(string id, BusinessScope scope, string serviceId) : base(id, scope)
        => ServiceId = Guard.Text(serviceId, nameof(serviceId));

    // Prices here are already authorized snapshots, not raw input from a waiter or browser.
    // The future catalog application service must be the only public path to normal charges.
    // C3b: el identificador de la linea es UNICO en la cuenta (duplicate_charge): con un identificador derivado de la orden
    // (DerivedLineId) la misma orden reenviada por otro actor nunca cobra dos veces.
    public void AddCharge(string lineId, string description, int quantity, long unitPriceCents, CommandStamp stamp,
        string? productId = null, string? presentationId = null, string? tariffId = null, int? guestPosition = null, string? origin = null)
    {
        Mutable(stamp);
        lineId = Guard.Text(lineId, nameof(lineId));
        description = Guard.Text(description, nameof(description));
        Guard.Rule(quantity > 0 && unitPriceCents >= 0, "invalid_charge", "Invalid quantity or price.");
        Guard.Rule(guestPosition is null or > 0, "invalid_guest", "Guest position must be positive.");
        Guard.Rule(origin is null || ChargeOrigin.Known(origin), "invalid_origin", "Unknown charge origin.");
        Guard.Rule(charges.All(c => c.Id != lineId), "duplicate_charge", "This charge ID already exists.");
        var line = new ChargeLine(lineId, description, quantity, unitPriceCents, ProductId: productId, PresentationId: presentationId, TariffId: tariffId,
            GuestPosition: guestPosition, Origin: origin, At: stamp.At);
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

    // Solo devuelve credito que existe (sobrepago o anulacion tras pagar). El pago original se conserva.
    public void Refund(string refundId, string method, long amountCents, string reason, CommandStamp stamp)
    {
        Mutable(stamp);
        refundId = Guard.Text(refundId, nameof(refundId));
        method = Guard.Text(method, nameof(method));
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(amountCents > 0, "invalid_refund", "Refund must be positive.");
        Guard.Rule(refunds.All(r => r.Id != refundId) && payments.All(p => p.Id != refundId),
            "duplicate_refund", "This refund ID already exists.");
        Guard.Rule(amountCents <= CreditCents, "refund_exceeds_credit", "A refund can only return existing credit.");
        refunds.Add(new RefundEntry(refundId, method, amountCents, reason, stamp.At));
        Emit("payment.refunded", stamp, ("refund_id", refundId), ("method", method),
            ("amount_cents", amountCents.ToString(CultureInfo.InvariantCulture)), ("reason", reason));
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

    // Reapertura auditada: la cuenta vuelve a admitir consumos, pagos, anulaciones y devoluciones.
    // Nada del historial cambia; el motivo y el actor quedan en outbox/auditoria.
    public void Reopen(string reason, CommandStamp stamp)
    {
        Check(stamp);
        reason = Guard.Text(reason, nameof(reason));
        Guard.Rule(State == AccountState.Closed, "account_not_closed", "Only a closed account can be reopened.");
        State = AccountState.Open;
        Emit("account.reopened", stamp, ("reason", reason));
    }

    // C3b (diferido de D6.7): identificador de una linea DERIVADO de la orden que la apunta: servicio + clave de idempotencia + indice
    // de la linea (SHA-256, 32 hexadecimales en minuscula, como un Guid "N"). La idempotencia del servidor es por actor; con esto, la
    // misma clave reenviada por OTRO actor llega a los mismos identificadores y la cuenta no admite el segundo cargo.
    public static string DerivedLineId(string serviceId, string commandKey, int index)
    {
        Guard.Rule(index >= 0, "invalid_charge", "Line index must not be negative.");
        var source = string.Join('\n', "charge", Guard.Text(serviceId, nameof(serviceId)), Guard.Text(commandKey, nameof(commandKey)),
            index.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)), 0, 16);
    }

    // C3b: aviso OPERATIVO de que cambiaron los consumos de un servicio (ronda, consumo suelto, Caja anade o anula). Agregado = el
    // servicio; sin datos: no empieza por account. ni por payment., asi que llega a todos los roles, y cada uno relee el tablero.
    public const string ConsumptionsChangedType = "service.consumptions_changed";
    public static DomainEvent ConsumptionsChanged(string serviceId, CommandStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        return new(Guid.NewGuid(), ConsumptionsChangedType, Guard.Text(serviceId, nameof(serviceId)), stamp.Scope, stamp.ActorId, stamp.At,
            ReadOnlyDictionary<string, string>.Empty);
    }

    public AccountView View() => new(Id, ServiceId, State, TotalCents, PaidCents, BalanceCents,
        CreditCents, Coverage, Array.AsReadOnly(charges.ToArray()), Array.AsReadOnly(payments.ToArray()),
        RefundedCents: RefundedCents, Refunds: Array.AsReadOnly(refunds.ToArray()));
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
