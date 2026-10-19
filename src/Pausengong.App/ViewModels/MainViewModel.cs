using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Pausengong.Core.Election;
using Pausengong.Core.Models;
using Pausengong.Core.Presence;
using Pausengong.Core.Scheduling;
using Pausengong.Core.Updates;

namespace Pausengong.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigStore _configStore;
    private readonly ConfigSyncService _configSync;
    private readonly NodePresenceService _nodePresence;
    private readonly RingCoordinatorService _ringCoordinator;
    private readonly UpdateService _updateService;
    private readonly IToastService _toastService;
    private readonly Version _currentVersion;
    private readonly DispatcherQueue _dispatcherQueue;
    private ChefElectionService? _electionService;
    private NodeIdentity? _identity;

    /// <summary>"v1.0.0" — shown small/grey in the bottom-right corner (see MainPage.xaml),
    /// clickable to re-check (<see cref="CheckForUpdatesCommand"/>). Always shows the currently
    /// running version, independent of whether an update is available.</summary>
    [ObservableProperty]
    private string _currentVersionText = "v?";

    /// <summary>Backs the update notice <c>InfoBar</c> in MainPage.xaml. Never opened for
    /// <see cref="UpdateUrgency.Mandatory"/> — that case is handled before the main window even
    /// loads, see <c>App.xaml.cs</c>/<c>UpdateRequiredWindow</c>.</summary>
    [ObservableProperty]
    private bool _updateBannerIsOpen;

    [ObservableProperty]
    private string _updateBannerMessage = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _updateBannerSeverity = InfoBarSeverity.Informational;

    /// <summary>"Nächster Gong um: HH:mm Uhr — Aktueller Host: Name (IP)" — always-on status strip.</summary>
    [ObservableProperty]
    private string _statusBannerText = "Netzwerk wird gesucht …";

    /// <summary>Which node currently rings for which room — the live pick from
    /// <see cref="RingCoordinatorService"/>'s draw (per room, "RoomName: NodeLabel"), or who
    /// actually rang last time once a cycle resolves. Empty until the first draw/ring happens
    /// (draws only run starting 60s before a scheduled break time).</summary>
    [ObservableProperty]
    private string _nextGongTargetText = "–";

    [ObservableProperty]
    private string _selfNodeLabel = Environment.MachineName;

    private string _currentHostLabel = "wird ermittelt …";
    private DispatcherQueueTimer? _bannerTimer;

    public ObservableCollection<RoomViewModel> Rooms { get; } = new();

    public MainViewModel(
        ConfigStore configStore,
        ConfigSyncService configSync,
        NodePresenceService nodePresence,
        RingCoordinatorService ringCoordinator,
        UpdateService updateService,
        IToastService toastService,
        DispatcherQueue dispatcherQueue,
        UpdateCheckResult initialUpdateResult)
    {
        _configStore = configStore;
        _configSync = configSync;
        _nodePresence = nodePresence;
        _ringCoordinator = ringCoordinator;
        _updateService = updateService;
        _toastService = toastService;
        _dispatcherQueue = dispatcherQueue;
        _configStore.ConfigChanged += OnConfigChanged;
        _nodePresence.NodesChanged += OnPresenceNodesChanged;
        _ringCoordinator.DrawPreviewChanged += OnDrawPreviewChanged;

        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        CurrentVersionText = "v" + _currentVersion.ToString(3);
        ApplyUpdateResult(initialUpdateResult); // the check already ran once in App.xaml.cs before this window was shown

        _bannerTimer = _dispatcherQueue.CreateTimer();
        _bannerTimer.Interval = TimeSpan.FromSeconds(30);
        _bannerTimer.Tick += (_, _) => RefreshStatusBanner();
    }

    public async Task InitializeAsync(ChefElectionService electionService, NodeIdentity identity)
    {
        _electionService = electionService;
        _identity = identity;
        SelfNodeLabel = identity.HostName;

        var config = await _configStore.LoadAsync();
        ApplyRooms(config.Rooms);
        RefreshStatusBanner();
        _bannerTimer?.Start();

        electionService.ChefChanged += OnChefChanged;
    }

    /// <summary>Bound to the version text's Tapped handler in MainPage.xaml — re-checks right
    /// now and always reports something back (an InfoBar for an available update, a toast if
    /// already up to date), unlike <see cref="RefreshUpdateStatusSilentlyAsync"/>.</summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        var result = await _updateService.CheckAsync(_currentVersion);
        ApplyUpdateResult(result);

        if (!result.IsUpdateAvailable)
        {
            await _toastService.ShowSimpleNotificationAsync("Pausengong ist aktuell", CurrentVersionText);
        }
    }

    /// <summary>Called when the window is (re)shown from the tray (see MainWindow.ShowFromTray) —
    /// same check, but doesn't nag with an "up to date" toast every single time someone reopens
    /// the window; an available update still opens the InfoBar either way.</summary>
    public async Task RefreshUpdateStatusSilentlyAsync()
    {
        var result = await _updateService.CheckAsync(_currentVersion);
        ApplyUpdateResult(result);
    }

    private void ApplyUpdateResult(UpdateCheckResult result)
    {
        if (!result.IsUpdateAvailable)
        {
            UpdateBannerIsOpen = false;
            return;
        }

        // Mandatory is handled before the main window ever loads (App.xaml.cs /
        // UpdateRequiredWindow) — if one shows up here anyway (e.g. discovered by a manual
        // re-check while already running), it's still just the strongest non-blocking banner,
        // since the app is already running at this point and can't retroactively un-load.
        UpdateBannerSeverity = result.Urgency switch
        {
            UpdateUrgency.Mandatory => InfoBarSeverity.Error,
            UpdateUrgency.Important => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Informational,
        };
        UpdateBannerMessage = string.IsNullOrWhiteSpace(result.Message)
            ? $"Update verfügbar: v{result.LatestVersion}"
            : $"Update verfügbar: v{result.LatestVersion} — {result.Message}";
        UpdateBannerIsOpen = true;
    }

    private void OnConfigChanged(AppConfig config)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            ApplyRooms(config.Rooms);
            RefreshStatusBanner();
        });
    }

    /// <summary>Recomputes the always-on status strip text — called on a timer (so the "next
    /// gong" time keeps advancing) and whenever the schedule or the current Chef changes.</summary>
    private void RefreshStatusBanner()
    {
        StatusBannerText = $"Nächster Gong um: {NextBreakTimeText()} Uhr — Aktueller Host: {_currentHostLabel}";
    }

    private string NextBreakTimeText()
    {
        var breakTimes = _configStore.Current.Schedule.BreakTimes;
        if (breakTimes.Count == 0)
        {
            return "–";
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var todaysNext = breakTimes.Where(t => t > now).OrderBy(t => t).Select(t => (TimeOnly?)t).FirstOrDefault();
        if (todaysNext is { } next)
        {
            return next.ToString("HH:mm");
        }

        var tomorrowsNext = breakTimes.OrderBy(t => t).First(); // today's times are all past
        return $"morgen {tomorrowsNext:HH:mm}";
    }

    /// <summary>Rebuilds Rooms from config — used both on load and when a newer room layout
    /// arrives from another PC over the network.</summary>
    private void ApplyRooms(RoomConfig roomConfig)
    {
        Rooms.Clear();
        foreach (var room in roomConfig.Rooms)
        {
            var vm = new RoomViewModel(room.RoomId, room.Name);
            foreach (var nodeId in room.NodeIds)
            {
                vm.Nodes.Add(new NodeListItem(nodeId, ResolveLabel(nodeId)));
            }

            Rooms.Add(vm);
        }

        AssignUnknownNodesToDefaultRoom();
    }

    /// <summary>Hostname (+ IP, when known) for a node id — from live presence data if we have
    /// it, otherwise the raw id, so a node is never silently hidden just because its status
    /// hasn't arrived yet (e.g. a peer still on an older build, see NodePresenceService).</summary>
    private string ResolveLabel(string nodeId)
    {
        var known = _nodePresence.KnownNodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (known is not null)
        {
            return known.DisplayLabel;
        }

        return _identity is not null && nodeId == _identity.NodeId ? _identity.HostName : nodeId;
    }

    /// <summary>Assigns every node currently known (self + whatever <see cref="NodePresenceService"/>
    /// has heard on the network) that isn't already sitting in some room to the always-present
    /// default landing room (<see cref="RoomConfig.DefaultRoomId"/>) — this is what a newly seen
    /// node lands in instead of a separate "unassigned" list. Persists only when it actually
    /// moved someone, so this is safe to call on every presence tick. Called on room-config
    /// changes and whenever presence changes.</summary>
    private void AssignUnknownNodesToDefaultRoom()
    {
        var assignedNodeIds = new HashSet<string>(Rooms.SelectMany(r => r.Nodes.Select(n => n.NodeId)));

        var knownNodeIds = new HashSet<string>(_nodePresence.KnownNodes.Select(n => n.NodeId));
        if (_identity is not null)
        {
            knownNodeIds.Add(_identity.NodeId);
        }

        var defaultRoom = FindOrCreateDefaultRoomVm();
        var addedAny = false;
        foreach (var nodeId in knownNodeIds)
        {
            if (!assignedNodeIds.Contains(nodeId))
            {
                defaultRoom.Nodes.Add(new NodeListItem(nodeId, ResolveLabel(nodeId)));
                addedAny = true;
            }
        }

        if (addedAny)
        {
            PersistRooms();
        }
    }

    /// <summary>The always-present landing room — created on the fly if this is the very first
    /// room-config load and it somehow isn't there yet (normally <see cref="ConfigStore"/>
    /// already guarantees it, see <see cref="RoomConfig.IsMissingDefaultRoom"/>).</summary>
    private RoomViewModel FindOrCreateDefaultRoomVm()
    {
        var existing = Rooms.FirstOrDefault(r => r.RoomId == RoomConfig.DefaultRoomId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new RoomViewModel(RoomConfig.DefaultRoomId, RoomConfig.DefaultRoomName);
        Rooms.Insert(0, created);
        return created;
    }

    /// <summary>Presence data changed — refresh display labels everywhere (a node's hostname/IP
    /// may have just arrived) and land any newly seen node in the default room.</summary>
    private void OnPresenceNodesChanged()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (var room in Rooms)
            {
                foreach (var node in room.Nodes)
                {
                    node.DisplayLabel = ResolveLabel(node.NodeId);
                }
            }

            AssignUnknownNodesToDefaultRoom();
        });
    }

    private void OnDrawPreviewChanged(IReadOnlyList<DrawPreviewEntry> preview)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            NextGongTargetText = preview.Count == 0
                ? "–"
                : string.Join(" · ", preview.Select(p => $"{p.RoomName}: {p.NodeLabel}"));
        });
    }

    private void OnChefChanged(SuccessionEntry chef)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _currentHostLabel = $"{chef.HostName} ({chef.Address})";
            RefreshStatusBanner();
        });

        _ = _configSync.OnConnectedAsync();
        if (_identity is not null)
        {
            _ = _nodePresence.OnConnectedAsync(_identity);
            _ = _ringCoordinator.StartAsync(_identity, () => _electionService?.IsSelfChief ?? false);
        }
    }

    [RelayCommand]
    private void AddRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            return;
        }

        var roomId = "room-" + Guid.NewGuid().ToString("N")[..8];
        Rooms.Add(new RoomViewModel(roomId, roomName.Trim()));
        PersistRooms();
    }

    /// <summary>Deletes a room and moves its nodes into the default landing room instead of
    /// dropping them into a separate "unassigned" concept — there isn't one any more. The
    /// default room itself can't be deleted (there'd be nowhere left for nodes to land).</summary>
    [RelayCommand]
    private void RemoveRoom(RoomViewModel room)
    {
        if (room is null || room.IsDefaultRoom || !Rooms.Remove(room))
        {
            return;
        }

        var defaultRoom = FindOrCreateDefaultRoomVm();
        foreach (var node in room.Nodes)
        {
            defaultRoom.Nodes.Add(node);
        }

        PersistRooms();
    }

    /// <summary>Moves a node between rooms via drag/drop. A null <paramref name="targetRoom"/>
    /// (no explicit drop target) falls back to the default landing room, same as a brand new
    /// node would.</summary>
    public void MoveNodeToRoom(string nodeId, RoomViewModel? targetRoom)
    {
        var label = ResolveLabel(nodeId);

        foreach (var room in Rooms)
        {
            var existing = room.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (existing is not null)
            {
                room.Nodes.Remove(existing);
            }
        }

        (targetRoom ?? FindOrCreateDefaultRoomVm()).Nodes.Add(new NodeListItem(nodeId, label));

        PersistRooms();
    }

    /// <summary>Called once an inline room rename finishes (Enter or losing focus — see
    /// MainPage.xaml.cs). <paramref name="room"/>'s <c>Name</c> is already updated by then (the
    /// editing <c>TextBox</c> is two-way bound straight to it); this just persists/syncs it,
    /// same as every other room-list mutation.</summary>
    public void CommitRoomRename(RoomViewModel room) => PersistRooms();

    private void PersistRooms()
    {
        var groups = Rooms.Select(r => new RoomGroup(r.RoomId, r.Name, r.Nodes.Select(n => n.NodeId).ToList())).ToList();
        _ = _configSync.UpdateRoomsAsync(groups);
    }
}
