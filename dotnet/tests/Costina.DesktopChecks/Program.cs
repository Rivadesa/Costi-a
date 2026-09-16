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
        if(((TabControl)window.FindName("Tabs")).IsEnabled) throw new Exception("Unauthenticated operations enabled.");
        // D3.1: la habilitacion es un mapeo puro de las affordances del servidor, sin reglas locales.
        var shell=new Costina.Desktop.ViewModels.ShellViewModel();
        shell.Session=new("service","t","c","l",["open"]);
        var prep=new Costina.Client.PreparationDto("i1","Plato","hot",1,null,true,"Fired",["preparation-start"]);
        shell.Service.Dining=new(3,new Costina.Client.DiningDto("s1","M1",2,"InService",[],["pause","fire-next"]));
        shell.Service.SelectedCourse=new Costina.Client.CourseDto("c1","Pase","Ready",null,null,null,null,[prep],["serve"]);
        if(!shell.Service.CanAction("pause")||!shell.Service.CanAction("fire-next")||!shell.Service.CanAction("serve")||!shell.Service.CanAction("preparation-start"))
            throw new Exception("Advertised affordances must enable their controls.");
        if(shell.Service.CanAction("complete")||shell.Service.CanAction("skip")||shell.Service.CanAction("preparation-ready")||shell.Service.CanAction("release"))
            throw new Exception("Actions the server did not advertise must stay disabled.");
        if(!shell.CanOpen) throw new Exception("Session affordance must drive the open panel.");
        shell.Busy=true;
        if(shell.Service.CanAction("pause")) throw new Exception("Busy must gate every action.");
        shell.Busy=false; shell.Session=null;
        if(shell.Service.CanAction("pause")||shell.CanOpen) throw new Exception("Disconnected must gate every action.");
        // D3.3: la orden incierta persiste cifrada con DPAPI y un fichero corrupto se descarta.
        var store=new DpapiPendingStore(new Uri("http://127.0.0.1:59999"),"checks");
        store.Clear();
        var command=new Costina.Client.PendingCommand("k123","services/x/commands/start","{\"marker-secreto\":7}","Start");
        store.Save(command);
        if(store.Load() is not {Key:"k123"} restored||restored.Body!=command.Body) throw new Exception("DPAPI round trip must be lossless.");
        var pendingFile=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Costina","pending-59999-checks.bin");
        if(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(pendingFile)).Contains("marker-secreto")) throw new Exception("Pending file must not be plaintext.");
        File.WriteAllBytes(pendingFile,[1,2,3]);
        if(store.Load() is not null) throw new Exception("A corrupted pending file must be discarded.");
        if(File.Exists(pendingFile)) throw new Exception("Discarding must delete the corrupted file.");
        Directory.CreateDirectory("artifacts/desktop");
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        image.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var file=File.Create("artifacts/desktop/startup.png")) encoder.Save(file);
        File.WriteAllText("artifacts/desktop/window-checks.txt","Startup and viewmodel affordance-mapping checks passed. Actual WPF window rendered. No live backend interaction claimed by these checks.");
        window.Close();app.Shutdown();Console.WriteLine("WPF startup and viewmodel checks passed");return 0;
    }
}
