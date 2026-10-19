namespace Pausengong.Core.Neighbors;

/// <summary>
/// Seam over the real BLE advertisement scan API (WinRT
/// <c>Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher</c> on Windows) —
/// same "not implemented yet, pending the Phase 0 hardware spike" reasoning as
/// <see cref="IBleAdvertiser"/>.
/// </summary>
public interface IBleWatcher
{
    /// <summary>Raised once per received advertisement: the sender's payload and the raw
    /// (unsmoothed) RSSI in dBm for that one reading. Smoothing (short moving average over ~30s,
    /// per HANDOFF.md "Phase 7 replacement", step 3) happens one layer up, not in here.</summary>
    event Action<string, double>? AdvertisementReceived;

    Task StartWatchingAsync(CancellationToken ct = default);
    Task StopWatchingAsync();
}
