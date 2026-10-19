namespace Pausengong.Core.Updates;

/// <summary>
/// The small JSON document that <see cref="IUpdateManifestFetcher"/> fetches — maintained by
/// hand in the repo (<c>version.json</c> at the repo root) and bumped together with
/// <c>Pausengong.App.csproj</c>'s <c>&lt;Version&gt;</c> on every release. <see cref="LatestVersion"/>
/// must parse via <see cref="System.Version.TryParse(string, out System.Version)"/> (plain
/// "major.minor.patch", no "v" prefix). <see cref="UrgencyScore"/> is 1-3 (see
/// <see cref="UpdateUrgency"/>) — out-of-range values are clamped, not rejected, by
/// <see cref="UpdateChecker"/>, so a typo never accidentally disables the whole mechanism.
/// </summary>
public sealed record UpdateManifest(string LatestVersion, int UrgencyScore, string? Message, string? DownloadUrl);
