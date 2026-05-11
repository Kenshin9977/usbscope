using System.Windows;
using UsbScope.Core.Providers;

namespace UsbScope.Tray;

public partial class PortsWindow : Window
{
    private readonly ISnapshotService _snapshotService;

    public PortsWindow(ISnapshotService snapshotService)
    {
        _snapshotService = snapshotService;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        StatusText.Text = "Reading USB-C state…";
        try
        {
            var snapshot = await _snapshotService.CaptureAsync();
            PortsList.ItemsSource = snapshot.Ports;
            StatusText.Text = snapshot.Ports.Count == 0
                ? "No USB-C ports detected yet — providers will land in upcoming commits."
                : $"{snapshot.Ports.Count} port(s) — captured {snapshot.CapturedAt:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }
}
