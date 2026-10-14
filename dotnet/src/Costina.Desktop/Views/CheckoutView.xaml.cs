using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Cuenta / Caja (nucleo, solo main).
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class CheckoutView : UserControl
{
    public CheckoutView() => InitializeComponent();
}
