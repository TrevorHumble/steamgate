using SteamGate.Core.Accounting;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class ExtensionCostTests
{
    [Fact]
    public void Values()
    {
        Assert.Equal(2, ExtensionCost.PastLimitWeight);

        Assert.Equal(20, ExtensionCost.WeekCharge(5390, 15, 5400, true));
        Assert.Equal(30, ExtensionCost.WeekCharge(5400, 15, 5400, true));
        Assert.Equal(15, ExtensionCost.WeekCharge(5400, 15, 5400, false));
        Assert.Equal(15, ExtensionCost.WeekCharge(5000, 15, 5400, true));
        Assert.Equal(30, ExtensionCost.WeekCharge(7200, 15, 5400, true));
        Assert.Equal(30, ExtensionCost.WeekCharge(7195, 15, 5400, true));

        Assert.Equal(6950, ExtensionCost.PlayableWeekSeconds(13800, 5300, 5400, true));
        Assert.Equal(250, ExtensionCost.PlayableWeekSeconds(400, 5300, 5400, true));
        Assert.Equal(300, ExtensionCost.PlayableWeekSeconds(300, 4800, 5400, true));
        Assert.Equal(13800, ExtensionCost.PlayableWeekSeconds(13800, 5300, 5400, false));
        Assert.Equal(2400, ExtensionCost.FullExtensionWeekSeconds(20));
    }
}
