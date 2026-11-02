using System.Reflection.Metadata;
using System.Text;

namespace RazerTaskbar.Hid;

// Razer vendor report format: razercommon.h (90 bytes); battery command:
// razerchromacommon.c (class 0x07 / command 0x80 / argument size 2).
// Windows HID may expose this as a 90-byte feature report or a 91-byte
// report (extra HID report-ID prefix). Never guess the offset: use HID caps.
internal static class RazerProtocol
{
    internal const int PacketLength = 90;
    internal const int BatteryArgumentOffset = 9; // arguments[1], offset 8 + 1

    // Only known, read-only battery query transaction IDs.
    // The unknown-device fallback is intentionally finite.
    // It does not brute-force command IDs.
    private static readonly byte[] AdaptiveTransactions = [0x1F, 0x3F, 0xFF, 0x08];

    internal static bool TryGetProfile(ushort pid, out string name, out byte[] transactions, out bool adaptive)
    {
        if (RazerMouseCatalog.Products.TryGetValue(pid, out RazerMouseProduct? product))
        {
            name = product.Name;
            transactions = product.BatteryTransaction is byte tid ? [tid] : [];
            adaptive = false;
            return true;
        }

        name = $"Razer mouse (PID 0x{pid:X4})";
        transactions = AdaptiveTransactions;
        adaptive = true;
        return true;
    }

    internal static bool IsSupportedFeatureReportLength(int featureLength) => featureLength is 90 or 91;

    internal static byte[] BatteryQuery(byte transaction, int featureLength)
        => PowerQuery(transaction, featureLength, 0x80);

    // OpenRazer's read-only charging-status command: class 0x07 / command 0x84.
    // https://github.com/openrazer/openrazer/blob/master/driver/razerchromacommon.c
    internal static byte[] ChargingQuery(byte transaction, int featureLength)
        => PowerQuery(transaction, featureLength, 0x84);

    private static byte[] PowerQuery(byte transaction, int featureLength, byte command)
    {
        if (!IsSupportedFeatureReportLength(featureLength))
        {
            throw new ArgumentOutOfRangeException(nameof(featureLength));
        }

        byte[] packet = new byte[featureLength];
        int offset = featureLength - PacketLength;
        // On a 91-byte Windows HID report, byte 0 is the HID report ID (zero).
        // A 90-byte report maps the zero status byte to the report-ID position.
        packet[offset + 0] = 0; // New command
        packet[offset + 1] = transaction;
        packet[offset + 5] = 2; // Argument count
        packet[offset + 6] = 0x07; // Battery command class
        packet[offset + 7] = command;
        byte checksum = 0;

        for (int index = 2; index < 88; index++)
        {
            checksum ^= packet[offset + index];
        }

        packet[offset + 88] = checksum;
        return packet;
    }

    internal static bool TryReadPercent(ReadOnlySpan<byte> featureReport, byte transaction, out int percentage)
    {
        percentage = 0;
        if (!IsValidPowerReply(featureReport, transaction, 0x80)) return false;

        percentage = (int)Math.Round(featureReport[^PacketLength..][BatteryArgumentOffset] * 100d / 255d);
        return true;
    }

    internal static bool TryReadCharging(ReadOnlySpan<byte> featureReport, byte transaction, out bool charging)
    {
        charging = false;
        if (!IsValidPowerReply(featureReport, transaction, 0x84)) return false;

        byte value = featureReport[^PacketLength..][BatteryArgumentOffset];
        if (value > 1) return false;
        charging = value == 1;
        return true;
    }

    private static bool IsValidPowerReply(ReadOnlySpan<byte> featureReport, byte transaction, byte command)
    {
        if (!IsSupportedFeatureReportLength(featureReport.Length))
        {
            return false;
        }

        ReadOnlySpan<byte> packet = featureReport[^PacketLength..];
        // Require a real successful reply, not an unchanged request or random
        // report. This is particularly important for adaptive PID detection.
        if (packet[0] != 0x02 || 
            packet[1] != transaction || 
            packet[2] != 0 || 
            packet[3] != 0 ||
            packet[4] != 0 || 
            packet[5] != 2 || 
            packet[6] != 0x07 || 
            packet[7] != command)
        {
            return false;
        }

        byte crc = 0;
        for (int index = 2; index < 88; index++) crc ^= packet[index];
        if (crc != packet[88])
        {
            return false;
        }

        return true;
    }
}
