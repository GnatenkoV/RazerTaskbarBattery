namespace RazerTaskbar.Models;

public sealed class AppSettings
{
    public bool RunAtStartup { get; set; }
    public bool ShowPercentage { get; set; } = true;
    public int PollingThrottleSeconds { get; set; } = 15;
    public bool DisplayChargingState { get; set; } = true;
    public string ShownDeviceHandle { get; set; } = string.Empty;
    public string SynapseVersion { get; set; } = "auto";
    public string BatterySource { get; set; } = "auto";
    public int HidPollSeconds { get; set; } = 60;

    public AppSettings Copy()
    {
        return new AppSettings
        {
            RunAtStartup = RunAtStartup,
            ShowPercentage = ShowPercentage,
            PollingThrottleSeconds = PollingThrottleSeconds,
            DisplayChargingState = DisplayChargingState,
            ShownDeviceHandle = ShownDeviceHandle,
            SynapseVersion = SynapseVersion,
            BatterySource = BatterySource,
            HidPollSeconds = HidPollSeconds
        };
    }

    public void Normalize()
    {
        PollingThrottleSeconds = Math.Clamp(PollingThrottleSeconds, 1, 3600);
        HidPollSeconds = Math.Clamp(HidPollSeconds, 15, 3600);
        if (BatterySource is not ("auto" or "hid" or "synapse"))
        {
            BatterySource = "auto";
        }

        ShownDeviceHandle ??= string.Empty;
        if (SynapseVersion is not ("auto" or "v3" or "v4"))
        {
            SynapseVersion = "auto";
        }
    }
}
