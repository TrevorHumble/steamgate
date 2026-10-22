using SteamGate.Core.Enforcement;
using Xunit;

namespace SteamGate.Tests.Enforcement;

public sealed class WarnMinutesTests
{
    [Fact]
    public void RoundsUp()
    {
        Assert.Equal(22, WarnMinutes.FromRemaining(1300));
        Assert.Equal(30, WarnMinutes.FromRemaining(1800));
        Assert.Equal(1, WarnMinutes.FromRemaining(1));
        Assert.Equal(1, WarnMinutes.FromRemaining(60));
        Assert.Equal(2, WarnMinutes.FromRemaining(61));
    }

    [Fact]
    public void NothingLeftIsZero()
    {
        Assert.Equal(0, WarnMinutes.FromRemaining(0));
        Assert.Equal(0, WarnMinutes.FromRemaining(-5));
    }
}
