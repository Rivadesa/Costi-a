using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Configuracion: salas, mesas y estaciones (nucleo, E2, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
public partial class OrganizationView : UserControl
{
    public OrganizationView() => InitializeComponent();
}
