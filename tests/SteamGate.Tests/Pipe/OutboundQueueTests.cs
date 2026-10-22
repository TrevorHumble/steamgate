using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class OutboundQueueTests
{
    [Fact]
    public void FullQueueRejects()
    {
        var queue = new OutboundQueue();

        for (var posted = 1; posted <= 16; posted++)
        {
            Assert.True(queue.TryPost(new WarnMessage(posted)), $"post {posted}");
        }

        Assert.False(queue.TryPost(new WarnMessage(17)));
    }

    [Fact]
    public async Task MessagesComeOutInOrderAndMakeRoom()
    {
        var queue = new OutboundQueue();
        for (var posted = 1; posted <= 16; posted++)
        {
            queue.TryPost(new WarnMessage(posted));
        }

        var first = await queue.ReadAsync(TestContext.Current.CancellationToken);
        var second = await queue.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new WarnMessage(1), first);
        Assert.Equal(new WarnMessage(2), second);
        Assert.True(queue.TryPost(new WarnMessage(17)));
        Assert.True(queue.TryPost(new WarnMessage(18)));
        Assert.False(queue.TryPost(new WarnMessage(19)));
    }

    [Fact]
    public void ACompletedQueueRejects()
    {
        var queue = new OutboundQueue();
        queue.Complete();

        Assert.False(queue.TryPost(new WarnMessage(1)));
    }

    [Fact]
    public void CapacityIsSixteen()
    {
        Assert.Equal(16, OutboundQueue.Capacity);
    }
}
