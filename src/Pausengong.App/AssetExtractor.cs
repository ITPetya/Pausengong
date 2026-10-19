namespace Pausengong.App;

/// <summary>
/// Assets (gong.wav, icons) are embedded directly into the exe rather than shipped as loose
/// files next to it — <c>dotnet publish -p:PublishSingleFile=true</c> was found to silently
/// drop plain `Content` items from the publish output, which would otherwise mean the real
/// gong sound is simply missing from the one artifact this app is supposed to ship as. On
/// first run, embedded resources are extracted once to local app data so APIs that need a
/// real file path (AudioFileReader, AppWindow.SetIcon) still work unchanged.
/// </summary>
internal static class AssetExtractor
{
    public static string ExtractedAssetsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pausengong", "assets");

    public static string GongWavPath => Path.Combine(ExtractedAssetsDirectory, "gong.wav");
    public static string LogoIcoPath => Path.Combine(ExtractedAssetsDirectory, "Logo.ico");
    public static string Logo256PngPath => Path.Combine(ExtractedAssetsDirectory, "Logo-256.png");
    public static string Logo44PngPath => Path.Combine(ExtractedAssetsDirectory, "Logo-44.png");

    public static void EnsureExtracted()
    {
        Directory.CreateDirectory(ExtractedAssetsDirectory);
        ExtractOne("Pausengong.Assets.gong.wav", GongWavPath);
        ExtractOne("Pausengong.Assets.Logo.ico", LogoIcoPath);
        ExtractOne("Pausengong.Assets.Logo-256.png", Logo256PngPath);
        ExtractOne("Pausengong.Assets.Logo-44.png", Logo44PngPath);
    }

    private static void ExtractOne(string resourceName, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            return; // already extracted (possibly by a previous version — good enough for now)
        }

        var assembly = typeof(AssetExtractor).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var fileStream = File.Create(destinationPath);
        resourceStream.CopyTo(fileStream);
    }
}
