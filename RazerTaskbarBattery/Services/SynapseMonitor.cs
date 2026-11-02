using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RazerTaskbar.Models;

namespace RazerTaskbar.Services;

internal sealed class SynapseMonitor
{
    // Latest log events are normally close to EOF; old state remains cached between scans.
    private const int MaximumTailBytes = 16 * 1024 * 1024;
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string V3Path = Path.Combine(LocalAppData, "Razer", "Synapse3", "Log", "Razer Synapse 3.log");
    private static readonly string V4Directory = Path.Combine(LocalAppData, "Razer", "RazerAppEngine", "User Data", "Logs");
    private static readonly Regex V4FileName = new(@"^systray_systrayv2(?<index>\d*)\.log$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, DeviceStatus> _devices = new(StringComparer.Ordinal);
    private string? _lastPath;

    public MonitorSnapshot Scan(string synapseVersion)
    {
        (SynapseSource source, string? path) = ResolveLogPath(synapseVersion);
        if (!string.Equals(_lastPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _devices.Clear();
            _lastPath = path;
        }

        if (path is null || !File.Exists(path))
        {
            _devices.Clear();
            return new MonitorSnapshot(source, path, Array.Empty<DeviceStatus>(), "Waiting for Razer Synapse logs");
        }

        try
        {
            string log = ReadTail(path);
            if (source == SynapseSource.Synapse4)
            {
                SynapseParsers.ParseV4(log, _devices);
            }
            else
            {
                SynapseParsers.ParseV3(log, _devices);
            }

            return new MonitorSnapshot(source, path, _devices.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException)
        {
            AppLog.Write($"Log processing failed: {ex}");
            return new MonitorSnapshot(source, path, _devices.Values.ToArray(), ex.Message);
        }
    }

    private static (SynapseSource Source, string? Path) ResolveLogPath(string synapseVersion)
    {
        string? v4 = FindLatestV4Log();
        if (synapseVersion == "v3")
        {
            return (SynapseSource.Synapse3, V3Path);
        }

        if (synapseVersion == "v4")
        {
            return (SynapseSource.Synapse4, v4);
        }

        if (v4 is not null)
        {
            return (SynapseSource.Synapse4, v4);
        }

        return (SynapseSource.Synapse3, V3Path);
    }

    private static string? FindLatestV4Log()
    {
        if (!Directory.Exists(V4Directory))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(V4Directory)
                .Select(path => new { Path = path, Match = V4FileName.Match(Path.GetFileName(path)) })
                .Where(item => item.Match.Success)
                .Select(item => new
                {
                    item.Path,
                    Sequence = int.TryParse(item.Match.Groups["index"].Value, out int number) ? number : -1,
                    Modified = File.GetLastWriteTimeUtc(item.Path)
                })
                .OrderByDescending(item => item.Sequence)
                .ThenByDescending(item => item.Modified)
                .Select(item => item.Path)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Cannot enumerate Synapse 4 logs: {ex.Message}");
            return null;
        }
    }

    private static string ReadTail(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 64 * 1024, FileOptions.SequentialScan);
        long firstByte = Math.Max(0, stream.Length - MaximumTailBytes);
        stream.Seek(firstByte, SeekOrigin.Begin);
        int count = checked((int)Math.Min(MaximumTailBytes, stream.Length - firstByte));
        byte[] data = new byte[count];
        int read = 0;

        while (read < count)
        {
            int amount = stream.Read(data, read, count - read);
            if (amount == 0)
            {
                break;
            }

            read += amount;
        }

        string text = Encoding.UTF8.GetString(data, 0, read);
        if (firstByte > 0)
        {
            int firstNewline = text.IndexOf('\n');
            if (firstNewline < 0)
            {
                return string.Empty;
            }

            text = text[(firstNewline + 1)..];
        }

        return text;
    }
}
