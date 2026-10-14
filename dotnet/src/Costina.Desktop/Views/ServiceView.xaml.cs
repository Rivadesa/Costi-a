using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Comedor y cocina (modulo Dining).
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class ServiceView : UserControl
{
    public ServiceView() => InitializeComponent();
}
