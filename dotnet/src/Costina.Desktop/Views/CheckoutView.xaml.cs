using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Cuenta / Caja (nucleo, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
public partial class CheckoutView : UserControl
{
    public CheckoutView() => InitializeComponent();
}
