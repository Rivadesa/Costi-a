using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop;

// Estados vacios: visible cuando NO hay datos (texto explicativo en lugar de una lista en blanco).
public sealed class InvertedBool2Vis : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

// Solo pegamento de presentacion: DataContext, PasswordBox (no enlazable a proposito, la clave
// no pasa por bindings ni se guarda), pestanas de administracion y guardas de cierre. Cero reglas de negocio:
// toda la habilitacion viene de las affordances del servidor a traves de los viewmodels.
public partial class MainWindow : Window
{
    private readonly ShellViewModel shell = new();
    // Los checks del WPF componen la ventana con datos leidos (sin servidor) para renderizarla y capturarla.
    public ShellViewModel Shell => shell;

    private readonly DispatcherTimer inactivity = new() { Interval = TimeSpan.FromMinutes(1) };

    public MainWindow()
    {
        InitializeComponent(); DataContext = shell;
        // Al conectar (o cambiar de rol) el area de trabajo muestra la primera vista que ese rol puede ver.
        shell.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ShellViewModel.Session)) SelectFirstVisibleView(); };
        // Corte B: la vista sensible pedida se abre sola cuando el responsable ha entrado.
        shell.ViewRequested += name => { if (FindName(name) is TabItem view) Tabs.SelectedItem = view; };
        // Corte B (B3): cualquier teclado o raton cuenta como actividad; cada minuto se comprueba si el responsable lleva
        // 15 minutos sin tocar nada y, si es asi, se vuelve al nivel Puesto.
        InputManager.Current.PreProcessInput += (_, _) => shell.NoteActivity();
        inactivity.Tick += (_, _) => shell.CheckInactivity();
        inactivity.Start();
    }

    // Corte B (B1): al abrirse, la ventana espera al servidor y entra como puesto si este PC lo es. Lo llama App.OnStartup;
    // los checks construyen la ventana sin llamarlo.
    public Task BootAsync() => shell.StartAsync();

    internal void SelectFirstVisibleView()
    {
        if (Tabs.SelectedItem is TabItem current && current.Visibility == Visibility.Visible) return;
        Tabs.SelectedItem = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible);
    }

    // Menu (y accesos de Inicio): cada entrada selecciona una vista del area de trabajo por su nombre (Tag).
    // Corte B: un apartado sensible desde el nivel Puesto pide primero al responsable (RequestElevation); el servidor lo
    // impone ademas con 403, asi que la pantalla solo evita un rechazo seguro.
    private void ShowView(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string name } || FindName(name) is not TabItem view) return;
        if (ShellViewModel.RequiresResponsible(name) && !shell.IsMain) { shell.RequestElevation(name); return; }
        Tabs.SelectedItem = view;
    }
    private void ElevateClick(object sender, RoutedEventArgs e) => shell.RequestElevation("HomeTab");
    private void AccessKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ConnectClick(sender, e); }
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void AboutClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        $"Costiña · puesto principal\nVersión {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}\n\nServidor: {shell.Endpoint}\n{shell.ConnectionText}",
        "Acerca de Costiña", MessageBoxButton.OK, MessageBoxImage.Information);

    private async void ConnectClick(object sender, RoutedEventArgs e)
    {
        await shell.LoginWithPassword(AccessKey.Password);
        if (shell.Connected) AccessKey.Clear();
    }

    private async void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl tabs) return;
        shell.Home.Visible = tabs.SelectedItem == HomeTab;
        shell.Checkout.Visible = tabs.SelectedItem == CheckoutTab;
        shell.Devices.Visible = tabs.SelectedItem == DevicesTab;
        shell.Organization.Visible = tabs.SelectedItem == OrganizationTab;
        shell.Catalog.Visible = tabs.SelectedItem == CatalogTab;
        shell.Offers.Visible = tabs.SelectedItem == OffersTab;
        if (shell.Home.Visible && shell.Connected && !shell.Busy)
            await shell.Run(shell.Home.LoadAsync);
        else if (tabs.SelectedItem is TabItem { Name: var selected } && ShellViewModel.RequiresResponsible(selected) && !shell.IsMain)
            SelectFirstVisibleView();   // una vista sensible no se queda abierta en el nivel Puesto
        else if (shell.Checkout.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Checkout.LoadAsync);
        else if (shell.Devices.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Devices.LoadAsync);
        else if (shell.Organization.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Organization.LoadAsync);
        else if (shell.Catalog.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Catalog.LoadAsync);
        else if (shell.Offers.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Offers.LoadAsync);
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        // Una peticion en vuelo sigue bloqueando el cierre; una orden pendiente ya NO:
        // esta persistida cifrada y se recupera al reconectar (cola durable D3.3).
        if (shell.Busy)
        {
            e.Cancel = true;
            shell.Status = "Hay una operación en curso. Espera a que termine antes de cerrar.";
            return;
        }
        shell.DisposeConnections();
    }
}
