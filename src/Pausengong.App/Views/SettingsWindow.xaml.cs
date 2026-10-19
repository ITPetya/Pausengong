using Microsoft.UI;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Pausengong.App;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow(IServiceProvider services)
    {
        InitializeComponent();
        Title = "Pausengong — Einstellungen";

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(AssetExtractor.LogoIcoPath);
        appWindow.Resize(new Windows.Graphics.SizeInt32(480, 640));

        Content = new SettingsPage(services, DispatcherQueue);
    }
}
