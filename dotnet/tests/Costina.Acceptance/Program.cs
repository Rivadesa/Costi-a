using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Costina.Core.Domain;
using Costina.Modules.Dining.Domain;
using Costina.Migration;

namespace Costina.Acceptance;

internal static class Program
{
    private static readonly BusinessScope Scope = new("tenant-1", "company-1", "location-1");
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-09-15T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly CommandStamp Stamp = new(Scope, "operator-1", Time);
    private static readonly List<(string Name, Action Test)> Tests = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true,
        Converters = { new JsonStringEnumConverter() } };

    private static int Main(string[] args)
    {
        Register();
        var results = new List<Result>();
        foreach (var (name, test) in Tests)
        {
            try { test(); results.Add(new(name, true, null)); Console.WriteLine($"PASS {name}"); }
            catch (Exception error) { results.Add(new(name, false, error.ToString())); Console.WriteLine($"FAIL {name}: {error.Message}"); }
        }
        var output = args.Length == 1 ? args[0] : "artifacts/d0";
        Directory.CreateDirectory(output);
        var failed = results.Count(r => !r.Passed);
        File.WriteAllText(Path.Combine(output, "acceptance.json"), JsonSerializer.Serialize(new {
            phase = "D0-native-domain", executedAtUtc = DateTimeOffset.UtcNow,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            total = results.Count, failed, results }, Json));
        var suite = new XElement("testsuite", new XAttribute("name", "Costina.D0"),
            new XAttribute("tests", results.Count), new XAttribute("failures", failed),
            results.Select(r => new XElement("testcase", new XAttribute("name", r.Name),
                r.Passed ? null : new XElement("failure", r.Error))));
        new XDocument(suite).Save(Path.Combine(output, "acceptance.xml"));
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed. Reports: {Path.GetFullPath(output)}");
        return failed == 0 ? 0 : 1;
    }

    private sealed record Result(string Name, bool Passed, string? Error);
    private sealed record LegacyCase(string Name, string Status, bool Started, bool Closed,
        string[] Courses, long Total, long Paid, string? ExpectedDining, string ExpectedCoverage);
    private static void Test(string name, Action test) => Tests.Add((name, test));
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}.");
    }
    private static void True(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Rule(string code, Action action)
    {
        try { action(); }
        catch (RuleViolation e) { Equal(code, e.Code); return; }
        throw new Exception($"Expected RuleViolation {code}.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
    private static DiningService Service() => new("service-1", Scope, "table-1", 2, [
        new("course-1", "Fish", [new("fish", "Fish for PAX 2", "fish", GuestPosition: 2), new("sauce", "Sauce", "hot")]),
        new("course-2", "Dessert", [new("dessert", "Dessert", "pastry", Quantity: 2)])]);
    private static SettlementAccount Account()
    {
        var account = new SettlementAccount("account-1", Scope, "service-1");
        account.AddCharge("menus", "Menu x2", 2, 15000, Stamp);
        return account;
    }
    private static TableOccupancy Occupancy() => new("occupancy-1", Scope, "table-1", "service-1");
    private static void ServeCurrent(DiningService service, string courseId)
    {
        var course = service.View().Courses.Single(c => c.Id == courseId);
        foreach (var p in course.Preparations)
        {
            if (p.State == PreparationState.Fired) service.StartPreparation(courseId, p.Id, Stamp);
            if (p.State is PreparationState.Fired or PreparationState.Preparing) service.ReadyPreparation(courseId, p.Id, Stamp);
        }
        service.ValidateReady(courseId, Stamp);
        service.Serve(courseId, Stamp);
    }
    private static void Finish(DiningService service)
    {
        service.Start(Stamp);
        ServeCurrent(service, service.FireNext(Stamp));
        ServeCurrent(service, service.FireNext(Stamp));
        service.Complete(Stamp);
    }
    private static void Register()
    {
        // E2: organizacion del nucleo. Codigos estables sin espacios ni acentos; aforo acotado; la estacion de pase es intocable.
        Test("organization accepts stable codes and printable names", () => {
            var table = Organization.Table(" M1 ", " Mesa 1 ", 12, "sala", null);
            Equal(("M1", "Mesa 1", 12, "sala", 0, true), (table.Id, table.Name, table.Capacity, table.ZoneId, table.Sort, table.Active));
            Equal(StationKind.Room, Organization.Station("sala-1", "Sala 1", StationKind.Room, 3).Kind);
        });
        Test("organization rejects bad codes, names, capacity and sort", () => {
            Rule("invalid_code", () => Organization.Zone("con espacio", "x", 0));
            Rule("invalid_code", () => Organization.Zone("ñ", "x", 0));
            Rule("invalid_code", () => Organization.Zone(new string('a', 33), "x", 0));
            Throws<ArgumentException>(() => Organization.Zone("", "x", 0));
            Rule("invalid_name", () => Organization.Zone("z", new string('a', 61), 0));
            Rule("invalid_name", () => Organization.Zone("z", "con\tcontrol", 0));
            Rule("invalid_capacity", () => Organization.Table("M1", "x", 0, "sala", 0));
            Rule("invalid_capacity", () => Organization.Table("M1", "x", 61, "sala", 0));
            Rule("invalid_sort", () => Organization.Zone("z", "x", 10000));
        });
        // E3: catalogo y tarifas del nucleo. Codigos como en E2; precio, tipo impositivo y color acotados; sin ciclos; general reservada.
        Test("catalog accepts products, presentations, taxes, tariffs and prices", () => {
            var product = Catalog.Product(" wine ", " Vino de ensayo ", "vinos", "iva-21", " 3754 ", null);
            Equal(("wine", "Vino de ensayo", "vinos", "iva-21", "3754", 0, true), (product.Id, product.Name, product.CategoryId, product.TaxId, product.Reference, product.Sort, product.Active));
            Equal(null, Catalog.Product("water", "Agua", "", "iva-10", " ", 1).CategoryId);
            Equal("Copa", Catalog.Presentation("wine", "glass", "Copa", 0).Name);
            Equal(10.5m, Catalog.Tax("iva-x", "IVA especial", 10.5m).Rate);
            Equal(("#1F4D3A", "bebidas"), (Catalog.Category("vinos", "Vinos", "bebidas", "#1f4d3a", 1).Color, Catalog.Category("vinos", "Vinos", "bebidas", null, 1).ParentId));
            var price = Catalog.Price("general", "wine", "glass", new DateOnly(2026, 9, 23), 950);
            Equal(("general", 950L), (price.TariffId, price.PriceCents));
            Equal(new DateOnly(2026, 10, 1), Catalog.ValidFrom(" 2026-10-01 ", new DateOnly(2026, 9, 23)));
            Equal(new DateOnly(2026, 9, 23), Catalog.ValidFrom(null, new DateOnly(2026, 9, 23)));
        });
        // E3.3: secciones del catalogo. Por la raiz si es una seccion; los productos-menu, ninguna; si no, por la estacion.
        Test("catalog sections come from the root category or, without one, from the station", () => {
            var parents = new Dictionary<string, string?> { ["_cocina"] = null, ["_bodega"] = null, ["vinos"] = "_bodega", ["tintos"] = "vinos", ["rioja"] = "tintos", ["carnes"] = "_cocina", ["bebidas"] = null, ["menus"] = null };
            string? ParentOf(string id) => parents.GetValueOrDefault(id);
            Equal("bodega", Catalog.SectionOf("rioja", ParentOf, "kitchen"));
            Equal("cocina", Catalog.SectionOf("carnes", ParentOf, null));
            Equal("cocina", Catalog.SectionOf("bebidas", ParentOf, "Kitchen"));
            Equal("cocina", Catalog.SectionOf(null, ParentOf, "pass"));
            Equal("bodega", Catalog.SectionOf("bebidas", ParentOf, null));
            Equal("bodega", Catalog.SectionOf("bebidas", ParentOf, "bar"));
            Equal(null, Catalog.SectionOf("menus", ParentOf, null));
            Equal("_bodega", Catalog.RootOf("rioja", ParentOf));
            Equal("Tintos", Catalog.CategoryPath("rioja", ParentOf, id => id switch { "vinos" => "Vinos", "tintos" => "Tintos", "rioja" => "Rioja", _ => null })[1]);
            Equal(3, Catalog.CategoryPath("rioja", ParentOf, id => id).Length);   // sin la seccion
            Equal((true, true, false), (Catalog.IsSection("_cocina"), Catalog.IsGlass("glass"), Catalog.IsGlass("bottle")));
            Equal("cocina", Catalog.Slug("_cocina"));   // un slug nunca produce el guion bajo de una seccion
        });
        // E3.3.1: LA copa de un vino entre sus presentaciones ('copa' o la 'glass' del laboratorio): la activa primero, entre varias
        // 'copa', nunca una botella ni otro codigo; null si no tiene ninguna (glass-sale-set crea entonces 'copa').
        Test("the glass of a wine is the active glass presentation, copa first", () => {
            PresentationDefinition P(string id, bool active) => new("wine", id, id, 0, active);
            Equal("glass", Catalog.GlassOf([P("copa", false), P("glass", true), P("bottle", true)])?.Id);
            Equal("copa", Catalog.GlassOf([P("glass", true), P("copa", true)])?.Id);
            Equal("copa", Catalog.GlassOf([P("glass", false), P("copa", false)])?.Id);
            Equal("glass", Catalog.GlassOf([P("glass", false), P("bottle", true)])?.Id);
            Equal(null, Catalog.GlassOf([P("bottle", true), P("copa-15-cl", true)])?.Id);
        });
        Test("catalog rejects bad prices, rates, colors, references and dates", () => {
            Rule("invalid_price", () => Catalog.Price("general", "wine", "glass", new DateOnly(2026, 1, 1), -1));
            Rule("invalid_price", () => Catalog.Price("general", "wine", "glass", new DateOnly(2026, 1, 1), Catalog.MaxPriceCents + 1));
            Rule("invalid_price", () => Catalog.Price("general", "wine", "glass", new DateOnly(2026, 1, 1), null));
            Rule("invalid_rate", () => Catalog.Tax("t", "x", 100.5m));
            Rule("invalid_rate", () => Catalog.Tax("t", "x", 10.123m));
            Rule("invalid_rate", () => Catalog.Tax("t", "x", null));
            Rule("invalid_color", () => Catalog.Category("c", "x", null, "rojo", 0));
            Rule("invalid_color", () => Catalog.Category("c", "x", null, "#12345", 0));
            Rule("invalid_parent", () => Catalog.Category("c", "x", "c", null, 0));
            Rule("invalid_reference", () => Catalog.Product("p", "x", null, "iva-10", new string('9', 65), 0));
            Rule("invalid_code", () => Catalog.Product("con espacio", "x", null, "iva-10", null, 0));
            Rule("invalid_code", () => Catalog.Presentation("p", "ñ", "x", 0));
            Rule("invalid_date", () => Catalog.ValidFrom("01/10/2026", new DateOnly(2026, 9, 23)));
        });
        Test("category trees have no cycles and at most four levels", () => {
            var parents = new Dictionary<string, string?> { ["a"] = null, ["b"] = "a", ["c"] = "b", ["d"] = "c", ["loop1"] = "loop2", ["loop2"] = "loop1" };
            string? ParentOf(string id) => parents.GetValueOrDefault(id);
            Equal(4, Catalog.Depth("d", "c", ParentOf));
            Rule("category_depth", () => Catalog.Depth("e", "d", ParentOf));
            Rule("category_cycle", () => Catalog.Depth("a", "d", ParentOf));      // a pasaria a colgar de su propio descendiente
            Rule("category_cycle", () => Catalog.Depth("loop1", "loop2", ParentOf));
        });
        Test("the general tariff is reserved and the price in force is the latest not after the date", () => {
            Rule("reserved_tariff", () => Catalog.Tariff(Catalog.GeneralTariff, "General", 0, active: false));
            True(Catalog.Tariff(Catalog.GeneralTariff, "General", 0).Active);
            PriceDefinition P(string from, long cents) => new("general", "wine", "glass", DateOnly.Parse(from), cents);
            var prices = new[] { P("2026-01-01", 900), P("2026-10-01", 1000), P("2026-09-01", 950) };
            Equal(950L, Catalog.Current(prices, new DateOnly(2026, 9, 23))!.PriceCents);
            Equal(1000L, Catalog.Current(prices, new DateOnly(2026, 10, 1))!.PriceCents);
            Equal(null, Catalog.Current(prices, new DateOnly(2025, 12, 31)));
        });
        Test("a charge freezes its product, presentation and tariff and never rereads the catalog", () => {
            var account = Account();
            account.AddCharge("c1", "Vino de ensayo · Copa", 2, 950, Stamp, "wine", "glass", "general");
            var line = account.View().Charges.Single(c => c.Id == "c1");
            Equal(("wine", "glass", "general", 1900L), (line.ProductId, line.PresentationId, line.TariffId, line.TotalCents));
            var restored = SettlementAccount.Restore(account.Snapshot()).View().Charges.Single(c => c.Id == "c1");
            Equal("glass", restored.PresentationId);
            Equal("limpio", Catalog.Slug("  Limpio "));
            Equal("el-rapolao-2023", Catalog.Slug("El Rapolao 2023"));
            Equal("anada-cinco-x", Catalog.Slug("Añada · cinco (x)"));
        });
        // C3b: la linea de la cuenta guarda tambien el comensal, su origen y la hora (campos aditivos). Una cuenta guardada antes del
        // corte, sin esos campos, se restaura igual; un comensal fuera de rango o un origen desconocido no se apuntan.
        Test("a charge keeps its guest, origin and time, and an older charge restores without them", () => {
            var account = Account();
            account.AddCharge("c1", "Vino de ensayo · Copa", 2, 950, Stamp, "wine", "glass", "general", guestPosition: 2, origin: ChargeOrigin.Consumption);
            var line = account.View().Charges.Single(c => c.Id == "c1");
            Equal<(int?, string?, DateTimeOffset?)>((2, "consumption", Time), (line.GuestPosition, line.Origin, line.At));
            Equal(line, SettlementAccount.Restore(account.Snapshot()).View().Charges.Single(c => c.Id == "c1"));
            var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            var stored = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(account.Snapshot(), wire))!;
            foreach (var charge in stored["charges"]!.AsArray()) foreach (var added in new[] { "guestPosition", "origin", "at" }) charge!.AsObject().Remove(added);
            var older = SettlementAccount.Restore(stored.Deserialize<AccountSnapshot>(wire)!).View().Charges.Single(c => c.Id == "c1");
            Equal(((int?)null, (string?)null, (DateTimeOffset?)null, 1900L), (older.GuestPosition, older.Origin, older.At, older.TotalCents));
            Rule("invalid_guest", () => account.AddCharge("c2", "Agua", 1, 400, Stamp, guestPosition: 0, origin: ChargeOrigin.Consumption));
            Rule("invalid_origin", () => account.AddCharge("c3", "Agua", 1, 400, Stamp, origin: "regalo"));
            Equal(2, account.View().Charges.Count);
            Rule("invalid_snapshot", () => SettlementAccount.Restore(account.Snapshot() with { Charges = [line with { GuestPosition = 0 }] }));
        });
        // C3b (diferido de D6.7): el identificador de una linea sale de la ORDEN (servicio, clave e indice). La idempotencia del servidor es
        // por actor: la misma clave reenviada por otro actor deriva los mismos identificadores y la cuenta no admite el segundo cargo.
        Test("a line id derived from its order is stable and the account never takes it twice", () => {
            var id = SettlementAccount.DerivedLineId("service-1", "key-1", 0);
            Equal(id, SettlementAccount.DerivedLineId("service-1", "key-1", 0));
            True(System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9a-f]{32}$"), id);
            foreach (var (service, key, index) in new[] { ("service-2", "key-1", 0), ("service-1", "key-2", 0), ("service-1", "key-1", 1), ("service-1", "key-10", 0) })
                True(SettlementAccount.DerivedLineId(service, key, index) != id, $"{service}/{key}/{index}");
            var account = Account(); var count = account.PendingEvents.Count;
            account.AddCharge(id, "Agua mineral · Botella", 1, 400, Stamp, "water", "bottle", "general", origin: ChargeOrigin.Consumption);
            Rule("duplicate_charge", () => account.AddCharge(id, "Agua mineral · Botella", 1, 400, new CommandStamp(Scope, "device:otro-nombre", Time),
                "water", "bottle", "general", origin: ChargeOrigin.Consumption));
            Equal((1, count + 1, 30400L), (account.View().Charges.Count(c => c.Id == id), account.PendingEvents.Count, account.TotalCents));
        });
        Test("the consumptions notice is operational, names the service and carries no data", () => {
            var notice = SettlementAccount.ConsumptionsChanged("service-1", Stamp);
            Equal(("service.consumptions_changed", "service-1", "operator-1", Time, 0), (notice.Type, notice.AggregateId, notice.ActorId, notice.At, notice.Data.Count));
            True(!notice.Type.StartsWith("account.", StringComparison.Ordinal) && !notice.Type.StartsWith("payment.", StringComparison.Ordinal));
        });
        // E3.2: imagenes de los articulos. Solo JPEG, PNG y WebP reconocidos por la firma de sus bytes (no por la extension ni por lo
        // que se declare), hasta 2 MB; el tipo declarado tiene que coincidir; la huella es el SHA-256 del contenido.
        Test("product images are recognised by their signature and carry their own fingerprint", () => {
            byte[] Bytes(byte[] head, int size) { var b = new byte[size]; head.CopyTo(b, 0); for (var i = head.Length; i < size; i++) b[i] = (byte)(i * 7); return b; }
            var jpeg = Bytes([0xFF, 0xD8, 0xFF, 0xE0], 64);
            var png = Bytes([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 64);
            var webp = Bytes([.. "RIFF"u8, 56, 0, 0, 0, .. "WEBP"u8], 64);
            Equal(("image/jpeg", "image/png", "image/webp"), (Catalog.ImageType(jpeg), Catalog.ImageType(png), Catalog.ImageType(webp)));
            var image = Catalog.Image(" wine ", " IMAGE/PNG ", png);
            Equal(("wine", "image/png", 64), (image.ProductId, image.ContentType, image.Bytes));
            Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(png)), image.Sha256);
            Equal("image/webp", Catalog.Image("wine", "image/webp", webp).ContentType);
            Equal(Catalog.MaxImageBytes, Catalog.Image("wine", "image/jpeg", Bytes([0xFF, 0xD8, 0xFF], Catalog.MaxImageBytes)).Bytes);   // justo 2 MB: vale
        });
        Test("product images reject SVG, disguised HTML, other formats, a wrong declared type, empty and over 2 MB", () => {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
            Rule("image_invalid", () => Catalog.Image("wine", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray()));
            Rule("image_invalid", () => Catalog.Image("wine", "image/png", "<html><body>no soy un png</body></html>"u8.ToArray()));
            Rule("image_invalid", () => Catalog.Image("wine", "image/gif", "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray()));
            Rule("image_invalid", () => Catalog.Image("wine", "image/webp", "RIFF\u0010\u0000\u0000\u0000WAVEfmt "u8.ToArray()));   // RIFF que no es WebP
            Rule("image_invalid", () => Catalog.Image("wine", "image/jpeg", png));      // los bytes son PNG
            Rule("image_invalid", () => Catalog.Image("wine", null, png));
            Rule("image_invalid", () => Catalog.Image("wine", "image/png", []));
            Rule("image_invalid", () => Catalog.Image("wine", "image/png", null));
            Rule("image_invalid", () => Catalog.Image("wine", "image/jpeg", [0xFF, 0xD8]));   // firma incompleta
            var big = new byte[Catalog.MaxImageBytes + 1]; png.CopyTo(big, 0);
            Rule("image_too_large", () => Catalog.Image("wine", "image/png", big));
            Rule("image_too_large", () => Catalog.ImageSize(Catalog.MaxImageBytes + 1L));
            Rule("invalid_code", () => Catalog.Image("con espacio", "image/png", png));
        });
        // E4a: menu cerrado = pase con eleccion por comensal antes de disparar; ofertas vigentes por fecha, dia y servicio.
        Test("a set-menu course needs a choice per guest before it fires and closes once fired", () => {
            var service = new DiningService("s", Scope, "M1", 2, [new("primeros", "Primeros", [], ChoiceRequired: true), new("postre", "Postre", [new("tarta", "Tarta", "pastry")])], "LAB-DAILY");
            Equal("LAB-DAILY", service.OfferId);
            True(service.View(true).Courses[0].Actions!.Contains("choose") && !service.View(true).Courses[1].Actions!.Contains("choose"));
            service.Start(Stamp);
            Rule("choice_missing", () => service.FireNext(Stamp));
            service.Choose("primeros", 1, new("sopa", "Sopa", "hot"), Stamp);
            Rule("choice_missing", () => service.FireNext(Stamp));
            Rule("invalid_guest", () => service.Choose("primeros", 3, new("sopa", "Sopa", "hot"), Stamp));
            Rule("no_choice", () => service.Choose("postre", 1, new("sopa", "Sopa", "hot"), Stamp));
            service.Choose("primeros", 1, new("ensalada", "Ensalada", "cold"), Stamp);   // cambia de idea: sustituye
            service.Choose("primeros", 2, new("sopa", "Sopa", "hot"), Stamp);
            var chosen = service.View().Courses[0].Preparations.Select(p => (p.Id, p.GuestPosition, p.StationId)).OrderBy(x => x.Id).ToArray();
            Equal("ensalada-1,cold,1;sopa-2,hot,2", string.Join(";", chosen.Select(x => $"{x.Id},{x.StationId},{x.GuestPosition}")));
            Equal("primeros", service.FireNext(Stamp));
            Rule("course_not_pending", () => service.Choose("primeros", 1, new("sopa", "Sopa", "hot"), Stamp));
            var restored = DiningService.Restore(service.Snapshot());
            True(restored.View().Courses[0].ChoiceRequired && restored.OfferId == "LAB-DAILY" && restored.View().Courses[0].Preparations.Count == 2);
        });
        // E4b: grupo de carta libre: vacio y opcional; los platos se anaden mientras esta pendiente; vacio no se dispara, se omite.
        Test("an a la carte group takes dishes while pending, never fires empty and can be skipped", () => {
            var service = new DiningService("s", Scope, "M1", 2, [new("entrantes", "Entrantes", [], Optional: true), new("postres", "Postres", [], Optional: true)], "LAB-CARTA");
            True(service.View(true).Courses[0].Actions!.Contains("add-dish") && !service.View(true).Courses[0].Actions!.Contains("choose"));
            service.Start(Stamp);
            Rule("course_empty", () => service.FireNext(Stamp));
            service.AddDish("entrantes", new("croquetas-unit-1", "Croquetas", "cold", 2, 1), Stamp);
            Rule("duplicate_preparation", () => service.AddDish("entrantes", new("croquetas-unit-1", "Croquetas", "cold"), Stamp));
            Rule("invalid_guest", () => service.AddDish("entrantes", new("x", "x", "cold", 1, 5), Stamp));
            Equal("entrantes", service.FireNext(Stamp));
            Rule("course_not_pending", () => service.AddDish("entrantes", new("croquetas-unit-2", "Croquetas", "cold"), Stamp));
            ServeCurrent(service, "entrantes");
            Rule("course_empty", () => service.FireNext(Stamp));
            service.Skip("postres", "no quieren postre", Stamp);
            service.Complete(Stamp);
            var restored = DiningService.Restore(service.Snapshot());
            True(restored.View().Courses[0].Optional && restored.View().Courses[0].Preparations.Single().Quantity == 2);
            Rule("no_dishes", () => Service().AddDish("course-1", new("x", "x", "cold"), Stamp));
            Rule("product_not_allowed", () => Offers.Offer("C", "Carta", OfferKind.ALaCarte, "menu-c", OfferService.Any, null, null, null, 0));
            Equal(null, Offers.Offer("C", "Carta", OfferKind.ALaCarte, null, OfferService.Any, null, null, null, 0).ProductId);
        });
        Test("offers are available by dates, weekdays and lunch or dinner service", () => {
            var always = Offers.Offer("T", "Degustación", OfferKind.Tasting, "menu-t", OfferService.Any, null, null, null, 0);
            True(Offers.Available(always, new DateTime(2026, 9, 23, 13, 0, 0)) && Offers.Available(always, new DateTime(2026, 9, 27, 21, 0, 0)));
            var weekendDinner = Offers.Offer("W", "Cena", OfferKind.SetMenu, "menu-w", OfferService.Dinner, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 96, 0);
            True(Offers.Available(weekendDinner, new DateTime(2026, 10, 3, 21, 0, 0)));     // sabado noche de octubre
            True(!Offers.Available(weekendDinner, new DateTime(2026, 10, 3, 13, 0, 0)));    // mediodia
            True(!Offers.Available(weekendDinner, new DateTime(2026, 10, 5, 21, 0, 0)));    // lunes
            True(!Offers.Available(weekendDinner, new DateTime(2026, 11, 7, 21, 0, 0)));    // fuera de fechas
            True(!Offers.Available(weekendDinner with { Active = false }, new DateTime(2026, 10, 3, 21, 0, 0)));
            Rule("product_required", () => Offers.Offer("X", "x", OfferKind.Tasting, null, OfferService.Any, null, null, null, 0));
            Rule("invalid_weekdays", () => Offers.Offer("X", "x", OfferKind.Tasting, "p", OfferService.Any, null, null, 0, 0));
            Rule("invalid_validity", () => Offers.Offer("X", "x", OfferKind.Tasting, "p", OfferService.Any, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), null, 0));
            Equal(1, Offers.WeekdayBit(DayOfWeek.Monday)); Equal(64, Offers.WeekdayBit(DayOfWeek.Sunday));
        });
        Test("the pass station is reserved: always kind pass and never deactivated", () => {
            Rule("reserved_station", () => Organization.Station(Organization.PassStation, "Pase", StationKind.Kitchen, 0));
            Rule("reserved_station", () => Organization.Station(Organization.PassStation, "Pase", StationKind.Pass, 0, active: false));
            True(Organization.Station(Organization.PassStation, "Pase", StationKind.Pass, 0).Active);
        });
        Test("full prepayment before arrival does not start or finish dining", () => {
            var service = Service(); var account = Account(); account.RecordPayment("p", "card", 30000, Stamp);
            Equal(DiningState.Open, service.State); service.Start(Stamp); Equal("course-1", service.FireNext(Stamp));
            Equal(PaymentCoverage.Paid, account.Coverage);
        });
        Test("partial payment during preparation permits remaining kitchen work and next course", () => {
            var service = Service(); var account = Account(); service.Start(Stamp); service.FireNext(Stamp);
            service.StartPreparation("course-1", "fish", Stamp); account.RecordPayment("p", "card", 10000, Stamp);
            Equal(DiningState.InService, service.State); ServeCurrent(service, "course-1");
            Equal("course-2", service.FireNext(Stamp)); Equal(20000L, account.BalanceCents);
        });
        Test("full payment during preparation does not remove the active course", () => {
            var service = Service(); var account = Account(); service.Start(Stamp); service.FireNext(Stamp);
            var before = JsonSerializer.Serialize(service.View(), Json); account.RecordPayment("p", "cash", 30000, Stamp);
            Equal(before, JsonSerializer.Serialize(service.View(), Json)); ServeCurrent(service, "course-1");
            Equal("course-2", service.FireNext(Stamp));
        });
        Test("pause and resume preserve account amounts", () => {
            var service = Service(); var account = Account(); account.RecordPayment("p", "card", 10000, Stamp);
            var before = JsonSerializer.Serialize(account.View(), Json); service.Start(Stamp);
            service.Pause("Guest requested a break", Stamp); service.Resume(Stamp);
            Equal(before, JsonSerializer.Serialize(account.View(), Json));
        });
        Test("payment during pause does not silently resume dining", () => {
            var service = Service(); var account = Account(); service.Start(Stamp); service.Pause("Guest", Stamp);
            account.RecordPayment("p", "card", 30000, Stamp); Equal(DiningState.Paused, service.State);
            Rule("service_not_running", () => service.FireNext(Stamp));
        });
        Test("pause permits acknowledgement of an already fired course", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp); service.Pause("Guest", Stamp);
            ServeCurrent(service, "course-1"); Equal(DiningState.Paused, service.State);
            service.Resume(Stamp); Equal("course-2", service.FireNext(Stamp));
        });
        Test("extra after payment changes balance but not pacing", () => {
            var service = Service(); var account = Account(); service.Start(Stamp); service.FireNext(Stamp);
            account.RecordPayment("p", "card", 30000, Stamp); account.AddCharge("water", "Water bottle", 1, 400, Stamp);
            Equal(400L, account.BalanceCents); Equal(PaymentCoverage.PartiallyPaid, account.Coverage);
            Equal(DiningState.InService, service.State);
        });
        Test("last served course does not automatically complete, pay or release", () => {
            var service = Service(); var account = Account(); var occupancy = Occupancy(); service.Start(Stamp);
            ServeCurrent(service, service.FireNext(Stamp)); ServeCurrent(service, service.FireNext(Stamp));
            Equal(DiningState.InService, service.State); Equal(0L, account.PaidCents);
            Equal(OccupancyState.Occupied, occupancy.State);
        });
        Test("completion does not require payment or release occupancy", () => {
            var service = Service(); var account = Account(); var occupancy = Occupancy(); Finish(service);
            Equal(DiningState.Completed, service.State); Equal(OccupancyState.Occupied, occupancy.State);
            Equal(PaymentCoverage.Unpaid, account.Coverage); Equal(AccountState.Open, account.State);
        });
        Test("explicit table release leaves unpaid account intact", () => {
            var service = Service(); var account = Account(); var occupancy = Occupancy(); Finish(service);
            occupancy.Release(service, "Diners have left", Stamp);
            Equal(OccupancyState.Released, occupancy.State); Equal(30000L, account.BalanceCents);
            Equal(1, occupancy.PendingEvents.Count); Equal(0, account.View().Payments.Count);
        });
        Test("paying after table release remains possible", () => {
            var service = Service(); var account = Account(); var occupancy = Occupancy(); Finish(service);
            occupancy.Release(service, "Diners have left", Stamp); account.RecordPayment("p", "card", 30000, Stamp);
            account.Close(Stamp); Equal(AccountState.Closed, account.State); Equal(DiningState.Completed, service.State);
        });
        Test("closing a balanced account does not finish an active service", () => {
            var service = Service(); var account = Account(); service.Start(Stamp); service.FireNext(Stamp);
            account.RecordPayment("p", "card", 30000, Stamp); account.Close(Stamp);
            ServeCurrent(service, "course-1"); Equal("course-2", service.FireNext(Stamp));
        });
        Test("an active service cannot release table", () => {
            var service = Service(); var occupancy = Occupancy(); service.Start(Stamp); service.FireNext(Stamp);
            Rule("service_not_finished", () => occupancy.Release(service, "Wrong action", Stamp));
            Equal(0, occupancy.PendingEvents.Count); Equal(OccupancyState.Occupied, occupancy.State);
        });
        Test("release requires correct service identity and scope", () => {
            var service = Service(); Finish(service);
            var wrong = new TableOccupancy("o", Scope, "table-2", "service-1");
            Rule("occupancy_mismatch", () => wrong.Release(service, "Wrong table", Stamp));
        });
        Test("release is not duplicated by reapplying the domain action", () => {
            var service = Service(); Finish(service); var occupancy = Occupancy(); occupancy.Release(service, "Left", Stamp);
            Rule("table_already_released", () => occupancy.Release(service, "Left", Stamp)); Equal(1, occupancy.PendingEvents.Count);
        });
        Test("chef cannot validate before all mandatory stations finish", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.ReadyPreparation("course-1", "fish", Stamp);
            Rule("mandatory_preparation_pending", () => service.ValidateReady("course-1", Stamp));
            service.ReadyPreparation("course-1", "sauce", Stamp); service.ValidateReady("course-1", Stamp);
            Equal(CourseState.Ready, service.View().Courses[0].State);
        });
        Test("next course cannot fire before active course is served", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            Rule("active_course", () => service.FireNext(Stamp));
        });
        Test("completion requires unsent courses to be explicitly skipped", () => {
            var service = Service(); service.Start(Stamp); ServeCurrent(service, service.FireNext(Stamp));
            Rule("unfinished_courses", () => service.Complete(Stamp));
            service.Skip("course-2", "Guest declined", Stamp); service.Complete(Stamp); Equal(DiningState.Completed, service.State);
        });
        Test("skip needs reason and does not recall an already fired course", () => {
            var service = Service(); Throws<ArgumentException>(() => service.Skip("course-2", " ", Stamp));
            service.Start(Stamp); service.FireNext(Stamp); Rule("course_not_pending", () => service.Skip("course-1", "Recall", Stamp));
        });
        Test("finished service rejects new kitchen actions", () => {
            var service = Service(); Finish(service); Rule("service_not_running", () => service.FireNext(Stamp));
            Rule("service_not_active", () => service.ReadyPreparation("course-1", "fish", Stamp));
        });
        Test("cancelling an unstarted service does not refund or release automatically", () => {
            var service = Service(); var account = Account(); var occupancy = Occupancy(); account.RecordPayment("p", "card", 30000, Stamp);
            service.CancelUnstarted("Did not dine", Stamp); Equal(30000L, account.PaidCents);
            Equal(OccupancyState.Occupied, occupancy.State); Equal(AccountState.Open, account.State);
        });
        Test("active service cancellation requires explicit kitchen recall workflow", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            Rule("service_already_started", () => service.CancelUnstarted("Cancel", Stamp));
        });
        Test("void preserves historical charge and exposes credit instead of erasing payment", () => {
            var account = Account(); account.RecordPayment("p", "card", 30000, Stamp);
            account.VoidCharge("menus", "Wrong account", Stamp); Equal(30000L, account.PaidCents);
            Equal(30000L, account.CreditCents); Equal(PaymentCoverage.Credit, account.Coverage);
            Equal(1, account.View().Charges.Count); True(account.View().Charges[0].Voided);
            Rule("account_not_balanced", () => account.Close(Stamp));
        });
        Test("closing unpaid account fails without side effects", () => {
            var account = Account(); var count = account.PendingEvents.Count;
            Rule("account_not_balanced", () => account.Close(Stamp)); Equal(count, account.PendingEvents.Count);
        });
        Test("closed account rejects charge, payment and void", () => {
            var account = Account(); account.RecordPayment("p", "card", 30000, Stamp); account.Close(Stamp);
            Rule("account_closed", () => account.AddCharge("x", "x", 1, 100, Stamp));
            Rule("account_closed", () => account.RecordPayment("q", "card", 100, Stamp));
            Rule("account_closed", () => account.VoidCharge("menus", "x", Stamp));
        });
        Test("duplicate financial identifiers do not duplicate events or amounts", () => {
            var account = Account(); account.RecordPayment("p", "card", 100, Stamp); var count = account.PendingEvents.Count;
            Rule("duplicate_payment", () => account.RecordPayment("p", "card", 100, Stamp));
            Rule("duplicate_charge", () => account.AddCharge("menus", "Copy", 1, 100, Stamp));
            Equal(count, account.PendingEvents.Count); Equal(100L, account.PaidCents); Equal(30000L, account.TotalCents);
        });
        Test("invalid amounts and blank methods are rejected", () => {
            var account = Account(); Rule("invalid_payment", () => account.RecordPayment("p", "card", 0, Stamp));
            Rule("invalid_charge", () => account.AddCharge("x", "x", 0, 50, Stamp));
            Rule("invalid_charge", () => account.AddCharge("x", "x", 1, -50, Stamp));
            Throws<ArgumentException>(() => account.RecordPayment("p", " ", 50, Stamp));
        });
        Test("charge arithmetic overflow cannot partially mutate account", () => {
            var account = Account(); var count = account.PendingEvents.Count;
            Throws<OverflowException>(() => account.AddCharge("overflow", "Overflow", 2, long.MaxValue, Stamp));
            Equal(30000L, account.TotalCents); Equal(count, account.PendingEvents.Count);
        });
        Test("payment arithmetic overflow cannot partially mutate account", () => {
            var account = Account(); account.RecordPayment("p", "card", long.MaxValue, Stamp); var count = account.PendingEvents.Count;
            Throws<OverflowException>(() => account.RecordPayment("q", "card", 1, Stamp));
            Equal(long.MaxValue, account.PaidCents); Equal(count, account.PendingEvents.Count);
        });
        Test("zero-value account closes without creating a payment", () => {
            var account = new SettlementAccount("a", Scope, "s"); account.Close(Stamp);
            Equal(0, account.View().Payments.Count); Equal("account.closed", account.PendingEvents.Single().Type);
        });
        Test("cross-company command cannot mutate any aggregate", () => {
            var foreign = new CommandStamp(new("tenant-1", "other-company", "location-1"), "operator-1", Time);
            var service = Service(); var account = Account(); var occupancy = Occupancy();
            Rule("scope_mismatch", () => service.Start(foreign));
            Rule("scope_mismatch", () => account.RecordPayment("p", "card", 1, foreign));
            Rule("scope_mismatch", () => occupancy.Release(service, "x", foreign)); Equal(DiningState.Open, service.State);
        });
        Test("menu input changes cannot rewrite the running snapshot", () => {
            var items = new List<PreparationDefinition> { new("p", "Original", "cold") };
            var definitions = new List<CourseDefinition> { new("c", "Original course", items) };
            var service = new DiningService("s", Scope, "t", 1, definitions); items.Clear(); definitions.Clear();
            Equal("Original", service.View().Courses[0].Preparations[0].Name);
        });
        Test("invalid guest position and duplicate course definitions are rejected", () => {
            Rule("invalid_guest", () => new DiningService("s", Scope, "t", 1,
                [new("c", "c", [new("p", "p", "cold", GuestPosition: 2)])]));
            Rule("duplicate_course", () => new DiningService("s", Scope, "t", 1,
                [new("c", "c", []), new("c", "c", [])]));
        });
        Test("operational projection contains no financial names or data", () => {
            var service = Service(); var occupancy = Occupancy(); var account = Account();
            account.AddCharge("secret", "Private bottle price", 1, 918273, Stamp);
            var text = JsonSerializer.Serialize(new ServiceBoardView(service.View(), occupancy.View()), Json).ToLowerInvariant();
            foreach (var forbidden in new[] { "account", "payment", "price", "balance", "credit", "918273", "private bottle" })
                True(!text.Contains(forbidden, StringComparison.Ordinal), $"Operational leak: {forbidden}");
        });
        Test("events retain identity, scope, actor and UTC timestamp", () => {
            var service = Service(); service.Start(Stamp); var e = service.PendingEvents.Single();
            Equal(Scope, e.Scope); Equal("operator-1", e.ActorId); Equal(Time, e.At);
            True(e.Id != Guid.Empty); Equal("service.started", e.Type);
        });
        Test("legacy status planning is read-only and refuses invented occupancy", () => {
            var input = new LegacyLifecycleInput("legacy-id", "paid", true, false, ["preparing", "pending"], 30000, 30000);
            var before = JsonSerializer.Serialize(input, Json); var plan = LegacyLifecyclePlanner.Plan(input);
            Equal(before, JsonSerializer.Serialize(input, Json)); Equal("legacy-id", plan.ServiceId);
            True(plan.SuggestedDiningState is null); True(plan.SuggestedOccupancyState is null);
            True(plan.SuggestedAccountState is null); True(plan.RequiresReview);
            True(plan.Reasons.Contains("financial_status_overwrote_dining_state"));
        });
        Test("negative legacy accounting requires reconciliation instead of truncation", () => {
            Throws<ArgumentException>(() => LegacyLifecyclePlanner.Plan(new("s", "open", false, false, [], -1, 0)));
        });
        Test("advertised affordances are exactly the executable actions in every stage", () => {
            bool Try(Action attempt) { try { attempt(); return true; } catch (RuleViolation) { return false; } }
            void Consistent(bool advertised, bool executable, string label)
            { if (advertised != executable) throw new Exception($"Affordance mismatch: {label} advertised={advertised} executable={executable}"); }
            var serviceCatalog = new Dictionary<string, Action<DiningService>> {
                ["start"] = s => s.Start(Stamp), ["fire-next"] = s => s.FireNext(Stamp),
                ["pause"] = s => s.Pause("x", Stamp), ["resume"] = s => s.Resume(Stamp),
                ["complete"] = s => s.Complete(Stamp), ["cancel-unstarted"] = s => s.CancelUnstarted("x", Stamp),
                ["declare-restriction"] = s => s.DeclareRestriction(1, RestrictionKind.Allergy, "apio", RestrictionSeverity.Severe, Stamp),
                ["remove-restriction"] = s => s.RemoveRestriction(s.Restrictions.FirstOrDefault()?.Id ?? "missing", "x", Stamp) };
            var courseCatalog = new Dictionary<string, Action<DiningService>> {
                ["ready"] = s => s.ValidateReady("course-1", Stamp), ["serve"] = s => s.Serve("course-1", Stamp),
                ["skip"] = s => s.Skip("course-1", "x", Stamp) };
            var prepCatalog = new Dictionary<string, Action<DiningService>> {
                ["preparation-start"] = s => s.StartPreparation("course-1", "fish", Stamp),
                ["preparation-ready"] = s => s.ReadyPreparation("course-1", "fish", Stamp),
                ["review-preparation"] = s => s.ReviewPreparation("course-1", "fish", ReviewDecision.Unaffected, "x", Stamp) };
            // E4e: la orden de grupo la deduce Affordances.Filter en el pase si alguna elaboracion anuncia la individual, y el cliente
            // envia SOLO las que la anuncian: con ellas, la orden de grupo tiene que ser ejecutable (una por pase para marcar).
            var groupCatalog = new Dictionary<string, Action<DiningService, (string Course, string Item)[]>> {
                ["preparation-start"] = (s, items) => { foreach (var c in items.GroupBy(i => i.Course)) s.StartPreparations(c.Key, c.Select(i => i.Item).ToArray(), Stamp); },
                ["preparation-ready"] = (s, items) => { foreach (var c in items.GroupBy(i => i.Course)) s.ReadyPreparations(c.Key, c.Select(i => i.Item).ToArray(), Stamp); },
                ["review-preparation"] = (s, items) => s.ReviewPreparations(items.Select(i => new PreparationDecision(i.Course, i.Item, ReviewDecision.Unaffected, "x")).ToArray(), Stamp) };
            var stages = new (string Name, Action<DiningService> Build)[] {
                ("open", _ => { }),
                ("started", s => s.Start(Stamp)),
                ("fired", s => { s.Start(Stamp); s.FireNext(Stamp); }),
                ("preparing", s => { s.Start(Stamp); s.FireNext(Stamp); s.StartPreparation("course-1", "fish", Stamp); }),
                ("mandatory-ready", s => { s.Start(Stamp); s.FireNext(Stamp); s.ReadyPreparation("course-1", "fish", Stamp); s.ReadyPreparation("course-1", "sauce", Stamp); }),
                ("course-ready", s => { s.Start(Stamp); s.FireNext(Stamp); s.ReadyPreparation("course-1", "fish", Stamp); s.ReadyPreparation("course-1", "sauce", Stamp); s.ValidateReady("course-1", Stamp); }),
                ("paused-fired", s => { s.Start(Stamp); s.FireNext(Stamp); s.Pause("x", Stamp); }),
                ("first-served", s => { s.Start(Stamp); ServeCurrent(s, s.FireNext(Stamp)); }),
                ("all-served", s => { s.Start(Stamp); ServeCurrent(s, s.FireNext(Stamp)); ServeCurrent(s, s.FireNext(Stamp)); }),
                ("completed", Finish),
                ("with-restriction", s => s.DeclareRestriction(2, RestrictionKind.Intolerance, "lactosa", RestrictionSeverity.Moderate, Stamp)),
                ("pending-ack", s => { s.Start(Stamp); s.FireNext(Stamp);
                    s.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp); }),
                ("pending-after-ready", s => { s.Start(Stamp); s.FireNext(Stamp);
                    s.ReadyPreparation("course-1", "fish", Stamp); s.ReadyPreparation("course-1", "sauce", Stamp);
                    s.ValidateReady("course-1", Stamp);
                    s.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp); }),
                ("pending-on-fish", s => { s.Start(Stamp); s.FireNext(Stamp);
                    s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp); }),
                ("reviewed-remake", s => { s.Start(Stamp); s.FireNext(Stamp);
                    s.ReadyPreparation("course-1", "fish", Stamp); s.ReadyPreparation("course-1", "sauce", Stamp);
                    s.ValidateReady("course-1", Stamp);
                    s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
                    s.ReviewPreparation("course-1", "sauce", ReviewDecision.Unaffected, "sin marisco", Stamp);
                    s.ReviewPreparation("course-1", "fish", ReviewDecision.Remake, "cambiar por verduras", Stamp); }),
                // E4e (ADR-016): revisar con la mesa sentada y en pases sin enviar; el bloqueo de fire-next es del pase que sale.
                ("seated-reviewed", s => { s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
                    s.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "vieira", Stamp);
                    s.ReviewPreparation("course-1", "sauce", ReviewDecision.Unaffected, "sin marisco", Stamp); }),
                ("started-pending-unsent", s => { s.Start(Stamp);
                    s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp); }),
                ("served-then-pending", s => { s.Start(Stamp); ServeCurrent(s, s.FireNext(Stamp));
                    s.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp); }),
                ("skipped-after-pending", s => { s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
                    s.Skip("course-1", "x", Stamp); }),
                ("cancelled-after-pending", s => { s.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
                    s.CancelUnstarted("x", Stamp); }) };
            foreach (var (name, build) in stages)
            {
                DiningService At() { var s = Service(); build(s); return s; }
                var view = At().View(true);
                var course = view.Courses.Single(c => c.Id == "course-1");
                var item = course.Preparations.Single(p => p.Id == "fish");
                foreach (var (action, run) in serviceCatalog)
                    Consistent(view.Actions!.Contains(action), Try(() => run(At())), $"{name}/{action}");
                foreach (var (action, run) in courseCatalog)
                    Consistent(course.Actions!.Contains(action), Try(() => run(At())), $"{name}/course-1/{action}");
                foreach (var (action, run) in prepCatalog)
                    Consistent(item.Actions!.Contains(action), Try(() => run(At())), $"{name}/fish/{action}");
                foreach (var (individual, run) in groupCatalog)
                {
                    var announced = view.Courses.SelectMany(c => c.Preparations.Where(p => p.Actions!.Contains(individual)).Select(p => (c.Id, p.Id))).ToArray();
                    if (announced.Length > 0) Consistent(true, Try(() => run(At(), announced)), $"{name}/group/{individual}");
                }
            }
        });
        Test("plain view carries no affordances and stays snapshot-identical", () => {
            var service = Service(); service.Start(Stamp);
            True(service.View().Actions is null && service.View().Courses.All(c => c.Actions is null));
        });
        Test("account affordances follow balance and lifecycle", () => {
            var account = Account();
            var view = account.ViewWithActions(); var actions = view.Actions!;
            True(actions.Contains("add-product") && actions.Contains("payment") && actions.Contains("void-charge"));
            True(!actions.Contains("close")); Equal(1, view.VoidableChargeIds!.Count);
            account.RecordPayment("p", "card", 30000, Stamp);
            True(account.ViewWithActions().Actions!.Contains("close"));
            account.VoidCharge("menus", "x", Stamp);
            view = account.ViewWithActions(); actions = view.Actions!;
            True(!actions.Contains("close") && !actions.Contains("void-charge") && view.VoidableChargeIds!.Count == 0);
            True(actions.Contains("refund"), "Credit after a void must be refundable.");
            account.AddCharge("l2", "Reequilibrio", 1, 30000, Stamp); account.Close(Stamp);
            True(account.ViewWithActions().Actions!.SequenceEqual(["reopen"]), "A closed account only offers an audited reopen.");
        });
        Test("closed account reopens with an audited reason and accepts later extras", () => {
            var account = Account(); account.RecordPayment("p", "card", 30000, Stamp); account.Close(Stamp);
            Rule("account_closed", () => account.AddCharge("water", "Agua", 1, 400, Stamp));
            Throws<ArgumentException>(() => account.Reopen(" ", Stamp));
            account.ClearPendingEvents(); account.Reopen("bebida pedida tras cerrar", Stamp);
            Equal(AccountState.Open, account.State); Equal("bebida pedida tras cerrar", account.PendingEvents.Single().Data["reason"]);
            Equal("account.reopened", account.PendingEvents.Single().Type);
            account.AddCharge("water", "Agua", 1, 400, Stamp);
            Equal(400L, account.BalanceCents); Equal(1, account.View().Payments.Count); Equal(30000L, account.PaidCents);
            Rule("account_not_closed", () => account.Reopen("otra vez", Stamp));
            account.RecordPayment("q", "cash", 400, Stamp); account.Close(Stamp);
        });
        Test("refund returns existing credit only, keeps the payment and is audited", () => {
            var account = Account(); account.RecordPayment("p", "card", 35000, Stamp);
            Equal(5000L, account.CreditCents); True(account.ViewWithActions().Actions!.Contains("refund"));
            Rule("refund_exceeds_credit", () => account.Refund("r1", "cash", 6000, "cobro de más", Stamp));
            Rule("invalid_refund", () => account.Refund("r1", "cash", 0, "x", Stamp));
            Throws<ArgumentException>(() => account.Refund("r1", "cash", 5000, " ", Stamp));
            Rule("account_not_balanced", () => account.Close(Stamp));
            account.ClearPendingEvents(); account.Refund("r1", "cash", 5000, "cobro de más", Stamp);
            Equal("payment.refunded", account.PendingEvents.Single().Type); Equal("5000", account.PendingEvents.Single().Data["amount_cents"]);
            Equal(0L, account.CreditCents); Equal(30000L, account.PaidCents); Equal(5000L, account.RefundedCents);
            Equal(PaymentCoverage.Paid, account.Coverage); Equal(1, account.View().Payments.Count); Equal(1, account.View().Refunds!.Count);
            Rule("duplicate_refund", () => account.Refund("r1", "cash", 1, "x", Stamp));
            Rule("duplicate_refund", () => account.Refund("p", "cash", 1, "x", Stamp));
            True(!account.ViewWithActions().Actions!.Contains("refund"));
            account.Close(Stamp);
            Rule("account_closed", () => account.Refund("r2", "cash", 1, "x", Stamp));
        });
        Test("void after payment leaves credit that a refund resolves before an honest close", () => {
            var account = Account(); account.RecordPayment("p", "card", 30000, Stamp);
            account.VoidCharge("menus", "no consumido", Stamp); Equal(30000L, account.CreditCents);
            account.Refund("r1", "card", 30000, "anulación tras pagar", Stamp);
            Equal(0L, account.TotalCents); Equal(0L, account.PaidCents); Equal(30000L, account.RefundedCents);
            account.Close(Stamp); Equal(AccountState.Closed, account.State);
        });
        // E4e (ADR-016): cocina decide cada plato afectado ANTES de que salga su pase; el bloqueo es por pase.
        Test("an allergy declared while seated marks every affected preparation, sent or not", () => {
            var service = Service();
            service.ClearPendingEvents();
            service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            True(service.RestrictionsPendingAck, "A seated table with an allergy waits for the kitchen.");
            var courses = service.View(true).Courses;
            True(courses[0].Preparations.Single(p => p.Id == "sauce").ReviewPending, "Whole-table sauce is affected by guest 1.");
            True(!courses[0].Preparations.Single(p => p.Id == "fish").ReviewPending, "Guest 2 fish is not affected by guest 1.");
            True(courses[1].Preparations.Single().ReviewPending, "The unsent dessert (whole table) is affected too.");
            True(courses[0].Preparations.Single(p => p.Id == "sauce").Actions!.Contains("review-preparation"), "Reviewable while seated.");
            Equal("2", service.PendingEvents.Single().Data["affected_preparations"]);
            True(!service.View(true).Actions!.Contains("fire-next"));
            Rule("restrictions_unreviewed", () => service.FireNext(Stamp));
            Equal(DiningState.Open, service.State);
            service.Start(Stamp);
            Rule("restrictions_unreviewed", () => service.FireNext(Stamp));
        });
        Test("a whole-table restriction marks every preparation of every course that is not served or skipped", () => {
            var service = Service(); service.Start(Stamp); ServeCurrent(service, service.FireNext(Stamp));
            service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            var courses = service.View().Courses;
            True(courses[0].Preparations.All(p => !p.ReviewPending), "A served course is never marked.");
            True(courses[1].Preparations.All(p => p.ReviewPending), "A mild preference is reviewed like any restriction.");
        });
        Test("the kitchen reviews an unsent course with the table seated and fire-next starts and sends it in one command", () => {
            var service = Service();
            service.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "vieira en vez de merluza", Stamp);
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Unaffected, "no lleva marisco", Stamp);
            Equal(DiningState.Open, service.State);
            var actions = service.View(true).Actions!;
            True(actions.Contains("fire-next") && actions.Contains("start"), "Seated: send the first course or just start.");
            service.ClearPendingEvents();
            Equal("course-1", service.FireNext(Stamp));
            Equal(DiningState.InService, service.State); Equal(Time, service.StartedAt);
            Equal("service.started,course.fired", string.Join(",", service.PendingEvents.Select(e => e.Type)));
            var fish = service.View().Courses[0].Preparations.Single(p => p.Id == "fish");
            Equal("Fish for PAX 2", fish.Name);   // el nombre del plato no cambia: la adaptacion va en la nota
            Equal((ReviewDecision.Adapt, "vieira en vez de merluza"), (fish.Review!.Decision, fish.Review.Note));
            True(service.RestrictionsPendingAck, "The unsent dessert still waits for its decision.");
        });
        Test("a reviewed course goes out while a later one still waits, and an unreviewed one does not", () => {
            var service = Service(); service.Start(Stamp);
            service.DeclareRestriction(null, RestrictionKind.Intolerance, "lactosa", RestrictionSeverity.Mild, Stamp);
            Rule("restrictions_unreviewed", () => service.FireNext(Stamp));
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Unaffected, "no lleva lacteos", Stamp);
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa sin nata", Stamp);
            True(service.View(true).Actions!.Contains("fire-next"));
            Equal("course-1", service.FireNext(Stamp));
            ServeCurrent(service, "course-1");
            True(!service.View(true).Actions!.Contains("fire-next"));
            Rule("restrictions_unreviewed", () => service.FireNext(Stamp));
            service.ReviewPreparation("course-2", "dessert", ReviewDecision.Adapt, "sorbete en vez de helado", Stamp);
            Equal("course-2", service.FireNext(Stamp));
        });
        Test("fire-next from a seated table is all or nothing", () => {
            var unreviewed = Service();
            unreviewed.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            unreviewed.ClearPendingEvents();
            Rule("restrictions_unreviewed", () => unreviewed.FireNext(Stamp));
            Equal(DiningState.Open, unreviewed.State); True(unreviewed.StartedAt is null && unreviewed.PendingEvents.Count == 0);
            var choice = new DiningService("s", Scope, "M1", 2, [new("primeros", "Primeros", [], ChoiceRequired: true)], "LAB-DAILY");
            Rule("choice_missing", () => choice.FireNext(Stamp));
            Equal(DiningState.Open, choice.State); True(choice.StartedAt is null && choice.PendingEvents.Count == 0);
            var carte = new DiningService("s", Scope, "M1", 2, [new("entrantes", "Entrantes", [], Optional: true)], "LAB-CARTA");
            Rule("course_empty", () => carte.FireNext(Stamp));
            Equal(DiningState.Open, carte.State);
            var paused = Service(); paused.Start(Stamp); paused.Pause("x", Stamp);
            Rule("service_not_running", () => paused.FireNext(Stamp));
        });
        Test("remake is only for work already sent", () => {
            var service = Service();
            service.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            Rule("nothing_to_remake", () => service.ReviewPreparation("course-1", "fish", ReviewDecision.Remake, "otra merluza", Stamp));
            True(service.View().Courses[0].Preparations.Single(p => p.Id == "fish").ReviewPending, "A refused remake decides nothing.");
            service.ClearPendingEvents();
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "vieira", Stamp);
            Equal("restriction.reviewed", service.PendingEvents.Single().Type);
        });
        Test("a choice or a dish ordered for a guest with a restriction is born pending review", () => {
            var menu = new DiningService("s", Scope, "M1", 2, [new("primeros", "Primeros", [], ChoiceRequired: true)], "LAB-DAILY");
            menu.DeclareRestriction(1, RestrictionKind.Allergy, "gluten", RestrictionSeverity.Severe, Stamp);
            menu.Choose("primeros", 1, new("sopa", "Sopa", "hot"), Stamp);
            menu.Choose("primeros", 2, new("sopa", "Sopa", "hot"), Stamp);
            True(menu.View().Courses[0].Preparations.Single(p => p.GuestPosition == 1).ReviewPending, "Guest 1's choice waits for the kitchen.");
            True(!menu.View().Courses[0].Preparations.Single(p => p.GuestPosition == 2).ReviewPending, "Guest 2 has no restriction.");
            menu.ReviewPreparation("primeros", "sopa-1", ReviewDecision.Unaffected, "sopa sin gluten", Stamp);
            menu.Choose("primeros", 1, new("ensalada", "Ensalada", "cold"), Stamp);   // cambia de idea: el plato nuevo vuelve a necesitar revision
            var replaced = menu.View().Courses[0].Preparations.Single(p => p.GuestPosition == 1);
            True(replaced.Id == "ensalada-1" && replaced.ReviewPending && replaced.Review is null);
            Rule("restrictions_unreviewed", () => menu.FireNext(Stamp));
            var carte = new DiningService("s", Scope, "M1", 2, [new("entrantes", "Entrantes", [], Optional: true)], "LAB-CARTA");
            carte.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            carte.AddDish("entrantes", new("croquetas-unit-1", "Croquetas", "cold", 2, 2), Stamp);   // para el comensal 2: no le aplica
            carte.AddDish("entrantes", new("croquetas-unit-2", "Croquetas", "cold", 1, 1), Stamp);   // para el comensal 1
            carte.AddDish("entrantes", new("pulpo-unit-1", "Pulpo", "hot"), Stamp);                  // para compartir: le aplica cualquiera
            Equal("croquetas-unit-2,pulpo-unit-1", string.Join(",", carte.View().Courses[0].Preparations.Where(p => p.ReviewPending).Select(p => p.Id)));
        });
        Test("skipping a course or cancelling an unstarted table withdraws its pending reviews", () => {
            var service = Service();
            service.DeclareRestriction(null, RestrictionKind.Allergy, "gluten", RestrictionSeverity.Severe, Stamp);
            service.ReviewPreparation("course-2", "dessert", ReviewDecision.Adapt, "tarta sin gluten", Stamp);
            service.DeclareRestriction(1, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            service.ClearPendingEvents();
            service.Skip("course-2", "no quieren postre", Stamp);
            var dessert = service.View().Courses[1].Preparations.Single();
            True(!dessert.ReviewPending && dessert.Review!.Note == "tarta sin gluten", "Withdrawn, and the decision taken stays.");
            Equal("1", service.PendingEvents.Single().Data["reviews_withdrawn"]);
            True(service.RestrictionsPendingAck, "Course 1 still waits.");
            var plain = Service(); plain.ClearPendingEvents(); plain.Skip("course-2", "x", Stamp);
            True(!plain.PendingEvents.Single().Data.ContainsKey("reviews_withdrawn"), "A skip without reviews leaves the event as before.");
            var cancelled = Service();
            cancelled.DeclareRestriction(null, RestrictionKind.Allergy, "gluten", RestrictionSeverity.Severe, Stamp);
            cancelled.ClearPendingEvents();
            cancelled.CancelUnstarted("no vienen", Stamp);
            True(!cancelled.RestrictionsPendingAck); Equal("3", cancelled.PendingEvents.Single().Data["reviews_withdrawn"]);
            Equal(DiningState.Cancelled, DiningService.Restore(cancelled.Snapshot()).State);
        });
        Test("a seated table with reviews on unsent courses is saved and read back", () => {
            var service = Service();
            service.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "vieira", Stamp);
            var snapshot = service.Snapshot();
            var restored = DiningService.Restore(snapshot);
            Equal(JsonSerializer.Serialize(snapshot, Json), JsonSerializer.Serialize(restored.Snapshot(), Json));
            True(restored.RestrictionsPendingAck && restored.View().Courses[1].Preparations.Single().ReviewPending);
            Equal(ReviewDecision.Adapt, restored.View().Courses[0].Preparations.Single(p => p.Id == "fish").Review!.Decision);
            Rule("invalid_snapshot", () => DiningService.Restore(snapshot with { State = DiningState.Cancelled }));
            Rule("invalid_snapshot", () => DiningService.Restore(snapshot with { Courses = [snapshot.Courses[0],
                snapshot.Courses[1] with { State = CourseState.Skipped, SkipReason = "x" }] }));
        });
        Test("restriction change after firing blocks kitchen flow until every affected preparation is reviewed", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            True(service.RestrictionsPendingAck);
            var course = service.View(true).Courses.Single(c => c.Id == "course-1");
            True(course.Preparations.Single(p => p.Id == "sauce").ReviewPending, "Whole-table sauce is affected by guest 1.");
            True(!course.Preparations.Single(p => p.Id == "fish").ReviewPending, "Guest 2 fish is not affected by guest 1.");
            True(course.Preparations.Single(p => p.Id == "sauce").Actions!.Contains("review-preparation"));
            service.StartPreparation("course-1", "fish", Stamp); // los marcados de estacion siguen
            service.ReadyPreparation("course-1", "fish", Stamp); service.ReadyPreparation("course-1", "sauce", Stamp);
            Rule("restrictions_unreviewed", () => service.ValidateReady("course-1", Stamp));
            Rule("nothing_to_review", () => service.ReviewPreparation("course-1", "fish", ReviewDecision.Unaffected, "x", Stamp));
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa sin marisco", Stamp);
            True(service.View().Courses[0].Preparations.All(p => !p.ReviewPending));
            Equal(ReviewDecision.Adapt, service.View().Courses[0].Preparations.Single(p => p.Id == "sauce").Review!.Decision);
            service.ValidateReady("course-1", Stamp); service.Serve("course-1", Stamp);
            // E4e: el postre (mesa entera) tambien quedo marcado aunque no se habia enviado; sale cuando cocina decide.
            Rule("restrictions_unreviewed", () => service.FireNext(Stamp));
            service.ReviewPreparation("course-2", "dessert", ReviewDecision.Unaffected, "no lleva marisco", Stamp);
            True(!service.RestrictionsPendingAck);
            Equal("course-2", service.FireNext(Stamp));
        });
        Test("ready course loses serve until reviewed and remake withdraws its validation", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.ReadyPreparation("course-1", "fish", Stamp); service.ReadyPreparation("course-1", "sauce", Stamp);
            service.ValidateReady("course-1", Stamp);
            service.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            var course = service.View(true).Courses.Single(c => c.Id == "course-1");
            Equal(CourseState.Ready, course.State);
            True(!course.Actions!.Contains("serve") && course.Preparations.All(p => p.ReviewPending));
            Rule("restrictions_unreviewed", () => service.Serve("course-1", Stamp));
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Unaffected, "sin marisco", Stamp);
            Rule("restrictions_unreviewed", () => service.Serve("course-1", Stamp)); // fish sigue pendiente
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Remake, "cambiar por verduras", Stamp);
            course = service.View(true).Courses.Single(c => c.Id == "course-1");
            True(course.Preparations.All(p => !p.ReviewPending));
            Equal(CourseState.Preparing, course.State); True(course.ReadyAt is null);
            Equal(PreparationState.Fired, course.Preparations.Single(p => p.Id == "fish").State);
            Equal(ReviewDecision.Remake, course.Preparations.Single(p => p.Id == "fish").Review!.Decision);
            Rule("mandatory_preparation_pending", () => service.ValidateReady("course-1", Stamp));
            service.ReadyPreparation("course-1", "fish", Stamp); service.ValidateReady("course-1", Stamp); service.Serve("course-1", Stamp);
        });
        Test("consecutive changes each demand their own decisions", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa aparte", Stamp);
            True(service.View().Courses[0].Preparations.All(p => !p.ReviewPending));
            service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            True(service.View().Courses[0].Preparations.All(p => p.ReviewPending), "A whole-table change affects every sent preparation again.");
            Rule("restrictions_unreviewed", () => service.ValidateReady("course-1", Stamp));
        });
        Test("legacy snapshot with global pending acknowledgement restores every sent preparation as pending review", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            var restored = DiningService.Restore(service.Snapshot() with { RestrictionsPendingAck = true });
            True(restored.RestrictionsPendingAck && restored.View().Courses[0].Preparations.All(p => p.ReviewPending));
            // La compatibilidad D3.2 marca solo lo ENVIADO; el marcado de E4e (tambien lo no enviado) no se aplica a un acuse viejo.
            True(restored.View().Courses[1].Preparations.All(p => !p.ReviewPending), "The D3.2 compatibility marks only sent courses.");
            // E4e: una revision pendiente en un pase SERVIDO sigue siendo ilegible (en uno sin enviar ya no: ver la instantanea sentada).
            var served = Service(); served.Start(Stamp); ServeCurrent(served, served.FireNext(Stamp));
            var snapshot = served.Snapshot(); var done = snapshot.Courses[0];
            Rule("invalid_snapshot", () => DiningService.Restore(snapshot with { Courses = [
                done with { Preparations = [done.Preparations[0] with { ReviewPending = true }, done.Preparations[1]] }, snapshot.Courses[1]] }));
        });
        Test("removal after firing also requires a review of the preparations it applied to", () => {
            var service = Service();
            var id = service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            service.ReviewPreparation("course-1", "fish", ReviewDecision.Unaffected, "no lleva cilantro", Stamp);
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa sin cilantro", Stamp);
            service.Start(Stamp); service.FireNext(Stamp);
            service.RemoveRestriction(id, "El cliente lo retira", Stamp);
            True(service.RestrictionsPendingAck && service.Restrictions.Count == 0);
            True(service.View().Courses[0].Preparations.All(p => p.ReviewPending));
            Rule("restrictions_unreviewed", () => service.ValidateReady("course-1", Stamp));
        });
        Test("invalid, duplicate and finished restriction changes are rejected", () => {
            var service = Service();
            Rule("invalid_guest", () => service.DeclareRestriction(3, RestrictionKind.Allergy, "x", RestrictionSeverity.Severe, Stamp));
            service.DeclareRestriction(1, RestrictionKind.Allergy, "Marisco", RestrictionSeverity.Severe, Stamp);
            Rule("duplicate_restriction", () => service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Mild, Stamp));
            Rule("restriction_not_found", () => service.RemoveRestriction("missing", "x", Stamp));
            Rule("nothing_to_review", () => service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "x", Stamp));   // comensal 2: no le aplica
            Rule("invalid_review", () => { service.DeclareRestriction(2, RestrictionKind.Allergy, "apio", RestrictionSeverity.Severe, Stamp);
                service.ReviewPreparation("course-1", "fish", (ReviewDecision)9, "x", Stamp); });
            Throws<ArgumentException>(() => service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, " ", Stamp));
            var finished = Service(); Finish(finished);
            Rule("service_finished", () => finished.DeclareRestriction(1, RestrictionKind.Allergy, "x", RestrictionSeverity.Severe, Stamp));
        });
        Test("preparations project only the applicable restrictions with severity", () => {
            var service = Service();
            service.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            var course = service.View(true).Courses.Single(c => c.Id == "course-1");
            var fish = course.Preparations.Single(p => p.Id == "fish");     // comensal 2
            var sauce = course.Preparations.Single(p => p.Id == "sauce");   // sin comensal
            Equal(2, fish.Restrictions!.Count);
            True(fish.Restrictions.Any(r => r.Substance == "marisco" && r.Severity == RestrictionSeverity.Severe));
            Equal(2, sauce.Restrictions!.Count); // sin posicion: puede ser para cualquier comensal
            True(service.View().Courses.All(c => c.Preparations.All(p => p.Restrictions is null)),
                "Plain views must not carry projected restrictions.");
            Equal(2, service.View().Restrictions!.Count);
        });
        Test("restriction events carry structured data for audit", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.ClearPendingEvents();
            service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            var declared = service.PendingEvents.Single();
            Equal("restriction.declared", declared.Type);
            Equal("marisco", declared.Data["substance"]); Equal("Severe", declared.Data["severity"]);
            Equal("2", declared.Data["affected_preparations"]);   // E4e: la salsa enviada y el postre sin enviar (mesa entera)
            service.ClearPendingEvents();
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa aparte", Stamp);
            var reviewed = service.PendingEvents.Single();
            Equal("restriction.reviewed", reviewed.Type); Equal("Adapt", reviewed.Data["decision"]); Equal("sauce", reviewed.Data["item_id"]);
        });
        // E4e: ordenes de grupo. UNA orden con varias elaboraciones: se comprueban todas antes de tocar ninguna (todo o nada tambien en
        // memoria) y deja un evento por elaboracion con los tipos de siempre.
        Test("group marks are one all-or-nothing change with one event per preparation", () => {
            var seated = Service();
            Rule("service_not_active", () => seated.ReadyPreparations("course-1", ["fish"], Stamp));
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            service.ClearPendingEvents();
            Rule("preparation_not_found", () => service.ReadyPreparations("course-1", ["fish", "missing"], Stamp));
            Throws<ArgumentException>(() => service.StartPreparations("course-1", ["fish", "fish"], Stamp));
            Throws<ArgumentException>(() => service.StartPreparations("course-1", [], Stamp));
            Rule("course_not_found", () => service.StartPreparations("course-9", ["fish"], Stamp));
            Rule("course_not_preparing", () => service.StartPreparations("course-2", ["dessert"], Stamp));
            True(service.View().Courses[0].Preparations.All(p => p.State == PreparationState.Fired) && service.PendingEvents.Count == 0, "A refused group changes nothing.");
            service.StartPreparation("course-1", "fish", Stamp);
            Rule("preparation_not_fired", () => service.StartPreparations("course-1", ["sauce", "fish"], Stamp));
            Equal(PreparationState.Fired, service.View().Courses[0].Preparations.Single(p => p.Id == "sauce").State);
            service.ClearPendingEvents();
            service.ReadyPreparations("course-1", ["fish", "sauce"], Stamp);
            True(service.View().Courses[0].Preparations.All(p => p.State == PreparationState.Ready));
            Equal("course_item.ready:fish,course_item.ready:sauce", string.Join(",", service.PendingEvents.Select(e => e.Type + ":" + e.Data["item_id"])));
            Rule("preparation_not_active", () => service.ReadyPreparations("course-1", ["fish"], Stamp));
            service.ValidateReady("course-1", Stamp);
            var started = Service(); started.Start(Stamp); started.FireNext(Stamp); started.ClearPendingEvents();
            started.StartPreparations("course-1", ["sauce", "fish"], Stamp);
            Equal(CourseState.Preparing, started.View().Courses[0].State);
            Equal("course_item.started:sauce,course_item.started:fish", string.Join(",", started.PendingEvents.Select(e => e.Type + ":" + e.Data["item_id"])));
        });
        Test("review-preparations decides a guest's whole menu in one command, each dish with its own decision and note", () => {
            var service = Service();
            service.DeclareRestriction(null, RestrictionKind.Allergy, "gluten", RestrictionSeverity.Severe, Stamp);
            service.ClearPendingEvents();
            static PreparationDecision Decide(string course, string item, ReviewDecision decision, string note) => new(course, item, decision, note);
            var menu = new[] { Decide("course-1", "fish", ReviewDecision.Adapt, "pan de maiz"), Decide("course-1", "sauce", ReviewDecision.Unaffected, "no lleva gluten"),
                Decide("course-2", "dessert", ReviewDecision.Adapt, "tarta sin harina") };
            Rule("nothing_to_remake", () => service.ReviewPreparations([menu[0], menu[1], menu[2] with { Decision = ReviewDecision.Remake }], Stamp));
            Throws<ArgumentException>(() => service.ReviewPreparations([menu[0], menu[1] with { Note = " " }], Stamp));
            Throws<ArgumentException>(() => service.ReviewPreparations([menu[0], menu[0]], Stamp));
            Throws<ArgumentException>(() => service.ReviewPreparations([], Stamp));
            Rule("preparation_not_found", () => service.ReviewPreparations([menu[0], Decide("course-1", "missing", ReviewDecision.Adapt, "x")], Stamp));
            Rule("invalid_review", () => service.ReviewPreparations([menu[0], menu[1] with { Decision = (ReviewDecision)9 }], Stamp));
            True(service.View().Courses.All(c => c.Preparations.All(p => p.ReviewPending && p.Review is null)) && service.PendingEvents.Count == 0,
                "A refused group decides nothing.");
            service.ReviewPreparations(menu, Stamp);
            True(!service.RestrictionsPendingAck);
            True(service.PendingEvents.All(e => e.Type == "restriction.reviewed"));
            Equal("fish:Adapt:pan de maiz,sauce:Unaffected:no lleva gluten,dessert:Adapt:tarta sin harina",
                string.Join(",", service.PendingEvents.Select(e => e.Data["item_id"] + ":" + e.Data["decision"] + ":" + e.Data["note"])));
            Rule("nothing_to_review", () => service.ReviewPreparations([menu[0]], Stamp));
            // Rehacer en grupo, solo lo enviado: vuelve a enviada y el pase pierde su validacion, como una a una.
            var sent = Service(); sent.Start(Stamp); sent.FireNext(Stamp);
            sent.ReadyPreparations("course-1", ["fish", "sauce"], Stamp); sent.ValidateReady("course-1", Stamp);
            sent.DeclareRestriction(2, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            sent.ReviewPreparations([Decide("course-1", "fish", ReviewDecision.Remake, "verduras"), Decide("course-1", "sauce", ReviewDecision.Unaffected, "sin marisco"),
                Decide("course-2", "dessert", ReviewDecision.Unaffected, "sin marisco")], Stamp);
            var course = sent.View().Courses[0];
            Equal((CourseState.Preparing, PreparationState.Fired), (course.State, course.Preparations.Single(p => p.Id == "fish").State));
            True(course.ReadyAt is null && !sent.RestrictionsPendingAck);
        });
        // E4e: quien abrio la mesa (la identidad de la orden 'open'), para «Mis mesas» de C3e. Aditivo: una mesa anterior lo lee vacio.
        Test("the table remembers who opened it and from which station, and an older snapshot reads without them", () => {
            CourseDefinition[] menu = [new("c", "C", [new("a", "A", "hot")])];
            var device = new DiningService("s", Scope, "M1", 2, menu, "LAB-TASTING", "device:movil-ana", "sala-1");
            Equal(("device:movil-ana", "sala-1"), (device.View(true).OpenedBy, device.View(true).OpenedStation));
            var restored = DiningService.Restore(device.Snapshot());
            Equal(("device:movil-ana", "sala-1"), (restored.OpenedBy, restored.OpenedStation));
            var person = JsonSerializer.SerializeToElement(new DiningService("s", Scope, "M1", 2, menu, "LAB-TASTING", "user:chef").View(true), Json);
            Equal("user:chef", person.GetProperty("OpenedBy").GetString());
            True(!person.TryGetProperty("OpenedStation", out _), "A person has no station: the key is left out.");
            var older = DiningService.Restore(device.Snapshot() with { OpenedBy = null, OpenedStation = null });
            True(older.OpenedBy is null && older.OpenedStation is null);
            True(!JsonSerializer.Serialize(older.View(true), Json).Contains("Opened", StringComparison.Ordinal), "A table saved before E4e shows neither key.");
            True(new DiningService("s", Scope, "M1", 2, menu, null, " ", "").OpenedBy is null, "Blank is the same as unknown.");
        });
        Test("release affordance appears only while occupied and dining finished", () => {
            var service = Service(); var occupancy = Occupancy();
            True(!occupancy.View(service).Actions!.Contains("release"));
            Finish(service);
            True(occupancy.View(service).Actions!.Contains("release"));
            occupancy.Release(service, "x", Stamp);
            True(!occupancy.View(service).Actions!.Contains("release"));
        });
        // Shared fixtures document mappings, including deliberately unresolved cases.
        var fixtures = JsonSerializer.Deserialize<List<LegacyCase>>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-lifecycle.json")), Json)
            ?? throw new Exception("Missing migration cases.");
        foreach (var fixture in fixtures)
        {
            var f = fixture;
            Test("legacy mapping: " + f.Name, () => {
                var plan = LegacyLifecyclePlanner.Plan(new("legacy-" + f.Name, f.Status, f.Started, f.Closed, f.Courses, f.Total, f.Paid));
                Equal(f.ExpectedDining, plan.SuggestedDiningState?.ToString()); Equal(f.ExpectedCoverage, plan.Coverage.ToString());
                True(plan.RequiresReview); True(plan.SuggestedOccupancyState is null); True(plan.SuggestedAccountState is null);
            });
        }
    }
}
