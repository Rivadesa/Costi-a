using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Costina.Client;

namespace Costina.Desktop.ViewModels;

// Superficie Cuenta/Caja (solo rol main; el servidor lo garantiza ademas por permisos).
// La habilitacion sale de las affordances del AccountDto; el importe se valida como formato,
// nunca como regla de negocio (el servidor decide precios y saldos). Un comando solo se construye
// desde la cuenta leida correctamente que coincide con la seleccionada: nunca se cobra en otra.
// C1: el desplegable de cuentas muestra el nombre de la mesa y el estado en castellano (AccountRow); la seleccion sigue siendo la
// cuenta leida (SelectedAccount). Las ordenes pendientes se describen por la mesa, no por el identificador del servicio.
// C2b (plan 2026-10-14): lista y ficha, como el Comedor. A la izquierda, las cuentas abiertas en tarjetas (mesa, sala, personas, total y
// pendiente) con buscador; a la derecha, la ficha de la cuenta elegida: cabecera con tres cifras, consumos con la fila de anadir, cobro
// con las formas de pago en botones y "Otras operaciones". Nada de eso decide: la lista sigue moviendo SelectedAccount (la lectura de
// siempre, F01), la ficha se compone SOLO del contexto (la cuenta leida que coincide con la elegida) y cada boton sigue con su orden, su
// cuerpo y su CanExecute de siempre. El total y el pendiente de las demas tarjetas salen de leer cada cuenta abierta (el listado del
// servidor solo trae servicio, mesa y estado; C2b no cambia el motor): una lectura que falla deja la tarjeta "Sin leer", nunca un importe
// inventado, y elegir otra tarjeta solo relee la elegida. La sala y las personas salen del plano del Comedor ya leido.
public sealed record AccountRow(AccountChoice Choice, string Text)
{
    public string Table { get; init; } = "";
    public string Detail { get; init; } = "";
    public string TotalText { get; init; } = "—";
    public string PendingText { get; init; } = "Sin leer";
    public string Coverage { get; init; } = "";
    public string CoverageTone { get; init; } = "other";
    public bool HasCoverage => Coverage.Length > 0;
}
// C2b: fila de la tabla de consumos (concepto con su precio unitario o su motivo de anulacion debajo) y de la lista de pagos.
public sealed record ChargeRow(string Description, string Note, string Quantity, string Amount, bool Voided);
public sealed record MoneyRow(string What, string Detail, string Amount);
// E3.3: el buscador de consumos de Caja agrupa los vendibles por producto, con un boton por presentacion (Copa, Botella...): la misma
// regla de busqueda que tendra el comandero en C3b (sin tildes; por nombre, referencia y familia). Elegir un boton solo mueve
// SelectedProduct: la orden add-product no cambia.
public sealed record ProductGroup(string Id, string Name, string Detail, PresentationButton[] Presentations);
public sealed record PresentationButton(ProductChoice Choice, string Label, bool IsChosen);

public sealed partial class CheckoutViewModel(ShellViewModel shell) : ObservableObject
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private bool rendering;
    private string? accountServiceId;   // destino solicitado; no es el contexto de un comando
    private int generation;
    // C2b (revision): a que cuenta pertenece lo escrito en el importe y el motivo. Elegir otra tarjeta (o que una relectura elija otra)
    // los vacia: el pendiente rellenado con "Todo lo pendiente" para una mesa nunca se cobra ni se devuelve en otra (espiritu de F01).
    private string? amountAccount;
    // C2b: ultima lectura de cada cuenta abierta, solo para pintar sus tarjetas (nunca es el contexto de una orden).
    private Dictionary<string, AccountDto> summaries = new(StringComparer.Ordinal);
    private bool summariesComplete;

    [ObservableProperty] private AccountChoice[] accounts = [];
    [ObservableProperty] private AccountRow[] accountItems = [];
    [ObservableProperty] private AccountChoice? selectedAccount;
    [ObservableProperty] private Versioned<AccountDto>? account;
    [ObservableProperty] private ChargeDto[] charges = [];
    [ObservableProperty] private ProductChoice[] products = [];
    [ObservableProperty] private ProductChoice? selectedProduct;
    [ObservableProperty] private string productSearch = "";
    [ObservableProperty] private ProductGroup[] productGroups = [];
    [ObservableProperty] private string productGroupsNote = "";
    [ObservableProperty] private string chosenProductText = "";
    public const int MaxProductGroups = 24;
    partial void OnProductSearchChanged(string value) { if (!rendering) BuildProductGroups(); }
    partial void OnSelectedProductChanged(ProductChoice? value) { if (!rendering) BuildProductGroups(); }
    // C3b: la MISMA regla que el selector «Añadir consumicion» del Comedor y que el comandero (Costina.Client/CatalogSearch, con la tabla de
    // casos comun): cada palabra escrita en el nombre, la referencia, la familia o la ruta de categorias, sin tildes ni mayusculas; con
    // texto, primero lo que empieza por lo escrito. Caja sigue buscando desde la primera letra y, sin texto, lo lista todo en el orden
    // del servidor. Solo en Caja, ademas, el nombre de su categoria (como antes de C3b): un producto colgado directamente de una seccion
    // («Bodega y bebidas», «Cocina») trae la ruta vacia y, sin esto, «bebidas» ya no lo encontraria. La regla comun no cambia.
    private static bool MatchesProduct(ProductChoice p, string[] words) => words.Length == 0
        || CatalogSearch.Matches(words, [.. DrinksPicker.SearchFields(p.Name, p.Reference, p.Family, p.CategoryPath, p.CategoryName), CatalogSearch.Fold(p.CategoryName)]);
    private void BuildProductGroups()
    {
        var text = ProductSearch.Trim();
        var words = CatalogSearch.Tokens(text);
        var groups = Products.Where(p => MatchesProduct(p, words)).GroupBy(p => p.Id).ToArray();
        if (words.Length > 0)
            groups = groups.Select(g => (Group: g, Rank: CatalogSearch.Rank(words, g.First().Name, g.First().Reference)))
                .OrderBy(e => e.Rank).ThenBy(e => e.Group, Comparer<IGrouping<string, ProductChoice>>.Create((a, b) => CatalogSearch.CompareByName(a.Key, a.First().Name, b.Key, b.First().Name)))
                .Select(e => e.Group).ToArray();
        ProductGroups = groups.Take(MaxProductGroups).Select(g =>
        {
            var first = g.First();
            string?[] detail = [first.Family ?? first.CategoryName, first.Reference];
            return new ProductGroup(g.Key, first.Name, string.Join(" · ", detail.Where(d => d is { Length: > 0 })),
                g.Select(p => new PresentationButton(p, $"{p.Presentation} · {Money.Format(p.PriceCents)}", ReferenceEquals(p, SelectedProduct) || SelectedProduct is { } s && s.Id == p.Id && s.PresentationId == p.PresentationId)).ToArray());
        }).ToArray();
        ProductGroupsNote = Products.Length == 0 ? "" : groups.Length == 0 ? $"Ningún producto coincide con «{text}»."
            : groups.Length > MaxProductGroups ? $"{groups.Length} coinciden; se muestran {MaxProductGroups}: afina la búsqueda." : "";
        ChosenProductText = SelectedProduct is { } chosen ? $"Elegido: {chosen.Name} · {chosen.Presentation} · {Money.Format(chosen.PriceCents)}" : "Elige una presentación de la lista.";
    }
    [RelayCommand] private void PickProduct(ProductChoice? choice) { if (choice is not null) SelectedProduct = choice; }
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private string amount = "";
    [ObservableProperty] private string method = "card";
    [ObservableProperty] private string note = "";
    [ObservableProperty] private string totals = "";
    [ObservableProperty] private string accountState = "";

    // ---- C2b: lista (buscador, resumen y vacio explicado) ----
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool accountsRead;   // la lista se ha leido con esta sesion (distingue "sin leer" de "ninguna abierta")
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private bool showListEmpty;
    [ObservableProperty] private string listEmptyHeading = "";
    [ObservableProperty] private string listEmptyText = "";
    [ObservableProperty] private bool showClearSearch;
    public bool ShowOpenTable => ShowListEmpty && AccountsRead && Accounts.Length == 0 && shell.Home.CanOpenTables;
    // Revision de C2b: sin cuentas (y sin busqueda escrita) el buscador se esconde, como las herramientas de lista de Catalogo y Oferta.
    public bool ShowListTools => Accounts.Length > 0 || Search.Length > 0;
    // Formas de pago como botones segmentados: el codigo elegido es Method (lo que viaja en el cuerpo); el texto, del vocabulario comun.
    public SegmentChoice[] Methods { get; } = PaymentChoices();
    private static SegmentChoice[] PaymentChoices()
    {
        var choices = new[] { "card", "cash", "other" }.Select(m => new SegmentChoice(m, Labels.PaymentMethod(m))).ToArray();
        SegmentChoice.Mark(choices, "card");   // la de partida, la misma que Method
        return choices;
    }

    // ---- C2b: ficha de la cuenta elegida (solo del contexto) ----
    [ObservableProperty] private bool hasContext;
    [ObservableProperty] private string emptyHeading = "";
    [ObservableProperty] private string emptyText = "";
    [ObservableProperty] private string detailContext = "";
    [ObservableProperty] private string detailTitle = "";
    [ObservableProperty] private string detailState = "";
    [ObservableProperty] private string detailTone = "other";
    [ObservableProperty] private string coverageText = "";
    [ObservableProperty] private string coverageTone = "other";
    [ObservableProperty] private string totalText = "";
    [ObservableProperty] private string paidText = "";
    [ObservableProperty] private string pendingText = "";
    [ObservableProperty] private string creditText = "";
    [ObservableProperty] private string refundedText = "";
    [ObservableProperty] private string closedText = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasLines))] private ChargeRow[] lineRows = [];
    public bool HasLines => LineRows.Length > 0;
    [ObservableProperty] private string linesTitle = "Consumos";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPayments))] private MoneyRow[] paymentRows = [];
    public bool HasPayments => PaymentRows.Length > 0;
    [ObservableProperty] private bool closeAnnounced;
    // Revision de C2b: con dinero de mas y sin cierre anunciado, el boton grande no es "Registrar pago de prueba" (tomaria aun mas) sino
    // un acceso a "Otras operaciones" para devolverlo; registrar otro pago queda al lado. Solo presentacion: ninguna orden ni CanExecute cambia.
    [ObservableProperty] private bool showPay = true;
    [ObservableProperty] private bool showPayAside;
    [ObservableProperty] private bool showRefundHint;

    public bool Visible { get; set; }

    private Versioned<AccountDto>? Context =>
        Account is not null && SelectedAccount?.ServiceId == Account.Data.ServiceId ? Account : null;
    private bool Allowed(string action) => Context?.Data.Actions?.Contains(action) == true;

    partial void OnMethodChanged(string value) => SegmentChoice.Mark(Methods, value);
    partial void OnAccountsChanged(AccountChoice[] value) => BuildRows();
    // Totales y estado salen siempre de la cuenta leida: nunca se escriben a mano.
    partial void OnAccountChanged(Versioned<AccountDto>? value)
    {
        if (value is not null)
        {
            var a = value.Data;
            Totals = $"Total {Money.Format(a.TotalCents)}  ·  Pagado {Money.Format(a.PaidCents)}  ·  Pendiente {Money.Format(a.BalanceCents)}  ·  Pagado de más {Money.Format(a.CreditCents)}  ·  Devuelto {Money.Format(a.RefundedCents)}";
            AccountState = $"Estado: {a.StateLabel} · {a.CoverageLabel}. {Labels.Plural(a.Payments.Length, "pago", "pagos")} y {Labels.Plural(a.Refunds?.Length ?? 0, "devolución", "devoluciones")} de prueba. Cerrar la cuenta no termina el servicio."
                + (a.State == "Closed" ? " CUENTA CERRADA: para admitir cambios, reábrela con motivo desde este puesto." : "");
        }
        Compose();
    }
    // C2: "Ver cuenta" desde la ficha de una mesa del comedor: la proxima lectura de Caja elige la cuenta de ese servicio. Es una peticion
    // de UN uso que se consume al empezar esa lectura (falle o no). Si Caja no lista esa cuenta (solo lista las abiertas: la mesa ya pago
    // y la cuenta esta cerrada, o no tiene cuenta) NO cae en la de otra mesa: no elige ninguna y lo dice. Solo fija el destino: el contexto
    // de las ordenes sigue siendo la cuenta leida. Null la olvida (la entrada del responsable cancelada).
    private (string ServiceId, string Table)? requested;
    internal void Request(string? serviceId, string table = "") => requested = serviceId is null ? null : (serviceId, table);
    // Para las ordenes pendientes: "cuenta de Mesa 1" (Context garantiza que la cuenta seleccionada es la leida).
    private string Where() => "cuenta de " + shell.Names.Table(SelectedAccount?.TableId);

    internal void Sync()
    {
        AddCommand.NotifyCanExecuteChanged(); PayCommand.NotifyCanExecuteChanged();
        CloseAccountCommand.NotifyCanExecuteChanged(); ReopenCommand.NotifyCanExecuteChanged(); RefundCommand.NotifyCanExecuteChanged();
        FillPendingCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(ShowOpenTable));
    }

    internal void Clear()
    {
        rendering = true;
        try
        {
            summaries = new(StringComparer.Ordinal); summariesComplete = false; AccountsRead = false; Search = "";
            Accounts = []; SelectedAccount = null; Products = []; SelectedProduct = null; ProductSearch = ""; BuildProductGroups();
            accountServiceId = null; requested = null; Amount = ""; amountAccount = null; Invalidate();
        }
        finally { rendering = false; }
        Compose();
    }

    private void Invalidate()
    {
        var wasRendering = rendering; rendering = true;
        try { Account = null; Charges = []; Totals = "Lectura pendiente: sin datos fiables de la cuenta seleccionada."; AccountState = ""; }
        finally { rendering = wasRendering; }
        Sync();
    }

    // Abrir la vista o actualizar: el listado, el catalogo de caja, la cuenta elegida y la de cada tarjeta. Elegir otra tarjeta
    // (LoadSelectionAsync) relee el listado, el catalogo y la elegida; las demas tarjetas conservan su ultima lectura.
    internal Task LoadAsync() => Load(selectionOnly: false);
    private Task LoadSelectionAsync() => Load(selectionOnly: true);
    private async Task Load(bool selectionOnly)
    {
        if (shell.Api is not { } api || !shell.IsMain) return;
        var attempt = ++generation;
        var request = requested; requested = null;   // un solo uso: una lectura fallida no la deja para una visita posterior
        // C2b: la cuenta pedida con "Ver cuenta" no queda escondida por una busqueda escrita antes.
        if (request is not null && Search.Length > 0) { var was = rendering; rendering = true; try { Search = ""; BuildRows(); } finally { rendering = was; } }
        var target = request?.ServiceId ?? accountServiceId;
        if (Account is not null && Account.Data.ServiceId != target) Invalidate();
        try
        {
            var choices = Ordered(await api.GetAsync<AccountChoice[]>("checkout/accounts"));
            var catalog = await api.GetAsync<ProductChoice[]>("checkout/catalog");
            // Sin destino pedido, la primera cuenta, como siempre (C2b: la primera que deja ver la busqueda); la cuenta pedida desde una
            // mesa que Caja no lista, ninguna.
            if (!choices.Any(a => a.ServiceId == target)) target = request is null ? choices.FirstOrDefault(c => Matches(c, Search.Trim()))?.ServiceId : null;
            var current = target is null ? null
                : await api.GetAsync<Versioned<AccountDto>>("checkout/services/" + ApiClient.Segment(target));
            var read = await ReadSummaries(api, choices, current?.Data, selectionOnly);
            if (attempt != generation) return;
            if (current is not null && current.Data.ServiceId != target)
                throw new InvalidOperationException("El servidor devolvió una cuenta distinta de la solicitada; lectura descartada.");
            accountServiceId = target;
            Render(choices, catalog, current, read);
            if (request is { } asked && target is null)
                shell.Status = $"La cuenta de {(asked.Table.Length > 0 ? asked.Table : "esa mesa")} no está abierta (ya se cerró o la mesa no tiene cuenta): Caja solo lista las cuentas abiertas. No se ha elegido ninguna.";
        }
        catch
        {
            if (attempt == generation) Invalidate();
            throw;
        }
    }

    // C2b: la lectura de cada cuenta de la lista, de cuatro en cuatro. La elegida es la que ya se ha leido; al elegir otra tarjeta las
    // demas conservan su lectura anterior. Una cuenta que no se puede leer (cerrada entre medias...) queda sin importes; un 401 sigue su
    // camino de siempre (vuelta al nivel Puesto).
    private async Task<Dictionary<string, AccountDto>> ReadSummaries(ApiClient api, AccountChoice[] choices, AccountDto? current, bool selectionOnly)
    {
        var result = new Dictionary<string, AccountDto>(StringComparer.Ordinal);
        if (current is not null) result[current.ServiceId] = current;
        if (selectionOnly)
            foreach (var choice in choices)
                if (!result.ContainsKey(choice.ServiceId) && summaries.TryGetValue(choice.ServiceId, out var known)) result[choice.ServiceId] = known;
        foreach (var chunk in choices.Where(c => !result.ContainsKey(c.ServiceId)).Chunk(4))
        {
            var reads = await Task.WhenAll(chunk.Select(async c => (c.ServiceId, Data: await TrySummary(api, c.ServiceId))));
            foreach (var (serviceId, data) in reads)
                if (data is not null && data.ServiceId == serviceId) result[serviceId] = data;   // nunca los importes de otra cuenta
        }
        return result;
    }
    private static async Task<AccountDto?> TrySummary(ApiClient api, string serviceId)
    {
        try { return (await api.GetAsync<Versioned<AccountDto>>("checkout/services/" + ApiClient.Segment(serviceId))).Data; }
        catch (ApiError error) when (error.Status != 401) { return null; }
    }

    private void Render(AccountChoice[] choices, ProductChoice[] catalog, Versioned<AccountDto>? current, Dictionary<string, AccountDto> read)
    {
        var previousProduct = SelectedProduct?.Id + "/" + SelectedProduct?.PresentationId;
        rendering = true;
        try
        {
            summaries = read; summariesComplete = choices.All(c => read.ContainsKey(c.ServiceId)); AccountsRead = true;
            Accounts = choices; SelectedAccount = choices.FirstOrDefault(a => a.ServiceId == accountServiceId);
            if (accountServiceId != amountAccount) { Amount = ""; Note = ""; amountAccount = accountServiceId; }
            Products = catalog; SelectedProduct = catalog.FirstOrDefault(p => p.Id + "/" + p.PresentationId == previousProduct) ?? catalog.FirstOrDefault();
            BuildProductGroups();
            Account = current; Charges = current?.Data.Charges ?? [];   // OnAccountChanged compone totales y estado
            if (current is null) { Totals = "Sin cuenta seleccionada"; AccountState = ""; }
        }
        finally { rendering = false; }
        Compose(); Sync();
    }

    partial void OnSelectedAccountChanged(AccountChoice? value)
    {
        Compose();
        if (rendering || shell.Busy || value is null) return;
        if (value.ServiceId != amountAccount) { Amount = ""; Note = ""; amountAccount = value.ServiceId; }
        accountServiceId = value.ServiceId;
        Sync();
        _ = shell.Run(LoadSelectionAsync);
    }

    // C2b: buscar por mesa o sala. Si la busqueda deja fuera la cuenta elegida, se deselecciona del todo (la ficha vuelve a decir que
    // elegir y ninguna orden queda activa sobre ella); si la sigue dejando ver, sigue elegida.
    partial void OnSearchChanged(string value)
    {
        if (rendering) return;
        var keep = SelectedAccount;
        rendering = true;
        try { BuildRows(); SelectedAccount = keep is not null && AccountItems.Any(r => r.Choice == keep) ? keep : null; }
        finally { rendering = false; }
        if (keep is not null && SelectedAccount is null) accountServiceId = null;
        Compose(); Sync();
    }
    [RelayCommand] private void ClearSearch() => Search = "";

    private bool Matches(AccountChoice choice, string text)
    {
        if (text.Length == 0) return true;
        bool Has(string value) => TextSearch.Contains(value, text);
        return Has(shell.Names.Table(choice.TableId)) || Has(ZoneOf(choice));
    }
    // Revision de C2b: el listado del servidor viene por identificador de servicio (un GUID: orden aleatorio en una instalacion real; la
    // muestra de los checks lo escondia). Las tarjetas y la cuenta elegida por defecto siguen el plano del Comedor (salas y mesas en su
    // orden) y, para una cuenta que el plano no lista, el nombre de su mesa. Solo ordena en la vista: el motor no cambia.
    private AccountChoice[] Ordered(AccountChoice[] choices)
    {
        if (choices.Length < 2) return choices;   // la misma instancia leida: una lista vacia nueva sigue avisando (ToArray devolveria la compartida)
        int Index(AccountChoice choice)
        {
            var i = 0;
            foreach (var tile in shell.Service.Tiles) { if (tile.Entry?.Service.Id == choice.ServiceId) return i; i++; }
            return int.MaxValue;
        }
        return choices.OrderBy(Index).ThenBy(c => shell.Names.Table(c.TableId), StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private TableTile? TileOf(AccountChoice choice) => shell.Service.Tiles.FirstOrDefault(t => t.Entry?.Service.Id == choice.ServiceId);
    private string ZoneOf(AccountChoice choice) =>
        TileOf(choice)?.ZoneName ?? shell.Service.Tables.FirstOrDefault(t => t.Id == choice.TableId)?.ZoneName ?? "";

    // Tarjetas de la lista (filtradas por la busqueda), resumen de la cabecera y vacio explicado.
    private void BuildRows()
    {
        var text = Search.Trim();
        AccountItems = Accounts.Where(a => Matches(a, text)).Select(Row).ToArray();
        var open = Accounts.Where(a => summaries.ContainsKey(a.ServiceId)).Sum(a => summaries[a.ServiceId].BalanceCents);
        Summary = !AccountsRead ? "" : Accounts.Length == 0 ? "Ninguna cuenta abierta ahora."
            : Labels.Plural(Accounts.Length, "cuenta abierta", "cuentas abiertas") + (summariesComplete ? " · Pendiente " + Money.Format(open) : "");
        ShowListEmpty = AccountItems.Length == 0;
        ShowClearSearch = Accounts.Length > 0 && AccountItems.Length == 0;
        (ListEmptyHeading, ListEmptyText) = !AccountsRead && Accounts.Length == 0 ? ("Sin leer todavía", "Las cuentas abiertas aparecerán aquí al leerlas del servidor.")
            : Accounts.Length == 0 ? ("No hay cuentas abiertas", "Cada mesa que se abre en Comedor y cocina tiene su cuenta: aparecerá aquí para añadir consumos y cobrarla.")
            : ("Ninguna cuenta coincide", $"Ninguna mesa ni sala con cuenta abierta coincide con «{text}».");
        OnPropertyChanged(nameof(ShowOpenTable)); OnPropertyChanged(nameof(ShowListTools));
    }
    private AccountRow Row(AccountChoice choice)
    {
        var tile = TileOf(choice);
        string?[] detail = [ZoneOf(choice), tile?.Entry is { } entry ? Labels.Plural(entry.Service.Pax, "persona", "personas") : null];
        var row = new AccountRow(choice, $"{shell.Names.Table(choice.TableId)} · {choice.StateLabel}")
        { Table = shell.Names.Table(choice.TableId), Detail = string.Join(" · ", detail.Where(d => !string.IsNullOrEmpty(d))) };
        return summaries.TryGetValue(choice.ServiceId, out var a)
            ? row with { TotalText = Money.Format(a.TotalCents), PendingText = "Pendiente " + Money.Format(a.BalanceCents), Coverage = a.CoverageLabel, CoverageTone = ToneOfCoverage(a.Coverage) }
            : row;
    }
    // Tono del chip de cobertura (solo color; la palabra va siempre): sin pagar, neutro (es lo normal durante el servicio); pago parcial,
    // azul; pagada, verde; pagado de mas, ambar (hay algo que devolver). Una cobertura desconocida sale tal cual y neutra.
    internal static string ToneOfCoverage(string coverage) => string.Concat(coverage.Where(char.IsLetter)).ToLowerInvariant() switch
    {
        "partiallypaid" => "info", "paid" => "ok", "credit" => "warn", _ => "other"
    };

    // La ficha se compone SOLO del contexto (la cuenta leida que coincide con la elegida): sin contexto dice que elegir y no muestra campos.
    private void Compose()
    {
        var context = Context;
        HasContext = context is not null;
        CloseAnnounced = Allowed("close");
        if (context is null)
        {
            (EmptyHeading, EmptyText) = SelectedAccount is { } chosen
                ? ("Cuenta de " + shell.Names.Table(chosen.TableId), "Sin datos fiables de esta cuenta todavía: se está leyendo o la lectura falló. Si no aparece, pulsa Actualizar.")
                : AccountsRead && Accounts.Length == 0 ? ("Nada que cobrar ahora", "Cuando haya una cuenta abierta, elígela en la lista para ver sus consumos y cobrarla.")
                : ("Elige una cuenta", "Pulsa una cuenta de la lista para ver sus consumos, sus pagos y cobrarla.");
            DetailContext = DetailTitle = DetailState = CoverageText = TotalText = PaidText = PendingText = CreditText = RefundedText = ClosedText = "";
            LineRows = []; PaymentRows = []; LinesTitle = "Consumos";
            ComposeActions();
            return;
        }
        var a = context.Data; var choice = SelectedAccount!;
        var tile = TileOf(choice);
        DetailContext = tile is null ? ZoneOf(choice) : tile.ZoneName + " · " + tile.Detail;
        DetailTitle = shell.Names.Table(choice.TableId);
        DetailState = a.StateLabel; DetailTone = a.State == "Open" ? "pending" : "other";
        CoverageText = a.CoverageLabel; CoverageTone = ToneOfCoverage(a.Coverage);
        TotalText = Money.Format(a.TotalCents); PaidText = Money.Format(a.PaidCents); PendingText = Money.Format(a.BalanceCents);
        CreditText = a.CreditCents > 0 ? $"Pagado de más: {Money.Format(a.CreditCents)}. Devuélvelo en «Otras operaciones», con su motivo." : "";
        RefundedText = a.RefundedCents > 0 ? "Devuelto " + Money.Format(a.RefundedCents) : "";
        ClosedText = a.State == "Closed" ? "Cuenta cerrada: no admite cambios. Para corregirla, reábrela con su motivo en «Otras operaciones»." : "";
        LineRows = a.Charges.Select(c => new ChargeRow(c.Description,
            c.Voided ? "Anulado" + (c.VoidReason is { Length: > 0 } reason ? ": " + reason : "") : c.Quantity > 1 ? Money.Format(c.UnitPriceCents) + " la unidad" : "",
            c.Quantity.ToString(CultureInfo.InvariantCulture), Money.Format(c.TotalCents), c.Voided)).ToArray();
        // Revision de C2b: el titulo cuenta las lineas que suman y, aparte, las anuladas (la tabla las lista todas).
        var voided = a.Charges.Count(c => c.Voided);
        LinesTitle = a.Charges.Length == 0 ? "Consumos" : "Consumos · " + Labels.Plural(a.Charges.Length - voided, "línea", "líneas") + (voided > 0 ? " · " + Labels.Plural(voided, "anulada", "anuladas") : "");
        PaymentRows = a.Payments.Select(p => new MoneyRow("Pago · " + p.MethodLabel, "a las " + Labels.Clock(p.At), Money.Format(p.AmountCents)))
            .Concat((a.Refunds ?? []).Select(r => new MoneyRow("Devolución · " + r.MethodLabel, $"a las {Labels.Clock(r.At)} · {r.Reason}", "− " + Money.Format(r.AmountCents))))
            .ToArray();
        ComposeActions();
    }
    private void ComposeActions()
    {
        var credit = CreditText.Length > 0;
        ShowPay = !CloseAnnounced && !credit; ShowPayAside = CloseAnnounced || credit; ShowRefundHint = credit && !CloseAnnounced;
    }

    private Versioned<AccountDto> Require() => Context
        ?? throw new InvalidOperationException("No hay una cuenta leída correctamente que coincida con la seleccionada.");

    private bool CanAdd() => shell.Writable && Allowed("add-product") && SelectedProduct is not null;
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task Add() => shell.Run(async () =>
    {
        var context = Require();
        if (!int.TryParse(Quantity, out var count) || count < 1) throw new ArgumentException("Introduce una cantidad positiva.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/add-product",
            new { expectedVersion = context.Version, productId = SelectedProduct!.Id, presentationId = SelectedProduct.PresentationId, quantity = count },   // E3: presentacion vendida
            "Añadir consumo del catálogo · " + Where());
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: consumo añadido.";
    });

    private bool CanPay() => shell.Writable && Allowed("payment");
    [RelayCommand(CanExecute = nameof(CanPay))]
    private Task Pay() => shell.Run(async () =>
    {
        var context = Require();
        var cents = Money.Parse(Amount);
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/payment",
            new { expectedVersion = context.Version, paymentId = Guid.NewGuid().ToString("N"), method = Method, amountCents = cents },
            "Registrar pago de prueba · " + Where());
        Amount = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: pago de prueba registrado.";
    });

    // C2b: "Todo lo pendiente" solo rellena el importe con el pendiente de la cuenta leida (sin separador de miles, como pide Money.Parse);
    // no envia nada: el pago sigue siendo "Registrar pago de prueba".
    private bool CanFillPending() => Context is { } context && context.Data.BalanceCents > 0 && CanPay();
    [RelayCommand(CanExecute = nameof(CanFillPending))]
    private void FillPending()
    {
        if (Context is { } context) Amount = (context.Data.BalanceCents / 100m).ToString("0.00", Spanish);
    }

    // Reapertura auditada (D3.6): solo desde el puesto principal y con motivo; el servidor la audita.
    private bool CanReopen() => shell.Writable && Allowed("reopen");
    [RelayCommand(CanExecute = nameof(CanReopen))]
    private Task Reopen() => shell.Run(async () =>
    {
        var context = Require();
        if (string.IsNullOrWhiteSpace(Note)) throw new ArgumentException("Indica el motivo de la reapertura.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/reopen",
            new { expectedVersion = context.Version, reason = Note.Trim() },
            "Reabrir cuenta con motivo · " + Where());
        Note = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta reabierta; vuelve a admitir cambios.";
    });

    // Devolucion: solo consume credito existente; el pago original nunca se borra.
    private bool CanRefund() => shell.Writable && Allowed("refund");
    [RelayCommand(CanExecute = nameof(CanRefund))]
    private Task Refund() => shell.Run(async () =>
    {
        var context = Require();
        var cents = Money.Parse(Amount);
        if (string.IsNullOrWhiteSpace(Note)) throw new ArgumentException("Indica el motivo de la devolución.");
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/refund",
            new { expectedVersion = context.Version, refundId = Guid.NewGuid().ToString("N"), method = Method, amountCents = cents, reason = Note.Trim() },
            "Devolver lo pagado de más · " + Where());
        Amount = ""; Note = "";
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: devolución registrada.";
    });

    private bool CanCloseAccount() => shell.Writable && Allowed("close");
    [RelayCommand(CanExecute = nameof(CanCloseAccount))]
    private Task CloseAccount() => shell.Run(async () =>
    {
        var context = Require();
        await shell.Api!.SendAsync("checkout/services/" + ApiClient.Segment(context.Data.ServiceId) + "/commands/close",
            new { expectedVersion = context.Version }, "Cerrar cuenta saldada · " + Where());
        await shell.RefreshAll();
        shell.Status = "Operación confirmada por el servidor: cuenta cerrada.";
    });
}
