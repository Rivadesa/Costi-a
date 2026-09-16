using System.Windows;
using System.Windows.Controls;
namespace Costina.Desktop;
public partial class App : Application
{
    // Test hosts own the window lifetime. This changes presentation startup, never API authorization.
    public bool CreateDefaultWindow { get; set; } = true;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!CreateDefaultWindow) return;
        var window = new MainWindow();
        ApplyTrialConnection(window);
        MainWindow = window;
        window.Show();
    }
    public static void ApplyTrialConnection(MainWindow window)
    {
        var url = Environment.GetEnvironmentVariable("COSTINA_DESKTOP_URL");
        var key = Environment.GetEnvironmentVariable("COSTINA_DESKTOP_KEY");
        Environment.SetEnvironmentVariable("COSTINA_DESKTOP_URL", null);
        Environment.SetEnvironmentVariable("COSTINA_DESKTOP_KEY", null);
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return;
        window.Loaded += (_, _) => {
            ((TextBox)window.FindName("Endpoint")).Text = url;
            ((PasswordBox)window.FindName("AccessKey")).Password = key;
            ((Button)window.FindName("ConnectButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
    }
}
