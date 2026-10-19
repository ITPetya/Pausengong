using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Pausengong.Core.Updates;
using WinRT.Interop;

namespace Pausengong.App;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly MainPage _mainPage;
    private bool _allowClose;

    public MainWindow(IServiceProvider services, UpdateCheckResult initialUpdateResult)
    {
        InitializeComponent();
        Title = "Pausengong";

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.SetIcon(AssetExtractor.LogoIcoPath);
        _appWindow.Closing += OnAppWindowClosing;

        _mainPage = new MainPage(services, this, initialUpdateResult);
        Content = _mainPage;
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

        // "beim Öffnen des Fensters" (the maintainer's requirement, 2026-10-19) — silent unless
        // it actually finds something, see MainViewModel.RefreshUpdateStatusSilentlyAsync.
        _ = _mainPage.ViewModel.RefreshUpdateStatusSilentlyAsync();
    }

    public void ExitApplication()
    {
        _allowClose = true;
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
