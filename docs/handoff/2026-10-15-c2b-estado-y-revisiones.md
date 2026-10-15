# C2b sin terminar: informes de las etapas y hallazgos de revisión pendientes (15-10-2026)

Rama `feat/c2b-vistas-de-gestion` (subida; punta `6955407`) sobre `develop` `8bb94a0`. Plan aprobado: sección «C2b» de `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md` (commit `ab3e940` de la rama). Versión `0.38.0-c2b` ya puesta en los csproj y en la PWA.

El flujo de agentes se paró el 15-10 porque el promotor necesitaba el equipo libre. Estaba así:
- **Hechas y confirmadas** las cinco etapas: Caja con piezas comunes, Catálogo, Oferta, Salas y Dispositivos (coherencia y versión). Cada agente dice haber pasado ClientChecks, los checks del WPF y la batería completa antes de su commit (ver sus informes abajo). **No se repitió la batería sobre la punta**: hacerlo al retomar.
- **Revisiones**: dos de tres terminaron: **aspecto** (revisión 1) y **comportamiento** (revisión 2). Sus 19 hallazgos van abajo, **sin aplicar**. La tercera (textos, dinero y pruebas: que ningún assert se haya debilitado y que no cambie ninguna llamada a `ApiClient`) **no terminó**: hay que hacerla.
- **Sin hacer**: aplicar los hallazgos; el **arreglo del cierre al guardar** (ver `docs/handoff/2026-10-15-c2b-y-carta-bodega.md`); batería y checks del WPF en primer plano; `docs/native/C2b-….md` con capturas en `docs/native/images/c2b/`; párrafos de `dotnet/AGENTS.md` y `docs/STATUS.md`; PR, CI leída, merge e instalador. **El promotor ya dio permiso** para subir y mezclar C2b en cuanto la CI esté verde («Adelante», 15-10).

Los informes y hallazgos siguientes son la salida literal de los agentes (en inglés).

### Etapa 1: Caja y piezas comunes

Commits: `dcfb7aa feat(desktop): C2b piezas comunes de lista y ficha y Caja con cuentas en tarjetas`

Pruebas (informe del agente):

> All runs were in the foreground, one at a time, on the final tree.
> - Client checks: 123 passed, 0 failed.
> - Desktop checks (sh tools/agent/local-tests.sh desktop): "WPF startup and viewmodel checks passed".
> - Full battery (sh tools/agent/local-tests.sh), run again after the last code change:
>   - build: 0 warnings, 0 errors;
>   - domain: 81/81;
>   - client checks: 123;
>   - HTTP: all 14 suites OK — packaging 14 tests (1 skipped), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 (1 skipped);
>   - realtime: 12/12.
> - I checked that the new clipped-text check really works: I put back a header note that was being cut, and the checks failed with "Caja clips text: Registro operativo…". After the fix they pass.
> - Screenshots reviewed by eye in dotnet/artifacts/desktop/: checkout.png (Mesa 4 selected), checkout-empty.png, checkout-unselected.png, checkout-settled.png and checkout-credit.png. service.png matches the C2 reference.

Pendiente que dejó:

- Version not bumped yet (plan: 0.38.0-c2b), and there is no C2b doc, AGENTS.md paragraph or docs/native/images/c2b captures. I left these for the closing stage.
- Caja now makes N+2 reads per refresh: list, till catalogue, the chosen account, plus one read per other open account for its card totals (cards keep their last read when another is chosen). An additive amounts field on GET /checkout/accounts would remove this, but that is an engine change and out of scope for C2b.
- Existing behaviour, unchanged: Caja lists only open accounts, so after closing an account it leaves the list and the view picks the first one. 'Reabrir la cuenta' is therefore practically unreachable from the list; it is kept and tested at viewmodel level.
- Existing gap, out of scope: the server announces 'void-charge' but the WPF has no void button.
- Gotcha for later stages: a TextBlock placed as content of InfoBar, NoteBar or PageNote does not wrap. The implicit style in the bar template's ContentPresenter.Resources does not reach it, so set TextWrapping="Wrap" explicitly. The header note was cut until I did; Clipped() catches this.
- Ready for the next stages and not used by Caja: PaneList, RowTitle and RowDetail (long virtualised lists such as the catalogue), PageAction (header create button), and the Clipped()/NestedScrolls() check helpers in DesktopChecks/Program.cs.
- Two extra screenshots, checkout-settled.png and checkout-credit.png, are saved only in dotnet/artifacts/desktop. Copying the chosen ones into docs is for the documentation stage.

### Etapa 2: Catálogo

Commits: `210bc53 feat(desktop): C2b Catalogo y tarifas con pestanas, lista y ficha`

Pruebas (informe del agente):

> All runs were in the foreground, one at a time, and the last ones ran on the final tree.
> - Client checks: 123 passed, 0 failed.
> - Desktop checks (sh tools/agent/local-tests.sh desktop): "WPF startup and viewmodel checks passed". Costina.Desktop and DesktopChecks rebuilt with --no-incremental: 0 warnings, 0 errors.
> - Full battery (sh tools/agent/local-tests.sh):
>   - build: 0 warnings, 0 errors;
>   - domain: 81/81;
>   - client checks: 123;
>   - HTTP: all 14 suites OK — packaging 14 (1 skipped), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 (1 skipped);
>   - realtime: 12/12.
> - Screenshots reviewed by eye in dotnet/artifacts/desktop/:
>   - catalog.png (Albariño selected: photo, data, General and Terraza prices);
>   - catalog-price.png (the price form in view after clicking Copa);
>   - catalog-categories.png (Tintos);
>   - catalog-tariffs.png (Terraza);
>   - catalog-taxes.png (IVA 21 %);
>   - catalog-empty.png (empty catalogue).
>   - checkout.png and the other existing captures are unchanged.

Pendiente que dejó:

- Side effect on your real installation: the desktop checks were already running App.OnStartup on their first dispatcher pump, before this change. Your real Costiña (C:\Program Files\Costina\desktop, started 11:36) holds the single-instance mutex, so each of my early check runs brought its window to the front and shut the checks' own application down. That happened about six times while I tracked it down; nothing was read or written in the real installation. When no real Costiña is running, OnStartup instead opened a second window that booted by itself. Fixed with App.Checks. Any future check or tool that does `new App()` must set App.Checks=true first; this deserves a line in AGENTS.md in the closing stage.
- Lesson for the Oferta and Salas stages: a ComboBox or ListBox whose ItemsSource is rebuilt loses its selection, and does not get it back when the bound value stays equal, because WPF does not notify an equal value. Records compare by value, so re-assigning an equal DTO is also silent. Use stable lists that are rebuilt only when their content changes, then re-apply the selection (set it empty, then back), as CatalogViewModel.RefreshChoices and Render do. OffersViewModel and OrganizationViewModel probably have the same empty-dropdown symptom.
- Deliberate behaviour changes, each covered by checks: - the category filter includes subcategories (before, exact match only); - the product filter no longer follows the category chosen in the Categorías tab; - search ignores accents; - starting a new item clears the list selection, and a confirmed create is selected after the re-read; - Desactivar and Reactivar are separate, explicitly labelled buttons, and General has none; - the station 'none' label is now 'No pasa por cocina'; - the new-product default category is the active filter category, if that category is active, instead of the Categorías-tab selection.
- At 1280 × 860 the presentation editor with 'Fijar precio' starts below the presentations table, inside the single ficha scroll. Clicking a presentation or '＋ Presentación' brings it into view; this is presentation-only code-behind in CatalogView.xaml.cs. Choosing another product scrolls its ficha back to the top; a re-read of the same product does not. Keyboard selection does not scroll.
- Left for the closing stage: no version bump (plan 0.38.0-c2b), and no C2b doc or AGENTS paragraph. The new captures are only in dotnet/artifacts/desktop: catalog, catalog-categories, catalog-tariffs, catalog-taxes, catalog-price and catalog-empty.
- In one review capture (catalog-price.png), the filter dropdown's arrow is caught mid-animation because the checks open and close that dropdown just before. The final catalog.png is fine. Re-open or wait before capturing if this ever matters.
- The FakeServer in DesktopChecks now applies tariff-create in memory, so a created tariff shows up on the re-read.

### Etapa 3: Oferta

Commits: `1e26d74 feat(desktop): C2b Oferta con lista, chips de tipo, pases numerados y platos desplegados`

Pruebas (informe del agente):

> All runs were in the foreground, one at a time. The last ones ran on the final tree, after the last code change.
> - Client checks: 123 passed, 0 failed.
> - Desktop checks (sh tools/agent/local-tests.sh desktop): "WPF startup and viewmodel checks passed". Costina.Desktop and DesktopChecks rebuilt with --no-incremental: 0 warnings, 0 errors.
> - Full battery (sh tools/agent/local-tests.sh):
>   - build: 0 warnings, 0 errors;
>   - domain: 81/81;
>   - client checks: 123;
>   - HTTP: all 14 suites OK — packaging 14 (1 skipped), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 (1 skipped);
>   - realtime: 12/12.
> - Screenshots reviewed by eye in dotnet/artifacts/desktop/:
>   - offers.png (Degustación Costiña: 6 numbered courses, Pescado unfolded with the vieiras sauce at Pase);
>   - offers-menu.png (top of the Menú del día ficha: data, Monday to Friday buttons, menu-product note, start of the courses);
>   - offers-dish.png (the hake ficha with its station shown by name);
>   - offers-carta.png (Carta, Postres unfolded, withdrawn cheesecake, add row);
>   - offers-new.png, offers-empty.png and offers-unselected.png.
>   - catalog*.png and the other existing captures are unchanged.

Pendiente que dejó:

- Left for the closing stage, as for the earlier stages: no version bump (plan 0.38.0-c2b), no C2b doc or AGENTS.md paragraph, and the captures are only in dotnet/artifacts/desktop. The offers ones are offers, offers-menu, offers-dish, offers-carta, offers-new, offers-empty and offers-unselected.
- Worth an AGENTS.md line in the closing stage: list searches must use ViewModels/TextSearch (invariant culture). With CurrentCulture on a Spanish PC, ñ is its own letter.
- Also for AGENTS.md: TextBlock.Text is empty when the text is built with Run inlines, so Texts()/Painted() in the checks cannot see it. Use one bound Text instead (OfferItemRow.Label). The new Painted() helper only returns visible text: a hidden chip's word does not count.
- Lesson for the Salas stage: rows rebuilt from records need the 'set empty, then the value' pattern on the bound selection. Dropdown lists must be stable: StationChoices was rebuilt on every Sync before, which is why the old offers.png showed an empty station box. OrganizationViewModel probably has the same symptom.
- Deliberate behaviour changes, each covered by checks: - starting an offer, course or dish clears that selection, and Cancelar restores the previous one; - a confirmed create is selected after the re-read; creating an offer clears the search and the type chip; - the first offer and course are still auto-selected on read, as before; - type chip counts follow the search; - the course code shows read-only in 'Datos del pase'; - the new-offer default 'orden' is the total number of offers, not the filtered count.
- Existing limits, unchanged: - the carta add row lists every till sellable, drinks included; the server rejects one without a station with station_required; - the dish product is still typed as a code, because the engine accepts any active product and the view only reads the till list; the hint line names it when it is a sellable; - a menu product outside the till list (always the case for the Menús category) is shown by its code.

### Etapa 4: Salas

Commits: `061ae23 feat(desktop): C2b Salas, mesas y estaciones con pestanas, salas, mesas en tarjetas y ficha`

Pruebas (informe del agente):

> All runs were in the foreground, one at a time, on the final tree.
> - Client checks: 123 passed, 0 failed.
> - Desktop checks (sh tools/agent/local-tests.sh desktop): "WPF startup and viewmodel checks passed". Costina.Desktop and DesktopChecks rebuilt with --no-incremental: 0 warnings, 0 errors.
> - Full battery (sh tools/agent/local-tests.sh):
>   - build: 0 warnings, 0 errors;
>   - domain: 81/81;
>   - client checks: 123;
>   - HTTP: all 14 suites OK — packaging 14 (1 skipped), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 (1 skipped);
>   - realtime: 12/12.
> - Before writing the new checks, I ran the existing desktop checks against the new view and viewmodel; they all passed unchanged, including the E2 organization checks and the E3.2 tariff-per-room checks.
> - Screenshots reviewed by eye in dotnet/artifacts/desktop/:
>   - configuration.png: Sala principal and its ficha; 9 table cards, Mesa 9 dashed;
>   - configuration-table.png: Mesa 4 chosen, its ficha and "Ver la sala";
>   - configuration-stations.png: Cocina caliente chosen; Pase marked "Reservada";
>   - configuration-empty.png: no rooms;
>   - configuration-inactive.png: the deactivated Comedor privado and its dashed table; the room dropdown marks it "(desactivada)".
>   - The other captures are unchanged.

Pendiente que dejó:

- Left for the closing stage, as for the earlier stages: no version bump (plan 0.38.0-c2b), no C2b doc or AGENTS.md paragraph, and the captures are only in dotnet/artifacts/desktop. The Salas ones are configuration, configuration-table, configuration-stations, configuration-empty and configuration-inactive.
- Worth an AGENTS.md line in the closing stage: PreviewMouseLeftButtonUp is a DIRECT routed event that WPF re-raises element by element. A check that raises it on a ListBoxItem never reaches the ListBox handler; raise PreviewMouseUpEvent instead (the left-button version is derived from it on every element of the route). The Salas check does this.
- Deliberate behaviour changes, each covered by checks: - starting a new room, table or station clears that selection, and Cancelar restores the previous one; - created items are chosen after the re-read; - after a re-read, the view follows a table that a save moved to another room; - with no previous choice, the first ACTIVE room is chosen (before: simply the first); - the summary adds plazas, and says 'Aún no hay salas: crea la primera con «Nueva sala».' when there are none; - the table ficha shows either the room or the table, never both forms at once; - the separate Desactivar and Reactivar buttons replace the old 'Desactivar / reactivar'.
- Existing behaviour kept on purpose: the Tipo dropdown stays enabled for the Pase station. The UI does not add a local rule; the server rejects a kind change with reserved_station, and the ficha note explains it. Deactivating Pase is still disabled by the existing CanToggleStation rule.
- The station kind help texts are descriptive copies of the server's device/station fit rule (A2): kitchen screens on kitchen, pass or bar; comanderos on room or bar. If that rule changes, update StationKindHelp in OrganizationViewModel.
- The FakeServer now applies organization commands in memory for every section. The E3.2 zone-tariff section, which ran before this one, also modifies server.Organization; its assertions are unaffected, and later sections reassign server.Organization explicitly.

### Etapa 5: Dispositivos, coherencia y versión

Commits: `6955407 feat(desktop): C2b Dispositivos con cabecera y tarjetas, repaso de coherencia y version 0.38.0-c2b`

Pruebas (informe del agente):

> All runs were in the foreground, one at a time, on the final tree.
> - Client checks: 123 passed, 0 failed.
> - Desktop checks (sh tools/agent/local-tests.sh desktop): "WPF startup and viewmodel checks passed". A --no-incremental rebuild of Costina.Desktop and DesktopChecks gave 0 warnings and 0 errors.
> - Full battery (sh tools/agent/local-tests.sh), run again after the last change:
>   - build: 0 warnings, 0 errors;
>   - domain: 81/81;
>   - client checks: 123;
>   - HTTP: all 14 suites OK — packaging 14 (1 skipped), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 (1 skipped);
>   - realtime: 12/12.
> - I looked at every PNG in dotnet/artifacts/desktop:
>   - devices views: devices, devices-empty, devices-assistant, devices-confirm, devices-removed, devices-legacy, devices-choose, devices-simple-off;
>   - checkout views: checkout, -empty, -unselected, -settled, -credit (now with a 360 px list);
>   - all catalog-*, offers-* and configuration-* views;
>   - sidebar-compact plus the new sidebar-compact-checkout, -configuration and -devices;
>   - service, home, startup.
>   The status bar shows "Costiña 0.38.0-c2b".

Pendiente que dejó:

- Left for the closing stage: the C2b document, the AGENTS.md paragraph, STATUS, and copying the chosen captures to docs/native/images/c2b. New captures from this stage, in dotnet/artifacts/desktop only: - devices.png and devices-empty.png; - devices-assistant, devices-confirm, devices-removed and devices-legacy (.png); - sidebar-compact-checkout, sidebar-compact-configuration and sidebar-compact-devices (.png).
- Worth recording in AGENTS.md at closing, with the earlier stages' notes: - ListWidth and CardRowTitle now live in Components.xaml; new list-and-ficha views should use them. - DesktopChecks has SameLayout (header position, list width and ficha start, unfolded and folded), DevicesFit and DeviceCard. DeviceCard scrolls the virtualised CardRows first; a container off screen is null. - The FakeServer applies a device revoke in memory.
- Deliberate behaviour changes in Dispositivos, each covered by checks: - no group headers; rows are sorted by kind then name, as on Inicio (before: by station code, then name); - the old 'Dispositivos antiguos con rol principal' group header is now a warning bar; - the 'Mostrar también los quitados' checkbox shows only when something was removed, with the count; - the remove button names the chosen device and is hidden while the confirmation bar is up; - the empty-list button reuses StartWaiterCommand with the room station chosen on the left, and its label names that station; - the old subtitle under the title is replaced by the summary.
- Device rows have three lines (name; kind and station; approver and date), not the plan's two. The approver and date had to be visible. The 'Este PC' chip is only checked through the existing IsThisPc logic, not visually: the captures run as user:jefa.
- With no room station, the empty device list shows no button; the left card already offers 'Crear la estación «Sala»'. The explanation text does not say that a station is needed first.
- Consistency items left as they were on purpose: - Comedor's header margin (12 against PageBar's 14) puts its tabs 2 px higher; C2 is not to be redesigned, and the titles do sit at the same place, which is checked. - Oferta keeps 'Guardar oferta' rather than 'Guardar cambios', because its ficha also has 'Guardar pase' and 'Guardar plato'. - Caja's account-card title keeps its own inline single-line style, which looks the same as CardRowTitle.
- The version bump only touches the csproj and package files, as earlier bumps did. The PWA build output is not rebuilt in this commit.

### Revisión 1 (sin aplicar)

Veredicto: C2b passes visual review with fixes; nothing blocks the merge. I looked at every PNG in dotnet/artifacts/desktop (checkout*, catalog*, offers*, configuration*, devices*, sidebar-compact-*, home, service) against docs/native/images/c2 and the Comedor mockup. service.png and home.png match the C2 reference. The five management views are coherent: page header on grey, 360 px list cards, a ficha with context, title and a word chip, one scroll per column, and big actions pinned at the bottom.  I found no clipped text, no nested scrollbars, no English or raw state values, and no ficha showing fields with nothing selected. Every empty list explains what is missing and offers its create button.  **Major (2):** - No list scrolls its selected row into view when the selection comes from the viewmodel. Ver cuenta, confirmed creates and Ver todos leave the chosen card off screen; checkout-credit.png shows Terraza 1 in the ficha and no marked card in the list. - A voided Caja line keeps its amount in bold like the counted lines, and 'Consumos · 3 líneas' sits above a 4-row table.  **Minor (polish a demanding owner would notice):** - Deactivated rows are dimmed with opacity, giving 3.0:1 contrast on 12 px text. - A deactivated table card copies Comedor's dashed 'Libre' look. - Offer rows break after a dangling '·'. - The 'Desactivada' chip wraps room names in the 250 px column. - Side-by-side empty states sit 24 to 38 px apart; Caja also keeps its search box on an empty list. - The overpaid account still has 'Registrar pago' as its big green action. - Wording: 'desactivado' against 'Retirado' in carta courses, straight quotes against «» in Dispositivos, and a comma separator in the Categorías count. - The Salas tabs have no counts. - Date fields are ISO next to dd/MM/yyyy displays (this predates C2b). - The menu product appears as «menu-dia» in the Oferta ficha. - catalog-taxes.png shows 'Tarifas · 4' against 3 in catalog-tariffs.png.  Each fix is presentation or wording only. None changes a command, body or CanExecute.

- **[major] dotnet/src/Costina.Desktop/Themes/Components.xaml:459-467 (PaneList, inherited by CardRows); new attached property in Ui.cs** — No list scrolls its selected row into view when the selection is set by the viewmodel rather than by a click. This covers Ver cuenta from Comedor (Checkout.Request), a create confirmed by the server, Ver todos / clearing a search, and following a table moved to another room. The ficha shows one item while the list shows a page where nothing is marked. No view calls ScrollIntoView: grep finds only BringIntoView inside fichas.
  - Evidencia: checkout-credit.png: the ficha shows Terraza 1 (Pagado de más), but the left list shows Mesa 1 to Mesa 8 and no card has the accent border. Terraza 1 is the 7th card, below the fold at 1280x860. The capture comes from Choose("st1") (Program.cs:1213), which is the same programmatic path that Ver cuenta uses. The same root cause hits Catálogo: 'Ver todos' keeps the selected product by id, but rebuilding ItemsSource resets the offset to the top.
  - Arreglo: Add a presentation-only attached behaviour in Ui.cs and switch it on in the shared list style, so every PaneList and CardRows list gets it.

Ui.cs:
public static readonly DependencyProperty KeepSelectionInViewProperty = DependencyProperty.RegisterAttached("KeepSelectionInView", typeof(bool), typeof(Ui), new PropertyMetadata(false, (d, e) => { if (d is not ListBox l) return; l.SelectionChanged -= Reveal; if ((bool)e.NewValue) l.SelectionChanged += Reveal; }));
public static bool GetKeepSelectionInView(DependencyObject o) => (bool)o.GetValue(KeepSelectionInViewProperty);
public static void SetKeepSelectionInView(DependencyObject o, bool v) => o.SetValue(KeepSelectionInViewProperty, v);
private static void Reveal(object s, SelectionChangedEventArgs e) { if (s is not ListBox { SelectedItem: { } item } l || !ReferenceEquals(e.OriginalSource, l)) return; l.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => { if (ReferenceEquals(l.SelectedItem, item) && l.IsVisible) l.ScrollIntoView(item); }); }

Keep the OriginalSource guard: SelectionChanged bubbles up from ComboBoxes and lists inside item templates.

Components.xaml, PaneList style: <Setter Property="local:Ui.KeepSelectionInView" Value="True"/>

DesktopChecks: after Choose("st1") and after Ver cuenta, assert that the selected ListBoxItem's bounds lie inside AccountList's viewport.
- **[major] dotnet/src/Costina.Desktop/Views/CheckoutView.xaml:174-175 (also CheckoutViewModel.cs:329)** — In Caja, a voided charge line strikes through only its description. Its quantity and amount are drawn exactly like the lines that count, with the amount in bold black. The section title then counts only the active lines, so it disagrees with the rows in the table. On the till screen this reads as if the voided euros were still charged.
  - Evidencia: checkout.png and sidebar-compact-checkout.png show 'Café solo · Unidad' struck through and the amount '1,50 €' in SemiBold Ink, the same weight as 74,00, 12,00 and 5,00. The total is 91,00 €, which excludes it. The title above says 'Consumos · 3 líneas' but the table has 4 rows.
  - Arreglo: In UserControl.Resources, define <Style x:Key="ChargeText" TargetType="TextBlock"><Style.Triggers><DataTrigger Binding="{Binding Voided}" Value="True"><Setter Property="TextDecorations" Value="Strikethrough"/><Setter Property="Foreground" Value="{StaticResource InkSoft}"/><Setter Property="FontWeight" Value="Normal"/></DataTrigger></Style.Triggers></Style> and a ChargeAmount style BasedOn it that adds <Setter Property="FontWeight" Value="SemiBold"/>.

Apply ChargeText to the description (line 165) and the quantity (line 174), and ChargeAmount to the amount (line 175). Remove the local FontWeight="SemiBold" from line 175, because a local value would override the trigger.

CheckoutViewModel.cs:329: LinesTitle = "Consumos · " + Labels.Plural(active, "línea", "líneas") + (voided > 0 ? " · " + Labels.Plural(voided, "anulada", "anuladas") : "");
- **[minor] dotnet/src/Costina.Desktop/Views/OrganizationView.xaml:59-61 (same trigger at CatalogView.xaml:82,307,409,507 and OffersView.xaml:30,104,252,311)** — Deactivated rows are dimmed with Opacity 0.7, which also fades their 12 px detail text and their chip. The result is below 4.5:1. Dispositivos dims removed devices differently: the title turns InkSoft and the detail stays readable. The two methods are inconsistent.
  - Evidencia: Darkest pixel measured on the captures: 'Degustación · Cena · 3 pases' (offers.png) is #898E96 on #F5F6F8, 3.05:1. 'Ninguna sala · 0 precios vigentes' (catalog-tariffs.png) and '0 % · 0 productos' (catalog-taxes.png) are also 3.05:1. 'Sin mesas activas' (configuration-inactive.png) is 3.17:1. The withdrawn carta item's station 'Cocina fría' (offers-carta.png) is #838C92 on #E2EFEC, 2.90:1.
  - Arreglo: In every trigger listed, replace <Setter Property="Opacity" Value="0.7"/> with <Setter Property="TextElement.Foreground" Value="{StaticResource InkSoft}"/>.

The title, CardRowTitle or RowTitle, has no Foreground of its own, so it inherits InkSoft. RowDetail already is InkSoft, which is about 5.7:1 on Surface2. ToneChip keeps its own TextElement.Foreground, so the 'Desactivada' chip stays readable.

This matches the 'Removed' DataTrigger in DevicesView.xaml:190-191.
- **[minor] dotnet/src/Costina.Desktop/Views/OrganizationView.xaml:40, 44-47** — A deactivated table card in Salas is drawn with the same dashed treatment that Comedor uses for a free table: transparent fill, dashed StrokeStrong 1.5 border, dash array 3 2.5. The same visual means 'Libre' in Comedor and 'Desactivada' in Salas, two views apart in the sidebar. The word chip prevents a colour-only state, but the visual language contradicts C2.
  - Evidencia: In configuration.png and sidebar-compact-configuration.png, Mesa 9 (Desactivada) is a dashed, transparent card. In service.png, Mesa 2 and Mesa 5 (Libre) have exactly the same dashed, transparent look. ServiceView.xaml:23,28 and OrganizationView.xaml:40,45-46 use the same Rectangle.
  - Arreglo: Remove the Dash Rectangle (line 40) and replace the Inactive trigger with:
<DataTrigger Binding="{Binding Inactive}" Value="True"><Setter TargetName="Bd" Property="Background" Value="{StaticResource Surface2}"/><Setter TargetName="Bd" Property="TextElement.Foreground" Value="{StaticResource InkSoft}"/></DataTrigger>
This gives a solid grey card with a grey name, like the other deactivated rows, and keeps the 'Desactivada' chip. Dashed then stays reserved for 'Libre'.

Update the comments at lines 7 and 12, and change the DesktopChecks assertion 'dashed only for Mesa 9' to check the grey card.
- **[minor] dotnet/src/Costina.Desktop/Views/OffersView.xaml:112 (OffersViewModel.cs:389)** — An offer row's detail is a single wrapped string. When the date range does not fit, the line breaks after the separator, leaving a dangling '·' at the end of the first line. Rows also end up with different heights: one detail line for 'sin fechas', two for dated offers.
  - Evidencia: offers.png (crop): 'Menú cerrado · Comida · 3 pases ·' / 'del 01/09/2026 al 31/12/2026' and 'Degustación · Cena · 3 pases ·' / 'del 15/06/2026 al 15/09/2026'. The 'Degustación Costiña' and 'Carta' rows have a single detail line.
  - Arreglo: Give the validity its own line in every row.

OffersViewModel.cs:389: OfferDetail returns only $"{kind} · {service} · {n pases}". Add a Validity property to OfferRow holding Validity(o.ValidFrom, o.ValidTo) with its first letter capitalised ('Sin fechas', 'Del 01/09/2026 al 31/12/2026').

OffersView.xaml:112: replace the line with
<TextBlock Text="{Binding Detail}" Style="{StaticResource RowDetail}" Margin="0,2,0,0"/>
<TextBlock Text="{Binding Validity}" Style="{StaticResource RowDetail}"/>

Update the row-text assertions.
- **[minor] dotnet/src/Costina.Desktop/Views/OrganizationView.xaml:101-110** — In the 250 px Salas column, the 'Desactivada' chip shares the title line, so an ordinary room name wraps onto two lines. The room's other chip (its tariff) already sits on its own line below the detail, so the two chips are placed inconsistently.
  - Evidencia: configuration-inactive.png (crop): 'Comedor / privado' breaks over two lines beside the 'Desactivada' chip. Active rooms such as 'Sala principal' stay on one line, and their 'Tarifa Terraza' chip sits on a third line.
  - Arreglo: Drop the DockPanel and the right-docked chip. Keep <TextBlock Text="{Binding Zone.Name}" Style="{StaticResource CardRowTitle}"/> and the Detail line, then add one chip row:
<WrapPanel>
 <Border Style="{StaticResource ToneChip}" Margin="0,8,6,1" Visibility="{Binding Inactive, Converter={StaticResource Bool2Vis}}"><TextBlock Text="Desactivada"/></Border>
 <Border Style="{StaticResource ToneChip}" Tag="info" Margin="0,8,6,1" Visibility="{Binding Tariff, Converter={StaticResource Text2Vis}}"><TextBlock Text="{Binding Tariff}"/></Border>
</WrapPanel>
- **[minor] dotnet/src/Costina.Desktop/Views/CheckoutView.xaml:34, 38, 73 (and the ficha EmptyStates of CatalogView, OffersView, OrganizationView)** — Side-by-side empty states sit at different heights. A list's empty block is centred together with its 52 px create button and a 24 px bottom margin, while the ficha's block is centred alone. These are the screens of a fresh installation. Caja also keeps its search box on an empty list, while Catálogo and Oferta hide their list tools.
  - Evidencia: Icon tops, list vs ficha: checkout-empty.png 372 vs 396; catalog-empty.png 381 vs 419; offers-empty.png 358 vs 396. configuration-empty.png steps down across three columns: headings at y = 449, 474 and 481. checkout-empty.png shows 'Buscar mesa o sala' above 'No hay cuentas abiertas'.
  - Arreglo: (a) In Components.xaml add <Style x:Key="FichaEmpty" TargetType="ContentControl" BasedOn="{StaticResource EmptyState}"><Setter Property="VerticalAlignment" Value="Center"/><Setter Property="Margin" Value="0,0,0,76"/></Style>. 76 = 24 (list margin) + 14 + 38 (button). Use it for every 'nothing chosen' ficha EmptyState (CheckoutView:73, CatalogView:330 and its siblings, OffersView:125, the Organization fichas and the centre column). The icons then line up once the list tools are hidden.

(b) CheckoutView.xaml:34: add Visibility="{Binding Checkout.ShowListTools, Converter={StaticResource Bool2Vis}}" with public bool ShowListTools => Accounts.Length > 0 || Search.Length > 0; in CheckoutViewModel. Raise it in Compose, as OffersViewModel:411 does.
- **[minor] dotnet/src/Costina.Desktop/Views/CheckoutView.xaml:124-129** — On an overpaid account, the biggest, green action of the ficha is 'Registrar pago de prueba', which takes even more money. At the same time, Pendiente reads 0,00 € and the warning bar says to refund in 'Otras operaciones'. The visual hierarchy contradicts what the screen is telling the cashier.
  - Evidencia: checkout-credit.png: under 'Pagado de más: 5,80 €. Devuélvelo en «Otras operaciones»', the full-width green BigAction reads 'Registrar pago de prueba', and 'Todo lo pendiente' is disabled because nothing is pending.
  - Arreglo: No CanExecute change. Add a VM flag HasCredit => CreditText.Length > 0, then in credit state:
- hide PayButton (Visibility: !CloseAnnounced && !HasCredit);
- show PayAgainButton (Visibility: CloseAnnounced || HasCredit);
- in the BigAction slot, show a secondary-styled big button 'Devolver lo pagado de más…'. Its Click (code-behind, presentation only, like OffersView.Show) runs OtherOperations.BringIntoView() and NoteBox.Focus(). The refund itself stays the existing RefundButton with its reason.
- **[minor] dotnet/src/Costina.Desktop/ViewModels/OffersViewModel.cs:404** — In a carta, a course row counts withdrawn items as 'desactivado'. The same item is shown with the chip 'Retirado', and its buttons are 'Retirar de la carta' / 'Volver a la carta'. Two words for one state.
  - Evidencia: offers-carta.png: the Postres row says '1 plato · 1 desactivado', and the table below shows 'Tarta de queso · Ración [Retirado]'.
  - Arreglo: return Labels.Plural(active, "plato", "platos") + (inactive > 0 ? " · " + (carte ? Labels.Plural(inactive, "retirado", "retirados") : Labels.Plural(inactive, "desactivado", "desactivados")) : "");
- **[minor] dotnet/src/Costina.Desktop/ViewModels/OrganizationViewModel.cs:81** — The Salas tabs have no counts. Every other Segmented tab set carries one: Comedor 'Sala principal · 8', Catálogo 'Productos · 18'.
  - Evidencia: configuration.png shows 'Salas y mesas' | 'Estaciones'; catalog.png shows 'Productos · 18' | 'Categorías · 16' | 'Tarifas · 3' | 'Impuestos · 4'.
  - Arreglo: When rendering the organization, set the labels as CatalogViewModel.cs:442 does: Sections[0].Label = "Salas y mesas · " + zones.Length; Sections[1].Label = "Estaciones · " + stations.Length;. Before the first read, use the bare labels.
- **[minor] dotnet/src/Costina.Desktop/ViewModels/DevicesViewModel.cs:126 (also 109, 209, 423, 522, 532, 563)** — Device names are quoted with straight ASCII quotes in the confirmation bar and status texts. The new C2b button, and the rest of the app ('«Nuevo producto»', '«Otras operaciones»'), use «». The quote style changes at the moment the button turns into its confirmation.
  - Evidencia: devices.png: the button reads 'Quitar «tablet-sala-1»'. devices-confirm.png: the bar reads '¿Quitar "tablet-terraza"? Dejará de funcionar…'.
  - Arreglo: Replace \"{…}\" with «{…}» in these strings. For example, line 126: $"¿Quitar «{RemovalCandidate.Device.Name}»? Dejará de funcionar en el acto y habrá que conectarlo de nuevo…". Update any DesktopChecks assertion that matches the quotes.
- **[minor] dotnet/src/Costina.Desktop/Views/CatalogView.xaml:269 (OffersView.xaml:188-189)** — Date inputs are ISO (AAAA-MM-DD) while every date the same fichas display is dd/MM/yyyy, and C2b now puts both side by side. This predates C2b (E3/E4a), but the new layout makes it obvious.
  - Evidencia: catalog-price.png: 'Desde (AAAA-MM-DD) 2026-10-15' right above 'Programado: Terraza 5,50 € desde el 01/11/2026'. offers-menu.png: the fields show '2026-09-01' / '2026-12-31' while the list row next to them says 'del 01/09/2026 al 31/12/2026'.
  - Arreglo: Labels become 'Desde (dd/mm/aaaa)' / 'Hasta (dd/mm/aaaa)'. In CatalogViewModel (default PriceValidFrom and the parse at :978) and OffersViewModel (:474), show dd/MM/yyyy and parse with DateOnly.TryParseExact(text, ["dd/MM/yyyy", "yyyy-MM-dd"], InvariantCulture…), then format to yyyy-MM-dd for the command. The command body stays byte-identical, so the existing body assertions still hold.
- **[minor] dotnet/src/Costina.Desktop/ViewModels/OffersViewModel.cs:153-156** — The Menú del día ficha shows the internal product code in a sentence. The plan's vocabulary rule says the code appears only in the «Código» field. The name is looked up only in the till list (checkout/catalog), which never contains the Menús category.
  - Evidencia: offers-menu.png: 'Se vende por persona con su producto-menú del catálogo (código «menu-dia»): su precio se fija en ERP › Catálogo y tarifas.' The ERP catalog in the same sample names that product 'Menú del día' (Program.cs:1305).
  - Arreglo: Look the name up in the already-read ERP catalog before falling back to the code. Expose a lookup on CatalogViewModel (for example public string? ProductName(string id) => catalog?.Products.FirstOrDefault(p => p.Id == id)?.Name;) and use shell.Catalog.ProductName(p) ?? ItemChoices…Name. Keep the code only when neither knows it. No new request is needed when Catálogo has been read.
- **[minor] dotnet/tests/Costina.DesktopChecks/Program.cs:1540-1552** — The tariff and tax captures disagree with each other because the test tariff 'Barra' is created between them. Copied into docs/native/images/c2b as planned, the set would be incoherent.
  - Evidencia: catalog-tariffs.png shows 'Tarifas · 3'. catalog-taxes.png, captured after tariff-create {"id":"barra"…}, shows 'Tarifas · 4'.
  - Arreglo: Capture catalog-taxes.png before the tariff-create check, or re-render the coherent wine catalogue (catalogView.Render(...)) just before Fits("catalog-taxes") / Save. Then all catalog-*.png show the same 3 tariffs.
- **[minor] dotnet/src/Costina.Desktop/ViewModels/CatalogViewModel.cs:720** — The Categorías count line separates its parts with a comma. Every other count and summary line in C2b uses ' · '.
  - Evidencia: catalog-categories.png: '16 categorías, 1 desactivada'. Compare '18 productos · precio General', '3 ofertas activas · 1 desactivada' and '8 mesas activas · 26 plazas · 1 desactivada'.
  - Arreglo: CategoryCount = … + (off > 0 ? " · " + Labels.Plural(off, "desactivada", "desactivadas") : ""); and update its assertion.

### Revisión 2 (sin aplicar)

Veredicto: The texts, money and tests checks are mostly clean, with two major issues in Caja and two minor ones. I ran the client checks on a scratch copy of HEAD (dotnet/ only): 123 passed, 0 failed. No Costina.Client file changed.  What is clean: - **Versions:** consistent. 0.38.0-c2b in both csproj files and in the DesktopChecks fake diagnostics; 0.38.0 in package.json and in both package-lock.json fields; no stale versions outside docs. - **Command bodies and API calls:** I compared every Command/SendAsync/GetAsync call site in the viewmodels against ab3e940. All bodies, paths and CanExecute rules are the same; Command only gained a 'confirmed' callback that runs after the server confirms. The only new reads hit an existing endpoint: one GET checkout/services/{id} per open account for the card totals. - **Money:**   - always decimal, through Money.Format / CatalogViewModel.Money (es-ES "N2" with " €");   - right-aligned in the lines and price tables;   - 'Todo lo pendiente' writes "0.00" in es-ES with no thousands separator, so Money.Parse accepts it;   - no double/float arithmetic;   - the header total appears only when every card was read; an unread card says "Sin leer", never an invented amount;   - figures add up in the captures (Mesa 4: 91,00 total, 40,00 paid, 51,00 pending; Terraza 1: 5,80 overpaid; header 420,60 €).   - All money views are main-only (IsMain and RequiresResponsible), and Clear() empties them. - **Notice and labels:** the 'registro de ensayo' notice keeps its exact text, now in the Caja page header. Unknown values stay raw through Labels: the coverage tone falls back to neutral, an unknown station kind shows as «x», and an unknown device role shows raw. Stations, tariffs, categories, zones and tables appear by name. No allergen or restriction texts appear in these views. - **DesktopChecks and ClientChecks not weakened:** ClientChecks are unchanged. DesktopChecks removes 15 lines, each legitimately replaced:   - the category-tab filter became the CategoryFilter check, now including subcategories;   - the catalog row text follows the new row layout;   - the device Group checks became Kind and Summary checks, and still cover hiding removed devices;   - the version string was bumped;   - the hand-set Organization and Caja capture data gave way to the coherent read state, plus new capture-loop assertions;   - the fake server is richer and keeps its old fallback.   The new GET commands/{key} fake (always 'not found') changes no earlier check, because none relied on reconcile.  The real problems: 1. **Caja card order (major).** The Caja cards and the default account follow the server's ORDER BY service_id, which is a random GUID. In a real installation the cards come out in random table order. The checks' s1..st1 ids hide this. 2. **Stale payment amount (major).** The amount filled by 'Todo lo pendiente' stays in the box after choosing another account, so it can be paid or refunded against the wrong account. 3. **Bar text not wrapping (minor).** The CreditBar and ClosedBar TextBlocks lack TextWrapping="Wrap"; the closed-account text would be cut off. 4. **Catalog row price (minor).** The row's 'Sin precio' looks only at the first presentation, which contradicts the view's own sellable rule.

- **[major] dotnet/src/Costina.Desktop/ViewModels/CheckoutViewModel.cs:272 (BuildRows), 176 (default pick in Load)** — In a real installation the new Caja card list comes out in random table order. The cards and the account picked by default follow the server's order, which is ORDER BY service_id. Service ids are random GUIDs. The DesktopChecks sample hides this because its ids (s1, s3, s4, s6, s7, s8, st1) happen to sort in table order.
  - Evidencia: BuildRows: `AccountItems = Accounts.Where(a => Matches(a, text)).Select(Row).ToArray();` does no ordering. Load: `target = ... choices.FirstOrDefault(c => Matches(c, Search.Trim()))?.ServiceId`. Server: DesktopReadRepository.cs:24 `SELECT service_id,table_id,state FROM core.accounts ... AND state='Open' ORDER BY service_id`. DiningOperations.cs:25 `var id = Guid.NewGuid().ToString("N");`. DesktopChecks Program.cs:740-766 seats the services as s1..st1, and the fake server returns Accounts in board (table) order. So checkout.png shows Mesa 1, 3, 4, 6, 7, 8, Terraza 1, while a real server would give an arbitrary mix such as Mesa 7, Terraza 1, Mesa 1, and the default account would be a random table.
  - Arreglo: Order the cards in the view only, with no engine change. In BuildRows, sort Accounts the way the Comedor plan does: by the index of TileOf(choice) in shell.Service.Tiles (zone order, then table sort), then by shell.Names.Table(choice.TableId) as a fallback. In Load, use the same ordered sequence for the default `FirstOrDefault` pick, so the auto-chosen account is the first card shown. In DesktopChecks, give the accounts GUID-like service ids whose ordinal order differs from table order (or have the fake checkout/accounts sort by service id, as the real server does). Then assert that the card order is Mesa 1, 3, 4, 6, 7, 8, Terraza 1 and that the default pick is the first card.
- **[major] dotnet/src/Costina.Desktop/ViewModels/CheckoutViewModel.cs:235-242 (OnSelectedAccountChanged), 219-233 (Render), 369-372 (FillPending)** — The new 'Todo lo pendiente' helper fills the amount box from the pending total of the account being read. That amount stays in the box after the user chooses another card. A refresh can also auto-select another account: for example, a search hides the chosen one and the next read picks the first match. Either way, 'Registrar pago de prueba' (and 'Devolver lo pagado de más', which uses the same box) would then post the first account's figure against a different account. This is money that is not the selected account's: the spirit of F01.
  - Evidencia: FillPending: `Amount = (context.Data.BalanceCents / 100m).ToString("0.00", Spanish);`. Nothing clears Amount when the selection changes: OnSelectedAccountChanged only runs Compose, sets accountServiceId, then Sync and LoadSelectionAsync. Render and Load do not touch Amount either; only Clear(), a successful Pay and a successful Refund reset it. DesktopChecks (Program.cs:1194-1202) only checks fill, then pay on the same account (Mesa 4, "51,00"). No check switches cards after filling.
  - Arreglo: Remember which service the amount was filled for (or simply which account was chosen when the user typed). In OnSelectedAccountChanged (when not rendering) and in Render, when accountServiceId differs from the previous one, set Amount = "" (and probably Note = ""). Add a DesktopChecks assertion: Mesa 4, press 'Todo lo pendiente' ("51,00"), click the Mesa 1 card; the amount box is empty and no POST was sent.
- **[minor] dotnet/src/Costina.Desktop/Views/CheckoutView.xaml:97-102** — The TextBlocks inside CreditBar and ClosedBar do not set TextWrapping="Wrap". The implementer's own C2b note says a TextBlock placed as the content of InfoBar, WarnBar or NoteBar does not pick up the template's implicit wrapping style. Every other bar TextBlock in the C2b views sets Wrap explicitly. ClosedText (about 98 characters) will be cut at 1280 x 860. No check covers this: ClosedBar is only checked at viewmodel level and is never laid out in a fit check.
  - Evidencia: `<ContentControl x:Name="CreditBar" Style="{StaticResource WarnBar}" ...><TextBlock Text="{Binding Checkout.CreditText}"/>` and the same for ClosedBar with ClosedText. In checkout-credit.png the 73-character credit text already takes about 460 px of the roughly 520 px text area. At the same rate, "Cuenta cerrada: no admite cambios. Para corregirla, reábrela con su motivo en «Otras operaciones»." needs about 615 px, so its end would be clipped. A grep of the bars in Checkout, Catalog, Offers, Organization and Devices finds only these two TextBlocks without Wrap.
  - Arreglo: Add TextWrapping="Wrap" to both TextBlocks, or bind the strings directly as Content so that the template's implicit style applies. Optionally lay out a closed-account ficha once in the Caja fit check, with Clipped().
- **[minor] dotnet/src/Costina.Desktop/ViewModels/CatalogViewModel.cs:ProductRowOf (diff hunk at the product row, `var price = active.Length == 0 ? null : Current(active[0], "general", today);`)** — The product row's price and its amber 'Sin precio' warning look only at the first active presentation. The view's own definition of sellable (the Sellable function used in the header summary and in the ficha's 'No se vende: sin precio General' chip) is: any active presentation with a General price. A product whose first presentation has no General price but whose second one does is therefore shown as 'Sin precio' in amber in the list, although the summary counts it as sellable and its ficha shows no warning.
  - Evidencia: `MainPrice = !p.Active ? "Desactivado" : price is null ? "Sin precio" : Money(price.PriceCents), PriceMissing = p.Active && price is null` against `private static bool Sellable(ProductDto product, string today) => product.Active && product.Presentations.Any(s => s.Active && Current(s, "general", today) is not null);`. The summary uses Sellable ("(16 vendibles hoy)"), and so does ProductWarnText.
  - Arreglo: Take the General price of the first active presentation that has one: `var price = active.Select(s => Current(s, "general", today)).FirstOrDefault(c => c is not null);`. Set PriceMissing = p.Active && !Sellable(p, today), so the row, the summary and the ficha agree. Add a DesktopChecks product whose first presentation has no General price and whose second does, and assert that its row shows the second presentation's price and no warning.

