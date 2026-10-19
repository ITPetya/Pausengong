namespace Pausengong.Core.Audio;

/// <summary>Shows the "gong would have fired" notification used while test mode is active.</summary>
public interface IToastService
{
    Task ShowGongWouldHaveFiredAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default);
}
