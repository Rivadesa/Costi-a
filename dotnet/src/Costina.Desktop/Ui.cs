using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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

    // Elemento de la barra lateral que corresponde a la vista abierta (lo fija el estilo comparando Tag con CurrentView).
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.RegisterAttached(
        "Current", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));
    public static bool GetCurrent(DependencyObject element) => (bool)element.GetValue(CurrentProperty);
    public static void SetCurrent(DependencyObject element, bool value) => element.SetValue(CurrentProperty, value);

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

// C1: un texto vacio no ocupa sitio (avisos que solo aparecen cuando hay algo que decir).
public sealed class Text2Vis : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
