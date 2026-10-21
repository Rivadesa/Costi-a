using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop;

// Estados vacios: visible cuando NO hay datos (texto explicativo en lugar de una lista en blanco). Recurso NotBool2Vis de Converters.xaml.
public sealed class InvertedBool2Vis : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

// Solo pegamento de presentacion: DataContext, PasswordBox (no enlazable a proposito, la clave
// no pasa por bindings ni se guarda), vistas del area de trabajo, barra lateral (C1), sitio de la tarjeta de entrada (C1) y
// guardas de cierre. Cero reglas de negocio:
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
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Session)) SelectFirstVisibleView();
            if (e.PropertyName is nameof(ShellViewModel.ElevationMode) or null or "") PlaceLoginCard();
        };
        PlaceLoginCard();
        // Corte B: la vista sensible pedida se abre sola cuando el responsable ha entrado.
        shell.ViewRequested += name => { if (FindName(name) is TabItem view) Select(view); };
        // Corte B (B3): cualquier teclado o raton cuenta como actividad; cada minuto se comprueba si el responsable lleva
        // 15 minutos sin tocar nada y, si es asi, se vuelve al nivel Puesto.
        InputManager.Current.PreProcessInput += (_, _) => shell.NoteActivity();
        inactivity.Tick += (_, _) => shell.CheckInactivity();
        inactivity.Tick += (_, _) => shell.Service.Tick();   // C2: los minutos de las mesas del plano avanzan sin releer
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

    // Menu: cada entrada selecciona una vista del area de trabajo por su nombre (Tag).
    // C1: los botones de las vistas (accesos de Inicio) no pueden usar Click=ShowView desde un UserControl: envian
    // NavigationCommands.GoToPage con el nombre de la vista y llegan aqui por la CommandBinding de la ventana. Misma logica.
    // Corte B: un apartado sensible desde el nivel Puesto pide primero al responsable (RequestElevation); el servidor lo
    // impone ademas con 403, asi que la pantalla solo evita un rechazo seguro.
    private void ShowView(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string name }) ShowView(name);
    }
    private void CanGoToPage(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void GoToPage(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string name) ShowView(name);
    }
    private void ShowView(string name)
    {
        // C1 (revision): durante una operacion no se cambia de vista por ningun camino. Los menus y las vistas ya estan deshabilitados
        // (TabsEnabled); la barra lateral solo ignora el raton, y sin esta guarda el teclado (Tab + Intro) abria una vista que nadie leia.
        if (!shell.TabsEnabled) return;
        if (FindName(name) is not TabItem view) return;
        if (ShellViewModel.RequiresResponsible(name) && !shell.IsMain) { shell.RequestElevation(name); return; }
        Select(view);
    }
    // C3b: salir del Comedor con una ronda sin enviar pide confirmacion en la propia ficha (la ronda es un borrador en memoria: se pierde).
    // Solo presentacion: la pregunta y la ronda viven en DrinksViewModel.
    private void Select(TabItem view)
    {
        if (!ReferenceEquals(view, ServiceTab) && ReferenceEquals(Tabs.SelectedItem, ServiceTab)
            && shell.Service.Drinks.AskLeave("vas a «" + Costina.Client.Labels.View(view.Name) + "»", () => Tabs.SelectedItem = view)) return;
        Tabs.SelectedItem = view;
    }
    private void ElevateClick(object sender, RoutedEventArgs e) => shell.RequestElevation("HomeTab");
    // C1: barra lateral plegada a iconos o desplegada; el aviso de estado se puede cerrar (vuelve con el siguiente mensaje).
    // Mas estrecha que la ventana por defecto (1280 px) la barra se pliega sola: las vistas de gestion no caben con ella desplegada.
    // Si la persona la pliega o despliega a mano, manda su eleccion.
    private const double SideNavFoldBelow = 1270;
    private bool sideNavChosen;
    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!sideNavChosen) shell.SideNavCompact = e.NewSize.Width < SideNavFoldBelow;
    }
    private void ToggleSideNav(object sender, RoutedEventArgs e) { sideNavChosen = true; shell.SideNavCompact = !shell.SideNavCompact; }
    private void DismissStatus(object sender, RoutedEventArgs e) => shell.Status = "";

    // C1: la tarjeta de entrada es UNA (LoginCard, con ConnectButton, AccessKey, el desplegable de otro servidor y PairCode): sin sesion
    // vive en el paso 1 de Primeros pasos; cuando un apartado sensible la pide desde el nivel Puesto se abre centrada sobre un velo.
    // Solo cambia de sitio: sus nombres, enlaces y la contrasena escrita siguen siendo los mismos.
    private void PlaceLoginCard()
    {
        var host = shell.ElevationMode ? ElevationLoginHost : FirstRunLoginHost;
        if (ReferenceEquals(LoginCard.Parent, host)) return;
        if (LoginCard.Parent is Decorator previous) previous.Child = null;
        host.Child = LoginCard;
        if (shell.ElevationMode) Dispatcher.BeginInvoke(() => AccessKey.Focus(), DispatcherPriority.Input);
    }
    private void AccessKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ConnectClick(sender, e); }
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void AboutClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        $"Costiña · puesto principal\nVersión {ShellViewModel.AppVersion}\n\nServidor: {shell.Endpoint}\n{shell.ConnectionText}",
        "Acerca de Costiña", MessageBoxButton.OK, MessageBoxImage.Information);

    private async void ConnectClick(object sender, RoutedEventArgs e)
    {
        await shell.LoginWithPassword(AccessKey.Password);
        if (shell.Connected) AccessKey.Clear();
    }

    private async void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        // C1: solo el cambio de vista del area de trabajo. Cualquier selector interior (ComboBox, ListBox, DataGrid o un
        // TabControl anidado en una vista) burbujea hasta aqui con su propio OriginalSource y se ignora.
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return;
        // C1: la barra lateral resalta la vista que de verdad se muestra (no el boton pulsado).
        shell.CurrentView = (Tabs.SelectedItem as TabItem)?.Name;
        shell.Home.Visible = Tabs.SelectedItem == HomeTab;
        shell.Checkout.Visible = Tabs.SelectedItem == CheckoutTab;
        shell.Devices.Visible = Tabs.SelectedItem == DevicesTab;
        shell.Organization.Visible = Tabs.SelectedItem == OrganizationTab;
        shell.Catalog.Visible = Tabs.SelectedItem == CatalogTab;
        shell.Offers.Visible = Tabs.SelectedItem == OffersTab;
        if (shell.Home.Visible && shell.Connected && !shell.Busy)
            await shell.Run(shell.Home.LoadAsync);
        else if (Tabs.SelectedItem is TabItem { Name: var selected } && ShellViewModel.RequiresResponsible(selected) && !shell.IsMain)
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
