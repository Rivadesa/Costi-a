using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop.Views;

// C1: ERP > Catalogo y tarifas (nucleo, E3, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
// C2b: pestanas, listas y fichas con las piezas comunes de Components.xaml. Solo presentacion, nada decide: pulsar una presentacion de
// la tabla (o "Presentación" para una nueva) trae a la vista su ficha con "Fijar precio", que queda debajo de la tabla; elegir OTRO
// producto muestra su ficha desde arriba (releer el mismo, tras una orden, no la mueve). La eleccion la sigue haciendo la lista.
public partial class CatalogView : UserControl
{
    private string? shownProduct;

    public CatalogView() => InitializeComponent();

    private void ShowPresentationEditor(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { if (PresentationEditor.IsVisible) PresentationEditor.BringIntoView(); });

    private void ProductChosen(object sender, SelectionChangedEventArgs e)
    {
        if ((ProductList.SelectedItem as ProductRow)?.Product.Id is not { } id || id == shownProduct) return;
        shownProduct = id; ProductScroll.ScrollToHome();
    }
}
