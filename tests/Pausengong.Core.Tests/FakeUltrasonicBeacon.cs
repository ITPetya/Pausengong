using Pausengong.Core.Audio;

namespace Pausengong.Core.Tests;

/// <summary>In-memory stand-in for real tone playback/capture so collection-loop logic (not yet
/// built, see HANDOFF.md "Track B: ultrasonic presence sensing", step 3) can eventually be tested
/// without real speaker/mic hardware.</summary>
internal sealed class FakeUltrasonicBeacon : IUltrasonicBeacon
{
    public List<TimeSpan> PlayedDurations { get; } = new();

    /// <summary>Queued readings returned by successive <see cref="ListenAsync"/> calls, in order.</summary>
    public Queue<double> QueuedReadings { get; } = new();

    public Task PlayToneAsync(TimeSpan duration, CancellationToken ct = default)
    {
        PlayedDurations.Add(duration);
        return Task.CompletedTask;
    }

    public Task<double> ListenAsync(TimeSpan duration, CancellationToken ct = default) =>
        Task.FromResult(QueuedReadings.Count > 0 ? QueuedReadings.Dequeue() : 0.0);
}
