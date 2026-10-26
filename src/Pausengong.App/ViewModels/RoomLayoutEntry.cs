using Microsoft.UI.Xaml;

namespace Pausengong.App.ViewModels;

/// <summary>
/// One room's position in the "building" layout — a spiral around the first room (requested and
/// chosen explicitly by the maintainer 2026-10-19, over a simpler row-grid that turned out to
/// look identical to the old single-strip layout whenever there were only 1-2 rooms, which was
/// most of the time in practice). See <see cref="MainViewModel.RoomLayout"/> for how this is
/// computed.
///
/// <see cref="DoorLeft"/>/<see cref="DoorTop"/>/<see cref="DoorVisibility"/> mark the connection
/// to the NEXT room in creation order (always geometrically adjacent to this one — see
/// <see cref="MainViewModel"/>'s spiral-walk comment for why that holds by construction) with a
/// plain, orientation-agnostic dashed-square marker rather than a rotated directional symbol —
/// simplified deliberately: a <c>RotateTransform</c> bound per-direction is one more thing that
/// could be subtly wrong and unverifiable here (no Windows in this tool environment), for a
/// decorative detail that doesn't need to be direction-precise to read as "these two connect
/// here".
/// </summary>
public sealed record RoomLayoutEntry(
    RoomViewModel Room,
    double Left,
    double Top,
    double DoorLeft,
    double DoorTop,
    bool HasDoorToNext)
{
    public Visibility DoorVisibility => HasDoorToNext ? Visibility.Visible : Visibility.Collapsed;
}
