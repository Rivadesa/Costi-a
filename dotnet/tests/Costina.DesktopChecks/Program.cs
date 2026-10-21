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
        // C2b: sin el arranque normal de la aplicacion (instancia unica y BootAsync): los checks construyen la ventana ellos mismos.
        App.Checks=true;
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
        if(!erp[0].IsEnabled||erp[0].Header?.ToString()!="Catálogo y tarifas"||!erp[1].IsEnabled||erp[1].Header?.ToString()!="Menús y cartas"||erp.Skip(2).Any(m=>m.IsEnabled)) throw new Exception("ERP menu: catalog and offers exist (E3/E4a); the rest stays disabled.");
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
        if(!shell.Checkout.HasContext||shell.Checkout.DetailTitle!="M1"||shell.Checkout.TotalText!="0,00 €") throw new Exception("The matching account fills the ficha (C2b): "+shell.Checkout.DetailTitle);
        shell.Checkout.SelectedAccount=shell.Checkout.Accounts[1];
        if(shell.Checkout.AddCommand.CanExecute(null)||shell.Checkout.PayCommand.CanExecute(null)) throw new Exception("Account actions must never target a different account than the loaded one.");
        // C2b: la ficha tampoco ensena nada de la cuenta leida mientras la elegida es otra: dice que la esta leyendo, sin cifras ni lineas.
        if(shell.Checkout.HasContext||shell.Checkout.TotalText.Length!=0||shell.Checkout.LineRows.Length!=0||shell.Checkout.CloseAnnounced||shell.Checkout.FillPendingCommand.CanExecute(null)
            ||shell.Checkout.EmptyHeading!="Cuenta de M2") throw new Exception("A chosen account that is not the read one shows no figures and offers nothing: "+shell.Checkout.EmptyHeading);
        // C2b: una cuenta cerrada solo ofrece reabrir (lo unico que anuncia) y lo explica; cobrar, anadir y cerrar quedan apagados.
        shell.Checkout.SelectedAccount=shell.Checkout.Accounts[0];
        shell.Checkout.Account=new(2,new Costina.Client.AccountDto("a1","s1","Closed",400,400,0,0,"Paid",[],[],["reopen"],[]));
        if(!shell.Checkout.ReopenCommand.CanExecute(null)||shell.Checkout.PayCommand.CanExecute(null)||shell.Checkout.AddCommand.CanExecute(null)||shell.Checkout.CloseAccountCommand.CanExecute(null)
            ||shell.Checkout.CloseAnnounced||shell.Checkout.DetailState!="Cerrada"||!shell.Checkout.ClosedText.StartsWith("Cuenta cerrada: no admite cambios.",StringComparison.Ordinal))
            throw new Exception("A closed account offers only reopening, and says why: "+shell.Checkout.ClosedText);
        // D3.4 (F02/F03): almacen durable aislado por instalacion, ambito, rol y ventana; la evidencia se conserva.
        var folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-checks-"+Guid.NewGuid().ToString("N"));
        var session=new Costina.Client.SessionInfo("checks","t","c","l",null,"inst-checks","0.7.0-d3.4",Actor:"user:jefa");
        // D6.7: sin actor no hay almacen (no se sabria de quien es cada orden): fallo cerrado.
        try { using var anonymous=new DpapiPendingStore(session with {Actor=null},System.IO.Path.Combine(folder,"sin-actor")); throw new Exception("A session without an actor must get no durable store."); }
        catch(ArgumentException) { }
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
            using(var foreign=new DpapiPendingStore(session with {InstallationId="inst-otra"},folder)) foreign.Save(command);
            File.Move(Directory.GetFiles(folder,"inst-otra-*.bin").Single(),files[0]);
            if(first.Load() is not {Outcome:Costina.Client.PendingOutcome.Unreadable,Key:"k123"}) throw new Exception("A command from another installation must never be restored.");
            first.Discard();
        }
        using(var third=new DpapiPendingStore(session,folder))
            if(third.Slot!=0) throw new Exception("A released slot must be reused by the next window.");
        Directory.Delete(folder,true);
        // D6.7 (PC): el servidor guarda la idempotencia POR ACTOR (la clave se busca con quien la manda). Con el mismo rol en este PC
        // (otra persona responsable, o un dispositivo antiguo con rol principal) el fichero durable es el mismo: la orden incierta de
        // «ana» no se reenvia con la sesion de «bea» (se aplicaria otra vez, o un 409 la cerraria como no aplicada) ni se pregunta por
        // ella (el servidor diria que no consta). Motor simulado con esa regla, almacen DPAPI real y dos identidades del mismo rol.
        var actorFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-checks-"+Guid.NewGuid().ToString("N"));
        string anaToken=new('a',40),beaToken=new('b',40);
        var actorServer=new ActorServer(new(){[anaToken]="user:ana",[beaToken]="user:bea"});
        var anaSession=new Costina.Client.SessionInfo("main","t","c","l",null,"inst-checks","0.40.1-d6.7",Actor:"user:ana");
        var beaSession=anaSession with {Actor="user:bea"};
        Costina.Client.ApiClient ActorClient(string token,Costina.Client.SessionInfo identity,string? where=null)
        {
            var client=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),token,actorServer);
            client.AttachPendingStore(new DpapiPendingStore(identity,where??actorFolder)); return client;
        }
        void Refused(Func<Task> attempt,string what)
        {
            try { attempt().GetAwaiter().GetResult(); } catch(InvalidOperationException) { return; }
            throw new Exception(what);
        }
        var glassOrder=new{items=new[]{new{productId="albarino",priceCents=450}},stop=Array.Empty<string>()};
        // «ana» manda una orden; el motor la aplica y la respuesta se pierde: queda incierta en el fichero durable del rol.
        string LostOrder(string? where=null)
        {
            using var anaClient=ActorClient(anaToken,anaSession,where);
            actorServer.LoseNextResponse=true;
            try { anaClient.SendAsync("erp/catalog/commands/glass-sale-set",glassOrder,"Vender por copas Albariño").GetAwaiter().GetResult(); throw new Exception("A lost response must leave the order uncertain."); }
            catch(System.Net.Http.HttpRequestException) { }
            return anaClient.Pending?.Key??throw new Exception("The order whose response was lost stays uncertain.");
        }
        var anaKey=LostOrder();
        var stored=File.ReadAllBytes(Directory.GetFiles(actorFolder,"*.bin").Single()); var storedHeader=System.Text.Encoding.UTF8.GetString(stored,0,Array.IndexOf(stored,(byte)'\n'));
        if(!storedHeader.Contains("\"v\":3",StringComparison.Ordinal)||!storedHeader.Contains("\"actor\":\"user:ana\"",StringComparison.Ordinal)||storedHeader.Contains("albarino",StringComparison.Ordinal))
            throw new Exception("The clear header names the actor (version 3); the order itself stays encrypted: "+storedHeader);
        using(var beaClient=ActorClient(beaToken,beaSession))
        {
            if(beaClient.Pending is not null) throw new Exception("Another person's uncertain order is never restored as this session's pending order: its retry would run under another actor.");
            if(beaClient.Blocked is null) throw new Exception("Another person's uncertain order blocks this session (one durable slot): nothing new goes over it.");
            if(beaClient.Blocked is not {Outcome:Costina.Client.PendingOutcome.OtherActor,Actor:"user:ana",Command.Description:"Vender por copas Albariño"} foreign||foreign.Key!=anaKey)
                throw new Exception("The block says whose order it is and what it was: "+beaClient.Blocked);
            var before=actorServer.Requests.Length;
            Refused(()=>beaClient.RetryAsync(),"Retrying another person's order is refused.");
            Refused(()=>beaClient.ReconcileAsync(),"Asking the server about another person's order is refused (it would say 'no consta').");
            Refused(()=>beaClient.SendAsync("organization/commands/zone-update",new{id="sala"},"Otra orden"),"No new order goes over another person's uncertain order.");
            if(actorServer.Requests.Length!=before||actorServer.Applied!=1) throw new Exception("The other session sends nothing at all: "+string.Join(" ; ",actorServer.Requests.Skip(before)));
        }
        // Control: con la MISMA persona la orden reaparece y su reintento es seguro (el motor la reconoce por su clave: un solo efecto).
        using(var anaAgain=ActorClient(anaToken,anaSession))
        {
            if(anaAgain.Pending?.Key!=anaKey||anaAgain.Blocked is not null) throw new Exception("The same person gets her uncertain order back.");
            anaAgain.RetryAsync().GetAwaiter().GetResult();
            if(anaAgain.Pending is not null||actorServer.Applied!=1||Directory.GetFiles(actorFolder,"*.bin").Length!=0) throw new Exception("Her identical retry is replayed by the server, applied once, and clears the file.");
        }
        // Descarte explicito: sin consultar ni enviar nada, la orden ajena va a cuarentena y la sesion vuelve a operar con lo suyo.
        LostOrder();
        using(var beaClient=ActorClient(beaToken,beaSession))
        {
            var before=actorServer.Requests.Length;
            beaClient.DiscardBlocked();
            if(beaClient.Blocked is not null||Directory.GetFiles(actorFolder,"*.bin").Length!=0||Directory.GetFiles(actorFolder,"*.cuarentena-*").Length!=1||actorServer.Requests.Length!=before)
                throw new Exception("Discarding another person's order quarantines it and sends nothing.");
            beaClient.SendAsync("organization/commands/zone-update",new{id="sala"},"Otra orden").GetAwaiter().GetResult();
            if(actorServer.Applied!=3||beaClient.Pending is not null) throw new Exception("After the discard the session works normally.");
        }
        // Fichero de 0.40.0: cabecera de version 2, sin actor (el cuerpo cifrado es el mismo). No se sabe de quien es: nunca se reenvia.
        // Se consulta (D6.6): con otra persona no consta (y no se sabe nada); con quien la hizo consta y se da por aplicada.
        var legacyKey=LostOrder();
        var legacyFile=Directory.GetFiles(actorFolder,"*.bin").Single(); var legacyBytes=File.ReadAllBytes(legacyFile); var legacyEnd=Array.IndexOf(legacyBytes,(byte)'\n');
        var header3=JsonSerializer.Deserialize<JsonElement>(legacyBytes.AsSpan(0,legacyEnd));
        var header2=JsonSerializer.SerializeToUtf8Bytes(new{v=2,key=legacyKey,installation=header3.GetProperty("installation").GetString(),scope=header3.GetProperty("scope").GetString(),role=header3.GetProperty("role").GetString()});
        File.WriteAllBytes(legacyFile,[..header2,(byte)'\n',..legacyBytes[(legacyEnd+1)..]]);
        var appliedBefore=actorServer.Applied;
        foreach(var (token,identity,expected) in new[]{(beaToken,beaSession,"unknown"),(anaToken,anaSession,"confirmed")})
            using(var client=ActorClient(token,identity))
            {
                if(client.Pending is not null||client.Blocked is not {Outcome:Costina.Client.PendingOutcome.UnknownActor,Command.Description:"Vender por copas Albariño"} legacy||legacy.Key!=legacyKey)
                    throw new Exception("An order written without its actor is blocked, never restored: "+client.Blocked);
                Refused(()=>client.RetryAsync(),"An order without its actor is never retried.");
                Refused(()=>{client.DiscardBlocked();return Task.CompletedTask;},"An order without its actor is asked about before it can be discarded.");
                if(client.ReconcileAsync().GetAwaiter().GetResult()!=expected) throw new Exception("Asked with "+identity.Actor+", the server says "+expected+".");
                if(expected=="confirmed"?client.Blocked is not null||Directory.GetFiles(actorFolder,"*.bin").Length!=0:client.Blocked is null)
                    throw new Exception("Found with this session it was applied (quarantined); not found, it stays blocked until an explicit discard.");
            }
        if(actorServer.Applied!=appliedBefore||actorServer.Requests.Count(r=>r.StartsWith("POST ",StringComparison.Ordinal)&&r.EndsWith("glass-sale-set",StringComparison.Ordinal))!=4)
            throw new Exception("An order without its actor is never resent: "+string.Join(" ; ",actorServer.Requests));
        Directory.Delete(actorFolder,true);
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
        // C2b: el filtro de la lista de productos es su desplegable de categoria (independiente de la categoria elegida en su pestana) y
        // abarca sus subcategorias: Vinos muestra el vino de Vinos y el tinto de Tintos.
        catalogView.SelectedCategory=catalogView.Categories[1];
        if(!catalogView.AllCategories||catalogView.Products.Length!=4||catalogView.CategoryFilter!="") throw new Exception("Choosing a category in its tab does not filter the product list (C2b).");
        catalogView.CategoryFilter="vinos";
        if(catalogView.AllCategories||!catalogView.Products.Select(p=>p.Product.Id).SequenceEqual(["wine","3760"])) throw new Exception("Filtering by a category lists its products and those of its subcategories: "+string.Join(",",catalogView.Products.Select(p=>p.Product.Id)));
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
        // C2b: el rechazo ya no sale del comando como excepcion (cerraba el programa): va a la barra de estado y no se envia nada.
        window.Shell.Status="";
        catalogView.SetPriceCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        if(!window.Shell.Status.StartsWith("Vuelve a elegir la tarifa",StringComparison.Ordinal)) throw new Exception("Pricing without a tariff is refused in the status bar: "+window.Shell.Status);
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
        catalogView.CategoryFilter="vinos"; catalogView.SelectedProduct=catalogView.Products[0];
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
        // C2b: a la derecha del nombre, el precio General de su primera presentacion; debajo, la ruta corta de su categoria y cuantas
        // presentaciones activas tiene.
        if(!Texts(wineItem).Contains("Vino de ensayo")||!Texts(wineItem).Contains("9,50 €")||!Texts(wineItem).Contains("Bebidas › Vinos · 2 presentaciones")) throw new Exception("The row shows the name with its General price and, below, category and presentations: "+string.Join(" | ",Texts(wineItem)));
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
        var soon=new Costina.Client.DiagnosticsDto("installation","0.41.0-c3a",DateTimeOffset.Now,new(true,5443,DateTimeOffset.UtcNow.AddDays(12).AddMinutes(5),"costina-server.local"),new(true,DateTimeOffset.Now.AddHours(-3)),true);
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
        // C2b: sin grupos, como en Inicio: cada fila dice su tipo y su estacion por su nombre; los quitados se cuentan en la cabecera y en la casilla.
        if(center.Rows.Length!=2||center.Rows[0].Kind!="Comandero · Sala 1"||center.Rows[1].Kind!="Pantalla de cocina · Cocina fría"
            ||center.Summary!="2 dispositivos activos: 1 comandero y 1 pantalla de cocina · 1 quitado"||center.RevokedToggleText!="Mostrar también los quitados (1)"||!center.HasRemoved)
            throw new Exception("The list names kind and station of the active devices and hides the removed ones: "+center.Summary);
        center.ShowRevoked=true;
        if(center.Rows.Length!=3||!center.Rows[2].Device.StateLabel.StartsWith("Quitado el ",StringComparison.Ordinal)||center.Rows[2].Kind!="Pantalla de cocina · Cocina caliente"||!center.Rows[2].Removed) throw new Exception("Removed devices appear on demand, stated in text.");
        if(!center.Rows[0].Text.Contains("· Conectado · aprobado por jefa el ")||!center.Rows[0].Approved.StartsWith("Aprobado por jefa el ",StringComparison.Ordinal)) throw new Exception("Device rows state presence in words and the approver without 'user:': "+center.Rows[0].Text);
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
        // D6.7 (PC): en la ventana, la orden incierta de otra persona sale en el aviso rojo con quien la hizo, que era y como comprobarla;
        // sin reintento ni "Consultar al servidor", y nada nuevo se envia. El descarte explicito la aparta a cuarentena sin enviar nada.
        var otherFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-checks-"+Guid.NewGuid().ToString("N"));
        LostOrder(otherFolder);
        levels.AssumeResponsible(beaSession with {Modules=["dining"]});
        levels.Api=ActorClient(beaToken,beaSession,otherFolder); levels.Pending=levels.Api.Pending; levels.Blocked=levels.Api.Blocked; levels.Status=""; levels.Sync(); window.UpdateLayout();
        var sentBefore=actorServer.Requests.Length;
        if(levels.Writable||levels.HasPending||levels.RetryCommand.CanExecute(null)||levels.ReconcileCommand.CanExecute(null)||!levels.DiscardCommand.CanExecute(null)||levels.BlockedAsksServer)
            throw new Exception("Another person's order offers only the explicit discard: no retry, no question to the server, nothing new.");
        if(!Painted(window).Any(t=>t.StartsWith("ORDEN SIN CONFIRMAR DE OTRA PERSONA, hecha por «ana»: «Vender por copas Albariño» (identificador ",StringComparison.Ordinal)&&t.Contains("pide a «ana» que entre como responsable",StringComparison.Ordinal)))
            throw new Exception("The red notice says whose order it is, what it was and how to check it: "+levels.BlockedText);
        if(((Button)window.FindName("ReconcileBlockedButton")).IsVisible||!((Button)window.FindName("DiscardBlockedButton")).IsVisible) throw new Exception("Only the discard is offered for another person's order.");
        RefuseRaw(window,"pending-other-actor"); Save(Shot(window),"pending-other-actor.png");
        Press((Button)window.FindName("DiscardBlockedButton"),"Descartar conservando evidencia");
        if(levels.HasBlocked||!levels.Writable||!levels.Status.StartsWith("Orden anterior descartada sin reenviarla.",StringComparison.Ordinal)||Directory.GetFiles(otherFolder,"*.bin").Length!=0
            ||Directory.GetFiles(otherFolder,"*.cuarentena-*").Length!=1||actorServer.Requests.Length!=sentBefore) throw new Exception("The explicit discard quarantines the file and sends nothing: "+levels.Status);
        levels.OnUnauthorized().GetAwaiter().GetResult();   // sin cierre de sesion por red: cierra el cliente y libera su hueco
        Directory.Delete(otherFolder,true);
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
        if(window.Shell.Service.Offers.Length!=3||window.Shell.Service.SelectedOffer?.Id!="LAB-TASTING"||window.Shell.Service.SelectedOffer.ToString()!="Menú de ensayo · Menú degustación") throw new Exception("Offers fill the open form.");
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
        // E4d: el tipo son dos botones (Menu degustacion y Carta); "Menu con eleccion" es una casilla de "Mas opciones" que traduce a set-menu.
        if(!offersView.KindChoices.Select(k=>k.Id+"="+k.Label).SequenceEqual(["tasting=Menú degustación","a-la-carte=Carta"])) throw new Exception("The offer kind buttons offer the tasting menu and the carta.");
        offersView.StartOfferCommand.Execute(null); offersView.OfferKind="a-la-carte";
        if(offersView.NewMenuOffer||!offersView.OfferProductText.StartsWith("Carta:",StringComparison.Ordinal)) throw new Exception("A new carta asks for no menu product.");
        offersView.KindSegment="tasting"; offersView.WithChoice=true;
        if(offersView.OfferKind!="set-menu"||!offersView.NewMenuOffer||offersView.OfferProductText.StartsWith("Carta:",StringComparison.Ordinal)) throw new Exception("A new menu with choice asks for its price or product: "+offersView.OfferKind);
        offersView.SelectedOffer=offersView.Offers[0];
        offersView.SelectedOffer=offersView.Offers[1];
        if(offersView.OfferKind!="set-menu"||offersView.OfferService!="lunch"||offersView.OfferValidFrom!="01/09/2026"||offersView.OfferDays[5]||!offersView.OfferDays[0]||offersView.Weekdays!=31) throw new Exception("Selecting an offer fills its form, weekdays included.");
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
        // C2b: la captura de Salas, mesas y estaciones es la del estado que deja su seccion (la organizacion del comedor de C2), no la de aqui.
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
        // C2b: la cuenta de cada una de esas mesas, como la calcula el motor (SettlementAccount): lo anulado no suma, lo pagado es neto, la
        // cobertura y lo que se anuncia salen de los importes. Consumos de su oferta y de la carta; coberturas distintas: sin pagar, pago
        // parcial (Mesa 4), saldada (Mesa 8, terminada: se anuncia cerrar) y pagado de mas (Terraza 1: se anuncia devolver).
        Costina.Client.ChargeDto Charge(string id,string what,int quantity,long unit,string? voided=null)=>new(id,what,quantity,unit,voided is not null,voided,quantity*unit);
        Costina.Client.AccountDto Bill(string serviceId,Costina.Client.ChargeDto[] charges,params (string Method,long Cents,int Ago)[] payments)
        {
            var total=charges.Where(c=>!c.Voided).Sum(c=>c.TotalCents); var paid=payments.Sum(p=>p.Cents);
            var balance=Math.Max(0,total-paid); var credit=Math.Max(0,paid-total);
            var coverage=paid>total?"Credit":paid==total?"Paid":paid==0?"Unpaid":"PartiallyPaid";
            string[] actions=["add-product","payment",..(charges.Any(c=>!c.Voided)?new[]{"void-charge"}:[]),..(credit>0?new[]{"refund"}:[]),..(balance==0&&credit==0?new[]{"close"}:[])];
            return new("cuenta-"+serviceId,serviceId,"Open",total,paid,balance,credit,coverage,charges,
                [..payments.Select((p,i)=>new Costina.Client.PaymentDto("pago-"+serviceId+"-"+i,p.Method,p.Cents,c2Now.AddMinutes(-p.Ago)))],actions,[..charges.Where(c=>!c.Voided).Select(c=>c.Id)]);
        }
        foreach(var bill in new[]{
            Bill("s1",[Charge("s1-menu","Degustación Costiña · Por persona",2,6500),Charge("s1-vino","Albariño Pazo de Señoráns · Botella",1,2600),Charge("s1-agua","Agua mineral · Botella",2,250)]),
            Bill("s3",[Charge("s3-pulpo","Pulpo á feira · Ración",1,1650),Charge("s3-croquetas","Croquetas de la casa · Ración",1,900),Charge("s3-cerveza","Estrella Galicia · Botellín",2,280)]),
            Bill("s4",[Charge("s4-menu","Menú del día · Por persona",4,1850),Charge("s4-vino","Vino de la casa · Botella",1,1200),Charge("s4-agua","Agua mineral · Botella",2,250),
                Charge("s4-cafe","Café solo · Unidad",1,150,"Pedido por error")],("card",4000,6)),
            Bill("s6",[Charge("s6-menu","Degustación Costiña · Por persona",2,6500)]),
            Bill("s7",[Charge("s7-pulpo","Pulpo á feira · Ración",1,1650),Charge("s7-lubina","Lubina a la plancha · Ración",1,2200),Charge("s7-copas","Albariño Pazo de Señoráns · Copa",2,450)]),
            Bill("s8",[Charge("s8-menu","Menú del día · Por persona",3,1850),Charge("s8-agua","Agua mineral · Botella",1,250),Charge("s8-cafe","Café solo · Unidad",3,150)],("cash",6250,4)),
            Bill("st1",[Charge("st1-pulpo","Pulpo á feira · Ración",2,1650),Charge("st1-cerveza","Estrella Galicia · Botellín",4,280)],("cash",5000,9))})
            server.AccountDetails[bill.ServiceId]=bill;
        server.CheckoutCatalog=[new("agua","Agua mineral","Botella",250,"botella","Bebidas","general"),new("albarino","Albariño Pazo de Señoráns","Botella",2600,"botella","Vinos","general"),
            new("albarino","Albariño Pazo de Señoráns","Copa",450,"copa","Vinos","general"),new("cafe","Café solo","Unidad",150,"unidad","Cafés","general"),
            new("estrella","Estrella Galicia","Botellín",280,"botellin","Cervezas","general"),new("vino-casa","Vino de la casa","Botella",1200,"botella","Vinos","general")];
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
        // E4d: abrir mesa propone SIEMPRE el menu del dia (la primera oferta vigente), aunque la vez anterior se eligiera otra.
        service.CancelOpenCommand.Execute(null); service.SelectedOffer=service.Offers[2]; service.StartOpenCommand.Execute(null); window.UpdateLayout();
        if(!service.OpenForm||service.SelectedOffer?.Id!=service.Offers[0].Id) throw new Exception("Opening a table proposes the daily menu (the first available offer), not the last choice: "+service.SelectedOffer?.Id);
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
        // ===== C3b (plan 2026-10-15, seccion 4.4): «Añadir consumicion» en la ficha de la mesa del Comedor =====
        // El MISMO comedor de C2 y sus cuentas: el catalogo operativo (GET /catalog, SIN precios) trae las bebidas de esas cuentas (Albarino,
        // Vino de la casa, Agua mineral, Cafe solo, Estrella Galicia) y otras de la bodega; un plato de cocina y el producto-menu no son de
        // Bodega y bebidas. La Mesa 4 ya lleva lo de su cuenta (el cafe, anulado en Caja). El motor simulado aplica la ronda como el de verdad
        // (idempotente por clave, con la version leida). En el nivel Puesto (dispositivo de sala) y con el almacen DPAPI real de la orden
        // incierta, para ver que la ronda nunca va a el antes de «Enviar».
        server.RoundClock=c2Now; var c3bStart=server.Requests.Length;
        string Photo(string id,Color glassColour,Color label){ var bytes=Bottle(glassColour,label,new PngBitmapEncoder()); server.Images[id]=("image/png",bytes); return Costina.Client.ProductImages.Sha256(bytes); }
        string albarinoSha=Photo("albarino",Color.FromRgb(0xC9,0xA2,0x3C),Color.FromRgb(0xF4,0xEC,0xD8)),ferreiroSha=Photo("ferreiro",Color.FromRgb(0x6B,0x8F,0x3A),Color.FromRgb(0xFF,0xFF,0xFF)),
            guimaroSha=Photo("guimaro",Color.FromRgb(0x5E,0x17,0x25),Color.FromRgb(0xEB,0xDC,0xB8)),aguaSha=Photo("agua",Color.FromRgb(0x4F,0x8F,0xC9),Color.FromRgb(0xFF,0xFF,0xFF));
        Costina.Client.CatalogItem Sell(string id,string presentationId,string name,string presentation,string family,string? region,string? sha=null,bool glass=false,string? reference=null,string? section="bodega")
            =>new(id,presentationId,name,presentation,"cat-"+id,region??family,sha,section,family,region is null?[family]:[family,region],reference,glass);
        server.Sellables=[
            Sell("albarino","botella-0-75-l","Albariño Pazo de Señoráns","Botella 0.75 L","Blancos","Rías Baixas",albarinoSha,reference:"V-1001"),
            Sell("albarino","copa","Albariño Pazo de Señoráns","Copa","Blancos","Rías Baixas",albarinoSha,glass:true,reference:"V-1001"),
            Sell("ferreiro","botella-0-75-l","Do Ferreiro Albariño 2022","Botella 0.75 L","Blancos","Rías Baixas",ferreiroSha,reference:"V-1002"),
            Sell("guimaro","copa","Guímaro Mencía 2022","Copa","Tintos","Ribeira Sacra",guimaroSha,glass:true),
            Sell("guimaro","botella-0-75-l","Guímaro Mencía 2022","Botella 0.75 L","Tintos","Ribeira Sacra",guimaroSha),
            Sell("lalama","copa","Lalama 2019","Copa","Tintos","Ribeira Sacra",glass:true),Sell("lalama","botella-0-75-l","Lalama 2019","Botella 0.75 L","Tintos","Ribeira Sacra"),
            Sell("vino-casa","botella","Vino de la casa","Botella","Tintos","Ribeiro"),Sell("vino-casa","copa","Vino de la casa","Copa","Tintos","Ribeiro",glass:true),
            Sell("agua","botella-1-l","Agua mineral","Botella 1 L","Refrescos y aguas",null,aguaSha),Sell("agua-gas","unit","Agua con gas Mondariz","Unidad","Refrescos y aguas",null),
            Sell("estrella","botellin","Estrella Galicia","Botellín","Cervezas",null),Sell("cafe","unit","Café solo","Unidad","Cafés e infusiones",null),
            Sell("pulpo","racion","Pulpo á feira","Ración","Entrantes",null,section:"cocina"),Sell("menu-dia","person","Menú del día","Por persona","Menús",null,section:null)];
        Costina.Client.ConsumptionDto[] mesa4Had=[new("had-vino","vino-casa","botella","Vino de la casa","Botella",1,null,c2Now.AddMinutes(-50)),
            new("had-agua","agua","botella-1-l","Agua mineral","Botella 1 L",2,null,c2Now.AddMinutes(-50)),new("had-cafe","cafe","unit","Café solo",null,1,null,c2Now.AddMinutes(-20),true)];
        Costina.Client.BoardEntry[] c3bBoard=[..diningBoard.Select(AsStation).Select(b=>b with {Consumptions=b.Service.Id=="s4"?mesa4Had:[]})];
        var puestoSession=new Costina.Client.SessionInfo("service","t","c","l",["open","add-consumption","add-consumptions"],"inst-checks","0.42.0-c3b",Actor:"device:pc-principal",StationName:"Sala 1",Modules:["dining"]);
        var roundFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"costina-checks-"+Guid.NewGuid().ToString("N"));
        var drinks=service.Drinks; var drinksPicker=(FrameworkElement)serviceView.FindName("DrinksPicker"); var pickerTray=(FrameworkElement)drinksPicker.FindName("PickerRound");
        var fichaPanel=(FrameworkElement)serviceView.FindName("TablePanel"); var addConsumption=Named("AddConsumptionButton");
        bool InFicha(string name)=>((FrameworkElement)serviceView.FindName(name)).IsVisible;
        Button Format(string product,string top){ window.UpdateLayout(); return Descendants<Button>(drinksPicker).FirstOrDefault(b=>b.IsVisible&&b.DataContext is Costina.Desktop.ViewModels.DrinkChoice c&&c.Product.Id==product&&c.Top==top)??throw new Exception("No button "+top+" painted for "+product); }
        Button InTray(string name)=>(Button)pickerTray.FindName(name);
        string DrinkRows()=>string.Join(" | ",drinks.Rows.Select(r=>r.Name+" ["+r.Detail+"] "+string.Join("/",r.Choices.Select(c=>c.Top+(c.HasBottom?" "+c.Bottom:"")))));
        string HadText()=>string.Join(" | ",drinks.Had.Select(h=>h.Quantity+" "+h.Name+(h.HasDetail?" ("+h.Detail+")":"")+(h.Voided?" ANULADO":"")));
        // Lo pintado no lleva importes ni codigos: ni euros ni cifras con centimos, ni el codigo de un producto o de una presentacion.
        var amount=new Regex(@"€|\d+,\d{2}\b",RegexOptions.CultureInvariant);
        var codes=new Regex(@"(?<![\w-])(albarino|ferreiro|guimaro|lalama|vino-casa|agua-gas|estrella|cafe|pulpo|menu-dia|botella-0-75-l|botella-1-l|botellin|unit|racion|cat-[\w-]+|s4|M4)(?![\w-])",RegexOptions.CultureInvariant);
        void NoMoneyNoCodes(string what)
        {
            RefuseRaw(window,what);
            if(Painted(serviceView).FirstOrDefault(t=>amount.IsMatch(t)||codes.IsMatch(t)) is {} leak) throw new Exception(what+": no amounts and no codes are painted, found '"+leak+"'.");
        }
        // Sin la accion anunciada (un servidor anterior, o un rol que no la tiene) no hay «Añadir consumicion»; lo que lleva la mesa, si.
        window.Shell.OnUnauthorized().GetAwaiter().GetResult();
        window.Shell.Session=puestoSession with {Actions=["open"]};
        server.Board=c3bBoard;
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        window.Shell.Api.AttachPendingStore(new DpapiPendingStore(window.Shell.Session!,roundFolder));
        window.Shell.Status=""; tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the Puesto reads the plan with what each table has");
        Pick("Mesa 4","s4");
        if(addConsumption.IsVisible||drinks.ShowAdd||drinks.OpenCommand.CanExecute(null)) throw new Exception("Without add-consumptions announced there is no «Añadir consumición».");
        if(!InFicha("HadBlock")||HadText()!="1× Vino de la casa (Botella · Toda la mesa) | 2× Agua mineral (Botella 1 L · Toda la mesa) | 1× Café solo (Toda la mesa) ANULADO")
            throw new Exception("What the table has is painted from the board: "+HadText());
        // Con la accion anunciada (el Puesto es un dispositivo de sala: la tiene), el boton sale habilitado y abre SIN pedir la contrasena.
        window.Shell.Session=puestoSession; window.UpdateLayout();
        if(!window.Shell.IsStation||!addConsumption.IsVisible||!addConsumption.IsEnabled) throw new Exception("At the Puesto level an open table offers «Añadir consumición», enabled by the announced add-consumptions.");
        var readsBeforePicker=server.Requests.Length;
        Press(addConsumption,"Añadir consumición");
        Wait(()=>!window.Shell.Busy&&drinks.Rows.Length>0,"the picker reads the operational catalog"); window.UpdateLayout();
        if(window.Shell.ElevationRequested||window.Shell.LoginCardVisible||window.Shell.RequestedView is not null) throw new Exception("«Añadir consumición» at the Puesto level never asks for the responsible's password.");
        if(!server.Requests.Skip(readsBeforePicker).Any(r=>r.Method=="GET"&&r.Path=="catalog")||server.Requests.Skip(c3bStart).Any(r=>r.Path.StartsWith("checkout/catalog",StringComparison.Ordinal)))
            throw new Exception("The picker reads GET /catalog (no prices), never /checkout/catalog.");
        if(!InFicha("DrinksPicker")||InFicha("DetailScroll")||Named("PrimaryActionButton").IsVisible||Math.Abs(fichaPanel.ActualWidth-480)>0.5||!InFicha("DrinksRestrictions"))
            throw new Exception("The picker opens INSIDE the ficha (480 px), with the restrictions still on top and the courses and the big action hidden: "+fichaPanel.ActualWidth);
        string ChipText()=>string.Join(", ",drinks.Chips.Select(c=>c.Label+" "+c.Count+(c.IsCurrent?"*":"")));
        if(ChipText()!="Por copa 4*, Blancos 2, Cafés e infusiones 1, Cervezas 1, Refrescos y aguas 2, Tintos 3"||drinks.ListTitle!="Por copa"||drinks.ListDetail!="4 vinos por copa · de la A a la Z")
            throw new Exception("Quick accesses: «Por copa» first and chosen, then the families A to Z with their counts (no kitchen dish, no menu): "+ChipText());
        if(DrinkRows()!="Albariño Pazo de Señoráns [Blancos › Rías Baixas] Copa/Botella 0.75 L | Guímaro Mencía 2022 [Tintos › Ribeira Sacra] Copa/Botella 0.75 L | Lalama 2019 [Tintos › Ribeira Sacra] Copa/Botella 0.75 L | Vino de la casa [Tintos › Ribeiro] Copa/Botella")
            throw new Exception("One row per product, family › region and one button per presentation, the glass first: "+DrinkRows());
        if(!drinks.Repeats.Select(r=>r.Name+" · "+r.Detail).SequenceEqual(["Vino de la casa · Botella","Agua mineral · Botella 1 L"])) throw new Exception("«En esta mesa» repeats what was sent and not voided: "+string.Join(", ",drinks.Repeats.Select(r=>r.Name)));
        Wait(()=>drinks.Rows.Where(r=>r.Product.ImageSha is not null).All(r=>r.Thumbnail is not null)&&window.Shell.Catalog.Images.Waiting==0,"the rows show their photos");
        if(drinks.Rows.Single(r=>r.Product.Id=="lalama") is not {Thumbnail:null,Initials:"L"}) throw new Exception("A product without a photo shows its initials, without the vintage.");
        // Familias: Tintos con sus subfamilias (la region); elegir una filtra.
        Press(Descendants<Button>(drinksPicker).First(b=>b.DataContext is Costina.Desktop.ViewModels.ChipRow{Label:"Tintos"}),"Tintos");
        if(!drinks.SubChips.Select(s=>s.Label).SequenceEqual(["Todas","Ribeira Sacra","Ribeiro"])||drinks.Rows.Length!=3||drinks.HasRepeats) throw new Exception("A family shows its subfamilies (2 or more) and its rows; «En esta mesa» only on the entrance.");
        Press(Descendants<Button>(drinksPicker).First(b=>b.DataContext is Costina.Desktop.ViewModels.SubChipRow{Label:"Ribeiro"}),"Ribeiro");
        if(DrinkRows()!="Vino de la casa [Tintos › Ribeiro] Copa/Botella") throw new Exception("A subfamily narrows the rows: "+DrinkRows());
        // Buscar «albar»: sin tildes, por nombre; el que empieza por lo escrito va primero. Un producto con una sola presentacion, «Añadir».
        var drinksSearch=(TextBox)drinksPicker.FindName("DrinksSearch");
        drinksSearch.Text="a"; window.UpdateLayout();
        if(drinks.Hint!="Escribe una letra más para buscar.") throw new Exception("One letter asks for one more.");
        drinksSearch.Text="albar"; window.UpdateLayout();
        if(drinks.Query!="albar"||drinks.ListTitle!="2 resultados"||drinks.Chips.Any(c=>c.IsCurrent)||drinks.HasSubChips
            ||DrinkRows()!="Albariño Pazo de Señoráns [Blancos › Rías Baixas] Copa/Botella 0.75 L | Do Ferreiro Albariño 2022 [Blancos › Rías Baixas · Botella 0.75 L] Añadir")
            throw new Exception("Searching «albar» finds both albariños, the one that starts with it first: "+DrinkRows());
        var postsBeforeRound=server.Count("POST","");
        Press(Format("albarino","Copa"),"Copa"); Press(Format("albarino","Copa"),"Copa otra vez"); Press(Format("ferreiro","Añadir"),"Añadir Do Ferreiro");
        if(drinks.Summary!="Ronda · 3 sin enviar"||drinks.Names!="Albariño Pazo de Señoráns · Copa, Do Ferreiro Albariño 2022 · Botella 0.75 L"||drinks.Rows[0].InRoundText!="2 en la ronda")
            throw new Exception("A tap adds 1 to the round for the whole table: "+drinks.Summary+" / "+drinks.Names);
        // La ronda es un BORRADOR EN MEMORIA: llenarla no manda nada ni escribe nada en el almacen de la orden incierta.
        if(server.Count("POST","")!=postsBeforeRound||window.Shell.Api.Pending is not null||(Directory.Exists(roundFolder)&&Directory.GetFiles(roundFolder,"*.bin").Length!=0))
            throw new Exception("Filling the round sends nothing and stores nothing: it is a draft in memory, never a queue.");
        // «Para»: toda la mesa o un comensal, con sus restricciones escritas.
        Press(InTray("RoundToggleButton"),"Ver la ronda"); window.UpdateLayout();
        var guestBox=Descendants<ComboBox>(pickerTray).First(c=>c.IsVisible&&c.DataContext is Costina.Desktop.ViewModels.RoundLineRow{Index:0});
        if(!guestBox.Items.Cast<Costina.Desktop.ViewModels.GuestChoice>().Select(g=>g.Label).SequenceEqual(["Toda la mesa","Comensal 1","Comensal 2 · ALERGIA frutos secos (grave)","Comensal 3 · Intolerancia lactosa (leve)","Comensal 4"])
            ||guestBox.SelectedValue as string!="") throw new Exception("A line is for the whole table by default, and each guest is named with their restrictions in words.");
        guestBox.SelectedIndex=3;   // como una persona: elegir «Comensal 3» en el desplegable de la primera linea
        Wait(()=>drinks.RoundLines.Length==2&&drinks.RoundLines[0].GuestKey=="3","the first line is for Comensal 3"); window.UpdateLayout();
        if(drinks.Lines[0].GuestPosition!=3||drinks.Lines[1].GuestPosition is not null||!Painted(pickerTray).Contains("Comensal 3 · Intolerancia lactosa (leve)")) throw new Exception("The chosen guest is the line's, written with their restriction.");
        var drinksResults=(FrameworkElement)drinksPicker.FindName("DrinksResults");
        if(window.ActualWidth>=1270&&drinksResults.ActualHeight<60) throw new Exception("With the round unfolded the results keep some room: "+drinksResults.ActualHeight);
        // Captura del selector (como la maqueta «5 · En el PC»): la busqueda, las filas con foto y Copa / Botella, y la ronda sin enviar
        // plegada en su barra. Cabe a 1.280 x 860.
        Press(InTray("RoundToggleButton"),"Ocultar la ronda"); window.UpdateLayout();
        if(drinks.TrayOpen||window.ActualWidth>=1270&&Descendants<FrameworkElement>(drinksResults).Where(e=>e.DataContext is Costina.Desktop.ViewModels.DrinkRow&&e is Border).Any(row=>row.TranslatePoint(new Point(0,row.ActualHeight),drinksResults).Y>drinksResults.ActualHeight+0.5))
            throw new Exception("With the round folded both results are in view.");
        window.Shell.Status=""; window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
        NoMoneyNoCodes("comedor-consumicion");
        if(!Painted(serviceView).Contains("Ronda · 3 sin enviar")||!Painted(serviceView).Contains("Añadir consumición")||!Painted(serviceView).Contains("Comensal 2 · ALERGIA frutos secos (grave)"))
            throw new Exception("The picker paints its round and the restrictions stay written on top.");
        var sendButton=InTray("SendRoundButton");
        if(!sendButton.IsEnabled||sendButton.TranslatePoint(new Point(0,sendButton.ActualHeight),serviceView).Y>serviceView.ActualHeight+0.5) throw new Exception("«Enviar» is enabled and in view.");
        if(NestedScrolls(serviceView) is {Length:>0} nestedPicker) throw new Exception("The picker nests scroll viewers: "+string.Join(", ",nestedPicker));
        if(window.ActualWidth>=1270||window.Shell.SideNavCompact)
            foreach(var part in new[]{(FrameworkElement)drinksPicker.FindName("DrinksResults"),pickerTray,(FrameworkElement)serviceView.FindName("DrinksRestrictions")})
                if(Clipped(part) is {Length:>0} clippedPicker) throw new Exception("The picker clips text: "+string.Join(" | ",clippedPicker));
        Save(Shot(window),"comedor-consumicion.png");
        // «Enviar» = UNA orden add-consumptions con la version de la mesa LEIDA y las lineas de la ronda; nada de Caja.
        Press(sendButton,"Enviar");
        Wait(()=>!window.Shell.Busy&&!drinks.HasLines,"the round is confirmed");
        var roundPosts=server.Requests.Where(r=>r.Method=="POST").Skip(postsBeforeRound).ToArray();
        if(roundPosts.Length!=1||roundPosts[0].Path!="dining/services/s4/commands/add-consumptions"
            ||roundPosts[0].Body!="{\"expectedVersion\":5,\"lines\":[{\"productId\":\"albarino\",\"presentationId\":\"copa\",\"quantity\":2,\"guestPosition\":3},{\"productId\":\"ferreiro\",\"presentationId\":\"botella-0-75-l\",\"quantity\":1,\"guestPosition\":null}]}")
            throw new Exception("«Enviar» sends exactly ONE add-consumptions with the read version and the round: "+string.Join(" ; ",roundPosts.Select(p=>p.Path+" "+p.Body)));
        if(server.Requests.Skip(c3bStart).Any(r=>r.Path.StartsWith("checkout/",StringComparison.Ordinal))) throw new Exception("Nothing of Caja is read or sent from the dining room.");
        if(drinks.EmptyRoundText!=Costina.Desktop.ViewModels.DrinksViewModel.RoundSent||window.Shell.Status!="Operación confirmada por el servidor: ronda enviada a Mesa 4."||window.Shell.HasPending)
            throw new Exception("A confirmed round empties and says so: "+window.Shell.Status);
        if(HadText()!="1× Vino de la casa (Botella · Toda la mesa) | 2× Agua mineral (Botella 1 L · Toda la mesa) | 1× Café solo (Toda la mesa) ANULADO | 2× Albariño Pazo de Señoráns (Copa · Comensal 3) | 1× Do Ferreiro Albariño 2022 (Botella 0.75 L · Toda la mesa)")
            throw new Exception("What the table has includes the confirmed round after the re-read: "+HadText());
        // version_conflict (cocina movio la mesa entre la lectura y el envio): la ronda SIGUE, la ficha se relee y se ofrece enviar de nuevo,
        // que es otra orden con otra clave y la version releida. Una relectura del tablero no toca la ronda.
        drinksSearch.Text=""; window.UpdateLayout();
        Press(Descendants<Button>(drinksPicker).First(b=>b.DataContext is Costina.Desktop.ViewModels.ChipRow{Label:"Cervezas"}),"Cervezas");
        Press(Format("estrella","Añadir"),"Añadir Estrella Galicia");
        server.Board=[..server.Board.Select(b=>b.Service.Id=="s4"?b with {Version=6}:b)];
        var postsBeforeConflict=server.Count("POST","");
        Press(InTray("SendRoundButton"),"Enviar (la mesa ha cambiado)");
        Wait(()=>!window.Shell.Busy&&drinks.Conflict,"the conflicted round stays");
        var conflicted=server.Requests.Where(r=>r.Method=="POST").Last();
        if(server.Count("POST","")!=postsBeforeConflict+1||drinks.Summary!="Ronda · 1 sin enviar"||drinks.SendText!="La mesa ha cambiado: enviar de nuevo"||service.Dining?.Version!=6
            ||drinks.RoundMessage!=Costina.Desktop.ViewModels.DrinksViewModel.RoundConflict||window.Shell.Status!=Costina.Desktop.ViewModels.DrinksViewModel.RoundConflict)
            throw new Exception("version_conflict keeps the round, re-reads the table and offers to send again: "+drinks.SendText+" / "+window.Shell.Status);
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"a board re-read with the round unsent");
        if(drinks.Summary!="Ronda · 1 sin enviar"||!drinks.Conflict||!drinks.IsOpen) throw new Exception("A board re-read never overwrites the round.");
        Press(InTray("SendRoundButton"),"La mesa ha cambiado: enviar de nuevo");
        Wait(()=>!window.Shell.Busy&&!drinks.HasLines,"the round is sent again");
        var sentAgain=server.Requests.Where(r=>r.Method=="POST").Last();
        if(sentAgain.Key==conflicted.Key||!sentAgain.Body.StartsWith("{\"expectedVersion\":6,",StringComparison.Ordinal)||drinks.Conflict) throw new Exception("Sending again is a NEW order (new key) with the re-read version: "+sentAgain.Body);
        // Una orden incierta (respuesta perdida): lo enviado YA es esa orden (su aviso la muestra y se reintenta identica) y la ronda se
        // vacia; mientras siga sin confirmar se puede llenar otra ronda, pero no enviar. El reintento no cobra dos veces.
        Press(Descendants<Button>(drinksPicker).First(b=>b.DataContext is Costina.Desktop.ViewModels.ChipRow{Label:"Refrescos y aguas"}),"Refrescos y aguas");
        Press(Format("agua-gas","Añadir"),"Añadir Agua con gas");
        server.LoseNextRound=true;
        Press(InTray("SendRoundButton"),"Enviar (respuesta perdida)");
        Wait(()=>!window.Shell.Busy&&window.Shell.HasPending,"a lost response leaves the round uncertain");
        if(drinks.HasLines||window.Shell.Pending?.Description!="Ronda para Mesa 4: 1 × Agua con gas Mondariz"||Directory.GetFiles(roundFolder,"*.bin").Length!=1)
            throw new Exception("An unconfirmed round IS the durable uncertain order (stored, described) and leaves the tray: "+window.Shell.Pending?.Description);
        var appliedRounds=server.RoundsApplied;
        Press(Format("agua","Añadir"),"Añadir Agua mineral con una orden incierta"); window.UpdateLayout();
        if(drinks.Summary!="Ronda · 1 sin enviar"||drinks.SendCommand.CanExecute(null)||InTray("SendRoundButton").IsEnabled||drinks.LockedText!=Costina.Desktop.ViewModels.DrinksViewModel.LockedHint
            ||!Painted(pickerTray).Contains(Costina.Desktop.ViewModels.DrinksViewModel.LockedHint)) throw new Exception("With an uncertain order the round can be filled but not sent.");
        Await(window.Shell.RetryCommand.ExecuteAsync(null),"retry the same order");
        if(window.Shell.HasPending||server.RoundsApplied!=appliedRounds||Directory.GetFiles(roundFolder,"*.bin").Length!=0||drinks.Had.Count(h=>h.Name=="Agua con gas Mondariz")!=1||!drinks.SendCommand.CanExecute(null))
            throw new Exception("The identical retry confirms ONE effect and the round can be sent again: "+HadText());
        // Cambiar de mesa (o de vista) con una ronda sin enviar pide confirmacion; quedarse no toca nada.
        var readsBeforeLeave=server.Requests.Length;
        Press(TileButton("Mesa 3"),"Mesa 3 with an unsent round"); window.UpdateLayout();
        if(!drinks.Leaving||!InFicha("LeaveRoundLayer")||service.SelectedEntry?.Service.Id!="s4"||server.Requests.Length!=readsBeforeLeave
            ||drinks.LeaveText!="La ronda de Mesa 4 tiene 1 sin enviar. Si cambias de mesa, se pierde: nada de ella ha llegado al servidor.")
            throw new Exception("Changing table with an unsent round asks first and reads nothing: "+drinks.LeaveText);
        RefuseRaw(window,"leave-round");
        Press(Named("StayRoundButton"),"Seguir con la ronda");
        if(drinks.Leaving||drinks.Summary!="Ronda · 1 sin enviar"||service.SelectedEntry?.Service.Id!="s4") throw new Exception("Staying keeps the round and the table.");
        Press((Button)window.FindName("NavHome"),"Inicio with an unsent round");
        if(!drinks.Leaving||tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")||!drinks.LeaveText.Contains("Si vas a «Inicio», se pierde",StringComparison.Ordinal)) throw new Exception("Leaving the view with an unsent round asks first: "+drinks.LeaveText);
        Press(Named("StayRoundButton"),"Seguir con la ronda (vista)");
        // Cerrar el selector: vuelven los pases, lo que lleva la mesa y la accion grande; la ronda sigue, con su bandeja sobre la accion.
        Press((Button)drinksPicker.FindName("ClosePickerButton"),"Cerrar el selector"); window.UpdateLayout();
        if(drinks.IsOpen||!InFicha("FichaRound")||!InFicha("DetailScroll")||!Named("PrimaryActionButton").IsVisible||Math.Abs(fichaPanel.ActualWidth-400)>0.5||drinks.Summary!="Ronda · 1 sin enviar")
            throw new Exception("Closing the picker brings the ficha back (400 px) and keeps the round in its tray.");
        Press(TileButton("Mesa 3"),"Mesa 3 again"); Press(Named("LeaveRoundButton"),"Salir sin enviar");
        Wait(()=>!window.Shell.Busy&&service.Dining?.Data.Id=="s3","leaving discards the round and reads Mesa 3");
        if(drinks.HasLines||drinks.IsOpen||InFicha("FichaRound")||server.RoundsApplied!=appliedRounds) throw new Exception("Leaving without sending discards the draft and sends nothing.");
        // Lo que lleva la mesa (captura): la ronda confirmada, con su comensal y su hora, y lo anulado en Caja (aqui, el agua con gas,
        // como lo dejaria void-charge, que avisa a todos con service.consumptions_changed). Sin importes.
        server.Board=[..server.Board.Select(b=>b.Service.Id!="s4"?b:b with {Consumptions=[..b.Consumptions!.Select(c=>c.Name=="Agua con gas Mondariz"?c with {Voided=true}:c)]})];
        Pick("Mesa 4","s4");
        if(HadText()!="1× Vino de la casa (Botella · Toda la mesa) | 2× Agua mineral (Botella 1 L · Toda la mesa) | 1× Café solo (Toda la mesa) ANULADO | 2× Albariño Pazo de Señoráns (Copa · Comensal 3) | 1× Do Ferreiro Albariño 2022 (Botella 0.75 L · Toda la mesa) | 1× Estrella Galicia (Botellín · Toda la mesa) | 1× Agua con gas Mondariz (Toda la mesa) ANULADO"
            ||drinks.Had.Any(h=>h.Time.Length==0)) throw new Exception("What the table has: the confirmed rounds with their guest and time, and «Anulado en Caja»: "+HadText());
        ((FrameworkElement)serviceView.FindName("HadBlock")).BringIntoView(); window.Shell.Status="";
        window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
        var hadTexts=Painted((FrameworkElement)serviceView.FindName("HadBlock")).ToArray();
        foreach(var text in new[]{"Lo que lleva la mesa","Añadir consumición","Albariño Pazo de Señoráns","Copa · Comensal 3","Anulado en Caja","Los importes solo se ven en Caja.",Costina.Client.Labels.Clock(c2Now)})
            if(!hadTexts.Contains(text)) throw new Exception("«Lo que lleva la mesa» paints '"+text+"': "+string.Join(" | ",hadTexts));
        NoMoneyNoCodes("comedor-lo-que-lleva");
        var hadBlock=(FrameworkElement)serviceView.FindName("HadBlock"); var detailScroll=(FrameworkElement)serviceView.FindName("DetailScroll");
        var hadTop=hadBlock.TranslatePoint(new Point(0,0),detailScroll).Y; var hadBottom=hadBlock.TranslatePoint(new Point(0,hadBlock.ActualHeight),detailScroll).Y;
        if(window.ActualWidth>=1270&&(hadTop<-0.5||hadBottom>detailScroll.ActualHeight+0.5)) throw new Exception("«Lo que lleva la mesa» fits in the ficha at 1.280 x 860: "+hadTop+".."+hadBottom+" of "+detailScroll.ActualHeight);
        Save(Shot(window),"comedor-lo-que-lleva.png");
        // Tambien para el responsable: sin precios (GET /catalog, nunca /checkout/catalog) y sin contrasena que pedir.
        window.Shell.AssumeResponsible(new("main","t","c","l",["open","add-consumption","add-consumptions"],"inst-checks","0.42.0-c3b",Actor:"user:jefa",Modules:["dining"]));
        server.Board=[..server.Board.Select(b=>b with {Service=diningBoard.Single(d=>d.Service.Id==b.Service.Id).Service,Occupancy=diningBoard.Single(d=>d.Service.Id==b.Service.Id).Occupancy})];
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the responsible reads the plan");
        Pick("Mesa 4","s4");
        Press(Named("AddConsumptionButton"),"Añadir consumición (responsable)");
        Wait(()=>!window.Shell.Busy&&drinks.IsOpen&&drinks.Rows.Length>0,"the responsible opens the picker"); window.UpdateLayout();
        if(server.Requests.Skip(c3bStart).Any(r=>r.Path.StartsWith("checkout/",StringComparison.Ordinal))||window.Shell.ElevationRequested) throw new Exception("The responsible's picker reads no prices either.");
        NoMoneyNoCodes("comedor-consumicion responsable");
        Press((Button)drinksPicker.FindName("ClosePickerButton"),"Cerrar el selector (responsable)");
        // De vuelta al estado de C2 para lo que sigue (Inicio, Caja...): responsable, el tablero de C2 sin consumos y la Mesa 4 elegida.
        window.Shell.OnUnauthorized().GetAwaiter().GetResult();
        if(drinks.HasLines||drinks.IsOpen||drinks.Had.Length!=0) throw new Exception("Leaving the session clears the picker.");
        Directory.Delete(roundFolder,true);
        window.Shell.AssumeResponsible(new("main","t","c","l",["open"],"inst-checks","0.37.0-c2",Actor:"user:jefa",Modules:["dining"]));
        server.Board=diningBoard;
        window.Shell.Api=new Costina.Client.ApiClient(new Uri(ChecksEndpoint),new string('c',40),server);
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Await(window.Shell.RefreshCommand.ExecuteAsync(null),"the responsible reads the C2 plan again");
        Pick("Mesa 4","s4"); window.Shell.Status="";
        if(InFicha("HadBlock")||addConsumption.IsVisible) throw new Exception("Against a server without consumptions nor add-consumptions, the ficha is the C2 one.");
        ((ScrollViewer)detailScroll).ScrollToHome(); window.UpdateLayout();   // las capturas de C2 parten de la ficha desde arriba
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
        if(Kpis()!="7 de 12 | 19 (2 menús degustación · 2 con elección · 3 a la carta) | 2 (1 listo para servir) | 1 (Mesa 4 · ALERGIA frutos secos (grave))"||!home.ReviewAlarm||!home.HasReady
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
            ||!HomeNamed("HomeOpenTableButton").IsVisible||!((FrameworkElement)homeView.FindName("ServerNote")).IsVisible||home.DeviceRows.Length!=0||Kpis()!="7 de 12 | 19 (2 menús degustación · 2 con elección · 3 a la carta) | 2 (1 listo para servir) | 1 (Mesa 4 · ALERGIA frutos secos (grave))")
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
        // ===== C2b (plan 2026-10-14): Cuenta / Caja con lista y ficha =====
        // Las cuentas de las MISMAS mesas del plano, servidas por el servidor simulado con sus consumos y su cobertura. Abrir Caja lee el
        // listado, el catalogo de caja y cada cuenta (los importes de las tarjetas); elegir una tarjeta relee SOLO la elegida y la ficha se
        // compone de esa lectura (F01); los botones siguen a lo que anuncia la cuenta y envian los cuerpos de siempre.
        var checkout=window.Shell.Checkout; var checkoutView=(FrameworkElement)window.FindName("CheckoutView");
        T InCheckout<T>(string name) where T:FrameworkElement=>(T)checkoutView.FindName(name);
        var accountList=InCheckout<ListBox>("AccountList"); var accountSearch=InCheckout<TextBox>("AccountSearch");
        bool Paints(string name)=>InCheckout<FrameworkElement>(name).IsVisible;
        bool Usable(string name)=>InCheckout<Button>(name) is {IsVisible:true,IsEnabled:true};
        string LastPost()=>server.Requests.Last(r=>r.Method=="POST") is var post?post.Path+" "+post.Body:"";
        void OpenCheckout(string what)
        {
            var before=server.Count("GET","checkout/accounts");
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); tabs.SelectedItem=(TabItem)window.FindName("CheckoutTab"); window.UpdateLayout();
            Wait(()=>!window.Shell.Busy&&server.Count("GET","checkout/accounts")>before,what); window.UpdateLayout();
        }
        void Choose(string serviceId)
        {
            var before=server.Requests.Length;
            accountList.SelectedItem=checkout.AccountItems.Single(r=>r.Choice.ServiceId==serviceId);   // como pulsar la tarjeta
            Wait(()=>!window.Shell.Busy&&checkout.Account?.Data.ServiceId==serviceId&&checkout.HasContext,"choosing the card of "+serviceId+" reads that account"); window.UpdateLayout();
            var reads=server.Requests.Skip(before).Where(r=>r.Path.StartsWith("checkout/services/",StringComparison.Ordinal)).Select(r=>r.Path).ToArray();
            if(!reads.SequenceEqual(["checkout/services/"+serviceId])) throw new Exception("Choosing a card reads only that account; the other cards keep their read: "+string.Join(", ",reads));
        }
        void Send(string button,string what)
        {
            var before=server.Count("POST","");
            Press(InCheckout<Button>(button),what);
            Wait(()=>!window.Shell.Busy&&server.Count("POST","")>before,what+" is sent"); window.UpdateLayout();
        }
        // Sin cuentas abiertas: la lista explica que falta y ofrece abrir una mesa (cada mesa abierta tiene su cuenta); la ficha no muestra campos.
        server.Accounts=[]; window.Shell.Status="";
        OpenCheckout("Caja reads an empty list");
        if(checkout.AccountItems.Length!=0||!checkout.ShowListEmpty||checkout.ListEmptyHeading!="No hay cuentas abiertas"||!checkout.ListEmptyText.StartsWith("Cada mesa que se abre en Comedor y cocina tiene su cuenta",StringComparison.Ordinal)
            ||checkout.Summary!="Ninguna cuenta abierta ahora."||checkout.HasContext||checkout.EmptyHeading!="Nada que cobrar ahora")
            throw new Exception("Without open accounts the list says what is missing and the ficha shows no fields: "+checkout.ListEmptyHeading+" / "+checkout.EmptyHeading);
        if(!Paints("AccountsEmpty")||Paints("AccountList")||Paints("AccountDetail")||!Paints("AccountEmpty")||!Usable("EmptyOpenTableButton")||Paints("ClearSearchButton")||!Texts(checkoutView).Contains("No hay cuentas abiertas"))
            throw new Exception("The empty list is painted with its explanation and the 'Abrir una mesa' button.");
        RefuseRaw(window,"checkout-empty"); Save(Shot(window),"checkout-empty.png");
        Press(InCheckout<Button>("EmptyOpenTableButton"),"Abrir una mesa (Caja)");
        if(tabs.SelectedItem!=(TabItem)window.FindName("ServiceTab")||!service.OpenForm) throw new Exception("'Abrir una mesa' takes Caja's empty list to the open form of Comedor.");
        service.CancelOpenCommand.Execute(null);
        // Las cuentas de las siete mesas ocupadas: una tarjeta por cuenta con su mesa, su sala y sus personas (del plano) y su total, su
        // cobertura y su pendiente (de SU lectura); el resumen suma lo pendiente.
        // Revision de C2b: el servidor lista las cuentas por identificador de servicio (orden aleatorio): aqui al reves del plano. Las tarjetas y la
        // cuenta elegida por defecto siguen el plano del Comedor.
        server.Accounts=[..diningBoard.AsEnumerable().Reverse().Select(b=>new Costina.Client.AccountChoice(b.Service.Id,b.Service.TableId,"Open"))];
        var readsBeforeCaja=server.Requests.Length;
        OpenCheckout("Caja reads its accounts");
        var cardReads=server.Requests.Skip(readsBeforeCaja).Where(r=>r.Path.StartsWith("checkout/services/",StringComparison.Ordinal)).Select(r=>r.Path).ToArray();
        if(cardReads.Length!=7||cardReads.Distinct().Count()!=7) throw new Exception("Opening Caja reads each open account once, for its card: "+string.Join(", ",cardReads));
        string Cards()=>string.Join(" | ",checkout.AccountItems.Select(r=>$"{r.Table}: {r.TotalText} · {r.Detail} · {r.Coverage} · {r.PendingText}"));
        if(Cards()!="Mesa 1: 161,00 € · Sala principal · 2 personas · Sin pagar · Pendiente 161,00 € | Mesa 3: 31,10 € · Sala principal · 2 personas · Sin pagar · Pendiente 31,10 €"
            +" | Mesa 4: 91,00 € · Sala principal · 4 personas · Pago parcial · Pendiente 51,00 € | Mesa 6: 130,00 € · Sala principal · 2 personas · Sin pagar · Pendiente 130,00 €"
            +" | Mesa 7: 47,50 € · Sala principal · 2 personas · Sin pagar · Pendiente 47,50 € | Mesa 8: 62,50 € · Sala principal · 3 personas · Pagada · Pendiente 0,00 €"
            +" | Terraza 1: 44,20 € · Terraza · 4 personas · Pagado de más · Pendiente 0,00 €"||checkout.Summary!="7 cuentas abiertas · Pendiente 420,60 €")
            throw new Exception("Each card names its table, zone and people from the plan and its amounts and coverage from its own read: "+Cards()+" / "+checkout.Summary);
        if(checkout.SelectedAccount?.ServiceId!="s1"||checkout.DetailTitle!="Mesa 1"||accountList.SelectedItem is not Costina.Desktop.ViewModels.AccountRow{Choice.ServiceId:"s1"})
            throw new Exception("Without a request Caja chooses the first account, as always, and marks its card.");
        // Elegir la tarjeta de la Mesa 4: se lee esa cuenta (y solo esa) y la ficha se compone de la lectura: cabecera, cifras, consumos y pagos.
        Choose("s4");
        if(checkout.DetailTitle!="Mesa 4"||checkout.DetailState!="Abierta"||checkout.CoverageText!="Pago parcial"||checkout.DetailContext!="Sala principal · 4 personas · Menú del día"
            ||checkout.TotalText!="91,00 €"||checkout.PaidText!="40,00 €"||checkout.PendingText!="51,00 €"||checkout.CreditText.Length!=0||checkout.ClosedText.Length!=0)
            throw new Exception("The ficha heads with the table, the account state and its coverage in words, and three figures: "+checkout.DetailContext+" / "+checkout.TotalText+" "+checkout.PaidText+" "+checkout.PendingText);
        if(!checkout.LineRows.Select(l=>$"{l.Description} | {l.Note} | {l.Quantity} | {l.Amount}{(l.Voided?" | anulado":"")}").SequenceEqual(["Menú del día · Por persona | 18,50 € la unidad | 4 | 74,00 €",
                "Vino de la casa · Botella |  | 1 | 12,00 €","Agua mineral · Botella | 2,50 € la unidad | 2 | 5,00 €","Café solo · Unidad | Anulado: Pedido por error | 1 | 1,50 € | anulado"])
            ||checkout.LinesTitle!="Consumos · 3 líneas · 1 anulada"||!checkout.PaymentRows.Select(p=>p.What+" "+p.Amount).SequenceEqual(["Pago · Tarjeta 40,00 €"]))
            throw new Exception("Consumption lines (concept, unit price, quantity, amount, voided) and payments come from the read: "+string.Join(" / ",checkout.LineRows.Select(l=>l.Description+" "+l.Note)));
        // E3.3: anadir un consumo con el buscador: grupos por producto con un boton por presentacion; el boton solo elige.
        checkout.ProductSearch="agua"; window.UpdateLayout();
        if(checkout.ProductGroups.Length!=1||checkout.ProductGroups[0].Name!="Agua mineral"||!checkout.ProductGroups[0].Presentations.Select(p=>p.Label).SequenceEqual(["Botella · 2,50 €"])) throw new Exception("The Caja search groups the sellables by product: "+string.Join(" | ",checkout.ProductGroups.Select(g=>g.Name)));
        var postsBeforePick=server.Count("POST","");
        checkout.PickProductCommand.Execute(checkout.ProductGroups[0].Presentations[0].Choice); window.UpdateLayout();
        if(checkout.SelectedProduct?.Id!="agua"||!checkout.ChosenProductText.StartsWith("Elegido: Agua mineral · Botella",StringComparison.Ordinal)||!checkout.ProductGroups[0].Presentations[0].IsChosen||server.Count("POST","")!=postsBeforePick)
            throw new Exception("Picking a presentation only chooses it (nothing is sent): "+checkout.ChosenProductText);
        checkout.ProductSearch="zzz"; window.UpdateLayout();
        if(checkout.ProductGroups.Length!=0||checkout.ProductGroupsNote!="Ningún producto coincide con «zzz».") throw new Exception("A search without results says so: "+checkout.ProductGroupsNote);
        // C3b: Caja busca con la MISMA regla que «Añadir consumición» y el comandero (CatalogSearch, tabla de casos comun drinks-cases.json):
        // cada palabra en cualquier campo (antes, la frase entera en uno) y primero lo que empieza por lo escrito (antes, el orden del servidor).
        checkout.ProductSearch="vino"; window.UpdateLayout();
        if(!checkout.ProductGroups.Select(g=>g.Name).SequenceEqual(["Vino de la casa","Albariño Pazo de Señoráns"])) throw new Exception("Caja ranks like the table picker: "+string.Join(" | ",checkout.ProductGroups.Select(g=>g.Name)));
        checkout.ProductSearch="pazo albar"; window.UpdateLayout();
        if(!checkout.ProductGroups.Select(g=>g.Name).SequenceEqual(["Albariño Pazo de Señoráns"])) throw new Exception("Caja finds every written word, in any order: "+string.Join(" | ",checkout.ProductGroups.Select(g=>g.Name)));
        checkout.ProductSearch=""; window.UpdateLayout();
        var cajaTexts=Texts(checkoutView).ToArray();
        foreach(var text in new[]{"Mesa 4","Abierta","Pago parcial","Sala principal · 4 personas · Menú del día","91,00 €","40,00 €","51,00 €","Total","Pagado","Pendiente","Menú del día · Por persona",
            "Anulado: Pedido por error","Anulado","Pago · Tarjeta","Añadir un consumo","Concepto","Cantidad","Importe","Otras operaciones","Pago parcial","Pendiente 51,00 €",
            "Registro operativo de ensayo: no emite facturas ni realiza cargos bancarios. Solo el puesto principal.","7 cuentas abiertas · Pendiente 420,60 €"})
            if(!cajaTexts.Contains(text)) throw new Exception("Caja paints '"+text+"'.");
        if(accountList.ItemContainerGenerator.ContainerFromItem(checkout.AccountItems.Single(r=>r.Choice.ServiceId=="s4")) is not ListBoxItem{IsSelected:true}
            ||checkout.AccountItems.Count(r=>accountList.ItemContainerGenerator.ContainerFromItem(r) is ListBoxItem{IsSelected:true})!=1) throw new Exception("The chosen card is marked, and only that one.");
        // Botones segun lo que anuncia una cuenta con pago parcial: cobrar y anadir; ni cerrar, ni devolver, ni reabrir.
        if(!Usable("PayButton")||Paints("CloseAccountButton")||Paints("PayAgainButton")||!Usable("AddChargeButton")||!Usable("FillPendingButton")||InCheckout<Button>("RefundButton").IsEnabled
            ||InCheckout<Button>("ReopenButton").IsEnabled||Paints("CreditBar")||Paints("ClosedBar")) throw new Exception("A partly paid account offers paying and adding; closing, refunding and reopening are not announced.");
        // Formas de pago en botones con su palabra; la elegida es el codigo que viaja (Method) y se pinta como elegida.
        var methodList=InCheckout<ListBox>("MethodList");
        if(checkout.Method!="card"||!checkout.Methods.Select(m=>m.Label+(m.IsCurrent?" (elegida)":"")).SequenceEqual(["Tarjeta (elegida)","Efectivo","Otro"])) throw new Exception("Payment methods are buttons with their words, Tarjeta first.");
        methodList.SelectedIndex=1; window.UpdateLayout();
        if(checkout.Method!="cash"||!checkout.Methods[1].IsCurrent||checkout.Methods.Count(m=>m.IsCurrent)!=1) throw new Exception("Choosing 'Efectivo' sets the method code and paints only it as chosen.");
        var postsBeforeFill=server.Count("POST","");
        Press(InCheckout<Button>("FillPendingButton"),"Todo lo pendiente");
        if(checkout.Amount!="51,00"||server.Count("POST","")!=postsBeforeFill) throw new Exception("'Todo lo pendiente' only writes the pending amount; it sends nothing: "+checkout.Amount);
        // Revision de C2b: el importe rellenado para la Mesa 4 no sobrevive a elegir otra tarjeta (se cobraria en otra cuenta); nada se envia.
        checkout.Note="motivo a medias"; Choose("s1");
        if(checkout.Amount.Length!=0||checkout.Note.Length!=0||server.Count("POST","")!=postsBeforeFill) throw new Exception("Choosing another card empties the amount and the note typed for the previous one: "+checkout.Amount);
        Choose("s4"); Press(InCheckout<Button>("FillPendingButton"),"Todo lo pendiente");
        if(checkout.Amount!="51,00"||checkout.Method!="cash") throw new Exception("Back on Mesa 4 the pending amount is filled again and the method is kept: "+checkout.Amount);
        window.Shell.Status="";
        Send("PayButton","Registrar pago de prueba");
        var payment=server.Requests.Last(r=>r.Method=="POST"); var paymentBody=JsonSerializer.Deserialize<JsonElement>(payment.Body);
        if(payment.Path!="checkout/services/s4/commands/payment"||paymentBody.GetProperty("expectedVersion").GetInt64()!=1||paymentBody.GetProperty("method").GetString()!="cash"
            ||paymentBody.GetProperty("amountCents").GetInt64()!=5100||paymentBody.GetProperty("paymentId").GetString() is not {Length:32}||window.Shell.Status!="Operación confirmada por el servidor: pago de prueba registrado.")
            throw new Exception("'Registrar pago de prueba' sends the usual payment of the READ account with the chosen method: "+payment.Path+" "+payment.Body+" / "+window.Shell.Status);
        if(checkout.Amount.Length!=0||checkout.SelectedAccount?.ServiceId!="s4"||!checkout.HasContext) throw new Exception("After paying the amount is cleared and the same account stays chosen.");
        // Cuenta saldada (Mesa 8): el servidor anuncia cerrar; "Cerrar cuenta saldada" pasa a ser el boton grande y registrar otro pago queda al lado.
        window.Shell.Status="";
        Choose("s8");
        if(checkout.CoverageText!="Pagada"||checkout.PendingText!="0,00 €"||!checkout.CloseAnnounced||!Usable("CloseAccountButton")||Paints("PayButton")||!Usable("PayAgainButton")||InCheckout<Button>("FillPendingButton").IsEnabled)
            throw new Exception("A settled account offers 'Cerrar cuenta saldada' as the big action (announced by the server); paying again stays beside it.");
        RefuseRaw(window,"checkout-settled"); Save(Shot(window),"checkout-settled.png");
        Send("CloseAccountButton","Cerrar cuenta saldada");
        if(LastPost()!="checkout/services/s8/commands/close {\"expectedVersion\":1}"||window.Shell.Status!="Operación confirmada por el servidor: cuenta cerrada.") throw new Exception("Closing sends the usual body for the read account: "+LastPost());
        // Pagado de mas (Terraza 1): se dice con su importe y se ofrece devolver (anunciado), con el importe, la forma de pago y el motivo.
        window.Shell.Status="";
        Choose("st1");
        if(checkout.CoverageText!="Pagado de más"||!checkout.CreditText.StartsWith("Pagado de más: 5,80 €.",StringComparison.Ordinal)||!Paints("CreditBar")||!Usable("RefundButton")||InCheckout<Button>("ReopenButton").IsEnabled||checkout.CloseAnnounced
            ||Paints("PayButton")||!Usable("RefundHintButton")||!Usable("PayAgainButton"))
            throw new Exception("An overpaid account says so and offers refunding (announced); the big action leads to 'Otras operaciones' instead of taking more money: "+checkout.CreditText);
        // Revision de C2b: la tarjeta elegida por el modelo (Terraza 1 es la ultima) se desplaza a la vista de la lista.
        // (ScrollIntoView sobre una lista virtualizada se completa en un paso Loaded posterior del dispatcher: se le deja correr.)
        Wait(()=>{ window.UpdateLayout(); return accountList.ItemContainerGenerator.ContainerFromItem(accountList.SelectedItem) is not null; },"the chosen card gets its container once scrolled into view");
        var chosenCard=accountList.ItemContainerGenerator.ContainerFromItem(accountList.SelectedItem) as ListBoxItem
            ?? throw new Exception($"The chosen card has no realized container: selected={accountList.SelectedItem} items={accountList.Items.Count} visible={accountList.IsVisible} keep={Costina.Desktop.Ui.GetKeepSelectionInView(accountList)} offset={Descendants<ScrollViewer>(accountList).FirstOrDefault()?.VerticalOffset}");
        var chosenTop=chosenCard.TranslatePoint(new Point(),accountList).Y;
        if(chosenTop<0||chosenTop+chosenCard.ActualHeight>accountList.ActualHeight+0.5) throw new Exception("A card chosen by the viewmodel is scrolled into view: top "+chosenTop+" of "+accountList.ActualHeight);
        InCheckout<ScrollViewer>("AccountScroll").ScrollToEnd(); window.UpdateLayout();
        RefuseRaw(window,"checkout-credit"); Save(Shot(window),"checkout-credit.png");
        InCheckout<ScrollViewer>("AccountScroll").ScrollToHome(); window.UpdateLayout();
        checkout.Amount="5,80"; checkout.Note="Sobraba el cambio";
        Send("RefundButton","Devolver lo pagado de más");
        var refund=server.Requests.Last(r=>r.Method=="POST"); var refundBody=JsonSerializer.Deserialize<JsonElement>(refund.Body);
        if(refund.Path!="checkout/services/st1/commands/refund"||refundBody.GetProperty("expectedVersion").GetInt64()!=1||refundBody.GetProperty("amountCents").GetInt64()!=580
            ||refundBody.GetProperty("reason").GetString()!="Sobraba el cambio"||refundBody.GetProperty("method").GetString()!="cash"||checkout.Note.Length!=0||checkout.Amount.Length!=0)
            throw new Exception("Refunding sends the usual body (amount, method and reason) for the read account: "+refund.Path+" "+refund.Body);
        // Buscar por mesa o sala, sin distinguir mayusculas: la elegida sigue elegida mientras se vea; si la busqueda la deja fuera se
        // deselecciona del todo (la ficha dice que elegir y nada queda activo); sin resultados, se dice y se ofrece borrar la busqueda.
        window.Shell.Status=""; methodList.SelectedIndex=0;
        Choose("s4");
        accountSearch.Text="4"; window.UpdateLayout();
        if(!checkout.AccountItems.Select(r=>r.Table).SequenceEqual(["Mesa 4"])||checkout.SelectedAccount?.ServiceId!="s4"||!checkout.HasContext
            ||accountList.SelectedItem is not Costina.Desktop.ViewModels.AccountRow{Choice.ServiceId:"s4"}||!checkout.PayCommand.CanExecute(null)) throw new Exception("A search that still shows the chosen account keeps it chosen.");
        accountSearch.Text="SALA PRINCIPAL"; window.UpdateLayout();
        if(checkout.AccountItems.Length!=6||checkout.SelectedAccount?.ServiceId!="s4") throw new Exception("The search finds zones too, whatever the case: "+checkout.AccountItems.Length);
        accountSearch.Text="terraza"; window.UpdateLayout();
        if(!checkout.AccountItems.Select(r=>r.Table).SequenceEqual(["Terraza 1"])||checkout.SelectedAccount is not null||checkout.HasContext||accountList.SelectedItem is not null
            ||checkout.PayCommand.CanExecute(null)||checkout.AddCommand.CanExecute(null)||checkout.EmptyHeading!="Elige una cuenta"||!Paints("AccountEmpty")||Paints("AccountDetail"))
            throw new Exception("A search that hides the chosen account deselects it completely: the ficha says what to pick and nothing is actionable.");
        RefuseRaw(window,"checkout-unselected"); Save(Shot(window),"checkout-unselected.png");
        accountSearch.Text="jardín"; window.UpdateLayout();
        if(checkout.AccountItems.Length!=0||checkout.ListEmptyHeading!="Ninguna cuenta coincide"||!Usable("ClearSearchButton")||Paints("EmptyOpenTableButton")) throw new Exception("A search without results says so and offers to clear it.");
        Press(InCheckout<Button>("ClearSearchButton"),"Borrar la búsqueda"); window.UpdateLayout();
        if(checkout.Search.Length!=0||accountSearch.Text.Length!=0||checkout.AccountItems.Length!=7||checkout.SelectedAccount is not null) throw new Exception("Clearing the search lists every account again, with none chosen.");
        // "Ver cuenta" desde el Comedor con una busqueda escrita en Caja: la cuenta pedida no queda escondida (la busqueda se borra) y su
        // tarjeta queda marcada. Es tambien el estado de la captura (Mesa 4, como el Comedor).
        accountSearch.Text="terraza"; window.UpdateLayout();
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        Pick("Mesa 4","s4");
        Press(Named("ViewAccountButton"),"Ver cuenta (Caja con una búsqueda)");
        Wait(()=>!window.Shell.Busy&&checkout.Account?.Data.ServiceId=="s4"&&checkout.HasContext,"'Ver cuenta' opens Mesa 4 in Caja"); window.UpdateLayout();
        if(tabs.SelectedItem!=(TabItem)window.FindName("CheckoutTab")||checkout.Search.Length!=0||checkout.AccountItems.Length!=7||checkout.SelectedAccount?.ServiceId!="s4"
            ||accountList.SelectedItem is not Costina.Desktop.ViewModels.AccountRow{Choice.ServiceId:"s4"}||checkout.DetailTitle!="Mesa 4") throw new Exception("'Ver cuenta' clears a search that would hide the requested account and marks its card.");
        // Encaje a 1.280 x 860: la lista mide 360 px (ListWidth, la de todas las vistas de lista y ficha desde el repaso de coherencia; antes
        // 320), la ficha deja el cobro a la vista, ningun texto se corta y hay una sola barra de desplazamiento por columna (ninguna anidada:
        // los consumos no desplazan por su cuenta).
        window.Shell.Status=""; window.UpdateLayout();
        var payButton=InCheckout<Button>("PayButton");
        if(Math.Abs(InCheckout<FrameworkElement>("AccountsPane").ActualWidth-ListWidth)>0.5||InCheckout<FrameworkElement>("AccountPanel").ActualWidth<460
            ||payButton.TranslatePoint(new Point(0,payButton.ActualHeight),checkoutView).Y>checkoutView.ActualHeight+0.5)
            throw new Exception("Caja fits: the list keeps 360 px and the payment stays in view: "+InCheckout<FrameworkElement>("AccountPanel").ActualWidth);
        if(Clipped(checkoutView) is {Length:>0} clippedCaja) throw new Exception("Caja clips text: "+string.Join(" | ",clippedCaja));
        if(NestedScrolls(checkoutView) is {Length:>0} nestedCaja) throw new Exception("Caja nests scroll viewers: "+string.Join(", ",nestedCaja));
        // ===== C2b (plan 2026-10-14): Catalogo y tarifas con pestanas, lista y ficha =====
        // Catalogo de ejemplo COHERENTE con el comedor de C2 y las cuentas de Caja: los mismos productos y precios (Albarino 26,00 la botella
        // y 4,50 la copa, Menu del dia 18,50...), una carta de vinos por tipo y region (Rioja bajo Tintos Y bajo Blancos, E3.1), botellas
        // dibujadas como en E3.2, presentaciones Botella y Copa, tarifas General y Terraza (la de la sala Terraza: precios propios para
        // vinos, agua y cafe; lo demas se cobra a precio General, como en la cuenta de Terraza 1) y los impuestos de serie. Las salas de la
        // organizacion son las del plano de C2. Abrir la vista lo lee todo del servidor simulado; elegir, buscar y filtrar no envian nada.
        Costina.Client.PresentationDto Pres(string id,string name,int sort,params (string Tariff,long Cents)[] prices)=>new(id,name,sort,true,[..prices.Select(p=>new Costina.Client.PriceDto(p.Tariff,"2026-01-01",p.Cents))]);
        Costina.Client.ProductDto Wine(string id,string name,string category,string? reference,int sort,byte[]? photo,params Costina.Client.PresentationDto[] presentations)
            =>new(id,name,category,"iva-21",reference,sort,true,presentations,null,photo is null?null:Sha(photo));
        Costina.Client.ProductDto Dish(string id,string name,string category,string? station,int sort,params Costina.Client.PresentationDto[] presentations)
            =>new(id,name,category,"iva-10",null,sort,true,presentations,station);
        var bottles=new Dictionary<string,(string Type,byte[] Content)>{
            ["albarino"]=("image/png",greenBottle),
            ["do-ferreiro"]=("image/png",Bottle(Color.FromRgb(0x6B,0x8F,0x3A),Color.FromRgb(0xFF,0xFF,0xFF),new PngBitmapEncoder())),
            ["gravonia"]=("image/jpeg",Bottle(Color.FromRgb(0x9A,0x6B,0x13),Color.FromRgb(0xF4,0xEC,0xD8),new JpegBitmapEncoder{QualityLevel=90})),
            ["guimaro"]=("image/jpeg",Bottle(Color.FromRgb(0x3A,0x15,0x20),Color.FromRgb(0xEB,0xDC,0xB8),new JpegBitmapEncoder{QualityLevel=90})),
            ["muga"]=("image/jpeg",redBottle),
            ["estrella"]=("image/png",Bottle(Color.FromRgb(0x5A,0x3A,0x1A),Color.FromRgb(0xC8,0x10,0x2E),new PngBitmapEncoder()))};
        var albarinoCopa=Pres("copa","Copa",1,("general",450),("terraza",500)); albarinoCopa=albarinoCopa with {Prices=[..albarinoCopa.Prices,new("terraza","2026-11-01",550)]};   // la copa en la Terraza sube el 1 de noviembre
        var wineCatalog=new Costina.Client.CatalogDto(
            [new("iva-10","IVA 10 %",10m,true),new("iva-21","IVA 21 %",21m,true),new("iva-4","IVA 4 %",4m,true),new("iva-0","Exento",0m,false)],
            [new("vinos","Vinos",null,"#8C1D1D",0,true),new("tintos","Tintos","vinos",null,0,true),new("tintos-rioja","Rioja","tintos",null,0,true),new("tintos-ribeira-sacra","Ribeira Sacra","tintos",null,1,true),
             new("blancos","Blancos","vinos",null,1,true),new("blancos-rias-baixas","Rías Baixas","blancos",null,0,true),new("blancos-rioja","Rioja","blancos",null,1,true),
             new("bebidas","Bebidas",null,"#2F6BC2",1,true),new("cervezas","Cervezas","bebidas",null,0,true),new("cafes","Cafés","bebidas",null,1,true),new("vermus","Vermús","bebidas",null,2,false),
             new("cocina","Cocina",null,"#B5673A",2,true),new("entrantes","Entrantes","cocina",null,0,true),new("principales","Principales","cocina",null,1,true),new("postres","Postres","cocina",null,2,true),
             new("menus","Menús",null,"#0E7A5F",3,true)],
            [Wine("albarino","Albariño Pazo de Señoráns","blancos-rias-baixas","VB-0112",0,greenBottle,Pres("botella","Botella",0,("general",2600),("terraza",2800)),albarinoCopa),
             Wine("do-ferreiro","Do Ferreiro 2023","blancos-rias-baixas","VB-0131",1,bottles["do-ferreiro"].Content,Pres("botella","Botella",0,("general",2900)),Pres("copa","Copa",1,("general",550))),
             Wine("gravonia","Viña Gravonia 2015","blancos-rioja","VB-0208",2,bottles["gravonia"].Content,Pres("botella","Botella",0,("general",3800))),
             Wine("vino-casa","Vino de la casa","tintos",null,3,null,Pres("botella","Botella",0,("general",1200),("terraza",1300))),
             Wine("guimaro","Guímaro Ribeira Sacra 2022","tintos-ribeira-sacra","VT-0310",4,bottles["guimaro"].Content,Pres("botella","Botella",0,("general",2200),("terraza",2400)),Pres("copa","Copa",1,("general",400))),
             Wine("muga","Muga Reserva 2019","tintos-rioja","VT-0415",5,redBottle,Pres("botella","Botella",0,("general",3200),("terraza",3500))),
             new("agua","Agua mineral","bebidas","iva-10",null,6,true,[Pres("botella","Botella",0,("general",250),("terraza",300))]),
             new("estrella","Estrella Galicia","cervezas","iva-10",null,7,true,[Pres("botellin","Botellín",0,("general",280))],null,Sha(bottles["estrella"].Content)),
             new("cafe","Café solo","cafes","iva-10",null,8,true,[Pres("unidad","Unidad",0,("general",150),("terraza",180))]),
             new("vermu","Vermú de grifo","vermus","iva-10",null,9,false,[Pres("copa","Copa",0,("general",300))]),
             Dish("pulpo","Pulpo á feira","entrantes","cold",10,Pres("racion","Ración",0,("general",1650)),Pres("media","Media ración",1,("general",950))),
             Dish("croquetas","Croquetas de la casa","entrantes","hot",11,Pres("racion","Ración",0,("general",900))),
             Dish("empanada","Empanada de zamburiñas","entrantes","cold",12,Pres("racion","Ración",0)),
             Dish("lubina","Lubina a la plancha","principales","hot",13,Pres("racion","Ración",0,("general",2200))),
             Dish("filloas","Filloas","postres","cold",14,Pres("racion","Ración",0,("general",600))),
             Dish("tarta","Tarta de queso","postres","cold",15,Pres("racion","Ración",0,("general",650))),
             Dish("menu-degustacion","Degustación Costiña","menus",null,16,Pres("persona","Por persona",0,("general",6500))),
             Dish("menu-dia","Menú del día","menus",null,17,Pres("persona","Por persona",0,("general",1850)))],
            [new("general","General",0,true),new("terraza","Terraza",1,true),new("eventos","Eventos",2,false)],"2026-10-15");
        var wineOrganization=new Costina.Client.OrganizationDto(
            [new("sala","Sala principal",0,true,[..diningConfig.Tables.Where(t=>t.ZoneId=="sala").Select((t,i)=>new Costina.Client.TableDto(t.Id,t.Name,t.Capacity,"sala",i,true))]),
             new("terraza","Terraza",1,true,[..diningConfig.Tables.Where(t=>t.ZoneId=="terraza").Select((t,i)=>new Costina.Client.TableDto(t.Id,t.Name,t.Capacity,"terraza",i,true))],"terraza")],
            [new("pase","Pase","Pass",0,true),new("cold","Cocina fría","Kitchen",1,true),new("hot","Cocina caliente","Kitchen",2,true),new("sala-1","Sala 1","Room",3,true),new("terraza","Terraza","Room",4,true)],
            wineCatalog.Tariffs);
        T InCatalog<T>(string name) where T:FrameworkElement=>(T)catalogPanel.FindName(name);
        bool Shows(string name)=>InCatalog<FrameworkElement>(name).IsVisible;
        bool Ready(string name)=>InCatalog<Button>(name) is {IsVisible:true,IsEnabled:true};
        // El cuerpo enviado, con sus tildes tal cual (el cliente las escapa en JSON) para compararlo con el texto esperado.
        string CatalogPost()=>server.Requests.Last(r=>r.Method=="POST") is var post?post.Path+" "+JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(post.Body),new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}):"";
        var sectionTabs=InCatalog<ListBox>("SectionTabs"); var filterBox=InCatalog<ComboBox>("CategoryFilterBox"); var catalogSearch=InCatalog<TextBox>("ProductSearch");
        void OpenCatalog(string what)
        {
            var before=server.Count("GET","erp/catalog");
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); tabs.SelectedItem=(TabItem)window.FindName("CatalogTab"); window.UpdateLayout();
            Wait(()=>!window.Shell.Busy&&server.Count("GET","erp/catalog")>before,what); window.UpdateLayout();
        }
        void Section(int index,string id){ sectionTabs.SelectedIndex=index; window.UpdateLayout(); if(catalogView.Section!=id||!catalogView.Sections[index].IsCurrent||catalogView.Sections.Count(s=>s.IsCurrent)!=1) throw new Exception("The tab "+id+" is chosen and painted as chosen."); }
        void PickProduct(string id){ productList.SelectedItem=catalogView.Products.Single(p=>p.Product.Id==id); window.UpdateLayout(); }   // como pulsar la fila
        string RowsText()=>string.Join(" | ",catalogView.Products.Select(p=>$"{p.Product.Name}: {p.MainPrice} · {p.Detail}"));
        string PriceRows()=>string.Join(" | ",catalogView.Presentations.Select(p=>p.Presentation.Name+": "+string.Join(" / ",p.Cells.Select(c=>c.Text))));
        void Fits(string what,string bottom)
        {
            window.Shell.Status=""; window.UpdateLayout();   // como en el resto de capturas: sin aviso abierto
            window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
            var button=InCatalog<Button>(bottom);
            if(button.TranslatePoint(new Point(0,button.ActualHeight),catalogPanel).Y>catalogPanel.ActualHeight+0.5) throw new Exception(what+": the ficha keeps its buttons in view.");
            // Repaso de coherencia de C2b: la lista de la pestana abierta mide ListWidth, como en Caja, Oferta y Estaciones.
            var pane=new[]{"ProductsPane","CategoriesPane","TariffsPane","TaxesPane"}.Select(InCatalog<FrameworkElement>).SingleOrDefault(p=>p.IsVisible);   // E3.3: Vinos por copa es una tabla a todo lo ancho
            if(pane is not null&&Math.Abs(pane.ActualWidth-ListWidth)>0.5) throw new Exception(what+": the catalog list keeps 360 px (ListWidth): "+pane.Name+" "+pane.ActualWidth);
            if(Clipped(catalogPanel) is {Length:>0} clipped) throw new Exception(what+" clips text: "+string.Join(" | ",clipped));
            if(NestedScrolls(catalogPanel) is {Length:>0} nested) throw new Exception(what+" nests scroll viewers: "+string.Join(", ",nested));
            RefuseRaw(window,what);
        }
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
        // Catalogo vacio (una instalacion recien hecha: solo la tarifa General y los impuestos de serie): cada lista explica que falta y
        // ofrece crear el primero; la ficha no muestra campos.
        server.Organization=wineOrganization; server.Images.Clear(); foreach(var (id,bottle) in bottles) server.Images[id]=bottle;
        server.Catalog=new([..wineCatalog.Taxes.Select(t=>t with {Active=true})],[],[],[new("general","General",0,true)],"2026-10-15");
        window.Shell.Status="";
        OpenCatalog("the catalog reads an empty catalogue");
        if(catalogView.Section!="products"||!catalogView.Sections.Select(s=>s.Label).SequenceEqual(["Productos · 0","Categorías · 0","Tarifas · 1","Impuestos · 4","Vinos por copa · 0"])||!catalogView.Sections[0].IsCurrent)
            throw new Exception("The catalog opens on Productos, with a tab per kind and its count: "+string.Join(",",catalogView.Sections.Select(s=>s.Label)));
        if(!catalogView.ShowProductListEmpty||catalogView.ProductListHeading!="Aún no hay productos"||!Ready("EmptyNewProductButton")||Shows("EmptyShowAllButton")||Shows("ProductList")||Shows("ShowAllButton")
            ||!Shows("ProductEmpty")||Shows("ProductDetail")||catalogView.ProductEmptyHeading!="Sin productos todavía"||!Texts(catalogPanel).Contains("Crea el primero o importa el catálogo desde un fichero en el equipo servidor."))
            throw new Exception("An empty catalogue says what is missing and offers 'Nuevo producto'; the ficha shows no fields: "+catalogView.ProductListHeading+" / "+catalogView.ProductEmptyHeading);
        if(Shows("ProductSearch")||Shows("CategoryFilterBox")||Shows("ProductCountText")) throw new Exception("Without products there is nothing to search or filter: the list is just the explained empty state.");
        // Repaso de coherencia de C2b: sin productos la cabecera dice que falta y como empezar, como Oferta y Salas (no "0 productos activos").
        if(catalogView.Summary!="Aún no hay productos: crea el primero con «Nuevo producto» o importa el catálogo.") throw new Exception("An empty catalogue's header says what is missing: "+catalogView.Summary);
        Fits("catalog-empty","NewProductButton"); Save(Shot(window),"catalog-empty.png");
        Section(1,"categories");
        if(!Shows("CategoriesEmpty")||!Ready("EmptyNewCategoryButton")||catalogView.CategoryListHeading!="Aún no hay categorías"||!Shows("NewCategoryButton")||Shows("NewProductButton")||catalogView.CategoryEmptyHeading!="Sin categorías todavía")
            throw new Exception("Without categories the tree says so and offers 'Nueva categoría'; the page action follows the tab.");
        Section(0,"products");
        // Un alta: la lista queda sin eleccion y la ficha es la del producto nuevo (sin foto todavia ni presentaciones; pide la primera);
        // "Cancelar" vuelve a la ficha vacia. Nada se envia.
        var postsBeforeNew=server.Count("POST","");
        Press(InCatalog<Button>("EmptyNewProductButton"),"Nuevo producto (catálogo vacío)");
        if(!catalogView.NewProduct||!Shows("ProductDetail")||catalogView.ProductTitle!="Nuevo producto"||catalogView.ProductContext!="Alta de producto"||catalogView.ProductSaveText!="Crear producto"
            ||!Ready("CancelProductButton")||Shows("DeactivateProductButton")||Shows("PresentationsArea")||catalogView.PhotoText!="Guarda el producto para añadirle una imagen."
            ||!Texts(catalogPanel).Contains("Primera presentación (p. ej. Botella, Ración)")||catalogView.ChangeImageCommand.CanExecute(null)||catalogView.ProductPresentationName!="Unidad")
            throw new Exception("'Nuevo producto' opens the new-product ficha: "+catalogView.ProductTitle);
        Press(InCatalog<Button>("CancelProductButton"),"Cancelar el alta");
        if(catalogView.NewProduct||Shows("ProductDetail")||!Shows("ProductEmpty")||server.Count("POST","")!=postsBeforeNew) throw new Exception("Cancelling the new product returns to the empty ficha and sends nothing.");
        // La carta de vinos: pestanas con su recuento, resumen, filas con su precio General y su categoria, miniaturas.
        server.Catalog=wineCatalog;
        // Revision de C2b: la fila de un producto toma el precio General de la primera presentacion activa que lo tenga y solo dice "Sin precio"
        // si ninguna lo tiene (lo mismo que cuenta el resumen y avisa la ficha). Se compone sin servidor y se deshace antes de leer la carta.
        catalogView.Render(wineCatalog with {Products=[..wineCatalog.Products,new("doble","Doble presentación",wineCatalog.Products[0].CategoryId,wineCatalog.Products[0].TaxId,null,99,true,
            [new("copa","Copa",0,true,[]),new("botella","Botella",1,true,[new("general","1970-01-01",2400)])])]}); window.UpdateLayout();
        var doble=catalogView.Products.Single(p=>p.Product.Id=="doble");
        if(doble.MainPrice!="24,00 €"||doble.PriceMissing||!catalogView.Summary.Contains("(17 vendibles")) throw new Exception("A product whose first presentation has no General price shows the next one's price and no warning: "+doble.MainPrice+" / "+catalogView.Summary);
        OpenCatalog("the catalog reads the wine list");
        if(!catalogView.Sections.Select(s=>s.Label).SequenceEqual(["Productos · 18","Categorías · 16","Tarifas · 3","Impuestos · 4","Vinos por copa · 9"])
            ||catalogView.Summary!="17 productos activos (16 vendibles hoy) · precios con IVA incluido"||catalogView.ProductCount!="18 productos · precio General"||Shows("ShowAllButton"))
            throw new Exception("Tabs and summary carry the counts of the read catalogue: "+string.Join(",",catalogView.Sections.Select(s=>s.Label))+" / "+catalogView.Summary);
        if(!Shows("ProductSearch")||!Texts(filterBox).Contains("Todas las categorías")) throw new Exception("With products the list offers the search and the category filter, on every category.");
        if(RowsText()!="Albariño Pazo de Señoráns: 26,00 € · Blancos › Rías Baixas · 2 presentaciones | Do Ferreiro 2023: 29,00 € · Blancos › Rías Baixas · 2 presentaciones"
            +" | Viña Gravonia 2015: 38,00 € · Blancos › Rioja · 1 presentación | Vino de la casa: 12,00 € · Vinos › Tintos · 1 presentación"
            +" | Guímaro Ribeira Sacra 2022: 22,00 € · Tintos › Ribeira Sacra · 2 presentaciones | Muga Reserva 2019: 32,00 € · Tintos › Rioja · 1 presentación"
            +" | Agua mineral: 2,50 € · Bebidas · 1 presentación | Estrella Galicia: 2,80 € · Bebidas › Cervezas · 1 presentación | Café solo: 1,50 € · Bebidas › Cafés · 1 presentación"
            +" | Vermú de grifo: Desactivado · Bebidas › Vermús · 1 presentación | Pulpo á feira: 16,50 € · Cocina › Entrantes · 2 presentaciones"
            +" | Croquetas de la casa: 9,00 € · Cocina › Entrantes · 1 presentación | Empanada de zamburiñas: Sin precio · Cocina › Entrantes · 1 presentación"
            +" | Lubina a la plancha: 22,00 € · Cocina › Principales · 1 presentación | Filloas: 6,00 € · Cocina › Postres · 1 presentación | Tarta de queso: 6,50 € · Cocina › Postres · 1 presentación"
            +" | Degustación Costiña: 65,00 € · Menús · 1 presentación | Menú del día: 18,50 € · Menús · 1 presentación")
            throw new Exception("Each row carries its General price (or why it has none) and, below, its category (Rioja says whether white or red) and presentations: "+RowsText());
        Wait(()=>catalogView.Images.Waiting==0&&catalogView.Products.Take(8).Where(p=>p.Product.ImageSha is not null).All(p=>p.Thumbnail is not null),"the visible rows show their bottle");
        if(catalogView.HasProductContext||!Shows("ProductEmpty")||catalogView.ProductEmptyHeading!="Elige un producto"||Shows("ProductDetail")||productList.SelectedItem is not null)
            throw new Exception("With nothing chosen the ficha says what to pick and shows no fields.");
        // Filtro de categoria: el arbol al abrirlo (con su recuento y su sangria), la ruta cerrado; la categoria y sus subcategorias.
        if(!catalogView.FilterChoices.Select(c=>c.ListText).SequenceEqual(["Todas las categorías · 18","Vinos · 6","Tintos · 3","Rioja · 1","Ribeira Sacra · 1","Blancos · 3","Rías Baixas · 2","Rioja · 1",
            "Bebidas · 4","Cervezas · 1","Cafés · 1","Vermús (desactivada) · 1","Cocina · 6","Entrantes · 3","Principales · 1","Postres · 2","Menús · 2"]))
            throw new Exception("The category filter is the tree with its counts: "+string.Join(",",catalogView.FilterChoices.Select(c=>c.ListText)));
        filterBox.IsDropDownOpen=true; window.UpdateLayout();
        Wait(()=>filterBox.ItemContainerGenerator.ContainerFromIndex(7) is ComboBoxItem{IsVisible:true},"the filter opens its tree");
        if(filterBox.ItemContainerGenerator.ContainerFromIndex(7) is not ComboBoxItem{Padding.Left:43} whiteRioja||!Texts(whiteRioja).Contains("Rioja · 1")
            ||filterBox.ItemContainerGenerator.ContainerFromIndex(1) is not ComboBoxItem{Padding.Left:11} vinos||!Texts(vinos).Contains("Vinos · 6"))
            throw new Exception("Opened, the filter shows the tree indented by level with each count.");
        filterBox.IsDropDownOpen=false; window.UpdateLayout();
        filterBox.SelectedValue="tintos"; window.UpdateLayout();
        if(catalogView.CategoryFilter!="tintos"||!catalogView.Products.Select(p=>p.Product.Id).SequenceEqual(["vino-casa","guimaro","muga"])||catalogView.ProductCount!="3 de 18 productos"
            ||!Ready("ShowAllButton")||!Texts(filterBox).Contains("Vinos › Tintos")) throw new Exception("Filtering by Tintos lists its reds and those of its regions; closed, the filter shows the path: "+catalogView.ProductCount);
        filterBox.SelectedValue="blancos-rioja"; window.UpdateLayout();
        if(!catalogView.Products.Select(p=>p.Product.Id).SequenceEqual(["gravonia"])||!Texts(filterBox).Contains("Vinos › Blancos › Rioja")) throw new Exception("The white Rioja is not the red Rioja.");
        filterBox.SelectedValue="tintos"; catalogSearch.Text="rioja"; window.UpdateLayout();
        if(!catalogView.ShowProductListEmpty||catalogView.ProductListHeading!="Ningún producto coincide"||catalogView.ProductListText!="Ningún producto de Vinos › Tintos coincide con «rioja»."
            ||!Ready("EmptyShowAllButton")||Shows("EmptyNewProductButton")||Shows("ShowAllButton")) throw new Exception("A filter without results says so and offers 'Ver todos los productos': "+catalogView.ProductListText);
        catalogSearch.Text="muga"; window.UpdateLayout(); PickProduct("muga");
        if(catalogView.ProductTitle!="Muga Reserva 2019"||catalogView.ProductContext!="Vinos › Tintos › Rioja") throw new Exception("Choosing a row fills the ficha: "+catalogView.ProductContext);
        Press(InCatalog<Button>("ShowAllButton"),"Ver todos");
        if(catalogView.Search.Length!=0||catalogSearch.Text.Length!=0||!catalogView.AllCategories||catalogView.Products.Length!=18||catalogView.SelectedProduct?.Product.Id!="muga"
            ||!ReferenceEquals(productList.SelectedItem,catalogView.SelectedProduct)||!Texts(filterBox).Contains("Todas las categorías")) throw new Exception("'Ver todos' clears search and category and keeps the chosen product.");
        catalogSearch.Text="albarino"; window.UpdateLayout();
        if(!catalogView.Products.Select(p=>p.Product.Id).SequenceEqual(["albarino"])||catalogView.SelectedProduct is not null||catalogView.HasProductContext)
            throw new Exception("The search ignores accents ('albarino' finds Albariño) and a search that hides the chosen product deselects it.");
        // C2b (Oferta): tambien en un PC en castellano, donde la ene es otra letra: el nombre se encuentra sin su virgulilla ni sus tildes.
        catalogSearch.Text="senorans"; window.UpdateLayout();
        if(!catalogView.Products.Select(p=>p.Product.Id).SequenceEqual(["albarino"])) throw new Exception("The search finds a name without its accents or its eñe ('senorans' finds Señoráns): "+RowsText());
        catalogSearch.Text=""; window.UpdateLayout();
        // Ficha del Albarino: ruta, nombre y estado; foto; datos (la categoria por su ruta, el impuesto y la estacion por su nombre);
        // presentaciones con su precio vigente en la General y en la Terraza; el precio programado, aparte.
        PickProduct("albarino");
        Wait(()=>catalogView.ProductPhoto is not null,"the ficha shows the bottle");
        if(catalogView.ProductContext!="Vinos › Blancos › Rías Baixas"||catalogView.ProductTitle!="Albariño Pazo de Señoráns"||catalogView.ProductStateText!="Activo"||catalogView.ProductWarnText.Length!=0
            ||catalogView.ProductId!="albarino"||catalogView.ProductName!="Albariño Pazo de Señoráns"||catalogView.ProductCategoryId!="blancos-rias-baixas"||catalogView.ProductTaxId!="iva-21"
            ||catalogView.ProductStationId!=""||catalogView.ProductReference!="VB-0112"||catalogView.ProductSort!="0") throw new Exception("The ficha shows the read product: "+catalogView.ProductContext+" / "+catalogView.ProductCategoryId);
        var albarinoTexts=Texts(catalogPanel).ToArray();
        foreach(var text in new[]{"Vinos › Blancos › Rías Baixas","Albariño Pazo de Señoráns","Activo","Código (fijo)","Nombre","Categoría","Impuesto","Estación de cocina","Referencia externa","Orden",
            "IVA 21 %","No pasa por cocina","Presentaciones y precios","Presentación","General","Terraza","Botella","Copa","26,00 €","28,00 €","4,50 €","5,00 €","Fijar precio (IVA incluido)",
            "Guardar cambios","Desactivar producto","Cambiar imagen…","Quitar imagen","Precios vigentes hoy, con IVA incluido.","18 productos · precio General"})
            if(!albarinoTexts.Contains(text)) throw new Exception("The product ficha paints '"+text+"'.");
        if(!Texts(InCatalog<ComboBox>("ProductCategoryBox")).Contains("Vinos › Blancos › Rías Baixas")) throw new Exception("Closed, the product category shows its path.");
        if(!catalogView.PriceColumns.SequenceEqual(["General","Terraza"])||PriceRows()!="Botella: 26,00 € / 28,00 € | Copa: 4,50 € / 5,00 €"||catalogView.SelectedPresentation?.Presentation.Id!="botella"
            ||!Shows("PresentationEditor")||catalogView.PresentationFormTitle!="Presentación: Botella"||!Ready("DeactivateProductButton")||Shows("ReactivateProductButton")||Shows("CancelProductButton"))
            throw new Exception("Presentations show their price in force in each active tariff and the first one is chosen: "+PriceRows());
        if(!ReferenceEquals(productList.SelectedItem,catalogView.SelectedProduct)||productList.ItemContainerGenerator.ContainerFromItem(catalogView.SelectedProduct) is not ListBoxItem{IsSelected:true}) throw new Exception("The chosen row is marked.");
        var presentationList=InCatalog<ListBox>("PresentationList");
        presentationList.SelectedIndex=1; window.UpdateLayout();
        if(catalogView.SelectedPresentation?.Presentation.Id!="copa"||catalogView.PriceText!="4,50"||catalogView.PriceValidFrom!="15/10/2026"||catalogView.ScheduledText!="Programado: Terraza 5,50 € desde el 01/11/2026"||!Shows("ScheduledPriceText"))
            throw new Exception("Choosing the glass shows its General price in the form and its scheduled Terraza price apart: "+catalogView.ScheduledText);
        // Pulsar una presentacion trae a la vista su ficha con "Fijar precio", que a 1.280 x 860 queda debajo de la tabla.
        var productScroll=InCatalog<ScrollViewer>("ProductScroll"); var setPriceButton=InCatalog<Button>("SetPriceButton");
        bool PriceFormInView()=>setPriceButton.TranslatePoint(new Point(0,0),productScroll).Y>=-0.5&&setPriceButton.TranslatePoint(new Point(0,setPriceButton.ActualHeight),productScroll).Y<=productScroll.ViewportHeight+0.5;
        if(!PriceFormInView())
        {
            presentationList.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
            Wait(()=>{ window.UpdateLayout(); return PriceFormInView(); },"clicking a presentation brings its price form into view");
        }
        catalogView.PriceTariffId="terraza"; window.UpdateLayout();
        if(catalogView.PriceText!="5,00") throw new Exception("The tariff of the price form shows its price in force: "+catalogView.PriceText);
        Fits("catalog-price","SaveProductButton"); Save(Shot(window),"catalog-price.png");
        catalogView.PriceText="5,20"; catalogView.PriceValidFrom="20/10/2026"; window.Shell.Status="";   // revision de C2b: se escribe dd/mm/aaaa; viaja AAAA-MM-DD
        var catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SetPriceButton"),"Fijar precio"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the price is sent");
        if(CatalogPost()!="erp/catalog/commands/price-set {\"tariffId\":\"terraza\",\"productId\":\"albarino\",\"presentationId\":\"copa\",\"validFrom\":\"2026-10-20\",\"priceCents\":520}"
            ||window.Shell.Status!="Fijar precio 5,20 € de Albariño Pazo de Señoráns · Copa en Terraza: confirmado por el servidor.") throw new Exception("'Fijar precio' sends the usual body for the chosen product and presentation: "+CatalogPost()+" / "+window.Shell.Status);
        // (el aviso de confirmacion ocupa sitio arriba y la ficha encoge: se mira que no haya vuelto arriba, no que todo siga a la vista)
        if(catalogView.SelectedProduct?.Product.Id!="albarino"||catalogView.SelectedPresentation?.Presentation.Id!="copa"||productScroll.VerticalOffset<1)
            throw new Exception("After the command the catalog is read again: the same product and presentation stay chosen and the ficha does not jump to the top.");
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SaveProductButton"),"Guardar cambios"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the product is saved");
        if(CatalogPost()!="erp/catalog/commands/product-update {\"id\":\"albarino\",\"name\":\"Albariño Pazo de Señoráns\",\"categoryId\":\"blancos-rias-baixas\",\"taxId\":\"iva-21\",\"reference\":\"VB-0112\",\"sort\":0,\"stationId\":\"\"}")
            throw new Exception("'Guardar cambios' sends the usual product-update body: "+CatalogPost());
        // Otros estados de la ficha: un tinto sin precio propio de copa en la Terraza (guion y nota), una empanada sin precio General (no se
        // vende: aviso con palabras), un plato con su estacion por el nombre y un vermu desactivado en una categoria desactivada (el
        // desplegable conserva su categoria, marcada; se ofrece reactivar).
        PickProduct("guimaro");
        if(productScroll.VerticalOffset!=0) throw new Exception("Choosing another product shows its ficha from the top.");
        if(PriceRows()!="Botella: 22,00 € / 24,00 € | Copa: 4,00 € / —"||!catalogView.PriceNote.EndsWith("«—»: sin precio propio en esa tarifa; se cobra el de la General.",StringComparison.Ordinal))
            throw new Exception("A tariff without its own price shows a dash, explained: "+PriceRows()+" / "+catalogView.PriceNote);
        PickProduct("empanada");
        if(catalogView.ProductWarnText!="No se vende: sin precio General"||PriceRows()!="Ración: Sin precio / —"||catalogView.Presentations[0].Cells[0].Tone!="missing"||!Texts(catalogPanel).Contains("No se vende: sin precio General"))
            throw new Exception("A product without a General price says it is not sold, in words: "+catalogView.ProductWarnText);
        PickProduct("pulpo");
        if(catalogView.ProductStationId!="cold"||!Texts(InCatalog<ComboBox>("ProductStationBox")).Contains("Cocina fría")||PriceRows()!="Ración: 16,50 € / — | Media ración: 9,50 € / —") throw new Exception("A dish names its kitchen station: "+catalogView.ProductStationId);
        PickProduct("vermu");
        if(catalogView.ProductStateText!="Desactivado"||catalogView.ProductCategoryId!="vermus"||!Texts(InCatalog<ComboBox>("ProductCategoryBox")).Contains("Bebidas › Vermús (desactivada)")
            ||!Ready("ReactivateProductButton")||Shows("DeactivateProductButton")||catalogView.ProductWarnText.Length!=0) throw new Exception("A deactivated product in a deactivated category keeps its category and offers reactivating: "+catalogView.ProductCategoryId);
        // La ficha final (la captura): el Albarino con su botella elegida; cabe a 1.280 x 860 sin cortar textos y con una barra por columna.
        PickProduct("albarino"); presentationList.SelectedIndex=0; catalogView.PriceTariffId="general";
        Wait(()=>catalogView.ProductPhoto is not null&&catalogView.Images.Waiting==0,"the final ficha shows the bottle");
        if(Math.Abs(InCatalog<FrameworkElement>("ProductsPane").ActualWidth-360)>0.5||InCatalog<FrameworkElement>("ProductPanel").ActualWidth<460) throw new Exception("The product list keeps 360 px.");
        Fits("catalog","SaveProductButton");
        // Categorias: el arbol con sangria, la muestra de su color y sus recuentos; su ficha no filtra la lista de productos y "Ver sus
        // productos" la abre filtrada.
        Section(1,"categories");
        if(catalogView.CategoryCount!="16 categorías · 1 desactivada"||catalogView.HasCategoryContext||!Shows("CategoryEmpty")||catalogView.CategoryEmptyHeading!="Elige una categoría"||!Shows("NewCategoryButton"))
            throw new Exception("The tree heads with its count and, with nothing chosen, the ficha says what to pick: "+catalogView.CategoryCount);
        if(!catalogView.Categories.Take(8).Select(c=>$"{new string('-',c.Depth)}{c.Category.Name}: {c.Detail}").SequenceEqual(["Vinos: 6 productos · 2 subcategorías","-Tintos: 3 productos · 2 subcategorías","--Rioja: 1 producto",
            "--Ribeira Sacra: 1 producto","-Blancos: 3 productos · 2 subcategorías","--Rías Baixas: 2 productos","--Rioja: 1 producto","Bebidas: 4 productos · 3 subcategorías"])
            ||catalogView.Categories.Single(c=>c.Category.Id=="vermus") is not {Detail:"1 producto",Inactive:true}||catalogView.Categories[0].Swatch is not SolidColorBrush{Color:{R:0x8C,G:0x1D,B:0x1D}}||catalogView.Categories[1].Swatch is not null)
            throw new Exception("The tree rows carry their counts, their state in words and their colour: "+string.Join(" | ",catalogView.Categories.Select(c=>c.Category.Name+": "+c.Detail)));
        var categoryList=InCatalog<ListBox>("CategoryList");
        // Repaso de coherencia de C2b: "Desactivada" se pinta a la derecha de la fila (como el "Desactivado" de un producto), no en el detalle.
        var vermus=catalogView.Categories.Single(c=>c.Category.Id=="vermus"); categoryList.ScrollIntoView(vermus); window.UpdateLayout();
        if(!new HashSet<string>(RowPainted(categoryList,o=>ReferenceEquals(o,vermus))).SetEquals(["Desactivada","Vermús","1 producto"]))
            throw new Exception("A deactivated category says so in words on its row: "+string.Join(" | ",RowPainted(categoryList,o=>ReferenceEquals(o,vermus))));
        if(RowPainted(categoryList,o=>o is Costina.Desktop.ViewModels.CategoryRow{Category.Id:"vinos"}).Contains("Desactivada")) throw new Exception("An active category paints no state word.");
        categoryList.SelectedItem=catalogView.Categories.Single(c=>c.Category.Id=="tintos"); window.UpdateLayout();
        if(catalogView.CategoryContext!="Dentro de Vinos"||catalogView.CategoryTitle!="Tintos"||catalogView.CategoryStateText!="Activa"||catalogView.CategoryId!="tintos"||catalogView.CategoryParentId!="vinos"
            ||catalogView.CategoryProductsFigure!="3"||catalogView.CategoryChildrenFigure!="2"||catalogView.CategoryChildrenText!="Subcategorías: Rioja · Ribeira Sacra"
            ||!Texts(InCatalog<ComboBox>("CategoryParentBox")).Contains("Vinos")||!Ready("DeactivateCategoryButton")||!Ready("ShowCategoryProductsButton"))
            throw new Exception("The category ficha shows its parent, data, counts and subcategories: "+catalogView.CategoryContext+" / "+catalogView.CategoryChildrenText);
        if(catalogView.Products.Length!=18||catalogView.CategoryFilter!=""||catalogView.SelectedProduct?.Product.Id!="albarino") throw new Exception("Choosing a category in its tab leaves the product list as it was.");
        Fits("catalog-categories","SaveCategoryButton"); Save(Shot(window),"catalog-categories.png");
        // E3.3: una familia raiz que no es una seccion ofrece "Mover a Cocina" y "Mover a Bodega y bebidas" (category-update con la seccion); una subfamilia, no.
        categoryList.SelectedItem=catalogView.Categories.Single(c=>c.Category.Id=="vinos"); window.UpdateLayout();
        if(!catalogView.CanPlaceCategory||!Ready("PlaceInKitchenButton")||!Ready("PlaceInCellarButton")) throw new Exception("A root family that is not a section can be placed in a section.");
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("PlaceInCellarButton"),"Mover a Bodega y bebidas"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the family is placed");
        if(CatalogPost()!="erp/catalog/commands/category-update {\"id\":\"vinos\",\"parentId\":\"_bodega\"}") throw new Exception("Placing a family sends category-update with the section as parent: "+CatalogPost());
        categoryList.SelectedItem=catalogView.Categories.Single(c=>c.Category.Id=="blancos-rioja"); window.UpdateLayout();
        if(catalogView.CanPlaceCategory||Shows("PlaceInKitchenButton")) throw new Exception("A subfamily is not offered the section buttons.");
        if(catalogView.CategoryContext!="Dentro de Vinos › Blancos"||catalogView.CategoryChildrenText!="Sin subcategorías.") throw new Exception("The white Rioja says where it hangs: "+catalogView.CategoryContext);
        categoryList.SelectedItem=catalogView.Categories.Single(c=>c.Category.Id=="tintos"); window.UpdateLayout();
        Press(InCatalog<Button>("ShowCategoryProductsButton"),"Ver sus productos");
        if(catalogView.Section!="products"||!Shows("ProductsSection")||catalogView.CategoryFilter!="tintos"||catalogView.Products.Length!=3||catalogView.SelectedProduct is not null)
            throw new Exception("'Ver sus productos' opens Productos filtered by the category: "+catalogView.CategoryFilter);
        Press(InCatalog<Button>("ShowAllButton"),"Ver todos"); PickProduct("albarino");
        // Tarifas: la General siempre (sin desactivar), las salas que usan cada una (de las salas leidas) y cuantos precios tiene.
        Section(2,"tariffs");
        if(!catalogView.TariffItems.Select(t=>t.Tariff.Name+": "+t.Detail).SequenceEqual(["General: Sala principal · 20 precios vigentes","Terraza: Terraza · 7 precios vigentes","Eventos: Ninguna sala · 0 precios vigentes"])
            ||catalogView.HasTariffContext||!Shows("TariffEmpty")||catalogView.TariffEmptyHeading!="Elige una tarifa"||!Shows("NewTariffButton"))
            throw new Exception("Each tariff names the rooms that use it and its prices: "+string.Join(" | ",catalogView.TariffItems.Select(t=>t.Tariff.Name+": "+t.Detail)));
        var tariffList=InCatalog<ListBox>("TariffList");
        // Repaso de coherencia de C2b: la tarjeta de una tarifa es la de Oferta y Salas: el estado en su chip, con su palabra, no en el detalle.
        if(!new HashSet<string>(RowPainted(tariffList,o=>o is Costina.Desktop.ViewModels.TariffRow{Tariff.Id:"eventos"})).SetEquals(["Eventos","Desactivada","Ninguna sala · 0 precios vigentes"])
            ||RowPainted(tariffList,o=>o is Costina.Desktop.ViewModels.TariffRow{Tariff.Id:"terraza"}).Contains("Desactivada")) throw new Exception("A deactivated tariff says so in its chip: "+string.Join(" | ",RowPainted(tariffList,o=>o is Costina.Desktop.ViewModels.TariffRow{Tariff.Id:"eventos"})));
        tariffList.SelectedIndex=0; window.UpdateLayout();
        if(catalogView.SelectedTariff?.Id!="general"||!catalogView.IsGeneralTariff||!catalogView.TariffZones.SequenceEqual(["Sala principal"])||catalogView.TariffPricedFigure!="20"||catalogView.TariffUnpricedFigure!="1"
            ||catalogView.TariffUnpricedLabel!="Sin precio: no se venden"||Shows("DeactivateTariffButton")||catalogView.ToggleTariffCommand.CanExecute(null)||!Texts(catalogPanel).Contains("Siempre presente")
            ||catalogView.TariffNote!="La tarifa General existe siempre y no se puede desactivar.") throw new Exception("The General tariff: rooms without their own tariff, its prices, never deactivated.");
        tariffList.SelectedIndex=1; window.UpdateLayout();
        if(catalogView.TariffTitle!="Terraza"||catalogView.TariffId!="terraza"||!catalogView.TariffZones.SequenceEqual(["Terraza"])||catalogView.TariffPricedFigure!="7"||catalogView.TariffUnpricedFigure!="14"
            ||catalogView.TariffUnpricedLabel!="Sin precio propio"||!Ready("DeactivateTariffButton")||!Texts(catalogPanel).Contains("Lo que no tiene precio propio en esta tarifa se cobra al precio de la General."))
            throw new Exception("The Terraza tariff names its room and its own prices: "+catalogView.TariffPricedFigure);
        Fits("catalog-tariffs","SaveTariffButton"); Save(Shot(window),"catalog-tariffs.png");
        tariffList.SelectedIndex=2; window.UpdateLayout();
        if(catalogView.TariffZones.Length!=0||!catalogView.TariffZonesText.StartsWith("Ninguna sala usa esta tarifa.",StringComparison.Ordinal)||!Ready("ReactivateTariffButton")||catalogView.TariffStateText!="Desactivada")
            throw new Exception("A deactivated tariff no room uses says so and offers reactivating.");
        // Alta de tarifa: la accion de la cabecera abre su ficha; al confirmarla el servidor, la nueva queda elegida tras releer.
        Press(InCatalog<Button>("NewTariffButton"),"Nueva tarifa");
        if(!catalogView.NewTariff||tariffList.SelectedItem is not null||catalogView.TariffTitle!="Nueva tarifa"||catalogView.TariffSaveText!="Crear tarifa"||!Ready("CancelTariffButton")||catalogView.HasZoneInfo)
            throw new Exception("'Nueva tarifa' opens an empty ficha with nothing chosen in the list.");
        catalogView.TariffId=""; catalogView.TariffName=""; catalogView.TariffSort="3"; catalogPosts=server.Count("POST",""); window.Shell.Status="";
        Press(InCatalog<Button>("SaveTariffButton"),"Crear tarifa sin nombre");
        if(window.Shell.Status!="El nombre es obligatorio."||server.Count("POST","")!=catalogPosts||!catalogView.NewTariff) throw new Exception("Saving a tariff without a name is refused in the status bar and sends nothing: "+window.Shell.Status);
        catalogView.TariffName="Barra";   // C2b: sin codigo tecleado, se genera del nombre
        Press(InCatalog<Button>("SaveTariffButton"),"Crear tarifa"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the tariff is created");
        if(CatalogPost()!="erp/catalog/commands/tariff-create {\"id\":\"barra\",\"name\":\"Barra\",\"sort\":3}"||catalogView.NewTariff||catalogView.SelectedTariff?.Id!="barra"||catalogView.TariffTitle!="Barra"
            ||!catalogView.Sections[2].Label.EndsWith("· 4",StringComparison.Ordinal)) throw new Exception("A created tariff is chosen once the server confirms it: "+CatalogPost());
        // ===== E3.3: secciones de la lista y Vinos por copa (la bodega en bloque) =====
        Section(0,"products");
        if(!catalogView.SectionFilters.Select(s=>s.Label).SequenceEqual(["Todos · 18","Cocina · 6","Bodega y bebidas · 10","Menús · 2"])||!Shows("SectionChips")) throw new Exception("The product list carries a chip per section with its count: "+string.Join(",",catalogView.SectionFilters.Select(s=>s.Label)));
        catalogView.SectionFilter="bodega"; window.UpdateLayout();
        if(catalogView.Products.Length!=10||catalogView.Products.Any(p=>p.Product.StationId is not null)||catalogView.Products.Any(p=>p.Product.CategoryId=="menus")) throw new Exception("'Bodega y bebidas' lists the products without a kitchen station (and the ones under _bodega): "+catalogView.Products.Length);
        catalogView.SectionFilter="menus"; window.UpdateLayout();
        if(catalogView.Products.Length!=2||catalogView.Products.Any(p=>p.Product.CategoryId!="menus")) throw new Exception("'Menús' lists the menu products, which are edited in Menús y cartas.");
        catalogView.SectionFilter="all"; window.UpdateLayout();
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="albarino"); window.UpdateLayout();   // el chip de Menus lo deselecciono (lo esconde)
        Section(4,"glass");
        string GlassText()=>string.Join(" | ",catalogView.GlassRows.Select(r=>r.ToString()));
        if(catalogView.GlassRows.Length!=9||!GlassText().StartsWith("Albariño Pazo de Señoráns · Vinos · botella 26,00 € · copa 4,50 | Do Ferreiro 2023 · Vinos · botella 29,00 € · copa 5,50 | Viña Gravonia 2015 · Vinos · botella 38,00 € · copa no",StringComparison.Ordinal)
            ||catalogView.GlassCount!="9 vinos y bebidas en Bodega"||catalogView.SaveGlassCommand.CanExecute(null)||InCatalog<Button>("SamePriceButton").IsEnabled)
            throw new Exception("'Vinos por copa' lists the cellar with the bottle price and the glass read from the catalogue: "+GlassText()+" / "+catalogView.GlassCount);
        catalogView.GlassFamily="Vinos"; window.UpdateLayout();
        if(catalogView.GlassRows.Length!=6||catalogView.GlassRows.Any(r=>r.Family!="Vinos")) throw new Exception("The family filter keeps the family and its subfamilies: "+catalogView.GlassRows.Length);
        catalogView.GlassSearch="gravonia"; window.UpdateLayout();
        if(catalogView.GlassRows.Length!=1||catalogView.GlassRows[0].Product.Id!="gravonia") throw new Exception("The glass search finds by name without accents.");
        // Marcar y "Mismo precio para los marcados": escribe el precio y pone por copa sin enviar nada; "Guardar copas" es UNA orden.
        var gravonia=catalogView.GlassRows[0]; gravonia.IsChecked=true; catalogView.SamePriceText="6"; window.UpdateLayout();
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SamePriceButton"),"Mismo precio para los marcados");
        if(!gravonia.ByGlass||gravonia.GlassPrice!="6,00"||!gravonia.Dirty||server.Count("POST","")!=catalogPosts||!catalogView.SaveGlassCommand.CanExecute(null)||catalogView.GlassSummary!="1 marcado · 1 cambio por guardar")
            throw new Exception("'Mismo precio' writes the glass price on the checked rows and sends nothing: "+gravonia+" / "+catalogView.GlassSummary);
        // F01: lo tecleado es un borrador que una relectura no pisa.
        gravonia.GlassPrice="7"; catalogView.Render(server.Catalog); window.UpdateLayout();
        var gravoniaAgain=catalogView.GlassRows.Single(r=>r.Product.Id=="gravonia");
        if(!gravoniaAgain.ByGlass||gravoniaAgain.GlassPrice!="7"||!gravoniaAgain.IsChecked) throw new Exception("A re-read keeps the typed draft and the checks: "+gravoniaAgain);
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the glass sale is sent");
        if(CatalogPost()!="erp/catalog/commands/glass-sale-set {\"items\":[{\"productId\":\"gravonia\",\"priceCents\":700}],\"stop\":[]}"||!window.Shell.Status.StartsWith("Vinos por copa guardados: 1 copa, 0 quitados",StringComparison.Ordinal))
            throw new Exception("'Guardar copas' sends one glass-sale-set with the marked wines and their prices: "+CatalogPost()+" / "+window.Shell.Status);
        catalogView.GlassSearch=""; catalogView.GlassFamily=""; window.UpdateLayout();
        var albarinoRow=catalogView.GlassRows.Single(r=>r.Product.Id=="albarino"); albarinoRow.ByGlass=false; window.UpdateLayout();
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas (quitar)"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the stop is sent");
        if(CatalogPost()!="erp/catalog/commands/glass-sale-set {\"items\":[],\"stop\":[\"albarino\"]}") throw new Exception("Unticking 'Por copa' on a wine sold by the glass sends it in stop: "+CatalogPost());
        // Cambiar IVA y mover los marcados: una orden para todos.
        foreach(var r in catalogView.GlassRows) r.IsChecked=r.Product.Id is "agua" or "cafe";
        catalogView.GlassTaxId="iva-21"; window.UpdateLayout();
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SetTaxButton"),"Cambiar IVA de los marcados"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the tax change is sent");
        if(CatalogPost()!="erp/catalog/commands/products-tax-set {\"productIds\":[\"agua\",\"cafe\"],\"taxId\":\"iva-21\"}") throw new Exception("'Cambiar IVA de los marcados' sends one products-tax-set: "+CatalogPost());
        foreach(var r in catalogView.GlassRows) r.IsChecked=r.Product.Id is "agua" or "cafe";
        catalogView.MoveTargetId="bebidas"; window.UpdateLayout();
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("MoveCheckedButton"),"Mover los marcados"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the move is sent");
        if(CatalogPost()!="erp/catalog/commands/products-move {\"productIds\":[\"agua\",\"cafe\"],\"categoryId\":\"bebidas\"}") throw new Exception("'Mover los marcados' sends one products-move: "+CatalogPost());
        foreach(var r in catalogView.GlassRows) r.IsChecked=false;
        catalogView.GlassRows.Single(r=>r.Product.Id=="muga").IsChecked=true; window.UpdateLayout();
        Fits("catalog-glass","SaveGlassButton"); Save(Shot(window),"catalog-glass.png");
        catalogView.GlassRows.Single(r=>r.Product.Id=="muga").IsChecked=false;
        // ===== E3.3.1: «Guardar copas» en ordenes de 300 cambios EN TOTAL, los borradores de cada parte y la copa activa =====
        // Bodega generada: 301 vinos (los 150 primeros ya por copa a 4,00) y uno con una 'copa' desactivada junto a una 'glass' activa (lo
        // que dejaba 0.40.0 tras quitar la copa del vino del laboratorio). El servidor simulado aplica glass-sale-set y exige el tope del motor.
        var cellarBefore=server.Catalog;
        Costina.Client.ProductDto CellarWine(int n)=>Wine($"bodega-{n:000}",$"Vino de bodega {n:000}","tintos",null,100+n,null,
            n<150?new[]{Pres("botella","Botella",0,("general",2000)),Pres("copa","Copa",1,("general",400))}:new[]{Pres("botella","Botella",0,("general",2000))});
        var twoGlasses=Wine("dos-copas","Vino de dos copas","tintos",null,99,null,Pres("copa","Copa",0,("general",450)) with {Active=false},Pres("glass","Copa",0,("general",800)),Pres("botella","Botella",1,("general",2400)));
        server.Catalog=wineCatalog with {Products=[twoGlasses,..Enumerable.Range(0,301).Select(CellarWine)]};
        catalogView.GlassSearch=""; catalogView.GlassFamily=""; catalogView.Render(server.Catalog); window.UpdateLayout();
        var twoGlassesRow=catalogView.GlassRows.Single(r=>r.Product.Id=="dos-copas");
        if(catalogView.GlassRows.Length!=302||!twoGlassesRow.ByGlassRead||twoGlassesRow.GlassPriceRead!="8,00"||twoGlassesRow.Dirty)
            throw new Exception("An inactive 'copa' never hides an active 'glass': the wine shows as sold by the glass, at the glass price: "+twoGlassesRow);
        JsonElement[] GlassSales(int skip)=>[..server.Requests.Where(r=>r.Method=="POST"&&r.Path=="erp/catalog/commands/glass-sale-set").Skip(skip).Select(r=>JsonSerializer.Deserialize<JsonElement>(r.Body))];
        static int Changes(JsonElement order)=>order.GetProperty("items").GetArrayLength()+order.GetProperty("stop").GetArrayLength();
        static IEnumerable<string> WinesIn(JsonElement order)=>order.GetProperty("items").EnumerateArray().Select(i=>i.GetProperty("productId").GetString()!).Concat(order.GetProperty("stop").EnumerateArray().Select(s=>s.GetString()!));
        Costina.Desktop.ViewModels.GlassRow GlassRowOf(string id)=>catalogView.GlassRows.Single(r=>r.Product.Id==id);
        Costina.Desktop.ViewModels.GlassRow[] CellarRows()=>[..catalogView.GlassRows.Where(r=>r.Product.Id.StartsWith("bodega-",StringComparison.Ordinal))];
        // 151 vinos nuevos por copa y 150 quitados: 301 cambios. El motor admite 300 por orden SUMANDO items y stop; antes salia una sola
        // orden de 151 + 150, rechazada siempre con too_many_items y sin aplicar nada.
        foreach(var r in CellarRows()) { if(r.ByGlassRead) r.ByGlass=false; else { r.ByGlass=true; r.GlassPrice="5,50"; } }
        window.UpdateLayout();
        if(catalogView.GlassSummary!="Ninguno marcado · 301 cambios por guardar") throw new Exception("The mixed edit counts its 301 changes: "+catalogView.GlassSummary);
        var salesBefore=GlassSales(0).Length;
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas (151 nuevas y 150 quitadas)");
        Wait(()=>!window.Shell.Busy&&GlassSales(salesBefore).Length>0,"the mixed glass save is sent");
        var mixed=GlassSales(salesBefore);
        if(mixed.Length!=2||mixed.Any(o=>Changes(o)>300)||mixed.SelectMany(WinesIn).Count()!=301||mixed.SelectMany(WinesIn).Distinct().Count()!=301
            ||!window.Shell.Status.StartsWith("Vinos por copa guardados: 151 copas, 150 quitados",StringComparison.Ordinal)||catalogView.GlassRows.Any(r=>r.Dirty)||catalogView.SaveGlassCommand.CanExecute(null))
            throw new Exception("151 new and 150 removed wines go in two orders of at most 300 changes IN TOTAL, all saved and nothing left to save: "
                +string.Join(" + ",mixed.Select(Changes))+" / "+window.Shell.Status+" / "+catalogView.GlassSummary);
        // Una orden posterior rechazada: la parte confirmada se relee y deja de ser borrador, y el aviso dice cuanto se guardo. Un precio
        // cambiado despues en otro sitio se ve tal cual: antes el borrador olvidado lo tapaba y el siguiente «Guardar copas» lo devolvia.
        foreach(var r in CellarRows()) { r.ByGlass=true; r.GlassPrice="6,00"; }
        window.UpdateLayout();
        salesBefore=GlassSales(0).Length; server.RejectGlassOrder=server.GlassOrders+2;
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas (301 precios; la segunda orden se rechaza)");
        Wait(()=>!window.Shell.Busy&&GlassSales(salesBefore).Length>1,"the second glass order is refused");
        var partial=GlassSales(salesBefore); var confirmed=partial[0..1].SelectMany(WinesIn).ToHashSet(StringComparer.Ordinal);
        var refusedWine=partial.Length==2?WinesIn(partial[1]).Single():"";
        if(partial.Length!=2||Changes(partial[0])!=300||!window.Shell.Status.StartsWith("Guardados 300 de 301 cambios de Vinos por copa",StringComparison.Ordinal)
            ||!window.Shell.Status.Contains(Costina.Client.Labels.Rejection("product_unavailable"),StringComparison.Ordinal)||window.Shell.Pending is not null
            ||catalogView.GlassRows.Where(r=>confirmed.Contains(r.Product.Id)).Any(r=>r.Dirty||r.GlassPriceRead!="6,00")
            ||GlassRowOf(refusedWine) is not {Dirty:true,GlassPrice:"6,00"}||catalogView.GlassSummary!="Ninguno marcado · 1 cambio por guardar")
            throw new Exception("After a later order is refused, the confirmed part is re-read and no longer a draft, and the notice says what was saved: "+window.Shell.Status+" / "+catalogView.GlassSummary);
        window.UpdateLayout();
        if(Clipped(catalogPanel) is {Length:>0} clippedPartial) throw new Exception("catalog-glass-partial clips text: "+string.Join(" | ",clippedPartial));
        RefuseRaw(window,"catalog-glass-partial"); Save(Shot(window),"catalog-glass-partial.png");
        var staleWine=confirmed.Order(StringComparer.Ordinal).First();
        Costina.Client.CatalogDto WithGlassPrice(Costina.Client.CatalogDto source,string id,long cents)=>source with {Products=[..source.Products.Select(p=>p.Id!=id?p:p with {Presentations=[..p.Presentations.Select(s=>s.Id!="copa"?s
            :s with {Prices=[..s.Prices.Where(x=>!(x.TariffId=="general"&&x.ValidFrom==source.Today)),new("general",source.Today,cents)]})]})]};
        server.Catalog=WithGlassPrice(server.Catalog,staleWine,450); catalogView.Render(server.Catalog); window.UpdateLayout();
        if(GlassRowOf(staleWine) is not {Dirty:false,GlassPrice:"4,50"}) throw new Exception("A price changed elsewhere after a partial save shows as read, never hidden by a forgotten draft: "+GlassRowOf(staleWine));
        salesBefore=GlassSales(0).Length; server.RejectGlassOrder=0;
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas (lo que quedaba)");
        Wait(()=>!window.Shell.Busy&&GlassSales(salesBefore).Length>0,"the rest is saved");
        var rest=GlassSales(salesBefore);
        if(rest.Length!=1||!WinesIn(rest[0]).SequenceEqual([refusedWine])||catalogView.GlassRows.Any(r=>r.Dirty)||GlassRowOf(staleWine).GlassPriceRead!="4,50")
            throw new Exception("Saving again sends only what was left, never an old price back: "+string.Join(",",rest.SelectMany(WinesIn)));
        // Una parte que queda SIN CONFIRMAR: la orden pendiente dice que parte era (no el total), el aviso cuenta lo guardado y, tras el
        // reintento identico, nada queda por guardar ni un borrador igual a lo leido tapa un cambio posterior. Revision de E3.3.1: el precio
        // se teclea como "6,5" (Cents lo acepta igual que "6,50"): el borrador se compara con lo leido por IMPORTE, no por texto.
        foreach(var r in CellarRows()) r.GlassPrice="6,5";
        window.UpdateLayout();
        salesBefore=GlassSales(0).Length; server.UncertainGlassOrder=server.GlassOrders+2;
        Press(InCatalog<Button>("SaveGlassButton"),"Guardar copas (la segunda orden queda sin confirmar)");
        Wait(()=>!window.Shell.Busy&&GlassSales(salesBefore).Length>1,"the second glass order is left unconfirmed");
        var uncertainWine=WinesIn(GlassSales(salesBefore)[^1]).Single();
        if(window.Shell.Pending?.Description!="Vinos por copa (parte 2 de 2): 1 copa, 0 quitados"||!window.Shell.Status.StartsWith("Guardados 300 de 301 cambios de Vinos por copa",StringComparison.Ordinal)
            ||GlassRowOf(uncertainWine) is not {Dirty:true,GlassPrice:"6,5"})
            throw new Exception("An unconfirmed part is described as that part and the partial save is told: "+window.Shell.Pending?.Description+" / "+window.Shell.Status);
        window.Shell.RetryCommand.Execute(null);
        Wait(()=>!window.Shell.Busy&&window.Shell.Pending is null,"the identical retry confirms the part");
        if(catalogView.GlassRows.Any(r=>r.Dirty)||GlassRowOf(uncertainWine) is not {GlassPriceRead:"6,50",GlassPrice:"6,50"})
            throw new Exception("After the retry nothing is left to save and the row shows what was read: "+catalogView.GlassSummary+" / "+GlassRowOf(uncertainWine));
        // Volver a teclear el mismo importe de otra forma no es un cambio: ni cuenta ni se guarda (ni deja un borrador que tape lo siguiente).
        GlassRowOf(uncertainWine).GlassPrice="6.50"; window.UpdateLayout();
        if(GlassRowOf(uncertainWine).Dirty||catalogView.SaveGlassCommand.CanExecute(null)||catalogView.GlassSummary!="Ninguno marcado · sin cambios por guardar")
            throw new Exception("The same amount typed another way is not a change: "+catalogView.GlassSummary);
        server.Catalog=WithGlassPrice(server.Catalog,uncertainWine,480); catalogView.Render(server.Catalog); window.UpdateLayout();
        if(GlassRowOf(uncertainWine) is not {Dirty:false,GlassPrice:"4,80"}) throw new Exception("A draft equal to what was read is dropped: a later change shows as read: "+GlassRowOf(uncertainWine));
        server.Catalog=cellarBefore; catalogView.Render(server.Catalog); window.Shell.Status="";
        catalogView.SelectedProduct=catalogView.Products.Single(p=>p.Product.Id=="albarino"); window.UpdateLayout();   // la bodega generada lo deselecciono
        Section(2,"tariffs");
        // Impuestos: tipo y productos que lo llevan.
        Section(3,"taxes");
        if(!catalogView.TaxItems.Select(t=>t.Tax.Name+": "+t.Detail).SequenceEqual(["IVA 10 %: 10 % · 12 productos","IVA 21 %: 21 % · 6 productos","IVA 4 %: 4 % · 0 productos","Exento: 0 % · 0 productos"])
            ||catalogView.HasTaxContext||!Shows("TaxEmpty")||!Shows("NewTaxButton")) throw new Exception("Each tax shows its rate and how many products use it: "+string.Join(" | ",catalogView.TaxItems.Select(t=>t.Detail)));
        var taxList=InCatalog<ListBox>("TaxList");
        if(!new HashSet<string>(RowPainted(taxList,o=>o is Costina.Desktop.ViewModels.TaxRow{Tax.Name:"Exento"})).SetEquals(["Exento","Desactivado","0 % · 0 productos"])) throw new Exception("A deactivated tax says so in its chip: "+string.Join(" | ",RowPainted(taxList,o=>o is Costina.Desktop.ViewModels.TaxRow{Tax.Name:"Exento"})));
        taxList.SelectedIndex=1; window.UpdateLayout();
        if(catalogView.TaxTitle!="IVA 21 %"||catalogView.TaxRate!="21"||catalogView.TaxProductsFigure!="6"
            ||catalogView.TaxProductsText!="Albariño Pazo de Señoráns · Do Ferreiro 2023 · Viña Gravonia 2015 · Vino de la casa · Guímaro Ribeira Sacra 2022 · Muga Reserva 2019."||!Ready("DeactivateTaxButton"))
            throw new Exception("The tax ficha names its products: "+catalogView.TaxProductsText);
        // Revision de C2b: la captura de impuestos se toma con las mismas 3 tarifas que la de tarifas (la tarifa de prueba 'barra' no sale en ella).
        catalogView.Render(wineCatalog); window.UpdateLayout();
        Fits("catalog-taxes","SaveTaxButton"); Save(Shot(window),"catalog-taxes.png");
        catalogView.Render(server.Catalog); window.UpdateLayout();
        catalogPosts=server.Count("POST","");
        Press(InCatalog<Button>("SaveTaxButton"),"Guardar impuesto"); Wait(()=>!window.Shell.Busy&&server.Count("POST","")>catalogPosts,"the tax is saved");
        if(CatalogPost()!="erp/catalog/commands/tax-update {\"id\":\"iva-21\",\"name\":\"IVA 21 %\",\"rate\":21}") throw new Exception("Saving a tax sends the usual body: "+CatalogPost());
        taxList.SelectedIndex=3; window.UpdateLayout();
        if(!Ready("ReactivateTaxButton")||Shows("DeactivateTaxButton")||catalogView.TaxStateText!="Desactivado") throw new Exception("A deactivated tax offers reactivating.");
        // Vuelta a la carta tal cual (sin la tarifa creada) y a Productos con el Albarino: es la captura catalog.png del bucle de capturas.
        server.Catalog=wineCatalog;
        OpenCatalog("the catalog is read again for the capture");
        Section(0,"products");
        if(catalogView.SelectedProduct?.Product.Id!="albarino"||catalogView.PriceColumns.Length!=2) throw new Exception("The capture shows the Albariño with General and Terraza.");
        Wait(()=>catalogView.ProductPhoto is not null&&catalogView.Images.Waiting==0&&catalogView.Products.Take(8).Where(p=>p.Product.ImageSha is not null).All(p=>p.Thumbnail is not null),"the capture shows the bottles");
        Fits("catalog","SaveProductButton");
        // ===== C2b (plan 2026-10-14): Oferta: cartas y menus con lista, pases numerados y platos =====
        // Ofertas de ejemplo COHERENTES con el comedor de C2, sus cuentas y el catalogo de vinos: la Degustacion Costina de la Mesa 1 (seis
        // pases con los platos que sirve; la salsa de vieiras, en el Pase), el Menu del dia de la Mesa 4 (platos a elegir; la sopa,
        // desactivada, ya no la ofrece la configuracion) y la Carta de las Mesas 3 y 7 (platos del catalogo con la estacion de su producto;
        // la tarta, retirada), mas una degustacion de verano desactivada. Sus productos-menu son los del catalogo (categoria Menus, que la
        // caja no vende sueltos) y los vendibles de caja salen del catalogo de vinos como los calcula el motor. Elegir, buscar y filtrar no
        // envian nada; cada orden lleva la oferta, el pase y el plato elegidos (F01).
        Costina.Client.OfferDishDto MenuDish(string offer,string course,string id,string name,string station,int sort,bool active=true,string? product=null)=>new(offer,course,id,name,station,product,sort,active);
        Costina.Client.OfferCourseDto MenuPass(string offer,string id,string name,int sort,params (string Id,string Name,string Station)[] dishes)=>new(id,name,sort,true,[..dishes.Select((d,i)=>MenuDish(offer,id,d.Id,d.Name,d.Station,i))]);
        Costina.Client.OfferItemDto CarteItem(string product,string name,string station,int sort,bool active=true)=>new(product,"racion",name,"Ración",station,sort,active);
        Costina.Client.OfferDto[] sampleOffersC2b=[
            new("DEGUSTACION","Degustación Costiña","tasting","menu-degustacion","any",null,null,126,0,true,[
                MenuPass("DEGUSTACION","aperitivos","Aperitivos",0,("aperitivos","Aperitivos de la casa","cold")),MenuPass("DEGUSTACION","mar","Mar",1,("zamburinas","Zamburiñas a la plancha","hot")),
                MenuPass("DEGUSTACION","arroz","Arroz",2,("arroz","Arroz de bogavante","hot")),MenuPass("DEGUSTACION","pescado","Pescado",3,("rodaballo","Rodaballo al horno","hot"),("salsa","Salsa de vieiras","pase")),
                MenuPass("DEGUSTACION","carne","Carne",4,("lomo","Lomo de vaca gallega","hot")),new("postre","Postre",5,true,[MenuDish("DEGUSTACION","postre","filloas","Filloas","cold",0,product:"filloas")])]),
            new("DIA","Menú del día","set-menu","menu-dia","lunch","2026-09-01","2026-12-31",31,1,true,[
                new("entrantes","Entrantes",0,true,[MenuDish("DIA","entrantes","ensalada","Ensalada de la huerta","cold",0),MenuDish("DIA","entrantes","sopa","Sopa del día","hot",1,active:false)]),
                MenuPass("DIA","principales","Principales",1,("merluza","Merluza a la gallega","hot"),("carrillera","Carrillera al vino","hot")),
                new("postres","Postres",2,true,[MenuDish("DIA","postres","tarta","Tarta de queso","cold",0,product:"tarta"),MenuDish("DIA","postres","flan","Flan de huevo","cold",1)])]),
            new("CARTA","Carta","a-la-carte",null,"any",null,null,127,2,true,[
                new("entrantes","Entrantes",0,true,[],[CarteItem("pulpo","Pulpo á feira","cold",0),CarteItem("croquetas","Croquetas de la casa","hot",1)]),
                new("principales","Principales",1,true,[],[CarteItem("lubina","Lubina a la plancha","hot",0)]),
                new("postres","Postres",2,true,[],[CarteItem("filloas","Filloas","cold",0),CarteItem("tarta","Tarta de queso","cold",1,active:false)])]),
            new("VERANO","Degustación de verano","tasting","menu-verano","dinner","2026-06-15","2026-09-15",56,3,false,[
                MenuPass("VERANO","aperitivos","Aperitivos",0,("gazpacho","Gazpacho de tomate","cold")),MenuPass("VERANO","pescado","Pescado",1,("sardinas","Sardinas asadas","hot")),
                MenuPass("VERANO","postre","Postre",2,("melon","Melón con hierbabuena","cold"))])];
        server.CheckoutCatalog=[..wineCatalog.Products.Where(p=>p.Active&&p.CategoryId!="menus").SelectMany(p=>p.Presentations.Where(s=>s.Active).Select(s=>(Product:p,Presentation:s,
                Price:s.Prices.Where(x=>x.TariffId=="general"&&string.CompareOrdinal(x.ValidFrom,wineCatalog.Today)<=0).OrderByDescending(x=>x.ValidFrom,StringComparer.Ordinal).FirstOrDefault())))
            .Where(t=>t.Price is not null).Select(t=>new Costina.Client.ProductChoice(t.Product.Id,t.Product.Name,t.Presentation.Name,t.Price!.PriceCents,t.Presentation.Id,wineCatalog.Categories.Single(c=>c.Id==t.Product.CategoryId).Name,"general"))];
        var offersPanel=(FrameworkElement)window.FindName("OffersView");
        T InOffers<T>(string name) where T:FrameworkElement=>(T)offersPanel.FindName(name);
        bool OfferShows(string name)=>InOffers<FrameworkElement>(name).IsVisible;
        bool OfferReady(string name)=>InOffers<Button>(name) is {IsVisible:true,IsEnabled:true};
        var offerList=InOffers<ListBox>("OfferList"); var kindChips=InOffers<ListBox>("KindChips"); var offerSearch=InOffers<TextBox>("OfferSearch");
        var dishList=InOffers<ListBox>("DishList"); var itemList=InOffers<ListBox>("ItemList"); var offerScroll=InOffers<ScrollViewer>("OfferScroll");
        void OpenOffers(string what)
        {
            var before=server.Count("GET","erp/offers");
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); tabs.SelectedItem=(TabItem)window.FindName("OffersTab"); window.UpdateLayout();
            Wait(()=>!window.Shell.Busy&&server.Count("GET","erp/offers")>before,what); window.UpdateLayout();
        }
        void PickOffer(string id){ offerList.SelectedItem=offersView.Offers.Single(o=>o.Offer.Id==id); window.UpdateLayout(); }   // como pulsar la tarjeta
        // Pulsa la fila de un pase (de las de antes o despues del desplegado), como una persona.
        void PickCourseRow(string id)
        {
            window.UpdateLayout();
            var row=Descendants<Button>(offersPanel).FirstOrDefault(b=>b.IsVisible&&b.DataContext is Costina.Desktop.ViewModels.OfferCourseRow r&&r.Course.Id==id)??throw new Exception("No row painted for the course "+id);
            Press(row,"pase "+id); window.UpdateLayout();
        }
        void SendOffer(string button,string what)
        {
            var before=server.Count("POST","");
            Press(InOffers<Button>(button),what);
            Wait(()=>!window.Shell.Busy&&server.Count("POST","")>before,what+" is sent"); window.UpdateLayout();
        }
        string OfferRowsText()=>string.Join(" | ",offersView.Offers.Select(o=>o.Offer.Name+": "+o.Detail+" · "+o.Validity));
        string CourseRowsText()=>string.Join(" | ",offersView.Courses.Select(c=>$"{c.Number} {c.Course.Name}: {c.Detail} ({c.Count})"));
        string DishRowsText()=>string.Join(" | ",offersView.Dishes.Select(d=>d.ToString()));
        string ItemRowsText()=>string.Join(" | ",offersView.Items.Select(i=>i.ToString()));
        string Days()=>string.Concat(InOffers<StackPanel>("OfferDaysPanel").Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Select(t=>t.IsChecked==true?t.Content as string:"·"));
        void OffersFit(string what,string bottom)
        {
            window.Shell.Status=""; window.UpdateLayout();
            window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
            var button=InOffers<Button>(bottom);
            if(button.TranslatePoint(new Point(0,button.ActualHeight),offersPanel).Y>offersPanel.ActualHeight+0.5) throw new Exception(what+": the ficha keeps its buttons in view.");
            if(Math.Abs(InOffers<FrameworkElement>("OffersPane").ActualWidth-ListWidth)>0.5||InOffers<FrameworkElement>("OfferPanel").ActualWidth<520) throw new Exception(what+": the offer list keeps 360 px (ListWidth) and the ficha the rest.");
            if(Clipped(offersPanel) is {Length:>0} clipped) throw new Exception(what+" clips text: "+string.Join(" | ",clipped));
            if(NestedScrolls(offersPanel) is {Length:>0} nested) throw new Exception(what+" nests scroll viewers: "+string.Join(", ",nested));
            RefuseRaw(window,what);
        }
        // Sin ofertas (una instalacion recien hecha): la lista explica que falta y ofrece crear la primera; la ficha no muestra campos.
        server.Offers=[]; window.Shell.Status="";
        OpenOffers("the offers view reads an empty list");
        if(offersView.Offers.Length!=0||!offersView.ShowListEmpty||offersView.ListEmptyHeading!="Aún no hay menús ni cartas"||offersView.ListEmptyText!="Crea el primero: un menú degustación, un menú con elección o una carta."
            ||!OfferReady("EmptyNewOfferButton")||OfferShows("EmptyShowAllButton")||OfferShows("OfferList")||OfferShows("OfferSearch")||OfferShows("KindChips")||!OfferReady("NewOfferButton")
            ||!OfferShows("OfferEmpty")||OfferShows("OfferDetail")||offersView.EmptyHeading!="Sin menús ni cartas todavía"||offersView.Summary!="Aún no hay menús ni cartas: crea el primero con «Nuevo menú o carta»."
            ||!Painted(offersPanel).Contains("Aún no hay menús ni cartas"))
            throw new Exception("Without offers the list says what is missing and offers 'Nueva oferta'; the ficha shows no fields: "+offersView.ListEmptyHeading+" / "+offersView.EmptyHeading);
        OffersFit("offers-empty","NewOfferButton"); Save(Shot(window),"offers-empty.png");
        // El alta: la ficha de la oferta nueva (codigo editable, precio o producto de un menu, sin pases todavia); "Cancelar" no envia nada.
        var postsBeforeNewOffer=server.Count("POST","");
        Press(InOffers<Button>("EmptyNewOfferButton"),"Nueva oferta (sin ofertas)");
        if(!offersView.NewOffer||!OfferShows("OfferDetail")||offersView.OfferTitle!="Nuevo menú o carta"||offersView.OfferContext!="Alta de menú o carta"||offersView.OfferSaveText!="Crear"
            ||!OfferReady("CancelOfferButton")||OfferShows("DeactivateOfferButton")||OfferShows("ReactivateOfferButton")||OfferShows("CoursesArea")||!OfferShows("NewOfferCoursesText")||!OfferShows("PriceFields")
            ||offersView.KindHelp!="Menú degustación: todos comen lo mismo; se sirven todos sus platos, pase a pase."||offersView.OfferProductText!="Al crearlo se crea también su producto-menú en el catálogo (categoría Menús)."
            ||InOffers<Expander>("MoreOptions").IsExpanded)   // E4d: lo tecnico (codigo incluido) viene plegado en "Mas opciones"
            throw new Exception("'Nueva oferta' opens the new-offer ficha: "+offersView.OfferTitle);
        Press(InOffers<Button>("CancelOfferButton"),"Cancelar el alta de oferta");
        if(offersView.NewOffer||OfferShows("OfferDetail")||!OfferShows("OfferEmpty")||server.Count("POST","")!=postsBeforeNewOffer) throw new Exception("Cancelling the new offer returns to the empty ficha and sends nothing.");
        // Las ofertas de ejemplo: chips de tipo con su recuento, resumen y una fila por oferta con su tipo, servicio, pases y vigencia.
        server.Offers=sampleOffersC2b;
        OpenOffers("the offers view reads the sample offers");
        if(!offersView.KindFilters.Select(k=>k.Label).SequenceEqual(["Todos · 4","Menú degustación · 2","Menú con elección · 1","Carta · 1"])||offersView.KindFilter!="all"||kindChips.SelectedValue as string!="all"
            ||offersView.Summary!="3 activos · 1 desactivado · los menús se cobran por persona y la carta, plato a plato"||!OfferShows("OfferSearch")||!OfferShows("KindChips"))
            throw new Exception("Type chips and the summary carry their counts: "+string.Join(",",offersView.KindFilters.Select(k=>k.Label))+" / "+offersView.Summary);
        if(OfferRowsText()!="Degustación Costiña: Menú degustación · Comida y cena · 6 pases · Sin fechas | Menú del día: Menú con elección · Comida · 3 pases · Del 01/09/2026 al 31/12/2026"
            +" | Carta: Carta · Comida y cena · 3 pases · Sin fechas | Degustación de verano: Menú degustación · Cena · 3 pases · Del 15/06/2026 al 15/09/2026")
            throw new Exception("Each row carries its kind, service, active courses and validity: "+OfferRowsText());
        // Revision de C2b: la vigencia va en su propia linea (antes se partia tras un "·" colgado).
        if(offersView.Offers[1].Validity!="Del 01/09/2026 al 31/12/2026"||offersView.Offers[1].Detail.Contains("2026")) throw new Exception("The validity of a row has its own line: "+offersView.Offers[1].Detail+" / "+offersView.Offers[1].Validity);
        if(offerList.ItemContainerGenerator.ContainerFromItem(offersView.Offers[3]) is not ListBoxItem summerRow||!Painted(summerRow).Contains("Desactivado")
            ||offersView.Offers.Take(3).Any(o=>offerList.ItemContainerGenerator.ContainerFromItem(o) is ListBoxItem row&&Painted(row).Contains("Desactivado")))
            throw new Exception("Only the deactivated offer is marked 'Desactivado', in words.");
        // Sin eleccion previa se elige la primera oferta, como siempre: datos, nota de su producto-menu (por su codigo: la caja no vende los
        // menus sueltos) y los pases numerados; el primero, desplegado con sus platos.
        if(offersView.SelectedOffer?.Offer.Id!="DEGUSTACION"||offerList.SelectedItem is not Costina.Desktop.ViewModels.OfferRow{Offer.Id:"DEGUSTACION"}
            ||offerList.ItemContainerGenerator.ContainerFromItem(offerList.SelectedItem) is not ListBoxItem{IsSelected:true}
            ||offersView.OfferContext!="Menú degustación · Comida y cena · de martes a domingo"||offersView.OfferTitle!="Degustación Costiña"||offersView.OfferStateText!="Activo"||offersView.OfferStateTone!="ok"
            ||offersView.OfferId!="DEGUSTACION"||offersView.OfferKind!="tasting"||offersView.OfferService!="any"||offersView.Weekdays!=126||offersView.OfferValidFrom.Length!=0||offersView.OfferSort!="0"
            ||offersView.OfferProductText!="Se vende por persona como el producto «Degustación Costiña»: su precio se fija en ERP › Catálogo y tarifas."||Days()!="·MXJVSD"
            ||!OfferReady("DeactivateOfferButton")||OfferShows("ReactivateOfferButton")||OfferShows("CancelOfferButton")||OfferShows("NewMenuFields")||OfferShows("NewOfferCoursesText"))
            throw new Exception("The first offer is chosen and its ficha shows its data, its menu product and its days as pressed toggles: "+offersView.OfferContext+" / "+Days());
        if(CourseRowsText()!="1 Aperitivos: Aperitivos de la casa (1 plato) | 2 Mar: Zamburiñas a la plancha (1 plato) | 3 Arroz: Arroz de bogavante (1 plato)"
            +" | 4 Pescado: Rodaballo al horno, Salsa de vieiras (2 platos) | 5 Carne: Lomo de vaca gallega (1 plato) | 6 Postre: Filloas (1 plato)"||offersView.CoursesTitle!="Pases · 6")
            throw new Exception("The courses are numbered, each with its active dishes by name and how many: "+CourseRowsText());
        if(offersView.SelectedCourse?.Course.Id!="aperitivos"||offersView.CoursesBefore.Length!=0||offersView.CoursesAfter.Length!=5||!OfferShows("SelectedCourseCard")||DishRowsText()!="Aperitivos de la casa · Cocina fría · activo"
            ||OfferShows("CarteArea")||!OfferShows("DishesArea")||OfferShows("DishEditor")||!OfferReady("AddDishButton")||!OfferReady("AddCourseButton")||!OfferReady("SaveCourseButton")||!OfferReady("DeactivateCourseButton"))
            throw new Exception("The first course is unfolded in place with its dishes, their station by name and its own buttons: "+DishRowsText());
        if(!Descendants<Border>(InOffers<FrameworkElement>("SelectedCourseLine")).Any(b=>b.Tag as string=="done"&&Painted(b).Contains("1"))) throw new Exception("The unfolded course carries its number in the accent dot.");
        var tastingTexts=Painted(offersPanel).ToArray();
        // E4d: el camino principal (nombre, tipo, precio y cuando se ofrece); lo tecnico esta plegado en "Mas opciones" y no se pinta.
        foreach(var text in new[]{"Menús y cartas","Nuevo menú o carta","Degustación Costiña","Activo","Menú degustación · Comida y cena · de martes a domingo","Datos","Tipo","Nombre","Menú degustación","Carta",
            "Precio por persona (€, IVA incl.; vacío = sin cambio)","Cuándo se ofrece","Más opciones","Menú degustación: todos comen lo mismo; se sirven todos sus platos, pase a pase.","Pases · 6","Pase","Aperitivos","Aperitivos de la casa",
            "Platos del pase · 1","Plato","Estación","Cocina fría","Datos del pase","Guardar pase","Subir","Bajar","Desactivar pase","Guardar","Desactivar","Pescado","Rodaballo al horno, Salsa de vieiras","2 platos",
            "Todos · 4","Menú con elección · 1","Un menú o una carta se puede abrir cuando tiene al menos un pase activo con un plato activo.","Degustación de verano","Desactivado","Menú del día"})
            if(!tastingTexts.Contains(text)) throw new Exception("The offers view paints '"+text+"'.");
        // "Nueva oferta" deja la lista sin eleccion y la ficha es la del alta; "Cancelar" vuelve a la oferta elegida antes, sin enviar nada.
        var postsBeforeCancel=server.Count("POST","");
        Press(InOffers<Button>("NewOfferButton"),"Nueva oferta");
        if(!offersView.NewOffer||offerList.SelectedItem is not null||offersView.SelectedOffer is not null||OfferShows("CoursesArea")||!OfferShows("NewOfferCoursesText")||offersView.OfferId.Length!=0
            ||offersView.OfferName.Length!=0||offersView.OfferSort!="4"||Days()!="LMXJVSD"||!OfferReady("SaveOfferButton")||!OfferReady("CancelOfferButton"))
            throw new Exception("'Nueva oferta' leaves the list with nothing chosen and opens an empty new-offer ficha.");
        OffersFit("offers-new","SaveOfferButton"); Save(Shot(window),"offers-new.png");
        Press(InOffers<Button>("CancelOfferButton"),"Cancelar el alta de oferta");
        if(offersView.NewOffer||offersView.SelectedOffer?.Offer.Id!="DEGUSTACION"||offerList.SelectedItem is not Costina.Desktop.ViewModels.OfferRow{Offer.Id:"DEGUSTACION"}||offersView.SelectedCourse?.Course.Id!="aperitivos"
            ||offersView.OfferId!="DEGUSTACION"||server.Count("POST","")!=postsBeforeCancel) throw new Exception("Cancelling the new offer returns to the offer chosen before and sends nothing.");
        // Pulsar la fila de un pase lo despliega en su sitio (los de antes arriba, los de despues abajo), sin leer ni enviar nada.
        var requestsBeforePick=server.Requests.Length;
        PickCourseRow("pescado");
        if(offersView.SelectedCourse?.Course.Id!="pescado"||!offersView.CoursesBefore.Select(c=>c.Course.Id).SequenceEqual(["aperitivos","mar","arroz"])||!offersView.CoursesAfter.Select(c=>c.Course.Id).SequenceEqual(["carne","postre"])
            ||DishRowsText()!="Rodaballo al horno · Cocina caliente · activo | Salsa de vieiras · Pase · activo"||offersView.DishesTitle!="Platos del pase · 2"||server.Requests.Length!=requestsBeforePick
            ||!Painted(InOffers<FrameworkElement>("SelectedCourseCard")).Contains("Salsa de vieiras")||!Painted(InOffers<FrameworkElement>("SelectedCourseCard")).Contains("Pase"))
            throw new Exception("Clicking a course row unfolds it in place with its dishes and stations by name, and reads or sends nothing: "+DishRowsText());
        // Elegir un plato abre su ficha debajo de la tabla (y la trae a la vista); "Guardar plato" envia el cuerpo de siempre del plato elegido.
        dishList.SelectedItem=offersView.Dishes[1]; window.UpdateLayout();
        if(!OfferShows("DishEditor")||offersView.DishFormTitle!="Plato: Salsa de vieiras"||offersView.DishId!="salsa"||offersView.DishName!="Salsa de vieiras"||offersView.DishStationId!="pase"||offersView.DishSort!="1"
            ||!Painted(InOffers<ComboBox>("DishStationBox")).Contains("Pase")||!OfferReady("DeactivateDishButton")||OfferShows("ReactivateDishButton")||OfferShows("CancelDishButton")||offersView.DishSaveText!="Guardar plato"
            ||dishList.ItemContainerGenerator.ContainerFromItem(offersView.SelectedDish) is not ListBoxItem{IsSelected:true})
            throw new Exception("Choosing a dish opens its ficha with its station by name: "+offersView.DishStationId);
        var saveDishButton=InOffers<Button>("SaveDishButton");
        bool DishEditorInView()=>saveDishButton.TranslatePoint(new Point(0,0),offerScroll).Y>=-0.5&&saveDishButton.TranslatePoint(new Point(0,saveDishButton.ActualHeight),offerScroll).Y<=offerScroll.ViewportHeight+0.5;
        if(!DishEditorInView())
        {
            dishList.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
            Wait(()=>{ window.UpdateLayout(); return DishEditorInView(); },"clicking a dish brings its ficha into view");
        }
        window.Shell.Status="";
        SendOffer("SaveDishButton","Guardar plato");
        if(CatalogPost()!="erp/offers/commands/dish-update {\"offerId\":\"DEGUSTACION\",\"courseId\":\"pescado\",\"id\":\"salsa\",\"name\":\"Salsa de vieiras\",\"stationId\":\"pase\",\"productId\":\"\",\"sort\":1}"
            ||window.Shell.Status!="Guardar plato Salsa de vieiras: confirmado por el servidor.") throw new Exception("'Guardar plato' sends the usual body of the chosen offer, course and dish: "+CatalogPost()+" / "+window.Shell.Status);
        if(offersView.SelectedOffer?.Offer.Id!="DEGUSTACION"||offersView.SelectedCourse?.Course.Id!="pescado"||offersView.SelectedDish?.Dish.Id!="salsa"||!OfferShows("DishEditor")||offersView.DishStationId!="pase")
            throw new Exception("After the command the offers are read again and the same offer, course and dish stay chosen.");
        // Un plato con su producto del catalogo lo nombra; otro pase empieza sin plato elegido.
        PickCourseRow("postre");
        if(offersView.SelectedDish is not null||OfferShows("DishEditor")) throw new Exception("Another course starts with no dish chosen.");
        dishList.SelectedItem=offersView.Dishes[0]; window.UpdateLayout();
        if(offersView.DishProductId!="filloas"||offersView.DishProductText!="Producto del catálogo: Filloas"||!Painted(offersPanel).Contains("Producto del catálogo: Filloas"))
            throw new Exception("A dish names its catalogue product: "+offersView.DishProductText);
        // F01: elegir otra oferta muestra SU primer pase, sin plato elegido, desde arriba (nada de la oferta anterior queda elegido).
        PickOffer("DIA");
        if(offersView.SelectedDish is not null||offersView.SelectedItem is not null||offersView.SelectedCourse?.Course.Id!="entrantes"||offersView.Dishes.Any(d=>d.Dish.OfferId!="DIA")||offerScroll.VerticalOffset!=0
            ||offersView.ToggleDishCommand.CanExecute(null)) throw new Exception("Choosing another offer shows its own first course, with no dish chosen, from the top.");
        if(offersView.OfferContext!="Menú con elección · Comida · de lunes a viernes"||offersView.OfferValidFrom!="01/09/2026"||offersView.OfferValidTo!="31/12/2026"||Days()!="LMXJV··"
            ||offersView.KindHelp!="Menú con elección: cada persona elige un plato en cada pase, entre los que pongas."||!offersView.OfferProductText.Contains("«Menú del día»",StringComparison.Ordinal))
            throw new Exception("The set menu ficha: lunch from Monday to Friday, its dates and its menu product: "+offersView.OfferContext+" / "+Days());
        if(CourseRowsText()!="1 Entrantes: Ensalada de la huerta (1 plato · 1 desactivado) | 2 Principales: A elegir: Merluza a la gallega o Carrillera al vino (2 platos) | 3 Postres: A elegir: Tarta de queso o Flan de huevo (2 platos)")
            throw new Exception("A set menu course says which dishes are to choose from: "+CourseRowsText());
        // Un plato desactivado lo dice con su palabra y ofrece reactivarlo.
        if(DishRowsText()!="Ensalada de la huerta · Cocina fría · activo | Sopa del día · Cocina caliente · desactivado") throw new Exception("Deactivated dishes stay listed: "+DishRowsText());
        dishList.SelectedItem=offersView.Dishes[1]; window.UpdateLayout();
        if(!OfferReady("ReactivateDishButton")||OfferShows("DeactivateDishButton")||!Painted(dishList).Contains("Desactivado")) throw new Exception("A deactivated dish says so and offers reactivating.");
        SendOffer("ReactivateDishButton","Reactivar plato");
        if(CatalogPost()!="erp/offers/commands/dish-reactivate {\"offerId\":\"DIA\",\"courseId\":\"entrantes\",\"id\":\"sopa\"}") throw new Exception("Reactivating sends the usual body: "+CatalogPost());
        // "Plato": un alta sin plato elegido en la tabla, en la primera estacion de cocina; "Cancelar" vuelve al plato de antes sin enviar nada.
        PickCourseRow("principales");
        dishList.SelectedItem=offersView.Dishes[0]; window.UpdateLayout();
        var postsBeforeNewDish=server.Count("POST","");
        Press(InOffers<Button>("AddDishButton"),"Plato");
        if(!offersView.NewDish||offersView.SelectedDish is not null||dishList.SelectedItem is not null||offersView.DishFormTitle!="Nuevo plato"||offersView.DishSaveText!="Crear plato"||offersView.DishId.Length!=0
            ||offersView.DishStationId!="cold"||!Painted(InOffers<ComboBox>("DishStationBox")).Contains("Cocina fría")||!OfferReady("CancelDishButton")||OfferShows("DeactivateDishButton")||offersView.DishSort!="2"
            ||!Descendants<TextBox>(offersPanel).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Código del plato").IsEnabled)
            throw new Exception("'Plato' opens a new dish in the first kitchen station, with nothing chosen in the table: "+offersView.DishStationId);
        offersView.DishProductId="pulpo";
        if(offersView.DishProductText!="Producto del catálogo: Pulpo á feira") throw new Exception("A typed product code is named when it is a sellable: "+offersView.DishProductText);
        Press(InOffers<Button>("CancelDishButton"),"Cancelar el plato nuevo");
        if(offersView.NewDish||offersView.SelectedDish?.Dish.Id!="merluza"||offersView.DishFormTitle!="Plato: Merluza a la gallega"||offersView.DishProductId.Length!=0||server.Count("POST","")!=postsBeforeNewDish)
            throw new Exception("Cancelling the new dish returns to the dish chosen before and sends nothing.");
        // "Pase": el alta va al final de la lista, ningun pase queda desplegado; al confirmarlo el servidor, el nuevo queda elegido y vacio.
        Press(InOffers<Button>("AddCourseButton"),"Pase");
        if(!offersView.NewCourse||offersView.SelectedCourse is not null||OfferShows("SelectedCourseCard")||!OfferShows("NewCoursePanel")||offersView.CoursesBefore.Length!=3||offersView.CoursesAfter.Length!=0
            ||offersView.CourseSort!="3"||!OfferReady("CancelCourseButton")||!OfferReady("CreateCourseButton")||offersView.StartDishCommand.CanExecute(null))
            throw new Exception("'Pase' opens the new course at the end of the list with no course unfolded.");
        Press(InOffers<Button>("CancelCourseButton"),"Cancelar el pase nuevo");
        if(offersView.NewCourse||offersView.SelectedCourse?.Course.Id!="principales"||!OfferShows("SelectedCourseCard")) throw new Exception("Cancelling the new course unfolds the course chosen before.");
        Press(InOffers<Button>("AddCourseButton"),"Pase");
        offersView.CourseId="cafe"; offersView.CourseName="Café y dulces"; window.UpdateLayout();
        SendOffer("CreateCourseButton","Crear pase");
        if(CatalogPost()!="erp/offers/commands/course-create {\"offerId\":\"DIA\",\"id\":\"cafe\",\"name\":\"Café y dulces\"}"||offersView.NewCourse||offersView.SelectedCourse?.Course.Id!="cafe"
            ||!OfferShows("SelectedCourseCard")||!offersView.DishesEmpty||!OfferShows("NoDishesText")||offersView.DishesEmptyText!="Este pase aún no tiene platos: añade el primero con «Plato»."
            ||offersView.Courses.Last().Detail!="Sin platos activos"||offersView.CoursesTitle!="Pases · 4"||OfferShows("NewCoursePanel"))
            throw new Exception("A created course is chosen once the server confirms it, unfolded and explained while empty: "+CatalogPost());
        // Un chip de tipo que esconde la oferta elegida la deselecciona del todo: la ficha dice que elegir y nada queda accionable.
        kindChips.SelectedValue="a-la-carte"; window.UpdateLayout();
        if(offersView.KindFilter!="a-la-carte"||!offersView.Offers.Select(o=>o.Offer.Id).SequenceEqual(["CARTA"])||offersView.SelectedOffer is not null||offersView.HasOfferContext||offerList.SelectedItem is not null
            ||!OfferShows("OfferEmpty")||OfferShows("OfferDetail")||offersView.EmptyHeading!="Elige un menú o una carta"||offersView.ToggleOfferCommand.CanExecute(null)||offersView.SaveOfferCommand.CanExecute(null)
            ||offersView.StartCourseCommand.CanExecute(null)||offersView.SaveDishCommand.CanExecute(null)||offersView.Courses.Length!=0||offersView.Dishes.Length!=0
            ||kindChips.ItemContainerGenerator.ContainerFromIndex(3) is not ListBoxItem{IsSelected:true})
            throw new Exception("A type chip that hides the chosen offer deselects it completely: the ficha says what to pick and nothing is actionable.");
        OffersFit("offers-unselected","NewOfferButton"); Save(Shot(window),"offers-unselected.png");
        // La carta: sin producto-menu; cada pase lleva platos del catalogo (producto y presentacion, con la estacion de su producto) y la fila
        // de anadir del catalogo (sin los menus, que no se venden sueltos).
        PickOffer("CARTA");
        if(offersView.OfferContext!="Carta · Comida y cena · todos los días"||!offersView.OfferProductText.StartsWith("Carta: sin producto-menú",StringComparison.Ordinal)||OfferShows("NewMenuFields")
            ||!OfferShows("CarteArea")||OfferShows("DishesArea")||OfferShows("AddDishButton")||offersView.SelectedCourse?.Course.Id!="entrantes"||offersView.DishesTitle!="Platos de la carta · 2"
            ||CourseRowsText()!="1 Entrantes: Pulpo á feira, Croquetas de la casa (2 platos) | 2 Principales: Lubina a la plancha (1 plato) | 3 Postres: Filloas (1 plato · 1 retirado)"
            ||ItemRowsText()!="Pulpo á feira · Ración · Cocina fría · activo | Croquetas de la casa · Ración · Cocina caliente · activo"||!Painted(itemList).Contains("Pulpo á feira · Ración"))
            throw new Exception("A carta lists its courses with catalogue dishes, each with its presentation and station: "+CourseRowsText()+" / "+ItemRowsText());
        var itemChoiceBox=InOffers<ComboBox>("ItemChoiceBox");
        if(offersView.SelectedItemChoice is null||!Equals(itemChoiceBox.SelectedItem,offersView.SelectedItemChoice)||!Painted(itemChoiceBox).Contains(offersView.SelectedItemChoice.ToString())
            ||offersView.ItemChoices.Any(c=>c.CategoryName=="Menús")||!offersView.AddItemCommand.CanExecute(null)) throw new Exception("The carta adder shows the chosen sellable: "+offersView.SelectedItemChoice);
        itemChoiceBox.SelectedItem=offersView.ItemChoices.Single(c=>c.Id=="pulpo"&&c.PresentationId=="media"); window.UpdateLayout();
        SendOffer("AddItemButton","Añadir a la carta");
        if(CatalogPost()!="erp/offers/commands/item-create {\"offerId\":\"CARTA\",\"courseId\":\"entrantes\",\"productId\":\"pulpo\",\"presentationId\":\"media\",\"sort\":2}"
            ||offersView.SelectedItem?.Key!="pulpo/media"||!OfferReady("RemoveItemButton")||OfferShows("RestoreItemButton")||offersView.SelectedCourse?.Course.Id!="entrantes"
            ||!ItemRowsText().EndsWith("Pulpo á feira · Media ración · Cocina fría · activo",StringComparison.Ordinal)||offersView.SelectedItemChoice?.PresentationId!="media")
            throw new Exception("'Añadir' sends the chosen sellable to the chosen course and the added dish is chosen after the re-read: "+CatalogPost());
        PickCourseRow("postres");
        if(ItemRowsText()!="Filloas · Ración · Cocina fría · activo | Tarta de queso · Ración · Cocina fría · desactivado"||offersView.SelectedItem is not null||OfferShows("RemoveItemButton")||OfferShows("RestoreItemButton"))
            throw new Exception("Another course lists its own carta dishes with none chosen: "+ItemRowsText());
        itemList.SelectedItem=offersView.Items[1]; window.UpdateLayout();
        if(!OfferReady("RestoreItemButton")||OfferShows("RemoveItemButton")||!Painted(itemList).Contains("Retirado")) throw new Exception("A withdrawn carta dish says so and offers putting it back.");
        OffersFit("offers-carta","SaveOfferButton"); Save(Shot(window),"offers-carta.png");
        SendOffer("RestoreItemButton","Volver a la carta");
        if(CatalogPost()!="erp/offers/commands/item-reactivate {\"offerId\":\"CARTA\",\"courseId\":\"postres\",\"productId\":\"tarta\",\"presentationId\":\"racion\"}"||offersView.SelectedItem?.Key!="tarta/racion")
            throw new Exception("'Volver a la carta' sends the usual body of the chosen carta dish: "+CatalogPost());
        // Buscar: sin distinguir tildes; los chips cuentan dentro de la busqueda; la elegida sigue elegida mientras se vea y, si no, se deselecciona.
        kindChips.SelectedValue="all"; window.UpdateLayout();
        if(offersView.Offers.Length!=4||offersView.SelectedOffer?.Offer.Id!="CARTA"||offersView.SelectedCourse?.Course.Id!="postres"||offerList.SelectedItem is not Costina.Desktop.ViewModels.OfferRow{Offer.Id:"CARTA"})
            throw new Exception("A chip that still shows the chosen offer keeps it chosen, with its course.");
        offerSearch.Text="costina"; window.UpdateLayout();
        if(!offersView.Offers.Select(o=>o.Offer.Id).SequenceEqual(["DEGUSTACION"])||offersView.SelectedOffer is not null||offersView.HasOfferContext
            ||!offersView.KindFilters.Select(k=>k.Label).SequenceEqual(["Todos · 1","Menú degustación · 1","Menú con elección · 0","Carta · 0"]))
            throw new Exception("The search ignores accents ('costina' finds Costiña), counts within it and deselects what it hides: "+string.Join(",",offersView.KindFilters.Select(k=>k.Label)));
        offerSearch.Text="verano"; window.UpdateLayout(); PickOffer("VERANO");
        if(offersView.OfferStateText!="Desactivado"||offersView.OfferStateTone!="other"||!OfferReady("ReactivateOfferButton")||OfferShows("DeactivateOfferButton")||offersView.OfferContext!="Menú degustación · Cena · de jueves a sábado"
            ||!offersView.OfferProductText.Contains("«menu-verano»",StringComparison.Ordinal)) throw new Exception("A deactivated offer says so and offers reactivating: "+offersView.OfferContext);
        offerSearch.Text="zzz"; window.UpdateLayout();
        if(!offersView.ShowListEmpty||offersView.ListEmptyHeading!="Ninguno coincide"||offersView.ListEmptyText!="Ningún menú ni carta coincide con «zzz»."||!OfferReady("EmptyShowAllButton")||OfferShows("EmptyNewOfferButton")
            ||offersView.SelectedOffer is not null||!OfferShows("OfferSearch")||!OfferShows("KindChips")) throw new Exception("A search without results says so and offers to see them all: "+offersView.ListEmptyText);
        kindChips.SelectedValue="set-menu"; window.UpdateLayout();
        if(offersView.ListEmptyText!="Ningún menú ni carta de tipo Menú con elección coincide con «zzz».") throw new Exception("The empty list names the chosen type: "+offersView.ListEmptyText);
        Press(InOffers<Button>("EmptyShowAllButton"),"Ver todas las ofertas");
        if(offersView.Search.Length!=0||offerSearch.Text.Length!=0||offersView.KindFilter!="all"||offersView.Offers.Length!=4||offersView.SelectedOffer is not null||!OfferShows("OfferEmpty")
            ||kindChips.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem{IsSelected:true}) throw new Exception("'Ver todas las ofertas' clears the search and the type.");
        // Guardar y desactivar la oferta elegida: los cuerpos de siempre.
        PickOffer("DIA");
        SendOffer("SaveOfferButton","Guardar oferta");
        if(CatalogPost()!="erp/offers/commands/offer-update {\"id\":\"DIA\",\"name\":\"Menú del día\",\"kind\":\"set-menu\",\"service\":\"lunch\",\"validFrom\":\"2026-09-01\",\"validTo\":\"2026-12-31\",\"weekdays\":31,\"sort\":1}"
            ||window.Shell.Status!="Guardar Menú del día: confirmado por el servidor."||offersView.SelectedOffer?.Offer.Id!="DIA") throw new Exception("'Guardar oferta' sends the usual body of the chosen offer: "+CatalogPost());
        SendOffer("DeactivateOfferButton","Desactivar oferta");
        if(CatalogPost()!="erp/offers/commands/offer-deactivate {\"id\":\"DIA\"}") throw new Exception("'Desactivar oferta' sends the usual body: "+CatalogPost());
        // Alta con una busqueda escrita: la lista queda sin eleccion; los dias son botones que escriben en la oferta; al confirmarla el
        // servidor, la busqueda se borra y la nueva queda elegida, sin pases todavia (lo dice).
        offerSearch.Text="carta"; window.UpdateLayout();
        Press(InOffers<Button>("NewOfferButton"),"Nueva oferta");
        if(!offersView.NewOffer||offerList.SelectedItem is not null||offersView.SelectedOffer is not null||OfferShows("CoursesArea")) throw new Exception("'Nueva oferta' leaves the list with nothing chosen.");
        InOffers<ListBox>("OfferKindBox").SelectedValue="a-la-carte"; window.UpdateLayout();
        if(OfferShows("PriceFields")||offersView.KindHelp!="Carta: platos sueltos que se cobran al pedirlos.") throw new Exception("A new carta asks for no menu product and explains the kind.");
        InOffers<StackPanel>("OfferDaysPanel").Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Last().IsChecked=false; window.UpdateLayout();
        if(offersView.Weekdays!=63||Days()!="LMXJVS·") throw new Exception("A day toggle writes into the offer's weekdays: "+offersView.Weekdays);
        offersView.OfferId="TERRAZA"; offersView.OfferName="Carta de terraza"; window.UpdateLayout();
        SendOffer("SaveOfferButton","Crear oferta");
        if(CatalogPost()!="erp/offers/commands/offer-create {\"id\":\"TERRAZA\",\"name\":\"Carta de terraza\",\"kind\":\"a-la-carte\",\"service\":\"any\",\"validFrom\":\"\",\"validTo\":\"\",\"weekdays\":63,\"sort\":4,\"priceCents\":null,\"productId\":\"\"}"
            ||offersView.NewOffer||offersView.Search.Length!=0||offerSearch.Text.Length!=0||offersView.SelectedOffer?.Offer.Id!="TERRAZA"||offersView.Offers.Length!=5||!offersView.CoursesEmpty||!OfferShows("NoCoursesText")
            ||offersView.OfferContext!="Carta · Comida y cena · de lunes a sábado"||offerList.SelectedItem is not Costina.Desktop.ViewModels.OfferRow{Offer.Id:"TERRAZA"})
            throw new Exception("A created offer is chosen once the server confirms it, visible (the search is cleared) and explained while it has no courses: "+CatalogPost());
        // C2b: guardar con datos vacios o mal escritos NO cierra el programa (el promotor lo vio al dar de alta sin codigo): se cuenta en la
        // barra de estado y no se envia nada. Un codigo vacio se genera del nombre (sin tildes, con guiones, unico); el de una oferta, a 27.
        Press(InOffers<Button>("NewOfferButton"),"Nueva oferta");
        var offerPosts=server.Count("POST",""); window.Shell.Status="";
        offersView.OfferId=""; offersView.OfferName=""; window.UpdateLayout();
        Press(InOffers<Button>("SaveOfferButton"),"Crear oferta sin nombre");
        if(window.Shell.Status!="El nombre es obligatorio."||server.Count("POST","")!=offerPosts||!offersView.NewOffer||window.Shell.Busy) throw new Exception("Saving without a name is refused in the status bar and sends nothing: "+window.Shell.Status);
        offersView.OfferName="Menú de la noche de verano 2026"; offersView.OfferSort="x"; window.UpdateLayout();
        Press(InOffers<Button>("SaveOfferButton"),"Crear oferta con orden mal escrito");
        if(window.Shell.Status!="Orden: introduce un número entre 0 y 9999."||server.Count("POST","")!=offerPosts||!offersView.NewOffer) throw new Exception("A misspelt order is refused in the status bar and sends nothing: "+window.Shell.Status);
        offersView.OfferSort="5"; window.UpdateLayout();
        Press(InOffers<Button>("SaveOfferButton"),"Crear menú sin precio");
        if(window.Shell.Status!="Un menú necesita su precio por persona (por ejemplo 65,00)."||server.Count("POST","")!=offerPosts) throw new Exception("E4d: a new menu without a price per person is refused in the status bar: "+window.Shell.Status);
        offersView.OfferPrice="65"; window.UpdateLayout();
        SendOffer("SaveOfferButton","Crear oferta sin código");
        if(!CatalogPost().StartsWith("erp/offers/commands/offer-create {\"id\":\"menu-de-la-noche-de-verano\",\"name\":\"Menú de la noche de verano 2026\",",StringComparison.Ordinal)||offersView.SelectedOffer?.Offer.Id!="menu-de-la-noche-de-verano")
            throw new Exception("An empty offer code is generated from the name and cut to 27 characters: "+CatalogPost());
        if(Costina.Desktop.Codes.Of("","Sala",["sala","SALA-2"])!="sala-3"||Costina.Desktop.Codes.Of(" Mesa_1 ","x",[])!="Mesa_1"||Costina.Desktop.Codes.Generated("Añada ¡2026!",[])!="anada-2026"
            ||Costina.Desktop.Codes.Generated("Degustación de otoño con maridaje",["degustacion-de-otono-con-ma"],27)!="degustacion-de-otono-con-2")
            throw new Exception("Generated codes are ASCII, unique with a suffix that fits the maximum, and a typed code stays as typed.");
        // ===== E4d: menu del dia, "Hacerlo menu del dia", Subir y Bajar =====
        // La marca la lleva la primera oferta vigente de la configuracion (DEGUSTACION): chip en su fila y en su ficha, sin el boton.
        PickOffer("DEGUSTACION");
        if(!offersView.IsDailyOffer||!offersView.Offers[0].IsDaily||offersView.Offers.Skip(1).Any(o=>o.IsDaily)||OfferShows("MakeDailyButton")||!OfferShows("DailyChip")||!Painted(offerList).Contains("Menú del día"))
            throw new Exception("The first available offer is the daily menu: chip on its row and its ficha, no 'Hacerlo menú del día' button.");
        // En otra, el boton manda offers-reorder con ella delante y se relee todo (la propuesta del Comedor cambia con la configuracion).
        PickOffer("CARTA");
        if(offersView.IsDailyOffer||OfferShows("DailyChip")||!OfferReady("MakeDailyButton")) throw new Exception("Another offer offers 'Hacerlo menú del día'.");
        var readsBeforeDaily=server.Count("GET","configuration");
        SendOffer("MakeDailyButton","Hacerlo menú del día");
        if(CatalogPost()!="erp/offers/commands/offers-reorder {\"offerIds\":[\"CARTA\"]}"||window.Shell.Status!="Carta es ahora el menú del día: confirmado por el servidor."||server.Count("GET","configuration")<=readsBeforeDaily)
            throw new Exception("'Hacerlo menú del día' sends offers-reorder with that offer first and re-reads the configuration: "+CatalogPost()+" / "+window.Shell.Status);
        // Subir y Bajar en los pases: con ordenes crecientes se intercambian los dos (dos course-update, sin numero de orden a la vista);
        // el primero no puede subir ni el ultimo bajar.
        PickOffer("DEGUSTACION"); offersView.PickCourseCommand.Execute(offersView.Courses[0]); window.UpdateLayout();
        if(offersView.MoveCourseCommand.CanExecute("up")||!offersView.MoveCourseCommand.CanExecute("down")||InOffers<Button>("CourseUpButton").IsEnabled||!InOffers<Button>("CourseDownButton").IsEnabled)
            throw new Exception("The first course can go down but not up.");
        offersView.PickCourseCommand.Execute(offersView.Courses[1]); window.UpdateLayout();
        var postsBeforeMove=server.Requests.Length;
        SendOffer("CourseUpButton","Subir el pase Mar");
        var moves=server.Requests.Skip(postsBeforeMove).Where(r=>r.Method=="POST").Select(r=>r.Path+" "+r.Body).ToArray();
        if(!moves.SequenceEqual(["erp/offers/commands/course-update {\"offerId\":\"DEGUSTACION\",\"id\":\"aperitivos\",\"sort\":1}","erp/offers/commands/course-update {\"offerId\":\"DEGUSTACION\",\"id\":\"mar\",\"sort\":0}"])
            ||window.Shell.Status!="Pase Mar movido: confirmado por el servidor.") throw new Exception("'Subir' swaps the two sorts with two course-update orders: "+string.Join(" | ",moves)+" / "+window.Shell.Status);
        if(!Costina.Desktop.ViewModels.OffersViewModel.MovedForChecks([0,0,0],1,2).SequenceEqual([0,20,10])) throw new Exception("With tied sorts a move renumbers every sibling by tens.");
        // Las capturas: el Menu del dia con sus principales a elegir y la merluza elegida (offers-menu.png); la Degustacion Costina con sus seis
        // pases y el Pescado desplegado es la de offers.png del bucle de capturas. Las ofertas vuelven a ser las de ejemplo.
        server.Offers=sampleOffersC2b;
        OpenOffers("the offers are read again for the captures");
        PickOffer("DIA"); PickCourseRow("principales"); dishList.SelectedItem=offersView.Dishes[0]; window.UpdateLayout();
        if(offersView.SelectedDish?.Dish.Id!="merluza"||!OfferShows("DishEditor")) throw new Exception("The capture shows the Menú del día with the hake chosen.");
        offerScroll.ScrollToHome(); window.UpdateLayout();
        OffersFit("offers-menu","SaveOfferButton"); Save(Shot(window),"offers-menu.png");
        dishList.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});
        Wait(()=>{ window.UpdateLayout(); return DishEditorInView(); },"clicking the hake brings its ficha into view");
        OffersFit("offers-dish","SaveOfferButton"); Save(Shot(window),"offers-dish.png");
        PickOffer("DEGUSTACION"); PickCourseRow("pescado");
        if(offersView.SelectedCourse?.Course.Id!="pescado"||offersView.SelectedDish is not null) throw new Exception("The capture shows the Degustación Costiña with Pescado unfolded.");
        OffersFit("offers","SaveOfferButton");
        // ===== C2b (plan 2026-10-14): Salas, mesas y estaciones con pestanas, salas, mesas en tarjetas y ficha =====
        // La organizacion de ejemplo es la del comedor de C2 (la de los checks del Catalogo: Sala principal con sus 8 mesas, Terraza con sus 4
        // y su tarifa propia, y las estaciones Pase, Cocina fria, Cocina caliente, Sala 1 y Terraza) mas lo que el comedor no ve: la Mesa 9,
        // desactivada. El servidor simulado aplica las ordenes en memoria como el motor y rechaza desactivar una mesa en uso (las siete
        // ocupadas del tablero de C2) o una sala con mesas activas. Elegir no envia nada; cada orden lleva la sala, la mesa o la estacion
        // elegidas (F01).
        var roomsOrganization=wineOrganization with {Zones=[..wineOrganization.Zones.Select(z=>z.Id!="sala"?z:z with {Tables=[..z.Tables,new("M9","Mesa 9",4,"sala",8,false)]})]};
        server.TablesInUse.Clear(); foreach(var seated in diningBoard) server.TablesInUse.Add(seated.Service.TableId);
        T InRooms<T>(string name) where T:FrameworkElement=>(T)organizationPanel.FindName(name);
        bool RoomShows(string name)=>InRooms<FrameworkElement>(name).IsVisible;
        bool RoomReady(string name)=>InRooms<Button>(name) is {IsVisible:true,IsEnabled:true};
        var zoneList=InRooms<ListBox>("ZoneList"); var tableList=InRooms<ListBox>("TableList"); var stationList=InRooms<ListBox>("StationList");
        var roomTabs=InRooms<ListBox>("SectionTabs"); var tableZoneBox=InRooms<ComboBox>("TableZoneBox"); var stationKindBox=InRooms<ComboBox>("StationKindBox");
        void OpenRooms(string what)
        {
            var before=server.Count("GET","organization");
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); tabs.SelectedItem=(TabItem)window.FindName("OrganizationTab"); window.UpdateLayout();
            Wait(()=>!window.Shell.Busy&&server.Count("GET","organization")>before,what); window.UpdateLayout();
        }
        void SendRooms(string button,string what)
        {
            var before=server.Count("POST","organization/commands/");
            Press(InRooms<Button>(button),what);
            Wait(()=>!window.Shell.Busy&&server.Count("POST","organization/commands/")>before,what+" is sent"); window.UpdateLayout();
        }
        // Como pulsar la tarjeta o la fila.
        void PickZone(string id){ zoneList.SelectedItem=organization.ZoneItems.Single(z=>z.Zone.Id==id); window.UpdateLayout(); }
        void PickTable(string id){ tableList.SelectedItem=organization.TableItems.Single(t=>t.Table.Id==id); window.UpdateLayout(); }
        void PickStation(string id){ stationList.SelectedItem=organization.StationItems.Single(s=>s.Station.Id==id); window.UpdateLayout(); }
        string ZoneRowsText()=>string.Join(" | ",organization.ZoneItems.Select(z=>z.Zone.Name+": "+z.Detail+(z.Tariff is {} tariff?" · "+tariff:"")+(z.Inactive?" · Desactivada":"")));
        string CardsText()=>string.Join(" | ",organization.TableItems.Select(t=>t.ToString()));
        ListBoxItem Card(string id)=>(ListBoxItem)tableList.ItemContainerGenerator.ContainerFromItem(organization.TableItems.Single(t=>t.Table.Id==id));
        bool Marked(string id,string part)=>Card(id).Template.FindName(part,Card(id)) is UIElement{Visibility:Visibility.Visible};   // Ring (elegida)
        // Revision de C2b: una mesa desactivada es una tarjeta gris (Surface2); el borde discontinuo queda para 'Libre' en el Comedor.
        bool Grey(string id)
        {
            var card=Card(id); card.ApplyTemplate(); card.UpdateLayout();   // un contenedor recien generado aun no tiene aplicada su plantilla
            // El fondo gris lo pisa el raton por encima (en CI el cursor puede quedar sobre la unica mesa de la sala): se mira el gris del texto, que
            // solo pone el disparador de mesa desactivada.
            return card.Template.FindName("Bd",card) is Border bd&&System.Windows.Documents.TextElement.GetForeground(bd) is SolidColorBrush b&&b.Color==((SolidColorBrush)window.FindResource("InkSoft")).Color;
        }
        IEnumerable<string> RowPainted(ListBox list,Func<object,bool> pick)=>list.ItemContainerGenerator.ContainerFromItem(list.Items.Cast<object>().Single(pick)) is ListBoxItem row?Painted(row):[];
        void RoomsFit(string what,string bottom)
        {
            window.Shell.Status=""; window.UpdateLayout();
            window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
            var button=InRooms<Button>(bottom);
            if(button.TranslatePoint(new Point(0,button.ActualHeight),organizationPanel).Y>organizationPanel.ActualHeight+0.5) throw new Exception(what+": the ficha keeps its buttons in view.");
            if(organization.ShowZones&&(Math.Abs(InRooms<FrameworkElement>("ZonesPane").ActualWidth-250)>0.5||Math.Abs(InRooms<FrameworkElement>("ZonePanel").ActualWidth-360)>0.5||InRooms<FrameworkElement>("TablesArea").ActualWidth<300))
                throw new Exception(what+": the rooms keep 250 px, the ficha 360 px and the tables the rest.");
            if(organization.ShowStations&&(Math.Abs(InRooms<FrameworkElement>("StationsPane").ActualWidth-ListWidth)>0.5||InRooms<FrameworkElement>("StationPanel").ActualWidth<460))
                throw new Exception(what+": the stations keep 360 px and the ficha the rest.");
            if(Clipped(organizationPanel) is {Length:>0} clipped) throw new Exception(what+" clips text: "+string.Join(" | ",clipped));
            if(NestedScrolls(organizationPanel) is {Length:>0} nested) throw new Exception(what+" nests scroll viewers: "+string.Join(", ",nested));
            RefuseRaw(window,what);
        }
        // Sin salas (una instalacion recien hecha: solo la estacion Pase y la tarifa General): la lista explica que falta y ofrece crear la
        // primera; el centro dice que pondra y la ficha no muestra campos.
        server.Organization=new([],[new("pase","Pase","Pass",0,true)],[new("general","General",0,true)]); window.Shell.Status="";
        OpenRooms("the configuration reads an empty organization");
        if(organization.Section!="zones"||!organization.Sections.Select(s=>s.Label).SequenceEqual(["Salas y mesas · 0","Estaciones · 1"])||!organization.Sections[0].IsCurrent||organization.Sections[1].IsCurrent)
            throw new Exception("The configuration opens on 'Salas y mesas', with its two tabs: "+string.Join(",",organization.Sections.Select(s=>s.Label)));
        if(!organization.ShowZoneListEmpty||organization.ZoneListHeading!="Aún no hay salas"||!RoomReady("EmptyNewZoneButton")||RoomShows("ZoneList")||!RoomReady("NewZoneButton")||RoomShows("NewStationButton")
            ||!RoomShows("TablesEmpty")||organization.TablesEmptyHeading!="Sin mesas todavía"||RoomShows("EmptyNewTableButton")||RoomShows("NewTableButton")||!RoomShows("ZoneEmpty")||RoomShows("ZoneDetail")
            ||RoomShows("TableDetail")||organization.ZoneEmptyHeading!="Sin salas todavía"
            ||organization.Summary!="Aún no hay salas: crea la primera con «Nueva sala»."||!Painted(organizationPanel).Contains("Crea la primera sala del local (Sala principal, Terraza…) y después sus mesas."))
            throw new Exception("Without rooms the list says what is missing and offers 'Nueva sala'; the centre says what will be there and the ficha shows no fields: "+organization.ZoneListHeading+" / "+organization.ZoneEmptyHeading);
        RoomsFit("configuration-empty","NewZoneButton"); Save(Shot(window),"configuration-empty.png");
        // El alta de una sala: su ficha (codigo editable, tarifa General) y el centro dice que sus mesas vendran despues; "Cancelar" no envia nada.
        var postsBeforeNewZone=server.Count("POST","");
        Press(InRooms<Button>("EmptyNewZoneButton"),"Nueva sala (sin salas)");
        if(!organization.NewZone||!RoomShows("ZoneDetail")||organization.ZoneTitle!="Nueva sala"||organization.ZoneContext!="Alta de sala"||organization.ZoneSaveText!="Crear sala"||!RoomReady("CancelZoneButton")
            ||RoomShows("DeactivateZoneButton")||RoomShows("ReactivateZoneButton")||RoomShows("ZoneTablesArea")||!RoomShows("TablesEmpty")||organization.TablesEmptyHeading!="Sala nueva"
            ||!Descendants<TextBox>(organizationPanel).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Código de la sala").IsEnabled
            ||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"General"})
            throw new Exception("'Nueva sala' opens the new-room ficha with the General tariff: "+organization.ZoneTitle);
        Press(InRooms<Button>("CancelZoneButton"),"Cancelar el alta de sala");
        if(organization.NewZone||RoomShows("ZoneDetail")||!RoomShows("ZoneEmpty")||server.Count("POST","")!=postsBeforeNewZone) throw new Exception("Cancelling the new room returns to the empty ficha and sends nothing.");
        // La organizacion del comedor: resumen, una tarjeta por sala (mesas activas y plazas; la tarifa propia solo en Terraza) y, sin eleccion
        // previa, la primera sala elegida con sus mesas en el centro (la desactivada, con borde discontinuo y su palabra) y su ficha.
        server.Organization=roomsOrganization;
        OpenRooms("the configuration reads the C2 organization");
        if(organization.Summary!="2 salas activas · 12 mesas activas (42 plazas) · 5 estaciones activas"||ZoneRowsText()!="Sala principal: 8 mesas activas · 26 plazas | Terraza: 4 mesas activas · 16 plazas · Tarifa Terraza")
            throw new Exception("Each room card carries its active tables, its seats and its own tariff (never 'General'): "+ZoneRowsText()+" / "+organization.Summary);
        if(!RowPainted(zoneList,o=>o is Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"terraza"}).Contains("Tarifa Terraza")||RowPainted(zoneList,o=>o is Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"sala"}).Any(t=>t.StartsWith("Tarifa",StringComparison.Ordinal)))
            throw new Exception("Only the room with its own tariff paints it.");
        if(organization.SelectedZone?.Id!="sala"||zoneList.SelectedItem is not Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"sala"}||zoneList.ItemContainerGenerator.ContainerFromItem(zoneList.SelectedItem) is not ListBoxItem{IsSelected:true}
            ||!RoomShows("ZoneDetail")||RoomShows("TableDetail")||RoomShows("ZoneEmpty")||organization.ZoneContext!="Sala · tarifa General"||organization.ZoneTitle!="Sala principal"||organization.ZoneStateText!="Activa"
            ||organization.ZoneStateTone!="ok"||organization.ZoneId!="sala"||organization.ZoneName!="Sala principal"||organization.ZoneSort!="0"||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"General"}
            ||organization.ZoneTablesFigure!="8"||organization.ZoneSeatsFigure!="26"||organization.ZoneTablesText!="1 mesa desactivada: Mesa 9."||organization.ZoneSaveText!="Guardar cambios"
            ||!RoomReady("SaveZoneButton")||!RoomReady("DeactivateZoneButton")||RoomShows("ReactivateZoneButton")||RoomShows("CancelZoneButton"))
            throw new Exception("The first room is chosen and its ficha shows its data, its tariff and its tables: "+organization.ZoneContext+" / "+organization.ZoneTablesText);
        if(organization.TablesTitle!="Mesas de Sala principal"||organization.TablesSummary!="8 mesas activas · 26 plazas · 1 desactivada"||!RoomReady("NewTableButton")||RoomShows("TablesEmpty")
            ||CardsText()!="Mesa 1 · 2 plazas · Activa | Mesa 2 · 4 plazas · Activa | Mesa 3 · 2 plazas · Activa | Mesa 4 · 4 plazas · Activa | Mesa 5 · 6 plazas · Activa | Mesa 6 · 2 plazas · Activa"
                +" | Mesa 7 · 2 plazas · Activa | Mesa 8 · 4 plazas · Activa | Mesa 9 · 4 plazas · Desactivada"||organization.SelectedTable is not null||tableList.SelectedItem is not null)
            throw new Exception("The centre shows the chosen room's tables as cards (name, seats and state in words), none chosen: "+CardsText());
        if(!Grey("M9")||organization.TableItems.Where(t=>t.Table.Id!="M9").Any(t=>Grey(t.Table.Id))||!Painted(Card("M9")).Contains("Desactivada")||!Painted(Card("M1")).Contains("Activa")
            ||!Painted(Card("M5")).Contains("6 plazas")) throw new Exception("Only the deactivated table is a grey card, and every card says its state in words.");
        if(window.ActualWidth>=1270&&!window.Shell.SideNavCompact&&(Card("M1").TranslatePoint(new Point(),tableList).Y!=Card("M2").TranslatePoint(new Point(),tableList).Y
            ||Card("M3").TranslatePoint(new Point(),tableList).Y<=Card("M1").TranslatePoint(new Point(),tableList).Y||Card("M3").TranslatePoint(new Point(),tableList).X!=Card("M1").TranslatePoint(new Point(),tableList).X))
            throw new Exception("At 1280 px with the sidebar unfolded the tables sit in two columns.");
        RoomsFit("configuration-room","SaveZoneButton");
        // Elegir una mesa solo la elige (nada se lee ni se envia): su ficha sustituye a la de la sala, con su sala por nombre y las salas
        // activas para moverla. El anillo de acento marca su tarjeta.
        var requestsBeforeTable=server.Requests.Length;
        PickTable("M4");
        if(organization.SelectedTable?.Id!="M4"||!RoomShows("TableDetail")||RoomShows("ZoneDetail")||organization.TableContext!="Mesa de Sala principal"||organization.TableTitle!="Mesa 4"
            ||organization.TableStateText!="Activa"||organization.TableId!="M4"||organization.TableName!="Mesa 4"||organization.TableCapacity!="4"||organization.TableSort!="3"||organization.TableZoneId!="sala"
            ||tableZoneBox.SelectedItem is not Costina.Desktop.ViewModels.ZoneChoice{Name:"Sala principal"}||!organization.ZoneChoices.Select(z=>z.Name).SequenceEqual(["Sala principal","Terraza"])
            ||!RoomReady("SaveTableButton")||!RoomReady("DeactivateTableButton")||RoomShows("ReactivateTableButton")||RoomShows("CancelTableButton")||!RoomReady("ShowZoneButton")
            ||!Marked("M4","Ring")||Marked("M1","Ring")||server.Requests.Length!=requestsBeforeTable)
            throw new Exception("Choosing a table card shows its ficha, reads and sends nothing: "+organization.TableContext+" / "+organization.TableTitle);
        RoomsFit("configuration-table","SaveTableButton"); Save(Shot(window),"configuration-table.png");
        // Una mesa en uso no se desactiva: el servidor lo rechaza (table_in_use) y su texto sale tal cual; la mesa sigue elegida y activa.
        SendRooms("DeactivateTableButton","Desactivar mesa en uso");
        if(CatalogPost()!="organization/commands/table-deactivate {\"id\":\"M4\"}"||window.Shell.Status!="La mesa está en uso: no se puede desactivar. Actualiza antes de una nueva acción."
            ||organization.SelectedTable?.Id!="M4"||organization.SelectedTable.Active!=true||!RoomReady("DeactivateTableButton")||window.Shell.Api.Pending is not null)
            throw new Exception("A table in use is not deactivated: the server's rejection is shown as it is and the table stays chosen and active: "+window.Shell.Status);
        // Una mesa libre si: el servidor la desactiva, la tarjeta pasa a gris con su palabra y la ficha ofrece reactivarla.
        PickTable("M2");
        SendRooms("DeactivateTableButton","Desactivar mesa");
        if(CatalogPost()!="organization/commands/table-deactivate {\"id\":\"M2\"}"||window.Shell.Status!="Desactivar mesa Mesa 2: confirmado por el servidor."||organization.SelectedTable?.Id!="M2"
            ||tableList.SelectedItem is not Costina.Desktop.ViewModels.TableCard{Table.Id:"M2"}||organization.TableStateText!="Desactivada"||!RoomReady("ReactivateTableButton")||RoomShows("DeactivateTableButton")
            ||organization.TablesSummary!="7 mesas activas · 22 plazas · 2 desactivadas"||!Grey("M2")||!Painted(Card("M2")).Contains("Desactivada"))
            throw new Exception("A free table is deactivated and stays chosen, grey and offering to reactivate: "+organization.TablesSummary);
        SendRooms("ReactivateTableButton","Reactivar mesa");
        if(CatalogPost()!="organization/commands/table-reactivate {\"id\":\"M2\"}"||organization.SelectedTable?.Id!="M2"||organization.TableStateText!="Activa"||Grey("M2")||!RoomReady("DeactivateTableButton"))
            throw new Exception("'Reactivar mesa' sends the usual body and the card is active again: "+CatalogPost());
        // F01: guardar una mesa con otra sala la lleva alli y la vista la sigue (esa sala y la mesa, elegidas); devolverla la trae de vuelta.
        PickTable("M8");
        tableZoneBox.SelectedValue="terraza"; window.UpdateLayout();
        SendRooms("SaveTableButton","Guardar mesa en la terraza");
        if(CatalogPost()!="organization/commands/table-update {\"id\":\"M8\",\"name\":\"Mesa 8\",\"capacity\":4,\"zoneId\":\"terraza\",\"sort\":7}"||organization.SelectedZone?.Id!="terraza"
            ||organization.SelectedTable?.Id!="M8"||zoneList.SelectedItem is not Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"terraza"}||tableList.SelectedItem is not Costina.Desktop.ViewModels.TableCard{Table.Id:"M8"}
            ||organization.TableContext!="Mesa de Terraza"||CardsText()!="Terraza 1 · 4 plazas · Activa | Terraza 2 · 2 plazas · Activa | Terraza 3 · 4 plazas · Activa | Terraza 4 · 6 plazas · Activa | Mesa 8 · 4 plazas · Activa")
            throw new Exception("A table moved to another room is followed there, with that room and the table chosen (F01): "+CatalogPost()+" / "+CardsText());
        tableZoneBox.SelectedValue="sala"; window.UpdateLayout();
        SendRooms("SaveTableButton","Devolver la mesa a su sala");
        if(organization.SelectedZone?.Id!="sala"||organization.SelectedTable?.Id!="M8"||organization.Tables.Length!=9||organization.Tables[7].Id!="M8") throw new Exception("The table is back in its room, chosen.");
        // "Ver la sala" (o pulsar otra vez la sala elegida) vuelve a la ficha de la sala: la mesa deja de estar elegida y sus ordenes, de poder enviarse.
        Press(InRooms<Button>("ShowZoneButton"),"Ver la sala");
        if(organization.SelectedTable is not null||tableList.SelectedItem is not null||!RoomShows("ZoneDetail")||RoomShows("TableDetail")||organization.SelectedZone?.Id!="sala"||organization.TableId.Length!=0
            ||organization.SaveTableCommand.CanExecute(null)||organization.ToggleTableCommand.CanExecute(null)||Marked("M8","Ring")) throw new Exception("'Ver la sala' shows the room's ficha and leaves no table chosen.");
        PickTable("M5");
        ((ListBoxItem)zoneList.ItemContainerGenerator.ContainerFromItem(zoneList.SelectedItem)).RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left)
            {RoutedEvent=UIElement.PreviewMouseUpEvent}); window.UpdateLayout();   // la pulsacion baja hasta la fila; cada elemento la convierte en la del boton izquierdo
        if(organization.SelectedTable is not null||!RoomShows("ZoneDetail")||organization.SelectedZone?.Id!="sala") throw new Exception("Clicking the chosen room again shows its ficha.");
        // Otra sala (F01): ninguna mesa de la anterior queda elegida ni en su ficha. Terraza muestra su tarifa propia (E3.2) y la guarda por codigo.
        PickTable("M4"); PickZone("terraza");
        if(organization.SelectedTable is not null||organization.TableId.Length!=0||organization.SaveTableCommand.CanExecute(null)||!RoomShows("ZoneDetail")||organization.ZoneContext!="Sala · tarifa Terraza"
            ||organization.ZoneTitle!="Terraza"||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"Terraza"}||organization.ZoneTariffId!="terraza"||!organization.TariffChoices.Select(t=>t.Name).SequenceEqual(["General","Terraza"])
            ||organization.TablesTitle!="Mesas de Terraza"||organization.TablesSummary!="4 mesas activas · 16 plazas"||organization.ZoneTablesText!="Todas sus mesas están activas.")
            throw new Exception("Choosing another room drops the table of the previous one and shows the room with its own tariff: "+organization.ZoneContext);
        SendRooms("SaveZoneButton","Guardar sala");
        if(CatalogPost()!="organization/commands/zone-update {\"id\":\"terraza\",\"name\":\"Terraza\",\"sort\":1,\"tariffId\":\"terraza\"}"||organization.SelectedZone?.Id!="terraza"
            ||zoneTariffBox.SelectedItem is not Costina.Client.TariffDto{Name:"Terraza"}) throw new Exception("'Guardar cambios' sends the usual body of the chosen room, its tariff by code: "+CatalogPost());
        // Una sala con mesas activas no se desactiva: lo rechaza el servidor (zone_in_use) y su texto sale tal cual.
        SendRooms("DeactivateZoneButton","Desactivar sala con mesas");
        if(CatalogPost()!="organization/commands/zone-deactivate {\"id\":\"terraza\"}"||window.Shell.Status!="La sala está en uso: no se puede desactivar. Actualiza antes de una nueva acción."
            ||organization.SelectedZone?.Active!=true||!RoomReady("DeactivateZoneButton")) throw new Exception("A room with active tables is not deactivated: the server's rejection is shown as it is: "+window.Shell.Status);
        // Alta de una sala con la Mesa 1 elegida: la lista queda sin eleccion y el centro dice que sus mesas vendran despues. Al confirmarla, la
        // sala nueva queda elegida, sin mesas (lo explica y ofrece la primera); la mesa nueva nace en ella y queda elegida.
        PickZone("sala"); PickTable("M1");
        Press(InRooms<Button>("NewZoneButton"),"Nueva sala");
        if(!organization.NewZone||organization.SelectedZone is not null||organization.SelectedTable is not null||zoneList.SelectedItem is not null||organization.TableItems.Length!=0||!RoomShows("TablesEmpty")
            ||organization.TablesEmptyHeading!="Sala nueva"||RoomShows("NewTableButton")||organization.ZoneSort!="2"||!RoomShows("ZoneDetail")) throw new Exception("'Nueva sala' leaves the list with nothing chosen.");
        Press(InRooms<Button>("CancelZoneButton"),"Cancelar el alta de sala");
        if(organization.SelectedZone?.Id!="sala"||organization.SelectedTable?.Id!="M1"||tableList.SelectedItem is not Costina.Desktop.ViewModels.TableCard{Table.Id:"M1"}||!RoomShows("TableDetail"))
            throw new Exception("Cancelling the new room returns to the room and table chosen before.");
        Press(InRooms<Button>("NewZoneButton"),"Nueva sala");
        organization.ZoneId="privado"; organization.ZoneName="Comedor privado"; window.UpdateLayout();
        SendRooms("SaveZoneButton","Crear sala");
        if(CatalogPost()!="organization/commands/zone-create {\"id\":\"privado\",\"name\":\"Comedor privado\",\"sort\":2,\"tariffId\":\"\"}"||organization.NewZone||organization.SelectedZone?.Id!="privado"
            ||zoneList.SelectedItem is not Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"privado"}||!RoomShows("ZoneDetail")||organization.ZoneTitle!="Comedor privado"||!RoomShows("TablesEmpty")
            ||organization.TablesEmptyHeading!="Sala sin mesas"||organization.TablesEmptyText!="Comedor privado aún no tiene mesas: añade la primera."||!RoomReady("EmptyNewTableButton")
            ||organization.ZoneTablesText!="Aún no tiene mesas: añádelas con «Mesa»."||!ZoneRowsText().EndsWith(" | Comedor privado: Sin mesas activas",StringComparison.Ordinal))
            throw new Exception("A created room is chosen once the server confirms it and explains it has no tables yet: "+CatalogPost()+" / "+ZoneRowsText());
        Press(InRooms<Button>("EmptyNewTableButton"),"Nueva mesa (sala sin mesas)");
        if(!organization.NewTable||!RoomShows("TableDetail")||organization.TableTitle!="Nueva mesa"||organization.TableContext!="Alta de mesa en Comedor privado"||organization.TableZoneId!="privado"
            ||organization.TableSaveText!="Crear mesa"||!RoomReady("CancelTableButton")||RoomShows("DeactivateTableButton")||tableZoneBox.SelectedItem is not Costina.Desktop.ViewModels.ZoneChoice{Name:"Comedor privado"})
            throw new Exception("'Nueva mesa' opens the new-table ficha inside the chosen room: "+organization.TableContext);
        organization.TableId=""; organization.TableName="Mesa del reservado"; organization.TableCapacity="muchas"; window.Shell.Status=""; var roomPosts=server.Count("POST",""); window.UpdateLayout();
        Press(InRooms<Button>("SaveTableButton"),"Crear mesa con plazas mal escritas");
        if(window.Shell.Status!="Plazas: introduce un número entre 1 y 60."||server.Count("POST","")!=roomPosts||!organization.NewTable) throw new Exception("Misspelt seats are refused in the status bar and send nothing: "+window.Shell.Status);
        organization.TableId="P1"; organization.TableCapacity="10"; window.UpdateLayout();
        SendRooms("SaveTableButton","Crear mesa");
        if(CatalogPost()!="organization/commands/table-create {\"id\":\"P1\",\"name\":\"Mesa del reservado\",\"capacity\":10,\"zoneId\":\"privado\",\"sort\":0}"||organization.SelectedZone?.Id!="privado"
            ||organization.SelectedTable?.Id!="P1"||tableList.SelectedItem is not Costina.Desktop.ViewModels.TableCard{Table.Id:"P1"}||organization.TableTitle!="Mesa del reservado"||organization.NewTable)
            throw new Exception("A created table is chosen in its room once the server confirms it: "+CatalogPost());
        // La sala se desactiva cuando ya no le quedan mesas activas: lista y ficha lo dicen con su palabra, y su mesa ofrece su sala marcada.
        SendRooms("DeactivateTableButton","Desactivar la mesa del reservado");
        Press(InRooms<Button>("ShowZoneButton"),"Ver la sala");
        SendRooms("DeactivateZoneButton","Desactivar sala");
        if(CatalogPost()!="organization/commands/zone-deactivate {\"id\":\"privado\"}"||organization.SelectedZone?.Id!="privado"||organization.ZoneStateText!="Desactivada"||organization.ZoneStateTone!="other"
            ||!RoomReady("ReactivateZoneButton")||RoomShows("DeactivateZoneButton")||!ZoneRowsText().EndsWith(" | Comedor privado: Sin mesas activas · Desactivada",StringComparison.Ordinal)
            ||!RowPainted(zoneList,o=>o is Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"privado"}).Contains("Desactivada")||RowPainted(zoneList,o=>o is Costina.Desktop.ViewModels.ZoneRow{Zone.Id:"sala"}).Contains("Desactivada")
            ||organization.TablesSummary!="Ninguna mesa activa · 1 desactivada"||!Grey("P1")||organization.Summary!="2 salas activas · 12 mesas activas (42 plazas) · 5 estaciones activas")
            throw new Exception("A room without active tables is deactivated and says so in words: "+ZoneRowsText()+" / state "+organization.ZoneStateText+" "+organization.ZoneStateTone
                +" / reactivate "+RoomReady("ReactivateZoneButton")+" deactivate "+RoomShows("DeactivateZoneButton")+" / tables "+organization.TablesSummary+" / grey "+Grey("P1")+" / summary "+organization.Summary+" / post "+CatalogPost());
        PickTable("P1");
        if(!organization.ZoneChoices.Select(z=>z.Name).SequenceEqual(["Sala principal","Terraza","Comedor privado (desactivada)"])||tableZoneBox.SelectedItem is not Costina.Desktop.ViewModels.ZoneChoice{Name:"Comedor privado (desactivada)"})
            throw new Exception("A table of a deactivated room is offered its room, marked: "+string.Join(",",organization.ZoneChoices.Select(z=>z.Name)));
        RoomsFit("configuration-inactive","SaveTableButton"); Save(Shot(window),"configuration-inactive.png");
        PickZone("sala"); PickTable("M1");
        if(!organization.ZoneChoices.Select(z=>z.Name).SequenceEqual(["Sala principal","Terraza"])) throw new Exception("Another table is never offered a deactivated room.");
        // "Mesa" con una mesa elegida: la lista queda sin eleccion; "Cancelar" vuelve a la mesa de antes sin enviar nada.
        var postsBeforeNewTable=server.Count("POST","");
        Press(InRooms<Button>("NewTableButton"),"Nueva mesa");
        if(!organization.NewTable||organization.SelectedTable is not null||tableList.SelectedItem is not null||organization.TableSort!="9"||organization.TableZoneId!="sala") throw new Exception("'Mesa' leaves the cards with none chosen.");
        Press(InRooms<Button>("CancelTableButton"),"Cancelar el alta de mesa");
        if(organization.NewTable||organization.SelectedTable?.Id!="M1"||tableList.SelectedItem is not Costina.Desktop.ViewModels.TableCard{Table.Id:"M1"}||server.Count("POST","")!=postsBeforeNewTable)
            throw new Exception("Cancelling the new table returns to the table chosen before and sends nothing.");
        // Estaciones: su pestana, la lista (la Pase, reservada, con su palabra) y, sin eleccion, la ficha que dice que elegir.
        roomTabs.SelectedIndex=1; window.UpdateLayout();
        if(organization.Section!="stations"||!organization.Sections[1].IsCurrent||organization.Sections[0].IsCurrent||!RoomShows("StationsSection")||RoomShows("ZonesSection")||!RoomReady("NewStationButton")
            ||RoomShows("NewZoneButton")) throw new Exception("The 'Estaciones' tab is chosen and painted as chosen, with its own create button.");
        if(string.Join(" | ",organization.StationItems.Select(s=>s.ToString()))!="Pase · Pase · existe siempre · reservada | Cocina fría · Cocina | Cocina caliente · Cocina | Sala 1 · Sala | Terraza · Sala"
            ||organization.SelectedStation is not null||!RoomShows("StationEmpty")||RoomShows("StationDetail")||organization.StationEmptyHeading!="Elige una estación")
            throw new Exception("Each station row carries its kind; the pass station is reserved; nothing chosen, the ficha says what to pick: "+string.Join(" | ",organization.StationItems.Select(s=>s.ToString())));
        if(!RowPainted(stationList,o=>o is Costina.Desktop.ViewModels.StationRow{Station.Id:"pase"}).Contains("Reservada")||organization.StationItems.Skip(1).Any(s=>RowPainted(stationList,o=>ReferenceEquals(o,s)).Contains("Reservada")))
            throw new Exception("Only the pass station is marked 'Reservada', in words.");
        RoomsFit("configuration-stations-unselected","NewStationButton");
        PickStation("pase");
        if(organization.StationContext!="Estación · Pase"||organization.StationTitle!="Pase"||organization.StationStateText!="Activa"||!organization.IsReservedStation||!Painted(InRooms<FrameworkElement>("StationDetail")).Contains("Reservada")
            ||RoomShows("DeactivateStationButton")||RoomShows("ReactivateStationButton")||!RoomReady("SaveStationButton")||organization.StationNote!="La estación Pase existe siempre: no se puede desactivar ni cambiar de tipo."
            ||organization.StationKind!="pass"||stationKindBox.SelectedItem is not ComboBoxItem{Content:"Pase"}||!organization.StationKindHelp.StartsWith("Pase:",StringComparison.Ordinal))
            throw new Exception("The pass station is reserved: no deactivate button and its note says why: "+organization.StationContext);
        PickStation("hot");
        if(organization.StationContext!="Estación · Cocina"||organization.StationTitle!="Cocina caliente"||organization.IsReservedStation||!RoomReady("DeactivateStationButton")||organization.StationKind!="kitchen"
            ||stationKindBox.SelectedItem is not ComboBoxItem{Content:"Cocina"}||organization.StationId!="hot"||organization.StationSort!="2"||!organization.StationKindHelp.StartsWith("Cocina:",StringComparison.Ordinal))
            throw new Exception("A kitchen station shows its data and kind: "+organization.StationContext);
        SendRooms("SaveStationButton","Guardar estación");
        if(CatalogPost()!="organization/commands/station-update {\"id\":\"hot\",\"name\":\"Cocina caliente\",\"kind\":\"kitchen\",\"sort\":2}"||organization.SelectedStation?.Id!="hot"
            ||stationList.SelectedItem is not Costina.Desktop.ViewModels.StationRow{Station.Id:"hot"}) throw new Exception("'Guardar cambios' sends the usual body of the chosen station: "+CatalogPost());
        PickStation("sala-1");
        SendRooms("DeactivateStationButton","Desactivar estación");
        if(CatalogPost()!="organization/commands/station-deactivate {\"id\":\"sala-1\"}"||organization.SelectedStation?.Id!="sala-1"||organization.StationStateText!="Desactivada"||!RoomReady("ReactivateStationButton")
            ||RoomShows("DeactivateStationButton")||!RowPainted(stationList,o=>o is Costina.Desktop.ViewModels.StationRow{Station.Id:"sala-1"}).Contains("Desactivada")||!organization.Summary.EndsWith(" · 4 estaciones activas",StringComparison.Ordinal))
            throw new Exception("A deactivated station stays chosen and says so in words: "+organization.Summary);
        SendRooms("ReactivateStationButton","Reactivar estación");
        if(CatalogPost()!="organization/commands/station-reactivate {\"id\":\"sala-1\"}"||organization.StationStateText!="Activa") throw new Exception("'Reactivar estación' sends the usual body: "+CatalogPost());
        // Alta de estacion: la lista queda sin eleccion; "Cancelar" vuelve a la de antes; al confirmarla el servidor, la nueva queda elegida.
        Press(InRooms<Button>("NewStationButton"),"Nueva estación");
        if(!organization.NewStation||stationList.SelectedItem is not null||organization.StationTitle!="Nueva estación"||organization.StationContext!="Alta de estación"||organization.StationKind!="kitchen"
            ||organization.StationSort!="5"||!RoomReady("CancelStationButton")||RoomShows("DeactivateStationButton")||organization.StationSaveText!="Crear estación") throw new Exception("'Nueva estación' opens the new-station ficha.");
        Press(InRooms<Button>("CancelStationButton"),"Cancelar el alta de estación");
        if(organization.NewStation||organization.SelectedStation?.Id!="sala-1") throw new Exception("Cancelling the new station returns to the station chosen before.");
        Press(InRooms<Button>("NewStationButton"),"Nueva estación");
        organization.StationId="barra"; organization.StationName="Barra"; stationKindBox.SelectedValue="bar"; window.UpdateLayout();
        if(organization.StationKind!="bar"||!organization.StationKindHelp.StartsWith("Barra:",StringComparison.Ordinal)) throw new Exception("The kind box writes its code and explains it.");
        SendRooms("SaveStationButton","Crear estación");
        if(CatalogPost()!="organization/commands/station-create {\"id\":\"barra\",\"name\":\"Barra\",\"kind\":\"bar\",\"sort\":5}"||organization.SelectedStation?.Id!="barra"||organization.StationItems.Length!=6
            ||stationList.SelectedItem is not Costina.Desktop.ViewModels.StationRow{Station.Id:"barra"}||organization.StationContext!="Estación · Barra") throw new Exception("A created station is chosen once the server confirms it: "+CatalogPost());
        window.Shell.Busy=true;
        if(organization.StartTableCommand.CanExecute(null)||organization.SaveStationCommand.CanExecute(null)||organization.ToggleStationCommand.CanExecute(null)) throw new Exception("Busy gates the organization commands.");
        window.Shell.Busy=false;
        // Las capturas, con la organizacion de ejemplo otra vez (sin lo creado arriba): Estaciones con la Cocina caliente elegida
        // (configuration-stations.png) y Salas y mesas con la Sala principal y su ficha (configuration.png, del bucle de capturas).
        server.Organization=roomsOrganization;
        OpenRooms("the organization is read again for the captures");
        PickStation("hot");
        RoomsFit("configuration-stations","SaveStationButton"); Save(Shot(window),"configuration-stations.png");
        roomTabs.SelectedIndex=0; window.UpdateLayout();
        PickZone("sala");
        if(organization.SelectedTable is not null) Press(InRooms<Button>("ShowZoneButton"),"Ver la sala");
        if(!RoomShows("ZoneDetail")||organization.SelectedZone?.Id!="sala"||organization.TableItems.Length!=9) throw new Exception("The capture shows the Sala principal with its ficha and its tables.");
        RoomsFit("configuration","SaveZoneButton");
        // ===== C2b (plan 2026-10-14): Dispositivos, retoques =====
        // La cabecera de las vistas de gestion (titulo y resumen de una linea), los dispositivos en tarjetas como en Inicio (tipo y estacion
        // por su nombre, el punto y la palabra de la conexion, quien lo aprobo y cuando), el vacio explicado con su boton de conectar y quitar
        // en dos pasos abajo, con el nombre de lo que se quita. Datos coherentes con Inicio: sus cinco dispositivos (y el quitado) en las
        // estaciones de la organizacion de C2. Elegir una tarjeta no lee ni envia nada; quitar envia la orden de siempre del elegido (F01).
        T InDevices<T>(string name) where T:FrameworkElement=>(T)devicesPanel.FindName(name);
        bool DeviceShows(string name)=>InDevices<FrameworkElement>(name).IsVisible;
        bool DeviceReady(string name)=>InDevices<Button>(name) is {IsVisible:true,IsEnabled:true};
        var deviceList=InDevices<ListBox>("DeviceList");
        void OpenDevices(string what)
        {
            var before=server.Count("GET","auth/devices");
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); tabs.SelectedItem=(TabItem)window.FindName("DevicesTab"); window.UpdateLayout();
            Wait(()=>!window.Shell.Busy&&server.Count("GET","auth/devices")>before,what); window.UpdateLayout();
        }
        string DeviceRowsText()=>string.Join(" | ",center.Rows.Select(r=>$"{r.Name}: {r.Kind} · {r.State} ({r.PresenceDot})"));
        ListBoxItem DeviceCard(string id){ var row=center.Rows.Single(r=>r.Device.Id==id); deviceList.ScrollIntoView(row); window.UpdateLayout(); return (ListBoxItem)deviceList.ItemContainerGenerator.ContainerFromItem(row); }   // la lista virtualiza
        void PickDevice(string id){ deviceList.SelectedItem=center.Rows.Single(r=>r.Device.Id==id); window.UpdateLayout(); }   // como pulsar la tarjeta
        void DevicesFit(string what)
        {
            window.Shell.Status=""; window.UpdateLayout();
            window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
            var pane=InDevices<FrameworkElement>("DevicesPane");
            if(pane.TranslatePoint(new Point(0,pane.ActualHeight),devicesPanel).Y>devicesPanel.ActualHeight+0.5) throw new Exception(what+": the device list ends inside the view.");
            if(DeviceShows("RemoveButton")&&InDevices<Button>("RemoveButton") is var remove&&remove.TranslatePoint(new Point(0,remove.ActualHeight),devicesPanel).Y>devicesPanel.ActualHeight+0.5)
                throw new Exception(what+": the remove button stays in view.");
            if(Math.Abs(pane.ActualWidth-InDevices<FrameworkElement>("ConnectPane").ActualWidth)>0.5) throw new Exception(what+": connecting and the list share the width.");
            if(Clipped(devicesPanel) is {Length:>0} clipped) throw new Exception(what+" clips text: "+string.Join(" | ",clipped));
            if(NestedScrolls(devicesPanel) is {Length:>0} nested) throw new Exception(what+" nests scroll viewers: "+string.Join(", ",nested));
            RefuseRaw(window,what);
        }
        // Sin dispositivos (una instalacion recien hecha): la lista explica que falta y ofrece conectar un comandero en la estacion de sala
        // elegida a la izquierda (lo dice el boton); ni casilla de quitados ni boton de quitar.
        server.Organization=wineOrganization; server.Devices=[]; server.Network=SimpleOn; window.Shell.Status="";
        OpenDevices("Dispositivos reads an installation without devices");
        if(center.Summary!="Aún no hay dispositivos: conecta el primero con el asistente de «Conectar un dispositivo»."||!center.ShowListEmpty||center.ListEmptyHeading!="Aún no hay dispositivos"
            ||!DeviceShows("DevicesEmpty")||DeviceShows("DeviceList")||DeviceShows("RemoveBar")||DeviceShows("ShowRevokedBox")||DeviceShows("LegacyBar")||center.OnlineText.Length!=0
            ||!DeviceReady("EmptyConnectButton")||center.EmptyConnectText!="Conectar comandero · Sala 1"||!Painted(devicesPanel).Contains("Aún no hay dispositivos")
            ||!Painted(devicesPanel).Contains(center.Summary))
            throw new Exception("Without devices the list says what is missing and offers to connect a waiter in the chosen room station: "+center.Summary+" / "+center.ListEmptyHeading);
        DevicesFit("devices-empty"); Save(Shot(window),"devices-empty.png");
        // El boton del vacio es el de siempre: el asistente del comandero en la estacion elegida, con su codigo de un uso (una sola vez).
        var pairingsBeforeEmpty=server.Count("POST","auth/pairings");
        Press(InDevices<Button>("EmptyConnectButton"),"Conectar comandero (lista vacía)");
        Wait(()=>!window.Shell.Busy&&center.HasAssistant,"the empty list opens the waiter assistant");
        if(center.AssistantTitle!="Conectar comandero · Sala 1"||center.IntentStationId!="sala-1"||server.Count("POST","auth/pairings")!=pairingsBeforeEmpty+1||DeviceReady("EmptyConnectButton"))
            throw new Exception("The empty list's button opens the usual waiter assistant for the chosen station, once: "+center.AssistantTitle);
        DevicesFit("devices-assistant"); Save(Shot(window),"devices-assistant.png");
        center.CancelCommand.Execute(null); window.Shell.Status="";
        // Los dispositivos de Inicio: el resumen por tipo con el quitado, la conexion "4 de 5" como en Inicio y una tarjeta por dispositivo
        // activo en el orden de Inicio, con su tipo y su estacion por su nombre, el punto y la palabra, y quien lo aprobo y cuando.
        server.Devices=panelDevices;
        OpenDevices("Dispositivos reads the C2 devices");
        home.ApplyDevices(panelDevices);   // la lista de Inicio con los mismos dispositivos (pura), para comparar orden, tipos y estaciones
        if(center.Summary!="5 dispositivos activos: 2 comanderos y 3 pantallas de cocina · 1 quitado"||center.OnlineText!="4 de 5 con conexión"||center.SelectedRow is not null
            ||DeviceRowsText()!="tablet-sala-1: Comandero · Sala 1 · Conectado (ok) | tablet-terraza: Comandero · Terraza · Conectado (ok) | pantalla-caliente: Pantalla de cocina · Cocina caliente · Conectado (ok)"
                +" | pantalla-fria: Pantalla de cocina · Cocina fría · Sin conexión (off) | pantalla-pase: Pantalla de cocina · Pase · Conectado (ok)"
            ||!center.Rows.Select(r=>r.Name).SequenceEqual(home.DeviceRows.Select(r=>r.Name))||!center.Rows.Select(r=>r.Kind).SequenceEqual(home.DeviceRows.Select(r=>r.Kind)))
            throw new Exception("Dispositivos lists the devices of Inicio, in its order, with kind, station and presence in words: "+center.Summary+" / "+DeviceRowsText());
        var approvedSalaOne="Aprobado por jefa el "+panelDevices[0].CreatedAt.ToLocalTime().ToString("dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture);
        if(!new HashSet<string>(Painted(DeviceCard("h1"))).SetEquals(["tablet-sala-1","Comandero · Sala 1",approvedSalaOne,"Conectado"]))
            throw new Exception("A device card paints its name, kind and station, approver and date, and its connection in words: "+string.Join(" | ",Painted(DeviceCard("h1"))));
        if(!new HashSet<string>(Painted(DeviceCard("h4"))).SetEquals(["pantalla-fria","Pantalla de cocina · Cocina fría","Aprobado por jefa el "+panelDevices[3].CreatedAt.ToLocalTime().ToString("dd/MM/yyyy",System.Globalization.CultureInfo.InvariantCulture),"Sin conexión"])
            ||Descendants<System.Windows.Shapes.Ellipse>(DeviceCard("h4")).Single().Tag as string!="off"||Descendants<System.Windows.Shapes.Ellipse>(DeviceCard("h1")).Single().Tag as string!="ok")
            throw new Exception("The connection is a dot AND a word: "+string.Join(" | ",Painted(DeviceCard("h4"))));
        if(!DeviceShows("ShowRevokedBox")||InDevices<CheckBox>("ShowRevokedBox").Content as string!="Mostrar también los quitados (1)"||DeviceShows("LegacyBar")||DeviceShows("DevicesEmpty")
            ||!Painted(devicesPanel).Contains(center.Summary)||!Painted(devicesPanel).Contains("4 de 5 con conexión"))
            throw new Exception("The header says the summary, the list its connection count and the removed one is offered on demand.");
        // Sin nada elegido, el boton de quitar dice que elegir; elegir una tarjeta no lee ni envia nada y el boton nombra lo que quitaria.
        if(!DeviceShows("RemoveButton")||InDevices<Button>("RemoveButton").IsEnabled||center.RemoveText!="Elige un dispositivo para quitarlo") throw new Exception("With nothing chosen the remove button says what to pick: "+center.RemoveText);
        DevicesFit("devices-unselected");
        var requestsBeforeDevicePick=server.Requests.Length;
        PickDevice("h2");
        if(server.Requests.Length!=requestsBeforeDevicePick||center.SelectedRow?.Device.Id!="h2"||!DeviceReady("RemoveButton")||center.RemoveText!="Quitar «tablet-terraza»"
            ||deviceList.SelectedItem is not Costina.Desktop.ViewModels.DevicesViewModel.DeviceRow{Device.Id:"h2"}) throw new Exception("Choosing a card reads nothing and the remove button names it: "+center.RemoveText);
        DevicesFit("devices-chosen");
        // Quitar en dos pasos (F9): la confirmacion nombra el dispositivo; "No" no envia nada; "Si, quitar" envia la orden del ELEGIDO y, releido,
        // deja de verse (queda quitado, no borrado) y la eleccion se vacia.
        Press(InDevices<Button>("RemoveButton"),"Quitar «tablet-terraza»");
        if(!center.ConfirmingRemoval||!DeviceShows("ConfirmRemoveBar")||DeviceShows("RemoveButton")||!center.RemovalText.StartsWith("¿Quitar «tablet-terraza»?",StringComparison.Ordinal)||server.Requests.Length!=requestsBeforeDevicePick)
            throw new Exception("Removing asks first, naming the device: "+center.RemovalText);
        DevicesFit("devices-confirm"); Save(Shot(window),"devices-confirm.png");
        center.CancelRemoveCommand.Execute(null); window.UpdateLayout();
        if(center.ConfirmingRemoval||!DeviceReady("RemoveButton")||server.Requests.Length!=requestsBeforeDevicePick) throw new Exception("'No' leaves the device alone and sends nothing.");
        Press(InDevices<Button>("RemoveButton"),"Quitar «tablet-terraza»");
        var revokesBefore=server.Count("POST","auth/devices/");
        Press(InDevices<Button>("ConfirmRemoveButton"),"Sí, quitar");
        Wait(()=>!window.Shell.Busy&&server.Count("POST","auth/devices/")>revokesBefore,"the removal is sent"); window.UpdateLayout();
        if(server.Requests.Last(r=>r.Method=="POST") is not {Path:"auth/devices/h2/revoke",Body:"{}"}||server.Count("POST","auth/devices/")!=revokesBefore+1||center.Rows.Any(r=>r.Device.Id=="h2")
            ||center.SelectedRow is not null||center.Summary!="4 dispositivos activos: 1 comandero y 3 pantallas de cocina · 2 quitados"||center.RevokedToggleText!="Mostrar también los quitados (2)")
            throw new Exception("'Sí, quitar' sends the usual revoke of the chosen device and the re-read hides it: "+server.Requests.Last(r=>r.Method=="POST").Path+" / "+center.Summary);
        // Los quitados, a peticion: atenuados, con "Quitado el ..." en palabras y el punto gris; uno elegido no se puede quitar otra vez.
        InDevices<CheckBox>("ShowRevokedBox").IsChecked=true; window.UpdateLayout();
        if(center.Rows.Length!=6||center.Rows.Where(r=>r.Removed).Select(r=>r.Name).Order(StringComparer.Ordinal).ToArray() is not ["tablet-terraza","vieja"]
            ||center.Rows.Where(r=>r.Removed).Any(r=>r.PresenceDot!="off"||!r.State.StartsWith("Quitado el ",StringComparison.Ordinal))) throw new Exception("Removed devices appear on demand, in words: "+DeviceRowsText());
        PickDevice("h6");
        if(InDevices<Button>("RemoveButton").IsEnabled||center.RemoveText!="«vieja» ya está quitado"||!Painted(DeviceCard("h6")).Any(t=>t.StartsWith("Quitado el ",StringComparison.Ordinal)))
            throw new Exception("A removed device cannot be removed again and says so: "+center.RemoveText);
        DevicesFit("devices-removed"); Save(Shot(window),"devices-removed.png");
        InDevices<CheckBox>("ShowRevokedBox").IsChecked=false; window.UpdateLayout();
        if(center.SelectedRow is not null||center.Rows.Length!=4) throw new Exception("Hiding the removed ones drops a removed choice.");
        // Todos quitados: la lista vacia lo explica y ofrece mostrarlos (la casilla sigue ahi).
        center.Render(wineOrganization.Stations,[],[..panelDevices.Select(d=>d with {RevokedAt=d.RevokedAt??DateTimeOffset.Now})]); window.UpdateLayout();
        if(!center.ShowListEmpty||center.ListEmptyHeading!="Ningún dispositivo activo"||!center.ListEmptyText.Contains("«Mostrar también los quitados»",StringComparison.Ordinal)||!DeviceShows("ShowRevokedBox")
            ||center.Summary!="Ningún dispositivo activo · 6 quitados") throw new Exception("With every device removed the list explains how to see them: "+center.ListEmptyText+" / "+center.Summary);
        // Un dispositivo antiguo con rol principal: su tipo en palabras ("Principal (antiguo)", nunca "Responsable") y un aviso para cambiarlo.
        center.Render(wineOrganization.Stations,[],[..panelDevices,new("h7","pc-viejo","main","","user:jefa",DateTimeOffset.Now.AddDays(-30),null)]); window.UpdateLayout();
        if(center.Rows.Last().Kind!="Principal (antiguo)"||!DeviceShows("LegacyBar")||!center.LegacyWarning.StartsWith("1 dispositivo antiguo con rol principal: conviene quitarlo",StringComparison.Ordinal)
            ||center.Summary!="6 dispositivos activos: 2 comanderos, 3 pantallas de cocina y 1 principal (antiguo) · 1 quitado") throw new Exception("A legacy main device is named and warned about: "+center.Summary);
        DevicesFit("devices-legacy"); Save(Shot(window),"devices-legacy.png");
        // Sin leer (al salir de un nivel): nada se supone; ni resumen ni boton de conectar.
        center.Clear(); window.UpdateLayout();
        if(center.Summary.Length!=0||!center.ShowListEmpty||center.ListEmptyHeading!="Sin leer todavía"||DeviceShows("EmptyConnectButton")||DeviceShows("RemoveBar")) throw new Exception("Unread, the device list says so and offers nothing.");
        // La lectura de la captura: los dispositivos de Inicio con el comandero de la sala elegido (su boton de quitar lo nombra).
        server.Devices=panelDevices;
        OpenDevices("Dispositivos reads the C2 devices for the capture");
        PickDevice("h1");
        DevicesFit("devices");
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout();
        window.Shell.Api.Dispose(); window.Shell.Api=null;
        SynchronizationContext.SetSynchronizationContext(previousContext);
        // C2b: la captura de Caja es la del estado que deja la seccion de arriba (la Mesa 4 leida, con todas las tarjetas): sin datos a mano.
        // C2b: la de Dispositivos, tambien (los dispositivos de Inicio leidos, con tablet-sala-1 elegido).
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
            // C2b: Caja pinta la Mesa 4 leida (la de "Ver cuenta"), sus cifras y las demas tarjetas con su cobertura en palabras.
            if(tab=="CheckoutTab"&&new[]{"Mesa 4","Pago parcial","51,00 €","Pendiente 161,00 €","Pagado de más","Pagada","Registrar pago de prueba"}.FirstOrDefault(t=>!Texts(window).Contains(t)) is {} missingCaja)
                throw new Exception("Caja paints the read account, its figures and the coverage of every card in words: missing '"+missingCaja+"'.");
            // C2b: Oferta pinta la Degustacion Costina con sus seis pases numerados y el Pescado desplegado (la salsa, en el Pase).
            if(tab=="OffersTab"&&new[]{"Degustación Costiña","Menú degustación · Comida y cena · de martes a domingo","Pases · 6","Pescado","Platos del pase · 2","Rodaballo al horno","Salsa de vieiras","Pase",
                "Menú del día","Carta","Degustación de verano","Desactivado","Todos · 4"}.FirstOrDefault(t=>!Painted(window).Contains(t)) is {} missingOffers)
                throw new Exception("Oferta paints the chosen offer, its numbered courses and the unfolded one with its dishes: missing '"+missingOffers+"'.");
            // C2b: Salas pinta las dos salas (la tarifa propia de la Terraza), las mesas de la Sala principal (la 9, desactivada, con su palabra)
            // y la ficha de la sala con sus cifras.
            if(tab=="OrganizationTab"&&new[]{window.Shell.Organization.Sections[0].Label,window.Shell.Organization.Sections[1].Label,"Sala principal","8 mesas activas · 26 plazas","Terraza","Tarifa Terraza","Mesas de Sala principal","Mesa 1","Mesa 9","Desactivada",
                "Activa","Sala · tarifa General","Mesas activas","26","1 mesa desactivada: Mesa 9.","Guardar cambios","Desactivar sala"}.FirstOrDefault(t=>!Painted(window).Contains(t)) is {} missingRooms)
                throw new Exception("Salas paints both rooms, the chosen room's tables and its ficha in words: missing '"+missingRooms+"'.");
            // C2b: Dispositivos pinta el resumen, los dispositivos de Inicio con su tipo y estacion por su nombre y su conexion en palabras, y el
            // boton de quitar con el nombre del elegido.
            if(tab=="DevicesTab"&&new[]{"Dispositivos","5 dispositivos activos: 2 comanderos y 3 pantallas de cocina · 1 quitado","Dispositivos conectados","4 de 5 con conexión","tablet-sala-1","Comandero · Sala 1",
                "Comandero · Terraza","Pantalla de cocina · Cocina fría","Sin conexión","Conectado","Mostrar también los quitados (1)","Quitar «tablet-sala-1»","Conectar un dispositivo"}.FirstOrDefault(t=>!Painted(window).Contains(t)) is {} missingDevices)
                throw new Exception("Dispositivos paints the summary, the devices of Inicio in words and the remove button of the chosen one: missing '"+missingDevices+"'.");
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
        // ===== C2b: repaso de coherencia de las cinco vistas de gestion, y con el Comedor =====
        // La misma cabecera (el titulo y su resumen en el mismo sitio en todas), la columna de lista del mismo ancho (ListWidth) y la ficha
        // empezando en el mismo sitio en las vistas de lista y ficha; con la barra lateral desplegada y plegada (abajo), sin texto cortado,
        // sin desplazamientos anidados y sin valores en crudo. Inicio queda fuera a proposito: su cabecera es el saludo con la fecha (C2).
        if(window.FindResource("ListWidth") is not GridLength{Value:ListWidth}) throw new Exception("The list column width is ListWidth (360 px).");
        (string Tab,string View,string? List,string? Ficha)[] managed=[("CheckoutTab","CheckoutView","AccountsPane","AccountPanel"),("CatalogTab","CatalogView","ProductsPane","ProductPanel"),
            ("OffersTab","OffersView","OffersPane","OfferPanel"),("OrganizationTab","OrganizationView",null,null),("DevicesTab","DevicesView",null,null)];
        var pageTitle=(Style)window.FindResource("PageTitle"); var pageSummary=(Style)window.FindResource("PageSummary");
        string HeaderAt(FrameworkElement view)
        {
            var title=Descendants<TextBlock>(view).Single(t=>t.IsVisible&&ReferenceEquals(t.Style,pageTitle));
            var line=Descendants<TextBlock>(view).Where(t=>t.IsVisible&&ReferenceEquals(t.Style,pageSummary)).ToArray();
            var at=title.TranslatePoint(new Point(0,0),view); var below=line.Length==1?line[0].TranslatePoint(new Point(0,0),view):new Point(-1,-1);
            return $"{at.X:0},{at.Y:0} / {below.X:0},{below.Y:0} ({title.FontSize:0}/{(line.Length==1?line[0].FontSize:0):0})";
        }
        void SameLayout(string mode)
        {
            tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.UpdateLayout(); window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
            var serviceTitle=Descendants<TextBlock>(serviceView).Single(t=>t.IsVisible&&ReferenceEquals(t.Style,pageTitle)).TranslatePoint(new Point(0,0),serviceView);
            var comedorHeader=$"{serviceTitle.X:0},{serviceTitle.Y:0}";
            var headers=new List<string>(); var fichaLefts=new List<double>();
            foreach(var (tab,viewName,list,ficha) in managed)
            {
                tabs.SelectedItem=(TabItem)window.FindName(tab); window.UpdateLayout(); window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout();
                var view=(FrameworkElement)window.FindName(viewName);
                headers.Add(HeaderAt(view));
                if(!HeaderAt(view).StartsWith(comedorHeader+" ",StringComparison.Ordinal)) throw new Exception(mode+": "+viewName+" puts its title where Comedor does: "+HeaderAt(view)+" vs "+comedorHeader);
                if(list is not null&&Math.Abs(((FrameworkElement)view.FindName(list)).ActualWidth-ListWidth)>0.5) throw new Exception(mode+": "+viewName+" keeps its list at ListWidth.");
                if(ficha is not null) fichaLefts.Add(((FrameworkElement)view.FindName(ficha)).TranslatePoint(new Point(0,0),view).X);
                foreach(var card in Descendants<Border>(view).Where(b=>b.IsVisible&&b.Style is {} s&&(ReferenceEquals(s,window.FindResource("Card"))||ReferenceEquals(s,window.FindResource("ListPane"))||ReferenceEquals(s,window.FindResource("Ficha")))))
                    if(card.CornerRadius!=new CornerRadius(12)) throw new Exception(mode+": "+viewName+" draws its cards with radius 12.");
                // Por debajo de 1270 px (la pantalla del CI) la ventana pliega la barra por si sola: con la barra forzada abierta a ese ancho el
                // recorte no dice nada (el encaje a 1.280 x 860 lo comprueba un PC ancho; la barra plegada se comprueba en todos).
                if((window.ActualWidth>=1270||window.Shell.SideNavCompact)&&Clipped(view) is {Length:>0} clipped) throw new Exception(mode+": "+viewName+" clips text: "+string.Join(" | ",clipped));
                if(NestedScrolls(view) is {Length:>0} nested) throw new Exception(mode+": "+viewName+" nests scroll viewers: "+string.Join(", ",nested));
                RefuseRaw(window,mode+" "+viewName);
            }
            if(headers.Distinct().Count()!=1) throw new Exception(mode+": every management view has the same header (title and summary line): "+string.Join(" | ",headers));
            if(fichaLefts.Distinct().Count()!=1||Math.Abs(fichaLefts[0]-ListWidth-16)>0.5) throw new Exception(mode+": the ficha starts at the same place in Caja, Catálogo and Oferta: "+string.Join(", ",fichaLefts));
        }
        window.Shell.SideNavCompact=false; window.UpdateLayout();
        SameLayout("sidebar unfolded");
        // C1: la barra lateral se pliega a iconos (boton de abajo) y se despliega de nuevo; plegada, sus elementos siguen ahi con su ayuda.
        // (Se parte de la barra desplegada: en una pantalla estrecha, como la del CI, la ventana la pliega sola por debajo de 1270 px.)
        tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.Shell.SideNavCompact=false; window.UpdateLayout();
        var sideNavBar=(FrameworkElement)window.FindName("SideNavBar"); var navToggle=(Button)window.FindName("SideNavToggle");
        navToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); window.UpdateLayout();
        if(!window.Shell.SideNavCompact||sideNavBar.ActualWidth>64||((Button)window.FindName("NavCatalog")).ToolTip as string!="Catálogo y tarifas") throw new Exception("The sidebar folds to icons, each with its name as a tooltip: "+sideNavBar.ActualWidth);
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
        Save(Shot(window),"sidebar-compact.png");
        // C2b: plegada la barra, las vistas de gestion ganan ancho: la lista sigue en ListWidth, la ficha empieza donde siempre y nada se corta.
        SameLayout("sidebar folded");
        foreach(var (tab,file) in new[]{("CheckoutTab","sidebar-compact-checkout.png"),("OrganizationTab","sidebar-compact-configuration.png"),("DevicesTab","sidebar-compact-devices.png")})
        { tabs.SelectedItem=(TabItem)window.FindName(tab); window.UpdateLayout(); window.Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle); window.UpdateLayout(); Save(Shot(window),file); }
        tabs.SelectedItem=(TabItem)window.FindName("HomeTab"); window.UpdateLayout();
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

    // C2b (repaso de coherencia): ancho de la columna de lista de las vistas de lista y ficha (Components.xaml, ListWidth).
    private const double ListWidth=360;
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
    // C2b: el texto que de verdad se ve (TextBlock visible) bajo un elemento: una palabra de un chip oculto no cuenta.
    private static IEnumerable<string> Painted(DependencyObject root) => Descendants<TextBlock>(root).Where(t=>t.IsVisible&&t.Text.Length>0).Select(t=>t.Text);
    // C2b: textos de una linea (sin partir) que no se ven enteros: no caben en su sitio (cortados o con puntos suspensivos) o un contenedor
    // los recorta (su recorte de diseno, LayoutInformation, no deja ver el texto a lo ancho: una nota con ancho maximo, una columna
    // estrecha). Se mide el texto con su letra y se situa segun su alineacion; un texto que se parte nunca se corta.
    private static string[] Clipped(FrameworkElement root) => Descendants<TextBlock>(root)
        .Where(t=>t.IsVisible&&t.TextWrapping==TextWrapping.NoWrap&&t.Text.Length>0&&IsClipped(t,root)).Select(t=>t.Text).ToArray();
    private static bool IsClipped(TextBlock text,FrameworkElement root)
    {
        var width=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentCulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),
            text.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(text).PixelsPerDip).WidthIncludingTrailingWhitespace;
        if(width>text.ActualWidth+1) return true;
        var left=text.TextAlignment switch{TextAlignment.Right=>text.ActualWidth-width,TextAlignment.Center=>(text.ActualWidth-width)/2,_=>0};
        var span=new Rect(left,0,width,Math.Max(text.ActualHeight,1));
        for(DependencyObject? node=text;node is not null;node=VisualTreeHelper.GetParent(node))
        {
            if(node is FrameworkElement element&&System.Windows.Controls.Primitives.LayoutInformation.GetLayoutClip(element) is {} clip)
            {
                var box=ReferenceEquals(element,text)?span:text.TransformToAncestor(element).TransformBounds(span);
                if(box.Left<clip.Bounds.Left-1||box.Right>clip.Bounds.Right+1) return true;
            }
            if(ReferenceEquals(node,root)) break;
        }
        return false;
    }
    // C2b: desplazamientos anidados bajo una vista (sin contar el de dentro de cada campo de texto): cada columna desplaza una sola vez.
    private static string[] NestedScrolls(FrameworkElement root)
    {
        static bool Counts(ScrollViewer s)=>s.IsVisible&&s.TemplatedParent is not System.Windows.Controls.Primitives.TextBoxBase;
        bool Nested(DependencyObject node)
        {
            for(var parent=VisualTreeHelper.GetParent(node);parent is not null&&!ReferenceEquals(parent,root);parent=VisualTreeHelper.GetParent(parent))
                if(parent is ScrollViewer outer&&Counts(outer)) return true;
            return false;
        }
        return Descendants<ScrollViewer>(root).Where(Counts).Where(s=>Nested(s)).Select(s=>(s.TemplatedParent as FrameworkElement)?.Name is {Length:>0} owner?owner:s.Name).ToArray();
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
    // D6.7: motor simulado con la idempotencia POR ACTOR, como PostgresStore: una orden se recuerda por (actor, clave), el reintento del
    // mismo actor devuelve la respuesta guardada y GET commands/{clave} solo encuentra las del actor que pregunta. Cada credencial es un
    // actor. Applied cuenta los efectos; LoseNextResponse aplica la siguiente orden nueva y pierde su respuesta (red cortada).
    private sealed class ActorServer(Dictionary<string,string> actors) : System.Net.Http.HttpMessageHandler
    {
        private readonly Dictionary<(string Actor,string Key),string> done=[];
        private readonly List<string> requests=[];
        public string[] Requests { get { lock(requests) return [..requests]; } }
        public int Applied { get; private set; }
        public bool LoseNextResponse { get; set; }
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken ct)
        {
            var actor=actors[request.Headers.Authorization!.Parameter!];
            var path=request.RequestUri!.AbsolutePath["/api/native/v1/".Length..];
            lock(requests) requests.Add(request.Method.Method+" "+actor+" "+path);
            if(request.Method==System.Net.Http.HttpMethod.Get&&path.StartsWith("commands/",StringComparison.Ordinal))
            {
                var asked=Uri.UnescapeDataString(path["commands/".Length..]);
                return Task.FromResult(Json(200,done.TryGetValue((actor,asked),out var stored)?"{\"key\":\""+asked+"\",\"found\":true,\"response\":"+stored+"}":"{\"key\":\""+asked+"\",\"found\":false}"));
            }
            var key=request.Headers.GetValues("Idempotency-Key").Single();
            if(!done.TryGetValue((actor,key),out var response))
            {
                Applied++; response="{\"changed\":true}"; done[(actor,key)]=response;
                if(LoseNextResponse) { LoseNextResponse=false; throw new System.Net.Http.HttpRequestException("respuesta perdida"); }
            }
            var ok=Json(200,response); ok.Headers.Add("Idempotency-Key",key);
            return Task.FromResult(ok);
        }
        private static System.Net.Http.HttpResponseMessage Json(int status,string json)=>new((System.Net.HttpStatusCode)status){Content=new System.Net.Http.StringContent(json,System.Text.Encoding.UTF8,"application/json")};
    }

    private sealed record Request(string Method,string Path,string Body,string? Key);
    private sealed class FakeServer(Costina.Client.CatalogDto catalog,Costina.Client.OrganizationDto organization) : System.Net.Http.HttpMessageHandler
    {
        public Costina.Client.CatalogDto Catalog { get; set; }=catalog;
        public Costina.Client.OrganizationDto Organization { get; set; }=organization;
        public Costina.Client.Configuration? Configuration { get; set; }
        public Costina.Client.BoardEntry[] Board { get; set; }=[];
        public Costina.Client.AccountChoice[] Accounts { get; set; }=[];
        // C2b: la cuenta completa de cada servicio listado (sin ella, una cuenta abierta vacia) y el catalogo de caja (vendibles con precio).
        public System.Collections.Concurrent.ConcurrentDictionary<string,Costina.Client.AccountDto> AccountDetails { get; }=new();
        public Costina.Client.ProductChoice[] CheckoutCatalog { get; set; }=[];
        // C2b: las ofertas completas (GET erp/offers, solo del responsable).
        public Costina.Client.OfferDto[] Offers { get; set; }=[];
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
                if(path=="catalog") return Json(200,JsonSerializer.Serialize(Sellables,Costina.Client.ApiClient.Json));   // C3b: el catalogo operativo, SIN precios
                if(path=="organization") return Json(200,JsonSerializer.Serialize(Organization,Costina.Client.ApiClient.Json));
                if(path=="configuration"&&Configuration is not null) return Json(200,JsonSerializer.Serialize(Configuration,Costina.Client.ApiClient.Json));
                if(path=="dining/board") return Json(200,JsonSerializer.Serialize(Board,Costina.Client.ApiClient.Json));
                if(path.StartsWith("dining/services/",StringComparison.Ordinal)&&Board.FirstOrDefault(b=>b.Service.Id==Uri.UnescapeDataString(path["dining/services/".Length..])) is {} entry)
                    return Json(200,JsonSerializer.Serialize(new Costina.Client.Versioned<Costina.Client.DiningDto>(entry.Version,entry.Service),Costina.Client.ApiClient.Json));
                if(path=="checkout/accounts") return Json(200,JsonSerializer.Serialize(Accounts,Costina.Client.ApiClient.Json));
                if(path=="checkout/catalog") return Json(200,JsonSerializer.Serialize(CheckoutCatalog,Costina.Client.ApiClient.Json));
                if(path=="erp/offers") return Json(200,JsonSerializer.Serialize(Offers,Costina.Client.ApiClient.Json));
                if(path=="diagnostics"&&Diagnostics is not null) return Json(200,JsonSerializer.Serialize(Diagnostics,Costina.Client.ApiClient.Json));
                if(path=="auth/devices") return Json(200,JsonSerializer.Serialize(Devices,Costina.Client.ApiClient.Json));
                if(path=="auth/pairings/pending") return Json(200,"[]");
                if(path=="installation/network"&&Network is not null) return Json(200,JsonSerializer.Serialize(Network,Costina.Client.ApiClient.Json));
                // C2b: tras un rechazo definitivo el cliente pregunta por su clave: no consta (el motor no la aplico).
                if(path.StartsWith("commands/",StringComparison.Ordinal)) return Json(200,JsonSerializer.Serialize(new Costina.Client.CommandLookup(Uri.UnescapeDataString(path["commands/".Length..]),false),Costina.Client.ApiClient.Json));
                if(path.StartsWith("checkout/services/",StringComparison.Ordinal)&&Accounts.FirstOrDefault(a=>a.ServiceId==Uri.UnescapeDataString(path["checkout/services/".Length..])) is {} account)
                    return Json(200,JsonSerializer.Serialize(new Costina.Client.Versioned<Costina.Client.AccountDto>(1,AccountDetails.TryGetValue(account.ServiceId,out var detail)?detail
                        :new(account.ServiceId+"-cuenta",account.ServiceId,"Open",0,0,0,0,"Unpaid",[],[],["add-product","payment"],[])),Costina.Client.ApiClient.Json));
                return Json(404,"{\"error\":\"not_found\"}");
            }
            if(path=="auth/pairings") return Json(200,JsonSerializer.Serialize(new{pairingId="pid-home",code="Home12_abc-def",expiresAt=DateTimeOffset.UtcNow.AddMinutes(5)}));
            // C2b: quitar un dispositivo se aplica en memoria, como el motor: queda con su fecha de quitado y sin conexion (nada se borra).
            if(path.StartsWith("auth/devices/",StringComparison.Ordinal)&&path.EndsWith("/revoke",StringComparison.Ordinal))
            {
                var id=Uri.UnescapeDataString(path["auth/devices/".Length..^"/revoke".Length]);
                Devices=[..Devices.Select(d=>d.Id==id&&d.RevokedAt is null?d with {RevokedAt=DateTimeOffset.Now,Online=false}:d)];
                return Json(200,"{\"revoked\":true}");
            }
            var command=JsonSerializer.Deserialize<JsonElement>(body);
            // C3b: la ronda (add-consumptions) como el motor: idempotente por clave (la misma clave devuelve lo guardado sin cobrar otra vez),
            // exige la version leida de la mesa (si no, 409 version_conflict con el eco de la clave: rechazo definitivo) y no la mueve; cada
            // linea entra en lo que lleva la mesa (nombre y formato del catalogo, sin el formato por defecto, comensal, hora). LoseNextRound
            // aplica la siguiente ronda nueva y pierde su respuesta (red cortada). RoundsApplied cuenta los efectos.
            if(path.StartsWith("dining/services/",StringComparison.Ordinal)&&path.EndsWith("/commands/add-consumptions",StringComparison.Ordinal))
            {
                if(Rounds.TryGetValue(key!,out var stored)) { var again=Json(200,stored); again.Headers.Add("Idempotency-Key",key); return again; }
                var serviceId=Uri.UnescapeDataString(path["dining/services/".Length..^"/commands/add-consumptions".Length]);
                var table=Board.Single(b=>b.Service.Id==serviceId);
                if(command.GetProperty("expectedVersion").GetInt64()!=table.Version) { var refused=Json(409,"{\"error\":\"version_conflict\"}"); refused.Headers.Add("Idempotency-Key",key); return refused; }
                var added=command.GetProperty("lines").EnumerateArray().Select((line,index)=>
                {
                    var productId=line.GetProperty("productId").GetString()!; var presentationId=line.GetProperty("presentationId").GetString()!;
                    var sellable=Sellables.Single(s=>s.Id==productId&&s.PresentationId==presentationId);
                    return new Costina.Client.ConsumptionDto(key+"-"+index,productId,presentationId,sellable.Name,presentationId=="unit"?null:sellable.Presentation,line.GetProperty("quantity").GetInt32(),
                        line.GetProperty("guestPosition").ValueKind==JsonValueKind.Number?line.GetProperty("guestPosition").GetInt32():null,RoundClock,false);
                }).ToArray();
                Board=[..Board.Select(b=>b.Service.Id==serviceId?b with {Consumptions=[..b.Consumptions??[],..added]}:b)];
                RoundsApplied++;
                var response="{\"version\":"+table.Version+",\"consumptionIds\":["+string.Join(",",added.Select(a=>"\""+a.Id+"\""))+"]}";
                Rounds[key!]=response;
                if(LoseNextRound) { LoseNextRound=false; throw new System.Net.Http.HttpRequestException("respuesta perdida"); }
                var applied=Json(200,response); applied.Headers.Add("Idempotency-Key",key); return applied;
            }
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
            // E3.3.1: Vinos por copa como el motor: hasta 300 cambios EN TOTAL por orden (items + stop; mas, 409 too_many_items con el eco
            // de la clave: rechazo definitivo, y la consulta de la clave dice que no consta); la copa del vino es la activa ('copa' antes
            // que 'glass') y quitar desactiva todas. RejectGlassOrder rechaza la orden glass-sale-set con ese numero (product_unavailable)
            // y UncertainGlassOrder la deja sin respuesta reconocible (503 sin aplicar: queda sin confirmar); un reintento lleva otro numero.
            if(path=="erp/catalog/commands/glass-sale-set")
            {
                var items=command.GetProperty("items").EnumerateArray().Select(i=>(Id:i.GetProperty("productId").GetString()!,Cents:i.GetProperty("priceCents").GetInt64())).ToArray();
                var stop=command.GetProperty("stop").EnumerateArray().Select(s=>s.GetString()!).ToArray();
                GlassOrders++;
                if(GlassOrders==UncertainGlassOrder) return Json(503,"{\"error\":\"unavailable\"}");
                if((items.Length+stop.Length>300?"too_many_items":GlassOrders==RejectGlassOrder?"product_unavailable":null) is {} refusal)
                {
                    var refused=Json(409,"{\"error\":\""+refusal+"\"}"); refused.Headers.Add("Idempotency-Key",key); return refused;
                }
                Catalog=Catalog with {Products=[..Catalog.Products.Select(p=>GlassSale(p,items.Where(i=>i.Id==p.Id).Select(i=>(long?)i.Cents).LastOrDefault(),stop.Contains(p.Id)))]};
            }
            // C2b: un alta de tarifa se aplica en memoria, como el motor, para comprobar que lo recien creado queda elegido al releer.
            if(path=="erp/catalog/commands/tariff-create")
                Catalog=Catalog with {Tariffs=[..Catalog.Tariffs,new(command.GetProperty("id").GetString()!,command.GetProperty("name").GetString()!,command.GetProperty("sort").GetInt32(),true)]};
            // C2b: las altas de la Oferta (oferta, pase, plato y plato de la carta) tambien, con lo que el motor guardaria: un menu nuevo con
            // su producto-menu "menu-<codigo>", una carta sin el; el plato de la carta con el nombre, la presentacion y la estacion de su producto.
            if(path.StartsWith("erp/offers/commands/",StringComparison.Ordinal))
            {
                string Text(string name)=>command.GetProperty(name).GetString()!;
                string? Date(string name)=>Text(name) is {Length:>0} date?date:null;
                Costina.Client.OfferDto[] With(string offerId,Func<Costina.Client.OfferDto,Costina.Client.OfferDto> change)=>[..Offers.Select(o=>o.Id==offerId?change(o):o)];
                Costina.Client.OfferCourseDto[] InCourse(Costina.Client.OfferDto o,Func<Costina.Client.OfferCourseDto,Costina.Client.OfferCourseDto> change)=>[..o.Courses.Select(c=>c.Id==Text("courseId")?change(c):c)];
                switch(path["erp/offers/commands/".Length..])
                {
                    case "offer-create":
                        Offers=[..Offers,new(Text("id"),Text("name"),Text("kind"),Text("kind")=="a-la-carte"?null:"menu-"+Text("id").ToLowerInvariant(),Text("service"),Date("validFrom"),Date("validTo"),
                            command.GetProperty("weekdays").GetInt32(),command.GetProperty("sort").GetInt32(),true,[])];
                        break;
                    case "course-create":
                        Offers=With(Text("offerId"),o=>o with {Courses=[..o.Courses,new(Text("id"),Text("name"),command.TryGetProperty("sort",out var cs)?cs.GetInt32():o.Courses.Length*10,true,[],o.Kind=="a-la-carte"?[]:null)]});
                        break;
                    case "dish-create":
                        Offers=With(Text("offerId"),o=>o with {Courses=InCourse(o,c=>c with {Dishes=[..c.Dishes,new(o.Id,c.Id,Text("id"),Text("name"),Text("stationId"),Text("productId") is {Length:>0} p?p:null,command.TryGetProperty("sort",out var ds)?ds.GetInt32():c.Dishes.Length*10,true)]})});
                        break;
                    case "item-create":
                        var product=Catalog.Products.Single(p=>p.Id==Text("productId")); var presentation=product.Presentations.Single(s=>s.Id==Text("presentationId"));
                        Offers=With(Text("offerId"),o=>o with {Courses=InCourse(o,c=>c with {Items=[..c.Items??[],new(product.Id,presentation.Id,product.Name,presentation.Name,product.StationId,command.GetProperty("sort").GetInt32(),true)]})});
                        break;
                }
            }
            // C2b: las ordenes de la organizacion se aplican en memoria, como el motor, con sus dos reglas de desactivar: una sala con mesas
            // activas (zone_in_use) y una mesa en uso (table_in_use: las de TablesInUse, las ocupadas del tablero). El rechazo es definitivo:
            // 409 con su codigo y el eco de la clave (y GET commands/{clave} dice que no consta), como el del motor.
            if(path.StartsWith("organization/commands/",StringComparison.Ordinal)&&OrganizationCommand(path["organization/commands/".Length..],command) is {} rejected)
            {
                var refused=Json(409,"{\"error\":\""+rejected+"\"}"); refused.Headers.Add("Idempotency-Key",key); return refused;
            }
            var ok=Json(200,"{\"changed\":true}"); ok.Headers.Add("Idempotency-Key",key); return ok;
        }
        public HashSet<string> TablesInUse { get; }=[];
        // C3b: el catalogo operativo (GET /catalog, sin precios), las rondas aplicadas por clave y la hora con que se anotan.
        public Costina.Client.CatalogItem[] Sellables { get; set; }=[];
        public System.Collections.Concurrent.ConcurrentDictionary<string,string> Rounds { get; }=new();
        public int RoundsApplied { get; private set; }
        public bool LoseNextRound { get; set; }
        public DateTimeOffset RoundClock { get; set; }=DateTimeOffset.UtcNow;
        public int GlassOrders { get; private set; }
        public int RejectGlassOrder { get; set; }
        public int UncertainGlassOrder { get; set; }
        // E3.3.1: glass-sale-set sobre un producto, como CatalogOperations: items reprecia (General, desde hoy) y reactiva la copa que
        // elige Catalog.GlassOf (la activa; entre varias, 'copa') o crea 'copa'; stop desactiva todas las copas activas.
        private Costina.Client.ProductDto GlassSale(Costina.Client.ProductDto product,long? cents,bool stop)
        {
            static bool IsGlass(Costina.Client.PresentationDto s)=>s.Glass||s.Id is "copa" or "glass";
            if(cents is null&&!stop) return product;
            var presentations=product.Presentations;
            if(cents is {} price)
            {
                var glass=presentations.Where(IsGlass).OrderByDescending(s=>s.Active).ThenBy(s=>s.Id=="copa"?0:1).ThenBy(s=>s.Id,StringComparer.Ordinal).FirstOrDefault()
                    ??new("copa","Copa",0,true,[],true);
                var today=Catalog.Today;
                presentations=[..presentations.Where(s=>s.Id!=glass.Id),glass with {Active=true,Prices=[..glass.Prices.Where(p=>!(p.TariffId=="general"&&p.ValidFrom==today)),new("general",today,price)]}];
            }
            if(stop) presentations=[..presentations.Select(s=>IsGlass(s)&&s.Active?s with {Active=false}:s)];
            return product with {Presentations=[..presentations.OrderBy(s=>s.Sort).ThenBy(s=>s.Id,StringComparer.Ordinal)]};
        }
        private string? OrganizationCommand(string action,JsonElement command)
        {
            string Text(string name)=>command.GetProperty(name).GetString()!;
            int Number(string name)=>command.GetProperty(name).GetInt32();
            string? Tariff()=>command.TryGetProperty("tariffId",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString():null;   // null = sin cambio
            var id=Text("id"); var zones=Organization.Zones; var stations=Organization.Stations;
            Costina.Client.TableDto? table=zones.SelectMany(z=>z.Tables).FirstOrDefault(t=>t.Id==id);
            Costina.Client.ZoneDto[] Tables(Func<Costina.Client.TableDto,Costina.Client.TableDto?> change)=>[..zones.Select(z=>z with {Tables=[..z.Tables.Select(change).OfType<Costina.Client.TableDto>()]})];
            static string Kind(string kind)=>kind switch{"kitchen"=>"Kitchen","pass"=>"Pass","bar"=>"Bar","room"=>"Room",_=>kind};
            switch(action)
            {
                case "zone-create":
                    zones=[..zones,new(id,Text("name"),Number("sort"),true,[],Tariff() is {Length:>0} own?own:null)];
                    break;
                case "zone-update":   // tariffId: null = sin cambio, "" = la General
                    zones=[..zones.Select(z=>z.Id!=id?z:z with {Name=Text("name"),Sort=Number("sort"),TariffId=Tariff() is {} t?(t.Length==0?null:t):z.TariffId})];
                    break;
                case "zone-deactivate" or "zone-reactivate":
                    if(action=="zone-deactivate"&&zones.Single(z=>z.Id==id).Tables.Any(t=>t.Active)) return "zone_in_use";
                    zones=[..zones.Select(z=>z.Id==id?z with {Active=action=="zone-reactivate"}:z)];
                    break;
                case "table-create" or "table-update":   // la mesa va a su sala (una mesa movida sale de la anterior), por orden y codigo
                    var moved=new Costina.Client.TableDto(id,Text("name"),Number("capacity"),Text("zoneId"),Number("sort"),table?.Active??true);
                    zones=[..Tables(t=>t.Id==id?null:t).Select(z=>z.Id!=moved.ZoneId?z:z with {Tables=[..z.Tables.Append(moved).OrderBy(t=>t.Sort).ThenBy(t=>t.Id,StringComparer.Ordinal)]})];
                    break;
                case "table-deactivate" or "table-reactivate":
                    if(action=="table-deactivate"&&TablesInUse.Contains(id)) return "table_in_use";
                    zones=Tables(t=>t.Id==id?t with {Active=action=="table-reactivate"}:t);
                    break;
                case "station-create":
                    stations=[..stations,new(id,Text("name"),Kind(Text("kind")),Number("sort"),true)];
                    break;
                case "station-update":
                    stations=[..stations.Select(s=>s.Id!=id?s:s with {Name=Text("name"),Kind=Kind(Text("kind")),Sort=Number("sort")})];
                    break;
                case "station-deactivate" or "station-reactivate":
                    if(id=="pase") return "reserved_station";
                    stations=[..stations.Select(s=>s.Id==id?s with {Active=action=="station-reactivate"}:s)];
                    break;
            }
            Organization=Organization with {Zones=zones,Stations=stations};
            return null;
        }
        private static System.Net.Http.HttpResponseMessage Json(int status,string json)=>new((System.Net.HttpStatusCode)status){Content=new System.Net.Http.StringContent(json,System.Text.Encoding.UTF8,"application/json")};
    }
}
