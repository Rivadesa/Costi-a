using System.Windows;
using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Comedor y cocina (modulo Dining). Vista extraida de MainWindow: hereda el DataContext del cascaron; el aspecto sale de Themes/.
// C2: plano de mesas y ficha. Unica pieza propia, de presentacion: el ancho de las tarjetas se reparte en columnas iguales segun el
// sitio que deja la ficha (tres con la barra lateral desplegada a 1.280 px; mas si se pliega o la ventana crece).
public partial class ServiceView : UserControl
{
    public const double MinTile = 176, TileGap = 10, ScrollAllowance = 18;
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(ServiceView),
        new FrameworkPropertyMetadata(MinTile + TileGap));
    // Ancho de cada hueco del plano (tarjeta + su separacion a la derecha). Lo usa el WrapPanel de cada sala (ItemWidth).
    public double TileWidth { get => (double)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }

    public ServiceView() => InitializeComponent();

    // Se reserva siempre el sitio de la barra de desplazamiento: asi aparecer o no aparecer no cambia el numero de columnas (sin vaivenes).
    private void FloorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(0, e.NewSize.Width - ScrollAllowance) + TileGap;
        var columns = Math.Max(2, (int)(width / (MinTile + TileGap)));
        TileWidth = Math.Floor(width / columns);
    }
}
