using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using RazerTaskbar.Models;

namespace RazerTaskbar.Services;

/// <summary>
/// Interprets supported Synapse log formats.
/// The dictionary retains the last known battery state when a new tail window loses old events.
/// </summary>
public static class SynapseParsers
{
    private const RegexOptions V3Options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

    private static readonly Regex BatteryChangedV3 = new(
        @"^.+? INFO.+?_OnBatteryLevelChanged[\s\S]*?Name: (?<name>[^\r\n]*)[\s\S]*?Handle: (?<handle>\d+)[\s\S]*?level (?<level>\d+) state (?<charging>\d+)",
        V3Options, RegexTimeout);
    private static readonly Regex DeviceLoadedV3 = new(
        @"^.+? INFO.+?_OnDeviceLoaded[\s\S]*?Name: (?<name>[^\r\n]*)[\s\S]*?Handle: (?<handle>\d+)",
        V3Options, RegexTimeout);
    private static readonly Regex DeviceRemovedV3 = new(
        @"^.+? INFO.+?_OnDeviceRemoved[\s\S]*?Name: (?<name>[^\r\n]*)[\s\S]*?Handle: (?<handle>\d+)",
        V3Options, RegexTimeout);

    private enum V3EventKind { Battery, Loaded, Removed }
    private sealed record V3Event(int Position, V3EventKind Kind, Match Match);

    public static void ParseV3(string logText, IDictionary<string, DeviceStatus> devices)
    {
        var events = new List<V3Event>();
        foreach (Match match in BatteryChangedV3.Matches(logText))
        {
            events.Add(new V3Event(match.Index, V3EventKind.Battery, match));
        }

        foreach (Match match in DeviceLoadedV3.Matches(logText))
        {
            events.Add(new V3Event(match.Index, V3EventKind.Loaded, match));
        }

        foreach (Match match in DeviceRemovedV3.Matches(logText))
        {
            events.Add(new V3Event(match.Index, V3EventKind.Removed, match));
        }

        events.Sort((left, right) => left.Position.CompareTo(right.Position));

        foreach (var entry in events)
        {
            var match = entry.Match;
            string handle = match.Groups["handle"].Value;
            string name = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            devices.TryGetValue(handle, out var previous);
            switch (entry.Kind)
            {
                case V3EventKind.Battery:
                    if (!int.TryParse(match.Groups["level"].Value, out int level))
                    {
                        break;
                    }
                    bool charging = match.Groups["charging"].Value != "0";
                    devices[handle] = new DeviceStatus(name, handle, Math.Clamp(level, 0, 100), charging, previous?.IsConnected ?? true);
                    break;
                case V3EventKind.Loaded:
                    devices[handle] = new DeviceStatus(name, handle, previous?.BatteryPercentage ?? 0, previous?.IsCharging ?? false, true);
                    break;
                case V3EventKind.Removed:
                    devices[handle] = new DeviceStatus(name, handle, previous?.BatteryPercentage ?? 0, previous?.IsCharging ?? false, false);
                    break;
            }
        }
    }

    public static void ParseV4(string logText, IDictionary<string, DeviceStatus> devices)
    {
        // Synapse 4 logs a complete connectingDeviceData JSON array, not just an individual device event.
        // Apply each successfully parsed snapshot in chronological order.
        using var reader = new StringReader(logText);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            const string marker = "connectingDeviceData: ";
            int index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            string json = line[(index + marker.Length)..].Trim();
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var connected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string handle = GetString(item, "serialNumber");
                    if (string.IsNullOrWhiteSpace(handle))
                    {
                        handle = GetString(item, "deviceContainerId");
                    }

                    if (string.IsNullOrWhiteSpace(handle))
                    {
                        continue;
                    }
                    connected.Add(handle);

                    if (!GetBoolean(item, "hasBattery"))
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("powerStatus", out var power) || power.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!TryGetInteger(power, "level", out int level))
                    {
                        continue;
                    }

                    string name = GetLocalizedName(item, "name");
                    if (string.IsNullOrWhiteSpace(name)) name = GetLocalizedName(item, "productName");
                    if (string.IsNullOrWhiteSpace(name)) name = handle;
                    bool isCharging = string.Equals(GetString(power, "chargingStatus"), "Charging", StringComparison.OrdinalIgnoreCase);
                    devices[handle] = new DeviceStatus(name, handle, Math.Clamp(level, 0, 100), isCharging, true);
                }

                // The latest connection snapshot is authoritative, including an empty array.
                foreach (string handle in devices.Keys.ToArray())
                {
                    devices[handle] = devices[handle] with { IsConnected = connected.Contains(handle) };
                }
            }
            catch (JsonException)
            {
                // Logs can contain a partially written or truncated final line.
            }
        }

        // Replace a temporary serial placeholder when the actual serial becomes available.
        foreach (string placeholder in new[] { "NOSERIALNUMBER", "NOSERIAL" })
        {
            if (devices.TryGetValue(placeholder, out var unknown) && devices.Values.Any(x => x.Handle != placeholder && x.Name == unknown.Name))
            {
                devices.Remove(placeholder);
            }
        }
    }

    private static string GetString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var element))
        {
            return string.Empty;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : string.Empty;
    }

    private static bool GetBoolean(JsonElement value, string property)
    {
        return value.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.True;
    }

    private static bool TryGetInteger(JsonElement value, string property, out int result)
    {
        result = 0;
        if (!value.TryGetProperty(property, out var element))
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out result),
            JsonValueKind.String => int.TryParse(element.GetString(), out result),
            _ => false
        };
    }

    private static string GetLocalizedName(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var names) || names.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        string english = GetString(names, "en");
        if (!string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        foreach (var item in names.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.String) return item.Value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
