using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using RazerTaskbar.Models;

namespace RazerTaskbar.UI.Views;

public partial class ColorPickerWindow : Window
{
    private bool _updating = true;
    private double _hue;
    private double _saturation;
    private double _brightness;

    public event Action<Color>? ColorChanged;

    public ColorPickerWindow(Color color, string title)
    {
        InitializeComponent();
        Title = $"Razer Taskbar Battery — {title}";
        PickerTitle.Text = title;
        SetRgb(color);
    }

    internal static Color FromRgb(int rgb) => rgb == BatteryPalette.Transparent
        ? Colors.Transparent
        : Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private void TransparencyChanged(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        Color color = HsvColor(_hue, _saturation, _brightness);
        color.A = TransparentToggle.IsChecked == true ? (byte)0 : (byte)255;
        RefreshPicker(color);
    }

    private void StartColorDrag(object sender, MouseButtonEventArgs e)
    {
        ColorPlane.CaptureMouse();
        PickFromPlane(e.GetPosition(ColorPlane));
    }

    private void DragColor(object sender, MouseEventArgs e)
    {
        if (ColorPlane.IsMouseCaptured)
        {
            PickFromPlane(e.GetPosition(ColorPlane));
        }
    }

    private void EndColorDrag(object sender, MouseButtonEventArgs e) => ColorPlane.ReleaseMouseCapture();

    private void PickFromPlane(Point point)
    {
        _saturation = Math.Clamp(point.X / ColorPlane.ActualWidth, 0, 1);
        _brightness = 1 - Math.Clamp(point.Y / ColorPlane.ActualHeight, 0, 1);
        RefreshPicker(HsvColor(_hue, _saturation, _brightness));
    }

    private void HueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating)
        {
            return;
        }

        _hue = e.NewValue;
        RefreshPicker(HsvColor(_hue, _saturation, _brightness));
    }

    private void HexChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        string hex = HexInput.Text.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
        {
            SetInputError("Enter six hex digits.");
            return;
        }

        SetRgb(FromRgb(rgb), updateHex: false);
    }

    private void RgbChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        if (!byte.TryParse(RedInput.Text, out byte red) || 
            !byte.TryParse(GreenInput.Text, out byte green) ||
            !byte.TryParse(BlueInput.Text, out byte blue))
        {
            SetInputError("Use RGB values from 0 to 255.");
            return;
        }

        SetRgb(Color.FromRgb(red, green, blue), updateRgb: false);
    }

    private void SetRgb(Color color, bool updateHex = true, bool updateRgb = true)
    {
        int brightest = Math.Max(color.R, Math.Max(color.G, color.B));
        int darkest = Math.Min(color.R, Math.Min(color.G, color.B));

        _hue = System.Drawing.Color.FromArgb(color.R, color.G, color.B).GetHue();
        _brightness = brightest / 255.0;
        _saturation = brightest == 0 ? 0 : (brightest - darkest) / (double)brightest;

        RefreshPicker(color, updateHex, updateRgb);
    }

    private void RefreshPicker(Color color, bool updateHex = true, bool updateRgb = true)
    {
        _updating = true;
        TransparentToggle.IsChecked = color.A == 0;
        HueBase.Fill = new SolidColorBrush(HsvColor(_hue, 1, 1));
        HueSlider.Value = _hue;
        ColorSample.Fill = new SolidColorBrush(color);
        PositionMarker();

        if (updateHex)
        {
            HexInput.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        if (updateRgb)
        {
            RedInput.Text = color.R.ToString(CultureInfo.InvariantCulture);
            GreenInput.Text = color.G.ToString(CultureInfo.InvariantCulture);
            BlueInput.Text = color.B.ToString(CultureInfo.InvariantCulture);
        }

        SetInputError(string.Empty);
        _updating = false;
        ColorChanged?.Invoke(color);
    }

    private void SetInputError(string message)
    {
        InputError.Text = message;
        ApplyButton.IsEnabled = message.Length == 0;
    }

    private void ColorPlaneSizeChanged(object sender, SizeChangedEventArgs e) => PositionMarker();

    private void PositionMarker()
    {
        double width = ColorPlane.ActualWidth;
        double height = ColorPlane.ActualHeight;
        if (width < Marker.Width || height < Marker.Height)
        {
            return;
        }

        Canvas.SetLeft(Marker, Math.Clamp(_saturation * width - Marker.Width / 2, 0, width - Marker.Width));
        Canvas.SetTop(Marker, Math.Clamp((1 - _brightness) * height - Marker.Height / 2, 0, height - Marker.Height));
    }

    private void SelectColor(object sender, RoutedEventArgs e) => DialogResult = true;

    private static Color HsvColor(double hue, double saturation, double brightness)
    {
        double sector = hue % 360 / 60;
        double chroma = brightness * saturation;
        double second = chroma * (1 - Math.Abs(sector % 2 - 1));
        double minimum = brightness - chroma;
        (double red, double green, double blue) = sector switch
        {
            < 1 => (chroma, second, 0.0),
            < 2 => (second, chroma, 0.0),
            < 3 => (0.0, chroma, second),
            < 4 => (0.0, second, chroma),
            < 5 => (second, 0.0, chroma),
            _ => (chroma, 0.0, second)
        };

        byte Channel(double amount) => (byte)Math.Round((amount + minimum) * 255);
        return Color.FromRgb(Channel(red), Channel(green), Channel(blue));
    }
}
