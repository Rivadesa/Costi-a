using System.Windows.Controls;
using System.Windows.Input;

namespace Costina.Desktop.Views;

// C1: Inicio (corte A): estado, accesos y dispositivos. Vista extraida de MainWindow: hereda el DataContext del cascaron; el aspecto sale de Themes/.
// Unica pieza propia: los accesos (GoToPage) se habilitan desde que nacen; quien navega es la ventana (ver HomeView.xaml).
public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
    private void CanGoToPage(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
}
