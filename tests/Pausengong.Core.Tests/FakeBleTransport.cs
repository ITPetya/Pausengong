using Pausengong.Core.Neighbors;

namespace Pausengong.Core.Tests;

/// <summary>In-memory stand-in for the real BLE publisher so collection-loop logic (not yet
/// built, see HANDOFF.md "Phase 7 replacement", step 2) can eventually be tested without real
/// Bluetooth hardware.</summary>
internal sealed class FakeBleAdvertiser : IBleAdvertiser
{
    public List<string> AdvertisedPayloads { get; } = new();
    public bool IsAdvertising { get; private set; }

    public Task StartAdvertisingAsync(string payload, CancellationToken ct = default)
    {
        IsAdvertising = true;
        AdvertisedPayloads.Add(payload);
        return Task.CompletedTask;
    }

    public Task StopAdvertisingAsync()
    {
        IsAdvertising = false;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory stand-in for the real BLE watcher — same purpose as <see cref="FakeBleAdvertiser"/>.</summary>
internal sealed class FakeBleWatcher : IBleWatcher
{
    public bool IsWatching { get; private set; }

    public event Action<string, double>? AdvertisementReceived;

    public Task StartWatchingAsync(CancellationToken ct = default)
    {
        IsWatching = true;
        return Task.CompletedTask;
    }

    public Task StopWatchingAsync()
    {
        IsWatching = false;
        return Task.CompletedTask;
    }

    /// <summary>Simulates a received advertisement arriving from a nearby node.</summary>
    public void SimulateReceived(string payload, double rssiDbm) => AdvertisementReceived?.Invoke(payload, rssiDbm);
}
