using System.Windows;
using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Cuenta / Caja (nucleo, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
// C2b: lista de cuentas y ficha, con las piezas comunes de Components.xaml; sigue sin logica propia (ShowRefund solo desplaza y enfoca).
public partial class CheckoutView : UserControl
{
    public CheckoutView() => InitializeComponent();
    private void ShowRefund(object sender, RoutedEventArgs e) { OtherOperations.BringIntoView(); NoteBox.Focus(); }
}
