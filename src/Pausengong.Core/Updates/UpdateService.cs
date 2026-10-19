namespace Pausengong.Core.Updates;

/// <summary>The one thing the App layer actually calls: fetch + compare in one step. A thin
/// composition of <see cref="IUpdateManifestFetcher"/> and <see cref="UpdateChecker"/>, kept as
/// its own class so that composition itself is unit-testable against a fake fetcher (including
/// the fetcher throwing outright, not just returning null) without needing a real HTTP call.</summary>
public sealed class UpdateService
{
    private readonly IUpdateManifestFetcher _fetcher;

    public UpdateService(IUpdateManifestFetcher fetcher) => _fetcher = fetcher;

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken ct = default)
    {
        UpdateManifest? manifest;
        try
        {
            manifest = await _fetcher.FetchAsync(ct);
        }
        catch
        {
            // Defense in depth: IUpdateManifestFetcher implementations are supposed to swallow
            // their own failures and return null, but this must stay fail-open even if some
            // future/fake implementation doesn't.
            manifest = null;
        }

        return UpdateChecker.Compare(currentVersion, manifest);
    }
}
