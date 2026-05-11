using System.Windows;
using H.NotifyIcon;
using WhatCable.Core.Providers;
using WhatCable.Providers.Windows;

namespace WhatCable.Tray;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private PortsWindow? _window;
    private ISnapshotService? _snapshotService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _snapshotService = new SnapshotService();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "WhatCable",
            NoLeftClickDelay = true,
        };
        _trayIcon.LeftClickCommand = new RelayCommand(ShowWindow);
        _trayIcon.ContextMenu = BuildContextMenu();
        _trayIcon.ForceCreate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void ShowWindow()
    {
        if (_window is null || !_window.IsLoaded)
        {
            _window = new PortsWindow(_snapshotService!);
        }
        _window.Show();
        _window.Activate();
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var showItem = new System.Windows.Controls.MenuItem { Header = "Show ports…" };
        showItem.Click += (_, _) => ShowWindow();
        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(showItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(quitItem);
        return menu;
    }
}
