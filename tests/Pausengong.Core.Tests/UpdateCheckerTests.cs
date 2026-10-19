using Pausengong.Core.Updates;
using Xunit;

namespace Pausengong.Core.Tests;

public class UpdateCheckerTests
{
    private static readonly Version Current = new(1, 2, 0);

    [Fact]
    public void Compare_WithNullManifest_IsUpToDate()
    {
        var result = UpdateChecker.Compare(Current, null);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void Compare_WithUnparsableLatestVersion_IsUpToDate()
    {
        var manifest = new UpdateManifest("not-a-version", 1, null, null);

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void Compare_WithLatestVersionEqualToCurrent_IsUpToDate()
    {
        var manifest = new UpdateManifest("1.2.0", 1, null, null);

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void Compare_WithLatestVersionOlderThanCurrent_IsUpToDate()
    {
        // Can happen during local dev (running a newer build than what's published) - must never
        // be treated as "you need to update".
        var manifest = new UpdateManifest("1.0.0", 3, null, null);

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void Compare_WithNewerLatestVersion_ReportsUpdateAvailableWithManifestDetails()
    {
        var manifest = new UpdateManifest("1.3.0", 2, "Neue Features", "https://example.invalid/download");

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.3.0", result.LatestVersion);
        Assert.Equal(UpdateUrgency.Important, result.Urgency);
        Assert.Equal("Neue Features", result.Message);
        Assert.Equal("https://example.invalid/download", result.DownloadUrl);
    }

    [Theory]
    [InlineData(1, UpdateUrgency.Informational)]
    [InlineData(2, UpdateUrgency.Important)]
    [InlineData(3, UpdateUrgency.Mandatory)]
    public void Compare_MapsUrgencyScoreToUrgency(int score, UpdateUrgency expected)
    {
        var manifest = new UpdateManifest("1.9.0", score, null, null);

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.Equal(expected, result.Urgency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(4)]
    [InlineData(99)]
    public void Compare_ClampsOutOfRangeUrgencyScores_InsteadOfRejectingTheManifest(int score)
    {
        var manifest = new UpdateManifest("1.9.0", score, null, null);

        var result = UpdateChecker.Compare(Current, manifest);

        Assert.True(result.IsUpdateAvailable);
        Assert.True(Enum.IsDefined(result.Urgency));
    }
}
