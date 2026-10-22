using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class HelloLogLimiterTests
{
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void FirstTwentyAreLoggedThenOneNoticeThenNothing()
    {
        var limiter = new HelloLogLimiter();

        for (var hello = 1; hello <= 20; hello++)
        {
            Assert.Equal(HelloLogAction.Log, limiter.Next(At(hello * 0.1)));
        }

        Assert.Equal(HelloLogAction.NoticeSuppressed, limiter.Next(At(3)));
        Assert.Equal(HelloLogAction.Skip, limiter.Next(At(4)));
        Assert.Equal(HelloLogAction.Skip, limiter.Next(At(59)));
    }

    [Fact]
    public void ANewMinuteStartsAgain()
    {
        var limiter = new HelloLogLimiter();
        for (var hello = 0; hello < 25; hello++)
        {
            limiter.Next(At(1));
        }

        Assert.Equal(HelloLogAction.Skip, limiter.Next(At(60.9)));
        Assert.Equal(HelloLogAction.Log, limiter.Next(At(61)));
        for (var hello = 0; hello < 19; hello++)
        {
            Assert.Equal(HelloLogAction.Log, limiter.Next(At(62)));
        }

        Assert.Equal(HelloLogAction.NoticeSuppressed, limiter.Next(At(63)));
    }

    [Fact]
    public void OneHelloIsAlwaysLogged()
    {
        Assert.Equal(HelloLogAction.Log, new HelloLogLimiter().Next(TimeSpan.Zero));
        Assert.Equal(HelloLogAction.Log, new HelloLogLimiter().Next(At(5000)));
        Assert.Equal(20, HelloLogLimiter.MaxPerMinute);
    }
}
