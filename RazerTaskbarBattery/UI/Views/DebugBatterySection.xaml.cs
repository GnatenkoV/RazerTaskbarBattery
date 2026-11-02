using System.Windows;
using System.Windows.Controls;

namespace RazerTaskbar.UI.Views;

// This view is excluded from Release builds by the project configuration.
public partial class DebugBatterySection : System.Windows.Controls.UserControl
{
    private bool _initializing;

    public event Action<bool, int>? StateChanged;

    public bool OverrideEnabled => OverrideToggle.IsChecked == true;
    public int BatteryLevel => (int)BatteryLevelSlider.Value;

    public DebugBatterySection(bool enabled, int batteryLevel)
    {
        _initializing = true;
        InitializeComponent();
        BatteryLevelSlider.Value = Math.Clamp(batteryLevel, 0, 100);
        OverrideToggle.IsChecked = enabled;
        LevelLabel.Text = BatteryLevel.ToString();
        _initializing = false;
    }

    private void OverrideChanged(object sender, RoutedEventArgs e) => PublishState();

    private void LevelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LevelLabel is not null)
        {
            LevelLabel.Text = BatteryLevel.ToString();
        }

        PublishState();
    }

    private void PublishState()
    {
        if (!_initializing)
        {
            StateChanged?.Invoke(OverrideEnabled, BatteryLevel);
        }
    }
}
