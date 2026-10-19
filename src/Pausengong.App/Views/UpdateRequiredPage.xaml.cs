using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pausengong.Core.Updates;

namespace Pausengong.App;

public sealed partial class UpdateRequiredPage : Page
{
    private readonly string? _downloadUrl;

    public UpdateRequiredPage(UpdateCheckResult updateResult)
    {
        InitializeComponent();
        _downloadUrl = updateResult.DownloadUrl;

        var reason = string.IsNullOrWhiteSpace(updateResult.Message)
            ? "Diese Version hat ein Kompatibilitäts- oder Zuverlässigkeitsproblem."
            : updateResult.Message;

        MessageText.Text =
            $"Eine neue Version (v{updateResult.LatestVersion}) ist erforderlich, bevor Pausengong weiterläuft.\n\n" +
            $"{reason}\n\n" +
            "Bitte die neue Version herunterladen und erneut starten.";
    }

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_downloadUrl) && Uri.TryCreate(_downloadUrl, UriKind.Absolute, out var uri))
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e) => Microsoft.UI.Xaml.Application.Current.Exit();
}
