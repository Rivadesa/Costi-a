using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E3: vista "ERP › Catálogo y tarifas" (solo rol main; el servidor lo impone ademas por permisos): categorias, productos con
// sus presentaciones y precios por tarifa, tarifas e impuestos. Patron lista + ficha de E2. Cada guardado es UN comando por la
// tuberia habitual (orden incierta persistida antes de enviar) y despues se relee el catalogo completo: la pantalla nunca
// muestra un estado que el servidor no haya confirmado. Nada se borra: se desactiva y se puede reactivar.
// C1: las listas y los titulos de las fichas nombran por el nombre; el codigo solo aparece en el campo "Código" de cada ficha.
// C2b (plan 2026-10-14): pestanas Productos · Categorias · Tarifas · Impuestos (Segmented, con su recuento; la elegida sale de
// IsCurrent) y, en cada una, lista a la izquierda y ficha a la derecha, como el Comedor. Productos: buscador, filtro de categoria (la
// categoria y sus subcategorias) y "Ver todos"; la ficha lleva la foto, los datos, las presentaciones con su precio en cada tarifa y
// "Fijar precio". El filtro de la lista de productos es independiente de la categoria elegida en su pestana. Nada de eso decide: cada
// boton sigue con su orden, su cuerpo y su CanExecute de siempre; la ficha se compone solo de lo leido (catalogo y salas de la
// organizacion) y sin nada elegido dice que elegir.
public sealed record CategoryRow(CategoryDto Category, int Depth)
{
    public string StateLabel => Labels.Active(Category.Active, feminine: true);
    // C1 (revision): la lista pinta Label con una sangria de margen (Indent), en la letra de la aplicacion; el ToString con espacios
    // queda de respaldo (y para los checks).
    public string Label => Category.Name + " · " + StateLabel;
    public Thickness Indent => new(Depth * 18, 0, 0, 0);
    // C2b: fila del arbol: ruta completa (para la ficha y los desplegables), detalle (productos, subcategorias y, si lo esta,
    // desactivada) y muestra de su color (#RRGGBB; null sin color o con uno que no se entiende).
    public string Path { get; init; } = Category.Name;
    public string Detail { get; init; } = "";
    public Brush? Swatch { get; init; }
    public bool HasParent => Depth > 0;
    public bool Inactive => !Category.Active;
    public override string ToString() => new string(' ', Depth * 4) + Label;
}
// C2b: una categoria en un desplegable. Abierto, el arbol con sangria (ListText, con Padding); cerrado, la ruta (BoxText), porque un
// nombre se repite en el arbol (Rioja en Tintos y en Blancos, E3.1). Id "" = "Todas las categorias" o "Sin categoria".
public sealed record CategoryChoice(string Id, string ListText, string BoxText, int Depth)
{
    public Thickness Padding => new(11 + Depth * 16, 5, 11, 7);
}
// E3.2: la fila pinta una miniatura de 32 px junto al nombre. Se pide solo cuando la fila llega a pintarse (lista virtualizada: el
// enlace lee Thumbnail al crear o reciclar su contenedor) y una sola vez por huella; mientras tanto se ve el hueco con su icono.
public sealed class ProductRow : ObservableObject
{
    private readonly ImageLoader? images;
    private BitmapSource? thumbnail;
    private bool requested;
    public ProductRow(ProductDto product, string categoryName, string priceText, ImageLoader? images = null)
    {
        Product = product; CategoryName = categoryName; PriceText = priceText; this.images = images;
        if (product.ImageSha is { } sha) thumbnail = images?.Thumbnail(sha);   // ya decodificada: aparece sin esperar
    }
    public ProductDto Product { get; }
    public string CategoryName { get; }   // C2b: ruta corta de su categoria (padre › categoria)
    public string PriceText { get; }
    public string StateLabel => Labels.Active(Product.Active, feminine: false);
    // C2b: a la derecha del nombre, el precio General de su primera presentacion activa ("Sin precio" si no lo tiene: no se vende);
    // un producto desactivado dice "Desactivado" en ese sitio. Debajo, la categoria y cuantas presentaciones activas tiene.
    public string MainPrice { get; init; } = "";
    public bool PriceMissing { get; init; }
    public int PresentationCount { get; init; }
    public bool Inactive => !Product.Active;
    public string Detail => CategoryName + " · " + (PresentationCount == 0 ? "sin presentaciones activas" : Labels.Plural(PresentationCount, "presentación", "presentaciones"));
    public string Tip => "Precio General: " + PriceText;
    public BitmapSource? Thumbnail
    {
        get
        {
            if (thumbnail is null && !requested && images is not null && Product.ImageSha is { } sha)
            {
                requested = true;
                images.RequestThumbnail(Product.Id, sha, image => { if (image is null) return; thumbnail = image; OnPropertyChanged(nameof(Thumbnail)); });
            }
            return thumbnail;
        }
    }
    public override string ToString() => $"{Product.Name} · {CategoryName} · {PriceText} · {StateLabel}";
}
// C2b: celda de precio de una presentacion en una tarifa. Tone: "" con precio vigente; "missing" sin precio en la General (no se vende);
// "fallback" sin precio propio en otra tarifa (se cobra el de la General).
public sealed record PriceCell(string Text, string Tone);
public sealed record PresentationRow(PresentationDto Presentation, string PriceText)
{
    public string StateLabel => Labels.Active(Presentation.Active, feminine: true);
    public PriceCell[] Cells { get; init; } = [];
    public bool Inactive => !Presentation.Active;
    public override string ToString() => $"{Presentation.Name} · {PriceText} · {StateLabel}";
}
// C2b: filas de las listas de tarifas e impuestos (la eleccion sigue siendo el DTO: SelectedTariff y SelectedTax).
public sealed record TariffRow(TariffDto Tariff, string Detail) { public bool Inactive => !Tariff.Active; public override string ToString() => Tariff.ToString(); }
public sealed record TaxRow(TaxDto Tax, string Detail) { public bool Inactive => !Tax.Active; public override string ToString() => Tax.ToString(); }

public sealed partial class CatalogViewModel(ShellViewModel shell) : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool rendering;
    private CatalogDto? catalog;
    // C2b: salas leidas con la organizacion (su tarifa propia), para decir que salas usan cada tarifa; null = sin leer.
    private ZoneDto[]? zones;
    // C2b: lo recien creado (confirmado por el servidor) queda elegido tras releer el catalogo.
    private string? createdCategory, createdProduct, createdPresentation, createdTariff, createdTax;
    // E3.2: fotos de los articulos. Los bytes los pide la conexion abierta (con su credencial y su cache por huella); sin conexion no
    // hay nada que pedir. Se decodifican fuera del hilo de la ventana (ImageLoader).
    private readonly ImageLoader images = new(async (productId, sha) => shell.Api is { } api ? (await api.GetProductImageAsync(productId, sha))?.Content : null,
        System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
    private int photoGeneration;
    public ImageLoader Images => images;
    // Foto de la ficha (null = hueco con icono y PhotoText) y el texto del hueco.
    [ObservableProperty] private BitmapSource? productPhoto;
    [ObservableProperty] private string photoText = "Sin producto seleccionado.";
    // E3.2: dialogo de Windows filtrado a JPEG, PNG y WebP (devuelve la ruta o null). Los checks lo sustituyen: no abren dialogos.
    public Func<string?> PickImageFile { get; set; } = () =>
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Imagen del producto (JPEG, PNG o WebP, hasta 2 MB)",
            Filter = "Imágenes JPEG, PNG o WebP (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
            CheckFileExists = true, Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    };

    [ObservableProperty] private CategoryRow[] categories = [];
    [ObservableProperty] private CategoryRow? selectedCategory;
    [ObservableProperty] private ProductRow[] products = [];
    [ObservableProperty] private ProductRow? selectedProduct;
    [ObservableProperty] private PresentationRow[] presentations = [];
    [ObservableProperty] private PresentationRow? selectedPresentation;
    [ObservableProperty] private TariffDto[] tariffs = [];
    [ObservableProperty] private TariffDto? selectedTariff;
    [ObservableProperty] private TaxDto[] taxes = [];
    [ObservableProperty] private TaxDto? selectedTax;
    [ObservableProperty] private string search = "";
    // "Ver todos los productos": sin filtro de categoria. El filtro (la categoria y sus subcategorias) es CategoryFilter.
    [ObservableProperty] private bool allCategories = true;
    // Fichas. NewX = alta: el codigo se teclea; en edicion el codigo es inmutable.
    [ObservableProperty] private bool newCategory; [ObservableProperty] private string categoryId = ""; [ObservableProperty] private string categoryName = "";
    [ObservableProperty] private string categoryParentId = ""; [ObservableProperty] private string categoryColor = ""; [ObservableProperty] private string categorySort = "0";
    [ObservableProperty] private bool newProduct; [ObservableProperty] private string productId = ""; [ObservableProperty] private string productName = "";
    [ObservableProperty] private string productCategoryId = ""; [ObservableProperty] private string productTaxId = "iva-10"; [ObservableProperty] private string productReference = "";
    [ObservableProperty] private string productStationId = "";   // E4b: estacion de cocina (vacio = bebida o consumo directo)
    [ObservableProperty] private StationDto[] stations = [];
    [ObservableProperty] private string productSort = "0"; [ObservableProperty] private string productPresentationName = "Unidad";
    [ObservableProperty] private bool newPresentation; [ObservableProperty] private string presentationId = ""; [ObservableProperty] private string presentationName = ""; [ObservableProperty] private string presentationSort = "0";
    [ObservableProperty] private string priceTariffId = "general"; [ObservableProperty] private string priceValidFrom = ""; [ObservableProperty] private string priceText = "";
    [ObservableProperty] private bool newTariff; [ObservableProperty] private string tariffId = ""; [ObservableProperty] private string tariffName = ""; [ObservableProperty] private string tariffSort = "0";
    [ObservableProperty] private bool newTax; [ObservableProperty] private string taxId = ""; [ObservableProperty] private string taxName = ""; [ObservableProperty] private string taxRate = "10";
    [ObservableProperty] private string summary = "Sin datos de catálogo todavía.";

    // ---- C2b: pestanas internas (Segmented); la elegida es Section y Mark la pinta ----
    public SegmentChoice[] Sections { get; } = SectionChoices();
    private static SegmentChoice[] SectionChoices()
    {
        var choices = new[] { new SegmentChoice("products", "Productos"), new SegmentChoice("categories", "Categorías"), new SegmentChoice("tariffs", "Tarifas"), new SegmentChoice("taxes", "Impuestos") };
        SegmentChoice.Mark(choices, "products");
        return choices;
    }
    private string section = "products";
    // Null o un codigo desconocido (la lista de pestanas perdiendo su eleccion) no cambian de pestana.
    public string? Section
    {
        get => section;
        set
        {
            if (value is null || value == section || !Sections.Any(s => s.Id == value)) return;
            section = value; SegmentChoice.Mark(Sections, value);
            foreach (var name in new[] { nameof(Section), nameof(ShowProducts), nameof(ShowCategories), nameof(ShowTariffs), nameof(ShowTaxes) }) OnPropertyChanged(name);
        }
    }
    public bool ShowProducts => section == "products";
    public bool ShowCategories => section == "categories";
    public bool ShowTariffs => section == "tariffs";
    public bool ShowTaxes => section == "taxes";

    // ---- C2b: lista de productos (filtro de categoria, recuento y vacio explicado) ----
    private static readonly CategoryChoice AllChoice = new("", "Todas las categorías", "Todas las categorías", 0);
    [ObservableProperty] private CategoryChoice[] filterChoices = [AllChoice];
    private string? filterCategoryId;
    // Codigo de la categoria que filtra la lista ("" = todas). Null es el desplegable perdiendo su eleccion al rehacer su lista: no
    // cambia el filtro. Elegir una categoria muestra sus productos y los de sus subcategorias; la eleccion de la lista se conserva por
    // codigo mientras se vea (como al buscar).
    public string? CategoryFilter
    {
        get => repushingFilter ? null : AllCategories || filterCategoryId is null ? "" : filterCategoryId;
        set { if (value is not null && !repushingFilter) SetFilter(value.Length == 0 ? null : value); }
    }
    private bool repushingFilter;
    // El desplegable del filtro pierde su eleccion al rehacer su lista y no la recupera si el filtro sigue igual (WPF no avisa de un
    // valor que no cambia): se le vuelve a dar, primero sin eleccion y despues la de siempre.
    private void RepushFilter()
    {
        repushingFilter = true; try { OnPropertyChanged(nameof(CategoryFilter)); } finally { repushingFilter = false; }
        OnPropertyChanged(nameof(CategoryFilter));
    }
    // Buscador, filtro y recuento solo cuando hay productos (sin ninguno, la lista es solo el vacio explicado).
    [ObservableProperty] private bool showProductTools;
    [ObservableProperty] private string productCount = "";
    [ObservableProperty] private bool isFiltered;
    [ObservableProperty] private bool showAllLink;
    [ObservableProperty] private bool showProductListEmpty;
    [ObservableProperty] private string productListHeading = "";
    [ObservableProperty] private string productListText = "";
    [ObservableProperty] private bool showCreateProduct;
    [ObservableProperty] private bool showShowAll;

    // ---- C2b: ficha del producto (solo de lo leido y de lo elegido) ----
    [ObservableProperty] private bool hasProductContext;
    [ObservableProperty] private string productEmptyHeading = "";
    [ObservableProperty] private string productEmptyText = "";
    [ObservableProperty] private string productContext = "";
    [ObservableProperty] private string productTitle = "";
    [ObservableProperty] private string productStateText = "";
    [ObservableProperty] private string productStateTone = "other";
    [ObservableProperty] private string productWarnText = "";
    [ObservableProperty] private string productSaveText = "Guardar cambios";
    [ObservableProperty] private bool productDeactivates;
    [ObservableProperty] private bool productReactivates;
    [ObservableProperty] private string[] priceColumns = [];
    [ObservableProperty] private string priceNote = "";
    [ObservableProperty] private bool showPresentations;
    [ObservableProperty] private bool presentationsEmpty;
    [ObservableProperty] private bool hasPresentationEditor;
    [ObservableProperty] private string presentationSaveText = "Guardar presentación";
    [ObservableProperty] private bool presentationDeactivates;
    [ObservableProperty] private bool presentationReactivates;
    [ObservableProperty] private string scheduledText = "";

    // ---- C2b: categorias ----
    [ObservableProperty] private string categoryCount = "";
    [ObservableProperty] private bool showCategoryListEmpty;
    [ObservableProperty] private string categoryListHeading = "";
    [ObservableProperty] private string categoryListText = "";
    [ObservableProperty] private bool hasCategoryContext;
    [ObservableProperty] private string categoryEmptyHeading = "";
    [ObservableProperty] private string categoryEmptyText = "";
    [ObservableProperty] private string categoryContext = "";
    [ObservableProperty] private string categoryTitle = "";
    [ObservableProperty] private string categoryStateText = "";
    [ObservableProperty] private string categoryStateTone = "other";
    [ObservableProperty] private string categorySaveText = "Guardar cambios";
    [ObservableProperty] private bool categoryDeactivates;
    [ObservableProperty] private bool categoryReactivates;
    [ObservableProperty] private string categoryProductsFigure = "";
    [ObservableProperty] private string categoryChildrenFigure = "";
    [ObservableProperty] private string categoryChildrenText = "";
    [ObservableProperty] private Brush? categorySwatch;

    // ---- C2b: tarifas ----
    [ObservableProperty] private TariffRow[] tariffItems = [];
    [ObservableProperty] private bool hasTariffContext;
    [ObservableProperty] private string tariffEmptyHeading = "";
    [ObservableProperty] private string tariffEmptyText = "";
    [ObservableProperty] private string tariffContext = "";
    [ObservableProperty] private string tariffTitle = "";
    [ObservableProperty] private string tariffStateText = "";
    [ObservableProperty] private string tariffStateTone = "other";
    [ObservableProperty] private bool isGeneralTariff;
    [ObservableProperty] private string tariffSaveText = "Guardar cambios";
    [ObservableProperty] private bool tariffDeactivates;
    [ObservableProperty] private bool tariffReactivates;
    [ObservableProperty] private bool hasZoneInfo;
    [ObservableProperty] private string[] tariffZones = [];
    [ObservableProperty] private string tariffZonesText = "";
    [ObservableProperty] private string tariffPricedFigure = "";
    [ObservableProperty] private string tariffUnpricedFigure = "";
    [ObservableProperty] private string tariffUnpricedLabel = "";
    [ObservableProperty] private string tariffPricesText = "";
    [ObservableProperty] private string tariffNote = "";

    // ---- C2b: impuestos ----
    [ObservableProperty] private TaxRow[] taxItems = [];
    [ObservableProperty] private bool showTaxListEmpty;
    [ObservableProperty] private bool hasTaxContext;
    [ObservableProperty] private string taxEmptyHeading = "";
    [ObservableProperty] private string taxEmptyText = "";
    [ObservableProperty] private string taxTitle = "";
    [ObservableProperty] private string taxStateText = "";
    [ObservableProperty] private string taxStateTone = "other";
    [ObservableProperty] private string taxSaveText = "Guardar cambios";
    [ObservableProperty] private bool taxDeactivates;
    [ObservableProperty] private bool taxReactivates;
    [ObservableProperty] private string taxProductsFigure = "";
    [ObservableProperty] private string taxProductsText = "";

    public bool Visible { get; set; }
    // Desplegables de las fichas (C2b: la ruta en el cuadro cerrado y el arbol al abrirlo). La primera entrada (codigo vacio) es "sin
    // categoria" / "ninguna". Llevan las activas y, solo si lo elegido usa una desactivada, esa tambien, marcada: asi el desplegable
    // nunca pierde la eleccion de lo leido (si la perdiera escribiria null y guardar se negaria a enviar).
    // C2b: las listas son ESTABLES (RefreshChoices solo las rehace si cambia su contenido) y, al rehacerlas, el valor elegido se vuelve a
    // dar: un desplegable cuya lista cambia pierde su eleccion (y puede escribir null) y no la recupera si el valor enlazado sigue igual,
    // porque WPF no avisa de un valor que no cambia (antes la categoria y el impuesto de la ficha salian vacios).
    private static readonly CategoryChoice NoCategory = new("", "Sin categoría", "Sin categoría", 0), NoParent = new("", "Ninguna: categoría principal", "Ninguna: categoría principal", 0);
    // La estacion "No pasa por cocina" (codigo vacio) es la de una bebida o un consumo directo (E4b).
    private static readonly StationDto NoStation = new("", "No pasa por cocina", "Kitchen", -1, true);
    private CategoryChoice[] activeCategories = [NoCategory], parentChoices = [NoParent];
    private TariffDto[] activeTariffs = [];
    private TaxDto[] activeTaxes = [];
    private StationDto[] stationChoices = [NoStation];
    public CategoryChoice[] ActiveCategories => activeCategories;
    public CategoryChoice[] ParentChoices => parentChoices;
    public TariffDto[] ActiveTariffs => activeTariffs;
    public TaxDto[] ActiveTaxes => activeTaxes;
    public StationDto[] StationChoices => stationChoices;
    private void RefreshChoices()
    {
        var product = NewProduct ? null : SelectedProduct?.Product; var category = NewCategory ? null : SelectedCategory?.Category;
        Choices(ref activeCategories, [NoCategory, .. TreeChoices(c => c.Active || c.Id == product?.CategoryId)], nameof(ActiveCategories), () => ProductCategoryId, v => ProductCategoryId = v);
        Choices(ref parentChoices, [NoParent, .. TreeChoices(c => c.Id != category?.Id && (c.Active || c.Id == category?.ParentId))], nameof(ParentChoices), () => CategoryParentId, v => CategoryParentId = v);
        Choices(ref activeTariffs, [.. Tariffs.Where(t => t.Active)], nameof(ActiveTariffs), () => PriceTariffId, v => PriceTariffId = v);
        Choices(ref activeTaxes, [.. Taxes.Where(t => t.Active || t.Id == product?.TaxId).Select(t => t.Active ? t : t with { Name = t.Name + " (desactivado)" })], nameof(ActiveTaxes), () => ProductTaxId, v => ProductTaxId = v);
        Choices(ref stationChoices, [NoStation, .. Stations.Where(s => s.Active && s.Kind != "Pass" || s.Id == product?.StationId).Select(s => s.Active ? s : s with { Name = s.Name + " (desactivada)" })],
            nameof(StationChoices), () => ProductStationId, v => ProductStationId = v);
    }
    private void Choices<T>(ref T[] field, T[] fresh, string list, Func<string> get, Action<string> set)
    {
        if (field.SequenceEqual(fresh)) return;
        var value = get();   // el de antes: al cambiar la lista el desplegable puede escribir null
        field = fresh; OnPropertyChanged(list);
        var was = rendering; rendering = true;
        try { set(null!); set(value); }
        finally { rendering = was; }
    }
    public bool HasCategories => Categories.Length > 0;
    public bool HasProducts => Products.Length > 0;
    public bool HasPresentations => Presentations.Length > 0;
    public string CategoryFormTitle => NewCategory ? "Nueva categoría" : SelectedCategory is null ? "Selecciona una categoría" : "Categoría: " + SelectedCategory.Category.Name;
    public string ProductFormTitle => NewProduct ? "Nuevo producto" : SelectedProduct is null ? "Selecciona un producto" : "Producto: " + SelectedProduct.Product.Name;
    public string PresentationFormTitle => NewPresentation ? "Nueva presentación" : SelectedPresentation is null ? "Selecciona una presentación" : "Presentación: " + SelectedPresentation.Presentation.Name;
    public string TariffFormTitle => NewTariff ? "Nueva tarifa" : SelectedTariff is null ? "Selecciona una tarifa" : "Tarifa: " + SelectedTariff.Name;
    public string TaxFormTitle => NewTax ? "Nuevo impuesto" : SelectedTax is null ? "Selecciona un impuesto" : "Impuesto: " + SelectedTax.Name;
    private bool Main => shell.Writable && shell.IsMain;
    public bool CategoryEditable => Main && (NewCategory || SelectedCategory is not null);
    public bool ProductEditable => Main && (NewProduct || SelectedProduct is not null);
    public bool PresentationEditable => Main && (NewPresentation || SelectedPresentation is not null);
    // E3.2 (revision): precio y desactivar presentacion exigen tambien el producto elegido (la orden lo lleva).
    public bool PriceEditable => Main && SelectedProduct is not null && SelectedPresentation is not null && !NewPresentation;
    public bool TariffEditable => Main && (NewTariff || SelectedTariff is not null);
    public bool TaxEditable => Main && (NewTax || SelectedTax is not null);

    internal void Sync()
    {
        foreach (var name in new[] { nameof(CategoryEditable), nameof(ProductEditable), nameof(PresentationEditable), nameof(PriceEditable), nameof(TariffEditable), nameof(TaxEditable),
            nameof(CategoryFormTitle), nameof(ProductFormTitle), nameof(PresentationFormTitle), nameof(TariffFormTitle), nameof(TaxFormTitle),
            nameof(HasCategories), nameof(HasProducts), nameof(HasPresentations) })
            OnPropertyChanged(name);
        foreach (var command in new IRelayCommand[] { StartCategoryCommand, SaveCategoryCommand, ToggleCategoryCommand, StartProductCommand, SaveProductCommand, ToggleProductCommand,
            StartPresentationCommand, SavePresentationCommand, TogglePresentationCommand, SetPriceCommand, StartTariffCommand, SaveTariffCommand, ToggleTariffCommand, StartTaxCommand, SaveTaxCommand, ToggleTaxCommand,
            ChangeImageCommand, RemoveImageCommand, CancelCategoryCommand, CancelProductCommand, CancelPresentationCommand, CancelTariffCommand, CancelTaxCommand, ShowCategoryProductsCommand })
            command.NotifyCanExecuteChanged();
        Compose();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            catalog = null; zones = null; Categories = []; Products = []; Presentations = []; Tariffs = []; Taxes = []; TariffItems = []; TaxItems = []; FilterChoices = [AllChoice];
            SelectedCategory = null; SelectedProduct = null; SelectedPresentation = null; SelectedTariff = null; SelectedTax = null;
            NewCategory = NewProduct = NewPresentation = NewTariff = NewTax = false; Search = ""; AllCategories = true; filterCategoryId = null; Summary = "Sin datos de catálogo todavía."; PriceValidFrom = "";
            createdCategory = createdProduct = createdPresentation = createdTariff = createdTax = null;
            images.Clear(); photoGeneration++; ProductPhoto = null; PhotoText = "Sin producto seleccionado.";
            Section = "products"; LabelSections();
        }
        finally { rendering = false; }
        RefreshChoices(); RepushFilter();
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        var catalog = await shell.Api.GetAsync<CatalogDto>("erp/catalog");
        var organization = await shell.Api.GetAsync<OrganizationDto>("organization");
        Stations = organization.Stations; zones = organization.Zones;   // C2b: las salas dicen que tarifa usan
        Render(catalog);
    }

    public static string Money(long cents) => (cents / 100m).ToString("N2", CultureInfo.GetCultureInfo("es-ES")) + " €";

    // Precio vigente hoy en una tarifa (la fila de mayor fecha no posterior a hoy), o null.
    private static PriceDto? Current(PresentationDto presentation, string tariffId, string today)
        => presentation.Prices.Where(p => p.TariffId == tariffId && string.CompareOrdinal(p.ValidFrom, today) <= 0).OrderByDescending(p => p.ValidFrom).FirstOrDefault();
    private string PriceLabel(PresentationDto presentation)
    {
        if (catalog is null) return "";
        var parts = Tariffs.Where(t => t.Active).Select(t => Current(presentation, t.Id, catalog.Today)).Zip(Tariffs.Where(t => t.Active), (p, t) => p is null ? null : $"{t.Name} {Money(p.PriceCents)}").Where(s => s is not null).ToArray();
        return parts.Length == 0 ? "sin precio" : string.Join(" · ", parts);
    }
    // C2b: un producto se vende hoy si esta activo y alguna presentacion activa tiene precio vigente en la General (lo que cuenta el resumen).
    private static bool Sellable(ProductDto product, string today) => product.Active && product.Presentations.Any(s => s.Active && Current(s, "general", today) is not null);

    // Tambien lo usan los checks del WPF sin servidor: la pantalla se compone solo de lo leido.
    public void Render(CatalogDto value)
    {
        rendering = true;
        try
        {
            var category = Take(ref createdCategory) ?? SelectedCategory?.Category.Id; var product = Take(ref createdProduct) ?? SelectedProduct?.Product.Id;
            var presentation = Take(ref createdPresentation) ?? SelectedPresentation?.Presentation.Id;
            var tariff = Take(ref createdTariff) ?? SelectedTariff?.Id; var tax = Take(ref createdTax) ?? SelectedTax?.Id;
            // C2b: la fecha de "Fijar precio" propone el hoy del servidor; si seguia proponiendo el de una lectura anterior (otro dia), se
            // pone al dia. Una fecha tecleada no se toca.
            if (PriceValidFrom.Length == 0 || PriceValidFrom == catalog?.Today) PriceValidFrom = value.Today;
            catalog = value; Tariffs = value.Tariffs; Taxes = value.Taxes;
            // Una lista rehecha pierde su eleccion y no la recupera si la fila nueva es igual a la de antes (los registros comparan por valor
            // y la propiedad no avisa): la eleccion se vuelve a dar, primero vacia y despues la de siempre.
            Categories = Flatten(value);
            SelectedCategory = null; SelectedCategory = Categories.FirstOrDefault(c => c.Category.Id == category);
            CategoryChoice[] filters = [new("", "Todas las categorías · " + value.Products.Length, "Todas las categorías", 0),
                .. Categories.Select(c => new CategoryChoice(c.Category.Id, $"{c.Category.Name}{(c.Inactive ? " (desactivada)" : "")} · {ProductsWithin(c.Category.Id)}", c.Path + (c.Inactive ? " (desactivada)" : ""), c.Depth))];
            if (!FilterChoices.SequenceEqual(filters)) FilterChoices = filters;
            RenderProducts();
            SelectedProduct = Products.FirstOrDefault(p => p.Product.Id == product);
            RenderPresentations();
            SelectedPresentation = Presentations.FirstOrDefault(p => p.Presentation.Id == presentation);
            TariffItems = value.Tariffs.Select(t => new TariffRow(t, TariffDetail(t))).ToArray();
            SelectedTariff = null; SelectedTariff = Tariffs.FirstOrDefault(t => t.Id == tariff);
            TaxItems = value.Taxes.Select(t => new TaxRow(t, TaxDetail(t))).ToArray();
            SelectedTax = null; SelectedTax = Taxes.FirstOrDefault(t => t.Id == tax);
            var sellable = value.Products.Count(p => Sellable(p, value.Today));
            Summary = $"{Labels.Plural(value.Products.Count(p => p.Active), "producto activo", "productos activos")} ({Labels.Plural(sellable, "vendible", "vendibles")} hoy) · precios con IVA incluido";
            LabelSections();
        }
        finally { rendering = false; }
        RefreshChoices(); RepushFilter();
        if (!NewCategory) FillCategoryForm(); if (!NewProduct) FillProductForm(); if (!NewPresentation) FillPresentationForm(); if (!NewTariff) FillTariffForm(); if (!NewTax) FillTaxForm();
        Sync();
    }
    private static string? Take(ref string? created) { var value = created; created = null; return value; }
    private void LabelSections()
    {
        int?[] counts = [catalog?.Products.Length, catalog?.Categories.Length, catalog?.Tariffs.Length, catalog?.Taxes.Length];
        foreach (var (choice, count) in Sections.Zip(counts))
            choice.Label = (choice.Id switch { "products" => "Productos", "categories" => "Categorías", "tariffs" => "Tarifas", _ => "Impuestos" }) + (count is { } n ? " · " + n : "");
    }

    // ---- C2b: arbol de categorias (rutas, descendientes y recuentos) ----
    private CategoryDto? CategoryOf(string? id) => id is null ? null : catalog?.Categories.FirstOrDefault(c => c.Id == id);
    // Ruta desde la raiz ("Vinos › Blancos › Rias Baixas"); levels limita a los ultimos niveles ("Blancos › Rias Baixas").
    private string PathOf(string id, int levels = 6)
    {
        var names = new List<string>();
        for (var c = CategoryOf(id); c is not null && names.Count < 6; c = CategoryOf(c.ParentId)) names.Insert(0, c.Name);
        if (names.Count == 0) return id;   // una categoria que el catalogo no lista: su codigo, tal cual
        return string.Join(" › ", names.Skip(Math.Max(0, names.Count - levels)));
    }
    // La categoria y todas sus subcategorias (el servidor impide ciclos; aun asi, cada una se visita una vez).
    private HashSet<string> Within(string id)
    {
        var result = new HashSet<string>(StringComparer.Ordinal) { id };
        var pending = new Queue<string>(); pending.Enqueue(id);
        while (pending.Count > 0)
        {
            var parent = pending.Dequeue();
            foreach (var child in catalog?.Categories.Where(c => c.ParentId == parent) ?? [])
                if (result.Add(child.Id)) pending.Enqueue(child.Id);
        }
        return result;
    }
    private int ProductsWithin(string id) { var within = Within(id); return catalog?.Products.Count(p => p.CategoryId is { } c && within.Contains(c)) ?? 0; }
    private CategoryChoice[] TreeChoices(Func<CategoryDto, bool> include) =>
        Categories.Where(r => include(r.Category)).Select(r => new CategoryChoice(r.Category.Id, r.Category.Name + (r.Inactive ? " (desactivada)" : ""), r.Path + (r.Inactive ? " (desactivada)" : ""), r.Depth)).ToArray();

    // Arbol de categorias como lista con sangria: padres primero, hijos debajo por orden; los huerfanos al final.
    private CategoryRow[] Flatten(CatalogDto value)
    {
        var categories = value.Categories;
        var result = new List<CategoryRow>(); var placed = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? parent, int depth)
        {
            if (depth > 6) return;
            foreach (var c in categories.Where(c => c.ParentId == parent && placed.Add(c.Id)).OrderBy(c => c.Sort).ThenBy(c => c.Id, StringComparer.Ordinal))
            { result.Add(Row(c, depth)); Add(c.Id, depth + 1); }
        }
        Add(null, 0);
        foreach (var c in categories.Where(c => placed.Add(c.Id))) result.Add(Row(c, 0));
        return result.ToArray();
        CategoryRow Row(CategoryDto c, int depth)
        {
            var children = categories.Count(k => k.ParentId == c.Id);
            string?[] detail = [c.Active ? null : "Desactivada", Labels.Plural(ProductsWithin(c.Id), "producto", "productos"), children == 0 ? null : Labels.Plural(children, "subcategoría", "subcategorías")];
            return new(c, depth) { Path = PathOf(c.Id), Detail = string.Join(" · ", detail.Where(d => d is not null)), Swatch = Swatch(c.Color) };
        }
    }
    // Color de una categoria (#RRGGBB) para su muestra; cualquier otra cosa, sin muestra.
    private static Brush? Swatch(string? color)
    {
        if (color is not { Length: 7 } || color[0] != '#' || !color.Skip(1).All(char.IsAsciiHexDigit)) return null;
        var brush = new SolidColorBrush(Color.FromRgb(Convert.ToByte(color[1..3], 16), Convert.ToByte(color[3..5], 16), Convert.ToByte(color[5..7], 16)));
        brush.Freeze();
        return brush;
    }

    private void RenderProducts()
    {
        if (catalog is null) { Products = []; return; }
        var text = Search.Trim();
        var within = !AllCategories && filterCategoryId is { } filter ? Within(filter) : null;
        Products = catalog.Products
            .Where(p => within is null || p.CategoryId is { } c && within.Contains(c))
            .Where(p => Matches(p, text))
            .Select(ProductRowOf)
            .ToArray();
    }
    // C2b: busca por nombre (sin distinguir mayusculas, tildes ni la virgulilla de la ene: "albarino" encuentra el albarino), codigo o referencia.
    private static bool Matches(ProductDto product, string text)
    {
        if (text.Length == 0) return true;
        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(product.Name, text, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0
            || product.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || (product.Reference?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }
    private ProductRow ProductRowOf(ProductDto p)
    {
        var today = catalog!.Today;
        var active = p.Presentations.Where(s => s.Active).OrderBy(s => s.Sort).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        var price = active.Length == 0 ? null : Current(active[0], "general", today);
        return new ProductRow(p, p.CategoryId is null ? "Sin categoría" : PathOf(p.CategoryId, 2),
            p.Presentations.Length == 0 ? "sin presentación" : string.Join(" / ", p.Presentations.Where(s => s.Active).Select(s => Current(s, "general", today) is { } current ? $"{s.Name} {Money(current.PriceCents)}" : s.Name + " sin precio")),
            images)
        { MainPrice = !p.Active ? "Desactivado" : price is null ? "Sin precio" : Money(price.PriceCents), PriceMissing = p.Active && price is null, PresentationCount = active.Length };
    }
    private void RenderPresentations() => Presentations = SelectedProduct is null ? [] : SelectedProduct.Product.Presentations.Select(p => new PresentationRow(p, PriceLabel(p)) { Cells = CellsOf(p) }).ToArray();
    // Precio vigente en cada tarifa activa (las columnas de la tabla): sin precio en la General no se vende; sin precio propio en otra
    // tarifa se cobra el de la General.
    private PriceCell[] CellsOf(PresentationDto presentation) => catalog is null ? [] : [.. Tariffs.Where(t => t.Active).Select(t =>
        Current(presentation, t.Id, catalog.Today) is { } price ? new PriceCell(Money(price.PriceCents), "")
        : t.Id == "general" ? new PriceCell("Sin precio", "missing") : new PriceCell("—", "fallback"))];

    partial void OnSelectedCategoryChanged(CategoryRow? value)
    {
        if (rendering) return;
        NewCategory = false; FillCategoryForm(); Sync();
    }
    partial void OnAllCategoriesChanged(bool value) { OnPropertyChanged(nameof(CategoryFilter)); if (rendering) return; Refilter(); }
    partial void OnSearchChanged(string value) { if (rendering) return; Refilter(); }
    // C2b: filtro de la lista por categoria (null = todas: "Ver todos los productos"); recuerda la ultima categoria elegida.
    private void SetFilter(string? categoryId)
    {
        if (categoryId is not null) filterCategoryId = categoryId;
        rendering = true;
        try { AllCategories = categoryId is null; }
        finally { rendering = false; }
        OnPropertyChanged(nameof(CategoryFilter));
        Refilter();
    }
    // E3.2 (revision): la fila del producto es una clase (su miniatura avisa al llegar) y compara por referencia, asi que al rehacer
    // la lista la ListBox ya no encuentra la fila elegida y la deja en null. Buscar, filtrar o "Ver todos" vuelve a elegir el MISMO
    // producto por su codigo (ficha y presentaciones siguen como estaban); si el filtro lo deja fuera, se deselecciona del todo:
    // ficha, foto y presentaciones limpias, nunca una ficha ni un precio de un producto que ya no esta elegido.
    private void Refilter()
    {
        var product = SelectedProduct?.Product.Id;
        rendering = true;
        try
        {
            RenderProducts();
            SelectedProduct = Products.FirstOrDefault(p => p.Product.Id == product);
            if (product is not null && SelectedProduct is null) { RenderPresentations(); SelectedPresentation = null; }
        }
        finally { rendering = false; }
        if (product is not null && SelectedProduct is null)
        {
            if (!NewProduct) FillProductForm();
            NewPresentation = false; FillPresentationForm();
        }
        Sync();
    }
    // C2b: "Ver todos los productos": sin busqueda ni categoria (la eleccion se conserva).
    [RelayCommand]
    private void ShowAll()
    {
        if (Search.Length > 0) { rendering = true; try { Search = ""; } finally { rendering = false; } }
        SetFilter(null);
    }
    partial void OnSelectedProductChanged(ProductRow? value)
    {
        if (rendering) return;
        NewProduct = false; FillProductForm();
        rendering = true; try { RenderPresentations(); SelectedPresentation = Presentations.FirstOrDefault(); } finally { rendering = false; }
        NewPresentation = false; FillPresentationForm(); Sync();
    }
    partial void OnSelectedPresentationChanged(PresentationRow? value) { if (rendering) return; NewPresentation = false; FillPresentationForm(); Sync(); }
    partial void OnSelectedTariffChanged(TariffDto? value) { if (rendering) return; NewTariff = false; FillTariffForm(); Sync(); }
    partial void OnSelectedTaxChanged(TaxDto? value) { if (rendering) return; NewTax = false; FillTaxForm(); Sync(); }
    partial void OnCategoryColorChanged(string value) => CategorySwatch = Swatch(value?.Trim());

    private void FillCategoryForm()
    {
        var c = SelectedCategory?.Category;
        RefreshChoices();   // antes que el valor: la lista ya lleva la eleccion de lo leido
        CategoryId = c?.Id ?? ""; CategoryName = c?.Name ?? ""; CategoryParentId = c?.ParentId ?? ""; CategoryColor = c?.Color ?? ""; CategorySort = (c?.Sort ?? 0).ToString();
    }
    private void FillProductForm()
    {
        var p = SelectedProduct?.Product;
        RefreshChoices();   // antes que los valores
        ProductId = p?.Id ?? ""; ProductName = p?.Name ?? ""; ProductCategoryId = p?.CategoryId ?? ""; ProductTaxId = p?.TaxId ?? "iva-10";
        ProductReference = p?.Reference ?? ""; ProductSort = (p?.Sort ?? 0).ToString(); ProductStationId = p?.StationId ?? "";
        _ = ShowPhotoAsync();
    }

    // E3.2: foto de la ficha del producto seleccionado. La ultima peticion manda: si se cambia de producto mientras carga, la
    // respuesta anterior no pisa la nueva. Nunca espera en el hilo de la ventana: el hueco dice "Cargando imagen…" mientras tanto.
    private async Task ShowPhotoAsync()
    {
        var generation = ++photoGeneration;
        var product = NewProduct ? null : SelectedProduct?.Product;
        ProductPhoto = null;
        if (product is null) { PhotoText = NewProduct ? "Guarda el producto para añadirle una imagen." : "Sin producto seleccionado."; return; }
        if (product.ImageSha is not { } sha) { PhotoText = "Sin imagen"; return; }
        if (images.Photo(sha) is { } cached) { ProductPhoto = cached; PhotoText = ""; return; }
        PhotoText = "Cargando imagen…";
        string text;
        BitmapSource? photo = null;
        try
        {
            var (image, found) = await images.LoadPhotoAsync(product.Id, sha);
            photo = image;
            text = image is not null ? "" : found
                ? "Este equipo no puede mostrar esta imagen. Si es WebP, instala «Extensiones de imagen WebP» de Microsoft Store."
                : "La imagen ya no está en el servidor. Pulsa Actualizar.";
        }
        catch (Exception e) when (e is not OutOfMemoryException) { text = "No se pudo cargar la imagen del servidor. Pulsa Actualizar para intentarlo de nuevo."; }
        if (generation != photoGeneration) return;
        ProductPhoto = photo; PhotoText = text;
    }
    private void FillPresentationForm()
    {
        var p = SelectedPresentation?.Presentation;
        PresentationId = p?.Id ?? ""; PresentationName = p?.Name ?? ""; PresentationSort = (p?.Sort ?? 0).ToString();
        // C1 (arreglo): el desplegable de tarifa escribe null en PriceTariffId cuando su lista se vacia (limpiar o releer el catalogo
        // con la vista abierta); sin tarifa elegida se muestra la general, como al empezar.
        var current = p is null || catalog is null ? null : Current(p, string.IsNullOrEmpty(PriceTariffId) ? "general" : PriceTariffId, catalog.Today);
        PriceText = current is null ? "" : (current.PriceCents / 100m).ToString("0.00", CultureInfo.GetCultureInfo("es-ES"));
    }
    partial void OnPriceTariffIdChanged(string value) { if (!rendering && !NewPresentation) FillPresentationForm(); }
    private void FillTariffForm() { TariffId = SelectedTariff?.Id ?? ""; TariffName = SelectedTariff?.Name ?? ""; TariffSort = (SelectedTariff?.Sort ?? 0).ToString(); }
    private void FillTaxForm() { TaxId = SelectedTax?.Id ?? ""; TaxName = SelectedTax?.Name ?? ""; TaxRate = (SelectedTax?.Rate ?? 10m).ToString(CultureInfo.InvariantCulture); }

    // ---- C2b: composicion de las listas y las fichas (solo presentacion: nada de esto habilita ni envia) ----
    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Spanish) + text[1..];
    private static string Rate(decimal rate) => rate.ToString("0.##", Spanish) + " %";
    // Presentaciones activas de productos activos (lo que puede venderse) y cuantas tienen precio vigente en una tarifa.
    private PresentationDto[] SellablePresentations() => catalog?.Products.Where(p => p.Active).SelectMany(p => p.Presentations.Where(s => s.Active)).ToArray() ?? [];
    private int PricedIn(string tariffId) => catalog is null ? 0 : SellablePresentations().Count(s => Current(s, tariffId, catalog.Today) is not null);
    // Salas que cobran con una tarifa: la General, las que no tienen tarifa propia; otra, las que la tienen (una sala desactivada, marcada).
    private string[] ZonesUsing(string tariffId) => zones is null ? [] : [.. zones
        .Where(z => tariffId == "general" ? z.TariffId is null or "" or "general" : z.TariffId == tariffId)
        .OrderBy(z => z.Sort).Select(z => z.Name + (z.Active ? "" : " (desactivada)"))];
    private string TariffDetail(TariffDto t)
    {
        var using_ = ZonesUsing(t.Id);
        string?[] parts = [t.Active ? null : "Desactivada", zones is null ? null : using_.Length == 0 ? "ninguna sala" : string.Join(", ", using_),
            Labels.Plural(PricedIn(t.Id), "precio vigente", "precios vigentes")];
        return Cap(string.Join(" · ", parts.Where(p => p is not null)));
    }
    private string TaxDetail(TaxDto t) => (t.Active ? "" : "Desactivado · ") + Rate(t.Rate) + " · " + Labels.Plural(catalog?.Products.Count(p => p.TaxId == t.Id) ?? 0, "producto", "productos");

    private void Compose()
    {
        ComposeProducts(); ComposeCategories(); ComposeTariffs(); ComposeTaxes();
    }
    private void ComposeProducts()
    {
        var total = catalog?.Products.Length ?? 0;
        var text = Search.Trim();
        var category = !AllCategories && filterCategoryId is { } filter ? PathOf(filter) : null;
        IsFiltered = catalog is not null && (text.Length > 0 || category is not null);
        ShowProductTools = total > 0;
        ProductCount = catalog is null ? "" : IsFiltered ? $"{Products.Length} de {Labels.Plural(total, "producto", "productos")}" : Labels.Plural(total, "producto", "productos") + " · precio General";
        ShowProductListEmpty = Products.Length == 0;
        ShowAllLink = IsFiltered && !ShowProductListEmpty;
        (ProductListHeading, ProductListText) = catalog is null ? ("Sin leer todavía", "Los productos aparecerán aquí al leer el catálogo del servidor.")
            : total == 0 ? ("Aún no hay productos", "Crea el primero o importa el catálogo desde un fichero en el equipo servidor.")
            : text.Length > 0 ? ("Ningún producto coincide", $"Ningún producto{(category is null ? "" : " de " + category)} coincide con «{text}».")
            : ("Ningún producto coincide", $"{category} no tiene productos todavía.");
        ShowCreateProduct = catalog is not null && total == 0;
        ShowShowAll = ShowProductListEmpty && IsFiltered;

        var product = SelectedProduct?.Product;
        HasProductContext = NewProduct || product is not null;
        (ProductEmptyHeading, ProductEmptyText) = catalog is null ? ("Sin leer todavía", "El catálogo aparecerá aquí al leerlo del servidor.")
            : total == 0 ? ("Sin productos todavía", "Cuando crees o importes productos, elige uno en la lista para ver su foto, sus datos y sus precios.")
            : ("Elige un producto", "Pulsa un producto de la lista para ver su foto, sus datos y sus precios en cada tarifa.");
        ProductContext = NewProduct ? "Alta de producto" : product is null ? "" : product.CategoryId is { } c ? PathOf(c) : "Sin categoría";
        ProductTitle = NewProduct ? "Nuevo producto" : product?.Name ?? "";
        ProductStateText = NewProduct || product is null ? "" : Cap(Labels.Active(product.Active, feminine: false));
        ProductStateTone = product?.Active == true ? "ok" : "other";
        ProductWarnText = !NewProduct && product is not null && catalog is not null && product.Active && !Sellable(product, catalog.Today) ? "No se vende: sin precio General" : "";
        ProductSaveText = NewProduct ? "Crear producto" : "Guardar cambios";
        ProductDeactivates = !NewProduct && product?.Active == true;
        ProductReactivates = !NewProduct && product?.Active == false;
        PriceColumns = [.. Tariffs.Where(t => t.Active).Select(t => t.Name)];
        ShowPresentations = !NewProduct && product is not null;
        PresentationsEmpty = ShowPresentations && Presentations.Length == 0;
        PriceNote = "Precios vigentes hoy, con IVA incluido." + (Presentations.Any(p => p.Cells.Any(c => c.Tone == "fallback")) ? " «—»: sin precio propio en esa tarifa; se cobra el de la General." : "");
        HasPresentationEditor = ShowPresentations && (NewPresentation || SelectedPresentation is not null);
        PresentationSaveText = NewPresentation ? "Crear presentación" : "Guardar presentación";
        PresentationDeactivates = !NewPresentation && SelectedPresentation?.Presentation.Active == true;
        PresentationReactivates = !NewPresentation && SelectedPresentation?.Presentation.Active == false;
        // Precios programados (desde una fecha posterior a hoy) de la presentacion elegida: la tabla solo muestra los vigentes.
        ScheduledText = NewPresentation || SelectedPresentation is null || catalog is null ? "" : string.Join(" · ", SelectedPresentation.Presentation.Prices
            .Where(p => string.CompareOrdinal(p.ValidFrom, catalog.Today) > 0).OrderBy(p => p.ValidFrom, StringComparer.Ordinal)
            .Select(p => $"{Tariffs.FirstOrDefault(t => t.Id == p.TariffId)?.Name ?? p.TariffId} {Money(p.PriceCents)} desde el {Day(p.ValidFrom)}")) is { Length: > 0 } scheduled ? "Programado: " + scheduled : "";
    }
    private static string Day(string date) => DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : date;
    private void ComposeCategories()
    {
        var all = catalog?.Categories ?? [];
        CategoryCount = catalog is null ? "" : Labels.Plural(all.Length, "categoría", "categorías") + (all.Count(c => !c.Active) is var off and > 0 ? $", {off} desactivada{(off == 1 ? "" : "s")}" : "");
        ShowCategoryListEmpty = Categories.Length == 0;
        (CategoryListHeading, CategoryListText) = catalog is null ? ("Sin leer todavía", "Las categorías aparecerán aquí al leer el catálogo del servidor.")
            : ("Aún no hay categorías", "Agrupan los productos (Vinos › Tintos › Rioja…). Crea la primera o impórtalas con el catálogo.");
        var c = SelectedCategory?.Category;
        HasCategoryContext = NewCategory || c is not null;
        (CategoryEmptyHeading, CategoryEmptyText) = catalog is not null && all.Length == 0 ? ("Sin categorías todavía", "Cuando crees o importes categorías, elige una en el árbol para ver sus datos y sus productos.")
            : ("Elige una categoría", "Pulsa una categoría del árbol para ver sus datos, sus subcategorías y cuántos productos tiene.");
        CategoryContext = NewCategory ? "Alta de categoría" : c is null ? "" : c.ParentId is { } parent ? "Dentro de " + PathOf(parent) : "Categoría principal";
        CategoryTitle = NewCategory ? "Nueva categoría" : c?.Name ?? "";
        CategoryStateText = NewCategory || c is null ? "" : Cap(Labels.Active(c.Active, feminine: true));
        CategoryStateTone = c?.Active == true ? "ok" : "other";
        CategorySaveText = NewCategory ? "Crear categoría" : "Guardar cambios";
        CategoryDeactivates = !NewCategory && c?.Active == true;
        CategoryReactivates = !NewCategory && c?.Active == false;
        var children = c is null || NewCategory ? [] : all.Where(k => k.ParentId == c.Id).OrderBy(k => k.Sort).ThenBy(k => k.Id, StringComparer.Ordinal).ToArray();
        CategoryProductsFigure = c is null || NewCategory ? "" : ProductsWithin(c.Id).ToString(CultureInfo.InvariantCulture);
        CategoryChildrenFigure = c is null || NewCategory ? "" : children.Length.ToString(CultureInfo.InvariantCulture);
        CategoryChildrenText = c is null || NewCategory ? "" : children.Length == 0 ? "Sin subcategorías." : "Subcategorías: " + string.Join(" · ", children.Select(k => k.Name + (k.Active ? "" : " (desactivada)")));
    }
    private void ComposeTariffs()
    {
        var t = SelectedTariff;
        HasTariffContext = NewTariff || t is not null;
        (TariffEmptyHeading, TariffEmptyText) = catalog is null ? ("Sin leer todavía", "Las tarifas aparecerán aquí al leer el catálogo del servidor.")
            : ("Elige una tarifa", "Pulsa una tarifa para ver qué salas la usan y cuántos precios tiene.");
        IsGeneralTariff = !NewTariff && t?.Id == "general";
        TariffContext = NewTariff ? "Alta de tarifa" : IsGeneralTariff ? "Tarifa base" : "Tarifa de precios";
        TariffTitle = NewTariff ? "Nueva tarifa" : t?.Name ?? "";
        TariffStateText = NewTariff || t is null ? "" : Cap(Labels.Active(t.Active, feminine: true));
        TariffStateTone = t?.Active == true ? "ok" : "other";
        TariffSaveText = NewTariff ? "Crear tarifa" : "Guardar cambios";
        TariffDeactivates = !NewTariff && t is { Active: true } && !IsGeneralTariff;
        TariffReactivates = !NewTariff && t is { Active: false };
        // Las salas que la usan solo si se han leido (GET /organization); si no, la seccion no se pinta.
        HasZoneInfo = zones is not null && !NewTariff && t is not null;
        TariffZones = t is null || NewTariff ? [] : ZonesUsing(t.Id);
        TariffZonesText = !HasZoneInfo ? "" : IsGeneralTariff
            ? (TariffZones.Length == 0 ? "Todas las salas tienen tarifa propia. " : "") + "Las salas sin tarifa propia cobran con la General; las demás, también cuando un producto no tiene precio en la suya."
            : TariffZones.Length == 0 ? "Ninguna sala usa esta tarifa. La tarifa de cada sala se elige en Salas, mesas y estaciones." : "Lo que no tiene precio propio en esta tarifa se cobra al precio de la General.";
        var sellable = SellablePresentations().Length;
        var priced = t is null || NewTariff ? 0 : PricedIn(t.Id);
        TariffPricedFigure = t is null || NewTariff ? "" : priced.ToString(CultureInfo.InvariantCulture);
        TariffUnpricedFigure = t is null || NewTariff ? "" : (sellable - priced).ToString(CultureInfo.InvariantCulture);
        TariffUnpricedLabel = IsGeneralTariff ? "Sin precio: no se venden" : "Sin precio propio";
        TariffPricesText = t is null || NewTariff ? "" : $"De {Labels.Plural(sellable, "presentación activa", "presentaciones activas")} del catálogo. Los precios se fijan en la ficha de cada producto, en Productos.";
        TariffNote = IsGeneralTariff ? "La tarifa General existe siempre y no se puede desactivar." : "Nada se borra: una tarifa desactivada deja de ofrecerse y conserva su historia.";
    }
    private void ComposeTaxes()
    {
        ShowTaxListEmpty = TaxItems.Length == 0;
        var t = SelectedTax;
        HasTaxContext = NewTax || t is not null;
        (TaxEmptyHeading, TaxEmptyText) = catalog is null ? ("Sin leer todavía", "Los impuestos aparecerán aquí al leer el catálogo del servidor.")
            : ("Elige un impuesto", "Pulsa un impuesto para ver su tipo y los productos que lo llevan.");
        TaxTitle = NewTax ? "Nuevo impuesto" : t?.Name ?? "";
        TaxStateText = NewTax || t is null ? "" : Cap(Labels.Active(t.Active, feminine: false));
        TaxStateTone = t?.Active == true ? "ok" : "other";
        TaxSaveText = NewTax ? "Crear impuesto" : "Guardar cambios";
        TaxDeactivates = !NewTax && t?.Active == true;
        TaxReactivates = !NewTax && t?.Active == false;
        var using_ = t is null || NewTax ? [] : catalog?.Products.Where(p => p.TaxId == t.Id).OrderBy(p => p.Sort).ThenBy(p => p.Name, StringComparer.CurrentCulture).ToArray() ?? [];
        TaxProductsFigure = t is null || NewTax ? "" : using_.Length.ToString(CultureInfo.InvariantCulture);
        TaxProductsText = t is null || NewTax ? "" : using_.Length == 0 ? "Ningún producto lleva este impuesto."
            : string.Join(" · ", using_.Take(6).Select(p => p.Name)) + (using_.Length > 6 ? $" y {using_.Length - 6} más." : ".");
    }

    private static int Number(string text, string what, int min, int max)
    {
        if (!int.TryParse(text.Trim(), out var value) || value < min || value > max) throw new ArgumentException($"{what}: introduce un número entre {min} y {max}.");
        return value;
    }
    private static string Code(string text)
    {
        var code = text.Trim();
        if (code.Length is 0 or > 32 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("El código es obligatorio: letras, números, '-' o '_', sin espacios ni acentos (máximo 32).");
        return code;
    }
    private static string Name(string text) => text.Trim().Length == 0 ? throw new ArgumentException("El nombre es obligatorio.") : text.Trim();
    // C2b: lo elegido en un desplegable. Null = el desplegable perdio su eleccion (su lista cambio): no se envia nada en lugar de
    // cambiar sin querer la categoria, el impuesto o la estacion.
    private static string Chosen(string? value, string what) => value?.Trim() ?? throw new ArgumentException($"Vuelve a elegir {what}: el desplegable se quedó sin elección.");
    // Importe tecleado en euros ("9,50" o "9.50") a centimos.
    public static long Cents(string text)
    {
        var t = text.Trim().Replace("€", "").Replace(" ", "");
        if (t.Contains(',') && t.Contains('.')) t = t.Replace(".", "").Replace(',', '.'); else t = t.Replace(',', '.');
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 999_999.99m)
            throw new ArgumentException("Precio: introduce un importe en euros, por ejemplo 9,50.");
        return (long)decimal.Round(value * 100, 0, MidpointRounding.AwayFromZero);
    }

    // C2b: confirmed se ejecuta solo cuando el servidor confirma la orden (antes de releer): lo recien creado queda elegido.
    private Task Command(string action, object body, string description, Action? confirmed = null) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("erp/catalog/commands/" + action, body, description);
        confirmed?.Invoke();
        NewCategory = NewProduct = NewPresentation = NewTariff = NewTax = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    // C2b: empezar un alta deja la lista sin eleccion (la ficha es la del alta) y abre su pestana; "Cancelar" vuelve a la ficha vacia.
    private bool CanStart() => Main;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartCategory()
    {
        var parent = SelectedCategory?.Category is { Active: true } current ? current.Id : "";   // dentro de la elegida, como siempre
        rendering = true; try { SelectedCategory = null; } finally { rendering = false; }
        NewCategory = true; Section = "categories"; RefreshChoices();
        CategoryId = ""; CategoryName = ""; CategoryParentId = parent; CategoryColor = ""; CategorySort = Categories.Length.ToString(); Sync();
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartProduct()
    {
        // La categoria del filtro de la lista, si esta activa (un producto nuevo mientras se mira "Tintos" nace en Tintos).
        var category = CategoryFilter is { Length: > 0 } filter && CategoryOf(filter) is { Active: true } ? filter : "";
        rendering = true; try { SelectedProduct = null; RenderPresentations(); SelectedPresentation = null; } finally { rendering = false; }
        NewProduct = true; NewPresentation = false; Section = "products";
        RefreshChoices();
        ProductId = ""; ProductName = ""; ProductCategoryId = category; ProductTaxId = "iva-10"; ProductReference = ""; ProductStationId = ""; ProductSort = Products.Length.ToString(); ProductPresentationName = "Unidad";
        FillPresentationForm(); _ = ShowPhotoAsync(); Sync();
    }
    private bool CanStartPresentation() => Main && SelectedProduct is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanStartPresentation))]
    private void StartPresentation()
    {
        rendering = true; try { SelectedPresentation = null; } finally { rendering = false; }
        NewPresentation = true; PresentationId = ""; PresentationName = ""; PresentationSort = Presentations.Length.ToString(); PriceText = ""; Sync();
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartTariff()
    {
        rendering = true; try { SelectedTariff = null; } finally { rendering = false; }
        NewTariff = true; Section = "tariffs"; TariffId = ""; TariffName = ""; TariffSort = Tariffs.Length.ToString(); Sync();
    }
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void StartTax()
    {
        rendering = true; try { SelectedTax = null; } finally { rendering = false; }
        NewTax = true; Section = "taxes"; TaxId = ""; TaxName = ""; TaxRate = "10"; Sync();
    }
    private bool CanCancelCategory() => NewCategory;
    [RelayCommand(CanExecute = nameof(CanCancelCategory))] private void CancelCategory() { NewCategory = false; FillCategoryForm(); Sync(); }
    private bool CanCancelProduct() => NewProduct;
    [RelayCommand(CanExecute = nameof(CanCancelProduct))] private void CancelProduct() { NewProduct = false; FillProductForm(); Sync(); }
    private bool CanCancelPresentation() => NewPresentation;
    [RelayCommand(CanExecute = nameof(CanCancelPresentation))]
    private void CancelPresentation()
    {
        NewPresentation = false;
        rendering = true; try { SelectedPresentation = Presentations.FirstOrDefault(); } finally { rendering = false; }
        FillPresentationForm(); Sync();
    }
    private bool CanCancelTariff() => NewTariff;
    [RelayCommand(CanExecute = nameof(CanCancelTariff))] private void CancelTariff() { NewTariff = false; FillTariffForm(); Sync(); }
    private bool CanCancelTax() => NewTax;
    [RelayCommand(CanExecute = nameof(CanCancelTax))] private void CancelTax() { NewTax = false; FillTaxForm(); Sync(); }
    // C2b: "Ver sus productos" desde la ficha de una categoria: la pestana Productos filtrada por ella (y sus subcategorias), sin busqueda.
    private bool CanShowCategoryProducts() => SelectedCategory is not null && !NewCategory;
    [RelayCommand(CanExecute = nameof(CanShowCategoryProducts))]
    private void ShowCategoryProducts()
    {
        var id = SelectedCategory!.Category.Id;
        if (Search.Length > 0) { rendering = true; try { Search = ""; } finally { rendering = false; } }
        Section = "products"; SetFilter(id);
    }

    private bool CanSaveCategory() => CategoryEditable;
    [RelayCommand(CanExecute = nameof(CanSaveCategory))]
    private Task SaveCategory()
    {
        var id = Code(NewCategory ? CategoryId : SelectedCategory!.Category.Id); var name = Name(CategoryName); var sort = Number(CategorySort, "Orden", 0, 9999);
        var parentId = Chosen(CategoryParentId, "dentro de qué categoría va"); var color = CategoryColor.Trim();
        return Command(NewCategory ? "category-create" : "category-update", new { id, name, parentId, color, sort }, (NewCategory ? "Crear categoría " : "Guardar categoría ") + name,
            NewCategory ? () => { createdCategory = id; } : null);
    }
    private bool CanToggleCategory() => Main && SelectedCategory is not null && !NewCategory;
    [RelayCommand(CanExecute = nameof(CanToggleCategory))]
    private Task ToggleCategory() => Command(SelectedCategory!.Category.Active ? "category-deactivate" : "category-reactivate", new { id = SelectedCategory.Category.Id },
        (SelectedCategory.Category.Active ? "Desactivar categoría " : "Reactivar categoría ") + SelectedCategory.Category.Name);

    private bool CanSaveProduct() => ProductEditable;
    [RelayCommand(CanExecute = nameof(CanSaveProduct))]
    private Task SaveProduct()
    {
        var id = Code(NewProduct ? ProductId : SelectedProduct!.Product.Id); var name = Name(ProductName); var sort = Number(ProductSort, "Orden", 0, 9999);
        var categoryId = Chosen(ProductCategoryId, "la categoría"); var taxId = Code(Chosen(ProductTaxId, "el impuesto")); var reference = ProductReference.Trim();
        var stationId = Chosen(ProductStationId, "la estación de cocina");
        return NewProduct
            ? Command("product-create", new { id, name, categoryId, taxId, reference, sort, stationId, presentation = Name(ProductPresentationName) }, "Crear producto " + name, () => { createdProduct = id; })
            : Command("product-update", new { id, name, categoryId, taxId, reference, sort, stationId }, "Guardar producto " + name);
    }
    private bool CanToggleProduct() => Main && SelectedProduct is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanToggleProduct))]
    private Task ToggleProduct() => Command(SelectedProduct!.Product.Active ? "product-deactivate" : "product-reactivate", new { id = SelectedProduct.Product.Id },
        (SelectedProduct.Product.Active ? "Desactivar producto " : "Reactivar producto ") + SelectedProduct.Product.Name);

    // E3.2: imagen del producto. Solo el responsable (el servidor lo impone igual) y sobre un producto ya guardado. El fichero se
    // comprueba aqui antes de enviar (hasta 2 MB, JPEG, PNG o WebP por sus bytes) y el servidor lo vuelve a validar todo. Se lee y se
    // envia fuera del hilo de la ventana; la orden va por la tuberia incierta como cualquier otra y despues se relee el catalogo.
    private bool CanChangeImage() => Main && SelectedProduct is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanChangeImage))]
    private Task ChangeImage()
    {
        var product = SelectedProduct!.Product;
        var path = PickImageFile();
        if (path is null) return Task.CompletedTask;
        var description = "Cambiar imagen de " + product.Name;
        return ImageCommand(async api =>
        {
            byte[] content;
            try
            {
                ProductImages.CheckSize(new FileInfo(path).Length);   // lo que pasa de 2 MB ni se lee
                content = await File.ReadAllBytesAsync(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new ArgumentException("No se pudo leer el fichero " + System.IO.Path.GetFileName(path) + ". Comprueba que existe y que no está abierto en otro programa."); }
            ProductImages.Check(content);
            await Task.Run(() => api.SetProductImageAsync(product.Id, content, description));
        }, description);
    }
    private bool CanRemoveImage() => Main && SelectedProduct?.Product.ImageSha is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    private Task RemoveImage()
    {
        var product = SelectedProduct!.Product; var description = "Quitar imagen de " + product.Name;
        return ImageCommand(api => api.RemoveProductImageAsync(product.Id, description), description);
    }
    private Task ImageCommand(Func<ApiClient, Task> send, string description) => shell.Run(async () =>
    {
        await send(shell.Api ?? throw new InvalidOperationException("Sin conexión con el servidor."));
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    private bool CanSavePresentation() => PresentationEditable && SelectedProduct is not null;
    [RelayCommand(CanExecute = nameof(CanSavePresentation))]
    private Task SavePresentation()
    {
        var productId = SelectedProduct!.Product.Id; var id = Code(NewPresentation ? PresentationId : SelectedPresentation!.Presentation.Id);
        var name = Name(PresentationName); var sort = Number(PresentationSort, "Orden", 0, 9999);
        return Command(NewPresentation ? "presentation-create" : "presentation-update", new { productId, id, name, sort }, (NewPresentation ? "Crear presentación " : "Guardar presentación ") + name + " de " + SelectedProduct.Product.Name,
            NewPresentation ? () => { createdPresentation = id; } : null);
    }
    private bool CanTogglePresentation() => Main && SelectedProduct is not null && SelectedPresentation is not null && !NewPresentation;
    [RelayCommand(CanExecute = nameof(CanTogglePresentation))]
    private Task TogglePresentation() => Command(SelectedPresentation!.Presentation.Active ? "presentation-deactivate" : "presentation-reactivate",
        new { productId = SelectedProduct!.Product.Id, id = SelectedPresentation.Presentation.Id },
        (SelectedPresentation.Presentation.Active ? "Desactivar presentación " : "Reactivar presentación ") + SelectedPresentation.Presentation.Name + " de " + SelectedProduct.Product.Name);

    private bool CanSetPrice() => PriceEditable;
    [RelayCommand(CanExecute = nameof(CanSetPrice))]
    private Task SetPrice()
    {
        var tariffId = Code(PriceTariffId ?? ""); var priceCents = Cents(PriceText); var validFrom = PriceValidFrom.Trim();
        if (validFrom.Length > 0 && !DateOnly.TryParseExact(validFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Vigencia: fecha AAAA-MM-DD (vacía = hoy).");
        return Command("price-set", new { tariffId, productId = SelectedProduct!.Product.Id, presentationId = SelectedPresentation!.Presentation.Id, validFrom, priceCents },
            $"Fijar precio {Money(priceCents)} de {SelectedProduct.Product.Name} · {SelectedPresentation.Presentation.Name} en {Tariffs.FirstOrDefault(t => t.Id == tariffId)?.Name ?? tariffId}");
    }

    private bool CanSaveTariff() => TariffEditable;
    [RelayCommand(CanExecute = nameof(CanSaveTariff))]
    private Task SaveTariff()
    {
        var id = Code(NewTariff ? TariffId : SelectedTariff!.Id); var name = Name(TariffName); var sort = Number(TariffSort, "Orden", 0, 9999);
        return Command(NewTariff ? "tariff-create" : "tariff-update", new { id, name, sort }, (NewTariff ? "Crear tarifa " : "Guardar tarifa ") + name, NewTariff ? () => { createdTariff = id; } : null);
    }
    private bool CanToggleTariff() => Main && SelectedTariff is not null && !NewTariff && SelectedTariff.Id != "general";
    [RelayCommand(CanExecute = nameof(CanToggleTariff))]
    private Task ToggleTariff() => Command(SelectedTariff!.Active ? "tariff-deactivate" : "tariff-reactivate", new { id = SelectedTariff.Id },
        (SelectedTariff.Active ? "Desactivar tarifa " : "Reactivar tarifa ") + SelectedTariff.Name);

    private bool CanSaveTax() => TaxEditable;
    [RelayCommand(CanExecute = nameof(CanSaveTax))]
    private Task SaveTax()
    {
        var id = Code(NewTax ? TaxId : SelectedTax!.Id); var name = Name(TaxName);
        if (!decimal.TryParse(TaxRate.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate < 0 || rate > 100) throw new ArgumentException("Tipo: porcentaje entre 0 y 100 (por ejemplo 10 o 10,5).");
        return Command(NewTax ? "tax-create" : "tax-update", new { id, name, rate }, (NewTax ? "Crear impuesto " : "Guardar impuesto ") + name, NewTax ? () => { createdTax = id; } : null);
    }
    private bool CanToggleTax() => Main && SelectedTax is not null && !NewTax;
    [RelayCommand(CanExecute = nameof(CanToggleTax))]
    private Task ToggleTax() => Command(SelectedTax!.Active ? "tax-deactivate" : "tax-reactivate", new { id = SelectedTax.Id },
        (SelectedTax.Active ? "Desactivar impuesto " : "Reactivar impuesto ") + SelectedTax.Name);
}
