using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using RazerTaskbar.Models;

namespace RazerTaskbar.Hid;

internal sealed record HidDevice(string Path, ushort ProductId);
internal sealed record HidScan(IReadOnlyList<DeviceStatus> Devices, IReadOnlyList<string> Diagnostics);

internal static class HidReader
{
    // Unknown PIDs that didn't respond are retried after a cooldown rather
    // than sending four vendor-specific feature requests every poll interval.
    private static readonly ConcurrentDictionary<string, DateTimeOffset> AdaptiveRetryAfter = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan AdaptiveRetryDelay = TimeSpan.FromMinutes(15);

    public static HidScan Read(CancellationToken cancellationToken)
    {
        var found = new List<DeviceStatus>();
        var messages = new List<string>();

        foreach (HidDevice device in Enumerate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            RazerProtocol.TryGetProfile(device.ProductId, out string name, out byte[] transactions, out bool adaptive);
            string id = StableDeviceId(device.Path, device.ProductId);
            messages.Add($"Device {name}, PID=0x{device.ProductId:X4}, mode={(adaptive ? "adaptive" : "OpenRazer")}, path={device.Path}");

            if (transactions.Length == 0)
            {
                messages.Add("  Listed in OpenRazer, but its mouse driver has no battery-level query for this PID.");
                continue;
            }

            if (adaptive && AdaptiveRetryAfter.TryGetValue(id, out DateTimeOffset retryAt) && DateTimeOffset.UtcNow < retryAt)
            {
                messages.Add($"  Adaptive scan skipped until {retryAt:O} (last scan found no valid battery response).");
                continue;
            }

            using SafeFileHandle handle = HidNative.CreateFile(device.Path, HidNative.GenericRead | HidNative.GenericWrite, HidNative.ShareRead | HidNative.ShareWrite, IntPtr.Zero, HidNative.OpenExisting, 0, IntPtr.Zero);

            if (!handle.IsInvalid)
            {
                Probe(handle, id, name, transactions, adaptive, found, messages, cancellationToken);
            }
            else
            {
                messages.Add($"  Read/write open failed: {Marshal.GetLastWin32Error()}, trying feature-only access.");
                using SafeFileHandle featureHandle = HidNative.CreateFile(device.Path, 0, HidNative.ShareRead | HidNative.ShareWrite, IntPtr.Zero, HidNative.OpenExisting, 0, IntPtr.Zero);
                if (featureHandle.IsInvalid)
                {
                    messages.Add($"  Feature-only open failed: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Probe(featureHandle, id, name, transactions, adaptive, found, messages, cancellationToken);
                }
            }

            // Set a negative cache ONLY if the device failed to respond.
            if (adaptive)
            {
                if (found.Any(d => d.Handle == id)) AdaptiveRetryAfter.TryRemove(id, out _);
                else AdaptiveRetryAfter[id] = DateTimeOffset.UtcNow + AdaptiveRetryDelay;
            }
        }

        return new HidScan(found, messages);
    }

    private static void Probe(SafeFileHandle handle, string id, string name, byte[] transactions, bool adaptive, List<DeviceStatus> results, List<string> diagnostics, CancellationToken cancellationToken)
    {
        if (!TryGetFeatureLength(handle, out int featureLength, out ushort usagePage))
        {
            diagnostics.Add("  Cannot inspect HID descriptor; refusing to guess report size.");
            return;
        }

        diagnostics.Add($"  HID UsagePage=0x{usagePage:X4}, FeatureReportLength={featureLength}");
        if (!RazerProtocol.IsSupportedFeatureReportLength(featureLength))
        {
            diagnostics.Add("  Skipped: not a compatible 90/91-byte Razer feature-report interface.");
            return;
        }

        // Some Razer devices expose their vendor feature report under a normal
        // HID usage page. The strict 90/91-byte descriptor gate prevents us from
        // sending this command to regular short mouse/keyboard reports.
        // A vendor-defined usage page remains useful diagnostic evidence.

        foreach (byte transactionId in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] request = RazerProtocol.BatteryQuery(transactionId, featureLength);
            if (!HidNative.HidD_SetFeature(handle, request, request.Length))
            {
                diagnostics.Add($"  SetFeature transaction 0x{transactionId:X2} failed: {Marshal.GetLastWin32Error()}");
                continue;
            }

            // Give wireless receivers time to complete the request.
            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(150)))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            byte[] reply = new byte[featureLength];
            reply[0] = 0; // HID report ID, not the Razer command status
            if (!HidNative.HidD_GetFeature(handle, reply, reply.Length))
            {
                diagnostics.Add($"  GetFeature transaction 0x{transactionId:X2} failed: {Marshal.GetLastWin32Error()}");
                continue;
            }

            if (RazerProtocol.TryReadPercent(reply, transactionId, out int percentage))
            {
                string? deviceName = HidNative.getProductName(handle);

                bool chargingKnown = TryReadCharging(handle, transactionId, featureLength, diagnostics, cancellationToken, out bool charging);
                results.Add(new DeviceStatus(deviceName ?? name, id, percentage, charging, true, adaptive ? "HID (adaptive)" : "HID", chargingKnown));
                diagnostics.Add($"  Battery: {percentage.ToString(CultureInfo.InvariantCulture)}%, TID=0x{transactionId:X2}");
                return;
            }

            diagnostics.Add($"  Transaction 0x{transactionId:X2}: no validated battery reply (status/class/command/CRC check failed). Response={Convert.ToHexString(reply)}");
        }
    }

    private static bool TryReadCharging(SafeFileHandle handle, byte transactionId, int featureLength,
        List<string> diagnostics, CancellationToken cancellationToken, out bool charging)
    {
        charging = false;
        cancellationToken.ThrowIfCancellationRequested();
        byte[] request = RazerProtocol.ChargingQuery(transactionId, featureLength);
        if (!HidNative.HidD_SetFeature(handle, request, request.Length))
        {
            diagnostics.Add($"  Charging SetFeature failed: {Marshal.GetLastWin32Error()}");
            return false;
        }

        if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(150)))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        byte[] reply = new byte[featureLength];
        if (!HidNative.HidD_GetFeature(handle, reply, reply.Length))
        {
            diagnostics.Add($"  Charging GetFeature failed: {Marshal.GetLastWin32Error()}");
            return false;
        }

        bool known = RazerProtocol.TryReadCharging(reply, transactionId, out charging);
        diagnostics.Add(known ? $"  Charging: {charging}, TID=0x{transactionId:X2}"
            : $"  Charging status unavailable (invalid or unsupported reply). Response={Convert.ToHexString(reply)}");
        return known;
    }

    private static bool TryGetFeatureLength(SafeFileHandle handle, out int featureLength, out ushort usagePage)
    {
        featureLength = 0;
        usagePage = 0;
        if (!HidNative.HidD_GetPreparsedData(handle, out IntPtr preparsedData))
        {
            return false;
        }

        try
        {
            // HidP_GetCaps returns an NTSTATUS value (nonnegative means success).
            if (HidNative.HidP_GetCaps(preparsedData, out var caps) < 0) return false;
            featureLength = caps.FeatureReportByteLength;
            usagePage = caps.UsagePage;
            return true;
        }
        finally
        {
            HidNative.HidD_FreePreparsedData(preparsedData);
        }
    }

    private static string StableDeviceId(string path, ushort pid)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return $"hid:{pid:X4}:{Convert.ToHexString(digest.AsSpan(0, 6))}";
    }

    private static IEnumerable<HidDevice> Enumerate()
    {
        HidNative.HidD_GetHidGuid(out var hidGuid);
        IntPtr info = HidNative.SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, HidNative.Present | HidNative.DeviceInterface);
        if (info == new IntPtr(-1))
        {
            yield break;
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new HidNative.DeviceInterfaceData 
                {
                    Size = Marshal.SizeOf<HidNative.DeviceInterfaceData>() 
                };

                if (!HidNative.SetupDiEnumDeviceInterfaces(info, IntPtr.Zero, ref hidGuid, index, ref data))
                {
                    break;
                }

                _ = HidNative.SetupDiGetDeviceInterfaceDetail(info, ref data, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                if (requiredSize < 8 || requiredSize > 64 * 1024)
                {
                    continue;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!HidNative.SetupDiGetDeviceInterfaceDetail(info, ref data, buffer, requiredSize, out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    string? path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4));
                    if (path is null || !path.Contains("vid_1532", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int pidIndex = path.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
                    if (pidIndex < 0 || path.Length < pidIndex + 8)
                    {
                        continue;
                    }

                    if (ushort.TryParse(path.AsSpan(pidIndex + 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort pid))
                    {
                        yield return new HidDevice(path, pid);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(info);
        }
    }
}
