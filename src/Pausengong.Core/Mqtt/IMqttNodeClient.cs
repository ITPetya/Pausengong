namespace Pausengong.Core.Mqtt;

/// <summary>Seam over <see cref="MqttNodeClient"/> so dependents like <see cref="Config.ConfigSyncService"/> can be unit tested against a fake.</summary>
public interface IMqttNodeClient
{
    bool IsConnected { get; }

    event Func<string, string, Task>? MessageReceived;
    event Func<Task>? Disconnected;

    Task<bool> ConnectAsync(string nodeId, string host, int port, CancellationToken ct = default);
    Task DisconnectAsync();
    Task SubscribeAsync(string topicFilter);
    Task PublishAsync(string topic, string payload, bool retain = false);
}
