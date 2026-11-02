using System.Windows;
using RazerTaskbar.UI.Services;

namespace RazerTaskbar;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\RazerTaskbarBattery.SingleInstance", out bool firstInstance);
        if (!firstInstance)
        {
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        try
        {
            _tray = new TrayService();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to start battery monitor: {ex.Message}", "Razer Taskbar Battery", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
