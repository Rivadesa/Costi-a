using CommunityToolkit.Mvvm.ComponentModel;

namespace Costina.Desktop.ViewModels;

// C2b: una opcion de unos botones segmentados o de unas pestanas internas (estilo Segmented de Components.xaml): formas de pago en
// Caja; Productos, Categorias, Tarifas e Impuestos en el Catalogo... Objeto ESTABLE con su texto y su marca de elegida (IsCurrent), que
// es lo que pinta la plantilla. La eleccion la guarda el viewmodel como codigo (Id) y Mark la refleja: la seleccion de la lista no manda,
// porque se pierde al rehacer la lista (la misma leccion que las pestanas de sala del Comedor, ZoneTab).
public sealed partial class SegmentChoice(string id, string text) : ObservableObject
{
    public string Id { get; } = id;
    [ObservableProperty] private string label = text;
    [ObservableProperty] private bool isCurrent;

    public static void Mark(IEnumerable<SegmentChoice> choices, string? current)
    {
        foreach (var choice in choices) choice.IsCurrent = choice.Id == current;
    }
}
