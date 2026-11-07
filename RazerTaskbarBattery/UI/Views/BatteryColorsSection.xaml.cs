using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using RazerTaskbar.Models;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;

namespace RazerTaskbar.UI.Views;

public partial class BatteryColorsSection : System.Windows.Controls.UserControl
{
    private readonly List<(Func<BatteryPalette, int> Read, System.Windows.Shapes.Rectangle Swatch, TextBlock Hex)> _swatches = [];
    private readonly List<(int Percentage, Image Image)> _previews = [];

    public BatteryPalette Palette { get; private set; } = new();
    public event Action<BatteryPalette>? PaletteChanged;

    public BatteryColorsSection()
    {
        InitializeComponent();
        AddLevel(1, "Low", "0–20%", 10, p => p.Low, (p, level) => p with { Low = level });
        AddLevel(2, "Medium", "21–40%", 30, p => p.Medium, (p, level) => p with { Medium = level });
        AddLevel(3, "High", "41–100%", 75, p => p.High, (p, level) => p with { High = level });
    }

    public void SetPalette(BatteryPalette palette)
    {
        Palette = palette;
        foreach (var (read, swatch, hex) in _swatches)
        {
            int rgb = read(palette);
            bool transparent = rgb == BatteryPalette.Transparent;
            swatch.Fill = new SolidColorBrush(ColorPickerWindow.FromRgb(rgb));
            hex.Text = transparent ? "Transparent" : $"#{rgb & 0xFFFFFF:X6}";
            hex.FontSize = transparent ? 10 : 11;
        }

        foreach (var (percentage, image) in _previews)
        {
            image.Source = BatteryIcon.CreatePreview(percentage, palette);
        }

        PaletteChanged?.Invoke(palette);
    }

    private void AddLevel(int row, string name, string range, int percentage, Func<BatteryPalette, BatteryLevelColors> read, Func<BatteryPalette, BatteryLevelColors, BatteryPalette> write)
    {
        StackPanel label = new() { VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("FieldLabelStyle") });
        label.Children.Add(new TextBlock { Text = range, Style = (Style)FindResource("FieldDescriptionStyle") });

        AddCell(label, row, 0);
        AddCell(CreateSwatch($"{name} battery border", p => read(p).Border, (p, rgb) => write(p, read(p) with { Border = rgb })), row, 1);
        AddCell(CreateSwatch($"{name} battery fill", p => read(p).Fill, (p, rgb) => write(p, read(p) with { Fill = rgb })), row, 2);
        AddCell(CreateSwatch($"{name} battery number", p => read(p).Number, (p, rgb) => write(p, read(p) with { Number = rgb })), row, 3);
        AddCell(CreateSwatch($"{name} battery empty interior", p => read(p).Empty, (p, rgb) => write(p, read(p) with { Empty = rgb })), row, 4);

        Image preview = new() { Width = 48, Height = 48, ToolTip = $"{name} battery at {percentage}%" };
        AutomationProperties.SetName(preview, $"{name} battery preview");
        _previews.Add((percentage, preview));
        AddCell(preview, row, 5);
    }

    private void AddCell(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        ColorTable.Children.Add(element);
    }

    private Button CreateSwatch(string name, Func<BatteryPalette, int> read, Func<BatteryPalette, int, BatteryPalette> write)
    {
        System.Windows.Shapes.Rectangle swatch = new();
        Border swatchFrame = new()
        {
            Width = 14, Height = 18, CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("CaptureProtectorBorderBrush"),
            Margin = new Thickness(0, 0, 4, 0), ClipToBounds = true,
            Background = (Brush)FindResource("TransparencyCheckerBrush"), Child = swatch
        };

        TextBlock hex = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        StackPanel content = new() { Orientation = System.Windows.Controls.Orientation.Horizontal };
        content.Children.Add(swatchFrame);
        content.Children.Add(hex);
        Button button = new()
        {
            Style = (Style)FindResource("SecondaryButtonStyle"), Height = 32,
            Padding = new Thickness(4, 0, 4, 0), Margin = new Thickness(3, 0, 3, 0),
            VerticalAlignment = VerticalAlignment.Center, Content = content, ToolTip = name
        };

        AutomationProperties.SetName(button, name);
        _swatches.Add((read, swatch, hex));

        button.Click += (_, _) =>
        {
            BatteryPalette original = Palette;
            ColorPickerWindow picker = new(ColorPickerWindow.FromRgb(read(original)), name) { Owner = Window.GetWindow(this) };
            picker.ColorChanged += color => SetPalette(write(original, color.A == 0
                ? BatteryPalette.Transparent
                : (color.R << 16) | (color.G << 8) | color.B));
            if (picker.ShowDialog() != true)
            {
                SetPalette(original);
            }
        };

        return button;
    }

    private void ResetColors(object sender, RoutedEventArgs e) => SetPalette(new BatteryPalette());
}
