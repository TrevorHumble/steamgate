using SteamGate.Core.Accounting;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class StepTimerTests
{
    private static readonly DateTime Utc = new(2026, 10, 22, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstCall_ReturnsRestartStepAndMaxRestartGap()
    {
        var loaded = Tally.Empty with { LastCountedUtc = Utc.AddSeconds(-20) };

        var (step, maxStep) = new StepTimer().Next(loaded, Utc, anyGameOpen: true);

        Assert.Equal(TimeSpan.FromSeconds(20), step);
        Assert.Equal(Accountant.MaxRestartGap, maxStep);
    }

    [Fact]
    public void FirstCall_WithNoGameOpen_ReturnsZeroStep()
    {
        var loaded = Tally.Empty with { LastCountedUtc = Utc.AddSeconds(-20) };

        var (step, maxStep) = new StepTimer().Next(loaded, Utc, anyGameOpen: false);

        Assert.Equal(TimeSpan.Zero, step);
        Assert.Equal(Accountant.MaxRestartGap, maxStep);
    }

    [Fact]
    public void LaterCalls_ReturnElapsedAndMaxTickStep()
    {
        var timer = new StepTimer();
        timer.Next(Tally.Empty, Utc, anyGameOpen: false);

        Thread.Sleep(50);
        var (step, maxStep) = timer.Next(Tally.Empty, Utc, anyGameOpen: true);

        Assert.InRange(step, TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(10));
        Assert.Equal(Accountant.MaxTickStep, maxStep);
    }
}
