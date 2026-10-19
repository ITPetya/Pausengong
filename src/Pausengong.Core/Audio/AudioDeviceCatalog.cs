using NAudio.CoreAudioApi;

namespace Pausengong.Core.Audio;

public sealed record AudioDeviceInfo(string Id, string Name);

public static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device =>
            {
                using (device)
                {
                    return new AudioDeviceInfo(device.ID, device.FriendlyName);
                }
            })
            .ToList();
    }
}
