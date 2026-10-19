using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Pausengong.App.ViewModels;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Pausengong.Core.Election;
using Pausengong.Core.Election.Discovery;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;
using Pausengong.Core.Scheduling;
using Pausengong.Core.Updates;

namespace Pausengong.App;

public sealed partial class MainPage : Page
{
    private readonly IServiceProvider _services;
    private readonly MainWindow _window;

    public MainViewModel ViewModel { get; }

    // Bound to TaskbarIcon.LeftClickCommand — a plain DependencyProperty, so unlike the
    // ContextFlyout (see MainPage.xaml) it isn't affected by whatever disables that.
    public ICommand ShowFromTrayCommand { get; }

    public MainPage(IServiceProvider services, MainWindow window, UpdateCheckResult initialUpdateResult)
    {
        _services = services;
        _window = window;
        ViewModel = new MainViewModel(
            services.GetRequiredService<ConfigStore>(),
            services.GetRequiredService<ConfigSyncService>(),
            services.GetRequiredService<NodePresenceService>(),
            services.GetRequiredService<RingCoordinatorService>(),
            services.GetRequiredService<UpdateService>(),
            services.GetRequiredService<IToastService>(),
            DispatcherQueue,
            initialUpdateResult);
        ShowFromTrayCommand = new RelayCommand(_window.ShowFromTray);

        InitializeComponent();

        LogoImage.Source = new BitmapImage(new Uri(AssetExtractor.Logo256PngPath));

        // Assigning a System.Drawing.Icon (loaded synchronously from the known-good .ico already
        // used for the window/taskbar icon) instead of IconSource=BitmapImage(png): IconSource
        // goes through an async WinRT-image-decode pipeline that silently failed here (PNG isn't
        // a valid Icon source), which is why the tray icon previously had no picture.
        TrayIcon.Icon = new System.Drawing.Icon(AssetExtractor.LogoIcoPath);

        // Relying on the implicit Loaded-triggered creation was flaky on some machines (icon
        // never appeared at all); ForceCreate makes the tray icon's creation unconditional.
        TrayIcon.ForceCreate(enablesEfficiencyMode: false);

        _ = StartNetworkingAsync();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_services);
        window.Activate();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        // Must be disposed explicitly before exit, otherwise the icon can linger in the tray
        // (visible but dead) until Explorer notices the process is gone.
        TrayIcon.Dispose();
        _window.ExitApplication();
    }

    private async Task StartNetworkingAsync()
    {
        try
        {
            var configStore = _services.GetRequiredService<ConfigStore>();
            var identity = await configStore.LoadOrCreateIdentityAsync();
            await _services.GetRequiredService<AudioDeviceSettingsStore>().LoadAsync();

            var successionStore = _services.GetRequiredService<SuccessionListStore>();
            var discovery = _services.GetRequiredService<ChefDiscoveryService>();
            var broker = _services.GetRequiredService<EmbeddedBrokerService>();
            var mqttClient = _services.GetRequiredService<MqttNodeClient>();

            var electionService = new ChefElectionService(identity, App.MqttPort, successionStore, discovery, broker, mqttClient);
            await ViewModel.InitializeAsync(electionService, identity);
            await electionService.StartAsync();
        }
        catch (Exception ex)
        {
            // Network startup must never fail silently — without this, a bug here (e.g. a
            // port already in use by another instance) leaves the UI stuck forever on
            // "Netzwerk wird gesucht …" with no indication anything went wrong.
            ViewModel.NextGongTargetText = $"Netzwerk-Start fehlgeschlagen: {ex.Message}";
            var logPath = Path.Combine(Pausengong.Core.Storage.AppPaths.LogsDirectory, "unhandled-exceptions.log");
            Pausengong.Core.Storage.AppPaths.EnsureDirectoriesExist();
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O}  StartNetworkingAsync failed:{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
    }

    private void OnVersionTextTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        ViewModel.CheckForUpdatesCommand.Execute(null);

    private void OnAddRoomClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AddRoomCommand.Execute(NewRoomNameBox.Text);
        NewRoomNameBox.Text = string.Empty;
    }

    private void OnDeleteRoomClicked(object sender, RoutedEventArgs e)
    {
        var room = (ViewModels.RoomViewModel)((Button)sender).Tag;
        ViewModel.RemoveRoomCommand.Execute(room);
    }

    // The rename/delete controls only show while hovering the room name itself (requested
    // 2026-10-19) — RoomViewModel.IsHeaderHovered drives EditButtonVisibility/
    // EffectiveDeleteButtonVisibility, which MainPage.xaml binds to.
    private void OnRoomLabelPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ((ViewModels.RoomViewModel)((FrameworkElement)sender).Tag).IsHeaderHovered = true;

    private void OnRoomLabelPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ((ViewModels.RoomViewModel)((FrameworkElement)sender).Tag).IsHeaderHovered = false;

    // Inline rename (requested 2026-10-19: hovering the name should offer renaming, not just
    // deleting). The editing TextBox is two-way bound straight to RoomViewModel.Name (see
    // MainPage.xaml), so by the time any of these fire the rename has already happened locally —
    // they only need to flip IsEditingName back off and persist/sync it.
    private void OnEditRoomNameClicked(object sender, RoutedEventArgs e) =>
        ((ViewModels.RoomViewModel)((Button)sender).Tag).IsEditingName = true;

    private void OnRoomNameTextBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter && e.Key != Windows.System.VirtualKey.Escape)
        {
            return;
        }

        // Escape does not revert in-progress typing — Name is already live-bound by then, there
        // is no separate draft/cancel buffer. Disclosed simplification, not an oversight.
        var room = (ViewModels.RoomViewModel)((TextBox)sender).Tag;
        room.IsEditingName = false;
        ViewModel.CommitRoomRename(room);
    }

    private void OnRoomNameEditingLostFocus(object sender, RoutedEventArgs e)
    {
        var room = (ViewModels.RoomViewModel)((TextBox)sender).Tag;
        room.IsEditingName = false;
        ViewModel.CommitRoomRename(room);
    }

    private void OnNodeDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // The desk grid's ItemsSource is RoomViewModel.DeskSlots now (real devices + decorative
        // empty-desk placeholders, see DeskSlot.cs) — an empty placeholder (Node == null) must
        // never be draggable.
        if (e.Items.Count == 0 || e.Items[0] is not ViewModels.DeskSlot { Node: { } node })
        {
            e.Cancel = true;
            return;
        }

        e.Data.SetText(node.NodeId);
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
    }

    private async void OnRoomDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.Text))
        {
            return;
        }

        var nodeId = await e.DataView.GetTextAsync();
        // ListViewBase, not ListView: the room's device grid is a GridView now (see
        // MainPage.xaml, room visualization added 2026-10-19) — GridView and ListView share
        // this base class and the same Drag/Drop event shapes, so this handler didn't otherwise
        // need to change, just the cast.
        var room = (ViewModels.RoomViewModel)((ListViewBase)sender).Tag;
        ViewModel.MoveNodeToRoom(nodeId, room);
    }
}
