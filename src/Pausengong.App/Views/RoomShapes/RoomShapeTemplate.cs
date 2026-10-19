using Windows.Foundation;

namespace Pausengong.App.Views.RoomShapes;

/// <summary>
/// One fixed, hand-verified room floor-plan outline for the room visualization (requested
/// 2026-10-19: "jeder Raum soll anders aussehen... auch mal Sonderheiten, nicht nur
/// rechteckig"). <see cref="Outline"/> and <see cref="ContentArea"/> are both expressed in the
/// same normalized design-space units (not necessarily 0..100 — some templates extend past 100
/// or below 0 for an outward bump, see <see cref="RoomShapeTemplates"/>), scaled uniformly to
/// the room's actual on-screen size at render time by <see cref="ViewModels.RoomViewModel"/>.
///
/// Deliberately rectilinear only (axis-aligned notches/bumps, no diagonal cuts): this keeps "is
/// ContentArea fully inside Outline" a trivial rectangle-overlap check that can be verified by
/// hand per template (see the comments in <see cref="RoomShapeTemplates"/>), which matters a lot
/// here because none of this has ever been visually rendered — this tool environment has no
/// Windows to preview WinUI3 XAML at all.
/// </summary>
public sealed record RoomShapeTemplate(string Name, IReadOnlyList<Point> Outline, Rect ContentArea)
{
    public double MinX { get; } = Outline.Min(p => p.X);
    public double MinY { get; } = Outline.Min(p => p.Y);
    public double MaxX { get; } = Outline.Max(p => p.X);
    public double MaxY { get; } = Outline.Max(p => p.Y);

    public double DesignWidth => MaxX - MinX;
    public double DesignHeight => MaxY - MinY;
}
