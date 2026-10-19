using Pausengong.Core.Updates;

namespace Pausengong.Core.Tests;

internal sealed class FakeUpdateManifestFetcher : IUpdateManifestFetcher
{
    public UpdateManifest? ManifestToReturn { get; set; }
    public bool ShouldThrow { get; set; }

    public Task<UpdateManifest?> FetchAsync(CancellationToken ct = default)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated fetch failure.");
        }

        return Task.FromResult(ManifestToReturn);
    }
}
