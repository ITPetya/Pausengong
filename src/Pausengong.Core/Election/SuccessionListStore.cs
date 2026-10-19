using Pausengong.Core.Storage;

namespace Pausengong.Core.Election;

/// <summary>
/// Local JSON cache of the succession list, kept ordered by <see cref="SuccessionOrdering"/>.
/// This is what lets a node self-promote to Chef on total network silence — the decision
/// needs no round-trip because the ordering was already agreed and cached ahead of time.
/// </summary>
public sealed class SuccessionListStore
{
    private readonly string _path;

    public SuccessionListStore(string? path = null)
    {
        _path = path ?? AppPaths.SuccessionFilePath;
    }

    public IReadOnlyList<SuccessionEntry> Current { get; private set; } = Array.Empty<SuccessionEntry>();

    public async Task<IReadOnlyList<SuccessionEntry>> LoadAsync(CancellationToken ct = default)
    {
        var loaded = await JsonFileStore.LoadOrDefaultAsync(_path, () => new List<SuccessionEntry>(), ct);
        Current = SuccessionOrdering.OrderByPriority(loaded);
        return Current;
    }

    public async Task SaveAsync(IEnumerable<SuccessionEntry> entries, CancellationToken ct = default)
    {
        Current = SuccessionOrdering.OrderByPriority(entries);
        await JsonFileStore.SaveAsync(_path, Current.ToList(), ct);
    }

    /// <summary>Adds a node or refreshes its address/host if it's already in the list.</summary>
    public Task<IReadOnlyList<SuccessionEntry>> UpsertAsync(SuccessionEntry entry, CancellationToken ct = default)
    {
        var withoutEntry = Current.Where(e => e.NodeId != entry.NodeId);
        return SaveAndReturnAsync(withoutEntry.Append(entry), ct);
    }

    public Task<IReadOnlyList<SuccessionEntry>> RemoveAsync(string nodeId, CancellationToken ct = default) =>
        SaveAndReturnAsync(Current.Where(e => e.NodeId != nodeId), ct);

    private async Task<IReadOnlyList<SuccessionEntry>> SaveAndReturnAsync(IEnumerable<SuccessionEntry> entries, CancellationToken ct)
    {
        await SaveAsync(entries, ct);
        return Current;
    }
}
