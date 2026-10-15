using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        // C1 (revision): las muestras llevan restricciones (una alergia conocida y un tipo desconocido, que sale crudo) para que la
        // captura del comedor pinte y compruebe el flujo de alergias.
        Costina.Client.GuestRestrictionDto[] sampleRestrictions=[new("r1",null,"Allergy","gluten","Severe"),new("r2",2,"Anaphylaxis","cacahuete","Critical")];
        var prep=new Costina.Client.PreparationDto("i1","Plato","hot",1,null,true,"Fired",["preparation-start","review-preparation"],sampleRestrictions,true);
        var dto=new Costina.Client.DiningDto("s1","M1",2,"InService",[],["pause","fire-next"],sampleRestrictions,true);
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
        // B.2 (ADR-014): con la conexion sencilla encendida el QR lleva http://IP:PUERTO con la IP de la red del local (nunca la de un
        // adaptador virtual) y nada que instalar; al apagarla la direccion http deja de valer. Con la red de Windows en "Publica" se avisa.
        var simple=new Costina.Desktop.ViewModels.ShellViewModel(); simple.Devices.MemoryFolder=qrFolder; simple.Devices.PairingCode="AbC123_def-456";
        simple.Devices.ApplyNetwork(SimpleOn);
        if(!simple.Devices.SimpleOn||simple.Devices.SimpleOff||simple.Devices.HasNetworkWarning) throw new Exception("The simple connection is on and the network is private.");
        if(simple.Devices.DeviceAddress!="http://192.168.0.81:5089"||simple.Devices.PairingUrl!="http://192.168.0.81:5089/app/#pair=AbC123_def-456"||!simple.Devices.HasQr)
            throw new Exception("With the simple connection the QR carries the LAN IP of the server over http: "+simple.Devices.PairingUrl);
        if(!simple.Devices.QrInfo.Contains("No hay que instalar nada",StringComparison.Ordinal)||!simple.Devices.SimpleText.Contains("sin cifrar",StringComparison.Ordinal)) throw new Exception("The screen says nothing has to be installed and that traffic is not encrypted.");
        simple.Devices.DeviceAddress="http://costina-server.local:5089";
        if(simple.Devices.HasQr) throw new Exception("Over http only a private IPv4 is a device address, never a name (HSTS, resolution outside the venue).");
        simple.Devices.DeviceAddress="http://192.168.0.81:5089";
        simple.Devices.ApplyNetwork(SimpleOn with {LanHttp=new(false,5089,true),Networks=[new("Red 8","public")]});
        if(simple.Devices.DeviceAddress.StartsWith("http://",StringComparison.Ordinal)||simple.Devices.HasQr||!simple.Devices.SimpleOff) throw new Exception("Switched off, an http device address is dropped and there is no QR with it.");
        if(!simple.Devices.NetworkWarning.Contains("«Red 8»",StringComparison.Ordinal)||!simple.Devices.NetworkWarning.Contains("Privada",StringComparison.Ordinal)) throw new Exception("A public Windows network is named, with the way out: "+simple.Devices.NetworkWarning);
        if(simple.Devices.AskEnableSimpleCommand.CanExecute(null)) throw new Exception("Without a responsible session the simple connection cannot be switched on.");
        simple.Devices.ApplyNetwork(null);
        if(simple.Devices.SimpleOn||simple.Devices.SimpleOff||simple.Devices.HasNetworkWarning||simple.Devices.SimpleText.Length!=0) throw new Exception("A server without the simple connection paints nothing about it.");
        Directory.Delete(qrFolder,true); DesktopSettings.Folder=checksRoot;
        // E2: pestana Configuracion (solo main). La pantalla se compone SOLO de lo leido; los comandos siguen la misma tuberia y
        // se habilitan por rol y por estado (ocupado, orden pendiente): nunca por reglas de negocio locales.
        var sampleOrganization=new Costina.Client.OrganizationDto(
            [new("sala","Sala",0,true,[new("M1","Mesa 1",12,"sala",0,true),new("M2","Mesa 2",4,"sala",1,true),new("M3","Mesa 3 (junto a la ventana)",6,"sala",2,false)]),
             new("terraza","Terraza",1,true,[new("T1","Terraza 1",4,"terraza",0,true)],"terraza")],
            [new("pase","Pase","Pass",0,true),new("cold","Cocina fría","Kitchen",1,true),new("hot","Cocina caliente","Kitchen",2,true),new("sala-1","Sala 1","Room",3,false)],
            // E3.2: las tarifas llegan con la organizacion; Eventos esta desactivada y ninguna sala la usa.
            [new("general","General",0,true),new("terraza","Terraza",1,true),new("eventos","Eventos",2,false)]);
        var organization=window.Shell.Organization;
        organization.Render(sampleOrganization);
        if(organization.SaveZoneCommand.CanExecute(null)||organization.StartTableCommand.CanExecute(null)) throw new Exception("Disconnected must gate every organization command.");
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        if(organization.SaveZoneCommand.CanExecute(null)||organization.StartStationCommand.CanExecute(null)) throw new Exception("Only the main desk edits the organization.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]);
        window.UpdateLayout();
        if(((MenuItem)window.FindName("ConfigurationMenu")).Visibility!=Visibility.Visible||((MenuItem)window.FindName("FiscalMenu")).Visibility!=Visibility.Visible) throw new Exception("The main desk sees every menu.");
        // La sesion simulada aqui no lleva persona (sin token de responsable): para la ventana es un puesto con el rol principal antiguo y
        // asi se rotula (revision C1). La persona (Responsable: jefa) se comprueba abajo, en los niveles, con AssumeResponsible.
        if(!window.Shell.ConnectionText.StartsWith("Puesto: Principal (antiguo) · ",StringComparison.Ordinal)) throw new Exception("The status bar states who is connected: "+window.Shell.ConnectionText);
        // C1: con sesion, Primeros pasos se recoge y aparece la barra lateral; Comedor y cocina solo con el modulo de comedor.
        if(firstRun.Visibility!=Visibility.Collapsed||((FrameworkElement)window.FindName("SideNavBar")).Visibility!=Visibility.Visible) throw new Exception("Connected: no Primeros pasos, the sidebar is shown.");
        var navService=(Button)window.FindName("NavService");
        if(navService.Visibility!=Visibility.Visible) throw new Exception("The Comedor sidebar item exists with the dining module.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:[]); window.UpdateLayout();
        if(navService.Visibility!=Visibility.Collapsed||((MenuItem)window.FindName("DiningMenu")).Visibility!=Visibility.Collapsed) throw new Exception("Without the dining module there is no Comedor in the sidebar (nor in the menu).");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.7.0-d3.4",Modules:["dining"]); window.UpdateLayout();
        // C1 (revision): durante una operacion la barra lateral no cambia de vista ni con el teclado (Tab + Intro llega como Click); sin
        // operacion, el mismo Click si la abre.
        var tabsControl=(TabControl)window.FindName("Tabs"); var navCatalog=(Button)window.FindName("NavCatalog");
        tabsControl.SelectedItem=(TabItem)window.FindName("HomeTab"); window.Shell.Busy=true;
        navCatalog.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if(tabsControl.SelectedItem!=(TabItem)window.FindName("HomeTab")) throw new Exception("While busy the sidebar must not change the view (keyboard included).");
        window.Shell.Busy=false;
        navCatalog.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if(tabsControl.SelectedItem!=(TabItem)window.FindName("CatalogTab")) throw new Exception("Not busy, the sidebar opens the view.");
        tabsControl.SelectedItem=(TabItem)window.FindName("HomeTab");
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
        // C1 (regresion de bde15b1): el desplegable de tarifa escribe null cuando su lista se vacia. Elegir despues otra presentacion no
        // revienta y muestra el precio general; fijar un precio sin tarifa se rechaza antes de enviar nada.
        catalogView.PriceTariffId=null!; catalogView.SelectedPresentation=catalogView.Presentations[1];
        if(catalogView.PriceText!="42,00") throw new Exception("Without a tariff chosen the price form shows the general price: "+catalogView.PriceText);
        try { catalogView.SetPriceCommand.ExecuteAsync(null).GetAwaiter().GetResult(); throw new Exception("Pricing without a tariff must be refused."); }
        catch(ArgumentException) { }
        catalogView.PriceTariffId="general"; catalogView.SelectedPresentation=catalogView.Presentations[0];
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
        // E3.2: imagenes de los articulos y tarifa de cada sala. La ventana habla con un servidor SIMULADO en memoria (un HttpMessageHandler):
        // ordenes, relecturas e imagenes pasan por el ApiClient real, sin red ni puerto. Las fotos son botellas dibujadas aqui y guardadas
        // como PNG y JPEG de verdad. Las esperas dejan trabajar al despachador: las imagenes se decodifican fuera de la ventana y vuelven a ella.
        var previousContext=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(window.Dispatcher));
        var greenBottle=Bottle(Color.FromRgb(0x24,0x55,0x35),Color.FromRgb(0xF4,0xEC,0xD8),new PngBitmapEncoder());
        var redBottle=Bottle(Color.FromRgb(0x5E,0x17,0x25),Color.FromRgb(0xEB,0xDC,0xB8),new JpegBitmapEncoder{QualityLevel=90});
        var amberBottle=Bottle(Color.FromRgb(0x9A,0x5B,0x13),Color.FromRgb(0xFF,0xFF,0xFF),new PngBitmapEncoder());
        if(Costina.Client.ProductImages.TypeOf(greenBottle)!="image/png"||Costina.Client.ProductImages.TypeOf(redBottle)!="image/jpeg"||greenBottle.Length>64*1024) throw new Exception("The sample photos must be small, real PNG and JPEG files.");
        string Sha(byte[] content)=>Costina.Client.ProductImages.Sha256(content);
        var imageCatalog=sampleCatalog with {Products=[..sampleCatalog.Products.Select(p=>p.Id switch{"wine"=>p with {ImageSha=Sha(greenBottle)},"3760"=>p with {ImageSha=Sha(redBottle)},_=>p})]};
        var server=new FakeServer(imageCatalog,sampleOrganization);
        void ServeSampleImages(){ server.Images.Clear(); server.Images["wine"]=("image/png",greenBottle); server.Images["3760"]=("image/jpeg",redBottle); }
        ServeSampleImages();
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        var catalogPanel=(FrameworkElement)window.FindName("CatalogView");
        var productList=(ListBox)catalogPanel.FindName("ProductList");
        tabsControl.SelectedItem=(TabItem)window.FindName("CatalogTab");   // abrir la vista relee el catalogo (del servidor simulado)
        Wait(()=>!window.Shell.Busy&&catalogView.Products.Any(p=>p.Product.ImageSha is not null),"opening the catalog reads it");
        catalogView.AllCategories=true; window.UpdateLayout();
        Wait(()=>catalogView.Images.ThumbnailCount==2&&catalogView.Images.Waiting==0,"the list loads the thumbnails of its visible rows");
        var wineRow=catalogView.Products.Single(p=>p.Product.Id=="wine"); var waterRow=catalogView.Products.Single(p=>p.Product.Id=="water");
        if(wineRow.Thumbnail is not {PixelWidth:64}||waterRow.Thumbnail is not null) throw new Exception("A row with an image gets a 64 px thumbnail (32 px painted); a row without one keeps its placeholder.");
        if(server.Count("GET","catalog/images/wine")!=1||server.Count("GET","catalog/images/3760")!=1||server.Count("GET","catalog/images/water")+server.Count("GET","catalog/images/coffee")!=0)
            throw new Exception("Only products with an image are asked for, once each: "+string.Join(", ",server.Requests.Select(r=>r.Path)));
        window.UpdateLayout();
        var wineItem=(ListBoxItem)productList.ItemContainerGenerator.ContainerFromItem(wineRow); var waterItem=(ListBoxItem)productList.ItemContainerGenerator.ContainerFromItem(waterRow);
        if(!Descendants<Border>(wineItem).Any(b=>b.IsVisible&&b.Background is ImageBrush{ImageSource:var shown}&&ReferenceEquals(shown,wineRow.Thumbnail))) throw new Exception("The row paints its thumbnail.");
        if(!Descendants<System.Windows.Shapes.Path>(waterItem).Any(p=>p.IsVisible&&p.Data==(Geometry)Application.Current.FindResource("IconImage"))||Descendants<Border>(waterItem).Any(b=>b.IsVisible&&b.Background is ImageBrush))
            throw new Exception("A row without an image paints the image placeholder icon.");
        if(!Texts(wineItem).Contains("Vino de ensayo")||!Texts(wineItem).Any(t=>t.StartsWith("Vinos · Copa 9,50 €",StringComparison.Ordinal))) throw new Exception("The row shows the name and, below, category, prices and state: "+string.Join(" | ",Texts(wineItem)));
        // Lista virtualizada: con 300 articulos con imagen solo piden la suya las filas que se pintan; al bajar al final llega la ultima.
        var manyProducts=Enumerable.Range(1,300).Select(i=>new Costina.Client.ProductDto($"v{i:000}",$"Vino de muestra {i:000}","tintos","iva-21",null,i,true,
            [new("botella","Botella",0,true,[new("general","1970-01-01",1500+i)])],null,i.ToString("x64"))).ToArray();
        foreach(var p in manyProducts) server.Images[p.Id]=("image/png",greenBottle);
        catalogView.Render(imageCatalog with {Products=manyProducts}); window.UpdateLayout();
        Wait(()=>catalogView.Images.Thumbnail(1.ToString("x64")) is not null&&catalogView.Images.Waiting==0,"the first rows of a long list load");
        var firstAsked=server.Count("GET","catalog/images/v");
        if(firstAsked is 0 or > 80) throw new Exception("A long list asks only for the images of the rows it paints: "+firstAsked+" of 300.");
        productList.ScrollIntoView(catalogView.Products[^1]); window.UpdateLayout();
        Wait(()=>catalogView.Images.Thumbnail(300.ToString("x64")) is not null&&catalogView.Images.Waiting==0,"scrolling to the end loads the last rows");
        if(server.Count("GET","catalog/images/v") is var allAsked&&(allAsked<=firstAsked||allAsked>=300)) throw new Exception("Scrolling asks for the newly painted rows, never for the whole list: "+allAsked);
        catalogView.Render(imageCatalog); productList.ScrollIntoView(catalogView.Products[0]); window.UpdateLayout();
        // Ficha CON imagen: la foto a 140 px (decodificada a 240, su tamano real); la lista y la ficha comparten la misma lectura.
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="wine");
        Wait(()=>catalogView.ProductPhoto is not null,"the product form shows its photo");
        if(catalogView.ProductPhoto is not {PixelWidth:240}||catalogView.PhotoText!=""||server.Count("GET","catalog/images/wine")!=1) throw new Exception("The photo comes from the bytes already read (one GET per sha): "+server.Count("GET","catalog/images/wine"));
        if(!catalogView.ChangeImageCommand.CanExecute(null)||!catalogView.RemoveImageCommand.CanExecute(null)) throw new Exception("The responsible can change and remove the image of a product that has one.");
        var photoWell=(Border)catalogPanel.FindName("ProductPhotoWell"); window.UpdateLayout();
        if(photoWell.ActualWidth!=140||!Descendants<Border>(photoWell).Any(b=>b.IsVisible&&b.Background is ImageBrush{ImageSource:var big}&&ReferenceEquals(big,catalogView.ProductPhoto))) throw new Exception("The form paints the photo in a 140 px rounded well.");
        // Ficha SIN imagen: el hueco con su icono dice "Sin imagen"; se puede poner una, no quitarla.
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="water"); window.UpdateLayout();
        if(catalogView.ProductPhoto is not null||catalogView.PhotoText!="Sin imagen"||!((TextBlock)catalogPanel.FindName("ProductPhotoText")).IsVisible) throw new Exception("Without an image the form says so: "+catalogView.PhotoText);
        if(!catalogView.ChangeImageCommand.CanExecute(null)||catalogView.RemoveImageCommand.CanExecute(null)) throw new Exception("Without an image it can be set, not removed.");
        // Solo el responsable: un puesto de sala no cambia imagenes (el servidor daria 403), ocupado tampoco; un producto nuevo aun no tiene ficha.
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.35.0-e3.2",Modules:["dining"]);
        if(catalogView.ChangeImageCommand.CanExecute(null)||catalogView.RemoveImageCommand.CanExecute(null)) throw new Exception("Image commands are only for the responsible.");
        window.Shell.Session=new("main","t","c","l",["open"],"inst-checks","0.35.0-e3.2",Modules:["dining"]);
        window.Shell.Busy=true;
        if(catalogView.ChangeImageCommand.CanExecute(null)) throw new Exception("Busy gates the image commands.");
        window.Shell.Busy=false;
        catalogView.StartProductCommand.Execute(null);
        if(catalogView.ChangeImageCommand.CanExecute(null)||catalogView.PhotoText!="Guarda el producto para añadirle una imagen.") throw new Exception("A product not saved yet has no image to change: "+catalogView.PhotoText);
        catalogView.SelectedProduct=null; catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="water");
        if(catalogView.NewProduct||!catalogView.ChangeImageCommand.CanExecute(null)) throw new Exception("Choosing a product again leaves the new-product form.");
        // Cambiar imagen: el fichero elegido viaja en base64 con el tipo de sus bytes, por la tuberia de siempre, y se relee el catalogo.
        // La imagen confirmada ya esta en memoria: la relectura no la vuelve a descargar.
        var imageFolder=System.IO.Path.Combine(checksRoot,"imagenes"); Directory.CreateDirectory(imageFolder);
        var amberFile=System.IO.Path.Combine(imageFolder,"agua.png"); File.WriteAllBytes(amberFile,amberBottle);
        catalogView.PickImageFile=()=>amberFile;
        Await(catalogView.ChangeImageCommand.ExecuteAsync(null),"change image");
        var imageSet=server.Requests.Last(r=>r.Method=="POST");
        var setBody=JsonSerializer.Deserialize<JsonElement>(imageSet.Body);
        if(imageSet.Path!="erp/catalog/commands/product-image-set"||setBody.GetProperty("productId").GetString()!="water"||setBody.GetProperty("contentType").GetString()!="image/png"
            ||!Convert.FromBase64String(setBody.GetProperty("data").GetString()!).SequenceEqual(amberBottle)||imageSet.Key is not {Length:32}) throw new Exception("product-image-set carries the product, the type of the bytes and the bytes in base64: "+imageSet.Path);
        if(window.Shell.Status!="Cambiar imagen de Agua mineral: confirmado por el servidor."||window.Shell.Pending is not null) throw new Exception("The change is confirmed by the server: "+window.Shell.Status);
        if(server.Requests.SkipWhile(r=>r!=imageSet).Skip(1).FirstOrDefault()?.Path!="erp/catalog") throw new Exception("After the command the catalog is read again.");
        Wait(()=>catalogView.ProductPhoto is not null&&catalogView.Products.Single(p=>p.Product.Id=="water").Thumbnail is not null,"the new image appears in the form and the list");
        if(server.Count("GET","catalog/images/water")!=0) throw new Exception("The confirmed image is never downloaded again.");
        var posts=server.Count("POST","");
        File.WriteAllBytes(amberFile,[..amberBottle[..8],..new byte[Costina.Client.ProductImages.MaxBytes]]);
        Await(catalogView.ChangeImageCommand.ExecuteAsync(null),"too large");
        if(!window.Shell.Status.StartsWith("La imagen ocupa 2,1 MB; el máximo es 2 MB.",StringComparison.Ordinal)||server.Count("POST","")!=posts) throw new Exception("Over 2 MB is refused before sending: "+window.Shell.Status);
        File.WriteAllText(amberFile,"<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        Await(catalogView.ChangeImageCommand.ExecuteAsync(null),"not an image");
        if(!window.Shell.Status.StartsWith("El fichero no es una imagen JPEG, PNG o WebP.",StringComparison.Ordinal)||server.Count("POST","")!=posts) throw new Exception("An SVG named .png is refused by its bytes before sending: "+window.Shell.Status);
        catalogView.PickImageFile=()=>null;
        Await(catalogView.ChangeImageCommand.ExecuteAsync(null),"cancelled dialog");
        if(server.Count("POST","")!=posts) throw new Exception("Cancelling the dialog sends nothing.");
        // Quitar imagen: una orden normal con solo el producto; despues la ficha y la fila vuelven al hueco.
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="wine");
        Await(catalogView.RemoveImageCommand.ExecuteAsync(null),"remove image");
        var imageRemoved=server.Requests.Last(r=>r.Method=="POST");
        if(imageRemoved.Path!="erp/catalog/commands/product-image-remove"||imageRemoved.Body!="{\"productId\":\"wine\"}") throw new Exception("product-image-remove carries only the product: "+imageRemoved.Body);
        if(catalogView.ProductPhoto is not null||catalogView.PhotoText!="Sin imagen"||catalogView.RemoveImageCommand.CanExecute(null)||catalogView.Products.Single(p=>p.Product.Id=="wine").Thumbnail is not null)
            throw new Exception("After removing, the form and the row show the placeholder: "+catalogView.PhotoText);
        // Revision E3.2: la fila es una clase y compara por referencia. Teclear en la busqueda o tocar "Ver todos los productos" con la
        // vista abierta conserva el producto elegido (por su codigo); si la busqueda lo deja fuera se deselecciona del todo y ni
        // "Fijar precio" ni desactivar la presentacion quedan activos sobre un producto que ya no esta elegido (antes: excepcion).
        var searchBox=(TextBox)catalogPanel.FindName("ProductSearch");
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="wine"); window.UpdateLayout();
        searchBox.Text="ensayo"; window.UpdateLayout();
        if(catalogView.SelectedProduct?.Product.Id!="wine"||!ReferenceEquals(productList.SelectedItem,catalogView.SelectedProduct)||catalogView.ProductFormTitle!="Producto: Vino de ensayo"
            ||catalogView.SelectedPresentation is null||!catalogView.SetPriceCommand.CanExecute(null)||!catalogView.TogglePresentationCommand.CanExecute(null))
            throw new Exception("A search that still matches keeps the selected product: "+(catalogView.SelectedProduct?.Product.Id??"null"));
        catalogView.AllCategories=false; window.UpdateLayout(); catalogView.AllCategories=true; window.UpdateLayout();
        if(catalogView.SelectedProduct?.Product.Id!="wine"||!ReferenceEquals(productList.SelectedItem,catalogView.SelectedProduct)) throw new Exception("'Ver todos los productos' keeps the selected product.");
        searchBox.Text="rapolao"; window.UpdateLayout();
        if(catalogView.SelectedProduct is not null||productList.SelectedItem is not null||catalogView.ProductFormTitle!="Selecciona un producto"||catalogView.ProductName!=""
            ||catalogView.PhotoText!="Sin producto seleccionado."||catalogView.Presentations.Length!=0||catalogView.SelectedPresentation is not null
            ||catalogView.SetPriceCommand.CanExecute(null)||catalogView.TogglePresentationCommand.CanExecute(null)||catalogView.ChangeImageCommand.CanExecute(null))
            throw new Exception("A search that hides the selected product deselects it completely: "+catalogView.ProductFormTitle);
        searchBox.Text=""; window.UpdateLayout();
        if(catalogView.Products.Length!=4||catalogView.SelectedProduct is not null) throw new Exception("Clearing the search lists every product again.");
        // Configuracion > Salas: la tarifa de cada sala. Desplegable con la General y las tarifas activas; la lista nombra la tarifa propia.
        var organizationPanel=(FrameworkElement)window.FindName("OrganizationView");
        var zoneTariffBox=(ComboBox)organizationPanel.FindName("ZoneTariffBox");
        tabsControl.SelectedItem=(TabItem)window.FindName("OrganizationTab");
        Wait(()=>!window.Shell.Busy&&server.Count("GET","organization")>0,"opening the configuration reads the organization");
        if(organization.Zones[0].ToString()!="Sala · 2 mesas activas · activa"||organization.Zones[1].ToString()!="Terraza · tarifa Terraza · 1 mesa activa · activa") throw new Exception("The zone list names a zone's own tariff (never 'General'): "+organization.Zones[1]);
        if(!organization.TariffChoices.Select(t=>t.Name).SequenceEqual(["General","Terraza"])) throw new Exception("The zone tariff offers the General and the ACTIVE tariffs: "+string.Join(",",organization.TariffChoices.Select(t=>t.Name)));
        organization.SelectedZone=organization.Zones[1]; window.UpdateLayout();
        if(organization.ZoneTariffId!="terraza"||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"Terraza"}||!zoneTariffBox.IsEnabled) throw new Exception("Selecting a zone shows its tariff.");
        organization.ZoneTariffId="";
        Await(organization.SaveZoneCommand.ExecuteAsync(null),"save zone with the general tariff");
        if(server.Requests.Last(r=>r.Method=="POST") is not {Path:"organization/commands/zone-update",Body:"{\"id\":\"terraza\",\"name\":\"Terraza\",\"sort\":1,\"tariffId\":\"\"}"}) throw new Exception("Back to the General: tariffId empty. "+server.Requests.Last(r=>r.Method=="POST").Body);
        organization.SelectedZone=organization.Zones[0]; window.UpdateLayout();
        if(organization.ZoneTariffId!=""||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"General"}) throw new Exception("A zone without its own tariff shows General.");
        organization.ZoneTariffId="terraza";
        Await(organization.SaveZoneCommand.ExecuteAsync(null),"save zone with its own tariff");
        if(server.Requests.Last(r=>r.Method=="POST") is not {Path:"organization/commands/zone-update",Body:"{\"id\":\"sala\",\"name\":\"Sala\",\"sort\":0,\"tariffId\":\"terraza\"}"}) throw new Exception("A zone saves its tariff by code: "+server.Requests.Last(r=>r.Method=="POST").Body);
        organization.StartZoneCommand.Execute(null); window.UpdateLayout();
        if(organization.ZoneTariffId!=""||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"General"}) throw new Exception("A new zone starts with the General tariff.");
        // Una sala que conserva una tarifa desactivada la ve marcada en SU ficha (no se cambia sin querer); ni una sala nueva ni otra sala
        // la ofrecen (el servidor la rechazaria: tariff_inactive). Un servidor anterior (sin tarifas) muestra el codigo de la tarifa de la sala.
        string Choices()=>string.Join(",",organization.TariffChoices.Select(t=>t.Name));
        organization.Render(sampleOrganization with {Zones=[..sampleOrganization.Zones,new("jardin","Jardín",2,false,[],"eventos")]}); window.UpdateLayout();
        if(Choices()!="General,Terraza"||organization.Zones[2].ToString()!="Jardín · tarifa Eventos · 0 mesas activas · desactivada") throw new Exception("A new zone is offered the General and the active tariffs only: "+Choices());
        organization.SelectedZone=organization.Zones[2]; window.UpdateLayout();
        if(Choices()!="General,Terraza,Eventos (desactivada)"||organization.ZoneTariffId!="eventos"||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"Eventos (desactivada)"})
            throw new Exception("A deactivated tariff still used by a zone is offered, marked, in that zone's form: "+Choices());
        organization.SelectedZone=organization.Zones[0]; window.UpdateLayout();
        if(Choices()!="General,Terraza"||organization.ZoneTariffId!=""||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"General"}) throw new Exception("Another zone is never offered a deactivated tariff: "+Choices());
        organization.SelectedZone=organization.Zones[2]; window.UpdateLayout(); organization.SelectedZone=organization.Zones[1]; window.UpdateLayout();
        if(Choices()!="General,Terraza"||organization.ZoneTariffId!="terraza"||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"Terraza"}) throw new Exception("Leaving that zone drops the deactivated tariff from the choices: "+Choices());
        organization.Render(sampleOrganization with {Tariffs=null}); window.UpdateLayout();
        if(Choices()!="General,terraza"||organization.ZoneTariffId!="terraza"||!organization.Zones[1].ToString().Contains("tarifa terraza")) throw new Exception("An older server without tariffs still shows the zone's tariff by code: "+Choices());
        organization.SelectedZone=organization.Zones[0]; window.UpdateLayout();
        if(Choices()!="General") throw new Exception("An older server offers only the General to a zone without its own tariff: "+Choices());
        window.Shell.Busy=true;
        if(organization.SaveZoneCommand.CanExecute(null)) throw new Exception("Busy gates saving a zone.");
        window.Shell.Busy=false;
        organization.Render(sampleOrganization);
        window.Shell.Api.Dispose(); window.Shell.Api=null;
        tabsControl.SelectedItem=(TabItem)window.FindName("HomeTab");
        SynchronizationContext.SetSynchronizationContext(previousContext);
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
        // Sesion simulada sin persona (ver arriba): Inicio la nombra como dispositivo antiguo; la persona se comprueba en los niveles.
        if(!home.WhoText.StartsWith("Principal (antiguo)",StringComparison.Ordinal)) throw new Exception("Inicio states who is connected: "+home.WhoText);
        home.ApplyDevices([new("d1","tablet-sala-1","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null,true),new("d2","tablet-sala-2","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null),
            new("d3","pantalla-fria","kitchen","cold","user:jefa",DateTimeOffset.Now.AddDays(-1),null,true),new("d4","vieja","kitchen","hot","user:jefa",DateTimeOffset.Now.AddDays(-9),DateTimeOffset.Now.AddDays(-2))]);
        // C2: Inicio lista los dispositivos ACTIVOS (comanderos primero) con su conexion en palabras; los quitados solo en Dispositivos.
        if(home.DevicesSummary!="2 de 3 con conexión"||!home.DeviceRows.Select(r=>r.Name+" "+r.State+" "+r.Dot).SequenceEqual(["tablet-sala-1 Conectado ok","tablet-sala-2 Sin conexión off","pantalla-fria Conectado ok"])
            ||home.DevicesMore.Length!=0||home.DevicesEmpty.Length!=0) throw new Exception("Inicio lists active devices with their presence: "+home.DevicesSummary+" / "+string.Join(", ",home.DeviceRows.Select(r=>r.Name+" "+r.State)));
        home.ApplyDevices([]);
        if(home.DeviceRows.Length!=0||home.DevicesSummary.Length!=0||!home.DevicesEmpty.StartsWith("Ningún dispositivo conectado todavía",StringComparison.Ordinal)) throw new Exception("Without devices Inicio says so.");
        home.ApplyDevices([..Enumerable.Range(1,7).Select(i=>new Costina.Client.DeviceRowDto("x"+i,"tablet-"+i,"service","sala-1","user:jefa",DateTimeOffset.Now,null,i%2==0)),
            new("old","pc-viejo","main","","user:jefa",DateTimeOffset.Now,null)]);
        if(home.DeviceRows.Length!=Costina.Desktop.ViewModels.HomeViewModel.DevicesShown||home.DevicesMore!="Y 3 dispositivos más en Dispositivos."||home.DevicesSummary!="3 de 8 con conexión"
            ||!home.DevicesWarning.StartsWith("1 dispositivo antiguo con rol principal",StringComparison.Ordinal)) throw new Exception("A long list is cut, counted, and a legacy main device is warned about: "+home.DevicesMore+" / "+home.DevicesWarning);
        // C2: saludo segun la hora local y servicio del dia con la regla de las ofertas (comida hasta las 17:00).
        if(Costina.Desktop.ViewModels.HomeViewModel.Greeting(new DateTime(2026,10,14,9,0,0))!="Buenos días"||Costina.Desktop.ViewModels.HomeViewModel.Greeting(new DateTime(2026,10,14,15,0,0))!="Buenas tardes"
            ||Costina.Desktop.ViewModels.HomeViewModel.Greeting(new DateTime(2026,10,14,22,0,0))!="Buenas noches"||Costina.Desktop.ViewModels.HomeViewModel.Greeting(new DateTime(2026,10,15,3,0,0))!="Buenas noches")
            throw new Exception("The greeting follows the local time.");
        if(Costina.Desktop.ViewModels.HomeViewModel.DateText(new DateTime(2026,10,14,21,30,0),true)!="Miércoles, 14 de octubre · Servicio de cena"
            ||Costina.Desktop.ViewModels.HomeViewModel.DateText(new DateTime(2026,10,15,16,59,0),true)!="Jueves, 15 de octubre · Servicio de comida"
            ||Costina.Desktop.ViewModels.HomeViewModel.DateText(new DateTime(2026,10,15,17,0,0),true)!="Jueves, 15 de octubre · Servicio de cena"
            ||Costina.Desktop.ViewModels.HomeViewModel.DateText(new DateTime(2026,10,15,17,0,0),false)!="Jueves, 15 de octubre") throw new Exception("Date and service of the day: lunch until 17:00, like the offers.");
        var soon=new Costina.Client.DiagnosticsDto("installation","0.37.0-c2",DateTimeOffset.Now,new(true,5443,DateTimeOffset.UtcNow.AddDays(12).AddMinutes(5),"costina-server.local"),new(true,DateTimeOffset.Now.AddHours(-3)),true);
        home.ApplyDiagnostics(soon);
        if(!home.HasCertificateWarning||!home.CertificateWarning.Contains("12 días")||!home.ServerDetail.Contains("costina-server.local")||home.ServerText.Contains("http")||!home.BackupText.StartsWith("Última copia",StringComparison.Ordinal)) throw new Exception("Inicio warns about a certificate expiring within 30 days and states server and backup: "+home.CertificateWarning);
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
        // C1 (revision): un dispositivo antiguo con rol principal es un dispositivo, no una persona: nunca se rotula "Responsable".
        levels.Session=new("main","t","c","l",["open"],"inst-checks","0.33.0-b1",Actor:"device:pc-viejo",Modules:["dining"]);
        if(!levels.ConnectionText.StartsWith("Puesto: pc-viejo · Principal (antiguo) · ",StringComparison.Ordinal)||levels.ConnectionText.Contains("Responsable")||!levels.Home.WhoText.StartsWith("Principal (antiguo) pc-viejo",StringComparison.Ordinal))
            throw new Exception("A legacy main device is labelled as a device: "+levels.ConnectionText+" / "+levels.Home.WhoText);
        levels.Session=new("service","t","c","l",["open"],"inst-checks","0.33.0-b1",Actor:"device:pc-principal",StationName:"Sala 1",Modules:["dining"]); window.UpdateLayout();
        if(((MenuItem)window.FindName("CheckoutMenu")).Visibility!=Visibility.Visible||((MenuItem)window.FindName("ErpMenu")).Visibility!=Visibility.Visible) throw new Exception("Every menu is visible at the Puesto level (decision 3: nothing hidden).");
        if(!Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("CheckoutTab")||!Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("DevicesTab")||Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("ServiceTab")||Costina.Desktop.ViewModels.ShellViewModel.RequiresResponsible("HomeTab")) throw new Exception("Caja, configuracion, ERP and devices are the responsible's; dining and home are not.");
        if(levels.LoginCardVisible||levels.ElevationRequested) throw new Exception("Connected as Puesto, no login card until something sensitive is asked.");
        levels.RequestElevation("CheckoutTab");
        if(!levels.ElevationRequested||!levels.LoginCardVisible||!levels.LoginTitle.Contains("Cuenta / Caja")||!levels.ConnectEnabled) throw new Exception("Asking for a sensitive view opens the responsible login naming the view: "+levels.LoginTitle);
        window.UpdateLayout();
        if(((Border)window.FindName("LoginCard")).Visibility!=Visibility.Visible||((Button)window.FindName("CancelElevationButton")).Visibility!=Visibility.Visible) throw new Exception("The login card and 'seguir como puesto' are shown.");
        // C1 (revision): con la tarjeta abierta el aviso va solo en ella (no dos veces) y el velo tapa TODA la ventana (menus y barra de estado).
        if(((FrameworkElement)window.FindName("StatusBarInfo")).Visibility!=Visibility.Collapsed||levels.Status.Length==0) throw new Exception("While the login card is up the request is said once, in the card.");
        if(((FrameworkElement)window.FindName("ElevationLayer")).ActualHeight!=((FrameworkElement)window.Content).ActualHeight) throw new Exception("The scrim covers the whole window, top bar and status bar included.");
        RefuseRaw(window,"elevation");
        // Revision C2: sin el tablero leido Inicio no presenta un comedor vacio: ni "0 de 0" ni "Nada por revisar" (falso negativo de alergias).
        if(levels.Service.BoardRead||levels.Home.OccupiedText!="—"||levels.Home.TablesText.Length!=0||levels.Home.ReviewText!="—"||levels.Home.ReviewDetail!="Sin leer todavía"||levels.Home.ReviewAlarm
            ||!levels.Home.FloorEmpty.StartsWith("Sin leer todavía",StringComparison.Ordinal)||Texts(window).Contains("Nada por revisar")) throw new Exception("Before any read Inicio says it has not read the floor: "+levels.Home.ReviewDetail+" / "+levels.Home.FloorEmpty);
        Save(Shot(window),"elevation.png");
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
        if(!levels.IsResponsible||levels.LevelText!="Responsable"||!levels.LeaveResponsibleCommand.CanExecute(null)||!levels.ConnectionText.StartsWith("Responsable: jefa · ",StringComparison.Ordinal)||!levels.Home.WhoText.StartsWith("Responsable: jefa",StringComparison.Ordinal)) throw new Exception("A person session is the Responsable level: "+levels.ConnectionText);
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
        if(levels.Service.BoardRead||levels.Home.ReviewDetail!="Sin leer todavía"||levels.Home.Tiles.Length!=0) throw new Exception("After leaving a level the floor is unread again, never 'Nada por revisar': "+levels.Home.ReviewDetail);
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
        // C2: la ficha y la tarjeta salen de la mesa LEIDA (en servicio y sin ningun pase enviado todavia: "Sentados").
        if(window.Shell.Service.ServiceTitle!="Mesa 1 · 2 personas · En servicio"||window.Shell.Service.DetailTitle!="Mesa 1"||window.Shell.Service.DetailState!="Sentados"
            ||window.Shell.Service.Tiles.Single(t=>t.Occupied).StateLabel!="Sentados") throw new Exception("The table is named and its state is in Spanish: "+window.Shell.Service.ServiceTitle);
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
        organization.Render(sampleOrganization); organization.SelectedZone=organization.Zones[1];   // E3.2: la sala con tarifa propia
        // E3.2: el catalogo de la captura lleva fotos (lista y ficha), cargadas por la tuberia real desde el servidor simulado; despues la
        // ventana se queda sin conexion, como en el resto de capturas.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
        server.Catalog=imageCatalog; ServeSampleImages();
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        catalogView.Render(imageCatalog); tabs.SelectedItem=(TabItem)window.FindName("CatalogTab");
        Wait(()=>!window.Shell.Busy,"the catalog is read for the capture");
        catalogView.SelectedCategory=null; catalogView.AllCategories=true; catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="wine"); window.UpdateLayout();
        Wait(()=>catalogView.ProductPhoto is not null&&catalogView.Products.Where(p=>p.Product.ImageSha is not null).All(p=>p.Thumbnail is not null)&&catalogView.Images.Waiting==0,"the capture shows the photos");
        window.Shell.Api.Dispose(); window.Shell.Api=null;
        SynchronizationContext.SetSynchronizationContext(previousContext);
        // ===== C2 (plan 2026-10-14): Comedor con plano de mesas y ficha =====
        // Datos de ejemplo COHERENTES: la configuracion (Sala principal con 8 mesas y Terraza con 4, ofertas y estaciones con nombre), el
        // tablero (siete mesas ocupadas en estados distintos, una con un cambio de alergia sin revisar) y las cuentas de Caja son del mismo
        // comedor. El servidor simulado los sirve: elegir una tarjeta pasa por la lectura de siempre (F01). El reloj queda fijo para que los
        // minutos de cada mesa sean exactos.
        var c2Now=DateTimeOffset.UtcNow; window.Shell.Clock=()=>c2Now;
        DateTimeOffset? Ago(int? minutes)=>minutes is {} m?c2Now.AddMinutes(-m):null;
        Costina.Client.PreparationDto Prep(string id,string name,string station,int? guest,string state,string[]? actions=null,Costina.Client.GuestRestrictionDto[]? restrictions=null,bool review=false)
            =>new(id,name,station,1,guest,true,state,actions??[],restrictions??[],review);
        Costina.Client.CourseDto Course(string id,string name,string state,int? fired,int? ready,int? served,Costina.Client.PreparationDto[] preps,string[]? actions=null,bool choice=false,bool optional=false)
            =>new(id,name,state,Ago(fired),Ago(ready),Ago(served),null,preps,actions??[],choice,optional);
        Costina.Client.BoardEntry Seat(string id,string table,int pax,string state,string offer,Costina.Client.CourseDto[] courses,string[] actions,Costina.Client.GuestRestrictionDto[]? restrictions=null,bool pendingReview=false,string[]? occupancy=null)
            =>new(5,new(id,table,pax,state,courses,actions,restrictions??[],pendingReview,offer),new("o-"+id,table,id,"Occupied",null,occupancy??[]),2);
        Costina.Client.GuestRestrictionDto nuts=new("r-nuts",2,"Allergy","frutos secos","Severe"),lactose=new("r-lactosa",3,"Intolerance","lactosa","Mild"),unknownKind=new("r-raro",1,"Anaphylaxis","cacahuete","Critical");
        var diningConfig=new Costina.Client.Configuration(
            [..new[]{("M1",2),("M2",4),("M3",2),("M4",4),("M5",6),("M6",2),("M7",2),("M8",4)}.Select(t=>new Costina.Client.TableChoice(t.Item1,"Mesa "+t.Item1[1..],t.Item2,"sala","Sala principal")),
             ..new[]{("T1",4),("T2",2),("T3",4),("T4",6)}.Select(t=>new Costina.Client.TableChoice(t.Item1,"Terraza "+t.Item1[1..],t.Item2,"terraza","Terraza"))],
            null,
            [new("DEGUSTACION","Degustación Costiña","tasting",[]),
             new("DIA","Menú del día","set-menu",[new("entrantes","Entrantes",[new("ensalada","Ensalada de la huerta")]),new("principales","Principales",[new("merluza","Merluza a la gallega"),new("carrillera","Carrillera al vino")]),new("postres","Postres",[new("tarta","Tarta de queso"),new("flan","Flan de huevo")])]),
             new("CARTA","Carta","a-la-carte",[new("entrantes","Entrantes",[],[new("pulpo","racion","Pulpo á feira","Ración"),new("croquetas","racion","Croquetas de la casa","Ración")]),new("principales","Principales",[],[new("lubina","racion","Lubina a la plancha","Ración")]),new("postres","Postres",[],[new("filloas","racion","Filloas","Ración")])])],
            [new("pase","Pase","pass"),new("cold","Cocina fría","kitchen"),new("hot","Cocina caliente","kitchen"),new("sala-1","Sala 1","room"),new("terraza","Terraza","room")]);
        string[] tastingCourses=["Aperitivos","Mar","Arroz","Pescado","Carne","Postre"];
        Costina.Client.BoardEntry[] diningBoard=[
            Seat("s1","M1",2,"InService","DEGUSTACION",[
                Course("aperitivos","Aperitivos","Served",48,44,40,[Prep("s1-aperitivos","Aperitivos de la casa","cold",null,"Ready")]),
                Course("mar","Mar","Served",35,31,28,[Prep("s1-mar","Zamburiñas a la plancha","hot",null,"Ready")]),
                Course("arroz","Arroz","Served",22,18,15,[Prep("s1-arroz","Arroz de bogavante","hot",null,"Ready")]),
                Course("pescado","Pescado","Preparing",12,null,null,[Prep("s1-rodaballo","Rodaballo al horno","hot",null,"Preparing",["preparation-ready"]),Prep("s1-salsa","Salsa de vieiras","pase",null,"Fired",["preparation-start","preparation-ready"])]),
                Course("carne","Carne","Pending",null,null,null,[Prep("s1-carne","Lomo de vaca gallega","hot",null,"Pending")],["skip"]),
                Course("postre","Postre","Pending",null,null,null,[Prep("s1-filloas","Filloas","cold",null,"Pending")],["skip"])],
                ["pause","declare-restriction"]),
            Seat("s3","M3",2,"InService","CARTA",[
                Course("entrantes","Entrantes","Ready",20,3,null,[Prep("s3-pulpo","Pulpo á feira","cold",null,"Ready"),Prep("s3-croquetas","Croquetas de la casa","hot",null,"Ready")],["serve"],optional:true),
                Course("principales","Principales","Pending",null,null,null,[],["skip","add-dish"],optional:true),
                Course("postres","Postres","Pending",null,null,null,[],["skip","add-dish"],optional:true)],
                ["pause","declare-restriction"]),
            Seat("s4","M4",4,"InService","DIA",[
                Course("entrantes","Entrantes","Served",60,54,50,[..Enumerable.Range(1,4).Select(g=>Prep("s4-ensalada-"+g,"Ensalada de la huerta","cold",g,"Ready"))]),
                Course("principales","Principales","Preparing",25,null,null,[Prep("s4-merluza-1","Merluza a la gallega","hot",1,"Ready"),
                    Prep("s4-merluza-2","Merluza a la gallega","hot",2,"Preparing",["preparation-ready","review-preparation"],[nuts],true),
                    Prep("s4-carrillera-3","Carrillera al vino","hot",3,"Ready",null,[lactose]),Prep("s4-carrillera-4","Carrillera al vino","hot",4,"Fired",["preparation-start","preparation-ready"])]),
                Course("postres","Postres","Pending",null,null,null,[Prep("s4-tarta-1","Tarta de queso","cold",1,"Pending"),Prep("s4-flan-2","Flan de huevo","cold",2,"Pending",null,[nuts])],["skip","choose"],choice:true)],
                ["pause","declare-restriction","remove-restriction"],[nuts,lactose],pendingReview:true),
            Seat("s6","M6",2,"Open","DEGUSTACION",[..tastingCourses.Select((n,i)=>Course("d"+i,n,"Pending",null,null,null,[Prep("s6-"+i,n,"hot",null,"Pending")],["skip"]))],["start","cancel-unstarted","declare-restriction"]),
            Seat("s7","M7",2,"Paused","CARTA",[
                Course("entrantes","Entrantes","Served",30,26,22,[Prep("s7-pulpo","Pulpo á feira","cold",null,"Ready")],optional:true),
                Course("principales","Principales","Pending",null,null,null,[Prep("s7-lubina","Lubina a la plancha","hot",null,"Pending",null,[unknownKind])],["skip","add-dish"],optional:true)],
                ["resume","declare-restriction","remove-restriction"],[unknownKind]),
            Seat("s8","M8",3,"Completed","DIA",[..new[]{"Entrantes","Principales","Postres"}.Select((n,i)=>Course("c"+i,n,"Served",95-25*i,90-25*i,85-25*i,[]))],[],null,false,["release"]),
            Seat("st1","T1",4,"InService","CARTA",[
                Course("entrantes","Entrantes","Served",20,12,8,[Prep("st1-pulpo","Pulpo á feira","cold",null,"Ready")],optional:true),
                Course("principales","Principales","Pending",null,null,null,[Prep("st1-lubina-1","Lubina a la plancha","hot",null,"Pending"),Prep("st1-lubina-2","Lubina a la plancha","hot",null,"Pending")],["skip","add-dish"],optional:true)],
                ["pause","fire-next","declare-restriction"])];
        // Lo que el servidor anuncia a este PC en el nivel Puesto (dispositivo de sala con una estacion de sala; Affordances.Filter): sin
        // terminar, cancelar ni liberar, sin validar el pase (solo 'pase') y sin acciones sobre elaboraciones de otra estacion ni revisiones.
        static Costina.Client.BoardEntry AsStation(Costina.Client.BoardEntry e)=>e with {
            Service=e.Service with {Actions=[..(e.Service.Actions??[]).Where(a=>a is not ("complete" or "cancel-unstarted"))],
                Courses=[..e.Service.Courses.Select(c=>c with {Actions=[..(c.Actions??[]).Where(a=>a!="ready")],Preparations=[..c.Preparations.Select(p=>p with {Actions=[]})]})]},
            Occupancy=e.Occupancy with {Actions=[]}};
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
        server.Configuration=diningConfig; server.Board=diningBoard;
        server.Accounts=[..diningBoard.Select(b=>new Costina.Client.AccountChoice(b.Service.Id,b.Service.TableId,"Open"))];
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        var service=window.Shell.Service; var serviceView=(FrameworkElement)window.FindName("ServiceView");
        Button TileButton(string name){ window.UpdateLayout(); return Descendants<Button>(serviceView).FirstOrDefault(b=>b.DataContext is Costina.Desktop.ViewModels.TableTile t&&t.Name==name)??throw new Exception("No tile painted for "+name); }
        Button Named(string name)=>(Button)serviceView.FindName(name);
        void Pick(string name,string serviceId){ Press(TileButton(name),"tile "+name); Wait(()=>!window.Shell.Busy&&service.Dining?.Data.Id==serviceId&&service.SelectedEntry?.Service.Id==serviceId,"clicking "+name+" reads that table"); window.UpdateLayout(); }
        string Actions()=>service.PrimaryAction+" | "+string.Join(",",service.SecondaryActions.Select(a=>a.Action+(a.Danger?"!":"")));
        window.Shell.Status=""; tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"refresh reads the configuration and the board");
        if(server.Count("GET","configuration")==0||service.Tiles.Length!=12||service.Tiles.Count(t=>t.Occupied)!=7) throw new Exception("One tile per ACTIVE table of the configuration, crossed with the board: "+service.Tiles.Length);
        if(!service.ZoneTabs.Select(z=>z.Label).SequenceEqual(["Todas las salas · 12","Sala principal · 8","Terraza · 4"])||!service.Groups.Select(g=>g.Name+": "+g.Summary).SequenceEqual(["Sala principal: 8 mesas · 6 ocupadas","Terraza: 4 mesas · 1 ocupada"]))
            throw new Exception("Tiles are grouped by zone and the zones are tabs with their counts: "+string.Join(",",service.Groups.Select(g=>g.Name+": "+g.Summary)));
        // Revision C2: la sala elegida se pinta como elegida (IsCurrent del viewmodel, no la seleccion de la lista, que se perdia).
        if(!service.ZoneTabs[0].IsCurrent||service.ZoneTabs.Count(z=>z.IsCurrent)!=1) throw new Exception("'Todas las salas' is painted as the chosen tab.");
        if(!service.Filters.Select(f=>f.Label).SequenceEqual(["Todas · 12","Ocupadas · 7","En cocina · 2","Para servir · 1","Libres · 5"])||service.FloorSummary!="7 de 12 mesas ocupadas · 19 personas")
            throw new Exception("Filter chips and the summary carry their counts: "+string.Join(",",service.Filters.Select(f=>f.Label))+" / "+service.FloorSummary);
        string States()=>string.Join(" | ",service.Tiles.Select(t=>t.Name+": "+t.StateLabel+(t.Elapsed.Length>0?" "+t.Elapsed:"")+(t.HasAlert?" ["+t.Alert+"]":"")+(t.HasSteps?" "+t.Progress:"")));
        if(States()!="Mesa 1: En cocina 12 min 3 de 6 | Mesa 2: Libre | Mesa 3: Listo para servir 3 min 0 de 3 | Mesa 4: En cocina 25 min [Alergia · por revisar] 1 de 3 | Mesa 5: Libre | Mesa 6: Sentados 0 de 6"
            +" | Mesa 7: En pausa [Anaphylaxis (Critical)] 1 de 2 | Mesa 8: Terminado, por liberar 3 de 3 | Terraza 1: Comiendo 8 min 1 de 2 | Terraza 2: Libre | Terraza 3: Libre | Terraza 4: Libre")
            throw new Exception("Tile states, minutes, alerts and course progress in Spanish, from the read data: "+States());
        if(!service.Tiles.Single(t=>t.Name=="Mesa 4").AlertAlarming||!service.Tiles.Single(t=>t.Name=="Mesa 7").AlertAlarming||service.Tiles.Single(t=>t.Name=="Mesa 2").Detail!="4 plazas"||service.Tiles.Single(t=>t.Name=="Mesa 4").Detail!="4 personas · Menú del día")
            throw new Exception("Alerts of an allergy or an unknown kind are alarming; a free tile shows its seats and an occupied one its people and offer.");
        if(!Texts(TileButton("Mesa 4")).Contains("Alergia · por revisar")||!Texts(TileButton("Mesa 1")).Contains("En cocina")) throw new Exception("The allergy badge and the state are painted with their words: "+string.Join(" | ",Texts(TileButton("Mesa 4"))));
        if(!Texts(TileButton("Mesa 7")).Contains("Anaphylaxis (Critical)")) throw new Exception("An unknown restriction kind is painted raw on its tile, never as a generic word: "+string.Join(" | ",Texts(TileButton("Mesa 7"))));
        // Elegir una mesa OCUPADA: la tarjeta mueve SelectedEntry y la ficha se compone de la lectura de esa mesa (F01).
        var readsBefore=server.Requests.Length;
        Pick("Mesa 4","s4");
        if(!server.Requests.Skip(readsBefore).Any(r=>r.Method=="GET"&&r.Path=="dining/services/s4")||service.OpenForm) throw new Exception("An occupied tile goes through the usual read of that table.");
        if(!service.Tiles.Single(t=>t.Name=="Mesa 4").IsSelected||service.Tiles.Count(t=>t.IsSelected)!=1) throw new Exception("The selected tile is ringed, and only that one.");
        if(service.DetailTitle!="Mesa 4"||service.DetailZoneOffer!="Sala principal · Menú del día"||service.DetailState!="En cocina"||!service.DetailInfo.StartsWith("4 personas · en cocina desde las ",StringComparison.Ordinal)||!service.DetailInfo.EndsWith(" (25 min)",StringComparison.Ordinal))
            throw new Exception("The panel names zone and offer, the table, its state, its people and its time: "+service.DetailZoneOffer+" / "+service.DetailInfo);
        if(!service.CourseRows.Select(r=>r.Number+" "+r.Course.Name+": "+r.State+" · "+r.Detail).SequenceEqual(["1 Entrantes: Servido · Servido a las "+Costina.Client.Labels.Clock(c2Now.AddMinutes(-50)),
            "2 Principales: En preparación · En cocina desde las "+Costina.Client.Labels.Clock(c2Now.AddMinutes(-25))+" · 2 de 4 elaboraciones listas","3 Postres: Falta elegir plato · Han elegido 2 de 4 personas"]))
            throw new Exception("Courses are numbered with their state (the missing choice is exact with the headcount) and their detail: "+string.Join(" | ",service.CourseRows.Select(r=>r.State+" · "+r.Detail)));
        if(service.PrimaryAction is not null||service.PrimaryText!="Esperando la revisión de cocina"||Named("PrimaryActionButton").IsEnabled||Actions()!=" | pause"||!service.ReasonNeeded)
            throw new Exception("Nothing announced for the big button: it waits, disabled; below, exactly the other announced actions: "+service.PrimaryText+" / "+Actions());
        if(service.PreparationRows.Any(r=>r.Station!="Cocina caliente")||service.SelectedPreparation?.Id!="s4-merluza-2"||!service.ReviewAnnounced||!service.PreparationActionsAnnounced||!service.ReviewCommand.CanExecute("adapt"))
            throw new Exception("Preparations name their station from the configuration and the review is offered on the one under review.");
        var detailTexts=Texts(serviceView).ToArray();
        foreach(var text in new[]{"Comensal 2 · ALERGIA frutos secos (grave)","Comensal 3 · Intolerancia lactosa (leve)","Restricciones de los comensales","Cocina caliente · Comensal 2","⚠ PENDIENTE DE REVISIÓN","Esperando la revisión de cocina"})
            if(!detailTexts.Contains(text)) throw new Exception("The panel paints '"+text+"' (restrictions whole and in words, station names, the pending review).");
        if(!detailTexts.Any(t=>t.StartsWith("SIN REVISAR POR COCINA: ",StringComparison.Ordinal))||detailTexts.Any(t=>t is "hot" or "cold"||t.StartsWith("hot ·",StringComparison.Ordinal))) throw new Exception("The pending review is said in words and stations are never coded.");
        if(service.RemoveRestrictionCommand.CanExecute(null)) throw new Exception("Removing a restriction needs one chosen.");
        var restrictionList=(ListBox)serviceView.FindName("RestrictionList"); restrictionList.SelectedIndex=0;
        if(service.SelectedRestriction?.Id!="r-nuts"||!service.RemoveRestrictionCommand.CanExecute(null)) throw new Exception("Choosing a restriction chip makes 'Retirar' act on it.");
        restrictionList.SelectedIndex=-1;
        // Una mesa LIBRE: la tarjeta elige la MESA del formulario de abrir (de siempre) y no lee nada ni toca la mesa leida.
        readsBefore=server.Requests.Length;
        Press(TileButton("Mesa 2"),"tile Mesa 2"); window.UpdateLayout();
        if(!service.OpenForm||service.SelectedTable?.Id!="M2"||service.SelectedEntry?.Service.Id!="s4"||server.Requests.Length!=readsBefore||!service.Tiles.Single(t=>t.Name=="Mesa 2").IsSelected||service.Tiles.Count(t=>t.IsSelected)!=1)
            throw new Exception("A free tile selects the TABLE for the open form, reads nothing and leaves the read table alone.");
        if(((FrameworkElement)serviceView.FindName("OpenPanel")).Visibility!=Visibility.Visible||((FrameworkElement)serviceView.FindName("TableDetail")).Visibility==Visibility.Visible
            ||!Named("OpenTableButton").IsEnabled||service.OpenButtonText!="Abrir Mesa 2"||service.BackText!="Volver a Mesa 4"||service.OpenInfo!="Sala principal · 4 plazas"||service.OpenState!="Libre"||service.OpenWarning.Length!=0)
            throw new Exception("The panel turns into the open form for that table: "+service.OpenButtonText+" / "+service.OpenInfo);
        RefuseRaw(window,"service-open"); Save(Shot(window),"service-open.png");
        // El boton de cabecera "Abrir mesa" propone una mesa libre si la elegida esta ocupada; abrir una ocupada se avisa (decide el servidor).
        service.SelectedTable=service.Tables.Single(t=>t.Id=="M1");
        if(service.OpenWarning.Length==0||!service.OpenCommand.CanExecute(null)) throw new Exception("An occupied table in the open form is warned about but the server decides.");
        service.CancelOpenCommand.Execute(null); Press(Named("StartOpenButton"),"Abrir mesa");
        if(!service.OpenForm||service.SelectedTable?.Id!="M2") throw new Exception("'Abrir mesa' opens the form on a free table: "+service.SelectedTable?.Id);
        // Filtros y salas: solo presentacion, sobre lo leido.
        string Shown()=>string.Join(",",service.Groups.SelectMany(g=>g.Tiles).Select(t=>t.Name));
        ((ListBox)serviceView.FindName("FilterList")).SelectedIndex=2;
        if(service.Filter!="kitchen"||Shown()!="Mesa 1,Mesa 4") throw new Exception("'En cocina' shows the tables in the kitchen: "+Shown());
        service.Filter="ready"; if(Shown()!="Mesa 3") throw new Exception("'Para servir': "+Shown());
        service.Filter="free"; if(Shown()!="Mesa 2,Mesa 5,Terraza 2,Terraza 3,Terraza 4") throw new Exception("'Libres': "+Shown());
        service.Filter="busy"; ((ListBox)serviceView.FindName("ZoneTabList")).SelectedIndex=2;
        if(service.SelectedZone!="terraza"||Shown()!="Terraza 1"||!service.Filters.Select(f=>f.Label).SequenceEqual(["Todas · 4","Ocupadas · 1","En cocina · 0","Para servir · 0","Libres · 3"]))
            throw new Exception("A zone tab narrows the plan and the counts: "+string.Join(",",service.Filters.Select(f=>f.Label)));
        service.Filter="kitchen"; if(service.FloorEmpty!="Ninguna mesa coincide con este filtro.") throw new Exception("An empty filter says so.");
        service.SelectedZone=""; service.Filter="all";
        // Mesa LISTA PARA SERVIR: la accion grande es "Marcar servido" del pase listo y envia la orden de siempre con el contexto leido.
        Pick("Mesa 3","s3");
        if(service.PrimaryAction!="serve"||service.PrimaryText!="Marcar servido · Entrantes"||!Named("PrimaryActionButton").IsEnabled||Actions()!="serve | pause")
            throw new Exception("A ready course puts 'Marcar servido' in the big button: "+service.PrimaryText+" / "+Actions());
        if(!service.PreparationRows.Select(r=>r.Line).SequenceEqual(["Cocina fría · Toda la mesa","Cocina caliente · Toda la mesa"])) throw new Exception("Station names come from configuration.stations: "+string.Join(",",service.PreparationRows.Select(r=>r.Line)));
        var servePosts=server.Count("POST","");
        Press(Named("PrimaryActionButton"),"Marcar servido");
        Wait(()=>!window.Shell.Busy&&server.Count("POST","")>servePosts,"the big button sends the announced action");
        var served=server.Requests.Last(r=>r.Method=="POST");
        if(served.Path!="dining/services/s3/commands/serve"||!served.Body.Contains("\"expectedVersion\":5",StringComparison.Ordinal)||!served.Body.Contains("\"courseId\":\"entrantes\"",StringComparison.Ordinal)
            ||window.Shell.Status!="Operación confirmada por el servidor: Marcar servido.") throw new Exception("The big button is the usual ActCommand with the read context: "+served.Path+" "+served.Body+" / "+window.Shell.Status);
        window.Shell.Status="";
        // Mesa SENTADA sin empezar: "Iniciar servicio" en grande y "Cancelar mesa sin empezar" como peligro; ocupado, nada se puede pulsar.
        Pick("Mesa 6","s6");
        if(Actions()!="start | skip,cancel-unstarted!"||service.PrimaryText!="Iniciar servicio"||!service.CourseRows.All(r=>r.State=="Sin enviar")) throw new Exception("An open table offers to start it: "+Actions());
        window.Shell.Busy=true;
        if(service.ActCommand.CanExecute("start")||service.OpenCommand.CanExecute(null)) throw new Exception("Busy gates the big button and opening.");
        window.Shell.Busy=false;
        // Mesa EN PAUSA con una restriccion de tipo desconocido: se pinta tal cual (nunca rebajada) y en rojo; "Reanudar" en grande.
        Pick("Mesa 7","s7");
        if(Actions()!="resume | skip"||!service.RestrictionsAlarming||!Texts(serviceView).Contains("Comensal 1 · Anaphylaxis cacahuete (Critical)")) throw new Exception("An unknown restriction is shown raw and alarming: "+Actions());
        // Mesa TERMINADA: liberar es la accion grande (ActCommand "release" -> la orden de ocupacion de siempre, con su version y motivo).
        Pick("Mesa 8","s8");
        if(Actions()!="release | "||service.PrimaryText!="Liberar mesa"||service.DetailState!="Terminado, por liberar") throw new Exception("A finished table offers to release it: "+Actions());
        service.Reason="Clientes ya se han ido"; servePosts=server.Count("POST","");
        Press(Named("PrimaryActionButton"),"Liberar mesa");
        Wait(()=>!window.Shell.Busy&&server.Count("POST","")>servePosts,"release is sent");
        if(server.Requests.Last(r=>r.Method=="POST") is not {Path:"dining/occupancy/s8/release"} release||!release.Body.Contains("\"expectedVersion\":2",StringComparison.Ordinal)) throw new Exception("Release goes to the occupancy with ITS version: "+server.Requests.Last(r=>r.Method=="POST").Path);
        window.Shell.Status=""; service.Reason="";
        // Mesa COMIENDO: enviar el siguiente pase, nombrandolo.
        Pick("Terraza 1","st1");
        if(Actions()!="fire-next | pause,skip"||service.PrimaryText!="Enviar siguiente pase a cocina · Principales"||service.DetailState!="Comiendo") throw new Exception("Between courses the next one is offered: "+service.PrimaryText);
        // "Ver cuenta" (responsable): abre Caja en la cuenta de ESA mesa.
        Pick("Mesa 4","s4");
        Press(Named("ViewAccountButton"),"Ver cuenta");
        Wait(()=>!window.Shell.Busy&&window.Shell.Checkout.Account?.Data.ServiceId=="s4","Caja opens the account of the table");
        if(tabs.SelectedItem!=(TabItem)window.FindName("CheckoutTab")||window.Shell.Checkout.SelectedAccount?.ServiceId!="s4"||window.Shell.Checkout.Account?.Data.ServiceId!="s4") throw new Exception("'Ver cuenta' opens Caja on that table's account.");
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        // Revision C2: la cuenta de una mesa terminada ya esta cerrada y Caja solo lista las abiertas: "Ver cuenta" NO cae en la cuenta de
        // otra mesa; Caja no elige ninguna y lo dice.
        server.Accounts=[..diningBoard.Where(b=>b.Service.Id!="s8").Select(b=>new Costina.Client.AccountChoice(b.Service.Id,b.Service.TableId,"Open"))];
        Pick("Mesa 8","s8"); window.Shell.Status="";
        var accountReads=server.Count("GET","checkout/accounts");
        Press(Named("ViewAccountButton"),"Ver cuenta (cuenta cerrada)");
        Wait(()=>!window.Shell.Busy&&server.Count("GET","checkout/accounts")>accountReads,"Caja reads its accounts");
        if(tabs.SelectedItem!=(TabItem)window.FindName("CheckoutTab")||window.Shell.Checkout.SelectedAccount is not null||window.Shell.Checkout.Account is not null
            ||window.Shell.Checkout.Totals!="Sin cuenta seleccionada"||!window.Shell.Status.StartsWith("La cuenta de Mesa 8 no está abierta",StringComparison.Ordinal))
            throw new Exception("'Ver cuenta' on a table whose account is not open never shows another table's account: "+window.Shell.Checkout.SelectedAccount?.ServiceId+" / "+window.Shell.Status);
        server.Accounts=[..diningBoard.Select(b=>new Costina.Client.AccountChoice(b.Service.Id,b.Service.TableId,"Open"))];
        window.Shell.Status=""; tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        // La configuracion se relee en cada actualizacion: una mesa nueva aparece sin reconectar; una mesa ocupada que la configuracion ya no
        // lista no se esconde nunca.
        server.Configuration=diningConfig with {Tables=[..diningConfig.Tables,new("T5","Terraza 5",2,"terraza","Terraza")]};
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"refresh with a new table");
        if(service.Tiles.Length!=13||service.Tiles.Last().Name!="Terraza 5"||service.Tiles.Last().Occupied) throw new Exception("A table created in Configuración appears on the next refresh.");
        server.Configuration=diningConfig with {Tables=[..diningConfig.Tables.Where(t=>t.Id!="M8")]};
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"refresh without an occupied table");
        if(service.Tiles.SingleOrDefault(t=>t.Entry?.Service.Id=="s8") is not {ZoneName:"Otras mesas ocupadas"}) throw new Exception("An occupied table missing from the configuration is still on the plan.");
        server.Configuration=diningConfig;
        // Nivel PUESTO (este PC como dispositivo de sala): el servidor no le anuncia cocina, pase, terminar ni liberar, y la ficha no los
        // pinta; "Ver cuenta" pide la contrasena del responsable, como Caja desde el menu.
        window.Shell.OnUnauthorized().GetAwaiter().GetResult();
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"device:pc-principal",StationName:"Sala 1",Modules:["dining"]);
        server.Board=[..diningBoard.Select(AsStation)];
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        window.Shell.Status=""; tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the Puesto reads the plan");
        if(!window.Shell.IsStation||window.Shell.IsKitchen||service.Tiles.Length!=12) throw new Exception("The Puesto level sees the same plan.");
        Pick("Mesa 4","s4");
        if(service.ReviewAnnounced||service.PreparationActionsAnnounced||Actions()!=" | pause"||((FrameworkElement)serviceView.FindName("RestrictionsBlock")).Visibility!=Visibility.Visible)
            throw new Exception("At the Puesto level no kitchen action is announced nor painted; restrictions are always shown: "+Actions());
        Pick("Mesa 8","s8");
        if(Actions()!=" | "||service.PrimaryText!="Sin acciones para esta mesa ahora"||Named("PrimaryActionButton").IsEnabled) throw new Exception("Releasing is not the Puesto's: nothing is offered: "+Actions());
        Press(Named("ViewAccountButton"),"Ver cuenta (puesto)");
        if(!window.Shell.ElevationRequested||!window.Shell.LoginTitle.Contains("Cuenta / Caja",StringComparison.Ordinal)) throw new Exception("'Ver cuenta' at the Puesto level asks for the responsible: "+window.Shell.LoginTitle);
        window.Shell.CancelElevationCommand.Execute(null); window.Shell.Status="";
        Pick("Mesa 3","s3");
        if(Actions()!="serve | pause"||!Named("PrimaryActionButton").IsEnabled) throw new Exception("The Puesto serves a ready course: "+Actions());
        window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
        RefuseRaw(window,"service-puesto"); Save(Shot(window),"service-puesto.png");
        // Revision C2: "Ver cuenta" de la Mesa 8 se pidio arriba en el nivel Puesto y se cancelo la entrada: el responsable que entra despues
        // (sin desconectar) y abre Caja desde el menu ve la primera cuenta, como siempre, no la de aquella peticion olvidada.
        window.Shell.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"user:jefa",Modules:["dining"]));
        var accountsBefore=server.Count("GET","checkout/accounts");
        tabs.SelectedItem=(TabItem)window.FindName("CheckoutTab"); window.UpdateLayout();
        Wait(()=>!window.Shell.Busy&&server.Count("GET","checkout/accounts")>accountsBefore,"the responsible opens Caja from the menu");
        if(window.Shell.Checkout.SelectedAccount?.ServiceId!="s1") throw new Exception("A cancelled 'Ver cuenta' never chooses the account later: "+window.Shell.Checkout.SelectedAccount?.ServiceId);
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        // Captura principal (como el lienzo): responsable, todas las salas, la Mesa 4 con su alergia por revisar elegida.
        window.Shell.OnUnauthorized().GetAwaiter().GetResult();
        window.Shell.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"user:jefa",Modules:["dining"]));
        server.Board=diningBoard;
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the responsible reads the plan");
        Pick("Mesa 4","s4");
        // Revision C2: tras salir de un nivel y volver a leer (las pestanas se rehacen) la sala elegida sigue pintada como elegida.
        if(service.SelectedZone!=""||!service.ZoneTabs[0].IsCurrent||service.ZoneTabs.Count(z=>z.IsCurrent)!=1) throw new Exception("After a level change and a new read 'Todas las salas' is still painted as chosen.");
        // La vista cabe a 1.280 x 860 con la barra lateral desplegada: la ficha mide 400 px y su accion grande queda a la vista; el plano
        // reparte las tarjetas en columnas iguales (tres a ese ancho) y desplaza lo que no cabe.
        var primaryButton=Named("PrimaryActionButton"); var tablePanel=(FrameworkElement)serviceView.FindName("TablePanel");
        if(Math.Abs(tablePanel.ActualWidth-400)>0.5||primaryButton.TranslatePoint(new Point(0,primaryButton.ActualHeight),serviceView).Y>serviceView.ActualHeight+0.5)
            throw new Exception("The panel keeps its width and its big action in view: "+tablePanel.ActualWidth+" / "+primaryButton.TranslatePoint(new Point(0,primaryButton.ActualHeight),serviceView).Y+" of "+serviceView.ActualHeight);
        var salaTops=service.Groups[0].Tiles.Take(4).Select(t=>Math.Round(TileButton(t.Name).TranslatePoint(new Point(0,0),serviceView).Y)).ToArray();
        if(window.ActualWidth>=1270&&!window.Shell.SideNavCompact&&(salaTops[0]!=salaTops[1]||salaTops[1]!=salaTops[2]||salaTops[3]<=salaTops[2])) throw new Exception("At 1280 px with the sidebar unfolded the plan shows three tiles per row: "+string.Join(",",salaTops));
        if(Descendants<Button>(serviceView).Where(b=>b.DataContext is Costina.Desktop.ViewModels.TableTile).Any(b=>b.ActualWidth<Costina.Desktop.Views.ServiceView.MinTile-0.5)) throw new Exception("No tile is narrower than its minimum.");
        // ===== C2 (plan 2026-10-14): Inicio como panel (Main.dc.html) =====
        // Las cifras y el mini plano salen de las MISMAS tarjetas del Comedor (sin lecturas propias); los dispositivos con su conexion y la
        // conexion sencilla, del servidor simulado; sus botones abren el asistente de Dispositivos; una mesa del mini plano y "Abrir mesa"
        // llevan al Comedor por el camino de siempre (F01). En el nivel Puesto no se ve nada del responsable.
        var homeView=(FrameworkElement)window.FindName("HomeView");
        Button HomeNamed(string name)=>(Button)homeView.FindName(name);
        Button MiniTile(string name){ window.UpdateLayout(); return Descendants<Button>((FrameworkElement)homeView.FindName("MiniPlan")).FirstOrDefault(b=>b.DataContext is Costina.Desktop.ViewModels.TableTile t&&t.Name==name)??throw new Exception("No mini tile painted for "+name); }
        void ShowHome(){ tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.UpdateLayout(); Wait(()=>!window.Shell.Busy,"Inicio is read"); window.UpdateLayout(); }
        var panelDevices=new Costina.Client.DeviceRowDto[]{new("h1","tablet-sala-1","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null,true),new("h2","tablet-terraza","service","terraza","user:jefa",DateTimeOffset.Now.AddDays(-3),null,true),
            new("h3","pantalla-caliente","kitchen","hot","user:jefa",DateTimeOffset.Now.AddDays(-2),null,true),new("h4","pantalla-fria","kitchen","cold","user:jefa",DateTimeOffset.Now.AddDays(-2),null),
            new("h5","pantalla-pase","kitchen","pase","user:jefa",DateTimeOffset.Now.AddDays(-2),null,true),new("h6","vieja","kitchen","hot","user:jefa",DateTimeOffset.Now.AddDays(-9),DateTimeOffset.Now.AddDays(-2))};
        server.Organization=sampleOrganization with {Stations=stationsForDevices}; server.Devices=panelDevices; server.Network=SimpleOn; server.Diagnostics=soon;
        var readsBeforeHome=server.Requests.Length;
        ShowHome();
        if(!server.Requests.Skip(readsBeforeHome).Select(r=>r.Path).ToHashSet().SetEquals(["diagnostics","auth/devices","installation/network"])) throw new Exception("Opening Inicio reads diagnostics, devices and network, never the board: "+string.Join(", ",server.Requests.Skip(readsBeforeHome).Select(r=>r.Path)));
        string Kpis()=>$"{home.OccupiedText} {home.TablesText} | {home.DinersText} ({home.DinersDetail}) | {home.KitchenText} ({home.ReadyText}) | {home.ReviewText} ({home.ReviewDetail})";
        if(Kpis()!="7 de 12 | 19 (2 degustaciones · 2 menús cerrados · 3 a la carta) | 2 (1 listo para servir) | 1 (Mesa 4 · ALERGIA frutos secos (grave))"||!home.ReviewAlarm||!home.HasReady
            ||home.OccupiedShare.Value!=7||home.FreeShare.Value!=5) throw new Exception("The Inicio figures come from the Comedor tiles of the coherent sample: "+Kpis());
        if(!home.Title.EndsWith(", Jefa",StringComparison.Ordinal)||!home.DateLine.EndsWith(c2Now.ToLocalTime().Hour<17?"· Servicio de comida":"· Servicio de cena",StringComparison.Ordinal)||!char.IsUpper(home.DateLine[0]))
            throw new Exception("Inicio greets the responsible by name, with the date and the service: "+home.Title+" / "+home.DateLine);
        if(!ReferenceEquals(home.Tiles,service.Tiles)||Descendants<Button>((FrameworkElement)homeView.FindName("MiniPlan")).Count(b=>b.DataContext is Costina.Desktop.ViewModels.TableTile)!=12||home.ZonesText!="Sala principal · Terraza")
            throw new Exception("'Sala ahora' paints the Comedor tiles, one per table: "+home.ZonesText);
        // Revision C2: el aviso del mini plano se parte solo por su sentido ("Alergia" / "por revisar"), y un tipo desconocido sale tal cual.
        if(!Texts(MiniTile("Mesa 4")).Contains("Alergia")||!Texts(MiniTile("Mesa 4")).Contains("por revisar")||!Texts(MiniTile("Mesa 4")).Contains("En cocina")||!Texts(MiniTile("Mesa 7")).Contains("Anaphylaxis (Critical)")||!Texts(MiniTile("Mesa 2")).Contains("Libre"))
            throw new Exception("A mini tile says its state and its allergy in words: "+string.Join(" | ",Texts(MiniTile("Mesa 4"))));
        if(!home.Legend.Select(l=>l.Label).SequenceEqual(["Libre","Sentados","En cocina","Listo para servir","Comiendo","En pausa","Terminado, por liberar","Alergia, restricción grave o por revisar"])) throw new Exception("The legend names the states on the plan: "+string.Join(",",home.Legend.Select(l=>l.Label)));
        // Revision C2: con solo una restriccion leve en el plano no hay borde rojo (pastilla ambar con su palabra) y la leyenda no lo promete.
        var readBoard=service.Board; var terrace=readBoard.Single(b=>b.Service.Id=="st1");
        service.Board=[terrace with {Service=terrace.Service with {Restrictions=[lactose]}}];
        if(home.Legend.Any(l=>l.Tone=="alert")||service.Tiles.Single(t=>t.Name=="Terraza 1") is not {Alert:"Restricción",AlertAlarming:false}) throw new Exception("A mild restriction alone paints no red border and the legend promises none: "+string.Join(",",home.Legend.Select(l=>l.Label)));
        service.Board=readBoard;
        if(home.DevicesSummary!="4 de 5 con conexión"||!home.DeviceRows.Select(r=>$"{r.Name}: {r.Kind} · {r.State}").SequenceEqual(["tablet-sala-1: Comandero · Sala 1 · Conectado","tablet-terraza: Comandero · Terraza · Conectado",
            "pantalla-caliente: Pantalla de cocina · Cocina caliente · Conectado","pantalla-fria: Pantalla de cocina · Cocina fría · Sin conexión","pantalla-pase: Pantalla de cocina · Pase · Conectado"])||!home.DeviceRows.Single(r=>r.Name=="pantalla-fria").Screen)
            throw new Exception("The device list names kind and station, with presence in words: "+string.Join(" | ",home.DeviceRows.Select(r=>$"{r.Name}: {r.Kind} · {r.State}")));
        if(home.SimpleState!="Conexión sencilla activa · http://192.168.0.81:5089"||home.SimpleDot!="warn"||!home.SimpleNotice.Contains("sin cifrar",StringComparison.Ordinal)||home.NetworkNotice.Length!=0||!home.HasCertificateWarning)
            throw new Exception("Inicio states the simple connection and warns about it and the certificate: "+home.SimpleState);
        home.ApplyNetwork(SimpleOn with {LanHttp=new(false,5089,true),Networks=[new("Red 8","public")]});
        if(home.SimpleState!="Conexión sencilla desactivada"||home.SimpleNotice.Length!=0||!home.NetworkNotice.Contains("«Red 8»",StringComparison.Ordinal)) throw new Exception("A public Windows network is warned about on Inicio too: "+home.NetworkNotice);
        home.ApplyNetwork(null);
        if(home.SimpleState.Length!=0||home.NetworkNotice.Length!=0) throw new Exception("A server without the simple connection paints nothing about it on Inicio.");
        home.ApplyNetwork(SimpleOn);
        foreach(var name in new[]{"ConnectDeviceButton","FloorLinkButton","HomeOpenTableButton","HomeConnectWaiterButton","HomeConnectKitchenButton","HomeDevicesButton"})
            if(!HomeNamed(name).IsVisible||!HomeNamed(name).IsEnabled) throw new Exception("The responsible sees "+name+" enabled from the start (GoToPage answered by the view, commands by their CanExecute).");
        foreach(var name in new[]{"KpiRow","FloorCard","HomeDevicesCard","CertificateBar","SimpleNoticeBar","BackupNote","ServerNote"})
            if(((FrameworkElement)homeView.FindName(name)).Visibility!=Visibility.Visible) throw new Exception(name+" is shown to the responsible.");
        // "Conectar comandero": una sola estacion de sala, asi que el asistente de Dispositivos arranca y emite su codigo (la orden de siempre).
        var pairingsBefore=server.Count("POST","auth/pairings");
        Press(HomeNamed("HomeConnectWaiterButton"),"Conectar comandero (Inicio)");
        Wait(()=>!window.Shell.Busy&&center.HasAssistant,"the waiter assistant opens in Dispositivos");
        if(tabs.SelectedItem!=(TabItem)window.FindName("DevicesTab")||center.AssistantTitle!="Conectar comandero · Sala 1"||center.PairingCode!="Home12_abc-def"||center.ChoosingStation
            ||server.Count("POST","auth/pairings")!=pairingsBefore+1) throw new Exception("'Conectar comandero' opens its assistant with a one-use code: "+center.AssistantTitle);
        center.CancelCommand.Execute(null); window.Shell.Status="";
        // Revision C2: el asistente pedido desde Inicio se consume al leer: si la lectura de Dispositivos falla, no se abre solo mas tarde (otra
        // visita o una actualizacion) ni emite un codigo que nadie ha pedido en ese momento; se vuelve a pulsar.
        ShowHome(); var postsBefore=server.Count("POST",""); var deviceReads=server.Count("GET","auth/devices");
        server.Failing["auth/devices"]=true;
        Press(HomeNamed("HomeConnectWaiterButton"),"Conectar comandero (lectura fallida)");
        Wait(()=>!window.Shell.Busy&&server.Count("GET","auth/devices")>deviceReads,"the devices read fails");
        server.Failing.Clear();
        if(tabs.SelectedItem!=(TabItem)window.FindName("DevicesTab")||center.HasAssistant||center.ChoosingStation||server.Count("POST","")!=postsBefore||window.Shell.Status.Length==0)
            throw new Exception("A failed read opens no assistant, issues no code and says so: "+window.Shell.Status);
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); window.Shell.Status="";
        deviceReads=server.Count("GET","auth/devices");
        tabs.SelectedItem=(TabItem)window.FindName("DevicesTab"); window.UpdateLayout();
        Wait(()=>!window.Shell.Busy&&server.Count("GET","auth/devices")>deviceReads,"a later visit reads Dispositivos");
        if(center.HasAssistant||center.ChoosingStation||server.Count("POST","")!=postsBefore) throw new Exception("A request whose read failed never opens an assistant later nor issues a code.");
        window.Shell.Status="";
        // "Pantalla de cocina": varias estaciones posibles, asi que el asistente se abre en su primer paso (elegir la estacion) sin emitir nada.
        ShowHome(); pairingsBefore=server.Count("POST","auth/pairings");
        Press(HomeNamed("HomeConnectKitchenButton"),"Pantalla de cocina (Inicio)");
        Wait(()=>!window.Shell.Busy&&center.ChoosingStation,"the kitchen screen assistant opens at choosing its station");
        var devicesPanel=(FrameworkElement)window.FindName("DevicesView"); window.UpdateLayout();
        if(tabs.SelectedItem!=(TabItem)window.FindName("DevicesTab")||center.HasAssistant||center.ChoosingIntent!="kitchen"||server.Count("POST","auth/pairings")!=pairingsBefore
            ||((FrameworkElement)devicesPanel.FindName("ChooseStationBar")).Visibility!=Visibility.Visible||((FrameworkElement)devicesPanel.FindName("WaiterSection")).Visibility!=Visibility.Collapsed
            ||((FrameworkElement)devicesPanel.FindName("KitchenSection")).Visibility!=Visibility.Visible||center.ChoosingText!="Pantalla de cocina: pulsa la estación donde va a estar la pantalla.")
            throw new Exception("With several kitchen stations the assistant opens at choosing one, with only that section shown: "+center.ChoosingText);
        RefuseRaw(window,"devices-choose"); Save(Shot(window),"devices-choose.png");
        Press(Descendants<Button>((FrameworkElement)devicesPanel.FindName("KitchenButtons")).Single(b=>Texts(b).Contains("Cocina fría")),"Cocina fría");
        Wait(()=>!window.Shell.Busy&&center.HasAssistant,"choosing the station starts the assistant");
        if(center.AssistantTitle!="Conectar pantalla de cocina · Cocina fría"||center.ChoosingStation||server.Count("POST","auth/pairings")!=pairingsBefore+1) throw new Exception("Choosing the station starts the usual assistant: "+center.AssistantTitle);
        center.CancelCommand.Execute(null); window.Shell.Status="";
        // Una mesa OCUPADA del mini plano: el Comedor con esa mesa leida por el camino de siempre (F01); una LIBRE: el formulario de abrir.
        ShowHome(); var readsBeforeTile=server.Requests.Length;
        Press(MiniTile("Mesa 3"),"mini Mesa 3");
        Wait(()=>!window.Shell.Busy&&service.Dining?.Data.Id=="s3"&&service.SelectedEntry?.Service.Id=="s3","the mini plan opens Mesa 3 in Comedor");
        if(tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")||service.OpenForm||!server.Requests.Skip(readsBeforeTile).Any(r=>r.Method=="GET"&&r.Path=="dining/services/s3"))
            throw new Exception("An occupied mini tile opens Comedor with that table read as usual.");
        ShowHome(); readsBeforeTile=server.Requests.Length;
        Press(MiniTile("Terraza 2"),"mini Terraza 2");
        if(tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")||!service.OpenForm||service.SelectedTable?.Id!="T2"||service.SelectedEntry?.Service.Id!="s3"||server.Requests.Skip(readsBeforeTile).Any(r=>r.Path.StartsWith("dining/",StringComparison.Ordinal)))
            throw new Exception("A free mini tile opens the open form for that table, reading nothing.");
        service.CancelOpenCommand.Execute(null);
        // "Abrir mesa" de la cabecera: el Comedor con el formulario de abrir mesa en una mesa libre (Service.StartOpen).
        service.SelectedTable=service.Tables.Single(t=>t.Id=="M1"); ShowHome();
        Press(HomeNamed("HomeOpenTableButton"),"Abrir mesa (Inicio)");
        if(tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")||!service.OpenForm||service.SelectedTable?.Id!="M2") throw new Exception("'Abrir mesa' on Inicio opens the form on a free table: "+service.SelectedTable?.Id);
        service.CancelOpenCommand.Execute(null);
        // "Ver plano": el Comedor, como el menu.
        ShowHome(); Press(HomeNamed("FloorLinkButton"),"Ver plano");
        if(tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")) throw new Exception("'Ver plano' opens Comedor.");
        // Nivel PUESTO: las cifras y el mini plano si (el comedor es del puesto); dispositivos, conectar, diagnostico y red, no: ni se leen.
        window.Shell.OnUnauthorized().GetAwaiter().GetResult();
        window.Shell.Session=new("service","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"device:pc-principal",StationName:"Sala 1",Modules:["dining"]);
        server.Board=[..diningBoard.Select(AsStation)];
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        window.Shell.Status=""; tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.UpdateLayout();
        readsBeforeHome=server.Requests.Length;
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the Puesto reads Inicio"); window.UpdateLayout();
        if(server.Requests.Skip(readsBeforeHome).Any(r=>r.Path is "diagnostics" or "auth/devices" or "installation/network")) throw new Exception("At the Puesto level Inicio reads nothing of the responsible's.");
        foreach(var name in new[]{"HomeDevicesCard","ConnectDeviceButton","CertificateBar","SimpleNoticeBar","BackupNote","HomeNetworkBar"})
            if(((FrameworkElement)homeView.FindName(name)).IsVisible) throw new Exception("At the Puesto level Inicio hides "+name+".");
        if(!((FrameworkElement)homeView.FindName("KpiRow")).IsVisible||!((FrameworkElement)homeView.FindName("FloorCard")).IsVisible||Grid.GetColumnSpan((FrameworkElement)homeView.FindName("FloorCard"))!=3
            ||!HomeNamed("HomeOpenTableButton").IsVisible||!((FrameworkElement)homeView.FindName("ServerNote")).IsVisible||home.DeviceRows.Length!=0||Kpis()!="7 de 12 | 19 (2 degustaciones · 2 menús cerrados · 3 a la carta) | 2 (1 listo para servir) | 1 (Mesa 4 · ALERGIA frutos secos (grave))")
            throw new Exception("At the Puesto level Inicio shows the dining figures and the whole-width plan: "+Kpis());
        if(!home.Title.EndsWith(", pc-principal",StringComparison.Ordinal)||!home.WhoText.StartsWith("Puesto de sala pc-principal · Sala 1",StringComparison.Ordinal)) throw new Exception("At the Puesto level Inicio greets the puesto: "+home.Title);
        RefuseRaw(window,"home-puesto"); Save(Shot(window),"home-puesto.png");
        // Un asistente pedido en el nivel Puesto pide antes la contrasena (Dispositivos es del responsable); cancelar la entrada lo olvida.
        home.ConnectDeviceCommand.Execute("kitchen");
        if(!window.Shell.ElevationRequested||!window.Shell.LoginTitle.Contains("Dispositivos",StringComparison.Ordinal)||tabs.SelectedItem!=(TabItem)window.FindName("HomeTab"))
            throw new Exception("An assistant asked at the Puesto level asks for the responsible first: "+window.Shell.LoginTitle);
        window.Shell.CancelElevationCommand.Execute(null); window.Shell.Status="";
        // De vuelta al responsable (sin desconectar, como al entrar con su contrasena: la peticion cancelada no debe sobrevivir) y con la
        // Mesa 4 elegida en el Comedor (captura principal).
        window.Shell.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"user:jefa",Modules:["dining"]));
        server.Board=diningBoard;
        tabs.SelectedItem=(TabItem)window.FindName("DevicesTab"); window.UpdateLayout();
        Wait(()=>!window.Shell.Busy&&center.KitchenStations.Length>0,"the responsible opens Dispositivos");
        if(center.ChoosingStation||center.HasAssistant) throw new Exception("A cancelled request never opens an assistant later.");
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the responsible reads the plan again");
        Pick("Mesa 4","s4");
        window.Shell.Api.Dispose(); window.Shell.Api=null;
        SynchronizationContext.SetSynchronizationContext(previousContext);
        window.Shell.Checkout.Products=[new Costina.Client.ProductChoice("water","Agua mineral","Botella",400,"bottle","Bebidas","general"),new Costina.Client.ProductChoice("wine","Vino de ensayo","Copa",950,"glass","Vinos","general")]; window.Shell.Checkout.SelectedProduct=window.Shell.Checkout.Products[0];
        window.Shell.Checkout.Accounts=[..diningBoard.Select(b=>new Costina.Client.AccountChoice(b.Service.Id,b.Service.TableId,"Open"))]; window.Shell.Checkout.SelectedAccount=window.Shell.Checkout.Accounts[0];
        window.Shell.Checkout.Charges=[new("ch1","Degustación Costiña · Por persona",2,15000,false,null,30000),new("ch2","Agua mineral · Botella",1,400,false,null,400)];
        window.Shell.Checkout.Account=new(1,new Costina.Client.AccountDto("a1","s1","Open",30400,0,30400,0,"Unpaid",window.Shell.Checkout.Charges,[],["add-product","payment"],[]));
        center.Render(stationsForDevices,[pendingOther],activeDevices);
        center.ApplyNetwork(SimpleOn);   // B.2: la captura de Dispositivos muestra la conexion sencilla activa
        home.ApplyDiagnostics(soon); home.ApplyDevices(panelDevices); home.ApplyNetwork(SimpleOn);   // C2: Inicio con sus dispositivos y la conexion sencilla
        window.Shell.Status="";   // C1 (revision): el programa real no pinta aviso al abrir una vista; las capturas tampoco
        window.Shell.Home.Visible=true;
        foreach(var (tab,file) in new[]{("HomeTab","home.png"),("ServiceTab","service.png"),("CheckoutTab","checkout.png"),("OrganizationTab","configuration.png"),("CatalogTab","catalog.png"),("OffersTab","offers.png"),("DevicesTab","devices.png")})
        {
            tabs.SelectedItem=(TabItem)window.FindName(tab); window.UpdateLayout();
            // Las columnas "*" del DataGrid se reparten en una pasada diferida: se vacia la cola del despachador antes de capturar.
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
            // C1: ningun estado, tipo o actor crudo del servidor en el texto pintado de la vista. C2: las estaciones, por su nombre.
            RefuseRaw(window,tab);
            if(tab=="ServiceTab"&&new[]{"Comensal 2 · ALERGIA frutos secos (grave)","Alergia · por revisar","Anaphylaxis (Critical)","Cocina caliente · Comensal 2","Mesa 4"}.FirstOrDefault(t=>!Texts(window).Contains(t)) is {} missing)
                throw new Exception("Comedor paints the allergy in words on the panel and on the tile, and the station by its name: missing '"+missing+"'.");
            // C2: Inicio pinta sus cuatro cifras con su palabra, la alergia por revisar con su mesa, el mini plano y los dispositivos.
            if(tab=="HomeTab"&&new[]{"Mesas ocupadas","de 12","Comensales","19","Pases en cocina","1 listo para servir","Alergias por revisar","Mesa 4 · ALERGIA frutos secos (grave)","Sala ahora",
                "Alergia","por revisar","Anaphylaxis (Critical)","4 de 5 con conexión","Pantalla de cocina · Cocina fría","Sin conexión","Conexión sencilla activa · http://192.168.0.81:5089"}.FirstOrDefault(t=>!Texts(window).Contains(t)) is {} missingHome)
                throw new Exception("Inicio paints its figures, the allergy to review, the plan and the devices in words: missing '"+missingHome+"'.");
            var shot=Shot(window);
            // C1: sin Mica la ventana pinta un fondo OPACO #F3F4F6 (con Mica la captura sale con huecos transparentes): se mira el
            // margen entre la barra lateral y la vista.
            if(Pixel(shot,workspace.TranslatePoint(new Point(-12,12),window)) is var gutter&&gutter!=Color.FromArgb(255,0xF3,0xF4,0xF6)) throw new Exception(tab+": the window background must be opaque #F3F4F6, found "+gutter);
            Save(shot,file);
            // C2: Inicio desplaza: la segunda captura es su final (accesos y notas: conexion sencilla, copia de seguridad y servidor).
            if(tab=="HomeTab")
            {
                var homeScroll=(ScrollViewer)((FrameworkElement)window.FindName("HomeView")).FindName("HomeScroll");
                homeScroll.ScrollToEnd(); window.UpdateLayout(); window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
                var homeEnd=(FrameworkElement)((FrameworkElement)window.FindName("HomeView")).FindName("HomeNotes");
                if(homeEnd.TranslatePoint(new Point(0,homeEnd.ActualHeight),homeScroll).Y>homeScroll.ActualHeight+0.5) throw new Exception("Scrolled to the end, Inicio shows its notes.");
                Save(Shot(window),"home-end.png"); homeScroll.ScrollToHome(); window.UpdateLayout();
            }
        }
        // B.2: Dispositivos con la red de Windows en "Publica" y la conexion sencilla apagada, ofrecida y pidiendo confirmacion.
        center.ApplyNetwork(SimpleOn with {LanHttp=new(false,5089,true),Networks=[new("Red 8","public")]});
        if(!center.AskEnableSimpleCommand.CanExecute(null)) throw new Exception("The responsible can ask to switch the simple connection on.");
        center.AskEnableSimpleCommand.Execute(null);
        if(!center.ConfirmingSimple||center.AskEnableSimpleCommand.CanExecute(null)||!center.ConfirmEnableSimpleCommand.CanExecute(null)) throw new Exception("Switching it on is confirmed in a second step.");
        tabs.SelectedItem=(TabItem)window.FindName("DevicesTab"); window.UpdateLayout();
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
        RefuseRaw(window,"DevicesTab");
        foreach(var bar in new[]{"NetworkWarningBar","SimpleConfirmBar"})
            if(((FrameworkElement)((FrameworkElement)window.FindName("DevicesView")).FindName(bar)).Visibility!=Visibility.Visible) throw new Exception(bar+" must be visible.");
        if(((FrameworkElement)((FrameworkElement)window.FindName("DevicesView")).FindName("SimpleOnBar")).Visibility==Visibility.Visible||((FrameworkElement)((FrameworkElement)window.FindName("DevicesView")).FindName("SimpleOffBar")).Visibility==Visibility.Visible) throw new Exception("While confirming, only the confirmation bar is painted (it is off).");
        Save(Shot(window),"devices-simple-off.png");
        center.CancelEnableSimpleCommand.Execute(null); center.ApplyNetwork(SimpleOn);
        // C1: la barra lateral se pliega a iconos (boton de abajo) y se despliega de nuevo; plegada, sus elementos siguen ahi con su ayuda.
        // (Se parte de la barra desplegada: en una pantalla estrecha, como la del CI, la ventana la pliega sola por debajo de 1270 px.)
        tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.Shell.SideNavCompact=false; window.UpdateLayout();
        var sideNavBar=(FrameworkElement)window.FindName("SideNavBar"); var navToggle=(Button)window.FindName("SideNavToggle");
        navToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(!window.Shell.SideNavCompact||sideNavBar.ActualWidth>64||((Button)window.FindName("NavCatalog")).ToolTip as string!="Catálogo y tarifas") throw new Exception("The sidebar folds to icons, each with its name as a tooltip: "+sideNavBar.ActualWidth);
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
        Save(Shot(window),"sidebar-compact.png");
        navToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(window.Shell.SideNavCompact||sideNavBar.ActualWidth<200) throw new Exception("The sidebar unfolds again.");
        // C1: startup.png es Primeros pasos tal como lo ve un PC recien arrancado: sin aviso pendiente y con el servidor ya comprobado
        // (lo haria StartAsync; los checks no tocan la red).
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.Shell.Status=""; window.Shell.Session=null; window.Shell.ServerReady=true; window.UpdateLayout();
        if(firstRun.Visibility!=Visibility.Visible||!firstRun.IsAncestorOf(loginCard)||((FrameworkElement)window.FindName("SideNavBar")).Visibility!=Visibility.Collapsed) throw new Exception("Disconnected again: Primeros pasos with the login card, no sidebar.");
        if(!window.Shell.FirstRunServerText.StartsWith("El servidor está listo ",StringComparison.Ordinal)||window.Shell.ServerDot!="ok") throw new Exception("Primeros pasos states the server: "+window.Shell.FirstRunServerText);
        if(window.Shell.ConnectionText!="Nadie ha entrado en este PC") throw new Exception("Before anyone enters the status bar says so: "+window.Shell.ConnectionText);
        // C1 (revision): al teclear otra direccion, la linea del servidor de Primeros pasos la sigue en CADA cambio (no solo en el primero).
        var firstRunServer=(TextBlock)window.FindName("FirstRunServer");
        window.Shell.ServerReady=false; window.Shell.Endpoint="https://otro.local:5443"; window.Shell.Endpoint="https://tercero.local:5443";
        if(firstRunServer.Text!="Servidor configurado en tercero.local") throw new Exception("Primeros pasos follows every edit of the server address: "+firstRunServer.Text);
        window.Shell.Endpoint=ChecksEndpoint; window.Shell.ServerReady=true; window.UpdateLayout();
        RefuseRaw(window,"startup");
        var image=Shot(window);
        if(Pixel(image,firstRun.TranslatePoint(new Point(8,8),window)) is var firstRunBackground&&firstRunBackground!=Color.FromArgb(255,0xF3,0xF4,0xF6)) throw new Exception("Primeros pasos must paint the opaque #F3F4F6, found "+firstRunBackground);
        Save(image,"startup.png");
        // C1: mientras arranca y espera al servidor (corte B) la ventana lo dice en su propia capa, sin Primeros pasos ni entrada.
        window.Shell.ServerReady=null;   // como al encender: el servidor aun no ha respondido
        window.Shell.Booting=true; window.Shell.BootText="Arrancando el servidor (12 s). Tras encender el equipo puede tardar de uno a tres minutos."; window.UpdateLayout();
        if(window.Shell.ServerStateText!="Esperando al servidor"||window.Shell.ServerDot!="warn") throw new Exception("While booting the status bar says it waits for the server: "+window.Shell.ServerStateText);
        if(((FrameworkElement)window.FindName("BootPanel")).Visibility!=Visibility.Visible||firstRun.Visibility!=Visibility.Collapsed||loginCard.Visibility!=Visibility.Collapsed) throw new Exception("While booting only the boot panel is shown.");
        Save(Shot(window),"booting.png");
        window.Shell.Booting=false; window.Shell.BootText=""; window.UpdateLayout();
        File.WriteAllText("artifacts/desktop/window-checks.txt","Startup, viewmodel affordance/context-mapping and durable-store isolation checks passed. Actual WPF window rendered. No live backend interaction claimed by these checks."
            +Environment.NewLine+"Version: "+Costina.Desktop.ViewModels.ShellViewModel.AppVersion+Environment.NewLine+"Fonts: "+fonts+Environment.NewLine);
        window.Close();app.Shutdown();
        try { Directory.Delete(checksRoot,true); } catch(IOException) { }   // la carpeta de los checks; nunca la real
        Console.WriteLine("WPF startup and viewmodel checks passed");return 0;
    }

    // C1: valores crudos del servidor que nunca deben verse (estados en ingles, tipos de oferta, actores con prefijo). Como palabra
    // suelta: dentro de un codigo con guiones (el producto «menu-lab-tasting») no es un estado.
    // Revision C1: tambien tipos de estacion, servicios de oferta y codigos de accion (una orden pendiente se describe en castellano).
    private static readonly Regex RawValue=new(@"(?<![\w-])(Open|InService|Paused|Completed|Cancelled|Pending|Fired|Preparing|Ready|Served|Skipped|Occupied|Released|Closed|Unpaid|PartiallyPaid|Paid|Credit|Allergy|Intolerance|Preference|Severe|Moderate|Mild|Unaffected|Adapt|Remake|tasting|set-menu|a-la-carte|Kitchen|Pass|Room|lunch|dinner|fire-next|cancel-unstarted|add-dish|preparation-start|preparation-ready|review-preparation)(?![\w-])|\b(user|device):",RegexOptions.CultureInvariant);
    private static void RefuseRaw(Window window,string what)
    {
        var leaked=Texts(window).Where(t=>RawValue.IsMatch(t)).ToArray();
        if(leaked.Length>0) throw new Exception(what+" shows raw server values: "+string.Join(" | ",leaked));
    }
    // C1 (revision): captura del AREA DE CLIENTE. La ventana mide tambien el marco y la captura salia con una franja transparente a la
    // derecha y abajo; se recorta al contenido (que empieza en el origen de la ventana) y se exige que sea opaca hasta la esquina.
    // B.2: red de ejemplo del servidor: conexion sencilla encendida, la IP del local sugerida y un adaptador virtual descartado.
    private static readonly Costina.Client.NetworkDto SimpleOn=new(new(true,5089,true),new(true,5443),
        [new("192.168.0.81","Ethernet",true,false,true),new("192.168.56.1","VirtualBox Host-Only Network",false,true,false)],[new("Red 8","private")]);

    private static BitmapSource Shot(Window window)
    {
        var client=(FrameworkElement)window.Content;
        if(client.TranslatePoint(new Point(0,0),window)!=new Point(0,0)) throw new Exception("The window content must start at the client origin.");
        var full=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); full.Render(window);
        var shot=new CroppedBitmap(full,new Int32Rect(0,0,Math.Min((int)client.ActualWidth,full.PixelWidth),Math.Min((int)client.ActualHeight,full.PixelHeight)));
        shot.Freeze();
        if(Pixel(shot,new Point(shot.PixelWidth-1,shot.PixelHeight-1)).A!=255||Pixel(shot,new Point(shot.PixelWidth-1,0)).A!=255) throw new Exception("The capture must be opaque up to its corners.");
        return shot;
    }
    private static void Save(BitmapSource image,string file)
    {
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream=File.Create("artifacts/desktop/"+file); encoder.Save(stream);
    }
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
    // E3.2: elementos de un tipo bajo un elemento del arbol visual.
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        var pending=new Stack<DependencyObject>(); pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();
            if(node is T match) yield return match;
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) pending.Push(VisualTreeHelper.GetChild(node,i));
        }
    }
    // E3.2: deja trabajar al despachador (descargas y decodificacion vuelven a la ventana por el) hasta que se cumpla la condicion.
    private static void Wait(Func<bool> done,string what)
    {
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(!done())
        {
            if(DateTime.UtcNow>deadline) throw new Exception("Timed out waiting: "+what);
            var frame=new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(2);
        }
    }
    private static void Await(Task task,string what){ Wait(()=>task.IsCompleted,what); task.GetAwaiter().GetResult(); }
    // C2: pulsa un boton como lo haria una persona (patron Invoke de automatizacion: ejecuta SU orden enlazada con su parametro); un
    // boton oculto o deshabilitado no se puede pulsar. Despues deja trabajar al despachador (la pulsacion llega por el).
    private static void Press(Button button,string what)
    {
        if(!button.IsVisible||!button.IsEnabled) throw new Exception(what+": the button must be visible and enabled.");
        var peer=new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
        ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)).Invoke();
        var frame=new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);
    }
    // E3.2: foto de muestra: una botella dibujada (cristal, capsula, etiqueta y brillo) sobre blanco, guardada como PNG o JPEG de verdad.
    private static byte[] Bottle(Color glass,Color label,BitmapEncoder encoder)
    {
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            var glassBrush=new SolidColorBrush(glass);
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,240,240));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xEE,0xEF,0xF1)),null,new Point(120,224),46,6);   // sombra
            dc.DrawGeometry(glassBrush,null,Geometry.Parse("M110,34 L130,34 L130,80 C130,98 154,100 154,124 L154,214 Q154,222 146,222 L94,222 Q86,222 86,214 L86,124 C86,100 110,98 110,80 Z"));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x2B,0x2B,0x2E)),null,new Rect(107,22,26,34),3,3);   // capsula
            dc.DrawRoundedRectangle(new SolidColorBrush(label),null,new Rect(91,142,58,52),4,4);                            // etiqueta
            dc.DrawRectangle(glassBrush,null,new Rect(101,156,38,4)); dc.DrawRectangle(glassBrush,null,new Rect(108,168,24,2)); dc.DrawRectangle(glassBrush,null,new Rect(108,176,24,2));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(60,255,255,255)),null,new Rect(93,116,7,92),3.5,3.5);   // brillo
        }
        var bitmap=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        encoder.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(bitmap,PixelFormats.Bgr24,null,0)));
        using var stream=new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }

    // E3.2: servidor simulado en memoria para los checks: sirve el catalogo, la organizacion y las imagenes (con su tipo y su ETag),
    // confirma las ordenes con el eco de su clave y aplica en memoria las de imagen, como el motor; registra cada peticion. Sin puertos.
    // C2: tambien la configuracion operativa, el tablero del comedor, cada mesa (la entrada del tablero con su version) y las cuentas
    // de Caja, para que elegir una tarjeta del plano pase por la lectura de siempre.
    private sealed record Request(string Method,string Path,string Body,string? Key);
    private sealed class FakeServer(Costina.Client.CatalogDto catalog,Costina.Client.OrganizationDto organization) : System.Net.Http.HttpMessageHandler
    {
        public Costina.Client.CatalogDto Catalog { get; set; }=catalog;
        public Costina.Client.OrganizationDto Organization { get; set; }=organization;
        public Costina.Client.Configuration? Configuration { get; set; }
        public Costina.Client.BoardEntry[] Board { get; set; }=[];
        public Costina.Client.AccountChoice[] Accounts { get; set; }=[];
        // C2: lo que lee Inicio del responsable (diagnostico, dispositivos, red) y lo que necesita un asistente de Dispositivos (solicitudes y
        // el codigo de un uso que emite POST auth/pairings).
        public Costina.Client.DiagnosticsDto? Diagnostics { get; set; }
        public Costina.Client.DeviceRowDto[] Devices { get; set; }=[];
        public Costina.Client.NetworkDto? Network { get; set; }
        public System.Collections.Concurrent.ConcurrentDictionary<string,(string Type,byte[] Content)> Images { get; }=new();
        // Revision C2: lecturas que fallan (503) mientras la ruta este aqui, para comprobar lo que queda tras una lectura fallida.
        public System.Collections.Concurrent.ConcurrentDictionary<string,bool> Failing { get; }=new();
        private readonly List<Request> requests=[];
        public Request[] Requests { get { lock(requests) return [..requests]; } }
        public int Count(string method,string path)=>Requests.Count(r=>r.Method==method&&r.Path.StartsWith(path,StringComparison.Ordinal));
        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath["/api/native/v1/".Length..];
            var body=request.Content is null?"":await request.Content.ReadAsStringAsync(ct);
            var key=request.Headers.TryGetValues("Idempotency-Key",out var keys)?keys.Single():null;
            lock(requests) requests.Add(new(request.Method.Method,path,body,key));
            if(request.Method==System.Net.Http.HttpMethod.Get)
            {
                if(Failing.ContainsKey(path)) return Json(503,"{\"error\":\"unavailable\"}");
                if(path.StartsWith("catalog/images/",StringComparison.Ordinal))
                {
                    if(!Images.TryGetValue(Uri.UnescapeDataString(path["catalog/images/".Length..]),out var image)) return Json(404,"{\"error\":\"not_found\"}");
                    var found=new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(image.Content)};
                    found.Content.Headers.ContentType=new(image.Type);
                    found.Headers.ETag=new("\""+Costina.Client.ProductImages.Sha256(image.Content)+"\"");
                    return found;
                }
                if(path=="erp/catalog") return Json(200,JsonSerializer.Serialize(Catalog,Costina.Client.ApiClient.Json));
                if(path=="organization") return Json(200,JsonSerializer.Serialize(Organization,Costina.Client.ApiClient.Json));
                if(path=="configuration"&&Configuration is not null) return Json(200,JsonSerializer.Serialize(Configuration,Costina.Client.ApiClient.Json));
                if(path=="dining/board") return Json(200,JsonSerializer.Serialize(Board,Costina.Client.ApiClient.Json));
                if(path.StartsWith("dining/services/",StringComparison.Ordinal)&&Board.FirstOrDefault(b=>b.Service.Id==Uri.UnescapeDataString(path["dining/services/".Length..])) is {} entry)
                    return Json(200,JsonSerializer.Serialize(new Costina.Client.Versioned<Costina.Client.DiningDto>(entry.Version,entry.Service),Costina.Client.ApiClient.Json));
                if(path=="checkout/accounts") return Json(200,JsonSerializer.Serialize(Accounts,Costina.Client.ApiClient.Json));
                if(path=="checkout/catalog") return Json(200,"[]");
                if(path=="diagnostics"&&Diagnostics is not null) return Json(200,JsonSerializer.Serialize(Diagnostics,Costina.Client.ApiClient.Json));
                if(path=="auth/devices") return Json(200,JsonSerializer.Serialize(Devices,Costina.Client.ApiClient.Json));
                if(path=="auth/pairings/pending") return Json(200,"[]");
                if(path=="installation/network"&&Network is not null) return Json(200,JsonSerializer.Serialize(Network,Costina.Client.ApiClient.Json));
                if(path.StartsWith("checkout/services/",StringComparison.Ordinal)&&Accounts.FirstOrDefault(a=>a.ServiceId==Uri.UnescapeDataString(path["checkout/services/".Length..])) is {} account)
                    return Json(200,JsonSerializer.Serialize(new Costina.Client.Versioned<Costina.Client.AccountDto>(1,new(account.ServiceId+"-cuenta",account.ServiceId,"Open",0,0,0,0,"Unpaid",[],[],["add-product","payment"],[])),Costina.Client.ApiClient.Json));
                return Json(404,"{\"error\":\"not_found\"}");
            }
            if(path=="auth/pairings") return Json(200,JsonSerializer.Serialize(new{pairingId="pid-home",code="Home12_abc-def",expiresAt=DateTimeOffset.UtcNow.AddMinutes(5)}));
            var command=JsonSerializer.Deserialize<JsonElement>(body);
            if(path=="erp/catalog/commands/product-image-set")
            {
                var id=command.GetProperty("productId").GetString()!; var content=Convert.FromBase64String(command.GetProperty("data").GetString()!);
                Images[id]=(command.GetProperty("contentType").GetString()!,content);
                Catalog=Catalog with {Products=[..Catalog.Products.Select(p=>p.Id==id?p with {ImageSha=Costina.Client.ProductImages.Sha256(content)}:p)]};
            }
            if(path=="erp/catalog/commands/product-image-remove")
            {
                var id=command.GetProperty("productId").GetString()!; Images.TryRemove(id,out _);
                Catalog=Catalog with {Products=[..Catalog.Products.Select(p=>p.Id==id?p with {ImageSha=null}:p)]};
            }
            var ok=Json(200,"{\"changed\":true}"); ok.Headers.Add("Idempotency-Key",key); return ok;
        }
        private static System.Net.Http.HttpResponseMessage Json(int status,string json)=>new((System.Net.HttpStatusCode)status){Content=new System.Net.Http.StringContent(json,System.Text.Encoding.UTF8,"application/json")};
    }
}
