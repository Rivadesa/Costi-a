using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Costina.Desktop;

// C1: propiedades adjuntas de presentacion que usan las plantillas de Themes/ (icono y texto de ayuda de un boton, elemento de la
// barra lateral resaltado, barra lateral plegada). Solo pintan: ninguna decide que se puede hacer.
public static class Ui
{
    // Geometria de trazo (Themes/Icons.xaml) que pinta la plantilla junto al texto.
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(Ui), new FrameworkPropertyMetadata(null));
    public static Geometry? GetIcon(DependencyObject element) => (Geometry?)element.GetValue(IconProperty);
    public static void SetIcon(DependencyObject element, Geometry? value) => element.SetValue(IconProperty, value);

    // Segunda linea de un acceso (descripcion corta bajo el titulo).
    public static readonly DependencyProperty HintProperty = DependencyProperty.RegisterAttached(
        "Hint", typeof(string), typeof(Ui), new FrameworkPropertyMetadata(null));
    public static string? GetHint(DependencyObject element) => (string?)element.GetValue(HintProperty);
    public static void SetHint(DependencyObject element, string? value) => element.SetValue(HintProperty, value);

    // C2b: titulo de un vacio explicado (EmptyState: "No hay cuentas abiertas"); el texto que explica va en Content.
    public static readonly DependencyProperty HeadingProperty = DependencyProperty.RegisterAttached(
        "Heading", typeof(string), typeof(Ui), new FrameworkPropertyMetadata(null));
    public static string? GetHeading(DependencyObject element) => (string?)element.GetValue(HeadingProperty);
    public static void SetHeading(DependencyObject element, string? value) => element.SetValue(HeadingProperty, value);

    // Elemento de la barra lateral que corresponde a la vista abierta (lo fija el estilo comparando Tag con CurrentView).
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));
    public static bool GetCurrent(DependencyObject element) => (bool)element.GetValue(CurrentProperty);
    public static void SetCurrent(DependencyObject element, bool value) => element.SetValue(CurrentProperty, value);

    // C2b (revision): una lista cuya eleccion la mueve el modelo ("Ver cuenta" desde el Comedor, un alta confirmada, "Ver todos", una mesa
    // que cambio de sala) desplaza la fila elegida a la vista; antes la ficha mostraba una fila que la lista tenia fuera de pantalla. Solo
    // pinta: no toca la eleccion. La guarda de OriginalSource deja pasar los SelectionChanged de los desplegables y listas de dentro de
    // cada fila, que suben hasta la lista.
    public static readonly DependencyProperty KeepSelectionInViewProperty = DependencyProperty.RegisterAttached(
        "KeepSelectionInView", typeof(bool), typeof(Ui), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not ListBox list) return;
            list.SelectionChanged -= Reveal;
            if ((bool)e.NewValue) list.SelectionChanged += Reveal;
        }));
    public static bool GetKeepSelectionInView(DependencyObject element) => (bool)element.GetValue(KeepSelectionInViewProperty);
    public static void SetKeepSelectionInView(DependencyObject element, bool value) => element.SetValue(KeepSelectionInViewProperty, value);
    private static void Reveal(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: { } item } list || !ReferenceEquals(e.OriginalSource, list)) return;
        list.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { if (ReferenceEquals(list.SelectedItem, item) && list.IsVisible) list.ScrollIntoView(item); });
    }

    // Barra lateral plegada a iconos: se fija una vez en la barra y la heredan todos sus elementos.
    public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached(
        "Compact", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetCompact(DependencyObject element) => (bool)element.GetValue(CompactProperty);
    public static void SetCompact(DependencyObject element, bool value) => element.SetValue(CompactProperty, value);
}

// C1: la vista de un elemento de navegacion (su Tag) es la abierta (CurrentView del cascaron).
public sealed class SameViewConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string tag && values[1] is string current && tag == current;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

// E3.2: hueco de una imagen que aun no hay: visible con null, recogido con valor. Con Invert, al reves (la propia imagen).
public sealed class NullToVisibility : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is null) != Invert ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

// C1: un texto vacio no ocupa sitio (avisos que solo aparecen cuando hay algo que decir).
public sealed class Text2Vis : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
