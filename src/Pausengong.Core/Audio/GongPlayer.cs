using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Pausengong.Core.Audio;

public sealed class GongPlayer : IGongPlayer
{
    private readonly string _wavFilePath;
    private readonly AudioDeviceSettingsStore _deviceSettings;

    public GongPlayer(string wavFilePath, AudioDeviceSettingsStore deviceSettings)
    {
        _wavFilePath = wavFilePath;
        _deviceSettings = deviceSettings;
    }

    public Task PlayAsync(CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new AudioFileReader(_wavFilePath);
        var enumerator = new MMDeviceEnumerator();
        var device = ResolveDevice(enumerator, _deviceSettings.Current.OutputDeviceId);
        var output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, latency: 200);
        output.Init(reader);

        var registration = ct.Register(() =>
        {
            try
            {
                output.Stop();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        output.PlaybackStopped += (_, args) =>
        {
            registration.Dispose();
            output.Dispose();
            reader.Dispose();
            device.Dispose();
            enumerator.Dispose();
            if (args.Exception is not null)
            {
                tcs.TrySetException(args.Exception);
            }
            else
            {
                tcs.TrySetResult();
            }
        };

        output.Play();
        return tcs.Task;
    }

    private static MMDevice ResolveDevice(MMDeviceEnumerator enumerator, string? deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                return enumerator.GetDevice(deviceId);
            }
            catch (Exception)
            {
                // Saved device is gone (unplugged, renamed, …) — fall back to the system default
                // rather than failing to ring at all.
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }
}
