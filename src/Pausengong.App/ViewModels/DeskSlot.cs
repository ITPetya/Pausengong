namespace Pausengong.App.ViewModels;

/// <summary>
/// One cell in a room's desk grid — either a real device (<see cref="Node"/> set, the actual
/// drag/drop payload) or a decorative, non-draggable placeholder (<see cref="Node"/> <c>null</c>,
/// use <see cref="Empty"/>) added purely so a room always shows "several empty desks" (requested
/// 2026-10-19), never a bare handful of occupied ones in a mostly-blank room.
///
/// One concrete type with a nullable <see cref="Node"/> rather than an occupied/empty type
/// hierarchy — deliberately: a single <c>DataTemplate</c> with plain property bindings is much
/// lower-risk to get right blind (no WinUI3 available anywhere this was written, see HANDOFF.md)
/// than an <c>ItemTemplateSelector</c> or <c>x:Bind</c> casting between subtypes would be.
/// <see cref="RoomViewModel.Nodes"/> remains the one real, persisted, draggable/droppable
/// collection — <see cref="RoomViewModel.DeskSlots"/> is a derived, display-only view over it
/// plus padding, never the other way around.
/// </summary>
public sealed record DeskSlot(NodeListItem? Node)
{
    public static DeskSlot Empty { get; } = new(null);

    public bool IsOccupied => Node is not null;

    /// <summary>Dims empty placeholder desks relative to real occupied ones.</summary>
    public double VisualOpacity => IsOccupied ? 1.0 : 0.35;
}
