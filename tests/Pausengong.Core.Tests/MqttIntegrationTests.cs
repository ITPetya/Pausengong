using Pausengong.Core.Mqtt;
using Xunit;

namespace Pausengong.Core.Tests;

/// <summary>
/// Real MQTTnet broker+client round trip on loopback. Unlike the discovery tests (which fake
/// the transport), this exercises the actual MQTTnet v5 API surface end to end — the one part
/// of the MQTT/election layer that unit tests against fakes can't catch API-usage mistakes in.
/// </summary>
public class MqttIntegrationTests
{
    [Fact]
    public async Task EmbeddedBroker_And_MqttNodeClient_ConnectPublishSubscribeReceive_RoundTrip()
    {
        var port = 18900 + Random.Shared.Next(0, 500); // avoid clashing with a parallel test run
        await using var broker = new EmbeddedBrokerService();
        await broker.StartAsync(port);
        Assert.True(broker.IsRunning);

        await using var client = new MqttNodeClient();
        var connected = await client.ConnectAsync("pc-test", "127.0.0.1", port);
        Assert.True(connected);
        Assert.True(client.IsConnected);

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += (topic, payload) =>
        {
            if (topic == "pausengong/test/topic")
            {
                received.TrySetResult(payload);
            }

            return Task.CompletedTask;
        };

        await client.SubscribeAsync("pausengong/test/topic");
        await client.PublishAsync("pausengong/test/topic", "hello-pausengong", retain: false);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        Assert.Equal("hello-pausengong", await received.Task);
    }

    [Fact]
    public async Task MqttNodeClient_ConnectAsync_PublishesRetainedOnlineMessage()
    {
        var port = 18900 + Random.Shared.Next(500, 1000);
        await using var broker = new EmbeddedBrokerService();
        await broker.StartAsync(port);

        await using var publisher = new MqttNodeClient();
        await publisher.ConnectAsync("pc-a", "127.0.0.1", port);

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observer = new MqttNodeClient();
        observer.MessageReceived += (topic, payload) =>
        {
            if (topic == TopicSchema.NodeOnline("pc-a"))
            {
                received.TrySetResult(payload);
            }

            return Task.CompletedTask;
        };
        await observer.ConnectAsync("pc-observer", "127.0.0.1", port);
        await observer.SubscribeAsync(TopicSchema.NodeOnline("pc-a"));

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        Assert.Equal("online", await received.Task);
    }
}
