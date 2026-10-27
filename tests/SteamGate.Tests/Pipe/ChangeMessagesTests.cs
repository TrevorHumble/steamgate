using SteamGate.Core.Accounting;
using SteamGate.Core.Extend;
using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class ChangeMessagesTests
{
    private const string AppliedText = "Applied now. Tonight ends 20 min later (limit 1 h 50 min).";

    [Fact]
    public void ExtendReply()
    {
        var applied = new ExtendOutcome(Tally.Empty, true, AppliedText);

        var reply = ChangeMessages.ExtendReply("s7", applied, saveFailed: false);
        Assert.Equal(new ReplyMessage("s7", true, AppliedText, null), reply);

        var held = ChangeMessages.ExtendReply("s7", applied, saveFailed: true);
        Assert.True(held.Ok);
        Assert.Equal(AppliedText + " Saved in memory; will retry.", held.Message);
        Assert.Null(held.Pending);

        var refused = new ExtendOutcome(Tally.Empty, false, "Extension used tonight: 20 min.");
        foreach (var saveFailed in new[] { false, true })
        {
            var answer = ChangeMessages.ExtendReply("s8", refused, saveFailed);
            Assert.False(answer.Ok);
            Assert.Equal("Extension used tonight: 20 min.", answer.Message);
            Assert.Equal("s8", answer.RequestId);
        }
    }
}
