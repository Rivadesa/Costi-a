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
        Directory.CreateDirectory("artifacts/desktop");
        var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
        image.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using(var file=File.Create("artifacts/desktop/startup.png")) encoder.Save(file);
        File.WriteAllText("artifacts/desktop/window-checks.txt","3/3 startup checks passed. Actual WPF window rendered. No live backend interaction claimed by these checks.");
        window.Close();app.Shutdown();Console.WriteLine("WPF startup: 3/3 passed");return 0;
    }
}
