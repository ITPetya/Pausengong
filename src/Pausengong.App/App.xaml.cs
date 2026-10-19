using System.IO;
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

namespace Pausengong.App;

public partial class App : Application
{
    public const int MqttPort = 18830;

    // Two Pausengong processes on the same PC would both try to bind the same discovery/MQTT
    // ports and silently fail (see: the exact bug this fixes — a second launch got stuck on
    // "Netzwerk wird gesucht" forever because its network startup threw and nobody caught it).
    // A named mutex, checked before any window/networking exists, prevents that outright.
    private const string SingleInstanceMutexName = "Global\\Pausengong.SingleInstance";
    private static Mutex? _singleInstanceMutex;

    private MainWindow? _window;

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

        _window = new MainWindow(Services);
        _window.Activate();
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

        return services.BuildServiceProvider();
    }
}
