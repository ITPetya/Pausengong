namespace Pausengong.Core.Audio;

/// <summary>
/// Seam over real tone playback/capture for Track B (ultrasonic presence sensing) so the
/// not-yet-built collection loop (see HANDOFF.md "Track B: ultrasonic presence sensing", step 3)
/// can eventually be unit tested against a fake. No real implementation exists yet — unlike the
/// BLE seams (<see cref="Neighbors.IBleAdvertiser"/>/<see cref="Neighbors.IBleWatcher"/>) this
/// isn't blocked on an open API-support question, but it still needs real speaker/mic hardware
/// (NAudio capture + FFT magnitude measurement) to write correctly and verify at all, and the
/// concrete tone frequency is itself an output of the still-pending Phase 0 audibility/frequency-
/// response spike, not a value to guess at here.
/// </summary>
public interface IUltrasonicBeacon
{
    /// <summary>Plays this node's fixed tone for the given duration (its round-robin slot).</summary>
    Task PlayToneAsync(TimeSpan duration, CancellationToken ct = default);

    /// <summary>Listens for the shared tone over the given window and returns a smoothed
    /// amplitude reading (unit/calibration deliberately unspecified here — see HANDOFF.md
    /// "Track B", step 1, the frequency and amplitude scale are spike outputs, not assumptions).</summary>
    Task<double> ListenAsync(TimeSpan duration, CancellationToken ct = default);
}
