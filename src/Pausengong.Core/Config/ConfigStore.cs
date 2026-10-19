using Pausengong.Core.Models;
using Pausengong.Core.Storage;

namespace Pausengong.Core.Config;

/// <summary>
/// Local persistence for <see cref="AppConfig"/> and this node's <see cref="NodeIdentity"/>.
/// Holds the last-loaded config in memory and raises <see cref="ConfigChanged"/> whenever it
/// changes, whether from a local edit or (from Phase 4 onward) an incoming MQTT sync.
/// </summary>
public sealed class ConfigStore
{
    private readonly string _configPath;
    private readonly string _identityPath;

    public ConfigStore(string? configPath = null, string? identityPath = null)
    {
        _configPath = configPath ?? AppPaths.ConfigFilePath;
        _identityPath = identityPath ?? AppPaths.NodeIdentityFilePath;
    }

    public AppConfig Current { get; private set; } = AppConfig.CreateDefault();

    public event Action<AppConfig>? ConfigChanged;

    public async Task<AppConfig> LoadAsync(CancellationToken ct = default)
    {
        var loaded = await JsonFileStore.LoadOrDefaultAsync(_configPath, AppConfig.CreateDefault, ct);

        // Self-heal a config saved before the default landing room existed (e.g. an old install
        // with zero rooms configured). Deliberately in-memory only, keeping the section's
        // existing UpdatedAt untouched: this must never look "newer" than a peer's real room
        // setup during MQTT last-writer-wins sync, it just needs to stop BreakScheduler from
        // having zero rooms to ever draw against. It gets persisted for real the next time
        // anything actually saves the config (e.g. the first room/schedule edit).
        Current = loaded.Rooms.IsMissingDefaultRoom
            ? loaded with { Rooms = loaded.Rooms with { Rooms = loaded.Rooms.Rooms.Append(new RoomGroup(RoomConfig.DefaultRoomId, RoomConfig.DefaultRoomName, Array.Empty<string>())).ToList() } }
            : loaded;

        return Current;
    }

    public async Task SaveAsync(AppConfig config, CancellationToken ct = default)
    {
        Current = config;
        await JsonFileStore.SaveAsync(_configPath, config, ct);
        ConfigChanged?.Invoke(config);
    }

    public async Task<NodeIdentity> LoadOrCreateIdentityAsync(CancellationToken ct = default)
    {
        var existing = await JsonFileStore.LoadOrDefaultAsync<NodeIdentity?>(_identityPath, () => null, ct);
        if (existing is not null)
        {
            return existing;
        }

        var created = NodeIdentity.CreateNew();
        await JsonFileStore.SaveAsync(_identityPath, created, ct);
        return created;
    }
}
