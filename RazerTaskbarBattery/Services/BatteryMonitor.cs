using RazerTaskbar.Hid;
using RazerTaskbar.Models;

namespace RazerTaskbar.Services;

internal sealed class BatteryMonitor
{
    private readonly SynapseMonitor _synapse = new();
    private HidScan _lastHid = new(Array.Empty<DeviceStatus>(), Array.Empty<string>());
    private DateTimeOffset _lastHidAt = DateTimeOffset.MinValue;
    private readonly object _sync = new();

    public IReadOnlyList<string> LastDiagnostics
    {
        get { lock (_sync) return _lastHid.Diagnostics.ToArray(); }
    }

    public MonitorSnapshot Scan(AppSettings settings, CancellationToken cancellationToken, bool forceHid = false)
    {
        settings.Normalize();
        HidScan hid;

        if (settings.BatterySource != "synapse")
        {
            lock (_sync)
            {
                bool due = forceHid || DateTimeOffset.UtcNow - _lastHidAt >= TimeSpan.FromSeconds(settings.HidPollSeconds);
                if (due)
                {
                    _lastHid = HidReader.Read(cancellationToken);
                    _lastHidAt = DateTimeOffset.UtcNow;
                }
                hid = _lastHid;
            }
        }
        else
        {
            hid = new HidScan(Array.Empty<DeviceStatus>(), Array.Empty<string>());
        }

        cancellationToken.ThrowIfCancellationRequested();

        MonitorSnapshot synapse = settings.BatterySource == "hid"
            ? new MonitorSnapshot(SynapseSource.None, null, Array.Empty<DeviceStatus>(), null)
            : _synapse.Scan(settings.SynapseVersion);

        List<DeviceStatus> output = new(hid.Devices);
        foreach (DeviceStatus device in synapse.Devices.Where(d => d.IsConnected))
        {
            // The Synapse and HID transports may describe the same physical product.
            // Prefer fresh HID readings; use Synapse charging only when HID cannot report it.
            int existing = output.FindIndex(x => Normalize(x.Name) == Normalize(device.Name));
            if (existing >= 0)
            {
                DeviceStatus candidate = output[existing];
                if (!candidate.ChargingKnown && device.ChargingKnown)
                {
                    output[existing] = candidate with { IsCharging = device.IsCharging, ChargingKnown = true };
                }
            }

            else
            {
                output.Add(device);
            }
        }

        output.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
        return new MonitorSnapshot(synapse.Source, synapse.LogPath, output, synapse.Error);
    }

    private static string Normalize(string name)
    {
        string lower = name.ToLowerInvariant()
            .Replace("(wireless)", "")
            .Replace("(wired)", "")
            .Replace("(2023)", "")
            .Replace("(2020)", "")
            .Replace("dongle", "")
            .Replace("receiver", "");

        return new string(lower.Where(char.IsLetterOrDigit).ToArray());
    }
}
