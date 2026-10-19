namespace Pausengong.Core.Audio;

/// <summary>Shows the "gong would have fired" notification used while test mode is active, plus
/// a generic one-off notification for anything else that doesn't deserve its own dedicated
/// method (currently just the update checker's "you're up to date" notice).</summary>
public interface IToastService
{
    Task ShowGongWouldHaveFiredAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default);

    Task ShowSimpleNotificationAsync(string title, string message, CancellationToken ct = default);
}
