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

    // "Building" layout (requested 2026-10-19: "wie ein zusammenhängendes Gebäude... rundrum um
    // Räume erweitert", confirmed explicitly over a simpler row-grid alternative after that grid
    // turned out to look identical to the old single-strip layout for the maintainer's actual
    // 1-2-room test case): rooms spiral out around the first one — see RebuildRoomLayout/
    // SpiralPositions below. Each room occupies one fixed-size cell in an infinite grid; the cell
    // is bigger than MaxSize (RoomViewModel) specifically so a room's own size changing never
    // requires repacking neighbors — only Rooms.Count changing (add/remove) does.
    private const double LayoutCellSize = 580;
    private const double LayoutDoorInset = 30;
    private const double LayoutDoorSize = 20;

    public ObservableCollection<RoomViewModel> Rooms { get; } = new();

    /// <summary>One entry per room with its spiral position — rebuilt wholesale on every
    /// <see cref="Rooms"/> change (add/remove/clear all route through the one
    /// <see cref="ObservableCollection{T}.CollectionChanged"/> subscription in the constructor).
    /// A plain rebuild, not an incremental update: room counts here are small (an office/school's
    /// worth of rooms), so this is cheap enough to not be worth patching in place.</summary>
    public ObservableCollection<RoomLayoutEntry> RoomLayout { get; } = new();

    /// <summary>Explicit size for the <c>Canvas</c> hosting <see cref="RoomLayout"/> in
    /// MainPage.xaml — a <c>Canvas</c> does not auto-size to children positioned via
    /// <c>Canvas.Left</c>/<c>Canvas.Top</c>, so without this the surrounding
    /// <c>ScrollViewer</c> would have no idea how much there is to scroll to.</summary>
    [ObservableProperty]
    private double _roomLayoutCanvasWidth;

    [ObservableProperty]
    private double _roomLayoutCanvasHeight;

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
        Rooms.CollectionChanged += (_, _) => RebuildRoomLayout();

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

    /// <summary>
    /// Rebuilds <see cref="RoomLayout"/>: spiral grid positions for <see cref="SpiralPositions"/>,
    /// converted to pixels, plus a connection-door marker to the next room (geometrically
    /// adjacent by construction — <see cref="SpiralPositions"/> only ever steps exactly one cell
    /// in one of the four cardinal directions, so <c>(next.x - cur.x, next.y - cur.y)</c> is
    /// always one of (1,0)/(-1,0)/(0,1)/(0,-1), never a diagonal jump or anything larger).
    /// </summary>
    private void RebuildRoomLayout()
    {
        RoomLayout.Clear();
        if (Rooms.Count == 0)
        {
            RoomLayoutCanvasWidth = 0;
            RoomLayoutCanvasHeight = 0;
            return;
        }

        var cells = SpiralPositions(Rooms.Count);
        var minX = cells.Min(c => c.X);
        var maxX = cells.Max(c => c.X);
        var minY = cells.Min(c => c.Y);
        var maxY = cells.Max(c => c.Y);

        for (var i = 0; i < Rooms.Count; i++)
        {
            var left = (cells[i].X - minX) * LayoutCellSize;
            var top = (cells[i].Y - minY) * LayoutCellSize;

            var hasDoor = i + 1 < Rooms.Count;
            var doorLeft = 0.0;
            var doorTop = 0.0;
            if (hasDoor)
            {
                var dx = cells[i + 1].X - cells[i].X;
                var dy = cells[i + 1].Y - cells[i].Y;

                // Centered on whichever edge of this room's cell faces the next room. See the
                // method doc above for why (dx, dy) is always exactly one of these four pairs,
                // and HANDOFF.md "Room visualization: spiral layout" for the hand-worked proof
                // that none of these four branches can ever produce a negative coordinate or one
                // past the canvas's own computed width/height — not just assumed, actually
                // checked, since this is the one thing in this whole visualization effort that's
                // pure arithmetic and didn't need a screenshot to verify.
                (doorLeft, doorTop) = (dx, dy) switch
                {
                    (1, 0) => (left + LayoutCellSize - LayoutDoorInset, top + (LayoutCellSize / 2) - (LayoutDoorSize / 2)),
                    (-1, 0) => (left - LayoutDoorInset, top + (LayoutCellSize / 2) - (LayoutDoorSize / 2)),
                    (0, 1) => (left + (LayoutCellSize / 2) - (LayoutDoorSize / 2), top + LayoutCellSize - LayoutDoorInset),
                    (0, -1) => (left + (LayoutCellSize / 2) - (LayoutDoorSize / 2), top - LayoutDoorInset),
                    _ => (left, top), // unreachable - see the method doc above
                };
            }

            RoomLayout.Add(new RoomLayoutEntry(Rooms[i], left, top, doorLeft, doorTop, hasDoor));
        }

        RoomLayoutCanvasWidth = (maxX - minX + 1) * LayoutCellSize;
        RoomLayoutCanvasHeight = (maxY - minY + 1) * LayoutCellSize;
    }

    /// <summary>
    /// Grid cell (not pixel) coordinates for the first <paramref name="count"/> rooms in an
    /// outward spiral starting at the origin — a classic "walk" spiral: step in the current
    /// direction (right/down/left/up, in that cyclic order) for the current leg length, turn 90°
    /// clockwise, and increase the leg length by one every second turn (1,1,2,2,3,3,4,4,...).
    /// Hand-traced for count=9 while writing this (see HANDOFF.md "Room visualization: spiral
    /// layout") and confirmed it fills a 3x3 grid around the origin exactly once with no gaps or
    /// overlaps — the one piece of this whole visualization effort actually verified by working
    /// through the arithmetic, not just reasoned about.
    /// </summary>
    private static List<(int X, int Y)> SpiralPositions(int count)
    {
        var positions = new List<(int X, int Y)> { (0, 0) };
        if (count <= 1)
        {
            return positions;
        }

        var directions = new (int Dx, int Dy)[] { (1, 0), (0, 1), (-1, 0), (0, -1) };
        var x = 0;
        var y = 0;
        var dirIndex = 0;
        var stepsInCurrentLeg = 0;
        var legLength = 1;
        var legsCompletedAtThisLength = 0;

        for (var i = 1; i < count; i++)
        {
            if (stepsInCurrentLeg == legLength)
            {
                dirIndex = (dirIndex + 1) % 4;
                stepsInCurrentLeg = 0;
                legsCompletedAtThisLength++;
                if (legsCompletedAtThisLength == 2)
                {
                    legLength++;
                    legsCompletedAtThisLength = 0;
                }
            }

            var (dx, dy) = directions[dirIndex];
            x += dx;
            y += dy;
            stepsInCurrentLeg++;
            positions.Add((x, y));
        }

        return positions;
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
    /// <summary>Presence data changed — refresh display labels everywhere a BETTER one is
    /// actually available, and land any newly seen node in the default room.
    ///
    /// Bug fixed 2026-10-19 (real report: "der alte PC... wird immer noch mit einer komischen
    /// Nummer angezeigt"): this used to call <see cref="ResolveLabel"/> unconditionally for
    /// every node in every room on every presence tick — including a node that just went
    /// offline. <see cref="ResolveLabel"/> falls back to the raw <c>NodeId</c> (a GUID-looking
    /// string — the "komische Nummer") once a node drops out of
    /// <see cref="Presence.NodePresenceService.KnownNodes"/>, so a device's nice hostname label
    /// was actively being *downgraded* to that raw id the moment it went offline, not just left
    /// stale. Room membership itself is untouched here — an offline PC correctly keeps its room
    /// assignment so it's still in the right place once it reconnects; only the DISPLAY LABEL is
    /// now sticky, never regressing to something worse than what was already shown.</summary>
    private void OnPresenceNodesChanged()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (var room in Rooms)
            {
                foreach (var node in room.Nodes)
                {
                    var known = _nodePresence.KnownNodes.FirstOrDefault(n => n.NodeId == node.NodeId);
                    if (known is not null)
                    {
                        node.DisplayLabel = known.DisplayLabel;
                    }
                    // else: no fresh info (node currently offline/unknown) - keep whatever label
                    // is already shown rather than regress it to the raw node id.
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
