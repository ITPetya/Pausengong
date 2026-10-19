using System.Collections.Concurrent;
using System.Text.Json;

namespace Pausengong.Core.Storage;

/// <summary>
/// Atomic (write-to-temp-then-replace) JSON persistence for a single file, guarded per-path
/// so concurrent callers within this process don't interleave writes.
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public static async Task<T> LoadOrDefaultAsync<T>(string path, Func<T> defaultFactory, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path))
            {
                return defaultFactory();
            }

            await using var stream = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, ct);
            return value ?? defaultFactory();
        }
        catch (JsonException)
        {
            // Corrupt file (e.g. crash mid-write on an older code path) — fall back rather than crash the node.
            return defaultFactory();
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task SaveAsync<T>(string path, T value, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, ct);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }
}
