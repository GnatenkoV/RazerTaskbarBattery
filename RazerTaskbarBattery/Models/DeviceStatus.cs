namespace RazerTaskbar.Models;

public sealed record DeviceStatus(string Name, string Handle, int BatteryPercentage, bool IsCharging, bool IsConnected, string Source = "Synapse", bool ChargingKnown = true);

public enum SynapseSource
{
    None,
    Synapse3,
    Synapse4
}

public sealed record MonitorSnapshot(SynapseSource Source, string? LogPath, IReadOnlyList<DeviceStatus> Devices, string? Error);
