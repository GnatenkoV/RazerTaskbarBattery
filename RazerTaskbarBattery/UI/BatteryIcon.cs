using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media.Imaging;
using RazerTaskbar.Models;

namespace RazerTaskbar.UI;

/// <summary>
/// Draws a compact, percentage-first battery icon for the Windows notification area.
/// Each icon contains independently rendered sizes so that Windows does not have to shrink a large image down to an unreadable 16-pixel tray icon.
/// </summary>
internal static class BatteryIcon
{
    private static readonly int[] IconSizes = [16, 20, 24, 28, 32, 40, 48, 64];
    private const int SmallIconMetric = 49; // SM_CXSMICON

    private static readonly Color BatteryBackground = Color.FromArgb(5, 17, 8);
    private static readonly Color UnknownOutline = Color.FromArgb(164, 173, 168);

    public static Icon Create(int? percentage, bool isCharging, bool showNumber, BatteryPalette palette)
    {
        int? value = percentage.HasValue ? Math.Clamp(percentage.Value, 0, 100) : null;
        var images = new List<byte[]>(IconSizes.Length);

        foreach (int size in IconSizes)
        {
            using var bitmap = Draw(size, value, isCharging, showNumber, palette);
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            images.Add(png.ToArray());
        }

        using var iconStream = new MemoryStream();
        using (var writer = new BinaryWriter(iconStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0); // ICONDIR: reserved
            writer.Write((ushort)1); // ICO image type
            writer.Write((ushort)images.Count);

            int imageOffset = 6 + images.Count * 16;
            for (int i = 0; i < images.Count; i++)
            {
                writer.Write((byte)IconSizes[i]);
                writer.Write((byte)IconSizes[i]);
                writer.Write((byte)0); // palette colors (none)
                writer.Write((byte)0); // reserved
                writer.Write((ushort)1); // planes
                writer.Write((ushort)32); // bits per pixel
                writer.Write((uint)images[i].Length);
                writer.Write((uint)imageOffset);
                imageOffset += images[i].Length;
            }

            foreach (byte[] image in images)
            {
                writer.Write(image);
            }
        }

        iconStream.Position = 0;
        // A NotifyIcon ultimately holds one HICON. Choose the image matching
        // the Windows taskbar DPI instead of selecting a 16px icon and
        // forcing Windows to enlarge it at 125% or 150% scaling.
        int traySize = GetTrayIconSize();
        using var icon = new Icon(iconStream, new Size(traySize, traySize));

        return (Icon)icon.Clone();
    }

    public static BitmapSource CreatePreview(int percentage, BatteryPalette palette)
    {
        using var bitmap = Draw(64, percentage, false, true, palette);
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        png.Position = 0;
        var preview = BitmapFrame.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        preview.Freeze();
        return preview;
    }

    private static Color RgbColor(int rgb) => rgb == BatteryPalette.Transparent
        ? Color.Transparent
        : Color.FromArgb(unchecked((int)0xFF000000) | rgb);

    private static Bitmap Draw(int size, int? percentage, bool isCharging, bool showNumber, BatteryPalette palette)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);

        float scale = size / 64f;
        BatteryLevelColors colors = palette.ForPercentage(percentage.GetValueOrDefault());
        Color outline = percentage.HasValue ? RgbColor(colors.Border) : UnknownOutline;
        Color fill = RgbColor(colors.Fill);
        Color number = percentage.HasValue ? RgbColor(colors.Number) : Color.White;

        // The body deliberately uses almost the entire tray tile. The terminal
        // remains visible, but does not compete with the percentage for space.
        RectangleF outer = Rect(1.5f, 7f, 52.5f, 50f, scale);
        RectangleF terminal = Rect(51f, 21f, 11f, 22f, scale);
        RectangleF inner = Inset(outer, 3.5f * scale);

        using var outlineBrush = new SolidBrush(outline);
        using var emptyBrush = new SolidBrush(RgbColor(percentage.HasValue ? colors.Empty : BatteryPalette.DefaultEmpty));
        using var terminalPath = RoundedRect(terminal, 5f * scale);
        using var outerPath = RoundedRect(outer, 9.5f * scale);
        using var innerPath = RoundedRect(inner, 6.5f * scale);

        // Copy alpha as well as color so a transparent interior clears the border beneath it.
        g.CompositingMode = CompositingMode.SourceCopy;
        g.FillPath(outlineBrush, terminalPath);
        using var innerTerminalPath = RoundedRect(Inset(terminal, 3f * scale), 2f * scale);
        g.FillPath(emptyBrush, innerTerminalPath);
        g.FillPath(outlineBrush, outerPath);
        g.FillPath(emptyBrush, innerPath);
        g.CompositingMode = CompositingMode.SourceOver;

        // Clip the charge level to the rounded interior, including at 0% and 100%.
        if (percentage.HasValue)
        {
            GraphicsState state = g.Save();
            g.SetClip(innerPath);
            float chargeWidth = inner.Width * percentage.Value / 100f;
            if (chargeWidth > 0)
            {
                using var fillBrush = new LinearGradientBrush(inner, fill, Color.FromArgb(fill.A, (int)(fill.R * 0.67f), (int)(fill.G * 0.67f), (int)(fill.B * 0.67f)), LinearGradientMode.Horizontal);
                g.FillRectangle(fillBrush, inner.Left, inner.Top, chargeWidth, inner.Height);
            }
            g.Restore(state);
        }

        if ((showNumber || !percentage.HasValue) && number.A != 0)
        {
            string label = percentage?.ToString() ?? "?";
            // Reserve a generous interior region for the digits, particularly
            // for 100%, which needs three characters in the same small tray tile.
            RectangleF textBounds = Rect(5.5f, 13.5f, 44f, 36f, scale);
            DrawLabel(g, label, textBounds, scale, number);
        }

        // A contrasting badge beside the digits remains visible even at 16px.
        if (isCharging && percentage.HasValue)
        {
            DrawChargingBolt(g, scale);
        }

        return bitmap;
    }

    private static int GetTrayIconSize()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            int size = GetSystemMetricsForDpi(SmallIconMetric, dpi);
            if (size is >= 16 and <= 64)
            {
                return size;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // The app targets Windows 10/11; retain a safe fallback.
        }

        return SystemInformation.SmallIconSize.Width;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    private static void DrawLabel(Graphics g, string label, RectangleF area, float scale, Color color)
    {
        using var fontFamily = new FontFamily("Arial");
        using var path = new GraphicsPath();
        path.AddString(label, fontFamily, (int)FontStyle.Bold, 42f * scale, new PointF(0, 0), StringFormat.GenericTypographic);

        RectangleF bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float fit = Math.Min(area.Width / bounds.Width, area.Height / bounds.Height) + 0.02f;
        float width = bounds.Width * fit;
        float height = bounds.Height * fit;

        using (var transform = new Matrix())
        {
            transform.Translate(-bounds.Left, -bounds.Top);
            transform.Scale(fit, fit, MatrixOrder.Append);
            transform.Translate(area.Left + (area.Width - width) / 2f, area.Top + (area.Height - height) / 2f, MatrixOrder.Append);
            path.Transform(transform);
        }

        // Outline the digits for contrast against the fill.
        using var darkPen = new Pen(Color.FromArgb(195, 3, 17, 5), Math.Max(0.9f, 2.1f * scale))
        {
            LineJoin = LineJoin.Round
        };

        using var textBrush = new SolidBrush(color);
        g.DrawPath(darkPen, path);
        g.FillPath(textBrush, path);
    }

    private static void DrawChargingBolt(Graphics g, float scale)
    {
        PointF[] points =
        [
            Point(59f, 25f, scale), Point(48f, 43f, scale),
            Point(55f, 43f, scale), Point(50f, 60f, scale),
            Point(63f, 37f, scale), Point(56f, 37f, scale)
        ];

        using var border = new Pen(BatteryBackground, 4f * scale) { LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(Color.FromArgb(255, 225, 50));
        g.DrawPolygon(border, points);
        g.FillPolygon(brush, points);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
        float diameter = radius * 2f;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static RectangleF Rect(float x, float y, float width, float height, float scale) => new(x * scale, y * scale, width * scale, height * scale);

    private static RectangleF Inset(RectangleF rect, float value) => RectangleF.Inflate(rect, -value, -value);

    private static PointF Point(float x, float y, float scale) => new(x * scale, y * scale);
}
