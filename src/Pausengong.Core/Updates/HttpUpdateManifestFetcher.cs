using System.Text.Json;

namespace Pausengong.Core.Updates;

/// <summary>
/// Fetches <see cref="UpdateManifest"/> as plain JSON over HTTPS (no auth — <c>version.json</c>
/// is a public file in the repo, fetched via the raw-content URL). Deliberately swallows every
/// failure (network unreachable, non-200, malformed JSON) and returns <c>null</c> instead of
/// throwing — see <see cref="IUpdateManifestFetcher"/> for why that matters here.
/// </summary>
public sealed class HttpUpdateManifestFetcher : IUpdateManifestFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly Uri _manifestUrl;

    public HttpUpdateManifestFetcher(HttpClient httpClient, Uri manifestUrl)
    {
        _httpClient = httpClient;
        _manifestUrl = manifestUrl;
    }

    public async Task<UpdateManifest?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_manifestUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        }
        catch
        {
            // Must never propagate: no internet, a GitHub outage, or a malformed manifest file
            // must never be the reason Pausengong fails to start or rings late. See
            // IUpdateManifestFetcher / UpdateChecker for the fail-open reasoning.
            return null;
        }
    }
}
