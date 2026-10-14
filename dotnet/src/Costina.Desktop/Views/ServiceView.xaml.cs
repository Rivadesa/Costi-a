using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Comedor y cocina (modulo Dining).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
public partial class ServiceView : UserControl
{
    public ServiceView() => InitializeComponent();
}
