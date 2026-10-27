using SteamGate.Core;
using Xunit;

namespace SteamGate.Tests;

public sealed class DurationTextTests
{
    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h")]
    [InlineData(61, "1 h 1 min")]
    [InlineData(90, "1 h 30 min")]
    [InlineData(600, "10 h")]
    [InlineData(0, "0 min")]
    [InlineData(230, "3 h 50 min")]
    [InlineData(59, "59 min")]
    public void Minutes(int minutes, string expected)
    {
        Assert.Equal(expected, DurationText.Minutes(minutes));
    }
}
