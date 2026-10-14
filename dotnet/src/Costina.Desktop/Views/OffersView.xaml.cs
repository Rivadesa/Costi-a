using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: ERP > Oferta: cartas y menus (nucleo, E4a, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
public partial class OffersView : UserControl
{
    public OffersView() => InitializeComponent();
}
