namespace Costina.Core.Domain;

// Affordances de la cuenta: calculadas al leer, nunca persistidas (mismo criterio que el modulo Dining).
public sealed partial class SettlementAccount
{
    public AccountView ViewWithActions()
    {
        var actions = new List<string>();
        var voidable = new List<string>();
        if (State == AccountState.Open)
        {
            actions.Add("add-product"); actions.Add("payment");
            voidable.AddRange(charges.Where(c => !c.Voided).Select(c => c.Id));
            if (voidable.Count > 0) actions.Add("void-charge");
            if (CreditCents > 0) actions.Add("refund");
            if (BalanceCents == 0 && CreditCents == 0) actions.Add("close");
        }
        else actions.Add("reopen");   // cerrada != intocable: reapertura auditada con motivo (D3.6)
        return View() with { Actions = actions.AsReadOnly(), VoidableChargeIds = voidable.AsReadOnly() };
    }
}
