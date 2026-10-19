using MQTTnet.Server;

namespace Pausengong.Core.Mqtt;

/// <summary>In-process MQTT broker the Chef hosts. No ASP.NET Core dependency — plain TCP.</summary>
public sealed class EmbeddedBrokerService : IAsyncDisposable
{
    private MqttServer? _server;

    public bool IsRunning => _server?.IsStarted ?? false;

    public async Task StartAsync(int port, CancellationToken ct = default)
    {
        if (_server is not null)
        {
            return;
        }

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        _server = new MqttServerFactory().CreateMqttServer(options);
        await _server.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_server is null)
        {
            return;
        }

        var stopOptions = new MqttServerFactory().CreateMqttServerStopOptionsBuilder().Build();
        await _server.StopAsync(stopOptions);
        _server.Dispose();
        _server = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
