using Windows.Foundation;

namespace Pausengong.App.Views.RoomShapes;

/// <summary>
/// The fixed set of room outlines a <see cref="ViewModels.RoomViewModel"/> picks from
/// (deterministically, by room id — see <c>RoomViewModel.StableHash</c>). Every notch/bump below
/// is a plain axis-aligned rectangle cut from or added to a 0..100 base square specifically so
/// "does ContentArea overlap this notch" is a trivial min/max comparison, not a line-intersection
/// problem — see <see cref="RoomShapeTemplate"/> for why that matters here. Each template's
/// comment states which rectangle(s) were cut/added and why the stated <c>ContentArea</c> cannot
/// overlap them.
/// </summary>
public static class RoomShapeTemplates
{
    public static IReadOnlyList<RoomShapeTemplate> All { get; } = new[]
    {
        // Plain rectangle — the baseline "normal" room.
        new RoomShapeTemplate(
            "Rechteck",
            new[] { new Point(0, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100) },
            new Rect(10, 10, 80, 80)),

        // Cuts a 25x25 square from the top-right corner: notch = x[75,100] * y[0,25].
        // ContentArea y starts at 30 (> 25), so it can't overlap the notch at any x.
        new RoomShapeTemplate(
            "Eckige Nische oben rechts",
            new[] { new Point(0, 0), new Point(75, 0), new Point(75, 25), new Point(100, 25), new Point(100, 100), new Point(0, 100) },
            new Rect(10, 30, 80, 60)),

        // Cuts 25x25 from top-right (x[75,100]*y[0,25]) AND bottom-left (x[0,25]*y[75,100]).
        // ContentArea y in [30,70] clears both: 30 > 25 (top notch's max y) and 70 < 75 (bottom
        // notch's min y), regardless of x.
        new RoomShapeTemplate(
            "Zwei Ecken versetzt",
            new[]
            {
                new Point(0, 0), new Point(75, 0), new Point(75, 25), new Point(100, 25), new Point(100, 100),
                new Point(25, 100), new Point(25, 75), new Point(0, 75),
            },
            new Rect(10, 30, 80, 40)),

        // Classic L-shape: removes the whole bottom-right quadrant, x[50,100]*y[50,100].
        // ContentArea uses the top band only (y in [10,40], max 40 < notch's min y 50), so it
        // can't overlap regardless of x, even though it spans the full width.
        new RoomShapeTemplate(
            "L-Form",
            new[] { new Point(0, 0), new Point(100, 0), new Point(100, 50), new Point(50, 50), new Point(50, 100), new Point(0, 100) },
            new Rect(10, 10, 80, 30)),

        // Outward bump on the right edge (x beyond 100) — purely additive, never removes area
        // from the base 0..100 square, so the plain-rectangle ContentArea is unaffected.
        new RoomShapeTemplate(
            "Erker rechts",
            new[]
            {
                new Point(0, 0), new Point(100, 0), new Point(100, 35), new Point(118, 35),
                new Point(118, 65), new Point(100, 65), new Point(100, 100), new Point(0, 100),
            },
            new Rect(10, 10, 80, 80)),

        // Outward bump above the top edge (y goes negative) — same reasoning as "Erker rechts",
        // additive only, base-square ContentArea unaffected.
        new RoomShapeTemplate(
            "Erker oben",
            new[]
            {
                new Point(0, 0), new Point(35, 0), new Point(35, -20), new Point(65, -20),
                new Point(65, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100),
            },
            new Rect(10, 10, 80, 80)),

        // Bigger single notch, top-left this time, 35x35: x[0,35]*y[0,35].
        // ContentArea x starts at 40 (> 35), clears the notch at any y.
        new RoomShapeTemplate(
            "Grosse Nische oben links",
            new[] { new Point(35, 0), new Point(100, 0), new Point(100, 100), new Point(0, 100), new Point(0, 35), new Point(35, 35) },
            new Rect(40, 10, 50, 80)),

        // Two opposite corners notched (top-left x[0,35]*y[0,35], bottom-right x[65,100]*y[65,100]
        // — diagonal/zigzag look. ContentArea is a horizontal band y in [40,60]: 40 > 35 clears
        // the top-left notch, 60 < 65 clears the bottom-right notch, regardless of x.
        new RoomShapeTemplate(
            "Versetztes Zickzack",
            new[]
            {
                new Point(35, 0), new Point(100, 0), new Point(100, 65), new Point(65, 65),
                new Point(65, 100), new Point(0, 100), new Point(0, 35), new Point(35, 35),
            },
            new Rect(10, 40, 80, 20)),
    };
}
