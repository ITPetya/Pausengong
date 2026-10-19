using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Pausengong.App.Notifications;
using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Pausengong.Core.Election;
using Pausengong.Core.Election.Discovery;
using Pausengong.Core.Mqtt;
using Pausengong.Core.Presence;
using Pausengong.Core.Scheduling;
using Pausengong.Core.Updates;

namespace Pausengong.App;

public partial class App : Application
{
    public const int MqttPort = 18830;

    // Raw-content URL for the repo's version.json — see HttpUpdateManifestFetcher and
    // HANDOFF.md "Update notification system". Public file, no auth needed.
    private static readonly Uri VersionManifestUrl =
        new("https://raw.githubusercontent.com/ITPetya/Pausengong/master/version.json");

    // Two Pausengong processes on the same PC would both try to bind the same discovery/MQTT
    // ports and silently fail (see: the exact bug this fixes — a second launch got stuck on
    // "Netzwerk wird gesucht" forever because its network startup threw and nobody caught it).
    // A named mutex, checked before any window/networking exists, prevents that outright.
    private const string SingleInstanceMutexName = "Global\\Pausengong.SingleInstance";
    private static Mutex? _singleInstanceMutex;

    private Window? _window;

    public IServiceProvider Services { get; } = null!;

    public App()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            NativeMessageBox.Show(
                "Pausengong läuft bereits.",
                "Ein weiteres Fenster lässt sich im System-Tray öffnen (Symbol unten rechts).");
            Environment.Exit(0);
            return;
        }

        AssetExtractor.EnsureExtracted();

        InitializeComponent();
        Services = BuildServices();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppNotificationManager.Default.Register();
        _ = LaunchAsync();
    }

    /// <summary>
    /// Runs the update check BEFORE any window is shown — a <see cref="UpdateUrgency.Mandatory"/>
    /// result (compatibility/reliability at risk on this version) shows only
    /// <see cref="UpdateRequiredWindow"/> instead of <see cref="MainWindow"/>: no networking, no
    /// MQTT, no scheduling loop starts, nothing beyond the update notice itself (the maintainer's
    /// explicit requirement 2026-10-19: "das Programm soll auch nicht vollständig laden").
    /// Everything else (no update / Informational / Important) loads completely normally — the
    /// check result is just carried into <see cref="MainViewModel"/> to show as an InfoBar.
    /// </summary>
    private async Task LaunchAsync()
    {
        var updateService = Services.GetRequiredService<UpdateService>();
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        UpdateCheckResult updateResult;
        try
        {
            updateResult = await updateService.CheckAsync(currentVersion);
        }
        catch
        {
            // One more defensive layer around the startup gate itself, on top of UpdateService's
            // own fail-open contract — a failed check here must never be able to block startup.
            updateResult = UpdateCheckResult.UpToDate;
        }

        if (updateResult is { IsUpdateAvailable: true, Urgency: UpdateUrgency.Mandatory })
        {
            _window = new UpdateRequiredWindow(updateResult);
            _window.Activate();
            return;
        }

        var mainWindow = new MainWindow(Services, updateResult);
        _window = mainWindow;
        mainWindow.Activate();
    }

    /// <summary>
    /// Last-line-of-defense safety net: this is a background service-like app that should keep
    /// running (and keep the network/election/scheduling loop alive) even if one UI action hits
    /// a bug, rather than the whole process — and any in-progress network coordination — going
    /// down. This does not replace fixing the actual bug; it just stops one bad click from
    /// taking the whole app out.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var logPath = Path.Combine(Pausengong.Core.Storage.AppPaths.LogsDirectory, "unhandled-exceptions.log");
            Pausengong.Core.Storage.AppPaths.EnsureDirectoriesExist();
            File.AppendAllText(logPath, $"{DateTimeOffset.Now:O}  {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never itself throw and defeat the safety net.
        }

        e.Handled = true;
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ConfigStore>();
        services.AddSingleton<SuccessionListStore>();
        services.AddSingleton<IUdpDiscoveryTransport>(_ => new UdpDiscoveryTransport());
        services.AddSingleton<ChefDiscoveryService>();
        services.AddSingleton<EmbeddedBrokerService>();
        services.AddSingleton<MqttNodeClient>();
        services.AddSingleton<IMqttNodeClient>(sp => sp.GetRequiredService<MqttNodeClient>());
        services.AddSingleton<AudioDeviceSettingsStore>();
        services.AddSingleton<IGongPlayer>(sp =>
            new GongPlayer(AssetExtractor.GongWavPath, sp.GetRequiredService<AudioDeviceSettingsStore>()));
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<RingExecutor>();
        services.AddSingleton<ConfigSyncService>();
        services.AddSingleton<NodePresenceService>();
        services.AddSingleton<RingCoordinatorService>();

        services.AddSingleton<HttpClient>();
        services.AddSingleton<IUpdateManifestFetcher>(sp =>
            new HttpUpdateManifestFetcher(sp.GetRequiredService<HttpClient>(), VersionManifestUrl));
        services.AddSingleton<UpdateService>();

        return services.BuildServiceProvider();
    }
}
