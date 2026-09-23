using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// E3: vista "ERP › Catálogo y tarifas" (solo rol main; el servidor lo impone ademas por permisos): categorias, productos con
// sus presentaciones y precios por tarifa, tarifas e impuestos. Patron lista + ficha de E2. Cada guardado es UN comando por la
// tuberia habitual (orden incierta persistida antes de enviar) y despues se relee el catalogo completo: la pantalla nunca
// muestra un estado que el servidor no haya confirmado. Nada se borra: se desactiva y se puede reactivar.
public sealed record CategoryRow(CategoryDto Category, int Depth)
{
    public string StateLabel => Category.Active ? "activa" : "desactivada";
    public override string ToString() => new string(' ', Depth * 4) + Category.Name + " (" + Category.Id + ") · " + StateLabel;
}
public sealed record ProductRow(ProductDto Product, string CategoryName, string PriceText)
{
    public string StateLabel => Product.Active ? "activo" : "desactivado";
    public override string ToString() => $"{Product.Name} · {CategoryName} · {PriceText} · {StateLabel}";
}
public sealed record PresentationRow(PresentationDto Presentation, string PriceText)
{
    public string StateLabel => Presentation.Active ? "activa" : "desactivada";
    public override string ToString() => $"{Presentation.Name} ({Presentation.Id}) · {PriceText} · {StateLabel}";
}

public sealed partial class CatalogViewModel(ShellViewModel shell) : ObservableObject
{
    private bool rendering;
    private CatalogDto? catalog;

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
    [ObservableProperty] private bool allCategories = true;
    // Fichas. NewX = alta: el codigo se teclea; en edicion el codigo es inmutable.
    [ObservableProperty] private bool newCategory; [ObservableProperty] private string categoryId = ""; [ObservableProperty] private string categoryName = "";
    [ObservableProperty] private string categoryParentId = ""; [ObservableProperty] private string categoryColor = ""; [ObservableProperty] private string categorySort = "0";
    [ObservableProperty] private bool newProduct; [ObservableProperty] private string productId = ""; [ObservableProperty] private string productName = "";
    [ObservableProperty] private string productCategoryId = ""; [ObservableProperty] private string productTaxId = "iva-10"; [ObservableProperty] private string productReference = "";
    [ObservableProperty] private string productSort = "0"; [ObservableProperty] private string productPresentationName = "Unidad";
    [ObservableProperty] private bool newPresentation; [ObservableProperty] private string presentationId = ""; [ObservableProperty] private string presentationName = ""; [ObservableProperty] private string presentationSort = "0";
    [ObservableProperty] private string priceTariffId = "general"; [ObservableProperty] private string priceValidFrom = ""; [ObservableProperty] private string priceText = "";
    [ObservableProperty] private bool newTariff; [ObservableProperty] private string tariffId = ""; [ObservableProperty] private string tariffName = ""; [ObservableProperty] private string tariffSort = "0";
    [ObservableProperty] private bool newTax; [ObservableProperty] private string taxId = ""; [ObservableProperty] private string taxName = ""; [ObservableProperty] private string taxRate = "10";
    [ObservableProperty] private string summary = "Sin datos de catálogo todavía.";

    public bool Visible { get; set; }
    // Desplegables de categoria: la primera entrada (codigo vacio) es "sin categoria" / "raiz".
    public CategoryDto[] ActiveCategories => [new("", "(sin categoría)", null, null, -1, true), .. catalog?.Categories.Where(c => c.Active) ?? []];
    public CategoryDto[] ParentChoices => [new("", "(raíz)", null, null, -1, true), .. catalog?.Categories.Where(c => c.Active && c.Id != SelectedCategory?.Category.Id) ?? []];
    public TariffDto[] ActiveTariffs => Tariffs.Where(t => t.Active).ToArray();
    public TaxDto[] ActiveTaxes => Taxes.Where(t => t.Active).ToArray();
    public bool HasCategories => Categories.Length > 0;
    public bool HasProducts => Products.Length > 0;
    public bool HasPresentations => Presentations.Length > 0;
    public string CategoryFormTitle => NewCategory ? "Nueva categoría" : SelectedCategory is null ? "Selecciona una categoría" : "Categoría " + SelectedCategory.Category.Id;
    public string ProductFormTitle => NewProduct ? "Nuevo producto" : SelectedProduct is null ? "Selecciona un producto" : "Producto " + SelectedProduct.Product.Id;
    public string PresentationFormTitle => NewPresentation ? "Nueva presentación" : SelectedPresentation is null ? "Selecciona una presentación" : "Presentación " + SelectedPresentation.Presentation.Id;
    public string TariffFormTitle => NewTariff ? "Nueva tarifa" : SelectedTariff is null ? "Selecciona una tarifa" : "Tarifa " + SelectedTariff.Id;
    public string TaxFormTitle => NewTax ? "Nuevo impuesto" : SelectedTax is null ? "Selecciona un impuesto" : "Impuesto " + SelectedTax.Id;
    private bool Main => shell.Writable && shell.IsMain;
    public bool CategoryEditable => Main && (NewCategory || SelectedCategory is not null);
    public bool ProductEditable => Main && (NewProduct || SelectedProduct is not null);
    public bool PresentationEditable => Main && (NewPresentation || SelectedPresentation is not null);
    public bool PriceEditable => Main && SelectedPresentation is not null && !NewPresentation;
    public bool TariffEditable => Main && (NewTariff || SelectedTariff is not null);
    public bool TaxEditable => Main && (NewTax || SelectedTax is not null);

    internal void Sync()
    {
        foreach (var name in new[] { nameof(CategoryEditable), nameof(ProductEditable), nameof(PresentationEditable), nameof(PriceEditable), nameof(TariffEditable), nameof(TaxEditable),
            nameof(CategoryFormTitle), nameof(ProductFormTitle), nameof(PresentationFormTitle), nameof(TariffFormTitle), nameof(TaxFormTitle),
            nameof(HasCategories), nameof(HasProducts), nameof(HasPresentations), nameof(ActiveCategories), nameof(ParentChoices), nameof(ActiveTariffs), nameof(ActiveTaxes) })
            OnPropertyChanged(name);
        foreach (var command in new IRelayCommand[] { StartCategoryCommand, SaveCategoryCommand, ToggleCategoryCommand, StartProductCommand, SaveProductCommand, ToggleProductCommand,
            StartPresentationCommand, SavePresentationCommand, TogglePresentationCommand, SetPriceCommand, StartTariffCommand, SaveTariffCommand, ToggleTariffCommand, StartTaxCommand, SaveTaxCommand, ToggleTaxCommand })
            command.NotifyCanExecuteChanged();
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            catalog = null; Categories = []; Products = []; Presentations = []; Tariffs = []; Taxes = [];
            SelectedCategory = null; SelectedProduct = null; SelectedPresentation = null; SelectedTariff = null; SelectedTax = null;
            NewCategory = NewProduct = NewPresentation = NewTariff = NewTax = false; Search = ""; AllCategories = true; Summary = "Sin datos de catálogo todavía.";
        }
        finally { rendering = false; }
        Sync();
    }

    internal async Task LoadAsync()
    {
        if (shell.Api is null || !shell.IsMain) return;
        Render(await shell.Api.GetAsync<CatalogDto>("erp/catalog"));
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

    // Tambien lo usan los checks del WPF sin servidor: la pantalla se compone solo de lo leido.
    public void Render(CatalogDto value)
    {
        rendering = true;
        try
        {
            var category = SelectedCategory?.Category.Id; var product = SelectedProduct?.Product.Id; var presentation = SelectedPresentation?.Presentation.Id;
            var tariff = SelectedTariff?.Id; var tax = SelectedTax?.Id;
            catalog = value; Tariffs = value.Tariffs; Taxes = value.Taxes;
            Categories = Flatten(value.Categories);
            SelectedCategory = Categories.FirstOrDefault(c => c.Category.Id == category);
            RenderProducts();
            SelectedProduct = Products.FirstOrDefault(p => p.Product.Id == product);
            RenderPresentations();
            SelectedPresentation = Presentations.FirstOrDefault(p => p.Presentation.Id == presentation);
            SelectedTariff = Tariffs.FirstOrDefault(t => t.Id == tariff);
            SelectedTax = Taxes.FirstOrDefault(t => t.Id == tax);
            var sellable = value.Products.Count(p => p.Active && p.Presentations.Any(s => s.Active && Current(s, "general", value.Today) is not null));
            Summary = $"{value.Products.Count(p => p.Active)} productos activos ({sellable} vendibles hoy) · {value.Categories.Count(c => c.Active)} categorías · {Tariffs.Count(t => t.Active)} tarifas · {Taxes.Count(t => t.Active)} impuestos · precios con IVA incluido";
            if (PriceValidFrom.Length == 0) PriceValidFrom = value.Today;
        }
        finally { rendering = false; }
        if (!NewCategory) FillCategoryForm(); if (!NewProduct) FillProductForm(); if (!NewPresentation) FillPresentationForm(); if (!NewTariff) FillTariffForm(); if (!NewTax) FillTaxForm();
        Sync();
    }

    // Arbol de categorias como lista con sangria: padres primero, hijos debajo por orden; los huerfanos al final.
    private static CategoryRow[] Flatten(CategoryDto[] categories)
    {
        var result = new List<CategoryRow>(); var placed = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? parent, int depth)
        {
            if (depth > 6) return;
            foreach (var c in categories.Where(c => c.ParentId == parent && placed.Add(c.Id)).OrderBy(c => c.Sort).ThenBy(c => c.Id, StringComparer.Ordinal))
            { result.Add(new(c, depth)); Add(c.Id, depth + 1); }
        }
        Add(null, 0);
        foreach (var c in categories.Where(c => placed.Add(c.Id))) result.Add(new(c, 0));
        return result.ToArray();
    }

    private void RenderProducts()
    {
        if (catalog is null) { Products = []; return; }
        var names = catalog.Categories.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);
        var text = Search.Trim();
        Products = catalog.Products
            .Where(p => AllCategories || SelectedCategory is null || p.CategoryId == SelectedCategory.Category.Id)
            .Where(p => text.Length == 0 || p.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) || p.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || (p.Reference?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(p => new ProductRow(p, p.CategoryId is null ? "sin categoría" : names.GetValueOrDefault(p.CategoryId, p.CategoryId),
                p.Presentations.Length == 0 ? "sin presentación" : string.Join(" / ", p.Presentations.Where(s => s.Active).Select(s => Current(s, "general", catalog.Today) is { } price ? $"{s.Name} {Money(price.PriceCents)}" : s.Name + " sin precio"))))
            .ToArray();
    }
    private void RenderPresentations() => Presentations = SelectedProduct is null ? [] : SelectedProduct.Product.Presentations.Select(p => new PresentationRow(p, PriceLabel(p))).ToArray();

    partial void OnSelectedCategoryChanged(CategoryRow? value)
    {
        if (rendering) return;
        NewCategory = false; FillCategoryForm();
        if (value is not null) AllCategories = false;
        rendering = true; try { RenderProducts(); SelectedProduct = null; RenderPresentations(); SelectedPresentation = null; } finally { rendering = false; }
        NewProduct = false; FillProductForm(); NewPresentation = false; FillPresentationForm(); Sync();
    }
    partial void OnAllCategoriesChanged(bool value) { if (rendering) return; rendering = true; try { RenderProducts(); } finally { rendering = false; } Sync(); }
    partial void OnSearchChanged(string value) { if (rendering) return; rendering = true; try { RenderProducts(); } finally { rendering = false; } Sync(); }
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

    private void FillCategoryForm()
    {
        var c = SelectedCategory?.Category;
        CategoryId = c?.Id ?? ""; CategoryName = c?.Name ?? ""; CategoryParentId = c?.ParentId ?? ""; CategoryColor = c?.Color ?? ""; CategorySort = (c?.Sort ?? 0).ToString();
    }
    private void FillProductForm()
    {
        var p = SelectedProduct?.Product;
        ProductId = p?.Id ?? ""; ProductName = p?.Name ?? ""; ProductCategoryId = p?.CategoryId ?? SelectedCategory?.Category.Id ?? ""; ProductTaxId = p?.TaxId ?? "iva-10";
        ProductReference = p?.Reference ?? ""; ProductSort = (p?.Sort ?? 0).ToString();
    }
    private void FillPresentationForm()
    {
        var p = SelectedPresentation?.Presentation;
        PresentationId = p?.Id ?? ""; PresentationName = p?.Name ?? ""; PresentationSort = (p?.Sort ?? 0).ToString();
        var current = p is null || catalog is null ? null : Current(p, PriceTariffId.Length == 0 ? "general" : PriceTariffId, catalog.Today);
        PriceText = current is null ? "" : (current.PriceCents / 100m).ToString("0.00", CultureInfo.GetCultureInfo("es-ES"));
    }
    partial void OnPriceTariffIdChanged(string value) { if (!rendering && !NewPresentation) FillPresentationForm(); }
    private void FillTariffForm() { TariffId = SelectedTariff?.Id ?? ""; TariffName = SelectedTariff?.Name ?? ""; TariffSort = (SelectedTariff?.Sort ?? 0).ToString(); }
    private void FillTaxForm() { TaxId = SelectedTax?.Id ?? ""; TaxName = SelectedTax?.Name ?? ""; TaxRate = (SelectedTax?.Rate ?? 10m).ToString(CultureInfo.InvariantCulture); }

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
    // Importe tecleado en euros ("9,50" o "9.50") a centimos.
    public static long Cents(string text)
    {
        var t = text.Trim().Replace("€", "").Replace(" ", "");
        if (t.Contains(',') && t.Contains('.')) t = t.Replace(".", "").Replace(',', '.'); else t = t.Replace(',', '.');
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 999_999.99m)
            throw new ArgumentException("Precio: introduce un importe en euros, por ejemplo 9,50.");
        return (long)decimal.Round(value * 100, 0, MidpointRounding.AwayFromZero);
    }

    private Task Command(string action, object body, string description) => shell.Run(async () =>
    {
        await shell.Api!.SendAsync("erp/catalog/commands/" + action, body, description);
        NewCategory = NewProduct = NewPresentation = NewTariff = NewTax = false;
        shell.Status = description + ": confirmado por el servidor.";
        await LoadAsync();
    });

    private bool CanStart() => Main;
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartCategory() { NewCategory = true; CategoryId = ""; CategoryName = ""; CategoryParentId = SelectedCategory?.Category.Id ?? ""; CategoryColor = ""; CategorySort = Categories.Length.ToString(); Sync(); }
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartProduct() { NewProduct = true; ProductId = ""; ProductName = ""; ProductCategoryId = SelectedCategory?.Category.Id ?? ""; ProductTaxId = "iva-10"; ProductReference = ""; ProductSort = Products.Length.ToString(); ProductPresentationName = "Unidad"; Sync(); }
    private bool CanStartPresentation() => Main && SelectedProduct is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanStartPresentation))] private void StartPresentation() { NewPresentation = true; PresentationId = ""; PresentationName = ""; PresentationSort = Presentations.Length.ToString(); PriceText = ""; Sync(); }
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartTariff() { NewTariff = true; TariffId = ""; TariffName = ""; TariffSort = Tariffs.Length.ToString(); Sync(); }
    [RelayCommand(CanExecute = nameof(CanStart))] private void StartTax() { NewTax = true; TaxId = ""; TaxName = ""; TaxRate = "10"; Sync(); }

    private bool CanSaveCategory() => CategoryEditable;
    [RelayCommand(CanExecute = nameof(CanSaveCategory))]
    private Task SaveCategory()
    {
        var id = Code(NewCategory ? CategoryId : SelectedCategory!.Category.Id); var name = Name(CategoryName); var sort = Number(CategorySort, "Orden", 0, 9999);
        var parentId = CategoryParentId.Trim(); var color = CategoryColor.Trim();
        return Command(NewCategory ? "category-create" : "category-update", new { id, name, parentId, color, sort }, (NewCategory ? "Crear categoría " : "Guardar categoría ") + id);
    }
    private bool CanToggleCategory() => Main && SelectedCategory is not null && !NewCategory;
    [RelayCommand(CanExecute = nameof(CanToggleCategory))]
    private Task ToggleCategory() => Command(SelectedCategory!.Category.Active ? "category-deactivate" : "category-reactivate", new { id = SelectedCategory.Category.Id },
        (SelectedCategory.Category.Active ? "Desactivar categoría " : "Reactivar categoría ") + SelectedCategory.Category.Id);

    private bool CanSaveProduct() => ProductEditable;
    [RelayCommand(CanExecute = nameof(CanSaveProduct))]
    private Task SaveProduct()
    {
        var id = Code(NewProduct ? ProductId : SelectedProduct!.Product.Id); var name = Name(ProductName); var sort = Number(ProductSort, "Orden", 0, 9999);
        var categoryId = ProductCategoryId.Trim(); var taxId = Code(ProductTaxId); var reference = ProductReference.Trim();
        return NewProduct
            ? Command("product-create", new { id, name, categoryId, taxId, reference, sort, presentation = Name(ProductPresentationName) }, "Crear producto " + id)
            : Command("product-update", new { id, name, categoryId, taxId, reference, sort }, "Guardar producto " + id);
    }
    private bool CanToggleProduct() => Main && SelectedProduct is not null && !NewProduct;
    [RelayCommand(CanExecute = nameof(CanToggleProduct))]
    private Task ToggleProduct() => Command(SelectedProduct!.Product.Active ? "product-deactivate" : "product-reactivate", new { id = SelectedProduct.Product.Id },
        (SelectedProduct.Product.Active ? "Desactivar producto " : "Reactivar producto ") + SelectedProduct.Product.Id);

    private bool CanSavePresentation() => PresentationEditable && SelectedProduct is not null;
    [RelayCommand(CanExecute = nameof(CanSavePresentation))]
    private Task SavePresentation()
    {
        var productId = SelectedProduct!.Product.Id; var id = Code(NewPresentation ? PresentationId : SelectedPresentation!.Presentation.Id);
        var name = Name(PresentationName); var sort = Number(PresentationSort, "Orden", 0, 9999);
        return Command(NewPresentation ? "presentation-create" : "presentation-update", new { productId, id, name, sort }, (NewPresentation ? "Crear presentación " : "Guardar presentación ") + productId + "/" + id);
    }
    private bool CanTogglePresentation() => Main && SelectedPresentation is not null && !NewPresentation;
    [RelayCommand(CanExecute = nameof(CanTogglePresentation))]
    private Task TogglePresentation() => Command(SelectedPresentation!.Presentation.Active ? "presentation-deactivate" : "presentation-reactivate",
        new { productId = SelectedProduct!.Product.Id, id = SelectedPresentation.Presentation.Id },
        (SelectedPresentation.Presentation.Active ? "Desactivar presentación " : "Reactivar presentación ") + SelectedPresentation.Presentation.Id);

    private bool CanSetPrice() => PriceEditable;
    [RelayCommand(CanExecute = nameof(CanSetPrice))]
    private Task SetPrice()
    {
        var tariffId = Code(PriceTariffId); var priceCents = Cents(PriceText); var validFrom = PriceValidFrom.Trim();
        if (validFrom.Length > 0 && !DateOnly.TryParseExact(validFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Vigencia: fecha AAAA-MM-DD (vacía = hoy).");
        return Command("price-set", new { tariffId, productId = SelectedProduct!.Product.Id, presentationId = SelectedPresentation!.Presentation.Id, validFrom, priceCents },
            $"Fijar precio {Money(priceCents)} de {SelectedProduct.Product.Id}/{SelectedPresentation.Presentation.Id} en {tariffId}");
    }

    private bool CanSaveTariff() => TariffEditable;
    [RelayCommand(CanExecute = nameof(CanSaveTariff))]
    private Task SaveTariff()
    {
        var id = Code(NewTariff ? TariffId : SelectedTariff!.Id); var name = Name(TariffName); var sort = Number(TariffSort, "Orden", 0, 9999);
        return Command(NewTariff ? "tariff-create" : "tariff-update", new { id, name, sort }, (NewTariff ? "Crear tarifa " : "Guardar tarifa ") + id);
    }
    private bool CanToggleTariff() => Main && SelectedTariff is not null && !NewTariff && SelectedTariff.Id != "general";
    [RelayCommand(CanExecute = nameof(CanToggleTariff))]
    private Task ToggleTariff() => Command(SelectedTariff!.Active ? "tariff-deactivate" : "tariff-reactivate", new { id = SelectedTariff.Id },
        (SelectedTariff.Active ? "Desactivar tarifa " : "Reactivar tarifa ") + SelectedTariff.Id);

    private bool CanSaveTax() => TaxEditable;
    [RelayCommand(CanExecute = nameof(CanSaveTax))]
    private Task SaveTax()
    {
        var id = Code(NewTax ? TaxId : SelectedTax!.Id); var name = Name(TaxName);
        if (!decimal.TryParse(TaxRate.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) || rate < 0 || rate > 100) throw new ArgumentException("Tipo: porcentaje entre 0 y 100 (por ejemplo 10 o 10,5).");
        return Command(NewTax ? "tax-create" : "tax-update", new { id, name, rate }, (NewTax ? "Crear impuesto " : "Guardar impuesto ") + id);
    }
    private bool CanToggleTax() => Main && SelectedTax is not null && !NewTax;
    [RelayCommand(CanExecute = nameof(CanToggleTax))]
    private Task ToggleTax() => Command(SelectedTax!.Active ? "tax-deactivate" : "tax-reactivate", new { id = SelectedTax.Id },
        (SelectedTax.Active ? "Desactivar impuesto " : "Reactivar impuesto ") + SelectedTax.Id);
}
