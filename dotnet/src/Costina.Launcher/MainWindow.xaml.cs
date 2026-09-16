using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Costina.TrialHost;
namespace Costina.Launcher;
public partial class MainWindow : Window
{
    private TrialController? controller;
    private bool busy;
    public MainWindow()
    {
        InitializeComponent();
        try {
            controller=new TrialController(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..")));
            controller.Progress=text=>Dispatcher.Invoke(()=>State.Text=text);
            State.Text=controller.Initialized ? "Datos existentes reconocidos. Pulsa Iniciar instalación." : "Pulsa Preparar datos ficticios una sola vez.";
            Detail.Text="Datos y diagnóstico: "+controller.Home+"\nNo compartas installation.dat. Las claves están protegidas para este usuario Windows. La base en disco NO es una copia de seguridad.";
        } catch(Exception e) {State.Text="No se pudo abrir el asistente.";Detail.Text=e is InvalidOperationException or ArgumentException ? e.Message : "Configuración inaccesible. No borres ni restablezcas los datos.";}
        Controls();
    }
    private void Controls()
    {
        Prepare.IsEnabled=!busy && controller is {Initialized:false};
        Start.IsEnabled=!busy && controller is {Initialized:true,Running:false};
        Stop.IsEnabled=!busy && controller is not null;
        Main.IsEnabled=Service.IsEnabled=Kitchen.IsEnabled=!busy && controller is {Running:true};
    }
    private async Task Run(Func<Task> action)
    {
        if(busy || controller is null)return;busy=true;Controls();
        try {await action();}
        catch(Exception e) {State.Text="La operación no se completó. Los datos no se borran.";Detail.Text=(e is InvalidOperationException or ArgumentException or TimeoutException ? e.Message : "Error técnico: "+e.GetType().Name)+"\nDiagnóstico local: "+controller.Home;}
        finally {busy=false;Controls();}
    }
    private async void PrepareClick(object sender,RoutedEventArgs e)=>await Run(()=>controller!.InitializeAsync());
    private async void StartClick(object sender,RoutedEventArgs e)=>await Run(()=>controller!.StartAsync());
    private async void StopClick(object sender,RoutedEventArgs e)=>await Run(()=>controller!.StopAsync());
    private void OpenClick(object sender,RoutedEventArgs e)
    {
        try {controller!.LaunchDesktop((string)((Button)sender).Tag);State.Text="Puesto abierto. Conserva este asistente durante el ensayo.";}
        catch(Exception ex) {State.Text=ex is InvalidOperationException ? ex.Message : "No se pudo abrir el puesto.";}
    }
    private void ClosingWindow(object? sender,CancelEventArgs e)
    {
        if(busy || controller is {HasRunningComponents:true}) {e.Cancel=true;State.Text="Cierra los puestos y pulsa Detener instalación antes de cerrar este asistente.";return;}
        controller?.Dispose();
    }
}
