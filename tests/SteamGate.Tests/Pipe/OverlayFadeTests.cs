using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class OverlayFadeTests
{
    [Fact]
    public void HoldThenFade()
    {
        Assert.Equal(1.0, OverlayFade.OpacityAt(TimeSpan.Zero));
        Assert.Equal(1.0, OverlayFade.OpacityAt(TimeSpan.FromSeconds(8)));
        Assert.Equal(0.5, OverlayFade.OpacityAt(TimeSpan.FromSeconds(9)));
        Assert.Equal(0.0, OverlayFade.OpacityAt(TimeSpan.FromSeconds(10)));
        Assert.Equal(0.0, OverlayFade.OpacityAt(TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public void FallsInAStraightLine()
    {
        Assert.Equal(0.75, OverlayFade.OpacityAt(TimeSpan.FromSeconds(8.5)), 6);
        Assert.Equal(0.25, OverlayFade.OpacityAt(TimeSpan.FromSeconds(9.5)), 6);
    }

    [Fact]
    public void HoldAndFadeAreTheApprovedLengths()
    {
        Assert.Equal(TimeSpan.FromSeconds(8), OverlayFade.OverlayHold);
        Assert.Equal(TimeSpan.FromSeconds(2), OverlayFade.FadeDuration);
    }
}
