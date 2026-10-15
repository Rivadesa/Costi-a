using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Costina.Desktop;

// Corte B (B1): la aplicacion arranca sola con Windows (acceso directo del instalador), es de instancia unica (un segundo
// arranque trae al frente la ventana abierta) y, nada mas abrirse, espera al servidor y entra como puesto si este PC lo es.
// Los checks del WPF construyen la ventana directamente, sin pasar por aqui: nunca tocan la red.
public partial class App : Application
{
    private Mutex? single;

    // C1: sin Mica. El fondo Mica de Fluent deja la ventana transparente (en pantalla toma el color del escritorio y las capturas
    // salen con huecos); con este interruptor la ventana Fluent pinta WindowBackground (#F3F4F6, Themes/Tokens.xaml). En el
    // constructor estatico para que llegue tambien a los checks, que hacen new App() sin pasar por OnStartup.
    static App() => AppContext.SetSwitch("Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop", true);

    // C2b (checks): los checks del WPF hacen new App() y construyen la ventana ellos mismos, pero WPF lanza igualmente OnStartup en el
    // primer bombeo del despachador. Con un Costina real abierto en el PC, la instancia unica lo traia al frente y cerraba la aplicacion
    // de los checks a mitad; sin el, abria una segunda ventana que arrancaba sola. Con Checks no se arranca nada.
    internal static bool Checks { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Checks) return;
        single = new Mutex(true, @"Local\Costina.Desktop.Single", out var first);
        if (!first)
        {
            NativeMethods.BringExistingToFront();
            Shutdown(); return;
        }
        var window = new MainWindow();
        window.Show();
        _ = window.BootAsync();
    }

    protected override void OnExit(ExitEventArgs e) { single?.Dispose(); base.OnExit(e); }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    private const int SwRestore = 9;

    public static void BringExistingToFront()
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("Costina.Desktop"))
            if (process.Id != Environment.ProcessId && process.MainWindowHandle != nint.Zero)
            { ShowWindow(process.MainWindowHandle, SwRestore); SetForegroundWindow(process.MainWindowHandle); return; }
    }
}
