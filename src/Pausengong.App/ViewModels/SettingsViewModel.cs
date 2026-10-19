using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;

namespace Pausengong.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigStore _configStore;
    private readonly ConfigSyncService _configSync;
    private readonly IGongPlayer _gongPlayer;
    private readonly AudioDeviceSettingsStore _audioDeviceSettings;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _suppressPersist;
    private bool _suppressDevicePersist;

    [ObservableProperty]
    private bool _testModeActive = true;

    [ObservableProperty]
    private bool _isTestGongPlaying;

    [ObservableProperty]
    private AudioDeviceInfo? _selectedAudioOutputDevice;

    public ObservableCollection<TimeOnly> BreakTimes { get; } = new();

    // Local to this PC/user only — deliberately not part of AppConfig, so it is never synced
    // to other nodes over MQTT (see AudioDeviceSettingsStore).
    public ObservableCollection<AudioDeviceInfo> AudioOutputDevices { get; } = new();

    public SettingsViewModel(
        ConfigStore configStore,
        ConfigSyncService configSync,
        IGongPlayer gongPlayer,
        AudioDeviceSettingsStore audioDeviceSettings,
        DispatcherQueue dispatcherQueue)
    {
        _configStore = configStore;
        _configSync = configSync;
        _gongPlayer = gongPlayer;
        _audioDeviceSettings = audioDeviceSettings;
        _dispatcherQueue = dispatcherQueue;
        _configStore.ConfigChanged += OnConfigChanged;
    }

    public void LoadFromCurrentConfig()
    {
        ApplyFromConfig(_configStore.Current);
        LoadAudioOutputDevices();
    }

    private void LoadAudioOutputDevices()
    {
        _suppressDevicePersist = true;
        AudioOutputDevices.Clear();
        foreach (var device in AudioDeviceCatalog.GetOutputDevices())
        {
            AudioOutputDevices.Add(device);
        }

        var savedDeviceId = _audioDeviceSettings.Current.OutputDeviceId;
        SelectedAudioOutputDevice = AudioOutputDevices.FirstOrDefault(d => d.Id == savedDeviceId);
        _suppressDevicePersist = false;
    }

    partial void OnSelectedAudioOutputDeviceChanged(AudioDeviceInfo? value)
    {
        if (_suppressDevicePersist)
        {
            return;
        }

        _ = _audioDeviceSettings.SaveAsync(new AudioDeviceSettings(value?.Id));
    }

    private void OnConfigChanged(AppConfig config)
    {
        // Reflect changes that arrived from another PC over the network while this window is open.
        _dispatcherQueue.TryEnqueue(() => ApplyFromConfig(config));
    }

    private void ApplyFromConfig(AppConfig config)
    {
        _suppressPersist = true;
        TestModeActive = config.TestMode.IsActive;
        BreakTimes.Clear();
        foreach (var time in config.Schedule.BreakTimes.OrderBy(t => t))
        {
            BreakTimes.Add(time);
        }

        _suppressPersist = false;
    }

    partial void OnTestModeActiveChanged(bool value)
    {
        if (_suppressPersist)
        {
            return;
        }

        _ = _configSync.UpdateTestModeAsync(value);
    }

    [RelayCommand]
    private void AddBreakTime(TimeSpan time)
    {
        var value = TimeOnly.FromTimeSpan(time);
        if (BreakTimes.Contains(value))
        {
            return;
        }

        var insertIndex = BreakTimes.Count;
        for (var i = 0; i < BreakTimes.Count; i++)
        {
            if (BreakTimes[i] > value)
            {
                insertIndex = i;
                break;
            }
        }

        BreakTimes.Insert(insertIndex, value);
        PersistBreakTimes();
    }

    [RelayCommand]
    private void RemoveBreakTime(TimeOnly time)
    {
        BreakTimes.Remove(time);
        PersistBreakTimes();
    }

    private void PersistBreakTimes()
    {
        if (_suppressPersist)
        {
            return;
        }

        _ = _configSync.UpdateScheduleAsync(BreakTimes.ToList());
    }

    [RelayCommand]
    private async Task TestGongAsync()
    {
        // Deliberately bypasses the test-mode gate: this button always plays real audio on
        // this PC only, so a user can verify their own speaker setup on demand.
        if (IsTestGongPlaying)
        {
            return;
        }

        IsTestGongPlaying = true;
        try
        {
            await _gongPlayer.PlayAsync();
        }
        finally
        {
            IsTestGongPlaying = false;
        }
    }
}
