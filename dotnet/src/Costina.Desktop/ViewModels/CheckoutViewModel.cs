using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie Cuenta/Caja (solo rol main; el servidor lo garantiza ademas por permisos).
// La habilitacion sale de las affordances del AccountDto; el importe se valida como formato,
// nunca como regla de negocio (el servidor decide precios y saldos). Un comando solo se construye
// desde la cuenta leida correctamente que coincide con la seleccionada: nunca se cobra en otra.
// C1: el desplegable de cuentas muestra el nombre de la mesa y el estado en castellano (AccountRow); la seleccion sigue siendo la
// cuenta leida (SelectedAccount). Las ordenes pendientes se describen por la mesa, no por el identificador del servicio.
public sealed record AccountRow(AccountChoice Choice, string Text);

public sealed partial class CheckoutViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private string? accountServiceId;   // destino solicitado; no es el contexto de un comando
    private int generation;

    [ObservableProperty] private AccountChoice[] accounts = [];
    [ObservableProperty] private AccountRow[] accountItems = [];
    [ObservableProperty] private AccountChoice? selectedAccount;
    [ObservableProperty] private Versioned<AccountDto>? account;
    [ObservableProperty] private ChargeDto[] charges = [];
    [ObservableProperty] private ProductChoice[] products = [];
    [ObservableProperty] private ProductChoice? selectedProduct;
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private string amount = "";
    [ObservableProperty] private string method = "card";
    [ObservableProperty] private string note = "";
    [ObservableProperty] private string totals = "";
    [ObservableProperty] private string accountState = "";

    public bool Visible { get; set; }

    private Versioned<AccountDto>? Context =>
        Account is not null && SelectedAccount?.ServiceId == Account.Data.ServiceId ? Account : null;
    private bool Allowed(string action) => Context?.Data.Actions?.Contains(action) == true;

    partial void OnAccountsChanged(AccountChoice[] value) => AccountItems = value.Select(a => new AccountRow(a, $"{shell.Names.Table(a.TableId)} · {a.StateLabel}")).ToArray();
    // Totales y estado salen siempre de la cuenta leida: nunca se escriben a mano.
    partial void OnAccountChanged(Versioned<AccountDto>? value)
    {
        if (value is null) return;
        var a = value.Data;
        Totals = $"Total {Money.Format(a.TotalCents)}  ·  Pagado {Money.Format(a.PaidCents)}  ·  Pendiente {Money.Format(a.BalanceCents)}  ·  Pagado de más {Money.Format(a.CreditCents)}  ·  Devuelto {Money.Format(a.RefundedCents)}";
        AccountState = $"Estado: {a.StateLabel} · {a.CoverageLabel}. {Labels.Plural(a.Payments.Length, "pago", "pagos")} y {Labels.Plural(a.Refunds?.Length ?? 0, "devolución", "devoluciones")} de prueba. Cerrar la cuenta no termina el servicio."
            + (a.State == "Closed" ? " CUENTA CERRADA: para admitir cambios, reábrela con motivo desde este puesto." : "");
    }
    // Para las ordenes pendientes: "cuenta de Mesa 1" (Context garantiza que la cuenta seleccionada es la leida).
    private string Where() => "cuenta de " + shell.Names.Table(SelectedAccount?.TableId);

    internal void Sync()
    {
        AddCommand.NotifyCanExecuteChanged(); PayCommand.NotifyCanExecuteChanged();
        CloseAccountCommand.NotifyCanExecuteChanged(); ReopenCommand.NotifyCanExecuteChanged(); RefundCommand.NotifyCanExecuteChanged();
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
        var previousProduct = SelectedProduct?.Id + "/" + SelectedProduct?.PresentationId;
        rendering = true;
        try
        {
            Accounts = choices; SelectedAccount = choices.FirstOrDefault(a => a.ServiceId == accountServiceId);
            Products = catalog; SelectedProduct = catalog.FirstOrDefault(p => p.Id + "/" + p.PresentationId == previousProduct) ?? catalog.FirstOrDefault();
            Account = current; Charges = current?.Data.Charges ?? [];   // OnAccountChanged compone totales y estado
            if (current is null) { Totals = "Sin cuenta seleccionada"; AccountState = ""; }
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
            new { expectedVersion = context.Version, productId = SelectedProduct!.Id, presentationId = SelectedProduct.PresentationId, quantity = count },   // E3: presentacion vendida
            "Añadir consumo del catálogo · " + Where());
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
            "Registrar pago de prueba · " + Where());
        Amount = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: pago de prueba registrado.";
    });

    // Reapertura auditada (D3.6): solo desde el puesto principal y con motivo; el servidor la audita.
    private bool CanReopen() => shell.Writable && Allowed("reopen");
    [RelayCommand(CanExecute = nameof(CanReopen))]
    private Task Reopen() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(Note)) throw new ArgumentException("Indica el motivo de la reapertura.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/reopen",
            new { expectedVersion = context.Version, reason = Note.Trim() },
            "Reabrir cuenta con motivo · " + Where());
        Note = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta reabierta; vuelve a admitir cambios.";
    });

    // Devolucion: solo consume credito existente; el pago original nunca se borra.
    private bool CanRefund() => shell.Writable && Allowed("refund");
    [RelayCommand(CanExecute = nameof(CanRefund))]
    private Task Refund() => shell.Run(async () =>
    {
        var context = Require();
        var cents = Money.Parse(Amount);
        if (string.IsNullOrWhiteSpace(Note)) throw new ArgumentException("Indica el motivo de la devolución.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/refund",
            new { expectedVersion = context.Version, refundId = Guid.NewGuid().ToString("N"), method = Method, amountCents = cents, reason = Note.Trim() },
            "Devolver lo pagado de más · " + Where());
        Amount = ""; Note = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: devolución registrada.";
    });

    private bool CanCloseAccount() => shell.Writable && Allowed("close");
    [RelayCommand(CanExecute = nameof(CanCloseAccount))]
    private Task CloseAccount() => shell.Run(async () =>
    {
        var context = Require();
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/close",
            new { expectedVersion = context.Version }, "Cerrar cuenta saldada · " + Where());
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta cerrada.";
    });
}
