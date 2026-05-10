using System.Runtime.InteropServices;
using System.Windows;

namespace MusicLibrary;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Stable AppUserModelID helps Windows taskbar stop reusing stale default app grouping/icons.
        _ = SetCurrentProcessExplicitAppUserModelID("MusicBakh.Player");
        base.OnStartup(e);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
