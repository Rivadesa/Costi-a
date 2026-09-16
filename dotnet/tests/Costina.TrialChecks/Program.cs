using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Costina.Client;
using Costina.Desktop;
using Costina.TrialHost;
using Npgsql;

internal static class Program
{
    private static readonly List<string> passed=[];
    private static string output="";
    private static TrialController? host;
    private static MainWindow? window;
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length!=2 || Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true")
            throw new InvalidOperationException("Installation integration checks run only in an isolated Windows CI runner.");
        output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var app=new App();app.InitializeComponent();app.StartupUri=null;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var result=1;
        app.Startup+=async (_,_)=>{
            try {await Run(Path.GetFullPath(args[0]));result=0;}
            catch(Exception e) {File.WriteAllText(Path.Combine(output,"failure.txt"),e.GetType().Name+": "+e.Message+"\n"+e.StackTrace);}
            finally {
                if(window is not null) {window.Close();window=null;}
                if(host is not null) {try {await host.StopAsync();host.Dispose();} catch(Exception e) {File.WriteAllText(Path.Combine(output,"cleanup-failure.txt"),e.GetType().Name);result=1;}}
                File.WriteAllText(Path.Combine(output,"trial-checks.json"),JsonSerializer.Serialize(new {passed=passed.Count,failed=result==0?0:1,cases=passed,commit=Environment.GetEnvironmentVariable("GITHUB_SHA"),environment="Windows native PostgreSQL + actual WPF; user session, not SCM"},new JsonSerializerOptions {WriteIndented=true}));
                app.Shutdown(result);
            }
        };
        app.Run();return result;
    }
    private static void Check(bool condition,string name) {if(!condition)throw new Exception(name);passed.Add(name);Console.WriteLine("PASS "+name);}
    private static T Ui<T>(string name) where T:FrameworkElement => (T)window!.FindName(name);
    private static async Task Wait(Func<bool> ready)
    {
        for(int n=0;n<200;n++) {if(ready())return;await Task.Delay(50);}
        throw new TimeoutException("WPF did not reach expected state: "+Ui<TextBlock>("Status").Text);
    }
    private static async Task Click(string name)
    {
        var button=Ui<Button>(name);if(!button.IsEnabled)throw new Exception("Disabled UI action: "+name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(()=>Ui<TabControl>("Tabs").IsEnabled);
        var status=Ui<TextBlock>("Status").Text;
        if(status.Contains("rechazó",StringComparison.Ordinal) || status.Contains("sin respuesta",StringComparison.Ordinal)
           || Ui<Border>("PendingPanel").Visibility==Visibility.Visible) throw new Exception("UI command not confirmed: "+name+" "+status);
    }
    private static async Task ShowWindow(string role)
    {
        Environment.SetEnvironmentVariable("COSTINA_DESKTOP_URL",host!.Settings!.Endpoint.ToString());
        Environment.SetEnvironmentVariable("COSTINA_DESKTOP_KEY",host.Settings.Key(role));
        window=new MainWindow();App.ApplyTrialConnection(window);window.Show();
        await Wait(()=>Ui<TextBlock>("Status").Text.StartsWith("Conectado",StringComparison.Ordinal));
    }
    private static async Task Run(string package)
    {
        var home=TrialController.DefaultHome;
        if(Directory.Exists(home))throw new InvalidOperationException("CI requires an empty dedicated trial home; never reuse user data.");
        host=new TrialController(package,home);
        var duplicateRejected=false;
        try {using var second=new TrialController(package,home);}catch(InvalidOperationException){duplicateRejected=true;}
        Check(duplicateRejected,"exclusive installation lock rejects second launcher");
        await host.InitializeAsync();
        Check(host.Initialized,"native PostgreSQL initialized through installer package");
        var saved=host.Settings!;
        var ciphertext=File.ReadAllBytes(Path.Combine(home,"installation.dat"));
        Check(!Encoding.UTF8.GetString(ciphertext).Contains(saved.RuntimePassword,StringComparison.Ordinal)
            && !File.Exists(Path.Combine(home,"bootstrap-password.tmp")),"DPAPI config persisted; temporary password removed");
        Check(SettingsVault.Load(home)==saved,"settings recover under same Windows user");
        await using(var runtime=new NpgsqlConnection(saved.Connection("runtime"))) {
            await runtime.OpenAsync();
            await using var role=new NpgsqlCommand("SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname=current_user",runtime);
            Check(!(bool)(await role.ExecuteScalarAsync())!,"runtime is not database administrator");
            bool denied=false;try {await using var deletion=new NpgsqlCommand("DELETE FROM native_d1.audit WHERE false",runtime);await deletion.ExecuteNonQueryAsync();}catch(PostgresException e)when(e.SqlState=="42501"){denied=true;}
            Check(denied,"runtime cannot delete audit records");
        }
        await host.StartAsync();Check(host.Running,"packaged native engine confirms authenticated readiness");
        await ShowWindow("main");
        Check(Ui<TabItem>("CheckoutTab").Visibility==Visibility.Visible,"actual WPF auto-connects through ordinary authentication");
        await Click("OpenButton");
        Check(Ui<ListBox>("BoardList").Items.Count==1,"actual WPF opens table in real PostgreSQL");
        var id=((BoardEntry)Ui<ListBox>("BoardList").SelectedItem).Service.Id;
        await Click("StartButton");await Click("FireButton");
        var count=Ui<DataGrid>("Preparations").Items.Count;
        for(int i=0;i<count;i++) {
            Ui<DataGrid>("Preparations").SelectedIndex=i;
            await Click("PrepStartButton");await Click("PrepReadyButton");
        }
        await Click("ValidateButton");await Click("ServeButton");
        Check(((CourseDto)Ui<DataGrid>("Courses").SelectedItem).State=="Served","actual WPF completes station work, validates and serves");
        Ui<TabControl>("Tabs").SelectedItem=Ui<TabItem>("CheckoutTab");
        await Wait(()=>Ui<Button>("AddButton").IsEnabled);
        Ui<ComboBox>("ProductSelect").SelectedItem=Ui<ComboBox>("ProductSelect").Items.Cast<ProductChoice>().Single(x=>x.Id=="water");
        await Click("AddButton");
        Ui<TextBox>("AmountInput").Text="300,00";await Click("PayButton");
        using(var api=host.Client("main")) {
            var account=await api.GetAsync<Versioned<AccountDto>>("checkout/services/"+id);
            Check(account.Data.BalanceCents==400 && account.Data.PaidCents==30000,"actual WPF adds server-priced water and records prepayment");
        }
        Ui<TabControl>("Tabs").SelectedItem=Ui<TabItem>("ServiceTab");await Click("FireButton");
        Check(((CourseDto)Ui<DataGrid>("Courses").SelectedItem).State is "Served" or "Fired","dining remains operable after payment");
        window!.UpdateLayout();
        var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(output,"connected-wpf.png")))encoder.Save(file);
        window.Close();window=null;
        await ShowWindow("service");
        Check(Ui<TabItem>("CheckoutTab").Visibility==Visibility.Collapsed,"sala WPF hides checkout after real login");
        using(var service=host.Client("service")) {
            bool denied=false;try {await service.GetAsync<ProductChoice[]>("checkout/catalog");}catch(ApiError e)when(e.Status==403){denied=true;}
            Check(denied,"server refuses financial catalog to sala");
        }
        window!.Close();window=null;
        await host.InitializeAsync();Check(host.Settings==saved,"repeated preparation preserves credentials and existing data");
        await host.StopAsync();host.Dispose();host=new TrialController(package,home);
        Check(host.Settings==saved,"launcher restart loads same protected installation");
        await host.StartAsync();
        using(var api=host.Client("main")) {
            var account=await api.GetAsync<Versioned<AccountDto>>("checkout/services/"+id);
            var service=await api.GetAsync<Versioned<DiningDto>>("services/"+id);
            Check(account.Data.PaidCents==30000 && account.Data.BalanceCents==400,"account survives PostgreSQL and engine restart");
            Check(service.Data.Courses.Single(c=>c.Id=="p2").State=="Fired","kitchen progress survives full native restart");
            Check((await api.GetAsync<BoardEntry[]>("board")).Length==1,"restart never duplicates service or reseeds");
        }
        await host.StopAsync();
        var blocker=new TcpListener(IPAddress.Loopback,saved.ApiPort);blocker.Start();
        try {
            bool refused=false;try {await host.StartAsync();}catch(InvalidOperationException){refused=true;}
            Check(refused && !host.Running,"occupied API port is rejected without terminating its owner");
        } finally {blocker.Stop();}
        host.Dispose();host=null;
        var dirty=Path.Combine(Path.GetTempPath(),"Costina D13 unrelated "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dirty);File.WriteAllText(Path.Combine(dirty,"sentinel.txt"),"keep");
        bool retained=false;try {using var unexpected=new TrialController(package,dirty);}catch(InvalidOperationException){retained=File.ReadAllText(Path.Combine(dirty,"sentinel.txt"))=="keep";}
        Check(retained,"unrecognized nonempty directory is never reset");
    }
}
