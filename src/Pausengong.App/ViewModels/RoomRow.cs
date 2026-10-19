using Microsoft.UI.Xaml;

namespace Pausengong.App.ViewModels;

/// <summary>
/// One horizontal row of rooms in the "building" layout (requested 2026-10-19: "wie ein
/// zusammenhängendes Gebäude mit mehreren Räumen", not one long strip of rooms side by side) —
/// see <see cref="MainViewModel.RoomRows"/>. A plain concrete type specifically so MainPage.xaml
/// can declare <c>x:DataType="vm:RoomRow"</c> on the outer template without any generic-XAML
/// syntax (<c>x:DataType</c> on a raw <c>IReadOnlyList&lt;RoomViewModel&gt;</c> would need that).
/// </summary>
public sealed record RoomRow(IReadOnlyList<RoomViewModel> Rooms, bool HasRowBelow)
{
    /// <summary>Whether to draw the vertical door connector to the next row down — explicit
    /// <see cref="Visibility"/>, not a direct bool-to-Visibility bind, for the same consistency
    /// reason as <see cref="RoomViewModel.NameTextVisibility"/>.</summary>
    public Visibility HasRowBelowVisibility => HasRowBelow ? Visibility.Visible : Visibility.Collapsed;
}
