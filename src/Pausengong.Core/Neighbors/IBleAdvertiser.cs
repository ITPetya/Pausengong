namespace Pausengong.Core.Neighbors;

/// <summary>
/// Seam over the real BLE advertisement broadcast API (WinRT
/// <c>Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementPublisher</c> on Windows) so
/// the not-yet-built collection loop (see HANDOFF.md "Phase 7 replacement: BLE neighborhood
/// clustering", step 2) can eventually be unit tested against a fake. No real implementation
/// exists yet — whether this WinRT API even works from this app's unpackaged build
/// (<c>WindowsPackageType=None</c>) is exactly the open question the Phase 0 hardware spike needs
/// to answer first; writing a real implementation before that would be guessing at an unknown
/// that could make the whole approach moot.
/// </summary>
public interface IBleAdvertiser
{
    Task StartAdvertisingAsync(string payload, CancellationToken ct = default);
    Task StopAdvertisingAsync();
}
