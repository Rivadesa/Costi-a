using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Costina.Client;
namespace Costina.Desktop;

// This bounded first UI contains presentation handlers only; all commands are authorized by the server.
// Realtime notices and reconnections only queue the same authoritative HTTP re-read that the
// manual button triggers; state is never taken from the channel and duplicates are harmless.
public partial class MainWindow : Window
{
    private ApiClient? api;
    private RealtimeSubscription? realtime;
    private SessionInfo? session;
    private BoardEntry[] board = [];
    private Versioned<DiningDto>? dining;
    private Versioned<AccountDto>? account;
    private string? serviceId, accountServiceId;
    private bool busy, rendering, refreshQueued;
    private bool Main => session?.Role == "main";
    private bool Sala => session?.Role is "main" or "service";
    private bool Kitchen => session?.Role is "main" or "kitchen";
    private CourseDto? Course => Courses.SelectedItem as CourseDto;
    private PreparationDto? Preparation => Preparations.SelectedItem as PreparationDto;
    public MainWindow() { InitializeComponent(); Controls(); }
    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; Controls();
        try { await action(); }
        catch (Exception e)
        {
            Status.Text = e switch {
                ApiError a => $"El servidor rechazó o no confirmó la petición: {a.Message}. Actualiza antes de una nueva acción.",
                HttpRequestException or TaskCanceledException => "Servidor sin respuesta. Los datos pueden estar desactualizados. Una orden pendiente NO está confirmada.",
                ArgumentException or InvalidOperationException => e.Message,
                _ => "No se pudo interpretar la respuesta. Comprueba la versión y actualiza; no dupliques la orden."
            };
        }
        finally
        {
            busy = false; Controls();
            if (refreshQueued && api is not null)
            {
                refreshQueued = false;
                _ = Dispatcher.BeginInvoke(async () => await Run(Refresh));
            }
        }
    }
    // Coalesce: los avisos que llegan durante una operación no la interrumpen; se relee una vez al terminar.
    private void QueueRefresh()
    {
        if (api is null) return;
        if (busy) { refreshQueued = true; return; }
        _ = Run(Refresh);
    }
    private void Controls()
    {
        if (Tabs is null) return;
        bool connected = session is not null, pending = api?.Pending is not null;
        bool writable = connected && !busy && !pending;
        ConnectButton.IsEnabled = !connected && !busy;
        DisconnectButton.IsEnabled = connected && !busy && !pending;
        Endpoint.IsEnabled = AccessKey.IsEnabled = !connected && !busy;
        RefreshButton.IsEnabled = connected && !busy;
        Tabs.IsEnabled = connected && !busy;
        CheckoutTab.Visibility = Main ? Visibility.Visible : Visibility.Collapsed;
        OpenPanel.Visibility = Sala ? Visibility.Visible : Visibility.Collapsed;
        KitchenActions.Visibility = Kitchen ? Visibility.Visible : Visibility.Collapsed;
        OpenButton.IsEnabled = writable && Sala;
        string? state = dining?.Data.State;
        bool active = state is "InService" or "Paused";
        var courses = dining?.Data.Courses ?? [];
        StartButton.IsEnabled = writable && Sala && state == "Open";
        FireButton.IsEnabled = writable && Sala && state == "InService" && courses.Any(c => c.State == "Pending") && !courses.Any(c => c.State is "Fired" or "Preparing" or "Ready");
        ServeButton.IsEnabled = writable && Sala && active && Course?.State == "Ready";
        PauseButton.IsEnabled = writable && Sala && state == "InService";
        ResumeButton.IsEnabled = writable && Sala && state == "Paused";
        SkipButton.IsEnabled = writable && Sala && (state is "Open" or "InService" or "Paused") && Course?.State == "Pending";
        CompleteButton.IsEnabled = writable && Main && active && courses.All(c => c.State is "Served" or "Skipped");
        ReleaseButton.IsEnabled = writable && Main && (state is "Completed" or "Cancelled");
        PrepStartButton.IsEnabled = writable && Kitchen && active && Preparation?.State == "Fired";
        PrepReadyButton.IsEnabled = writable && Kitchen && active && Preparation?.State == "Preparing";
        ValidateButton.IsEnabled = writable && Kitchen && active && (Course?.State is "Fired" or "Preparing") && Course.Preparations.Where(p => p.Mandatory).All(p => p.State == "Ready");
        bool openAccount = account?.Data.State == "Open";
        AddButton.IsEnabled = PayButton.IsEnabled = writable && Main && openAccount;
        CloseAccountButton.IsEnabled = writable && Main && openAccount && account?.Data.BalanceCents == 0 && account?.Data.CreditCents == 0;
        PendingPanel.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        PendingText.Text = pending ? "SIN CONFIRMAR: " + api!.Pending!.Description + ". Conservamos el mismo identificador. No repitas la acción por otro medio sin comprobarla." : "";
        RetryButton.IsEnabled = pending && !busy;
    }
    private async Task Connect()
    {
        if (!Uri.TryCreate(Endpoint.Text.Trim(), UriKind.Absolute, out var uri)) throw new ArgumentException("Dirección no válida.");
        var candidate = new ApiClient(uri, AccessKey.Password);
        var key = AccessKey.Password;
        try
        {
            var identity = await candidate.GetAsync<SessionInfo>("session");
            var config = await candidate.GetAsync<Configuration>("configuration");
            api = candidate; session = identity;
            await StartRealtime(uri, key);
            rendering = true;
            try { TableSelect.ItemsSource = config.Tables; TableSelect.SelectedIndex = 0; MenuSelect.ItemsSource = config.Menus; MenuSelect.SelectedIndex = 0; }
            finally { rendering = false; }
            await Refresh(); AccessKey.Clear();
            Status.Text = $"Conectado al servidor real · rol {identity.Role} · {identity.CompanyId}/{identity.LocationId}";
        }
        catch { if (api == candidate) Reset(); else candidate.Dispose(); throw; }
    }
    // Fallo del canal = degradación, no error: el refresco manual sigue siendo el respaldo.
    private async Task StartRealtime(Uri uri, string key)
    {
        try
        {
            realtime = new RealtimeSubscription(uri, key);
            realtime.Notified += _ => Dispatcher.BeginInvoke(QueueRefresh);
            realtime.Reconnected += () => Dispatcher.BeginInvoke(QueueRefresh);
            realtime.StateChanged += state => Dispatcher.BeginInvoke(() => RealtimeState.Text = state switch
            {
                "connected" => "Tiempo real activo: los cambios de otros puestos aparecen sin pulsar Actualizar.",
                "reconnecting" => "Tiempo real reconectando. Los datos pueden estar desactualizados.",
                _ => "Tiempo real desconectado. Usa Actualizar; los datos no se refrescan solos."
            });
            await realtime.StartAsync();
        }
        catch
        {
            _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null;
            RealtimeState.Text = "Tiempo real no disponible en este servidor. Usa el botón Actualizar.";
        }
    }
    private void Reset()
    {
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask); realtime = null; refreshQueued = false;
        RealtimeState.Text = "Tiempo real inactivo.";
        api?.Dispose(); api = null; session = null; dining = null; account = null; board = [];
        serviceId = accountServiceId = null; rendering = true;
        try {
            BoardList.ItemsSource = Courses.ItemsSource = Preparations.ItemsSource = null;
            AccountSelect.ItemsSource = Charges.ItemsSource = ProductSelect.ItemsSource = null;
            TableSelect.ItemsSource = MenuSelect.ItemsSource = null;
            Totals.Text = AccountState.Text = ""; AmountInput.Clear(); Reason.Clear();
            ServiceTitle.Text = "Selecciona una mesa"; Tabs.SelectedItem = ServiceTab;
        } finally { rendering = false; }
        Status.Text = "Desconectado. La clave no se guarda en disco.";
        ReadTime.Text = "Sin lectura actual del servidor.";
    }
    private async Task Refresh()
    {
        if (api is null) return;
        var fresh = await api.GetAsync<BoardEntry[]>("board");
        board = fresh;
        if (!board.Any(b => b.Service.Id == serviceId)) serviceId = board.FirstOrDefault()?.Service.Id;
        dining = serviceId is null ? null : await api.GetAsync<Versioned<DiningDto>>("services/" + ApiClient.Segment(serviceId));
        var previous = Course?.Id;
        rendering = true;
        try {
            BoardList.ItemsSource = board; BoardList.SelectedItem = board.FirstOrDefault(b => b.Service.Id == serviceId);
            ServiceTitle.Text = dining is null ? "Sin mesas ocupadas. Puedes abrir una mesa." : $"{dining.Data.TableId} · {dining.Data.Pax} personas · {dining.Data.State}";
            var courses = dining?.Data.Courses ?? [];
            Courses.ItemsSource = courses;
            Courses.SelectedItem = courses.FirstOrDefault(c => c.Id == previous) ?? courses.FirstOrDefault(c => c.State is "Fired" or "Preparing" or "Ready") ?? courses.FirstOrDefault(c => c.State == "Pending") ?? courses.LastOrDefault();
            UpdatePreparations();
        } finally { rendering = false; }
        if (Main && Tabs.SelectedItem == CheckoutTab) await RefreshAccount();
        ReadTime.Text = $"Datos leídos a las {DateTimeOffset.Now:HH:mm:ss} (lectura autoritativa del servidor).";
    }
    private void UpdatePreparations()
    {
        var id = Preparation?.Id;
        Preparations.ItemsSource = Course?.Preparations;
        Preparations.SelectedItem = Course?.Preparations.FirstOrDefault(p => p.Id == id) ?? Course?.Preparations.FirstOrDefault(p => p.State is "Fired" or "Preparing") ?? Course?.Preparations.FirstOrDefault();
    }
    private async Task RefreshAccount()
    {
        if (api is null || !Main) return;
        var choices = await api.GetAsync<AccountChoice[]>("checkout/accounts");
        var products = await api.GetAsync<ProductChoice[]>("checkout/catalog");
        if (!choices.Any(a => a.ServiceId == accountServiceId)) accountServiceId = choices.FirstOrDefault(a => a.ServiceId == serviceId)?.ServiceId ?? choices.FirstOrDefault()?.ServiceId;
        account = accountServiceId is null ? null : await api.GetAsync<Versioned<AccountDto>>("checkout/services/" + ApiClient.Segment(accountServiceId));
        var productId = (ProductSelect.SelectedItem as ProductChoice)?.Id;
        rendering = true;
        try {
            AccountSelect.ItemsSource = choices; AccountSelect.SelectedItem = choices.FirstOrDefault(a => a.ServiceId == accountServiceId);
            ProductSelect.ItemsSource = products; ProductSelect.SelectedItem = products.FirstOrDefault(p => p.Id == productId) ?? products.FirstOrDefault();
            Charges.ItemsSource = account?.Data.Charges;
            Totals.Text = account is null ? "Sin cuenta seleccionada" : $"Total {Money.Format(account.Data.TotalCents)}  ·  Pagado {Money.Format(account.Data.PaidCents)}  ·  Pendiente {Money.Format(account.Data.BalanceCents)}  ·  Crédito {Money.Format(account.Data.CreditCents)}";
            AccountState.Text = account is null ? "" : $"Estado {account.Data.State}. {account.Data.Payments.Length} pagos de prueba registrados. Cerrar la cuenta no termina el servicio.";
        } finally { rendering = false; }
    }
    private async Task Send(string path, object body, string description)
    {
        var result = await api!.SendAsync(path, body, description);
        if (path == "services") serviceId = result.GetProperty("serviceId").GetString();
        await Refresh(); Status.Text = "Operación confirmada por el servidor: " + description;
    }
    private async void ConnectClick(object sender, RoutedEventArgs e) => await Run(Connect);
    private void DisconnectClick(object sender, RoutedEventArgs e) { Reset(); Controls(); }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Run(Refresh);
    private async void RetryClick(object sender, RoutedEventArgs e) => await Run(async () => {
        var path = api!.Pending!.Path; var result = await api.RetryAsync();
        if (path == "services") serviceId = result.GetProperty("serviceId").GetString();
        await Refresh(); Status.Text = "Reintento confirmado con el mismo identificador.";
    });
    private async void OpenClick(object sender, RoutedEventArgs e) => await Run(async () => {
        if (TableSelect.SelectedItem is not TableChoice table || MenuSelect.SelectedItem is not MenuChoice menu || !int.TryParse(PaxInput.Text, out var pax)) throw new ArgumentException("Selecciona mesa, menú y personas.");
        await Send("services", new { tableId = table.Id, pax, menuId = menu.Id }, "Abrir " + table.Name);
    });
    private async void DiningClick(object sender, RoutedEventArgs e) => await Run(async () => {
        if (dining is null || serviceId is null) return;
        var action = (string)((Button)sender).Tag;
        if (action is "pause" or "skip" && string.IsNullOrWhiteSpace(Reason.Text)) throw new ArgumentException("Introduce el motivo.");
        await Send("services/" + ApiClient.Segment(serviceId) + "/commands/" + action,
            new { expectedVersion = dining.Version, courseId = Course?.Id, itemId = Preparation?.Id, reason = Reason.Text }, action);
    });
    private async void ReleaseClick(object sender, RoutedEventArgs e) => await Run(async () => {
        var entry = board.FirstOrDefault(b => b.Service.Id == serviceId);
        if (entry is null || string.IsNullOrWhiteSpace(Reason.Text)) throw new ArgumentException("Introduce el motivo de liberación.");
        await Send("occupancy/" + ApiClient.Segment(entry.Service.Id) + "/release", new { expectedVersion = entry.OccupancyVersion, reason = Reason.Text }, "Liberar mesa sin cambiar su cuenta");
    });
    private async void AddClick(object sender, RoutedEventArgs e) => await Run(async () => {
        if (account is null || ProductSelect.SelectedItem is not ProductChoice p || !int.TryParse(QuantityInput.Text, out var qty) || qty < 1) throw new ArgumentException("Selecciona producto y cantidad positiva.");
        await Send("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/add-product", new { expectedVersion = account.Version, productId = p.Id, quantity = qty }, "Añadir consumo del catálogo");
    });
    private async void PayClick(object sender, RoutedEventArgs e) => await Run(async () => {
        if (account is null) return;
        long cents = Money.Parse(AmountInput.Text); var method = (string)((ComboBoxItem)MethodSelect.SelectedItem).Tag;
        await Send("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/payment", new { expectedVersion = account.Version, paymentId = Guid.NewGuid().ToString("N"), method, amountCents = cents }, "Registrar pago de prueba");
        AmountInput.Clear();
    });
    private async void CloseAccountClick(object sender, RoutedEventArgs e) => await Run(async () => {
        if (account is null) return;
        await Send("checkout/services/" + ApiClient.Segment(accountServiceId!) + "/commands/close", new { expectedVersion = account.Version }, "Cerrar cuenta saldada");
    });
    private async void BoardChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rendering || busy || BoardList.SelectedItem is not BoardEntry entry) return;
        serviceId = entry.Service.Id; await Run(Refresh);
    }
    private void CourseChanged(object sender, SelectionChangedEventArgs e) { if (!rendering) { UpdatePreparations(); Controls(); } }
    private void PreparationChanged(object sender, SelectionChangedEventArgs e) { if (!rendering) Controls(); }
    private async void AccountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rendering || busy || AccountSelect.SelectedItem is not AccountChoice choice) return;
        accountServiceId = choice.ServiceId; await Run(RefreshAccount);
    }
    private async void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != Tabs || rendering || busy || !Main || Tabs.SelectedItem != CheckoutTab) return;
        await Run(RefreshAccount);
    }
    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (busy || api?.Pending is not null) { e.Cancel = true; Status.Text = "Hay una operación en curso o sin confirmar. Comprueba/reintenta antes de cerrar. Un cierre forzado pierde el reintento de esta sesión."; return; }
        _ = (realtime?.DisposeAsync() ?? ValueTask.CompletedTask);
        api?.Dispose();
    }
}
