namespace Pausengong.Core.Updates;

/// <summary>
/// Pure comparison logic, deliberately separate from fetching (<see cref="IUpdateManifestFetcher"/>)
/// so it's unit-testable without any network involved. Fails open on every ambiguous input —
/// missing manifest, unparsable version, current version already newer than "latest" (can happen
/// during local dev) — all resolve to <see cref="UpdateCheckResult.UpToDate"/> rather than ever
/// defaulting to "update needed"/mandatory. This matters: Pausengong is a LAN-only break-bell
/// system that must keep working with no internet access at all, so a failed/garbled fetch must
/// never be treated as a reason to nag, let alone block startup.
/// </summary>
public static class UpdateChecker
{
    public static UpdateCheckResult Compare(Version currentVersion, UpdateManifest? manifest)
    {
        if (manifest is null || !Version.TryParse(manifest.LatestVersion, out var latest))
        {
            return UpdateCheckResult.UpToDate;
        }

        if (latest <= currentVersion)
        {
            return UpdateCheckResult.UpToDate;
        }

        var urgency = (UpdateUrgency)Math.Clamp(manifest.UrgencyScore, (int)UpdateUrgency.Informational, (int)UpdateUrgency.Mandatory);
        return new UpdateCheckResult(IsUpdateAvailable: true, manifest.LatestVersion, urgency, manifest.Message, manifest.DownloadUrl);
    }
}
