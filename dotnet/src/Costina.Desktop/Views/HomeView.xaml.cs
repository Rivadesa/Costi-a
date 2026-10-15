using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Costina.Desktop.Views;

// C1: Inicio (corte A): estado, accesos y dispositivos. Vista extraida de MainWindow: hereda el DataContext del cascaron; el aspecto sale de Themes/.
// Pieza propia: los accesos (GoToPage) se habilitan desde que nacen; quien navega es la ventana (ver HomeView.xaml).
// C2: Inicio como panel. Otra pieza de presentacion: las columnas del mini plano ("Sala ahora") segun el ancho de su tarjeta (cinco con la
// barra lateral desplegada a 1.280 px; mas si se pliega, la ventana crece o no hay tarjeta de dispositivos al lado).
public partial class HomeView : UserControl
{
    public const double MiniTile = 104, MiniGap = 8;
    public static readonly DependencyProperty MiniColumnsProperty = DependencyProperty.Register(nameof(MiniColumns), typeof(int), typeof(HomeView),
        new FrameworkPropertyMetadata(5));
    // Columnas del mini plano (UniformGrid): tantas como mesas de MiniTile px quepan, y nunca menos de tres.
    public int MiniColumns { get => (int)GetValue(MiniColumnsProperty); set => SetValue(MiniColumnsProperty, value); }

    public HomeView() => InitializeComponent();
    private void CanGoToPage(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

    private void MiniPlanSizeChanged(object sender, SizeChangedEventArgs e) =>
        MiniColumns = Math.Max(3, (int)((e.NewSize.Width + MiniGap) / (MiniTile + MiniGap)));
}
