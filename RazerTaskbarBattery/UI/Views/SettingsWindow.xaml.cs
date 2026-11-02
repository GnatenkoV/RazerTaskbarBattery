using System.IO;
using System.Windows;
using System.Windows.Controls;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;
using RazerTaskbar.Models;

namespace RazerTaskbar.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly Func<IReadOnlyList<string>> _diagnosticsProvider;
    private readonly List<string> _deviceHandles = [];
    public AppSettings UpdatedSettings { get; }

#if DEBUG
    private DebugBatterySection? _debugBatterySection;
    private Action<bool, int, bool>? _debugPreview;

    // Debug overrides exist only in memory and are not part of AppSettings.
    public void ConfigureDebugBatteryPreview(bool enabled, int level, Action<bool, int, bool> onPreview)
    {
        if (_debugBatterySection is not null)
        {
            throw new InvalidOperationException("Debug battery preview has already been configured.");
        }

        _debugPreview = onPreview;
        _debugBatterySection = new DebugBatterySection(enabled, level);
        _debugBatterySection.StateChanged += (active, value) => PublishDebugPreview(active, value);
        PercentageToggle.Checked += PercentageDisplayChanged;
        PercentageToggle.Unchecked += PercentageDisplayChanged;
        SettingsSections.Children.Add(_debugBatterySection);
    }

    private void PercentageDisplayChanged(object sender, RoutedEventArgs e)
    {
        if (_debugBatterySection is not null)
        {
            PublishDebugPreview(_debugBatterySection.OverrideEnabled, _debugBatterySection.BatteryLevel);
        }
    }

    private void PublishDebugPreview(bool enabled, int level)
    {
        _debugPreview?.Invoke(enabled, level, PercentageToggle.IsChecked == true);
    }
#endif

    public SettingsWindow(AppSettings settings, IReadOnlyList<DeviceStatus> devices, Func<IReadOnlyList<string>> diagnosticsProvider)
    {
        InitializeComponent();
        UpdatedSettings = settings.Copy();
        _diagnosticsProvider = diagnosticsProvider;

        SourceCombo.SelectedIndex = settings.BatterySource switch { "hid" => 1, "synapse" => 2, _ => 0 };
        SynapseCombo.SelectedIndex = settings.SynapseVersion switch { "v3" => 1, "v4" => 2, _ => 0 };
        StartupToggle.IsChecked = settings.RunAtStartup;
        PercentageToggle.IsChecked = settings.ShowPercentage;
        ChargingToggle.IsChecked = settings.DisplayChargingState;
        SynapseInterval.Text = settings.PollingThrottleSeconds.ToString();
        HidInterval.Text = settings.HidPollSeconds.ToString();
        _deviceHandles.Add(string.Empty);
        DevicesCombo.Items.Add("Automatically select device");

        foreach (DeviceStatus device in devices)
        {
            if (_deviceHandles.Contains(device.Handle))
            {
                continue;
            }

            _deviceHandles.Add(device.Handle);
            DevicesCombo.Items.Add($"{device.Name}  ·  {device.Source}");
        }

        DevicesCombo.SelectedIndex = Math.Max(0, _deviceHandles.FindIndex(id => id == settings.ShownDeviceHandle));
        UpdateDependencies();
    }

    private void SourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SynapseCombo is not null)
        {
            UpdateDependencies();
        }
    }

    private void UpdateDependencies()
    {
        bool synapse = SourceCombo.SelectedIndex != 1;
        bool hid = SourceCombo.SelectedIndex != 2;
        SynapseCombo.IsEnabled = synapse;
        SynapseInterval.IsEnabled = synapse;
        HidInterval.IsEnabled = hid;
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SynapseInterval.Text, out int synapseInterval) || synapseInterval is < 1 or > 3600)
        {
            MessageBox.Show(this, "Synapse refresh must be between 1 and 3600 seconds.", "Invalid interval", MessageBoxButton.OK, MessageBoxImage.Warning);
            SynapseInterval.Focus();
            return;
        }

        if (!int.TryParse(HidInterval.Text, out int hidInterval) || hidInterval is < 15 or > 3600)
        {
            MessageBox.Show(this, "HID refresh must be between 15 and 3600 seconds.", "Invalid interval", MessageBoxButton.OK, MessageBoxImage.Warning);
            HidInterval.Focus();
            return;
        }

        UpdatedSettings.RunAtStartup = StartupToggle.IsChecked == true;
        UpdatedSettings.ShowPercentage = PercentageToggle.IsChecked == true;
        UpdatedSettings.DisplayChargingState = ChargingToggle.IsChecked == true;
        UpdatedSettings.BatterySource = SourceCombo.SelectedIndex switch { 1 => "hid", 2 => "synapse", _ => "auto" };
        UpdatedSettings.SynapseVersion = SynapseCombo.SelectedIndex switch { 1 => "v3", 2 => "v4", _ => "auto" };
        UpdatedSettings.ShownDeviceHandle = _deviceHandles[Math.Max(0, DevicesCombo.SelectedIndex)];
        UpdatedSettings.PollingThrottleSeconds = synapseInterval;
        UpdatedSettings.HidPollSeconds = hidInterval;
        UpdatedSettings.Normalize();
        DialogResult = true;
    }

    private void Cancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ExportDiagnostics(object sender, RoutedEventArgs e)
    {
        SaveFileDialog picker = new()
        {
            Filter = "Text files (*.txt)|*.txt",
            FileName = "razer-taskbar-diagnostics.txt",
            Title = "Save HID diagnostic report"
        };

        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            IReadOnlyList<string> diagnostics = _diagnosticsProvider();
            string report = $"Generated: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                "Note: HID paths can contain identifying device information. Review before sharing." + Environment.NewLine +
                string.Join(Environment.NewLine, diagnostics);
            File.WriteAllText(picker.FileName, report);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Unable to export diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
