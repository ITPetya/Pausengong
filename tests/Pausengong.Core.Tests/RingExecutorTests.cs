using Pausengong.Core.Audio;
using Pausengong.Core.Config;
using Xunit;

namespace Pausengong.Core.Tests;

public class RingExecutorTests : IDisposable
{
    private readonly string _tempDir;

    public RingExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PausengongTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private sealed class FakeGongPlayer : IGongPlayer
    {
        public bool WasCalled { get; private set; }
        public Task PlayAsync(CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeToastService : IToastService
    {
        public bool WasCalled { get; private set; }
        public Task ShowGongWouldHaveFiredAsync(string roomName, DateTimeOffset atTime, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private async Task<ConfigStore> CreateStoreWithTestModeAsync(bool testModeActive)
    {
        var store = new ConfigStore(
            configPath: Path.Combine(_tempDir, "config.json"),
            identityPath: Path.Combine(_tempDir, "node-id.json"));
        var config = AppConfig.CreateDefault() with { TestMode = new TestModeState(testModeActive, DateTimeOffset.UtcNow) };
        await store.SaveAsync(config);
        return store;
    }

    [Fact]
    public async Task ExecuteRingAsync_WhenTestModeActive_ShowsToast_NeverPlaysRealAudio()
    {
        var store = await CreateStoreWithTestModeAsync(testModeActive: true);
        var gongPlayer = new FakeGongPlayer();
        var toastService = new FakeToastService();
        var executor = new RingExecutor(store, gongPlayer, toastService);

        await executor.ExecuteRingAsync("Klassenzimmer 3B", DateTimeOffset.Now);

        Assert.True(toastService.WasCalled);
        Assert.False(gongPlayer.WasCalled);
    }

    [Fact]
    public async Task ExecuteRingAsync_WhenTestModeInactive_PlaysRealAudio_NeverShowsToast()
    {
        var store = await CreateStoreWithTestModeAsync(testModeActive: false);
        var gongPlayer = new FakeGongPlayer();
        var toastService = new FakeToastService();
        var executor = new RingExecutor(store, gongPlayer, toastService);

        await executor.ExecuteRingAsync("Klassenzimmer 3B", DateTimeOffset.Now);

        Assert.True(gongPlayer.WasCalled);
        Assert.False(toastService.WasCalled);
    }

    [Fact]
    public async Task ExecuteRingAsync_OnFreshDefaultConfig_UsesToastPath_SinceTestModeDefaultsOn()
    {
        var store = new ConfigStore(
            configPath: Path.Combine(_tempDir, "config.json"),
            identityPath: Path.Combine(_tempDir, "node-id.json"));
        await store.LoadAsync(); // no file on disk yet -> AppConfig.CreateDefault()
        var gongPlayer = new FakeGongPlayer();
        var toastService = new FakeToastService();
        var executor = new RingExecutor(store, gongPlayer, toastService);

        await executor.ExecuteRingAsync("Klassenzimmer 3B", DateTimeOffset.Now);

        Assert.True(toastService.WasCalled);
        Assert.False(gongPlayer.WasCalled);
    }
}
