namespace Pausengong.Core.Storage;

public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pausengong");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string ConfigFilePath => Path.Combine(RootDirectory, "config.json");

    public static string SuccessionFilePath => Path.Combine(RootDirectory, "succession.json");

    public static string NodeIdentityFilePath => Path.Combine(RootDirectory, "node-id.json");

    public static string AudioDeviceSettingsFilePath => Path.Combine(RootDirectory, "audio-device.json");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
