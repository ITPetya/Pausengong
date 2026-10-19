using CommunityToolkit.Mvvm.ComponentModel;

namespace Pausengong.App.ViewModels;

/// <summary>UI-list entry for a node: stable <see cref="NodeId"/> for drag/drop and persistence,
/// mutable <see cref="DisplayLabel"/> (hostname/IP) that's refreshed as presence data arrives.</summary>
public partial class NodeListItem : ObservableObject
{
    public string NodeId { get; }

    [ObservableProperty]
    private string _displayLabel;

    public NodeListItem(string nodeId, string displayLabel)
    {
        NodeId = nodeId;
        _displayLabel = displayLabel;
    }
}
