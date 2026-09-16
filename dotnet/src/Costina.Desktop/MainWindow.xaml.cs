using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop;

// Solo pegamento de presentacion: DataContext, PasswordBox (no enlazable a proposito, la clave
// no pasa por bindings ni se guarda), pestana de cuenta y guardas de cierre. Cero reglas de negocio:
// toda la habilitacion viene de las affordances del servidor a traves de los viewmodels.
public partial class MainWindow : Window
{
    private readonly ShellViewModel shell = new();

    public MainWindow() { InitializeComponent(); DataContext = shell; }

    private async void ConnectClick(object sender, RoutedEventArgs e)
    {
        await shell.ConnectWithKey(AccessKey.Password);
        if (shell.Connected) AccessKey.Clear();
    }

    private async void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl tabs) return;
        shell.Checkout.Visible = tabs.SelectedItem == CheckoutTab;
        if (shell.Checkout.Visible && shell.IsMain && !shell.Busy)
            await shell.Run(shell.Checkout.LoadAsync);
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (shell.Busy || shell.HasPending)
        {
            e.Cancel = true;
            shell.Status = "Hay una operación en curso o sin confirmar. Comprueba/reintenta antes de cerrar. Un cierre forzado pierde el reintento de esta sesión.";
            return;
        }
        shell.DisposeConnections();
    }
}
