using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Costina.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app=new App(); app.InitializeComponent();
        var window=new MainWindow(); window.Show(); window.UpdateLayout();
        if(!((Button)window.FindName("ConnectButton")).IsEnabled) throw new Exception("Connection button must be available.");
        if(((TabItem)window.FindName("CheckoutTab")).Visibility!=Visibility.Collapsed) throw new Exception("Checkout visible without authenticated role.");
        if(((TabItem)window.FindName("OrganizationTab")).Visibility!=Visibility.Collapsed) throw new Exception("Configuration visible without authenticated role.");   // E2
        if(((TabControl)window.FindName("Tabs")).IsEnabled) throw new Exception("Unauthenticated operations enabled.");
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
        var deviceFile=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Costina","device-59998.bin");
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
        if(again.Devices.DeviceAddress!="https://costina-server.local:5443") throw new Exception("On loopback the last device address used on this machine is proposed.");
        var lan=new Costina.Desktop.ViewModels.ShellViewModel(); lan.Devices.MemoryFolder=qrFolder; lan.Endpoint="https://otro-servidor.local:5443"; lan.Devices.SuggestAddress();
        if(lan.Devices.DeviceAddress!="https://otro-servidor.local:5443") throw new Exception("A client already on the LAN proposes its own server address.");
        Directory.Delete(qrFolder,true);
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
        // Capturas del tema visual (E2): cada pestana del puesto principal compuesta con datos leidos.
        var tabs=(TabControl)window.FindName("Tabs");
        window.Shell.Service.Board=[entry]; window.Shell.Service.SelectedEntry=entry; window.Shell.Service.Dining=new(3,dto);
        window.Shell.Service.Tables=[new("M1","Mesa 1",12,"sala","Sala"),new("M2","Mesa 2",4,"sala","Sala")]; window.Shell.Service.SelectedTable=window.Shell.Service.Tables[0];
        window.Shell.Service.Courses=[new Costina.Client.CourseDto("c1","Aperitivos","Ready",null,null,null,null,[prep],["serve"])]; window.Shell.Service.SelectedCourse=window.Shell.Service.Courses[0];
        window.Shell.Service.Preparations=[prep]; window.Shell.Service.ServiceTitle="Mesa M1 · 2 personas · En servicio";
        window.Shell.Checkout.Products=[new Costina.Client.ProductChoice("water","Agua mineral","Botella",400)]; window.Shell.Checkout.SelectedProduct=window.Shell.Checkout.Products[0];
        window.Shell.Checkout.Accounts=[new Costina.Client.AccountChoice("s1","M1","Open")]; window.Shell.Checkout.SelectedAccount=window.Shell.Checkout.Accounts[0];
        window.Shell.Checkout.Charges=[new("ch1","Menú de ensayo",2,15000,false,null,30000),new("ch2","Agua mineral · Botella",1,400,false,null,400)];
        window.Shell.Checkout.Totals="Total 304,00 € · Pagado 0,00 € · Pendiente 304,00 €";
        window.Shell.Status="Datos leídos del servidor. Las acciones se habilitan según lo que el servidor anuncia.";
        window.Shell.Devices.DeviceAddress="https://costina-server.local:5443"; window.Shell.Devices.PairingCode="AbC123_def-456";
        window.Shell.Devices.Stations=sampleOrganization.Stations; window.Shell.Devices.ApproveStation="cold";
        window.Shell.Devices.Devices=[new("d1","tablet-sala-1","service","sala-1","user:jefa",DateTimeOffset.Now.AddDays(-3),null)];
        foreach(var (tab,file) in new[]{("ServiceTab","service.png"),("CheckoutTab","checkout.png"),("OrganizationTab","configuration.png"),("DevicesTab","devices.png")})
        {
            tabs.SelectedItem=(TabItem)window.FindName(tab); window.UpdateLayout();
            // Las columnas "*" del DataGrid se reparten en una pasada diferida: se vacia la cola del despachador antes de capturar.
            window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle); window.UpdateLayout();
            var shot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); shot.Render(window);
            var shotEncoder=new PngBitmapEncoder(); shotEncoder.Frames.Add(BitmapFrame.Create(shot));
            using var shotFile=File.Create("artifacts/desktop/"+file); shotEncoder.Save(shotFile);
        }
        tabs.SelectedItem=(TabItem)window.FindName("ServiceTab"); window.Shell.Session=null; window.UpdateLayout();
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        image.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var file=File.Create("artifacts/desktop/startup.png")) encoder.Save(file);
        File.WriteAllText("artifacts/desktop/window-checks.txt","Startup, viewmodel affordance/context-mapping and durable-store isolation checks passed. Actual WPF window rendered. No live backend interaction claimed by these checks.");
        window.Close();app.Shutdown();Console.WriteLine("WPF startup and viewmodel checks passed");return 0;
    }
}
