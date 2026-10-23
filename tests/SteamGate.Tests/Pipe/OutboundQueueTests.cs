using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class OutboundQueueTests
{
    [Fact]
    public void FullQueueRejects()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);

        for (var posted = 1; posted <= 16; posted++)
        {
            Assert.True(queue.TryPost(new WarnMessage(posted)), $"post {posted}");
        }

        Assert.False(queue.TryPost(new WarnMessage(17)));
    }

    [Fact]
    public async Task MessagesComeOutInOrderAndMakeRoom()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
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
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        queue.Complete();

        Assert.False(queue.TryPost(new WarnMessage(1)));
    }

    [Fact]
    public void CapacityIsSixteen()
    {
        Assert.Equal(16, OutboundQueue.Capacity);
    }

    [Fact]
    public void AFailedPostCancelsTheConnection()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        for (var posted = 1; posted <= 16; posted++)
        {
            Assert.True(queue.TryPost(new WarnMessage(posted)));
            Assert.False(connection.IsCancellationRequested);
        }

        Assert.False(queue.TryPost(new WarnMessage(17)));

        Assert.True(connection.IsCancellationRequested);
    }

    [Fact]
    public void APostToAClosedQueueCancelsTheConnection()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        queue.Complete();

        Assert.False(queue.TryPost(new WarnMessage(1)));

        Assert.True(connection.IsCancellationRequested);
    }

    [Fact]
    public void ALatePostAfterTheSourceIsDisposedReturnsFalseWithoutThrowing()
    {
        var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        queue.Complete();
        connection.Dispose();

        Assert.False(queue.TryPost(new WarnMessage(1)));
    }
}
