using Costina.Core.Domain;
using Costina.Core.Persistence;
using static Costina.Core.Persistence.CommandGuards;

namespace Costina.Server;

// Cuenta / Caja: NUCLEO (ventas). Solo el puesto principal (ADR-007); los modulos apuntan cargos por la Unit.
public sealed record AccountCommand(long ExpectedVersion,string? ProductId=null,int Quantity=1,string? PaymentId=null,
    string? Method=null,long AmountCents=0,string? ChargeId=null,string? Reason=null,string? RefundId=null,string? PresentationId=null);

public static class CheckoutOperations
{
    public static async Task<object> Account(Unit unit,ExecutionIdentity identity,string id,string action,AccountCommand request)
    {
        var stored=await unit.Account(id); Version(stored.Version,request.ExpectedVersion);
        var entity=stored.Entity; var stamp=new CommandStamp(identity.Scope,identity.ActorId,DateTimeOffset.UtcNow);
        switch(action)
        {
            case "add-product":
            {
                // E3: vendible del catalogo al precio vigente de la tarifa de la sala de la cuenta (o la general); origen congelado en el cargo.
                var (product,presentation)=await unit.Sellable(Required(request.ProductId),request.PresentationId);
                var (cents,tariff)=await unit.PriceFor(await unit.TariffForService(id),product.Id,presentation.Id,DateOnly.FromDateTime(DateTime.Now));
                entity.AddCharge(Guid.NewGuid().ToString("N"),product.Name+" · "+presentation.Name,request.Quantity,cents,stamp,product.Id,presentation.Id,tariff);
                break;
            }
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
