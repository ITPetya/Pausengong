namespace Pausengong.Core.Updates;

/// <summary>Result of comparing the running app's version against <see cref="UpdateManifest"/> —
/// see <see cref="UpdateChecker.Compare"/>.</summary>
public sealed record UpdateCheckResult(bool IsUpdateAvailable, string? LatestVersion, UpdateUrgency Urgency, string? Message, string? DownloadUrl)
{
    public static UpdateCheckResult UpToDate { get; } = new(false, null, UpdateUrgency.Informational, null, null);
}
