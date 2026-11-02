using Microsoft.Win32;

namespace RazerTaskbar.Services;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RazerTaskbarBattery";

    public static void Configure(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ?? throw new InvalidOperationException("Cannot open the Windows startup registry key.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        string path = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Application path not available");
        key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
    }

    public static bool IsConfigured()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value && value.Contains(Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Application path not available"), StringComparison.OrdinalIgnoreCase);
    }
}
