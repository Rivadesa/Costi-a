using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: ERP > Catalogo y tarifas (nucleo, E3, solo main).
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class CatalogView : UserControl
{
    public CatalogView() => InitializeComponent();
}
