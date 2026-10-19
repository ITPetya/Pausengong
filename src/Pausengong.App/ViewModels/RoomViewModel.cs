using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Pausengong.Core.Config;

namespace Pausengong.App.ViewModels;

public partial class RoomViewModel : ObservableObject
{
    public string RoomId { get; }

    [ObservableProperty]
    private string _name;

    /// <summary>The always-present landing room (see <see cref="RoomConfig.DefaultRoomId"/>) —
    /// can't be deleted, since there'd be nowhere left for newly seen nodes to land.</summary>
    public bool IsDefaultRoom => RoomId == RoomConfig.DefaultRoomId;

    /// <summary>Hides the "✕ Raum löschen" button for <see cref="IsDefaultRoom"/>.</summary>
    public Visibility DeleteButtonVisibility => IsDefaultRoom ? Visibility.Collapsed : Visibility.Visible;

    public ObservableCollection<NodeListItem> Nodes { get; } = new();

    public RoomViewModel(string roomId, string name)
    {
        RoomId = roomId;
        _name = name;
    }
}
