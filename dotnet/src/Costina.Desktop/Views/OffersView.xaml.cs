using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: ERP > Oferta: cartas y menus (nucleo, E4a, solo main).
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class OffersView : UserControl
{
    public OffersView() => InitializeComponent();
}
