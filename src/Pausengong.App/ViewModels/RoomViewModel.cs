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
    // Adjust freely once this is on screen for real. Bumped up 2026-10-19 ("die Räume sollen
    // größer sein") alongside MinDeskSlots below.
    private const double BaseSize = 260;
    private const double GrowthPerNode = 20;
    private const double MaxSize = 520;

    /// <summary>A room always shows at least this many desks (requested 2026-10-19: "immer mit
    /// mehreren leeren Tischen gefüllt") — real devices fill the first slots, the rest render as
    /// empty placeholders. See <see cref="DeskSlots"/>.</summary>
    private const int MinDeskSlots = 6;

    public string RoomId { get; }

    [ObservableProperty]
    private string _name;

    /// <summary>The always-present landing room (see <see cref="RoomConfig.DefaultRoomId"/>) —
    /// can't be deleted, since there'd be nowhere left for newly seen nodes to land. Renaming it
    /// is still allowed — nothing is keyed off its display name, only <see cref="RoomId"/>.</summary>
    public bool IsDefaultRoom => RoomId == RoomConfig.DefaultRoomId;

    /// <summary>Hides the "✕ Raum löschen" button for <see cref="IsDefaultRoom"/>.</summary>
    public Visibility DeleteButtonVisibility => IsDefaultRoom ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Set from <c>MainPage.xaml.cs</c>'s PointerEntered/Exited on the room-name area
    /// (requested 2026-10-19: the rename/delete controls should only appear on hovering the
    /// name, not sit there permanently).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDeleteButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(EditButtonVisibility))]
    private bool _isHeaderHovered;

    /// <summary>Whether the name is currently being edited inline (requested 2026-10-19: hovering
    /// the name should offer renaming, not just deleting) — see <c>OnEditRoomNameClicked</c> /
    /// <c>OnRoomNameTextBoxKeyDown</c> / <c>OnRoomNameEditingLostFocus</c> in MainPage.xaml.cs.
    /// Swaps the name <c>TextBlock</c> for a <c>TextBox</c> in MainPage.xaml — via the two
    /// explicit <see cref="Visibility"/> properties below, not a direct bool-to-Visibility
    /// <c>x:Bind</c> (which WinUI3/UWP does support implicitly, but every other Visibility
    /// binding already in this codebase uses an explicit <c>Visibility</c>-typed property, so
    /// this stays consistent with that proven pattern instead of relying on a different,
    /// unexercised-here one).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameTextVisibility))]
    [NotifyPropertyChangedFor(nameof(NameEditVisibility))]
    [NotifyPropertyChangedFor(nameof(EffectiveDeleteButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(EditButtonVisibility))]
    private bool _isEditingName;

    public Visibility NameTextVisibility => IsEditingName ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NameEditVisibility => IsEditingName ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EffectiveDeleteButtonVisibility =>
        DeleteButtonVisibility == Visibility.Visible && IsHeaderHovered && !IsEditingName ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EditButtonVisibility =>
        IsHeaderHovered && !IsEditingName ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<NodeListItem> Nodes { get; } = new();

    /// <summary>
    /// The room's full desk grid — every real device (<see cref="Nodes"/>, wrapped as an
    /// occupied <see cref="DeskSlot"/>) followed by enough empty placeholder slots to reach
    /// <see cref="MinDeskSlots"/>. Deliberately a separate list, never <see cref="Nodes"/> itself
    /// — see <see cref="DeskSlot"/> for why padding must never leak into the real, persisted,
    /// draggable data.
    /// </summary>
    public IReadOnlyList<DeskSlot> DeskSlots
    {
        get
        {
            var slots = new List<DeskSlot>(Math.Max(Nodes.Count, MinDeskSlots));
            foreach (var node in Nodes)
            {
                slots.Add(new DeskSlot(node));
            }

            for (var i = Nodes.Count; i < MinDeskSlots; i++)
            {
                slots.Add(DeskSlot.Empty);
            }

            return slots;
        }
    }

    /// <summary>This room's fixed floor-plan outline — picked once, deterministically, from
    /// <see cref="RoomId"/>, so a room keeps the same look across app restarts (an ordinary
    /// <c>string.GetHashCode()</c> would NOT do that: .NET randomizes its seed per process by
    /// design, see <see cref="StableHash"/>).</summary>
    public RoomShapeTemplate Shape { get; }

    /// <summary>The base scale unit (1 design-space unit = <see cref="Size"/>/100 pixels) —
    /// grows with device count, clamped, so a fuller room visibly reads as "fuller". Driven by
    /// the real device count only, not <see cref="DeskSlots"/>'s padding — the minimum size
    /// already comes from <see cref="BaseSize"/> being large enough to comfortably fit
    /// <see cref="MinDeskSlots"/> empty desks.</summary>
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
    /// Inset so the content area (desks + the name band below them — see MainPage.xaml) never
    /// overlaps any notch in <see cref="Shape"/> — derived from
    /// <see cref="RoomShapeTemplate.ContentArea"/>, which every template's own hand-verified
    /// comment (see <see cref="RoomShapeTemplates"/>) guarantees clears every notch for that
    /// specific shape.
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
        // collection changes so the room visibly grows/shrinks as devices are dragged in/out,
        // and the desk grid (DeskSlots) refills with the right number of empty placeholders.
        Nodes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(GridWidth));
            OnPropertyChanged(nameof(GridHeight));
            OnPropertyChanged(nameof(OutlinePoints));
            OnPropertyChanged(nameof(ContentMargin));
            OnPropertyChanged(nameof(DeskSlots));
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
