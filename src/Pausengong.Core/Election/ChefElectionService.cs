using Pausengong.Core.Config;
using Pausengong.Core.Election.Discovery;
using Pausengong.Core.Mqtt;

namespace Pausengong.Core.Election;

/// <summary>
/// Orchestrates Chef election end to end: discover the current Chef (or self-promote), connect
/// as an MQTT client or host the broker accordingly, and react to disconnects / split-brain
/// conflicts by re-running the same decision logic rather than blind retries.
/// </summary>
public sealed class ChefElectionService : IAsyncDisposable
{
    private readonly NodeIdentity _identity;
    private readonly int _mqttPort;
    private readonly SuccessionListStore _successionStore;
    private readonly ChefDiscoveryService _discovery;
    private readonly EmbeddedBrokerService _broker;
    private readonly IMqttNodeClient _mqttClient;

    private CancellationTokenSource? _responderCts;
    private Task? _responderTask;
    private readonly SemaphoreSlim _electionGate = new(1, 1);

    public ChefElectionService(
        NodeIdentity identity,
        int mqttPort,
        SuccessionListStore successionStore,
        ChefDiscoveryService discovery,
        EmbeddedBrokerService broker,
        IMqttNodeClient mqttClient)
    {
        _identity = identity;
        _mqttPort = mqttPort;
        _successionStore = successionStore;
        _discovery = discovery;
        _broker = broker;
        _mqttClient = mqttClient;

        _discovery.ConflictingChefAnnounced += OnConflictingChefAnnounced;
        _mqttClient.Disconnected += OnMqttClientDisconnectedAsync;
    }

    public SuccessionEntry? CurrentChef { get; private set; }

    public bool IsSelfChief => CurrentChef?.NodeId == _identity.NodeId;

    /// <summary>Raised whenever the known Chef changes (including self becoming/ceasing to be Chef).</summary>
    public event Action<SuccessionEntry>? ChefChanged;

    private SuccessionEntry SelfEntry => new(
        _identity.NodeId,
        _identity.HostName,
        NetworkHelper.GetPrimaryIPv4Address(),
        _mqttPort,
        _identity.CreatedAt); // priority timestamp assigned once, at first-ever join

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _successionStore.LoadAsync(ct);
        var cachedChef = SuccessionOrdering.HighestPriority(_successionStore.Current);

        var announced = await _discovery.DiscoverChefAsync(_identity.NodeId, cachedChef, ct);
        await _successionStore.UpsertAsync(SelfEntry, ct);

        if (announced is not null)
        {
            await ConnectAsClientAsync(announced, ct);
        }
        else
        {
            await SelfPromoteAsync(ct);
        }

        _responderCts = new CancellationTokenSource();
        _responderTask = _discovery.RunResponderLoopAsync(() => IsSelfChief, () => CurrentChef ?? SelfEntry, _responderCts.Token);
    }

    private async Task ConnectAsClientAsync(SuccessionEntry chef, CancellationToken ct)
    {
        if (_broker.IsRunning)
        {
            await _broker.StopAsync();
        }

        var connected = await _mqttClient.ConnectAsync(_identity.NodeId, chef.Address, chef.MqttPort, ct);
        if (!connected)
        {
            // Chef announced itself but isn't actually reachable (e.g. just died) — self-promote instead.
            await SelfPromoteAsync(ct);
            return;
        }

        SetCurrentChef(chef);
    }

    private async Task SelfPromoteAsync(CancellationToken ct)
    {
        var self = SelfEntry;
        await _broker.StartAsync(_mqttPort, ct);
        await _mqttClient.ConnectAsync(_identity.NodeId, "127.0.0.1", _mqttPort, ct);
        await _mqttClient.PublishAsync(TopicSchema.ElectionChief, System.Text.Json.JsonSerializer.Serialize(self), retain: true);
        SetCurrentChef(self);
    }

    private void SetCurrentChef(SuccessionEntry chef)
    {
        CurrentChef = chef;
        ChefChanged?.Invoke(chef);
    }

    private async void OnConflictingChefAnnounced(SuccessionEntry other)
    {
        await _electionGate.WaitAsync();
        try
        {
            if (!IsSelfChief || CurrentChef is null)
            {
                return;
            }

            if (SuccessionOrdering.HasHigherPriority(other, CurrentChef))
            {
                // We lose: tear down our broker and reconnect as a client to the winner.
                await _broker.StopAsync();
                await ConnectAsClientAsync(other, CancellationToken.None);
            }
            else
            {
                // We win: re-announce so the other side yields to us.
                await _mqttClient.PublishAsync(TopicSchema.ElectionChief, System.Text.Json.JsonSerializer.Serialize(CurrentChef), retain: true);
            }
        }
        finally
        {
            _electionGate.Release();
        }
    }

    private async Task OnMqttClientDisconnectedAsync()
    {
        await _electionGate.WaitAsync();
        try
        {
            if (IsSelfChief)
            {
                return; // our own broker going away is handled elsewhere; nothing to reconnect to
            }

            var cachedChef = SuccessionOrdering.HighestPriority(_successionStore.Current);
            var announced = await _discovery.DiscoverChefAsync(_identity.NodeId, cachedChef, CancellationToken.None);
            if (announced is not null)
            {
                await ConnectAsClientAsync(announced, CancellationToken.None);
            }
            else
            {
                await SelfPromoteAsync(CancellationToken.None);
            }
        }
        finally
        {
            _electionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _discovery.ConflictingChefAnnounced -= OnConflictingChefAnnounced;
        _mqttClient.Disconnected -= OnMqttClientDisconnectedAsync;

        _responderCts?.Cancel();
        if (_responderTask is not null)
        {
            try
            {
                await _responderTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _electionGate.Dispose();
    }
}
