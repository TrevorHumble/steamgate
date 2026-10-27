using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using Xunit;

namespace SteamGate.Tests.Rules;

public sealed class WorkerCommandTests
{
    [Fact]
    public void Kinds()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        WorkerCommand settings = new SettingsCommand("s1", new ChangeRequest("enabled", null, true, null, null), null, queue, 12, 1);
        WorkerCommand extend = new ExtendCommand("s7", 20, queue, 13, 2);

        Assert.IsType<SettingsCommand>(settings);
        Assert.IsType<ExtendCommand>(extend);
        Assert.Equal(("s1", 12, 1), (settings.RequestId, settings.ClientPid, settings.SessionId));
        Assert.Equal(("s7", 13, 2), (extend.RequestId, extend.ClientPid, extend.SessionId));
        Assert.Same(queue, settings.Replies);
        Assert.Same(queue, extend.Replies);
        Assert.Equal(20, ((ExtendCommand)extend).Minutes);
        Assert.NotEqual(settings, extend);
    }
}
