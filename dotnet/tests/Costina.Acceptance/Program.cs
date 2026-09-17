using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Costina.Domain;
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
                    s.ReviewPreparation("course-1", "fish", ReviewDecision.Remake, "cambiar por verduras", Stamp); }) };
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
        Test("declaring before firing needs no kitchen acknowledgement", () => {
            var service = Service();
            service.DeclareRestriction(1, RestrictionKind.Allergy, "marisco", RestrictionSeverity.Severe, Stamp);
            True(!service.RestrictionsPendingAck); service.Start(Stamp);
            Equal("course-1", service.FireNext(Stamp));
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
            True(!service.RestrictionsPendingAck);
            Equal(ReviewDecision.Adapt, service.View().Courses[0].Preparations.Single(p => p.Id == "sauce").Review!.Decision);
            service.ValidateReady("course-1", Stamp); service.Serve("course-1", Stamp);
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
            True(!service.RestrictionsPendingAck);
            course = service.View(true).Courses.Single(c => c.Id == "course-1");
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
            True(!service.RestrictionsPendingAck);
            service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
            True(service.View().Courses[0].Preparations.All(p => p.ReviewPending), "A whole-table change affects every sent preparation again.");
            Rule("restrictions_unreviewed", () => service.ValidateReady("course-1", Stamp));
        });
        Test("legacy snapshot with global pending acknowledgement restores every sent preparation as pending review", () => {
            var service = Service(); service.Start(Stamp); service.FireNext(Stamp);
            var restored = DiningService.Restore(service.Snapshot() with { RestrictionsPendingAck = true });
            True(restored.RestrictionsPendingAck && restored.View().Courses[0].Preparations.All(p => p.ReviewPending));
            True(restored.View().Courses[1].Preparations.All(p => !p.ReviewPending), "Unsent courses never carry reviews.");
            var snapshot = service.Snapshot(); var unsent = snapshot.Courses[1];
            Rule("invalid_snapshot", () => DiningService.Restore(snapshot with { Courses = [snapshot.Courses[0],
                unsent with { Preparations = [unsent.Preparations[0] with { ReviewPending = true }] }] }));
        });
        Test("removal after firing also requires a review of the preparations it applied to", () => {
            var service = Service();
            var id = service.DeclareRestriction(null, RestrictionKind.Preference, "sin cilantro", RestrictionSeverity.Mild, Stamp);
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
            Rule("nothing_to_review", () => { service.Start(Stamp); service.FireNext(Stamp);
                service.ReviewPreparation("course-1", "fish", ReviewDecision.Adapt, "x", Stamp); });
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
            Equal("1", declared.Data["affected_preparations"]);
            service.ClearPendingEvents();
            service.ReviewPreparation("course-1", "sauce", ReviewDecision.Adapt, "salsa aparte", Stamp);
            var reviewed = service.PendingEvents.Single();
            Equal("restriction.reviewed", reviewed.Type); Equal("Adapt", reviewed.Data["decision"]); Equal("sauce", reviewed.Data["item_id"]);
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
