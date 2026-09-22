using Costina.Core.Domain;
using Costina.Core.Persistence;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Server;

// Cuenta / Caja: NUCLEO (ventas). Solo el puesto principal (ADR-007); los modulos apuntan cargos por la Unit.
public sealed record AccountCommand(long ExpectedVersion,string? ProductId=null,int Quantity=1,string? PaymentId=null,
    string? Method=null,long AmountCents=0,string? ChargeId=null,string? Reason=null,string? RefundId=null);

public static class CheckoutOperations
{
    public static async Task<object> Account(Unit unit,ExecutionIdentity identity,string id,string action,AccountCommand request)
    {
        var stored=await unit.Account(id); Version(stored.Version,request.ExpectedVersion);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        switch(action)
        {
            case "add-product":
                var product=await unit.Configuration<ProductDefinition>("product",Required(request.ProductId));
                if(!product.Active) throw new RuleViolation("product_unavailable","Product unavailable.");
                entity.AddCharge(Guid.NewGuid().ToString("N"),product.Name+" · "+product.Presentation,request.Quantity,product.PriceCents,stamp);
                break;
            case "payment":
                if(request.Method is not ("cash" or "card" or "other")) throw new ArgumentException("Unsupported payment method.");
                entity.RecordPayment(Required(request.PaymentId),request.Method,request.AmountCents,stamp); break;
            case "void-charge": entity.VoidCharge(Required(request.ChargeId),Required(request.Reason),stamp); break;
            case "close": entity.Close(stamp); break;
            case "reopen": entity.Reopen(Required(request.Reason),stamp); break;
            case "refund":
                if(request.Method is not ("cash" or "card" or "other")) throw new ArgumentException("Unsupported refund method.");
                entity.Refund(Required(request.RefundId),request.Method,request.AmountCents,Required(request.Reason),stamp); break;
            default: throw new StoreNotFound();
        }
        await unit.Save(entity,stored.Version);
        return new Versioned<AccountView>(stored.Version+1,entity.View());
    }
}
