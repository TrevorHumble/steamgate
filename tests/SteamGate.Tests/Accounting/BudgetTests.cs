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
        Assert.Equal(180, Budget.NightLimitMinutes(new DateOnly(2026, 10, 25), Defaults));
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

        var ticked = Accountant.Tick(tally, local, DateTime.UtcNow, TimeSpan.FromSeconds(5), Accountant.MaxTickStep, [], GateSettings.Defaults);
        Assert.Equal(new DateOnly(2026, 10, 23), ticked.Night);
        Assert.Equal(0, ticked.NightSeconds);
    }

    [Fact]
    public void Remaining_StaleWeekCountsAsZero()
    {
        var tally = new Tally(new DateOnly(2026, 10, 25), 0, new DateOnly(2026, 10, 19), 36000, null);

        Assert.Equal(5400, Budget.RemainingSeconds(tally, Defaults, new DateTime(2026, 10, 26, 22, 0, 0)));
    }

    [Fact]
    public void ExtensionRaisesTheLimitAndTheWeekCostsDouble()
    {
        var monday = new DateOnly(2026, 10, 26);
        var local = new DateTime(2026, 10, 26, 21, 0, 0);

        Tally Of(double night, double week, int minutes) => new(monday, night, monday, week, null, new NightExtension(monday, minutes));

        var twenty = Of(4680, 22200, 20);
        Assert.Equal(110, Budget.TonightLimitMinutes(twenty, Defaults, local));
        Assert.Equal(1920, Budget.RemainingSeconds(twenty, Defaults, local));
        Assert.Equal(900, Budget.RemainingSeconds(Of(5700, 22200, 20), Defaults, local));
        Assert.Equal(300, Budget.RemainingSeconds(Of(5400, 35400, 20), Defaults, local));
        Assert.Equal(250, Budget.RemainingSeconds(Of(5300, 35600, 30), Defaults, local));
        Assert.Equal(300, Budget.RemainingSeconds(Of(4800, 35700, 20), Defaults, local));

        var plain = new Tally(monday, 4680, monday, 22200, null);
        Assert.Equal(90, Budget.TonightLimitMinutes(plain, Defaults, local));
        Assert.Equal(720, Budget.RemainingSeconds(plain, Defaults, local));

        var yesterday = new Tally(new DateOnly(2026, 10, 25), 4680, monday, 22200, null, new NightExtension(new DateOnly(2026, 10, 25), 20));
        Assert.Equal(90, Budget.TonightLimitMinutes(yesterday, Defaults, local));
    }

    [Fact]
    public void AnExtensionOfZeroMinutesCountsAsNoneEverywhere()
    {
        var monday = new DateOnly(2026, 10, 26);
        var local = new DateTime(2026, 10, 26, 21, 0, 0);
        var zero = new Tally(monday, 5400, monday, 10000, null, new NightExtension(monday, 0));
        var none = zero with { Extension = null };

        Assert.False(zero.HasExtension);
        Assert.Equal(0, zero.ExtensionMinutes);
        Assert.Equal(Budget.RemainingSeconds(none, Defaults, local), Budget.RemainingSeconds(zero, Defaults, local));
        Assert.Equal(Budget.TonightLimitMinutes(none, Defaults, local), Budget.TonightLimitMinutes(zero, Defaults, local));
        var ticked = Accountant.Tick(zero, local, DateTime.UtcNow, TimeSpan.FromSeconds(15), Accountant.MaxTickStep, [new SteamGate.Core.Steam.RunningGame("g", "g", [])], Defaults);
        Assert.Equal(10015, ticked.WeekSeconds);
    }

    [Fact]
    public void OriginalLimitSeconds()
    {
        Assert.Equal(5400, Budget.OriginalLimitSeconds(Defaults, new DateTime(2026, 10, 26, 21, 0, 0)));
        Assert.Equal(10800, Budget.OriginalLimitSeconds(Defaults, new DateTime(2026, 10, 30, 21, 0, 0)));
        Assert.Equal(10800, Budget.OriginalLimitSeconds(Defaults, new DateTime(2026, 10, 31, 2, 0, 0)));
        Assert.Equal(10800, Budget.OriginalLimitSeconds(Defaults, new DateTime(2026, 10, 26, 3, 0, 0)));
    }
}
