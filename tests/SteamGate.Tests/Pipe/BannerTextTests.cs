using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class BannerTextTests
{
    [Fact]
    public void Wording()
    {
        Assert.Equal("1 minute of gaming left.", BannerText.ForWarn(1));
        Assert.Equal("5 minutes of gaming left.", BannerText.ForWarn(5));
        Assert.Equal("30 minutes of gaming left.", BannerText.ForWarn(30));
        Assert.Equal("22 minutes of gaming left.", BannerText.ForWarn(22));
        Assert.Equal("Time is up. Closing Viewfinder.", BannerText.ForClose("Viewfinder"));
    }
}
