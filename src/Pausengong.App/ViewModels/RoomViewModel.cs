using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Pausengong.App.Views.RoomShapes;
using Pausengong.Core.Config;
using Windows.Foundation;

namespace Pausengong.App.ViewModels;

public partial class RoomViewModel : ObservableObject
{
    // Starting values only — nobody has actually seen this render anywhere (no Windows in this
    // tool environment), so these are a reasonable first guess, not a visually tuned result.
    // Adjust freely once this is on screen for real.
    private const double BaseSize = 150;
    private const double GrowthPerNode = 22;
    private const double MaxSize = 340;

    public string RoomId { get; }

    [ObservableProperty]
    private string _name;

    /// <summary>The always-present landing room (see <see cref="RoomConfig.DefaultRoomId"/>) —
    /// can't be deleted, since there'd be nowhere left for newly seen nodes to land.</summary>
    public bool IsDefaultRoom => RoomId == RoomConfig.DefaultRoomId;

    /// <summary>Hides the "✕ Raum löschen" button for <see cref="IsDefaultRoom"/>.</summary>
    public Visibility DeleteButtonVisibility => IsDefaultRoom ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Set from <c>MainPage.xaml.cs</c>'s PointerEntered/Exited on the room-name area
    /// (requested 2026-10-19: the delete "✕" should only appear on hovering the name, not sit
    /// there permanently). Combined with <see cref="DeleteButtonVisibility"/> below — a default
    /// room is never deletable regardless of hover.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDeleteButtonVisibility))]
    private bool _isHeaderHovered;

    public Visibility EffectiveDeleteButtonVisibility =>
        DeleteButtonVisibility == Visibility.Visible && IsHeaderHovered ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<NodeListItem> Nodes { get; } = new();

    /// <summary>This room's fixed floor-plan outline — picked once, deterministically, from
    /// <see cref="RoomId"/>, so a room keeps the same look across app restarts (an ordinary
    /// <c>string.GetHashCode()</c> would NOT do that: .NET randomizes its seed per process by
    /// design, see <see cref="StableHash"/>).</summary>
    public RoomShapeTemplate Shape { get; }

    /// <summary>The base scale unit (1 design-space unit = <see cref="Size"/>/100 pixels) —
    /// grows with device count, clamped, so a fuller room visibly reads as "fuller".</summary>
    public double Size => Math.Min(BaseSize + (Nodes.Count * GrowthPerNode), MaxSize);

    private double ScaleFactor => Size / 100.0;

    /// <summary>Pixel size of the bounding box that actually hosts this room's content —
    /// <see cref="Shape"/>'s design-space bounding box times <see cref="ScaleFactor"/>. Not
    /// always a square: a template with an outward bump (see <see cref="RoomShapeTemplates"/>)
    /// has a bounding box bigger than the plain base square on whichever side the bump is.</summary>
    public double GridWidth => Shape.DesignWidth * ScaleFactor;

    public double GridHeight => Shape.DesignHeight * ScaleFactor;

    /// <summary>The room's outline, already shifted so its own minimum is (0,0) and scaled to
    /// pixels — ready to bind straight to a <c>Polygon.Points</c>.</summary>
    public PointCollection OutlinePoints
    {
        get
        {
            var points = new PointCollection();
            foreach (var p in Shape.Outline)
            {
                points.Add(new Point((p.X - Shape.MinX) * ScaleFactor, (p.Y - Shape.MinY) * ScaleFactor));
            }

            return points;
        }
    }

    /// <summary>
    /// Inset so the device grid never overlaps any notch in <see cref="Shape"/> — derived from
    /// <see cref="RoomShapeTemplate.ContentArea"/>, which every template's own hand-verified
    /// comment (see <see cref="RoomShapeTemplates"/>) guarantees clears every notch for that
    /// specific shape. A single margin now (simplified 2026-10-19 when the separate header row
    /// was removed — the room name moved to a centered floor label instead, see MainPage.xaml):
    /// there's only one content region left to inset, not two stacked rows to split it across.
    /// </summary>
    public Thickness ContentMargin
    {
        get
        {
            var area = Shape.ContentArea;
            var left = (area.X - Shape.MinX) * ScaleFactor;
            var top = (area.Y - Shape.MinY) * ScaleFactor;
            var right = (Shape.MaxX - (area.X + area.Width)) * ScaleFactor;
            var bottom = (Shape.MaxY - (area.Y + area.Height)) * ScaleFactor;
            return new Thickness(left, top, right, bottom);
        }
    }

    public RoomViewModel(string roomId, string name)
    {
        RoomId = roomId;
        _name = name;
        Shape = RoomShapeTemplates.All[StableHash(roomId) % RoomShapeTemplates.All.Count];

        // Every one of these depends on Nodes.Count (Size) — re-raise them whenever the
        // collection changes so the room visibly grows/shrinks as devices are dragged in/out.
        Nodes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(GridWidth));
            OnPropertyChanged(nameof(GridHeight));
            OnPropertyChanged(nameof(OutlinePoints));
            OnPropertyChanged(nameof(ContentMargin));
        };
    }

    /// <summary>A plain, unseeded string hash — deliberately NOT <c>string.GetHashCode()</c>,
    /// which .NET randomizes per process (hash-flooding mitigation) and would pick a different
    /// shape for the same room every time the app restarts.</summary>
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
            {
                hash = (hash * 31) + c;
            }

            return hash & int.MaxValue; // strip sign bit so '% count' is never negative
        }
    }
}
