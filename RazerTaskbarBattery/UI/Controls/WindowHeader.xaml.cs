using System.Windows;
using System.Windows.Input;

namespace RazerTaskbar.UI.Controls;

public partial class WindowHeader : System.Windows.Controls.UserControl
{
    public WindowHeader()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Window.GetWindow(this) is Window window)
            {
                MaximizeButton.Visibility = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip
                    ? Visibility.Visible : Visibility.Collapsed;
                window.StateChanged += (_, _) => UpdateGlyph(window);
                UpdateGlyph(window);
            }
        };
    }

    private void UpdateGlyph(Window window) => MaximizeGlyph.Text = window.WindowState == WindowState.Maximized ? "" : "";

    private void HeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Window.GetWindow(this) is not Window window)
            return;
        if (e.ClickCount == 2 && window.ResizeMode is (ResizeMode.CanResize or ResizeMode.CanResizeWithGrip))
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        window.DragMove();
    }

    private void Minimize(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Window window) window.WindowState = WindowState.Minimized;
    }

    private void Maximize(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Window window)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
