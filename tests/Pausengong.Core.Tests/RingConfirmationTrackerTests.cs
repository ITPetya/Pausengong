using Microsoft.Extensions.Time.Testing;
using Pausengong.Core.Scheduling;
using Xunit;

namespace Pausengong.Core.Tests;

public class RingConfirmationTrackerTests
{
    [Fact]
    public async Task WaitForConfirmAsync_ReturnsTrue_WhenConfirmArrivesBeforeTimeout()
    {
        var tracker = new RingConfirmationTracker();
        var waitTask = tracker.WaitForConfirmAsync("node-1", TimeSpan.FromSeconds(5));

        tracker.OnConfirmReceived("node-1");

        Assert.True(await waitTask);
    }

    [Fact]
    public async Task WaitForConfirmAsync_ReturnsFalse_WhenTimeoutElapsesWithNoConfirm()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new RingConfirmationTracker(fakeTime);

        var waitTask = tracker.WaitForConfirmAsync("node-1", TimeSpan.FromSeconds(5));
        fakeTime.Advance(TimeSpan.FromSeconds(6));

        Assert.False(await waitTask);
    }

    [Fact]
    public async Task OnConfirmReceived_ForADifferentNode_DoesNotResolveTheWait()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new RingConfirmationTracker(fakeTime);

        var waitTask = tracker.WaitForConfirmAsync("node-1", TimeSpan.FromSeconds(5));
        tracker.OnConfirmReceived("some-other-node");
        fakeTime.Advance(TimeSpan.FromSeconds(6));

        Assert.False(await waitTask);
    }
}
