using Pausengong.Core.Updates;
using Xunit;

namespace Pausengong.Core.Tests;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenFetcherReturnsANewerManifest_ReportsUpdateAvailable()
    {
        var fetcher = new FakeUpdateManifestFetcher { ManifestToReturn = new UpdateManifest("9.0.0", 3, "Wichtig", null) };
        var service = new UpdateService(fetcher);

        var result = await service.CheckAsync(new Version(1, 0, 0));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(UpdateUrgency.Mandatory, result.Urgency);
    }

    [Fact]
    public async Task CheckAsync_WhenFetcherReturnsNull_IsUpToDate()
    {
        var fetcher = new FakeUpdateManifestFetcher { ManifestToReturn = null };
        var service = new UpdateService(fetcher);

        var result = await service.CheckAsync(new Version(1, 0, 0));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_WhenFetcherThrows_FailsOpenInsteadOfPropagating()
    {
        // Defense in depth: even a fetcher implementation that doesn't follow the
        // "swallow failures, return null" contract must not be able to make this throw or block
        // startup - see UpdateService's own try/catch.
        var fetcher = new FakeUpdateManifestFetcher { ShouldThrow = true };
        var service = new UpdateService(fetcher);

        var result = await service.CheckAsync(new Version(1, 0, 0));

        Assert.False(result.IsUpdateAvailable);
    }
}
