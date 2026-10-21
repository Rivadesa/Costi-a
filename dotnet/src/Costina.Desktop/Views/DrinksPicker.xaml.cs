using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Costina.Desktop.Views;

// C3b: «Añadir consumicion» (DrinksViewModel). Solo presentacion, nada decide: al abrirse, el cursor va al buscador (Bebidas -> 3-4 letras
// -> Copa -> Enviar, sin tocar el raton para escribir) y la rueda del raton sobre una fila de fichas la desplaza a lo ancho.
public partial class DrinksPicker : UserControl
{
    public DrinksPicker()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(DispatcherPriority.Input, () => { if (DrinksSearch.IsVisible) DrinksSearch.Focus(); });
        };
    }

    private void WheelSideways(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer row || row.ScrollableWidth <= 0) return;
        row.ScrollToHorizontalOffset(row.HorizontalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
}
