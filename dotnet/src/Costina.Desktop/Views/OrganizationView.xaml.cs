using System.Windows.Controls;

namespace Costina.Desktop.Views;

// C1: Configuracion: salas, mesas y estaciones (nucleo, E2, solo main).
// Vista extraida de MainWindow sin cambios: hereda el DataContext del cascaron y no tiene logica propia.
public partial class OrganizationView : UserControl
{
    public OrganizationView() => InitializeComponent();
}
