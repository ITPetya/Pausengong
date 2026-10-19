using Microsoft.UI;
using Microsoft.UI.Xaml;
using Pausengong.Core.Updates;
using WinRT.Interop;

namespace Pausengong.App;

/// <summary>
/// Shown INSTEAD OF <see cref="MainWindow"/> when the update check returns
/// <see cref="UpdateUrgency.Mandatory"/> — see <c>App.xaml.cs</c> <c>LaunchAsync</c>. No
/// networking, no MQTT, no scheduling loop ever starts in this path; only this window's content
/// (<see cref="UpdateRequiredPage"/>) does. Same "host real content in a Page, keep Window XAML
/// nearly empty" pattern as <see cref="MainWindow"/>/<see cref="SettingsWindow"/> — see
/// HANDOFF.md Gotchas on why (native crash otherwise).
/// </summary>
public sealed partial class UpdateRequiredWindow : Window
{
    public UpdateRequiredWindow(UpdateCheckResult updateResult)
    {
        InitializeComponent();
        Title = "Pausengong — Update erforderlich";

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon(AssetExtractor.LogoIcoPath);
        appWindow.Resize(new Windows.Graphics.SizeInt32(440, 320));

        Content = new UpdateRequiredPage(updateResult);
    }
}
