namespace Pausengong.Core.Updates;

/// <summary>Seam over however <see cref="UpdateManifest"/> actually gets fetched (HTTP against
/// the repo's <c>version.json</c> in the real implementation) so <see cref="UpdateService"/> can
/// be unit tested against a fake — same seam pattern as <c>IMqttNodeClient</c>/<c>IUdpDiscoveryTransport</c>.
/// Implementations should return <c>null</c> rather than throw on any failure (no network, bad
/// JSON, 404, ...) — <see cref="UpdateService"/> treats <c>null</c> as "nothing to report", not
/// an error, and <see cref="UpdateChecker"/> fails open on a <c>null</c> manifest.</summary>
public interface IUpdateManifestFetcher
{
    Task<UpdateManifest?> FetchAsync(CancellationToken ct = default);
}
