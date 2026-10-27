using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using Xunit;

namespace SteamGate.Tests.Rules;

public sealed class SettingsCommandTests
{
    [Fact]
    public void ExactlyOneKind()
    {
        using var connection = new CancellationTokenSource();
        var queue = new OutboundQueue(connection);
        var request = new ChangeRequest("enabled", null, true, null, null);
        var id = Guid.NewGuid();

        Assert.Equal(request, new SettingsCommand("s1", request, null, queue, 1, 1).Request);
        Assert.Equal(id, new SettingsCommand("s2", null, id, queue, 1, 1).CancelId);
        Assert.Throws<ArgumentException>(() => new SettingsCommand("s3", null, null, queue, 1, 1));
        Assert.Throws<ArgumentException>(() => new SettingsCommand("s4", request, id, queue, 1, 1));
    }
}
