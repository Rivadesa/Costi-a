using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Costina.Desktop.ViewModels;

namespace Costina.Desktop.Views;

// C1: ERP > Oferta: cartas y menus (nucleo, E4a, solo main).
// Vista extraida de MainWindow (C1): hereda el DataContext del cascaron y no tiene logica propia; el aspecto sale de Themes/.
// C2b: lista y ficha con las piezas comunes de Components.xaml. Solo presentacion, nada decide: elegir OTRA oferta muestra su ficha desde
// arriba (releer la misma, tras una orden, no la mueve); pulsar un pase trae a la vista el pase desplegado; pulsar un plato (o "Plato" para
// uno nuevo) trae a la vista su ficha, debajo de la tabla; "Pase" trae a la vista el alta del pase, al final de la lista. La eleccion la
// siguen haciendo la lista y las ordenes del viewmodel.
public partial class OffersView : UserControl
{
    private string? shownOffer;

    public OffersView() => InitializeComponent();

    private void OfferChosen(object sender, SelectionChangedEventArgs e)
    {
        if ((OfferList.SelectedItem as OfferRow)?.Offer.Id is not { } id || id == shownOffer) return;
        shownOffer = id; OfferScroll.ScrollToHome();
    }

    // El pase desplegado puede quedar mas arriba o mas abajo que la fila pulsada (el anterior se pliega): se trae a la vista su cabecera y
    // lo que quepa de sus platos.
    private void CoursePicked(object sender, RoutedEventArgs e) => Show(SelectedCourseCard, 260);
    private void ShowDishEditor(object sender, RoutedEventArgs e) => Show(DishEditor);
    private void ShowNewCourse(object sender, RoutedEventArgs e) => Show(NewCoursePanel);

    private void Show(FrameworkElement element, double? height = null) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
    {
        if (!element.IsVisible) return;
        element.BringIntoView(new Rect(0, 0, element.ActualWidth, height is { } h ? Math.Min(h, element.ActualHeight) : element.ActualHeight));
    });
}
