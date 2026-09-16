using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie Cuenta/Caja (solo rol main; el servidor lo garantiza ademas por permisos).
// La habilitacion sale de las affordances del AccountDto; el importe se valida como formato,
// nunca como regla de negocio (el servidor decide precios y saldos). Un comando solo se construye
// desde la cuenta leida correctamente que coincide con la seleccionada: nunca se cobra en otra.
public sealed partial class CheckoutViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? accountServiceId;   // destino solicitado; no es el contexto de un comando
    private int generation;

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

    private Versioned<AccountDto>? Context =>
        Account is not null && SelectedAccount?.ServiceId == Account.Data.ServiceId ? Account : null;
    private bool Allowed(string action) => Context?.Data.Actions?.Contains(action) == true;

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
            Accounts = []; SelectedAccount = null; Products = []; SelectedProduct = null;
            accountServiceId = null; Amount = ""; Invalidate();
        }
        finally { rendering = false; }
    }

    private void Invalidate()
    {
        var wasRendering = rendering; rendering = true;
        try { Account = null; Charges = []; Totals = "Lectura pendiente: sin datos fiables de la cuenta seleccionada."; AccountState = ""; }
        finally { rendering = wasRendering; }
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var attempt = ++generation;
        var target = accountServiceId;
        if (Account is not null && Account.Data.ServiceId != target) Invalidate();
        try
        {
            var choices = await shell.Api.GetAsync<AccountChoice[]>("checkout/accounts");
            var catalog = await shell.Api.GetAsync<ProductChoice[]>("checkout/catalog");
            if (!choices.Any(a => a.ServiceId == target)) target = choices.FirstOrDefault()?.ServiceId;
            var current = target is null ? null
                : await shell.Api.GetAsync<Versioned<AccountDto>>("checkout/services/" + ApiClient.Segment(target));
            if (attempt != generation) return;
            if (current is not null && current.Data.ServiceId != target)
                throw new InvalidOperationException("El servidor devolvió una cuenta distinta de la solicitada; lectura descartada.");
            accountServiceId = target;
            Render(choices, catalog, current);
        }
        catch
        {
            if (attempt == generation) Invalidate();
            throw;
        }
    }

    private void Render(AccountChoice[] choices, ProductChoice[] catalog, Versioned<AccountDto>? current)
    {
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
        Sync();
        _ = shell.Run(LoadAsync);
    }

    private Versioned<AccountDto> Require() => Context
        ?? throw new InvalidOperationException("No hay una cuenta leída correctamente que coincida con la seleccionada.");

    private bool CanAdd() => shell.Writable && Allowed("add-product") && SelectedProduct is not null;
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task Add() => shell.Run(async () =>
    {
        var context = Require();
        if (!int.TryParse(Quantity, out var count) || count < 1) throw new ArgumentException("Introduce una cantidad positiva.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/add-product",
            new { expectedVersion = context.Version, productId = SelectedProduct!.Id, quantity = count },
            "Añadir consumo del catálogo · cuenta " + context.Data.ServiceId[..Math.Min(8, context.Data.ServiceId.Length)]);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: consumo añadido.";
    });

    private bool CanPay() => shell.Writable && Allowed("payment");
    [RelayCommand(CanExecute = nameof(CanPay))]
    private Task Pay() => shell.Run(async () =>
    {
        var context = Require();
        var cents = Money.Parse(Amount);
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/payment",
            new { expectedVersion = context.Version, paymentId = Guid.NewGuid().ToString("N"), method = Method, amountCents = cents },
            "Registrar pago de prueba · cuenta " + context.Data.ServiceId[..Math.Min(8, context.Data.ServiceId.Length)]);
        Amount = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: pago de prueba registrado.";
    });

    private bool CanCloseAccount() => shell.Writable && Allowed("close");
    [RelayCommand(CanExecute = nameof(CanCloseAccount))]
    private Task CloseAccount() => shell.Run(async () =>
    {
        var context = Require();
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/close",
            new { expectedVersion = context.Version }, "Cerrar cuenta saldada · cuenta " + context.Data.ServiceId[..Math.Min(8, context.Data.ServiceId.Length)]);
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta cerrada.";
    });
}
