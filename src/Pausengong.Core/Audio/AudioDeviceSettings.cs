using Pausengong.Core.Storage;

namespace Pausengong.Core.Audio;

/// <summary>
/// Which playback device the gong plays on. Deliberately local-only — kept out of
/// <see cref="Config.AppConfig"/> and never synced over MQTT, since different PCs/rooms
/// routinely have different speakers plugged in and each should keep its own choice.
/// </summary>
public sealed record AudioDeviceSettings(string? OutputDeviceId)
{
    public static AudioDeviceSettings Default { get; } = new(OutputDeviceId: null);
}

public sealed class AudioDeviceSettingsStore
{
    private readonly string _path;

    public AudioDeviceSettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.AudioDeviceSettingsFilePath;
    }

    public AudioDeviceSettings Current { get; private set; } = AudioDeviceSettings.Default;

    public async Task<AudioDeviceSettings> LoadAsync(CancellationToken ct = default)
    {
        Current = await JsonFileStore.LoadOrDefaultAsync(_path, () => AudioDeviceSettings.Default, ct);
        return Current;
    }

    public async Task SaveAsync(AudioDeviceSettings settings, CancellationToken ct = default)
    {
        Current = settings;
        await JsonFileStore.SaveAsync(_path, settings, ct);
    }
}
