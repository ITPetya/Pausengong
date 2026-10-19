using Pausengong.Core.Config;

namespace Pausengong.Core.Audio;

/// <summary>
/// The single chokepoint that decides toast-vs-real-audio when a ring command executes.
/// Every ring must go through <see cref="ExecuteRingAsync"/> — nothing else in the codebase
/// may call <see cref="IGongPlayer"/> directly for a scheduled/network-triggered ring. The
/// manual "Test-Gong" button is the one deliberate exception and must call
/// <see cref="IGongPlayer"/> straight from its own click handler instead of through here.
/// </summary>
public sealed class RingExecutor
{
    private readonly ConfigStore _configStore;
    private readonly IGongPlayer _gongPlayer;
    private readonly IToastService _toastService;

    public RingExecutor(ConfigStore configStore, IGongPlayer gongPlayer, IToastService toastService)
    {
        _configStore = configStore;
        _gongPlayer = gongPlayer;
        _toastService = toastService;
    }

    public Task ExecuteRingAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default) =>
        _configStore.Current.TestMode.IsActive
            ? _toastService.ShowGongWouldHaveFiredAsync(roomName, atTime, ct)
            : _gongPlayer.PlayAsync(ct);
}
