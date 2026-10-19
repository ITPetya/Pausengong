using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Pausengong.App.ViewModels;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;

namespace Pausengong.App;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(IServiceProvider services, DispatcherQueue dispatcherQueue)
    {
        ViewModel = new SettingsViewModel(
            services.GetRequiredService<ConfigStore>(),
            services.GetRequiredService<ConfigSyncService>(),
            services.GetRequiredService<IGongPlayer>(),
            services.GetRequiredService<AudioDeviceSettingsStore>(),
            dispatcherQueue);
        InitializeComponent();
        ViewModel.LoadFromCurrentConfig();
    }

    private void OnAddBreakTimeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.AddBreakTimeCommand.Execute(NewBreakTimePicker.Time);
    }

    private void OnRemoveBreakTimeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (BreakTimesList.SelectedItem is TimeOnly time)
        {
            ViewModel.RemoveBreakTimeCommand.Execute(time);
        }
    }
}
