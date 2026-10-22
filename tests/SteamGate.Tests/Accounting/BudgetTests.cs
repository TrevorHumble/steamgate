using SteamGate.Core;
using SteamGate.Core.Accounting;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class BudgetTests
{
    private static readonly GateSettings Defaults = GateSettings.Defaults;

    [Fact]
    public void NightLimitByWeekday()
    {
        Assert.Equal(90, Budget.NightLimitMinutes(new DateOnly(2026, 10, 22), Defaults));
        Assert.Equal(180, Budget.NightLimitMinutes(new DateOnly(2026, 10, 23), Defaults));
        Assert.Equal(180, Budget.NightLimitMinutes(new DateOnly(2026, 10, 24), Defaults));
        Assert.Equal(90, Budget.NightLimitMinutes(new DateOnly(2026, 10, 25), Defaults));
    }

    [Fact]
    public void NightLimit_FollowsTheSettings()
    {
        var custom = Defaults with { WeeknightMinutes = 45, WeekendNightMinutes = 200, WeekendNights = [DayOfWeek.Sunday] };

        Assert.Equal(200, Budget.NightLimitMinutes(new DateOnly(2026, 10, 25), custom));
        Assert.Equal(45, Budget.NightLimitMinutes(new DateOnly(2026, 10, 23), custom));
    }

    [Fact]
    public void RemainingIsSmallerOfNightAndWeek()
    {
        var local = new DateTime(2026, 10, 22, 22, 0, 0);
        var week = new DateOnly(2026, 10, 19);
        var night = new DateOnly(2026, 10, 22);

        Assert.Equal(400, Budget.RemainingSeconds(new Tally(night, 5000, week, 34000, null), Defaults, local));
        Assert.Equal(200, Budget.RemainingSeconds(new Tally(night, 5000, week, 35800, null), Defaults, local));
        Assert.Equal(0, Budget.RemainingSeconds(new Tally(night, 6000, week, 34000, null), Defaults, local));
    }

    [Fact]
    public void Remaining_WeekOverCap_IsZero()
    {
        var tally = new Tally(new DateOnly(2026, 10, 22), 0, new DateOnly(2026, 10, 19), 40000, null);

        Assert.Equal(0, Budget.RemainingSeconds(tally, Defaults, new DateTime(2026, 10, 22, 22, 0, 0)));
    }

    [Fact]
    public void Remaining_KeepsFractionalSeconds()
    {
        var tally = new Tally(new DateOnly(2026, 10, 22), 5000.5, new DateOnly(2026, 10, 19), 100, null);

        Assert.Equal(399.5, Budget.RemainingSeconds(tally, Defaults, new DateTime(2026, 10, 22, 22, 0, 0)), 6);
    }

    [Fact]
    public void StaleNightCountsAsZero()
    {
        var tally = new Tally(new DateOnly(2026, 10, 22), 5400, new DateOnly(2026, 10, 19), 5400, null);
        var local = new DateTime(2026, 10, 23, 17, 0, 0);

        Assert.Equal(10800, Budget.RemainingSeconds(tally, Defaults, local));

        var ticked = Accountant.Tick(tally, local, DateTime.UtcNow, TimeSpan.FromSeconds(5), Accountant.MaxTickStep, []);
        Assert.Equal(new DateOnly(2026, 10, 23), ticked.Night);
        Assert.Equal(0, ticked.NightSeconds);
    }

    [Fact]
    public void Remaining_StaleWeekCountsAsZero()
    {
        var tally = new Tally(new DateOnly(2026, 10, 25), 0, new DateOnly(2026, 10, 19), 36000, null);

        Assert.Equal(5400, Budget.RemainingSeconds(tally, Defaults, new DateTime(2026, 10, 26, 22, 0, 0)));
    }
}
