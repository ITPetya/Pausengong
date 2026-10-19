using System.Buffers;
using System.Text;
using MQTTnet;

namespace Pausengong.Core.Mqtt;

/// <summary>
/// Thin wrapper around MQTTnet's plain <see cref="IMqttClient"/>.
///
/// MQTTnet 5.x dropped the old auto-reconnecting "managed client" entirely (no
/// MQTTnet.Extensions.ManagedClient package exists past 4.3.7, confirmed by inspecting the
/// v5 assemblies directly). That's actually a good fit here: after a Chef dies, a plain
/// "retry the same server" reconnect loop would be wrong anyway — we need to re-run Chef
/// discovery to find whoever took over. So reconnect policy deliberately lives in
/// <see cref="Election.ChefElectionService"/> (Phase 4), driven by the <see cref="Disconnected"/>
/// event here, rather than being handled transparently inside this class.
/// </summary>
public sealed class MqttNodeClient : IMqttNodeClient, IAsyncDisposable
{
    private readonly IMqttClient _client;

    public event Func<string, string, Task>? MessageReceived;
    public event Func<Task>? Disconnected;

    public MqttNodeClient()
    {
        _client = new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
    }

    public bool IsConnected => _client.IsConnected;

    public async Task<bool> ConnectAsync(string nodeId, string host, int port, CancellationToken ct = default)
    {
        var options = new MqttClientOptionsBuilder()
            .WithClientId(nodeId)
            .WithTcpServer(host, port)
            .WithWillTopic(TopicSchema.NodeOnline(nodeId))
            .WithWillPayload("offline")
            .WithWillRetain(true)
            .WithCleanSession(true)
            .Build();

        var result = await _client.ConnectAsync(options, ct);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            return false;
        }

        await PublishAsync(TopicSchema.NodeOnline(nodeId), "online", retain: true);
        return true;
    }

    public async Task DisconnectAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        var options = new MqttClientFactory().CreateClientDisconnectOptionsBuilder().Build();
        await _client.DisconnectAsync(options);
    }

    public Task SubscribeAsync(string topicFilter)
    {
        var options = new MqttClientFactory().CreateSubscribeOptionsBuilder()
            .WithTopicFilter(topicFilter)
            .Build();
        return _client.SubscribeAsync(options);
    }

    public Task PublishAsync(string topic, string payload, bool retain = false)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .Build();
        return _client.PublishAsync(message);
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var bytes = args.ApplicationMessage.Payload.ToArray();
        var payload = Encoding.UTF8.GetString(bytes);
        return MessageReceived?.Invoke(args.ApplicationMessage.Topic, payload) ?? Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args) =>
        Disconnected?.Invoke() ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        if (_client.IsConnected)
        {
            await DisconnectAsync();
        }

        _client.Dispose();
    }
}
