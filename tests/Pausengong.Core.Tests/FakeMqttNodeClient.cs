using Pausengong.Core.Mqtt;

namespace Pausengong.Core.Tests;

/// <summary>In-memory stand-in for <see cref="MqttNodeClient"/> so higher-level sync logic can be tested without a real broker.</summary>
internal sealed class FakeMqttNodeClient : IMqttNodeClient
{
    public List<(string Topic, string Payload, bool Retain)> Published { get; } = new();
    public List<string> Subscriptions { get; } = new();

    public bool IsConnected { get; private set; } = true;

    public event Func<string, string, Task>? MessageReceived;
    public event Func<Task>? Disconnected;

    public Task<bool> ConnectAsync(string nodeId, string host, int port, CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Disconnected?.Invoke() ?? Task.CompletedTask;
    }

    public Task SubscribeAsync(string topicFilter)
    {
        Subscriptions.Add(topicFilter);
        return Task.CompletedTask;
    }

    public Task PublishAsync(string topic, string payload, bool retain = false)
    {
        Published.Add((topic, payload, retain));
        return Task.CompletedTask;
    }

    /// <summary>Simulates a message arriving from the broker (from another node, or our own echo).</summary>
    public Task DeliverAsync(string topic, string payload) =>
        MessageReceived?.Invoke(topic, payload) ?? Task.CompletedTask;
}
