using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Costina.Desktop;

internal static class Program
{
    // C1 (revision de seguridad): direccion de servidor de los checks: loopback en un puerto donde no escucha nada. El cierre de sesion
    // de mejor esfuerzo al salir del modo responsable (inactividad) no llega nunca a un motor, y menos al real de este PC (5088).
    private const string ChecksEndpoint="http://127.0.0.1:59997";

    [STAThread]
    private static int Main()
    {
        // C1 (revision de seguridad): ANTES de crear la ventana, la carpeta de datos de este equipo (desktop.json, credencial cifrada del
        // puesto, orden incierta) apunta a un temporal con su propio desktop.json. Los checks nunca leen ni escriben la instalacion real
        // (%LOCALAPPDATA%\Costina) ni conectan con su servidor; cada cambio de carpeta de abajo vuelve aqui, nunca a la real.
        var checksRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-desktop-checks-"+Guid.NewGuid().ToString("N"));
        DesktopSettings.Folder=checksRoot;
        new DesktopSettings{Endpoint=ChecksEndpoint}.Save();
        if(!DesktopSettings.Path.StartsWith(checksRoot,StringComparison.Ordinal)||!File.Exists(DesktopSettings.Path)) throw new Exception("The checks must keep their settings in their own temporary folder.");
        var app=new App(); app.InitializeComponent();
        var window=new MainWindow(); window.Show(); window.UpdateLayout();
        if(window.Shell.Endpoint!=ChecksEndpoint||window.Shell.HasPairedDevice) throw new Exception("The window must start from the checks' own settings, never from this PC's installation: "+window.Shell.Endpoint);
        if(!((Button)window.FindName("ConnectButton")).IsEnabled) throw new Exception("Connection button must be available.");
        if(((TabItem)window.FindName("CheckoutTab")).Visibility!=Visibility.Collapsed) throw new Exception("Checkout visible without authenticated role.");
        if(((TabItem)window.FindName("OrganizationTab")).Visibility!=Visibility.Collapsed) throw new Exception("Configuration visible without authenticated role.");   // E2
        if(((TabItem)window.FindName("CatalogTab")).Visibility!=Visibility.Collapsed) throw new Exception("Catalog visible without authenticated role.");   // E3
        if(((TabItem)window.FindName("OffersTab")).Visibility!=Visibility.Collapsed) throw new Exception("Offers visible without authenticated role.");   // E4a
        // E2: barra de menus clasica. Estructura definitiva; lo que no existe va deshabilitado; sin cabeceras de pestana.
        var menu=(Menu)window.FindName("MainMenu");
        var headers=menu.Items.OfType<MenuItem>().Select(m=>m.Header?.ToString()?.Replace("_","")).ToArray();
        if(!headers.SequenceEqual(new[]{"Archivo","Comedor","Caja","Configuración","ERP","Fiscalidad","Ayuda"})) throw new Exception("Menu bar structure: "+string.Join(",",headers));
        // E3: la primera entrada del ERP (Catalogo y tarifas) existe; el resto sigue deshabilitado con su corte en el tooltip.
        var erp=((MenuItem)window.FindName("ErpMenu")).Items.OfType<MenuItem>().ToArray();
        if(!erp[0].IsEnabled||erp[0].Header?.ToString()!="Catálogo y tarifas"||!erp[1].IsEnabled||erp[1].Header?.ToString()!="Oferta: cartas y menús"||erp.Skip(2).Any(m=>m.IsEnabled)) throw new Exception("ERP menu: catalog and offers exist (E3/E4a); the rest stays disabled.");
        if(((MenuItem)window.FindName("ConfigurationMenu")).Visibility!=Visibility.Collapsed||((MenuItem)window.FindName("DiningMenu")).Visibility!=Visibility.Collapsed) throw new Exception("Role menus are hidden without a session.");
        if(((TabItem)window.FindName("ServiceTab")).ActualHeight!=0) throw new Exception("Tab headers must not be painted: the menu selects the view.");
        if(((TabControl)window.FindName("Tabs")).IsEnabled) throw new Exception("Unauthenticated operations enabled.");
        // C1 (plan 2026-10-14): aspecto Fluent claro con nuestros tokens. El acento es el verde del lienzo en TODO equipo (no el azul de la
        // personalizacion de Windows), la ventana pinta un fondo opaco y la letra es Segoe UI Variable Text (se registra el fichero que
        // resolvio, para explicar diferencias entre capturas locales y de CI).
        foreach(var key in new[]{"AccentFillColorDefaultBrush","AccentButtonBackground","Accent"})
            if(Application.Current.FindResource(key) is not SolidColorBrush{Color:var accent}||accent!=Color.FromRgb(0x0E,0x7A,0x5F)) throw new Exception("The accent brush "+key+" must resolve to #0E7A5F.");
        if(window.Background is not SolidColorBrush{Color:var windowBackground}||windowBackground!=Color.FromRgb(0xF3,0xF4,0xF6)) throw new Exception("The window background must be the opaque #F3F4F6.");
        if(window.FontFamily.Source!="Segoe UI Variable Text, Segoe UI"||window.FontSize!=14) throw new Exception("Body font: "+window.FontFamily.Source+" "+window.FontSize);
        var fonts=string.Join(" | ",new[]{window.FontFamily,(FontFamily)Application.Current.FindResource("DisplayFont")}.Select(f=>
            new Typeface(f,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out var glyph)?f.Source+" -> "+glyph.FontUri.LocalPath:f.Source+" -> (sin resolver)"));
        Console.WriteLine("Fonts: "+fonts);
        if(!fonts.Contains(".tt",StringComparison.OrdinalIgnoreCase)) throw new Exception("The body and title fonts must resolve to a font file: "+fonts);
        // C1: sin sesion se ve Primeros pasos (con la tarjeta de entrada en su paso 1) y no hay barra lateral ni aviso de estado vacio.
        var firstRun=(FrameworkElement)window.FindName("FirstRunPanel");
        var loginCard=(Border)window.FindName("LoginCard");
        if(firstRun.Visibility!=Visibility.Visible||!firstRun.IsAncestorOf(loginCard)) throw new Exception("Without a session the Primeros pasos panel is shown and hosts the login card.");
        if(((FrameworkElement)window.FindName("SideNavBar")).Visibility!=Visibility.Collapsed||((FrameworkElement)window.FindName("StatusBarInfo")).Visibility!=Visibility.Collapsed) throw new Exception("Without a session there is no sidebar, and an empty status takes no room.");
        // C1: barra lateral con sus tres secciones (los mismos apartados y nombres de vista que el menu); lo que aun no existe, deshabilitado.
        var sideNav=(StackPanel)window.FindName("SideNav");
        var sections=sideNav.Children.OfType<StackPanel>().Select(s=>s.Children.OfType<TextBlock>().First().Text).ToArray();
        if(!sections.SequenceEqual(new[]{"Servicio","Gestión","Próximamente"})) throw new Exception("Sidebar sections: "+string.Join(",",sections));
        var navTags=sideNav.Children.OfType<StackPanel>().Take(2).SelectMany(s=>s.Children.OfType<Button>()).Select(b=>b.Tag as string).ToArray();
        if(!navTags.SequenceEqual(new[]{"HomeTab","ServiceTab","CheckoutTab","CatalogTab","OffersTab","OrganizationTab","DevicesTab"})) throw new Exception("Sidebar items open the same views as the menu: "+string.Join(",",navTags));
        var soonItems=((StackPanel)window.FindName("NavSoonSection")).Children.OfType<Button>().ToArray();
        if(soonItems.Length!=3||soonItems.Any(b=>b.IsEnabled||b.ToolTip as string!="Próximamente"||b.Tag is not null)) throw new Exception("Próximamente items are disabled and say so.");
        // D3.1: la habilitacion es un mapeo puro de las affordances del servidor, sin reglas locales.
        // D3.4 (F01): ademas, solo hay contexto accionable cuando la fila seleccionada coincide con la entidad leida.
        var shell=new Costina.Desktop.ViewModels.ShellViewModel();
        shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        if(!shell.HasDining) throw new Exception("The dining tab exists only when the server lists the dining module.");
        shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:[]);
        if(shell.HasDining) throw new Exception("Without the dining module there is no dining tab.");   // ADR-012
        shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        var prep=new Costina.Client.PreparationDto("i1","Plato","hot",1,null,true,"Fired",["preparation-start","review-preparation"],null,true);
        var dto=new Costina.Client.DiningDto("s1","M1",2,"InService",[],["pause","fire-next"]);
        var entry=new Costina.Client.BoardEntry(3,dto,new("o1","M1","s1","Occupied",null,["release"]),1);
        shell.Service.Board=[entry]; shell.Service.SelectedEntry=entry;   // sin Api la seleccion no dispara lecturas
        shell.Service.Dining=new(3,dto);
        shell.Service.SelectedCourse=new Costina.Client.CourseDto("c1","Pase","Ready",null,null,null,null,[prep],["serve"]);
        if(!shell.Service.CanAction("pause")||!shell.Service.CanAction("fire-next")||!shell.Service.CanAction("serve")||!shell.Service.CanAction("preparation-start")||!shell.Service.CanAction("release")||!shell.Service.CanAction("review-preparation"))
            throw new Exception("Advertised affordances must enable their controls.");
        if(prep.ReviewText!="⚠ PENDIENTE DE REVISIÓN") throw new Exception("A pending review must be stated in text, never colour only.");
        if(shell.Service.CanAction("complete")||shell.Service.CanAction("skip")||shell.Service.CanAction("preparation-ready"))
            throw new Exception("Actions the server did not advertise must stay disabled.");
        if(!shell.CanOpen) throw new Exception("Session affordance must drive the open panel.");
        shell.Busy=true;
        if(shell.Service.CanAction("pause")) throw new Exception("Busy must gate every action.");
        shell.Busy=false;
        var other=new Costina.Client.BoardEntry(1,new("s2","M2",2,"InService",[],["pause"]),new("o2","M2","s2","Occupied",null,["release"]),1);
        shell.Service.Board=[entry,other]; shell.Service.SelectedEntry=other;   // destino nuevo con el detalle antiguo aun cargado
        if(shell.Service.CanAction("pause")||shell.Service.CanAction("fire-next")||shell.Service.CanAction("serve")||shell.Service.CanAction("release")||shell.Service.CanAction("preparation-start"))
            throw new Exception("A selection that does not match the loaded detail must disable every action.");
        shell.Service.SelectedEntry=entry;
        if(!shell.Service.CanAction("pause")) throw new Exception("Returning to the loaded entity restores its actions.");
        shell.Session=null;
        if(shell.Service.CanAction("pause")||shell.CanOpen) throw new Exception("Disconnected must gate every action.");
        // D3.4 (F01) en caja: nunca se cobra ni se carga en una cuenta distinta de la leida.
        shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4");
        shell.Checkout.Products=[new Costina.Client.ProductChoice("water","Agua","Botella",400)]; shell.Checkout.SelectedProduct=shell.Checkout.Products[0];
        shell.Checkout.Accounts=[new Costina.Client.AccountChoice("s1","M1","Open"),new Costina.Client.AccountChoice("s2","M2","Open")];
        shell.Checkout.SelectedAccount=shell.Checkout.Accounts[0];
        shell.Checkout.Account=new(1,new Costina.Client.AccountDto("a1","s1","Open",0,0,0,0,"none",[],[],["add-product","payment"],[]));
        if(!shell.Checkout.AddCommand.CanExecute(null)||!shell.Checkout.PayCommand.CanExecute(null)) throw new Exception("Matching account must enable its advertised actions.");
        shell.Checkout.SelectedAccount=shell.Checkout.Accounts[1];
        if(shell.Checkout.AddCommand.CanExecute(null)||shell.Checkout.PayCommand.CanExecute(null)) throw new Exception("Account actions must never target a different account than the loaded one.");
        // D3.4 (F02/F03): almacen durable aislado por instalacion, ambito, rol y ventana; la evidencia se conserva.
        var folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-checks-"+Guid.NewGuid().ToString("N"));
        var session=new Costina.Client.SessionInfo("checks","t","c","l",null,"inst-checks","0.7.0-d3.4");
        string[] files;
        using(var first=new DpapiPendingStore(session,folder))
        using(var second=new DpapiPendingStore(session,folder))
        {
            if(first.Slot==second.Slot) throw new Exception("Two live windows of the same role must never share a durable slot.");
            var command=new Costina.Client.PendingCommand("k123","services/x/commands/start","{\"marker-secreto\":7}","Start");
            first.Save(command);
            if(second.Load().Outcome!=Costina.Client.PendingOutcome.Absent) throw new Exception("A window must not see another window's pending command.");
            var load=first.Load();
            if(load.Outcome!=Costina.Client.PendingOutcome.Restored||load.Command is not {Key:"k123"} restored||restored.Body!=command.Body) throw new Exception("DPAPI round trip must be lossless.");
            files=Directory.GetFiles(folder,"*.bin");
            if(files.Length!=1||System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(files[0])).Contains("marker-secreto")) throw new Exception("Pending body must not be plaintext.");
            first.Clear("otra-clave");
            if(!File.Exists(files[0])) throw new Exception("Clearing a different key must not delete the stored command.");
            // Cuerpo cifrado danado: se conserva la cabecera con la clave, se informa y NO se borra.
            var bytes=File.ReadAllBytes(files[0]); var newline=Array.IndexOf(bytes,(byte)'\n');
            File.WriteAllBytes(files[0],[..bytes[..(newline+1)],(byte)1,(byte)2,(byte)3]);
            var damaged=first.Load();
            if(damaged.Outcome!=Costina.Client.PendingOutcome.Unreadable||damaged.Key!="k123"||!File.Exists(files[0])) throw new Exception("A damaged pending file must be reported with its key and kept.");
            first.Discard();
            if(File.Exists(files[0])||Directory.GetFiles(folder,"*.cuarentena-*").Length!=1) throw new Exception("Discarding must quarantine the evidence, not destroy it.");
            if(first.Load().Outcome!=Costina.Client.PendingOutcome.Absent) throw new Exception("After quarantine the slot is empty.");
            first.Save(command); first.Clear("k123");
            if(File.Exists(files[0])) throw new Exception("Clearing the confirmed key must delete the file.");
            // Fichero de otra instalacion en el mismo hueco: se informa y nunca se reenvia.
            using(var foreign=new DpapiPendingStore(new("checks","t","c","l",null,"inst-otra","0.7.0-d3.4"),folder)) foreign.Save(command);
            File.Move(Directory.GetFiles(folder,"inst-otra-*.bin").Single(),files[0]);
            if(first.Load() is not {Outcome:Costina.Client.PendingOutcome.Unreadable,Key:"k123"}) throw new Exception("A command from another installation must never be restored.");
            first.Discard();
        }
        using(var third=new DpapiPendingStore(session,folder))
            if(third.Slot!=0) throw new Exception("A released slot must be reused by the next window.");
        Directory.Delete(folder,true);
        // D4.3b: la credencial del puesto emparejado persiste cifrada y lo corrupto se descarta.
        var deviceStore=new DpapiDeviceStore(new Uri("http://127.0.0.1:59998"));
        deviceStore.Clear();
        deviceStore.Save("dev.abc123.secreto-de-prueba");
        if(deviceStore.Load()!="dev.abc123.secreto-de-prueba") throw new Exception("Device token round trip must be lossless.");
        var deviceFile=System.IO.Path.Combine(checksRoot,"device-59998.bin");   // C1: en la carpeta de los checks, nunca en la real
        if(!File.Exists(deviceFile)) throw new Exception("The device store must follow the checks' data folder.");
        if(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(deviceFile)).Contains("secreto-de-prueba")) throw new Exception("Device token file must not be plaintext.");
        File.WriteAllBytes(deviceFile,[9,9,9]);
        if(deviceStore.Load() is not null||File.Exists(deviceFile)) throw new Exception("Corrupted device file must be discarded and deleted.");
        // D6.5: QR de emparejamiento en Puestos. Sin direccion https valida para los DISPOSITIVOS no hay QR y se dice por que;
        // el enlace lleva SOLO el codigo de un uso, en el fragmento; la direccion se recuerda en este equipo.
        var qrFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-qr-"+Guid.NewGuid().ToString("N"));
        var admin=new Costina.Desktop.ViewModels.ShellViewModel(); admin.Devices.MemoryFolder=qrFolder;
        admin.Devices.PairingCode="AbC123_def-456";
        if(admin.Devices.HasQr||!admin.Devices.QrInfo.StartsWith("Sin QR",StringComparison.Ordinal)) throw new Exception("Without a device address there is no QR, and the screen says why.");
        foreach(var bad in new[]{"http://127.0.0.1:5088","http://192.168.1.10:5088","https://costina-server.local:5443/app","https://user@costina-server.local:5443","costina-server"})
        {
            admin.Devices.DeviceAddress=bad;
            if(admin.Devices.HasQr||admin.Devices.PairingUrl.Length!=0) throw new Exception("Only https://NAME:PORT is a device address: "+bad);
        }
        admin.Devices.DeviceAddress="https://costina-server.local:5443";
        if(admin.Devices.PairingUrl!="https://costina-server.local:5443/app/#pair=AbC123_def-456") throw new Exception("The QR link must be exactly the PWA pairing URL with the code in the fragment.");
        if(admin.Devices.PairingQr is not BitmapSource {PixelWidth:>100} qr||qr.PixelWidth!=qr.PixelHeight||!qr.IsFrozen) throw new Exception("The QR must be a real, square, frozen bitmap.");
        Directory.CreateDirectory("artifacts/desktop");
        var qrEncoder=new PngBitmapEncoder();qrEncoder.Frames.Add(BitmapFrame.Create(qr));
        using(var qrFile=File.Create("artifacts/desktop/pairing-qr.png")) qrEncoder.Save(qrFile);
        admin.Devices.PairingCode="no valido!";
        if(admin.Devices.HasQr) throw new Exception("Anything that is not a pairing code never reaches a URL.");
        admin.Devices.PairingCode="";
        if(admin.Devices.HasQr||admin.Devices.PairingUrl.Length!=0) throw new Exception("No code, no QR.");
        var again=new Costina.Desktop.ViewModels.ShellViewModel(); again.Devices.MemoryFolder=qrFolder; again.Devices.SuggestAddress();
        if(!File.ReadAllText(System.IO.Path.Combine(qrFolder,"desktop.json")).Contains("costina-server.local")||File.ReadAllText(System.IO.Path.Combine(qrFolder,"desktop.json")).Contains("dev.")) throw new Exception("The device address is remembered in desktop.json without secrets.");
        if(again.Devices.DeviceAddress!="https://costina-server.local:5443") throw new Exception("On loopback the last device address used on this machine is proposed.");
        var lan=new Costina.Desktop.ViewModels.ShellViewModel(); lan.Devices.MemoryFolder=qrFolder; lan.Endpoint="https://otro-servidor.local:5443"; lan.Devices.SuggestAddress();
        if(lan.Devices.DeviceAddress!="https://otro-servidor.local:5443") throw new Exception("A client already on the LAN proposes its own server address.");
        Directory.Delete(qrFolder,true); DesktopSettings.Folder=checksRoot;
        // E2: pestana Configuracion (solo main). La pantalla se compone SOLO de lo leido; los comandos siguen la misma tuberia y
        // se habilitan por rol y por estado (ocupado, orden pendiente): nunca por reglas de negocio locales.
        var sampleOrganization=new Costina.Client.OrganizationDto(
            [new("sala","Sala",0,true,[new("M1","Mesa 1",12,"sala",0,true),new("M2","Mesa 2",4,"sala",1,true),new("M3","Mesa 3 (junto a la ventana)",6,"sala",2,false)]),
             new("terraza","Terraza",1,true,[new("T1","Terraza 1",4,"terraza",0,true)])],
            [new("pase","Pase","Pass",0,true),new("cold","Cocina fría","Kitchen",1,true),new("hot","Cocina caliente","Kitchen",2,true),new("sala-1","Sala 1","Room",3,false)]);
        var organization=window.Shell.Organization;
        organization.Render(sampleOrganization);
        if(organization.SaveZoneCommand.CanExecute(null)||organization.StartTableCommand.CanExecute(null)) throw new Exception("Disconnected must gate every organization command.");
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        if(organization.SaveZoneCommand.CanExecute(null)||organization.StartStationCommand.CanExecute(null)) throw new Exception("Only the main desk edits the organization.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        window.UpdateLayout();
        if(((MenuItem)window.FindName("ConfigurationMenu")).Visibility!=Visibility.Visible||((MenuItem)window.FindName("FiscalMenu")).Visibility!=Visibility.Visible) throw new Exception("The main desk sees every menu.");
        if(!window.Shell.ConnectionText.Contains("Responsable")) throw new Exception("The status bar states who is connected: "+window.Shell.ConnectionText);
        // C1: con sesion, Primeros pasos se recoge y aparece la barra lateral; Comedor y cocina solo con el modulo de comedor.
        if(firstRun.Visibility!=Visibility.Collapsed||((FrameworkElement)window.FindName("SideNavBar")).Visibility!=Visibility.Visible) throw new Exception("Connected: no Primeros pasos, the sidebar is shown.");
        var navService=(Button)window.FindName("NavService");
        if(navService.Visibility!=Visibility.Visible) throw new Exception("The Comedor sidebar item exists with the dining module.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:[]); window.UpdateLayout();
        if(navService.Visibility!=Visibility.Collapsed||((MenuItem)window.FindName("DiningMenu")).Visibility!=Visibility.Collapsed) throw new Exception("Without the dining module there is no Comedor in the sidebar (nor in the menu).");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]); window.UpdateLayout();
        if(organization.SelectedZone?.Id!="sala"||organization.ZoneName!="Sala"||organization.Tables.Length!=3) throw new Exception("Rendering must select the first zone and fill its form and tables.");
        if(!organization.SaveZoneCommand.CanExecute(null)||!organization.ToggleZoneCommand.CanExecute(null)) throw new Exception("A selected zone is editable by the main desk.");
        organization.SelectedTable=organization.Tables[2];
        if(organization.TableName!="Mesa 3 (junto a la ventana)"||organization.TableCapacity!="6"||organization.TableZoneId!="sala"||organization.SelectedTable.StateLabel!="desactivada") throw new Exception("Selecting a table fills its form and states its status in text.");
        organization.StartTableCommand.Execute(null);
        if(!organization.NewTable||organization.TableId!=""||organization.TableZoneId!="sala"||organization.TableFormTitle!="Nueva mesa") throw new Exception("A new table starts empty inside the selected zone.");
        organization.SelectedStation=organization.Stations[0];
        if(organization.ToggleStationCommand.CanExecute(null)) throw new Exception("The reserved pass station can never be deactivated from the screen.");
        organization.SelectedStation=organization.Stations[3];
        if(!organization.ToggleStationCommand.CanExecute(null)||organization.StationKind!="room") throw new Exception("Other stations toggle and their kind fills the form.");
        window.Shell.Busy=true;
        if(organization.SaveZoneCommand.CanExecute(null)||organization.ToggleStationCommand.CanExecute(null)) throw new Exception("Busy must gate every organization command.");
        window.Shell.Busy=false;
        // E3: vista ERP > Catalogo y tarifas (solo main). Se compone SOLO de lo leido; los comandos siguen la misma tuberia.
        var sampleCatalog=new Costina.Client.CatalogDto(
            [new("iva-21","IVA 21 %",21m,true),new("iva-10","IVA 10 %",10m,true),new("iva-4","IVA 4 %",4m,true),new("iva-0","Exento",0m,false)],
            [new("bebidas","Bebidas",null,"#1F4D3A",0,true),new("vinos","Vinos","bebidas","#8C1D1D",1,true),new("tintos","Tintos","vinos",null,0,true),new("comida","Comida",null,"#B5673A",2,false)],
            [new("water","Agua mineral","bebidas","iva-10",null,0,true,[new("bottle","Botella",0,true,[new("general","1970-01-01",400)])]),
             new("wine","Vino de ensayo","vinos","iva-21","3754",1,true,[new("glass","Copa",0,true,[new("general","1970-01-01",950),new("terraza","2026-09-01",1200),new("terraza","2026-12-01",1300)]),new("bottle","Botella",1,true,[new("general","1970-01-01",4200)])]),
             new("3760","El Rapolao 2023","tintos","iva-21","3760",2,true,[new("botella","Botella",0,true,[new("general","2026-09-23",4000)])]),
             new("coffee","Café solo",null,"iva-10",null,3,false,[new("unit","Unidad",0,true,[])])],
            [new("general","General",0,true),new("terraza","Terraza",1,true)],"2026-09-23");
        var catalogView=window.Shell.Catalog;
        window.Shell.Session=null; catalogView.Render(sampleCatalog);
        if(catalogView.StartProductCommand.CanExecute(null)||catalogView.SetPriceCommand.CanExecute(null)) throw new Exception("Disconnected must gate every catalog command.");
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        if(catalogView.StartProductCommand.CanExecute(null)||catalogView.StartTaxCommand.CanExecute(null)) throw new Exception("Only the main desk edits the catalog.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        if(catalogView.Categories.Select(c=>c.Category.Id).SequenceEqual(new[]{"bebidas","vinos","tintos","comida"})==false||catalogView.Categories[2].Depth!=2) throw new Exception("Categories render as an indented tree.");
        if(catalogView.Products.Length!=4||!catalogView.Summary.Contains("3 productos activos (3 vendibles hoy)")) throw new Exception("All products listed and summary counts sellables: "+catalogView.Summary);
        catalogView.SelectedCategory=catalogView.Categories[1];
        if(catalogView.AllCategories||catalogView.Products.Length!=1||catalogView.Products[0].Product.Id!="wine") throw new Exception("Selecting a category filters its products.");
        catalogView.SelectedProduct=catalogView.Products[0];
        if(catalogView.ProductName!="Vino de ensayo"||catalogView.ProductTaxId!="iva-21"||catalogView.ProductReference!="3754"||catalogView.Presentations.Length!=2) throw new Exception("Selecting a product fills its form and lists its presentations.");
        if(catalogView.SelectedPresentation?.Presentation.Id!="glass"||catalogView.PriceText!="9,50"||!catalogView.Presentations[0].PriceText.Contains("Terraza 12,00 €")) throw new Exception("The price in force today per tariff is shown (not the scheduled one): "+catalogView.Presentations[0].PriceText);
        catalogView.PriceTariffId="terraza";
        if(catalogView.PriceText!="12,00") throw new Exception("Changing the tariff shows its price in force.");
        if(!catalogView.SetPriceCommand.CanExecute(null)||!catalogView.StartPresentationCommand.CanExecute(null)) throw new Exception("A selected presentation can be priced by the main desk.");
        catalogView.AllCategories=true; catalogView.Search="rapolao";
        if(catalogView.Products.Length!=1||catalogView.Products[0].Product.Id!="3760") throw new Exception("Search filters by name or reference.");
        catalogView.Search=""; catalogView.SelectedTariff=catalogView.Tariffs[0];
        if(catalogView.ToggleTariffCommand.CanExecute(null)) throw new Exception("The general tariff can never be deactivated from the screen.");
        catalogView.SelectedTariff=catalogView.Tariffs[1];
        if(!catalogView.ToggleTariffCommand.CanExecute(null)||catalogView.TariffName!="Terraza") throw new Exception("Other tariffs toggle and fill the form.");
        catalogView.SelectedTax=catalogView.Taxes[3];
        if(catalogView.SelectedTax.StateLabel!="desactivado"||catalogView.TaxRate!="0") throw new Exception("Selecting a tax fills its form and states its status in text.");
        if(Costina.Desktop.ViewModels.CatalogViewModel.Cents("9,50")!=950||Costina.Desktop.ViewModels.CatalogViewModel.Cents("1.250,50")!=125050||Costina.Desktop.ViewModels.CatalogViewModel.Cents("12")!=1200) throw new Exception("Typed euro amounts convert to cents.");
        window.Shell.Busy=true;
        if(catalogView.SaveProductCommand.CanExecute(null)||catalogView.SetPriceCommand.CanExecute(null)) throw new Exception("Busy must gate every catalog command.");
        window.Shell.Busy=false;
        catalogView.SelectedCategory=catalogView.Categories[1]; catalogView.SelectedProduct=catalogView.Products[0];
        // Corte A (plan 2026-09-24): Inicio es la primera vista al entrar y resume servidor, certificado, copias y dispositivos;
        // el centro de dispositivos ofrece asistentes CON INTENCION (comandero / pantalla por estacion), reconoce solo su propia
        // solicitud, no ofrece 'Principal', agrupa la lista con presencia y quita en dos pasos. Todo compuesto de lo leido.
        var archivo=((Menu)window.FindName("MainMenu")).Items.OfType<MenuItem>().First().Items.OfType<MenuItem>().Select(m=>m.Header?.ToString()).ToArray();
        if(archivo[0]!="Inicio") throw new Exception("Archivo starts with Inicio: "+string.Join(",",archivo));
        var workspace=(TabControl)window.FindName("Tabs");
        window.Shell.Session=null; window.UpdateLayout();
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]); window.UpdateLayout();
        if(workspace.SelectedItem!=(TabItem)window.FindName("HomeTab")) throw new Exception("Connecting selects Inicio first.");
        var home=window.Shell.Home;
        if(!home.WhoText.StartsWith("Responsable",StringComparison.Ordinal)) throw new Exception("Inicio states who is connected: "+home.WhoText);
        home.ApplyDevices([new("d1","tablet-sala-1","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null,true),new("d2","tablet-sala-2","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null),
            new("d3","pantalla-fria","kitchen","cold","user:jefa",DateTimeOffset.Now.AddDays(-1),null,true),new("d4","vieja","kitchen","hot","user:jefa",DateTimeOffset.Now.AddDays(-9),DateTimeOffset.Now.AddDays(-2))]);
        if(home.DevicesSummary!="2 comanderos (1 con conexión ahora) · 1 pantalla de cocina (1 con conexión ahora)") throw new Exception("Inicio summarises active devices and presence: "+home.DevicesSummary);
        var soon=new Costina.Client.DiagnosticsDto("installation","0.33.0-b1",DateTimeOffset.Now,new(true,5443,DateTimeOffset.UtcNow.AddDays(12).AddMinutes(5),"costina-server.local"),new(true,DateTimeOffset.Now.AddHours(-3)),true);
        home.ApplyDiagnostics(soon);
        if(!home.HasCertificateWarning||!home.CertificateWarning.Contains("12 días")||!home.ServerText.Contains("costina-server.local")||!home.BackupText.StartsWith("Última copia",StringComparison.Ordinal)) throw new Exception("Inicio warns about a certificate expiring within 30 days and states server and backup: "+home.CertificateWarning);
        home.ApplyDiagnostics(soon with {Tls=soon.Tls! with {ServerCertificateExpiresAt=DateTimeOffset.UtcNow.AddDays(200)}});
        if(home.HasCertificateWarning) throw new Exception("No warning for a certificate with months left.");
        home.ApplyDiagnostics(soon with {Tls=soon.Tls! with {ServerCertificateExpiresAt=DateTimeOffset.UtcNow.AddDays(-1)}});
        if(!home.CertificateWarning.StartsWith("EL CERTIFICADO DEL SERVIDOR CADUCÓ",StringComparison.Ordinal)) throw new Exception("An expired certificate is said loudly.");
        home.ApplyDiagnostics(soon);
        var center=window.Shell.Devices;
        var stationsForDevices=new Costina.Client.StationDto[]{new("pase","Pase","Pass",0,true),new("cold","Cocina fría","Kitchen",1,true),new("hot","Cocina caliente","Kitchen",2,true),new("sala-1","Sala 1","Room",3,true),new("terraza","Terraza","Room",4,false)};
        var pendingOther=new Costina.Client.PairingPendingDto("pid-otra","tablet-ajena",DateTimeOffset.Now);
        var activeDevices=new Costina.Client.DeviceRowDto[]{new("d1","tablet-sala-1","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null,true),new("d3","pantalla-fria","kitchen","cold","user:jefa",DateTimeOffset.Now.AddDays(-1),null),new("d4","vieja","kitchen","hot","user:jefa",DateTimeOffset.Now.AddDays(-9),DateTimeOffset.Now.AddDays(-2))};
        center.Render(stationsForDevices,[pendingOther],activeDevices);
        if(!center.RoomStations.Select(s=>s.Id).SequenceEqual(new[]{"sala-1"})||!center.KitchenStations.Select(s=>s.Id).SequenceEqual(new[]{"pase","cold","hot"})) throw new Exception("Waiters go to room/bar stations and screens to kitchen/pass/bar; inactive ones are never offered.");
        // C1: la vista de dispositivos es un UserControl con su propio ambito de nombres: se busca a traves de el.
        var devicesView=(FrameworkElement)window.FindName("DevicesView");
        if(devicesView.FindName("ApproveStationBox") is not null||devicesView.FindName("RoomStationBox") is null) throw new Exception("No role list and no 'Principal' option: the assistant carries the intent.");
        if(!center.StartWaiterCommand.CanExecute(null)||!center.StartKitchenCommand.CanExecute(center.KitchenStations[1])||center.CreateRoomStationCommand.CanExecute(null)) throw new Exception("With a room station the waiter assistant starts and 'create Sala' is not offered.");
        if(center.Rows.Length!=2||center.Rows[0].Group!="Comanderos"||center.Rows[1].Group!="Pantalla de cocina · Cocina fría"||!center.ListSummary.Contains("1 quitado (oculto).")) throw new Exception("The list groups active devices and hides the removed ones: "+center.ListSummary);
        center.ShowRevoked=true;
        if(center.Rows.Length!=3||!center.Rows[2].Device.StateLabel.StartsWith("Quitado el ",StringComparison.Ordinal)||center.Rows[2].Group!="Pantalla de cocina · Cocina caliente") throw new Exception("Removed devices appear on demand, stated in text.");
        if(!center.Rows[0].Text.Contains("· Conectado · aprobado por jefa el ")) throw new Exception("Device rows state presence in words and the approver without 'user:': "+center.Rows[0].Text);
        center.ShowRevoked=false;
        center.BeginAssistant("kitchen",center.KitchenStations[1]);
        if(!center.HasAssistant||center.AssistantTitle!="Conectar pantalla de cocina · Cocina fría"||center.StartWaiterCommand.CanExecute(null)) throw new Exception("One assistant at a time, with its intent in the title.");
        center.Issued("pid-propia","AbC123_def-456",DateTimeOffset.UtcNow.AddMinutes(5));
        center.DeviceAddress="https://costina-server.local:5443";
        if(!center.HasQr||center.PairingUrl!="https://costina-server.local:5443/app/#pair=AbC123_def-456") throw new Exception("The assistant shows the QR of the one-use code.");
        center.Render(stationsForDevices,[pendingOther],activeDevices);
        if(center.HasRecognised||center.ApproveCommand.CanExecute(null)||!center.DenyCommand.CanExecute(null)) throw new Exception("A request that is not the assistant's own is never recognised nor approvable (only denied).");
        center.Render(stationsForDevices,[pendingOther,new("pid-propia","pantalla-fria",DateTimeOffset.Now)],activeDevices);
        if(!center.HasRecognised||!center.ApproveCommand.CanExecute(null)||!center.RecognisedText.Contains("pantalla de cocina de Cocina fría")) throw new Exception("The assistant recognises its own request by pairingId and offers to approve it.");
        if(!center.HasDuplicateWarning) throw new Exception("A request named like an active device is flagged (F12).");
        center.Render(stationsForDevices,[pendingOther,new("pid-propia","pantalla-caliente",DateTimeOffset.Now)],activeDevices);
        if(center.HasDuplicateWarning||center.NewCodeCommand.CanExecute(null)) throw new Exception("A fresh name raises no warning; with a recognised request no new code is offered.");
        center.SelectedRow=center.Rows[0];
        if(!center.AskRemoveCommand.CanExecute(null)||center.ConfirmRemoveCommand.CanExecute(null)) throw new Exception("Removing asks first.");
        center.AskRemoveCommand.Execute(null);
        if(!center.ConfirmingRemoval||!center.RemovalText.Contains("tablet-sala-1")||!center.ConfirmRemoveCommand.CanExecute(null)||center.AskRemoveCommand.CanExecute(null)) throw new Exception("Removal is confirmed in a second step that names the device.");
        center.CancelRemoveCommand.Execute(null);
        if(center.ConfirmingRemoval) throw new Exception("Cancelling leaves the device alone.");
        center.Render([new("pase","Pase","Pass",0,true),new("cold","Cocina fría","Kitchen",1,true)],[],[]);
        if(!center.NeedsRoomStation||!center.CreateRoomStationCommand.CanExecute(null)||!center.HasAssistant) throw new Exception("Without a room station the screen offers to create 'Sala'; the open assistant survives a re-read.");
        window.Shell.Busy=true;
        if(center.ApproveCommand.CanExecute(null)||center.StartKitchenCommand.CanExecute(center.KitchenStations[0])) throw new Exception("Busy must gate every device command.");
        window.Shell.Busy=false;
        center.Render(stationsForDevices,[pendingOther,new("pid-propia","pantalla-caliente",DateTimeOffset.Now)],activeDevices);
        // Corte B (ADR-013): niveles Puesto / Responsable. Todo visible; lo sensible pide al responsable; la sesion caducada
        // vuelve a Puesto sin matar el programa; la inactividad tambien; desktop.json nunca guarda secretos; el arranque
        // clasifica red frente a certificado. Nada de esto toca la red: estados simulados.
        var settingsFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-settings-"+Guid.NewGuid().ToString("N"));
        DesktopSettings.Folder=settingsFolder;
        new DesktopSettings{Endpoint="https://costina-server.local:5443",Username="jefa",DeviceAddress="https://costina-server.local:5443"}.Save();
        var reloaded=DesktopSettings.Load();
        if(reloaded.Endpoint!="https://costina-server.local:5443"||reloaded.Username!="jefa"||File.ReadAllText(DesktopSettings.Path).Contains("dev.")) throw new Exception("desktop.json round-trips the address and the user and holds no secrets.");
        // C1 (arreglo): un puesto adicional guarda https://NOMBRE:PUERTO; la ventana arranca con esa direccion (antes: NullReference).
        File.WriteAllText(DesktopSettings.Path,"{\"endpoint\":\"https://costina-server.local:5443\",\"username\":\"jefa\"}");
        Costina.Desktop.ViewModels.ShellViewModel workstation;
        try { workstation=new Costina.Desktop.ViewModels.ShellViewModel(); }
        catch(Exception e) { throw new Exception("A workstation that saved another server address must start: "+e.GetType().Name+" "+e.Message,e); }
        if(workstation.Endpoint!="https://costina-server.local:5443"||workstation.Username!="jefa") throw new Exception("The saved address and user are restored at startup: "+workstation.Endpoint+" "+workstation.Username);
        new DesktopSettings{Endpoint="http://127.0.0.1:5088",Username="dev.abc.secreto"}.Save();
        if(DesktopSettings.Load().Username!="jefa") throw new Exception("A value that looks like a credential is never written to desktop.json.");
        File.WriteAllText(DesktopSettings.Path,"{\"endpoint\":\"http://127.0.0.1:5088\",\"username\":\"dev.abc.secreto\"}");
        if(DesktopSettings.Load().Username!="") throw new Exception("A credential found in desktop.json is discarded on load.");
        if(Costina.Client.AuthClient.Classify(new System.Net.Http.HttpRequestException("tls",new System.Security.Authentication.AuthenticationException("untrusted")))!=Costina.Client.StartupProblem.Certificate
            ||Costina.Client.AuthClient.Classify(new System.Net.Http.HttpRequestException("refused"))!=Costina.Client.StartupProblem.Unreachable
            ||Costina.Client.AuthClient.Classify(new Costina.Client.ApiError(502,"health_unavailable"))!=Costina.Client.StartupProblem.Other) throw new Exception("Startup failures are classified: certificate (never retried), unreachable (wait), other.");
        var levels=window.Shell;
        levels.Session=new("service","t","c","l",["open"],"inst-checks","0.33.0-b1",Actor:"device:pc-principal",StationName:"Sala 1",Modules:["dining"]); window.UpdateLayout();
        if(!levels.IsStation||levels.IsResponsible||levels.LevelText!="Puesto"||!levels.ConnectionText.StartsWith("Puesto: pc-principal · Puesto de sala · Sala 1 · ",StringComparison.Ordinal)) throw new Exception("A device session is the Puesto level: "+levels.ConnectionText);
        if(((MenuItem)window.FindName("CheckoutMenu")).Visibility!=Visibility.Visible||((MenuItem)window.FindName("ErpMenu")).Visibility!=Visibility.Visible) throw new Exception("Every menu is visible at the Puesto level (decision 3: nothing hidden).");
        if(!Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("CheckoutTab")||!Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("DevicesTab")||Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("ServiceTab")||Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("HomeTab")) throw new Exception("Caja, configuracion, ERP and devices are the responsible's; dining and home are not.");
        if(levels.LoginCardVisible||levels.ElevationRequested) throw new Exception("Connected as Puesto, no login card until something sensitive is asked.");
        levels.RequestElevation("CheckoutTab");
        if(!levels.ElevationRequested||!levels.LoginCardVisible||!levels.LoginTitle.Contains("Cuenta / Caja")||!levels.ConnectEnabled) throw new Exception("Asking for a sensitive view opens the responsible login naming the view: "+levels.LoginTitle);
        window.UpdateLayout();
        if(((Border)window.FindName("LoginCard")).Visibility!=Visibility.Visible||((Button)window.FindName("CancelElevationButton")).Visibility!=Visibility.Visible) throw new Exception("The login card and 'seguir como puesto' are shown.");
        var elevationShot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); elevationShot.Render(window);
        var elevationEncoder=new PngBitmapEncoder(); elevationEncoder.Frames.Add(BitmapFrame.Create(elevationShot));
        using(var elevationFile=File.Create("artifacts/desktop/elevation.png")) elevationEncoder.Save(elevationFile);
        levels.CancelElevationCommand.Execute(null);
        if(levels.ElevationRequested||levels.LoginCardVisible||levels.ConnectEnabled) throw new Exception("'Seguir como puesto' closes the card and keeps the station connected.");
        // C1: los accesos de Inicio viven en su vista y navegan con GoToPage: nacen habilitados y siguen la logica del menu.
        var homeDevices=(Button)((FrameworkElement)window.FindName("HomeView")).FindName("HomeDevicesButton");
        if(!homeDevices.IsEnabled) throw new Exception("The Inicio shortcuts are enabled from the start (GoToPage CanExecute).");
        System.Windows.Input.NavigationCommands.GoToPage.Execute(homeDevices.CommandParameter,homeDevices);
        if(!levels.ElevationRequested||!levels.LoginTitle.Contains("Dispositivos")||workspace.SelectedItem!=(TabItem)window.FindName("HomeTab")) throw new Exception("A sensitive Inicio shortcut at the Puesto level asks for the responsible and keeps Inicio: "+levels.LoginTitle);
        levels.CancelElevationCommand.Execute(null);
        // C1: la barra lateral aplica la misma logica: Caja desde el nivel Puesto pide al responsable, la tarjeta se abre centrada sobre
        // un velo y la vista NO cambia; el resaltado sigue a la vista abierta (Inicio), no al elemento pulsado.
        var navCheckout=(Button)window.FindName("NavCheckout");
        navCheckout.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(!levels.ElevationRequested||!levels.LoginTitle.Contains("Cuenta / Caja")||workspace.SelectedItem!=(TabItem)window.FindName("HomeTab")||levels.CurrentView!="HomeTab") throw new Exception("The Caja sidebar item at the Puesto level asks for the responsible and keeps Inicio: "+levels.CurrentView);
        if(Costina.Desktop.Ui.GetCurrent(navCheckout)||!Costina.Desktop.Ui.GetCurrent((Button)window.FindName("NavHome"))) throw new Exception("The sidebar highlights the open view, not the clicked item.");
        if(loginCard.Parent!=window.FindName("ElevationLoginHost")||((FrameworkElement)window.FindName("ElevationLayer")).Visibility!=Visibility.Visible||firstRun.Visibility!=Visibility.Collapsed) throw new Exception("At the Puesto level the login card opens centred over a scrim, not in Primeros pasos.");
        levels.CancelElevationCommand.Execute(null); window.UpdateLayout();
        if(loginCard.Parent!=window.FindName("FirstRunLoginHost")||((FrameworkElement)window.FindName("ElevationLayer")).Visibility!=Visibility.Collapsed) throw new Exception("Cancelling puts the login card back in Primeros pasos and lifts the scrim.");
        var opened=""; levels.ViewRequested+=name=>opened=name;
        levels.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.33.0-b1",Actor:"user:jefa",Modules:["dining"]));
        if(!levels.IsResponsible||levels.LevelText!="Responsable"||!levels.LeaveResponsibleCommand.CanExecute(null)) throw new Exception("A person session is the Responsable level.");
        levels.RequestElevation("CatalogTab");
        if(opened!="CatalogTab"||levels.ElevationRequested) throw new Exception("A responsible asking for a sensitive view just gets it.");
        var moment=new DateTimeOffset(2026,10,13,12,0,0,TimeSpan.Zero); levels.Clock=()=>moment; levels.NoteActivity();
        moment=moment.AddMinutes(14);
        if(levels.CheckInactivity()||!levels.IsResponsible) throw new Exception("Under 15 minutes of inactivity the responsible stays.");
        moment=moment.AddMinutes(2);
        if(!levels.CheckInactivity()||levels.Connected||!levels.Status.Contains("Sin actividad")) throw new Exception("15 minutes without input leave the responsible level and say so: "+levels.Status);
        levels.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.33.0-b1",Actor:"user:jefa",Modules:["dining"]));
        levels.OnUnauthorized().GetAwaiter().GetResult();
        if(levels.Connected||!levels.Status.Contains("caducado")) throw new Exception("A 401 of the person session returns to Puesto (or to the login card) and explains it: "+levels.Status);
        levels.Clock=()=>DateTimeOffset.UtcNow;
        if(Costina.Desktop.ViewModels.DevicesViewModel.ThisPcName().Length<4||!Costina.Desktop.ViewModels.DevicesViewModel.ThisPcName().StartsWith("pc-",StringComparison.Ordinal)) throw new Exception("This PC pairs under a stable, readable name.");
        Directory.Delete(settingsFolder,true); DesktopSettings.Folder=checksRoot;
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        // Capturas del tema visual (E2): cada pestana del puesto principal compuesta con datos leidos.
        var tabs=workspace;
        window.Shell.Service.Board=[entry]; window.Shell.Service.SelectedEntry=entry; window.Shell.Service.Dining=new(3,dto);
        window.Shell.Service.Tables=[new("M1","Mesa 1",12,"sala","Sala"),new("M2","Mesa 2",4,"sala","Sala")]; window.Shell.Service.SelectedTable=window.Shell.Service.Tables[0];
        // E4a: apertura por oferta y eleccion de plato en un pase de menu cerrado; la vista ERP > Oferta se compone de lo leido.
        var sampleOffers=new Costina.Client.OfferChoice[]{new("LAB-TASTING","Menú de ensayo","tasting",[]),new("LAB-DAILY","Menú del día de ensayo","set-menu",[new("primeros","Primeros",[new("ensalada","Ensalada de la huerta"),new("sopa","Sopa del día")]),new("segundos","Segundos",[new("pescado","Pescado del día")])]),
            new("LAB-CARTA","Carta de ensayo","a-la-carte",[new("entrantes","Entrantes",[],[new("croquetas","unit","Croquetas de la casa","Ración")])])};
        window.Shell.Service.ApplyConfiguration(new Costina.Client.Configuration(window.Shell.Service.Tables,null,sampleOffers));
        if(window.Shell.Service.Offers.Length!=3||window.Shell.Service.SelectedOffer?.Id!="LAB-TASTING"||window.Shell.Service.SelectedOffer.ToString()!="Menú de ensayo · Degustación") throw new Exception("Offers fill the open form.");
        if(window.Shell.Service.Offers[2].ToString()!="Carta de ensayo · Carta") throw new Exception("A carta is offered as 'Carta', never as its code: "+window.Shell.Service.Offers[2]);   // C1
        window.Shell.Service.ApplyConfiguration(new Costina.Client.Configuration(window.Shell.Service.Tables,[new("LAB-TASTING","Menú de ensayo")],null));
        if(window.Shell.Service.Offers.Length!=1||window.Shell.Service.Offers[0].Kind!="tasting") throw new Exception("A pre-E4a server with only menus still fills the open form.");
        window.Shell.Service.ApplyConfiguration(new Costina.Client.Configuration(window.Shell.Service.Tables,null,sampleOffers));
        var daily=new Costina.Client.DiningDto("s2","M2",2,"InService",[new("primeros","Primeros","Pending",null,null,null,null,[new("sopa-1","Sopa del día","hot",1,1,true,"Pending")],["skip","choose"],true)],["pause"],[],false,"LAB-DAILY");
        var dailyEntry=new Costina.Client.BoardEntry(4,daily,new("o2","M2","s2","Occupied",null),1);
        window.Shell.Service.Board=[dailyEntry]; window.Shell.Service.SelectedEntry=dailyEntry; window.Shell.Service.Dining=new(4,daily); window.Shell.Service.Courses=daily.Courses; window.Shell.Service.SelectedCourse=daily.Courses[0];
        if(!window.Shell.Service.CanChoose||window.Shell.Service.ChoiceDishes.Length!=2||window.Shell.Service.ChoicesText!="1: Sopa del día · 2: sin elegir") throw new Exception("A set-menu course announcing 'choose' offers its dishes and states who has chosen: "+window.Shell.Service.ChoicesText);
        if(!window.Shell.Service.ChooseCommand.CanExecute(null)) throw new Exception("Choose is enabled from the server affordance.");
        // E4b: un grupo de carta que anuncia add-dish ofrece sus items; sin la affordance no hay fila de pedido.
        var carte=new Costina.Client.DiningDto("s3","M3",2,"Open",[new("entrantes","Entrantes","Pending",null,null,null,null,[],["skip","add-dish"],false,true)],["start"],[],false,"LAB-CARTA");
        var carteEntry=new Costina.Client.BoardEntry(1,carte,new("o3","M3","s3","Occupied",null),1);
        window.Shell.Service.Board=[carteEntry]; window.Shell.Service.SelectedEntry=carteEntry; window.Shell.Service.Dining=new(1,carte); window.Shell.Service.Courses=carte.Courses; window.Shell.Service.SelectedCourse=carte.Courses[0];
        if(!window.Shell.Service.CanAddDish||window.Shell.Service.CanChoose||window.Shell.Service.DishItems.Length!=1||window.Shell.Service.SelectedDishItem?.Key!="croquetas/unit"||!window.Shell.Service.AddDishCommand.CanExecute(null)) throw new Exception("An a la carte group announcing add-dish offers its items.");
        window.Shell.Session=new("kitchen","t","c","l",[],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        var offersView=window.Shell.Offers;
        var sampleOfferDtos=new Costina.Client.OfferDto[]{
            new("LAB-TASTING","Menú de ensayo","tasting","menu-lab-tasting","any",null,null,127,0,true,[new("p1","Aperitivos",0,true,[new("LAB-TASTING","p1","frio","Preparación fría","cold",null,0,true),new("LAB-TASTING","p1","caliente","Preparación caliente","hot",null,1,true)]),new("p2","Segundo pase",1,true,[new("LAB-TASTING","p2","principal","Preparación principal","hot",null,0,true)])]),
            new("LAB-DAILY","Menú del día de ensayo","set-menu","menu-lab-daily","lunch","2026-09-01","2026-12-31",31,1,true,[new("primeros","Primeros",0,true,[new("LAB-DAILY","primeros","ensalada","Ensalada de la huerta","cold",null,0,true),new("LAB-DAILY","primeros","sopa","Sopa del día","hot",null,1,false)])]),
            new("VERANO","Degustación de verano","tasting","menu-verano","dinner",null,null,96,2,false,[]),
            new("LAB-CARTA","Carta de ensayo","a-la-carte",null,"any",null,null,127,3,true,[new("entrantes","Entrantes",0,true,[],[new("croquetas","unit","Croquetas de la casa","Ración","cold",0,true)])])};
        offersView.ItemChoices=[new Costina.Client.ProductChoice("menu-lab-tasting","Menú de ensayo","Por persona",4500,"person","Menús","general"),new Costina.Client.ProductChoice("croquetas","Croquetas de la casa","Ración",900,"unit","Entrantes","general")];
        offersView.Render(sampleOfferDtos,sampleOrganization.Stations);
        if(offersView.OfferProductText!="Se vende por persona como el producto «Menú de ensayo»: su precio se fija en ERP › Catálogo y tarifas.") throw new Exception("The menu product is named, not coded: "+offersView.OfferProductText);   // C1
        if(offersView.Offers.Length!=4||offersView.SelectedOffer?.Offer.Id!="LAB-TASTING"||offersView.Courses.Length!=2||offersView.SelectedCourse?.Course.Id!="p1"||offersView.Dishes.Length!=2||offersView.IsCarte) throw new Exception("Rendering selects the first offer and lists its courses and dishes.");
        offersView.SelectedOffer=offersView.Offers[3];
        if(!offersView.IsCarte||offersView.Items.Length!=1||offersView.Items[0].Item.StationId!="cold"||!offersView.OfferProductText.StartsWith("Carta: sin producto-menú",StringComparison.Ordinal)) throw new Exception("An a la carte offer lists its items per group.");
        if(offersView.Items[0].ToString()!="Croquetas de la casa · Ración · Cocina fría · activo"||offersView.Offers[3].ToString()!="Carta de ensayo · Carta · activa"||offersView.OfferKind!="a-la-carte") throw new Exception("Offer rows name kind and station in Spanish: "+offersView.Items[0]);
        // C1: la ficha de oferta ofrece "Carta" (codigo a-la-carte); una carta nueva no pide precio ni producto-menu.
        var offerKindBox=(ComboBox)((FrameworkElement)window.FindName("OffersView")).FindName("OfferKindBox");
        if(!offerKindBox.Items.OfType<ComboBoxItem>().Any(i=>i.Tag as string=="a-la-carte"&&i.Content as string=="Carta")) throw new Exception("The offer kind dropdown offers 'Carta'.");
        offersView.StartOfferCommand.Execute(null); offersView.OfferKind="a-la-carte";
        if(offersView.NewMenuOffer||!offersView.OfferProductText.StartsWith("Carta:",StringComparison.Ordinal)) throw new Exception("A new carta asks for no menu product.");
        offersView.OfferKind="set-menu";
        if(!offersView.NewMenuOffer||offersView.OfferProductText.StartsWith("Carta:",StringComparison.Ordinal)) throw new Exception("A new menu asks for its price or product.");
        offersView.SelectedOffer=offersView.Offers[0];
        offersView.SelectedOffer=offersView.Offers[1];
        if(offersView.OfferKind!="set-menu"||offersView.OfferService!="lunch"||offersView.OfferValidFrom!="2026-09-01"||offersView.OfferDays[5]||!offersView.OfferDays[0]||offersView.Weekdays!=31) throw new Exception("Selecting an offer fills its form, weekdays included.");
        offersView.SelectedDish=offersView.Dishes[1];
        if(offersView.DishStationId!="hot"||offersView.SelectedDish.StateLabel!="desactivado"||offersView.DishFormTitle!="Plato: Sopa del día"||offersView.SelectedDish.ToString()!="Sopa del día · Cocina caliente · desactivado") throw new Exception("Selecting a dish fills its form and states its status in text: "+offersView.SelectedDish);
        offersView.StartDishCommand.Execute(null);
        if(!offersView.NewDish||offersView.DishId!=""||offersView.DishStationId!="cold") throw new Exception("A new dish proposes the first active kitchen station.");
        window.Shell.Busy=true;
        if(offersView.SaveOfferCommand.CanExecute(null)||window.Shell.Service.ChooseCommand.CanExecute(null)) throw new Exception("Busy must gate offer commands and choices.");
        window.Shell.Busy=false; offersView.SelectedOffer=offersView.Offers[0]; offersView.SelectedCourse=offersView.Courses[0]; offersView.SelectedDish=offersView.Dishes[0];
        // C1: el titulo de la mesa, su fila en la lista y los totales de la cuenta salen de lo leido; ya no se escriben a mano (asi la
        // captura demuestra el castellano y los nombres de mesa en lugar de taparlos).
        window.Shell.Service.Board=[entry]; window.Shell.Service.SelectedEntry=entry; window.Shell.Service.Dining=new(3,dto);
        var servedAt=new DateTimeOffset(2026,10,14,11,5,0,TimeSpan.Zero);
        window.Shell.Service.Courses=[new Costina.Client.CourseDto("c0","Bienvenida","Served",servedAt,servedAt,servedAt,null,[]),new Costina.Client.CourseDto("c1","Aperitivos","Ready",null,null,null,null,[prep],["serve"])]; window.Shell.Service.SelectedCourse=window.Shell.Service.Courses[1];
        window.Shell.Service.Preparations=[prep];
        if(window.Shell.Service.ServiceTitle!="Mesa 1 · 2 personas · En servicio"||window.Shell.Service.BoardItems.Single().Text!="Mesa 1 · 2 personas · En servicio") throw new Exception("The table is named and its state is in Spanish: "+window.Shell.Service.ServiceTitle);
        if(window.Shell.Service.Courses[1].StateLabel!="Listo para servir"||window.Shell.Service.Courses[0].ServedAtLabel!=servedAt.ToLocalTime().ToString("HH:mm")||prep.StateLabel!="Por empezar"||prep.GuestLabel!="Toda la mesa") throw new Exception("Course and preparation states, guest and served time (local) in Spanish.");
        window.Shell.Checkout.Products=[new Costina.Client.ProductChoice("water","Agua mineral","Botella",400,"bottle","Bebidas","general"),new Costina.Client.ProductChoice("wine","Vino de ensayo","Copa",950,"glass","Vinos","general")]; window.Shell.Checkout.SelectedProduct=window.Shell.Checkout.Products[0];
        window.Shell.Checkout.Accounts=[new Costina.Client.AccountChoice("s1","M1","Open")]; window.Shell.Checkout.SelectedAccount=window.Shell.Checkout.Accounts[0];
        window.Shell.Checkout.Charges=[new("ch1","Menú de ensayo",2,15000,false,null,30000),new("ch2","Agua mineral · Botella",1,400,false,null,400)];
        window.Shell.Checkout.Account=new(1,new Costina.Client.AccountDto("a1","s1","Open",30400,0,30400,0,"Unpaid",window.Shell.Checkout.Charges,[],["add-product","payment"],[]));
        if(!window.Shell.Checkout.Totals.StartsWith("Total 304,00 €",StringComparison.Ordinal)||!window.Shell.Checkout.Totals.Contains("Pagado de más 0,00 €")||!window.Shell.Checkout.AccountState.StartsWith("Estado: Abierta · Sin pagar. 0 pagos y 0 devoluciones",StringComparison.Ordinal)
            ||window.Shell.Checkout.AccountItems.Single().Text!="Mesa 1 · Abierta") throw new Exception("The account is named by its table and its totals and state are composed from the read: "+window.Shell.Checkout.AccountState);
        // C1: las capturas muestran cada vista con sus datos de ejemplo (las comprobaciones de niveles las vaciaron al salir del modo
        // responsable) y con un responsable identificado, como en el uso real. Solo datos: ninguna asercion anterior cambia.
        window.Shell.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.34.0-c1",Actor:"user:jefa",Modules:["dining"]));
        organization.Render(sampleOrganization);
        catalogView.Render(sampleCatalog); catalogView.SelectedCategory=catalogView.Categories[1]; catalogView.SelectedProduct=catalogView.Products[0];
        center.Render(stationsForDevices,[pendingOther],activeDevices);
        home.ApplyDiagnostics(soon); home.ApplyDevices(activeDevices);
        window.Shell.Status="Datos leídos del servidor. Las acciones se habilitan según lo que el servidor anuncia.";
        window.Shell.Home.Visible=true;
        foreach(var (tab,file) in new[]{("HomeTab","home.png"),("ServiceTab","service.png"),("CheckoutTab","checkout.png"),("OrganizationTab","configuration.png"),("CatalogTab","catalog.png"),("OffersTab","offers.png"),("DevicesTab","devices.png")})
        {
            tabs.SelectedItem=(TabItem)window.FindName(tab); window.UpdateLayout();
            // Las columnas "*" del DataGrid se reparten en una pasada diferida: se vacia la cola del despachador antes de capturar.
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
            // C1: ningun estado, tipo o actor crudo del servidor en el texto pintado de la vista (las estaciones aun salen por su codigo: C2).
            var leaked=Texts(window).Where(t=>RawValue.IsMatch(t)).ToArray();
            if(leaked.Length>0) throw new Exception(tab+" shows raw server values: "+string.Join(" | ",leaked));
            var shot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); shot.Render(window);
            // C1: sin Mica la ventana pinta un fondo OPACO #F3F4F6 (con Mica la captura sale con huecos transparentes): se mira el
            // margen entre la barra lateral y la vista.
            if(Pixel(shot,workspace.TranslatePoint(new Point(-12,12),window)) is var gutter&&gutter!=Color.FromArgb(255,0xF3,0xF4,0xF6)) throw new Exception(tab+": the window background must be opaque #F3F4F6, found "+gutter);
            var shotEncoder=new PngBitmapEncoder(); shotEncoder.Frames.Add(BitmapFrame.Create(shot));
            using var shotFile=File.Create("artifacts/desktop/"+file); shotEncoder.Save(shotFile);
        }
        // C1: la barra lateral se pliega a iconos (boton de abajo) y se despliega de nuevo; plegada, sus elementos siguen ahi con su ayuda.
        // (Se parte de la barra desplegada: en una pantalla estrecha, como la del CI, la ventana la pliega sola por debajo de 1270 px.)
        tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.Shell.SideNavCompact=false; window.UpdateLayout();
        var sideNavBar=(FrameworkElement)window.FindName("SideNavBar"); var navToggle=(Button)window.FindName("SideNavToggle");
        navToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(!window.Shell.SideNavCompact||sideNavBar.ActualWidth>64||((Button)window.FindName("NavCatalog")).ToolTip as string!="Catálogo y tarifas") throw new Exception("The sidebar folds to icons, each with its name as a tooltip: "+sideNavBar.ActualWidth);
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
        var compactShot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); compactShot.Render(window);
        var compactEncoder=new PngBitmapEncoder(); compactEncoder.Frames.Add(BitmapFrame.Create(compactShot));
        using(var compactFile=File.Create("artifacts/desktop/sidebar-compact.png")) compactEncoder.Save(compactFile);
        navToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(window.Shell.SideNavCompact||sideNavBar.ActualWidth<200) throw new Exception("The sidebar unfolds again.");
        // C1: startup.png es Primeros pasos tal como lo ve un PC recien arrancado: sin aviso pendiente y con el servidor ya comprobado
        // (lo haria StartAsync; los checks no tocan la red).
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.Shell.Status=""; window.Shell.Session=null; window.Shell.ServerReady=true; window.UpdateLayout();
        if(firstRun.Visibility!=Visibility.Visible||!firstRun.IsAncestorOf(loginCard)||((FrameworkElement)window.FindName("SideNavBar")).Visibility!=Visibility.Collapsed) throw new Exception("Disconnected again: Primeros pasos with the login card, no sidebar.");
        if(!window.Shell.FirstRunServerText.StartsWith("El servidor está listo ",StringComparison.Ordinal)||window.Shell.ServerDot!="ok") throw new Exception("Primeros pasos states the server: "+window.Shell.FirstRunServerText);
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        image.Render(window);
        if(Pixel(image,firstRun.TranslatePoint(new Point(8,8),window)) is var firstRunBackground&&firstRunBackground!=Color.FromArgb(255,0xF3,0xF4,0xF6)) throw new Exception("Primeros pasos must paint the opaque #F3F4F6, found "+firstRunBackground);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var file=File.Create("artifacts/desktop/startup.png")) encoder.Save(file);
        // C1: mientras arranca y espera al servidor (corte B) la ventana lo dice en su propia capa, sin Primeros pasos ni entrada.
        window.Shell.Booting=true; window.Shell.BootText="Arrancando el servidor (12 s). Tras encender el equipo puede tardar de uno a tres minutos."; window.UpdateLayout();
        if(((FrameworkElement)window.FindName("BootPanel")).Visibility!=Visibility.Visible||firstRun.Visibility!=Visibility.Collapsed||loginCard.Visibility!=Visibility.Collapsed) throw new Exception("While booting only the boot panel is shown.");
        var bootShot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bootShot.Render(window);
        var bootEncoder=new PngBitmapEncoder(); bootEncoder.Frames.Add(BitmapFrame.Create(bootShot));
        using(var bootFile=File.Create("artifacts/desktop/booting.png")) bootEncoder.Save(bootFile);
        window.Shell.Booting=false; window.Shell.BootText=""; window.UpdateLayout();
        File.WriteAllText("artifacts/desktop/window-checks.txt","Startup, viewmodel affordance/context-mapping and durable-store isolation checks passed. Actual WPF window rendered. No live backend interaction claimed by these checks."
            +Environment.NewLine+"Version: "+Costina.Desktop.ViewModels.ShellViewModel.AppVersion+Environment.NewLine+"Fonts: "+fonts+Environment.NewLine);
        window.Close();app.Shutdown();
        try { Directory.Delete(checksRoot,true); } catch(IOException) { }   // la carpeta de los checks; nunca la real
        Console.WriteLine("WPF startup and viewmodel checks passed");return 0;
    }

    // C1: valores crudos del servidor que nunca deben verse (estados en ingles, tipos de oferta, actores con prefijo). Como palabra
    // suelta: dentro de un codigo con guiones (el producto «menu-lab-tasting») no es un estado.
    private static readonly Regex RawValue=new(@"(?<![\w-])(Open|InService|Paused|Completed|Cancelled|Pending|Fired|Preparing|Ready|Served|Skipped|Occupied|Released|Closed|Unpaid|PartiallyPaid|Paid|Credit|Allergy|Intolerance|Preference|Severe|Moderate|Mild|Unaffected|Adapt|Remake|tasting|set-menu|a-la-carte)(?![\w-])|\b(user|device):",RegexOptions.CultureInvariant);
    // C1: color de un pixel de una captura (coordenadas del cliente de la ventana).
    private static Color Pixel(BitmapSource image,Point at)
    {
        var bgra=new byte[4]; image.CopyPixels(new Int32Rect((int)at.X,(int)at.Y,1,1),bgra,4,0);
        return Color.FromArgb(bgra[3],bgra[2],bgra[1],bgra[0]);
    }
    // Todo el texto pintado (TextBlock) bajo un elemento del arbol visual.
    private static IEnumerable<string> Texts(DependencyObject root)
    {
        var pending=new Stack<DependencyObject>(); pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();
            if(node is TextBlock{Text:{Length:>0} text}) yield return text;
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) pending.Push(VisualTreeHelper.GetChild(node,i));
        }
    }
}
