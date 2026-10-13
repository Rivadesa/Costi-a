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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
