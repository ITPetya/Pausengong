using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Pausengong.Core.Config;

namespace Pausengong.App.ViewModels;

/// <summary>
/// One room, shown as a vertical list of workstations (requested 2026-10-19, replacing the
/// floor-plan/polygon/spiral visualization that preceded it — "wir schaffen die Grundrisse ab,
/// machen Listen nur durch dezente vertikale Linie getrennt"). That earlier visualization's
/// `Canvas`-based absolute positioning failed three separate, differently-reasoned fix attempts
/// in a row with the identical symptom (every room rendering stacked at the same position) —
/// this room went back to layout mechanics already proven correct by real screenshots (a plain
/// horizontal <c>ItemsControl</c> of rooms, each a vertical list) rather than attempting a fourth
/// blind fix. See HANDOFF.md "Room visualization" history for the full story, including what was
/// tried and why each attempt failed.
/// </summary>
public partial class RoomViewModel : ObservableObject
{
    /// <summary>A room always shows at least this many workstations (kept from the floor-plan
    /// version: "immer mit mehreren leeren Tischen gefüllt") — real devices fill the first slots,
    /// the rest render as empty placeholders. See <see cref="DeskSlots"/>.</summary>
    private const int MinDeskSlots = 6;

    public string RoomId { get; }

    [ObservableProperty]
    private string _name;

    /// <summary>The always-present landing room (see <see cref="RoomConfig.DefaultRoomId"/>) —
    /// can't be deleted, since there'd be nowhere left for newly seen nodes to land. Renaming it
    /// is still allowed — nothing is keyed off its display name, only <see cref="RoomId"/>.</summary>
    public bool IsDefaultRoom => RoomId == RoomConfig.DefaultRoomId;

    /// <summary>Hides the "Löschen" button for <see cref="IsDefaultRoom"/>.</summary>
    public Visibility DeleteButtonVisibility => IsDefaultRoom ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Set from <c>MainPage.xaml.cs</c>'s PointerEntered/Exited on the room-name area —
    /// the rename/delete controls should only appear on hovering the name, not sit there
    /// permanently.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveDeleteButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(EditButtonVisibility))]
    private bool _isHeaderHovered;

    /// <summary>Whether the name is currently being edited inline — see
    /// <c>OnEditRoomNameClicked</c>/<c>OnRoomNameTextBoxKeyDown</c>/<c>OnRoomNameEditingLostFocus</c>
    /// in MainPage.xaml.cs. Swaps the name <c>TextBlock</c> for a <c>TextBox</c> in MainPage.xaml
    /// via the two explicit <see cref="Visibility"/> properties below, not a direct bool-to-Visibility
    /// <c>x:Bind</c> (which WinUI3/UWP does support implicitly, but every other Visibility binding
    /// already in this codebase uses an explicit <c>Visibility</c>-typed property, so this stays
    /// consistent with that proven pattern instead of relying on a different, unexercised one).</summary>
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
    /// The room's full workstation list — every real device (<see cref="Nodes"/>, wrapped as an
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

    public RoomViewModel(string roomId, string name)
    {
        RoomId = roomId;
        _name = name;

        Nodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DeskSlots));
    }
}
