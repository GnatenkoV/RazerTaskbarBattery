using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using RazerTaskbar.Models;
using RazerTaskbar.Services;
using RazerTaskbar.UI.Views;

namespace RazerTaskbar.UI.Services;

// WPF owns all UI, including the flyout menu and windows.
// WinForms is used only for the built-in Windows notification-area NotifyIcon bridge.
internal sealed class TrayService : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly BatteryMonitor _monitor = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly DispatcherTimer _timer = new();
    private readonly Forms.NotifyIcon _icon;
    private readonly ContextMenu _menu;
    private readonly MenuItem _devicesMenu;
    private readonly MenuItem _statusMenu;
    private readonly Style _menuItemStyle;
    private readonly Style _separatorStyle;
    private AppSettings _settings;
    private IReadOnlyList<DeviceStatus> _devices = [];
    private Icon? _renderedIcon;
    private DeviceStatus? _displayDevice;

#if DEBUG
    private bool _debugBatteryEnabled;
    private int _debugBatteryLevel = 50;
    private bool? _previewShowPercentage;
#endif

    private (int? Percentage, bool Charging, bool ShowNumber)? _lastIconState;
    private bool _refreshing;
    private bool _settingsOpen;
    private bool _aboutOpen;
    private bool _disposed;

    public TrayService()
    {
        _settings = _store.Load();
        if (_store.IsFirstRun)
        {
            _store.Save(_settings);
        }

        _menuItemStyle = (Style)Application.Current.FindResource("TrayMenuItemStyle");
        _separatorStyle = (Style)Application.Current.FindResource("TraySeparatorStyle");
        _menu = new ContextMenu
        {
            Style = (Style)Application.Current.FindResource("TrayContextMenuStyle"),
            Placement = PlacementMode.MousePoint,
            StaysOpen = false
        };
        _menu.Opened += (_, _) => ActivateMenu();
        _menu.Closed += (_, _) => _devicesMenu?.IsSubmenuOpen = false;

        _statusMenu = new MenuItem
        {
            Header = "No battery information",
            Style = (Style)Application.Current.FindResource("TrayStatusItemStyle"),
            IsEnabled = false
        };
        _devicesMenu = Item("Devices");
        BuildMenu();

        _icon = new Forms.NotifyIcon { Text = "Razer Taskbar Battery" };
        _icon.MouseUp += OnIconMouseUp;
        _icon.MouseDoubleClick += OnIconDoubleClick;
        UpdateIcon(null);
        _icon.Visible = true;

        // The first tick runs after WPF has entered its dispatcher loop.
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromSeconds(_settings.PollingThrottleSeconds);
            await RefreshAsync();
        };
        _timer.Start();
    }

    private MenuItem Item(string text, Action? action = null)
    {
        MenuItem item = new() { Header = text, Style = _menuItemStyle };
        if (action is not null)
        {
            item.Click += (_, _) => action();
        }
        return item;
    }

    private Separator Separator() => new() { Style = _separatorStyle };

    private void BuildMenu()
    {
        _menu.Items.Add(new MenuItem
        {
            Header = "RAZER TASKBAR BATTERY",
            Style = (Style)Application.Current.FindResource("TrayHeaderItemStyle"),
            IsEnabled = false
        });

        _menu.Items.Add(_statusMenu);
        _menu.Items.Add(Separator());
        _menu.Items.Add(_devicesMenu);
        _menu.Items.Add(Item("Refresh now", () => _ = RefreshAsync(forceHid: true)));
        _menu.Items.Add(Item("Settings", ShowSettings));
        _menu.Items.Add(Item("About", ShowAbout));
        _menu.Items.Add(Separator());
        _menu.Items.Add(Item("Exit", () => Application.Current.Shutdown()));

        UpdateDevicesMenu();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    private void ActivateMenu()
    {
        if (!_menu.IsOpen || _disposed)
        {
            return;
        }

        // A context menu opened from NotifyIcon has no active WPF owner window.
        // Without activating the popup HWND, clicks in other applications do not
        // reliably dismiss it. Let WPF handle focus loss, Escape and submenus.
        if (PresentationSource.FromVisual(_menu) is HwndSource source)
        {
            SetForegroundWindow(source.Handle);
        }

        _menu.Focus();
    }

    private void OnIconMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        // Left-click dismisses the existing menu; another right-click toggles it
        // instead of forcibly closing and reopening at the cursor.
        if (_menu.IsOpen)
        {
            if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right)
            {
                _menu.IsOpen = false;
            }
            return;
        }

        if (e.Button != Forms.MouseButtons.Right)
        {
            return;
        }

        _menu.Placement = PlacementMode.MousePoint;
        _menu.IsOpen = true;
        ActivateMenu();
    }

    private void OnIconDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ShowSettings();
        }
    }

    private async Task RefreshAsync(bool forceHid = false)
    {
        if (_refreshing || _disposed)
        {
            return;
        }

        _refreshing = true;
        try
        {
            AppSettings copy = _settings.Copy();
            MonitorSnapshot snapshot = await Task.Run(() => _monitor.Scan(copy, _cancel.Token, forceHid), _cancel.Token);
            if (_disposed)
            {
                return;
            }

            _devices = snapshot.Devices;
            UpdateDevicesMenu();
            DeviceStatus? selected = _devices.FirstOrDefault(x => x.Handle == _settings.ShownDeviceHandle);
            selected ??= _devices.Where(x => x.IsConnected).OrderBy(x => x.BatteryPercentage).FirstOrDefault();
            _displayDevice = selected;

            if (selected is null)
            {
                _icon.Text = "Razer Taskbar: no battery information";
                _statusMenu.Header = "No battery information";
                UpdateIcon(null);
            }
            else
            {
                string charging = selected.ChargingKnown && selected.IsCharging ? " (charging)" : string.Empty;
                string tooltip = $"{selected.Name}: {selected.BatteryPercentage}%{charging} [{selected.Source}]";
                _icon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
                string shortName = selected.Name.Length > 34 ? selected.Name[..31] + "..." : selected.Name;
                _statusMenu.Header = $"{shortName}  ·  {selected.BatteryPercentage}%{charging}";
                UpdateIcon(selected);
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AppLog.Write($"Battery refresh failed: {ex}");
            if (!_disposed)
            {
                _statusMenu.Header = "Refresh failed";
                _icon.Text = "Razer Taskbar: refresh failed";
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateDevicesMenu()
    {
        _devicesMenu.Items.Clear();
        MenuItem automatic = Item("Auto select", () => SelectDevice(string.Empty));
        automatic.IsChecked = string.IsNullOrEmpty(_settings.ShownDeviceHandle);
        _devicesMenu.Items.Add(automatic);

        if (_devices.Count > 0)
        {
            _devicesMenu.Items.Add(Separator());
        }

        foreach (DeviceStatus device in _devices)
        {
            string handle = device.Handle;
            MenuItem item = Item($"{device.Name} — {device.BatteryPercentage}% ({device.Source})", () => SelectDevice(handle));
            item.IsChecked = handle == _settings.ShownDeviceHandle;
            _devicesMenu.Items.Add(item);
        }
    }

    private void SelectDevice(string handle)
    {
        _settings.ShownDeviceHandle = handle;
        try
        {
            _store.Save(_settings);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Could not save device preference: {ex}");
        }

        UpdateDevicesMenu();
        _ = RefreshAsync();
    }

    private void UpdateIcon(DeviceStatus? device)
    {
        int? displayedPercentage = device?.BatteryPercentage;
        bool charging = _settings.DisplayChargingState && device is { IsCharging: true, ChargingKnown: true };
        bool showNumber = _settings.ShowPercentage;
#if DEBUG
        if (_debugBatteryEnabled)
        {
            displayedPercentage = _debugBatteryLevel;
        }
        showNumber = _previewShowPercentage ?? showNumber;
#endif
        var state = (Percentage: displayedPercentage, Charging: charging, ShowNumber: showNumber);
        if (_lastIconState == state)
        {
            return;
        }

        Icon next = BatteryIcon.Create(state.Percentage, state.Charging, state.ShowNumber);
        _icon.Icon = next;
        _renderedIcon?.Dispose();
        _renderedIcon = next;
        _lastIconState = state;
    }

    private void ShowSettings()
    {
        if (_disposed || _settingsOpen)
        {
            return;
        }

        _menu.IsOpen = false;
        _settingsOpen = true;
#if DEBUG
        bool initialDebugEnabled = _debugBatteryEnabled;
        int initialDebugLevel = _debugBatteryLevel;
        bool saved = false;
#endif
        try
        {
            SettingsWindow dialog = new(_settings, _devices, () => _monitor.LastDiagnostics);
#if DEBUG
            dialog.ConfigureDebugBatteryPreview(_debugBatteryEnabled, _debugBatteryLevel, (enabled, level, showNumber) =>
            {
                _debugBatteryEnabled = enabled;
                _debugBatteryLevel = Math.Clamp(level, 0, 100);
                _previewShowPercentage = showNumber;
                UpdateIcon(_displayDevice);
            });
#endif
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            AppSettings updated = dialog.UpdatedSettings;
            try
            {
                StartupManager.Configure(updated.RunAtStartup);
                _store.Save(updated);
                _settings = updated;
                _timer.Interval = TimeSpan.FromSeconds(_settings.PollingThrottleSeconds);
#if DEBUG
                saved = true;
#endif
                _lastIconState = null;
                _ = RefreshAsync(forceHid: true);
            }
            catch (Exception ex)
            {
                AppLog.Write($"Settings save failed: {ex}");
                MessageBox.Show($"Unable to save settings: {ex.Message}", "Razer Taskbar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
#if DEBUG
            if (!saved)
            {
                _debugBatteryEnabled = initialDebugEnabled;
                _debugBatteryLevel = initialDebugLevel;
            }
            _previewShowPercentage = null;
            // Restore the saved setting on Cancel or keep the override after Save.
            UpdateIcon(_displayDevice);
#endif
            _settingsOpen = false;
        }
    }

    private void ShowAbout()
    {
        if (_disposed || _aboutOpen)
        {
            return;
        }

        _menu.IsOpen = false;
        _aboutOpen = true;
        try
        {
            new AboutWindow().ShowDialog();
        }
        finally
        {
            _aboutOpen = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancel.Cancel();
        _timer.Stop();
        _menu.IsOpen = false;
        _icon.Visible = false;
        _icon.Dispose();
        _renderedIcon?.Dispose();
        // Keep _cancel alive until any outstanding HID scan has finished observing its token.
    }
}
