using SteamGate.Core.Accounting;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class NightClockTests
{
    [Fact]
    public void NightEndsAt4AM()
    {
        Assert.Equal(new DateOnly(2026, 10, 23), NightClock.NightOf(new DateTime(2026, 10, 24, 3, 59, 0)));
        Assert.Equal(new DateOnly(2026, 10, 24), NightClock.NightOf(new DateTime(2026, 10, 24, 4, 0, 0)));
        Assert.Equal(new DateOnly(2026, 10, 24), NightClock.NightOf(new DateTime(2026, 10, 24, 23, 59, 0)));
    }

    [Fact]
    public void WeekResetsMonday4AM()
    {
        Assert.Equal(new DateOnly(2026, 10, 19), NightClock.WeekOf(new DateTime(2026, 10, 25, 23, 0, 0)));
        Assert.Equal(new DateOnly(2026, 10, 19), NightClock.WeekOf(new DateTime(2026, 10, 26, 3, 59, 0)));
        Assert.Equal(new DateOnly(2026, 10, 26), NightClock.WeekOf(new DateTime(2026, 10, 26, 4, 0, 0)));
    }

    [Fact]
    public void WeekOf_MondayThroughSunday_AllNamePreviousMonday()
    {
        for (var day = 19; day <= 25; day++)
        {
            Assert.Equal(new DateOnly(2026, 10, 19), NightClock.WeekOf(new DateTime(2026, 10, day, 12, 0, 0)));
        }
    }

    [Fact]
    public void RollOver_SameNightAndWeek_KeepsCounts()
    {
        var tally = new Tally(new DateOnly(2026, 10, 22), 3000, new DateOnly(2026, 10, 19), 20000, null);

        var result = NightClock.RollOver(tally, new DateTime(2026, 10, 22, 21, 0, 0));

        Assert.Equal(tally, result);
    }

    [Fact]
    public void RollOver_NewNightSameWeek_ZerosOnlyTheNight()
    {
        var tally = new Tally(new DateOnly(2026, 10, 22), 3000, new DateOnly(2026, 10, 19), 20000, null);

        var result = NightClock.RollOver(tally, new DateTime(2026, 10, 23, 17, 0, 0));

        Assert.Equal(new DateOnly(2026, 10, 23), result.Night);
        Assert.Equal(0, result.NightSeconds);
        Assert.Equal(new DateOnly(2026, 10, 19), result.Week);
        Assert.Equal(20000, result.WeekSeconds);
    }

    [Fact]
    public void RollOver_NewWeek_ZerosBoth_AndKeepsLastCounted()
    {
        var last = new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc);
        var tally = new Tally(new DateOnly(2026, 10, 25), 3000, new DateOnly(2026, 10, 19), 30000, last);

        var result = NightClock.RollOver(tally, new DateTime(2026, 10, 26, 4, 0, 0));

        Assert.Equal(new Tally(new DateOnly(2026, 10, 26), 0, new DateOnly(2026, 10, 26), 0, last), result);
    }

    [Fact]
    public void RollOver_EmptyTally_NamesTheNightAndWeek()
    {
        var result = NightClock.RollOver(Tally.Empty, new DateTime(2026, 10, 22, 21, 0, 0));

        Assert.Equal(new Tally(new DateOnly(2026, 10, 22), 0, new DateOnly(2026, 10, 19), 0, null), result);
    }

    [Fact]
    public void ExtensionLivesOneNight()
    {
        var tonight = new DateOnly(2026, 10, 26);
        var tally = new Tally(tonight, 5000, tonight, 9000, null, new NightExtension(tonight, 20));

        Assert.Equal(tally, NightClock.RollOver(tally, new DateTime(2026, 10, 26, 21, 0, 0)));
        Assert.Equal(tally, NightClock.RollOver(tally, new DateTime(2026, 10, 27, 3, 59, 0)));

        var next = NightClock.RollOver(tally, new DateTime(2026, 10, 27, 4, 0, 0));
        Assert.Equal(new DateOnly(2026, 10, 27), next.Night);
        Assert.Equal(0, next.NightSeconds);
        Assert.Equal(9000, next.WeekSeconds);
        Assert.Null(next.Extension);

        var stale = tally with { Extension = new NightExtension(new DateOnly(2026, 10, 25), 20) };
        Assert.Null(NightClock.RollOver(stale, new DateTime(2026, 10, 26, 21, 0, 0)).Extension);
    }
}
