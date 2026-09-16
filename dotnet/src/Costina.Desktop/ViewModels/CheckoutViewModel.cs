using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie Cuenta/Caja (solo rol main; el servidor lo garantiza ademas por permisos).
// La habilitacion sale de las affordances del AccountDto; el importe se valida como formato,
// nunca como regla de negocio (el servidor decide precios y saldos).
public sealed partial class CheckoutViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? accountServiceId;

    [ObservableProperty] private AccountChoice[] accounts = [];
    [ObservableProperty] private AccountChoice? selectedAccount;
    [ObservableProperty] private Versioned<AccountDto>? account;
    [ObservableProperty] private ChargeDto[] charges = [];
    [ObservableProperty] private ProductChoice[] products = [];
    [ObservableProperty] private ProductChoice? selectedProduct;
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private string amount = "";
    [ObservableProperty] private string method = "card";
    [ObservableProperty] private string totals = "";
    [ObservableProperty] private string accountState = "";

    public bool Visible { get; set; }

    private bool Allowed(string action) => Account?.Data.Actions?.Contains(action) == true;

    internal void Sync()
    {
        AddCommand.NotifyCanExecuteChanged(); PayCommand.NotifyCanExecuteChanged();
        CloseAccountCommand.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            Accounts = []; SelectedAccount = null; Account = null; Charges = []; Products = [];
            SelectedProduct = null; accountServiceId = null; Totals = ""; AccountState = ""; Amount = "";
        }
        finally { rendering = false; }
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var choices = await shell.Api.GetAsync<AccountChoice[]>("checkout/accounts");
        var catalog = await shell.Api.GetAsync<ProductChoice[]>("checkout/catalog");
        if (!choices.Any(a => a.ServiceId == accountServiceId))
            accountServiceId = choices.FirstOrDefault()?.ServiceId;
        var current = accountServiceId is null ? null
            : await shell.Api.GetAsync<Versioned<AccountDto>>("checkout/services/" + ApiClient.Segment(accountServiceId));
        var previousProduct = SelectedProduct?.Id;
        rendering = true;
        try
        {
            Accounts = choices; SelectedAccount = choices.FirstOrDefault(a => a.ServiceId == accountServiceId);
            Products = catalog; SelectedProduct = catalog.FirstOrDefault(p => p.Id == previousProduct) ?? catalog.FirstOrDefault();
            Account = current; Charges = current?.Data.Charges ?? [];
            Totals = current is null ? "Sin cuenta seleccionada"
                : $"Total {Money.Format(current.Data.TotalCents)}  ·  Pagado {Money.Format(current.Data.PaidCents)}  ·  Pendiente {Money.Format(current.Data.BalanceCents)}  ·  Crédito {Money.Format(current.Data.CreditCents)}";
            AccountState = current is null ? ""
                : $"Estado {current.Data.State}. {current.Data.Payments.Length} pagos de prueba registrados. Cerrar la cuenta no termina el servicio.";
        }
        finally { rendering = false; }
        Sync();
    }

    partial void OnSelectedAccountChanged(AccountChoice? value)
    {
        if (rendering || shell.Busy || value is null) return;
        accountServiceId = value.ServiceId;
        _ = shell.Run(LoadAsync);
    }

    private bool CanAdd() => shell.Writable && Allowed("add-product") && SelectedProduct is not null;
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task Add() => shell.Run(async () =>
    {
        if (!int.TryParse(Quantity, out var count) || count < 1) throw new ArgumentException("Introduce una cantidad positiva.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/add-product",
            new { expectedVersion = Account!.Version, productId = SelectedProduct!.Id, quantity = count },
            "Añadir consumo del catálogo");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: consumo añadido.";
    });

    private bool CanPay() => shell.Writable && Allowed("payment");
    [RelayCommand(CanExecute = nameof(CanPay))]
    private Task Pay() => shell.Run(async () =>
    {
        var cents = Money.Parse(Amount);
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/payment",
            new { expectedVersion = Account!.Version, paymentId = Guid.NewGuid().ToString("N"), method = Method, amountCents = cents },
            "Registrar pago de prueba");
        Amount = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: pago de prueba registrado.";
    });

    private bool CanCloseAccount() => shell.Writable && Allowed("close");
    [RelayCommand(CanExecute = nameof(CanCloseAccount))]
    private Task CloseAccount() => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/close",
            new { expectedVersion = Account!.Version }, "Cerrar cuenta saldada");
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta cerrada.";
    });
}
