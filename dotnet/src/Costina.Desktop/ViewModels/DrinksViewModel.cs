using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// C3b (plan 2026-10-15, seccion 4.4, «Puesto principal (WPF), Comedor › ficha de la mesa»; maqueta, pestana «5 · En el PC»):
// «Añadir consumicion» en la ficha de una mesa abierta, sin salir del Comedor ni ir a Caja, y «Lo que lleva la mesa».
//  - El boton existe y se habilita SOLO porque el servidor anuncia la accion de sesion `add-consumptions` (sala y principal la tienen; el
//    nivel Puesto es un dispositivo de sala). Nunca pasa por RequestElevation: no pide la contrasena del responsable.
//  - Sin precios, tambien para el responsable: lee GET /catalog (sin importes), nunca /checkout/catalog. La busqueda, la agrupacion por
//    producto (Copa primero) y la ronda son las de Costina.Client (DrinksPicker, CatalogSearch, Round), con la tabla de casos comun con el
//    comandero; las fotos van por el cargador del Catalogo (ImageLoader, con la credencial de la conexion y una vez por huella).
//  - La RONDA es un borrador en memoria (Round): nunca va al almacen de la orden incierta (IPendingStore) antes de enviarse y nunca es una
//    cola. Se puede llenar con una orden incierta pendiente, pero no enviar. «Enviar» es UNA orden add-consumptions con la version de la
//    ficha LEIDA en ese momento (F01). version_conflict deja la ronda, relee y ofrece «La mesa ha cambiado: enviar de nuevo» (otra orden,
//    otra clave). Una relectura del tablero no la toca; cambiar de mesa o de vista con ella pide confirmacion.
//  - «Lo que lleva la mesa» es la lista `consumptions` del tablero (nombre, formato, cantidad, comensal, hora y «Anulado en Caja»), sin
//    importes ni totales (ADR-007, apartado C3b: no es la cuenta provisional).
// Ninguna regla de negocio: el servidor fija el importe, comprueba el comensal y rechaza un plato de cocina (use_add_dish).

// Hueco de la foto de una fila: la miniatura se pide cuando la fila llega a pintarse (el enlace lee Thumbnail), una vez por huella, con
// el cargador del Catalogo; mientras tanto (o sin foto) se ven las iniciales del nombre.
public abstract class DrinkPhotoRow : ObservableObject
{
    private readonly ImageLoader? images;
    private readonly string productId;
    private readonly string? sha;
    private BitmapSource? thumbnail;
    private bool requested;
    protected DrinkPhotoRow(string productId, string name, string? sha, ImageLoader? images)
    {
        this.productId = productId; this.sha = sha; this.images = images; Initials = DrinksViewModel.Initials(name);
        if (sha is not null) thumbnail = images?.Thumbnail(sha);   // ya decodificada: aparece sin esperar
    }
    public string Initials { get; }
    public BitmapSource? Thumbnail
    {
        get
        {
            if (thumbnail is null && !requested && images is not null && sha is not null)
            {
                requested = true;
                images.RequestThumbnail(productId, sha, image => { if (image is null) return; thumbnail = image; OnPropertyChanged(nameof(Thumbnail)); });
            }
            return thumbnail;
        }
    }
}

// Un boton de una fila: su presentacion en dos alturas («Botella» y, debajo, «0.75 L»; «Copa» y «Añadir» en una) y lo que dice a un lector.
public sealed record DrinkChoice(DrinkProduct Product, DrinkButton Button, string Top, string Bottom, string Spoken)
{
    public bool HasBottom => Bottom.Length > 0;
}
// Fila de un producto: foto, nombre, familia › region y un boton por presentacion; «n en la ronda» si ya esta en ella.
public sealed partial class DrinkRow(DrinkProduct product, string detail, DrinkChoice[] choices, ImageLoader? images) : DrinkPhotoRow(product.Id, product.Name, product.ImageSha, images)
{
    public DrinkProduct Product { get; } = product;
    public string Name => Product.Name;
    public string Detail { get; } = detail;
    public bool HasDetail => Detail.Length > 0;
    public DrinkChoice[] Choices { get; } = choices;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(InRoundText), nameof(HasInRound))] private int inRound;
    public bool HasInRound => InRound > 0;
    public string InRoundText => InRound > 0 ? InRound.ToString(CultureInfo.InvariantCulture) + " en la ronda" : "";
}
// «En esta mesa»: lo ya enviado, para repetirlo con +1.
public sealed class RepeatRow(TableRepeat repeat, DrinkChoice choice, ImageLoader? images) : DrinkPhotoRow(repeat.ProductId, repeat.Name, repeat.ImageSha, images)
{
    public string Name { get; } = repeat.Name;
    public string Detail { get; } = repeat.Detail;
    public DrinkChoice Choice { get; } = choice;
    public string Spoken => "Uno más de " + repeat.Name + (repeat.Detail.Length > 0 ? ", " + repeat.Detail : "");
}
// Ficha de acceso (Por copa o una familia) y de subfamilia. Objetos estables: su cifra y su marca cambian en el sitio.
public sealed partial class ChipRow(string key, string label, bool glass, string? family) : ObservableObject
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool Glass { get; } = glass;
    public string? Family { get; } = family;
    [ObservableProperty] private int count;
    [ObservableProperty] private bool isCurrent;
    public string Spoken => $"{Label}, {Count}";
}
public sealed partial class SubChipRow(string? name, string label) : ObservableObject
{
    public string? Name { get; } = name;   // null = todas
    public string Label { get; } = label;
    [ObservableProperty] private bool isCurrent;
}
// Para quien es una linea: Key "" = toda la mesa; "n" = el comensal n, con sus restricciones ESCRITAS (nunca solo color).
public sealed record GuestChoice(string Key, string Label)
{
    public override string ToString() => Label;
}
// Una linea de la ronda en la bandeja. Cambiar «Para» llama al dueno (la ronda puede juntar dos lineas iguales).
public sealed class RoundLineRow(DrinksViewModel owner, int index, RoundLine line, bool bad, bool sending) : ObservableObject
{
    public int Index { get; } = index;
    public string Text { get; } = line.Text;
    public int Quantity { get; } = line.Quantity;
    public string QuantityText => Quantity.ToString(CultureInfo.InvariantCulture);
    public bool Bad { get; } = bad;
    public bool Editable { get; } = !sending;
    public bool CanPlus => Editable && Quantity < Round.MaxQuantity;
    public bool MinusRemoves => Quantity <= 1;
    public string MinusSpoken => MinusRemoves ? "Quitar " + Text : "Uno menos de " + Text;
    public string PlusSpoken => "Uno más de " + Text;
    public string ForSpoken => "Para quién: " + Text;
    // Lo elegido se ve al momento (el enlace relee esta propiedad tras escribirla); la bandeja se rehace despues con la ronda ya cambiada.
    private string guestKey = line.GuestPosition is { } guest ? guest.ToString(CultureInfo.InvariantCulture) : "";
    public string GuestKey
    {
        get => guestKey;
        set { if (value is not null && value != guestKey) { guestKey = value; owner.ChangeGuest(Index, value); } }   // null: el desplegable perdio su lista, no una eleccion
    }
}
// «Lo que lleva la mesa»: una linea ya enviada, sin importes.
public sealed record HadRow(string Id, string Quantity, string Name, string Detail, string Time, bool Voided)
{
    public bool HasDetail => Detail.Length > 0;
}

public sealed partial class DrinksViewModel(ShellViewModel shell, ServiceViewModel service) : ObservableObject
{
    // Textos (los del comandero, frontend/pwa/src/bodega.ts). Sin palabras de caja: los importes, en Caja.
    public const int Page = 60;
    public const string RoundAction = "add-consumptions";
    public const string EmptyRound = "Ronda vacía. Copa o Botella suman 1 para toda la mesa.";
    public const string RoundSent = "Ronda enviada: ya sale en «Lo que lleva la mesa».";
    public const string RoundHint = "Sin enviar. Si cierras el programa o sales de la mesa, la ronda se pierde.";
    public const string RoundConflict = "La mesa ha cambiado mientras tanto y se ha vuelto a leer: revisa la ronda y envíala de nuevo.";
    public const string LockedHint = "Hay una orden sin confirmar: resuélvela arriba antes de enviar la ronda. Mientras tanto puedes seguir llenándola.";
    public const string HadTitle = "Lo que lleva la mesa";
    public const string HadSub = "Bebidas y extras ya enviados";
    public const string HadEmptyText = "Aún no se ha enviado ninguna bebida ni ningún extra.";
    public const string HadFoot = "Los importes solo se ven en Caja.";
    public const string Voided = "Anulado en Caja";
    public const string OneMoreLetter = "Escribe una letra más para buscar.";
    public const string NoResultsHint = "Busca por nombre, familia, región o referencia. La uva o la añada, solo si están en el nombre.";
    public const string GlassKey = "glass", OtherKey = "other";
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private Round round = new();
    private string? roundService;          // la mesa (servicio) de la ronda: la de la ficha leida al abrir el selector (F01)
    private string roundTable = "";
    private DrinkProduct[] products = [];
    private bool reading, readFailed, everRead;
    private string? chipKey, subfamily;
    private int visible = Page;
    private bool entrance;                 // la vista de entrada del selector (sin buscar y en la primera ficha): lleva «En esta mesa»
    private string addMessage = "";
    private Action? leaveProceed;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowPicker), nameof(ShowFicha), nameof(ShowTrayInFicha))] private bool isOpen;
    [ObservableProperty] private string query = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasChips))] private ChipRow[] chips = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSubChips))] private SubChipRow[] subChips = [];
    [ObservableProperty] private DrinkRow[] rows = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasRepeats))] private RepeatRow[] repeats = [];
    [ObservableProperty] private string listTitle = "";
    [ObservableProperty] private string listDetail = "";
    [ObservableProperty] private string notice = "";
    [ObservableProperty] private string hint = "";
    [ObservableProperty] private string moreText = "";
    [ObservableProperty] private RoundLineRow[] roundLines = [];
    [ObservableProperty] private GuestChoice[] guestChoices = [];
    [ObservableProperty] private bool trayOpen;
    [ObservableProperty] private string roundMessage = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EmptyRoundText))] private string sentText = "";
    [ObservableProperty] private bool leaving;
    [ObservableProperty] private string leaveText = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HadEmpty))] private HadRow[] had = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HadEmpty), nameof(ShowHad))] private bool hasHadList;

    partial void OnQueryChanged(string value) { visible = Page; Render(); }

    // ---- Lo que anuncia el servidor y lo que se ve ----
    public bool CanConsume => shell.Connected && shell.Session?.Actions?.Contains(RoundAction, StringComparer.Ordinal) == true;
    public bool ShowAdd => CanConsume;
    public bool ShowPicker => IsOpen && service.HasContext;
    public bool ShowFicha => !IsOpen && service.HasContext;
    public bool ShowHad => service.HasContext && (HasHadList || CanConsume);
    public bool HadEmpty => HasHadList && Had.Length == 0;
    public bool HasChips => Chips.Length > 0;
    public bool HasSubChips => SubChips.Length > 0;
    public bool HasRepeats => Repeats.Length > 0;
    public string Title => roundTable.Length > 0 ? "Bodega y bebidas · " + roundTable : "Bodega y bebidas";
    public RoundLine[] Lines => [.. round.Lines];
    public bool HasLines => round.Lines.Count > 0;
    public bool Sending => round.Sending;
    public bool Conflict => round.Conflict;
    public string Summary => round.Summary;
    public string Names => string.Join(", ", round.Lines.Select(l => l.Text));
    public string SendText => round.Sending ? "Enviando…" : round.SendLabel;
    public string EmptyRoundText => SentText.Length > 0 ? SentText : EmptyRound;
    public bool ShowTrayInFicha => HasLines && !IsOpen;
    public string TrayToggleText => TrayOpen ? "Ocultar la ronda" : "Ver la ronda";
    // Una orden sin confirmar, una bloqueada u otra en curso: la ronda se llena, pero no sale (D6.7: una sola orden incierta).
    public bool Locked => shell.Busy || shell.Pending is not null || shell.Blocked is not null;
    public string LockedText => HasLines && (shell.Pending is not null || shell.Blocked is not null) ? LockedHint : "";
    partial void OnTrayOpenChanged(bool value) => OnPropertyChanged(nameof(TrayToggleText));

    internal void Sync()
    {
        foreach (var name in new[] { nameof(CanConsume), nameof(ShowAdd), nameof(ShowPicker), nameof(ShowFicha), nameof(ShowHad), nameof(Locked), nameof(LockedText),
                     nameof(SendText), nameof(ShowTrayInFicha), nameof(Title) })
            OnPropertyChanged(name);
        OpenCommand.NotifyCanExecuteChanged(); SendCommand.NotifyCanExecuteChanged();
    }

    // ---- Abrir y cerrar el selector, dentro de la ficha ----
    private bool CanOpenPicker() => CanConsume && service.ReadContext is not null && !shell.Busy && !IsOpen;
    [RelayCommand(CanExecute = nameof(CanOpenPicker))]
    private Task Open()
    {
        var context = service.ReadContext;
        if (context is null) return Task.CompletedTask;
        if (roundService != context.Data.Id) { round = new Round(); roundService = context.Data.Id; RoundMessage = ""; addMessage = ""; }
        roundTable = service.TableName(context);
        SentText = ""; IsOpen = true;
        Render(); RenderRound();
        // El catalogo se lee al abrir el selector (GET /catalog: sin importes). Nunca /checkout/catalog, ni para el responsable.
        return shell.Run(LoadCatalogAsync);
    }
    [RelayCommand] private void Close() { IsOpen = false; Query = ""; Sync(); }

    internal async Task LoadCatalogAsync()
    {
        if (shell.Api is null) return;
        reading = true; readFailed = false; Render();
        try
        {
            products = DrinksPicker.Products(await shell.Api.GetAsync<CatalogItem[]>("catalog"));
            everRead = true;
        }
        catch { readFailed = true; throw; }
        finally { reading = false; Render(); }
    }

    // ---- Accesos, busqueda y filas ----
    [RelayCommand] private void PickChip(ChipRow? chip) { if (chip is null) return; chipKey = chip.Key; subfamily = null; visible = Page; if (Query.Length > 0) Query = ""; else Render(); }
    [RelayCommand] private void PickSub(SubChipRow? sub) { if (sub is null) return; subfamily = sub.Name; visible = Page; Render(); }
    [RelayCommand] private void ShowMore() { visible += Page; Render(); }
    [RelayCommand] private void ClearQuery() => Query = "";

    // Accesos: «Por copa» (si el servidor marca alguna copa) y una ficha por familia; el primero es la vista de entrada (bodega.ts).
    private static (string Key, string Label, int Count, bool Glass, string? Family)[] ChipsOf(DrinkProduct[] list)
    {
        var glasses = DrinksPicker.ByGlass(list).Length;
        var families = DrinksPicker.Families(list).Select(f => (f.Family is null ? OtherKey : "family:" + f.Family, f.Label, f.Count, false, f.Family));
        return glasses > 0 ? [(GlassKey, "Por copa", glasses, true, null), .. families] : [.. families];
    }

    private void Render()
    {
        var found = ChipsOf(products);
        if (!found.Select(c => c.Key).SequenceEqual(Chips.Select(c => c.Key), StringComparer.Ordinal))
            Chips = found.Select(c => new ChipRow(c.Key, c.Label, c.Glass, c.Family)).ToArray();
        for (var i = 0; i < found.Length; i++) Chips[i].Count = found[i].Count;
        var chip = Chips.FirstOrDefault(c => c.Key == chipKey) ?? Chips.FirstOrDefault();
        var searching = DrinksPicker.Search(products, Query);
        foreach (var c in Chips) c.IsCurrent = searching is null && ReferenceEquals(c, chip);

        SubfamilyChip[] subs = chip is null || chip.Glass || searching is not null ? [] : DrinksPicker.Subfamilies(products, chip.Family);
        var sub = subs.Any(s => s.Name == subfamily) ? subfamily : null;
        SubChipRow[] subRows = subs.Length == 0 ? [] : [new SubChipRow(null, "Todas"), .. subs.Select(s => new SubChipRow(s.Name, s.Name))];
        foreach (var row in subRows) row.IsCurrent = row.Name == sub;
        if (!subRows.Select(s => s.Name).SequenceEqual(SubChips.Select(s => s.Name))) SubChips = subRows;
        else for (var i = 0; i < subRows.Length; i++) SubChips[i].IsCurrent = subRows[i].IsCurrent;

        DrinkProduct[] listed = chip is null ? [] : chip.Glass ? DrinksPicker.ByGlass(products) : DrinksPicker.InFamily(products, chip.Family, sub);
        var source = searching ?? listed;
        var detailQuery = searching is null ? "" : Query;
        var images = shell.Catalog.Images;
        Rows = source.Take(visible).Select(p => new DrinkRow(p, DrinksPicker.Detail(p, detailQuery), ChoicesOf(p), images)).ToArray();
        MarkRows();
        var left = source.Length - Rows.Length;
        MoreText = left > 0 ? $"Ver {Math.Min(left, Page)} más (quedan {left})" : "";
        entrance = searching is null && chip is not null && ReferenceEquals(chip, Chips.FirstOrDefault());
        RenderRepeats();

        Notice = !everRead ? reading ? "Leyendo la bodega y bebidas…" : readFailed ? "No se pudo leer la bodega y bebidas. Cierra y vuelve a abrir «Añadir consumición» para intentarlo de nuevo." : ""
            : products.Length == 0 ? "No hay bebidas ni extras a la venta en el catálogo."
            : readFailed ? "No se pudo releer la bodega y bebidas: se ve la última lista leída." : "";
        Hint = searching is null && CatalogSearch.Fold(Query).Trim().Length == 1 ? OneMoreLetter : "";
        if (searching is not null)
        {
            ListTitle = searching.Length > 0 ? Labels.Plural(searching.Length, "resultado", "resultados") : $"No hay nada con «{Query.Trim()}».";
            ListDetail = searching.Length > 0 ? "en toda la bodega y bebidas" : NoResultsHint;
        }
        else
        {
            ListTitle = chip?.Label ?? "";
            ListDetail = chip is null ? "" : (chip.Glass ? Labels.Plural(listed.Length, "vino por copa", "vinos por copa") : Labels.Plural(listed.Length, "producto", "productos")) + " · de la A a la Z";
        }
    }
    // «En esta mesa» solo en la vista de entrada (sin buscar y en la primera ficha), con lo enviado segun la ultima lectura de la ficha.
    private void RenderRepeats()
    {
        var images = shell.Catalog.Images;
        RepeatRow[] next = !entrance ? [] : DrinksPicker.AtThisTable(service.ReadEntry?.Consumptions ?? [], products)
            .Select(r => new RepeatRow(r, Choice(r.Product, r.Button), images)).ToArray();
        if (!next.Select(r => r.Choice).SequenceEqual(Repeats.Select(r => r.Choice))) Repeats = next;
    }
    private static DrinkChoice[] ChoicesOf(DrinkProduct product) => product.Buttons.Select(b => Choice(product, b)).ToArray();
    private static DrinkChoice Choice(DrinkProduct product, DrinkButton button)
    {
        var single = product.Buttons.Count == 1;
        var (top, bottom) = FormatParts(button.Label);
        var spoken = single ? "Añadir 1 " + DrinksPicker.LineText(product.Name, button.PresentationId, button.Presentation) : $"Añadir 1 {button.Presentation} de {product.Name}";
        return new(product, button, top, bottom, spoken);
    }
    // «Botella 0.75 L» se escribe en dos alturas («Botella» y, debajo, «0.75 L»), como en el comandero; «Copa» y «Añadir», en una.
    public static (string Top, string Bottom) FormatParts(string label)
    {
        if (label == DrinksPicker.Add) return (label, "");
        var text = label.Trim(); var cut = text.IndexOf(' ', StringComparison.Ordinal);
        return cut < 0 ? (text, "") : (text[..cut], text[(cut + 1)..].Trim());
    }
    // Iniciales del recuadro sin foto (frontend/pwa/src/photos.ts): las dos primeras palabras sin las de enlace ni las cifras («Lalama 2019»
    // -> «L»); un nombre sin palabras, su primer caracter.
    private static readonly HashSet<string> Links = new(["de", "del", "la", "las", "el", "los", "y", "e", "a", "con", "sin", "en", "al"], StringComparer.Ordinal);
    public static string Initials(string name)
    {
        var words = Regex.Split(name, @"[^\p{L}\p{Nd}]+").Where(w => w.Length > 0 && char.IsLetter(w, 0)).ToArray();
        if (words.Length == 0) { var trimmed = name.Trim(); return trimmed.Length == 0 ? "?" : char.ConvertFromUtf32(char.ConvertToUtf32(trimmed, 0)).ToUpper(Spanish); }
        var second = words.Skip(1).FirstOrDefault(w => !Links.Contains(w.ToLower(Spanish)));
        return string.Concat(new[] { words[0], second }.OfType<string>().Select(w => char.ConvertFromUtf32(char.ConvertToUtf32(w, 0)))).ToUpper(Spanish);
    }
    private void MarkRows() { foreach (var row in Rows) row.InRound = round.UnitsOf(row.Product.Id); }

    // ---- La ronda (borrador en memoria) ----
    [RelayCommand]
    private void Add(DrinkChoice? choice)
    {
        if (choice is null || !IsOpen) return;
        var result = round.Add(DrinksPicker.Item(choice.Product, choice.Button));
        addMessage = result switch
        {
            RoundAdd.Full => $"La ronda ya tiene {Round.MaxLines} líneas: envíala y sigue en otra.",
            RoundAdd.Max => $"Como mucho {Round.MaxQuantity} en una misma línea de la ronda.",
            RoundAdd.Sending => "La ronda se está enviando: espera a que termine.",
            _ => ""
        };
        if (result is RoundAdd.Added or RoundAdd.More) SentText = "";
        RenderRound();
    }
    [RelayCommand] private void Repeat(RepeatRow? repeat) { if (repeat is not null) Add(repeat.Choice); }
    [RelayCommand] private void Increment(RoundLineRow? line) { if (line is not null && round.Increment(line.Index)) Changed(); }
    [RelayCommand] private void Decrement(RoundLineRow? line) { if (line is not null && round.Decrement(line.Index)) Changed(); }
    [RelayCommand] private void ToggleTray() => TrayOpen = !TrayOpen && HasLines;
    // Desde el desplegable «Para» de una linea: la bandeja se rehace despues de que el desplegable termine su propio cambio de eleccion.
    internal void ChangeGuest(int index, string key)
    {
        int? guest = key.Length == 0 ? null : int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var position) ? position : null;
        if ((key.Length == 0 || guest is not null) && round.SetGuest(index, guest)) addMessage = "";
        // Siempre se rehace: si la ronda no admitio el cambio (se esta enviando), el desplegable vuelve a lo que de verdad lleva la linea.
        if (Application.Current?.Dispatcher is { } dispatcher) dispatcher.InvokeAsync(RenderRound, DispatcherPriority.Background); else RenderRound();
    }
    private void Changed() { addMessage = ""; RenderRound(); }

    private void RenderRound()
    {
        RoundLines = round.Lines.Select((line, index) => new RoundLineRow(this, index, line, round.Problem?.Line == index, round.Sending)).ToArray();
        RoundMessage = ProblemText() is { Length: > 0 } problem ? problem : addMessage;
        if (!HasLines) TrayOpen = false;
        MarkRows();
        foreach (var name in new[] { nameof(Lines), nameof(HasLines), nameof(Summary), nameof(Names), nameof(SendText), nameof(Sending), nameof(Conflict), nameof(ShowTrayInFicha),
                     nameof(LockedText), nameof(TrayToggleText) })
            OnPropertyChanged(name);
        SendCommand.NotifyCanExecuteChanged();
    }
    // El ultimo rechazo, con la linea que el servidor dice (desde 0) si la hay y sigue en la ronda (bodega.ts, roundProblem).
    private string ProblemText()
    {
        if (round.Problem is not { } problem) return "";
        if (problem.Code == "version_conflict") return RoundConflict;
        var text = Labels.Rejection(problem.Code);
        return problem.Line is { } line && line >= 0 && line < round.Lines.Count ? $"Línea {line + 1}, {round.Lines[line].Text}: {text}" : text;
    }

    // ---- Enviar: UNA orden add-consumptions con la version de la ficha leida (F01) ----
    private bool CanSendRound() => CanConsume && round.CanSend(Locked) && service.ReadContext?.Data.Id is { } id && id == roundService;
    [RelayCommand(CanExecute = nameof(CanSendRound))]
    private Task Send() => shell.Run(async () =>
    {
        var context = service.ReadContext is { } read && read.Data.Id == roundService ? read
            : throw new InvalidOperationException("La ronda es de otra mesa: vuelve a abrir «Añadir consumición».");
        var api = shell.Api ?? throw new InvalidOperationException("Sin conexión con el servidor: la ronda sigue sin enviar.");
        if (api.Pending is not null || api.Blocked is not null) throw new InvalidOperationException("Resuelve primero la orden sin confirmar: la ronda sigue sin enviar.");
        var request = round.Begin(context.Version, roundTable);
        if (request is null) return;
        var path = "dining/services/" + ApiClient.Segment(context.Data.Id) + "/commands/" + RoundAction;
        var body = JsonSerializer.Serialize(request.Body, ApiClient.Json);
        RenderRound();
        try
        {
            try { await api.SendAsync(path, request.Body, request.Description); }
            catch (ApiError rejected) when (rejected.Status != 401 && api.Pending is null)
            {
                // Rechazo definitivo (la orden ya se cerro como no aplicada): la ronda sigue entera, con el motivo y la linea.
                round.Settle(RoundOutcome.Rejected, rejected.Code, rejected.Line);
                if (rejected.Line is not null) TrayOpen = true;
                RenderRound();
                if (rejected.Code == "version_conflict") { await shell.RefreshAll(); shell.Status = RoundConflict; }
                else shell.Status = "La ronda no se ha enviado: " + Labels.Rejection(rejected.Code) + " Sigue en la bandeja, sin enviar.";
                return;
            }
            catch (Exception) when (api.Pending is { } pending && pending.Path == path && pending.Body == body)
            {
                // Sin confirmar: lo enviado YA es la orden incierta (su aviso la muestra y se reintenta identica); la ronda se vacia para no
                // mandarlo dos veces.
                round.Settle(RoundOutcome.Unconfirmed); RenderRound();
                throw;
            }
            catch (Exception notSent) when (api.Pending is null && notSent is not ApiError)
            {
                // No salio: el hueco de la orden incierta estaba ocupado o el almacen no pudo guardarla. La ronda sigue igual.
                round.Settle(RoundOutcome.NotSent); RenderRound();
                shell.Status = "La ronda no se ha enviado" + (notSent is InvalidOperationException ? ": " + notSent.Message : ": no se pudo guardar la orden en este PC antes de mandarla.")
                    + " Sigue en la bandeja.";
                return;
            }
            round.Settle(RoundOutcome.Confirmed);
            SentText = RoundSent; RenderRound();
            await shell.RefreshAll();
            shell.Status = "Operación confirmada por el servidor: ronda enviada a " + roundTable + ".";
        }
        finally
        {
            if (round.Sending) { round.Settle(RoundOutcome.NotSent); RenderRound(); }
        }
    });

    // ---- Salir con una ronda sin enviar: se pregunta (en la propia vista, sin ventanas modales) ----
    // where completa «Si …, se pierde»: «cambias de mesa», «vas a «Inicio»»... Devuelve true si ha preguntado (la salida espera).
    public bool AskLeave(string where, Action proceed)
    {
        if (!HasLines || round.Sending) return false;
        LeaveText = $"La ronda de {roundTable} tiene {round.Units} sin enviar. Si {where}, se pierde: nada de ella ha llegado al servidor.";
        leaveProceed = proceed; Leaving = true;
        return true;
    }
    [RelayCommand] private void Stay() { Leaving = false; leaveProceed = null; }
    [RelayCommand]
    private void LeaveAnyway()
    {
        var proceed = leaveProceed;
        Leaving = false; leaveProceed = null;
        Discard();
        proceed?.Invoke();
    }

    // La ronda y el selector se cierran: otra mesa, la ficha ya no es la de la ronda o se desconecta. Nada se envia.
    internal void Discard()
    {
        if (round.Sending) return;
        round = new Round(); roundService = null; addMessage = ""; RoundMessage = ""; SentText = "";
        IsOpen = false; if (Query.Length > 0) Query = "";
        RenderRound(); Sync();
    }

    internal void Clear()
    {
        round = new Round(); roundService = null; roundTable = ""; products = []; everRead = reading = readFailed = false; chipKey = subfamily = null; visible = Page;
        addMessage = ""; leaveProceed = null; Leaving = false; SentText = ""; IsOpen = false; Query = "";
        Had = []; HasHadList = false; GuestChoices = [];
        Render(); RenderRound(); Sync();
    }

    // Tras cada lectura correcta de la ficha: lo que lleva la mesa, para quien puede ser cada linea y «En esta mesa». La ronda NO se toca;
    // solo se pierde si la lectura ya no es la de su mesa (se libero o se cerro en otro puesto), y se dice.
    internal void OnRead()
    {
        var context = service.ReadContext;
        if (context is not null && roundService is not null && context.Data.Id != roundService)
        {
            var lost = HasLines; var table = roundTable;
            Discard();
            if (lost) shell.Status = $"La ronda de {table} se ha perdido: esa mesa ya no está abierta en el servidor. No se envió nada de ella.";
        }
        if (context is not null && context.Data.Id == roundService) roundTable = service.TableName(context);
        var guests = GuestsOf(context?.Data);
        if (!guests.SequenceEqual(GuestChoices)) GuestChoices = guests;
        var entry = service.ReadEntry;
        HasHadList = entry?.Consumptions is not null;
        var had = (entry?.Consumptions ?? []).Select(HadOf).ToArray();
        if (!had.SequenceEqual(Had)) Had = had;
        if (IsOpen) RenderRepeats();
        Sync();
    }
    private static GuestChoice[] GuestsOf(DiningDto? dining)
    {
        if (dining is null) return [];
        var restrictions = dining.Restrictions ?? [];
        return [new("", Labels.Guest(null)), .. Enumerable.Range(1, Math.Max(0, dining.Pax)).Select(g => new GuestChoice(g.ToString(CultureInfo.InvariantCulture),
            Labels.Guest(g) + string.Concat(restrictions.Where(r => r.GuestPosition == g).Select(r => $" · {r.KindLabel} {r.Substance} ({r.SeverityLabel})"))))];
    }
    private static HadRow HadOf(ConsumptionDto line) => new(line.Id, line.Quantity.ToString(CultureInfo.InvariantCulture) + "×", line.Name,
        string.Join(" · ", new[] { line.Presentation?.Trim() ?? "", Labels.Guest(line.GuestPosition) }.Where(part => part.Length > 0)),
        line.At is { } at ? Labels.Clock(at) : "", line.Voided);
}
