namespace Pausengong.Core.Updates;

/// <summary>
/// How urgently a user should update, carried alongside the latest version number in
/// <see cref="UpdateManifest"/> rather than derived from the version numbers themselves — the
/// publisher of a release is the one who knows whether it only touched UI, or fixed something
/// that affects compatibility/reliability, not something that can be inferred from "1.2.0" vs
/// "1.3.0" alone.
/// </summary>
public enum UpdateUrgency
{
    /// <summary>UI-only or cosmetic changes. A simple, dismissible notice is enough — never
    /// blocks anything.</summary>
    Informational = 1,

    /// <summary>Worth updating soon, but the current version still works. A more prominent,
    /// harder-to-miss notice — still never blocks the app from loading.</summary>
    Important = 2,

    /// <summary>Compatibility or reliability is at risk on the current version. The app must not
    /// fully load until updated — see <c>App.xaml.cs</c> / <c>UpdateRequiredWindow</c>.</summary>
    Mandatory = 3,
}
