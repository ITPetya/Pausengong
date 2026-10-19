namespace Pausengong.Core.Audio;

/// <summary>Plays the real gong sound on this PC's own audio output.</summary>
public interface IGongPlayer
{
    Task PlayAsync(CancellationToken ct = default);
}
