using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Dispositivos (nucleo, solo main; corte A): conectar comanderos y pantallas de cocina.
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class DevicesView : UserControl
{
    public DevicesView() => InitializeComponent();
}
