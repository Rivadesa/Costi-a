using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    public MainWindow()
    {
        InitializeComponent(); DataContext = shell;
        // Al conectar (o cambiar de rol) el area de trabajo muestra la primera vista que ese rol puede ver.
        shell.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ShellViewModel.Session)) SelectFirstVisibleView(); };
    }

    internal void SelectFirstVisibleView()
    {
        if (Tabs.SelectedItem is TabItem current && current.Visibility == Visibility.Visible) return;
        Tabs.SelectedItem = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible);
    }

    // Menu: cada entrada selecciona una vista del area de trabajo por su nombre (Tag).
    private void ShowView(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string name } && FindName(name) is TabItem view) Tabs.SelectedItem = view;
    }
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
        shell.Checkout.Visible = tabs.SelectedItem == CheckoutTab;
        shell.Devices.Visible = tabs.SelectedItem == DevicesTab;
        shell.Organization.Visible = tabs.SelectedItem == OrganizationTab;
        shell.Catalog.Visible = tabs.SelectedItem == CatalogTab;
        shell.Offers.Visible = tabs.SelectedItem == OffersTab;
        if (shell.Checkout.Visible && shell.IsMain && !shell.Busy)
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
