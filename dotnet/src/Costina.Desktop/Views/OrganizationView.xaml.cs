using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop.Views;

// C1: Configuracion: salas, mesas y estaciones (nucleo, E2, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
// C2b: pestanas, listas y fichas con las piezas comunes de Components.xaml. Solo presentacion, nada decide: el ancho de las tarjetas de
// mesa se reparte en columnas iguales segun el sitio del centro (dos con la barra lateral desplegada a 1.280 px; mas si se pliega o la
// ventana crece), como el plano del Comedor; y pulsar otra vez la sala elegida vuelve a su ficha desde la de una mesa (la orden de la vista,
// ShowZone, solo cambia la eleccion).
public partial class OrganizationView : UserControl
{
    public const double MinTile = 140, TileGap = 10, ScrollAllowance = 18;
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(OrganizationView),
        new FrameworkPropertyMetadata(MinTile + TileGap));
    // Ancho de cada hueco de las mesas (tarjeta + su separacion a la derecha). Lo usa el WrapPanel de la lista (ItemWidth).
    public double TileWidth { get => (double)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }

    public OrganizationView() => InitializeComponent();

    // Se reserva siempre el sitio de la barra de desplazamiento: asi aparecer o no aparecer no cambia el numero de columnas (sin vaivenes).
    private void TablesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = Math.Max(0, e.NewSize.Width - ScrollAllowance) + TileGap;
        var columns = Math.Max(2, (int)(width / (MinTile + TileGap)));
        TileWidth = Math.Floor(width / columns);
    }

    private void ZoneClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ShellViewModel { Organization: var organization } || e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(ZoneList, source) is ListBoxItem { DataContext: ZoneRow row } && Equals(row.Zone, organization.SelectedZone)
            && organization.ShowZoneCommand.CanExecute(null))
            organization.ShowZoneCommand.Execute(null);
    }
}
