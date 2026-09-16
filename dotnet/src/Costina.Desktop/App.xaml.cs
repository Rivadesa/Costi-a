using System.Windows;
using System.Windows.Controls;
namespace Costina.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Test hosts may deliberately own the window lifecycle. Standard manual connection still works.
        if (StartupUri is not null && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSTINA_DESKTOP_URL")))
        {
            StartupUri = null;
            var window = new MainWindow();
            ApplyTrialConnection(window);
            MainWindow = window;
            window.Show();
        }
        base.OnStartup(e);
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
