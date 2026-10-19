using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Pausengong.App;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;
    private bool _allowClose;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        Title = "Pausengong";

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.SetIcon(AssetExtractor.LogoIcoPath);
        _appWindow.Closing += OnAppWindowClosing;

        Content = new MainPage(services, this);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        // Closing the window (X button) minimizes to tray instead of exiting — the app keeps
        // running in the background so it can still ring on schedule. Reopen via the tray
        // icon's left-click, quit via the main window's "Beenden" button.
        args.Cancel = true;
        _appWindow.Hide();
    }

    public void ShowFromTray()
    {
        _appWindow.Show();
        Activate();
    }

    public void ExitApplication()
    {
        _allowClose = true;
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
