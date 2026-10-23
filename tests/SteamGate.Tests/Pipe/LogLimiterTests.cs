using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class LogLimiterTests
{
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void FirstTwentyAreLoggedThenOneNoticeThenNothing()
    {
        var limiter = new LogLimiter();

        for (var hello = 1; hello <= 20; hello++)
        {
            Assert.Equal(LogAction.Log, limiter.Next(At(hello * 0.1)));
        }

        Assert.Equal(LogAction.NoticeSuppressed, limiter.Next(At(3)));
        Assert.Equal(LogAction.Skip, limiter.Next(At(4)));
        Assert.Equal(LogAction.Skip, limiter.Next(At(59)));
    }

    [Fact]
    public void ANewMinuteStartsAgain()
    {
        var limiter = new LogLimiter();
        for (var hello = 0; hello < 25; hello++)
        {
            limiter.Next(At(1));
        }

        Assert.Equal(LogAction.Skip, limiter.Next(At(60.9)));
        Assert.Equal(LogAction.Log, limiter.Next(At(61)));
        for (var hello = 0; hello < 19; hello++)
        {
            Assert.Equal(LogAction.Log, limiter.Next(At(62)));
        }

        Assert.Equal(LogAction.NoticeSuppressed, limiter.Next(At(63)));
    }

    [Fact]
    public void OneHelloIsAlwaysLogged()
    {
        Assert.Equal(LogAction.Log, new LogLimiter().Next(TimeSpan.Zero));
        Assert.Equal(LogAction.Log, new LogLimiter().Next(At(5000)));
        Assert.Equal(20, LogLimiter.MaxPerMinute);
    }
}
