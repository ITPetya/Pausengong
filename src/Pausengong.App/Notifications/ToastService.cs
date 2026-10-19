using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Pausengong.Core.Audio;

namespace Pausengong.App.Notifications;

public sealed class ToastService : IToastService
{
    public Task ShowGongWouldHaveFiredAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default)
    {
        var builder = new AppNotificationBuilder()
            .AddText("Testmodus: Pausengong (nicht abgespielt)")
            .AddText($"{roomName} — {atTime:HH:mm}")
            .AddText("Testmodus ist aktiv, es wurde kein echter Ton abgespielt.");

        AppNotificationManager.Default.Show(builder.BuildNotification());
        return Task.CompletedTask;
    }

    public Task ShowSimpleNotificationAsync(string title, string message, CancellationToken ct = default)
    {
        var builder = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message);

        AppNotificationManager.Default.Show(builder.BuildNotification());
        return Task.CompletedTask;
    }
}
