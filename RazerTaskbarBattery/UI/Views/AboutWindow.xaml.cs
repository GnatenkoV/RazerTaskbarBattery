using System.Diagnostics;
using System.Reflection;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
namespace RazerTaskbar.UI.Views;

public partial class AboutWindow : Window
{
    private const string RepositoryUrl = "https://github.com/GnatenkoV/RazerTaskbarBattery";

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
    }

    private void OpenRepository(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
}
