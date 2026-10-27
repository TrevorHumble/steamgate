using SteamGate.Core;
using SteamGate.Core.Status;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusTextTests
{
    [Fact]
    public void Meters()
    {
        Assert.Equal("43 min", StatusText.Left(2580));
        Assert.Equal("43 min", StatusText.Left(2579));
        Assert.Equal("3 h 50 min", StatusText.Left(13800));
        Assert.Equal("0 min", StatusText.Left(0));
        Assert.Equal("0 min", StatusText.Left(-30));

        Assert.Equal("47 of 90 min used", StatusText.Used(2820, 90));
        Assert.Equal("30 of 75 min used", StatusText.Used(1800, 75));
        Assert.Equal("6 h 10 min of 10 h used", StatusText.Used(22200, 600));
        Assert.Equal("1 h 30 min of 3 h used", StatusText.Used(5400, 180));
        Assert.Equal("46 of 90 min used", StatusText.Used(2819, 90));
        Assert.Equal("0 of 90 min used", StatusText.Used(-5, 90));

        Assert.Equal(52, StatusText.FillPercent(2820, 90));
        Assert.Equal(62, StatusText.FillPercent(22200, 600));
        Assert.Equal(0, StatusText.FillPercent(0, 90));
        Assert.Equal(100, StatusText.FillPercent(6000, 90));
        Assert.Equal(100, StatusText.FillPercent(0, 0));
        Assert.Equal(0, StatusText.FillPercent(-100, 90));
        Assert.Equal(51, StatusText.FillPercent(2754, 90));
        Assert.Equal(97, StatusText.FillPercent(34980, 600));

        Assert.Equal("Tonight (weeknight, ends 4 AM)", StatusText.TonightLabel(false));
        Assert.Equal("Tonight (weekend night, ends 4 AM)", StatusText.TonightLabel(true));
        Assert.Equal("This week (resets Monday 4 AM)", StatusText.WeekLabel);
    }

    [Fact]
    public void EachMeterShowsItsOwnTimeLeft()
    {
        var state = StateFixtures.State(nightSeconds: 2820, weekSeconds: 22200);
        Assert.Equal("43 min", StatusText.TonightLeft(state));
        Assert.Equal("3 h 50 min", StatusText.WeekLeft(state));

        var weekRunsOut = StateFixtures.State(nightSeconds: 0, weekSeconds: 34980, remainingSeconds: 1020);
        Assert.Equal("1 h 30 min", StatusText.TonightLeft(weekRunsOut));
        Assert.Equal("17 min", StatusText.WeekLeft(weekRunsOut));

        var pastTheLimit = StateFixtures.State(nightSeconds: 6000);
        Assert.Equal("0 min", StatusText.TonightLeft(pastTheLimit));
    }

    [Fact]
    public void TonightUsesTheNightLimitWhenTheServiceSentNone()
    {
        var weeknight = StateFixtures.State(tonightLimit: null, weekend: false, nightSeconds: 0);
        var weekendNight = StateFixtures.State(tonightLimit: null, weekend: true, nightSeconds: 0);

        Assert.Equal(90, StatusText.TonightLimit(weeknight));
        Assert.Equal(180, StatusText.TonightLimit(weekendNight));
        Assert.Equal(75, StatusText.TonightLimit(StateFixtures.State(tonightLimit: 75, weekend: true)));
        Assert.Equal("3 h", StatusText.TonightLeft(weekendNight));
    }

    [Fact]
    public void TimesAndDays()
    {
        Assert.Equal("Applies Thu, Oct 22, 7:02 PM", StatusText.AppliesAt(new DateTime(2026, 10, 22, 19, 2, 0)));
        Assert.Equal("Wed, Oct 21, 9:14 AM", StatusText.Clock(new DateTime(2026, 10, 21, 9, 14, 0)));

        Assert.Equal("in 1 d 22 h", StatusText.In(164880));
        Assert.Equal("in 1 d 0 h", StatusText.In(86400));
        Assert.Equal("in 2 h", StatusText.In(7200));
        Assert.Equal("in 1 h", StatusText.In(1800));
        Assert.Equal("in 0 h", StatusText.In(1799));
        Assert.Equal("in 23 h", StatusText.In(82800));

        Assert.Equal("Fri, Sat, Sun", StatusText.Days([DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday]));
        Assert.Equal("Fri, Sun", StatusText.Days([DayOfWeek.Sunday, DayOfWeek.Friday]));
        Assert.Equal("Mon, Tue, Wed, Thu, Fri, Sat, Sun", StatusText.Days(Enum.GetValues<DayOfWeek>()));
        Assert.Equal(string.Empty, StatusText.Days([]));
        Assert.Equal("48 h", StatusText.Hours(48));
    }

    [Fact]
    public void ReplyColors()
    {
        foreach (var green in new[]
        {
            "Applied now.",
            "Applied now. Saved in memory; will retry.",
            "Applied now. Tonight ends 20 min later (limit 1 h 50 min).",
            "Applied now. Tonight ends 20 min later (limit 1 h 50 min). Saved in memory; will retry.",
        })
        {
            Assert.True(StatusText.ReplyIsNow(green), green);
        }

        string[] amber =
        [
            "Waits 48 h 0 min.",
            "Waits 48 h 0 min. Saved in memory; will retry.",
            "Applied now: Friday. Waits 48 h 0 min: Sunday.",
            "Applied now: Friday. Waits 48 h 0 min: Sunday. Saved in memory; will retry.",
            "Applied now: Friday. Not allowed: Sunday.",
            "Applied now: Friday. Not allowed: Sunday. Saved in memory; will retry.",
            "Already set.",
            "Not in the list.",
            "No pending change with that id.",
            "Out of range: 0 to 1440.",
            "Not allowed.",
            "Busy, try again.",
            string.Empty,
            "Saved in memory; will retry.",
            "Extension used tonight: 20 min.",
            "Extend is available in the last 30 minutes before tonight's limit.",
            "Not enough time left this week.",
            "Applied now. Tonight ends",
        ];
        foreach (var text in amber)
        {
            Assert.False(StatusText.ReplyIsNow(text), text);
        }
    }

    [Fact]
    public void FixedTexts()
    {
        Assert.Equal("Loosening a rule waits the cooldown. Tightening a rule applies now.", StatusText.RulesNote);
        Assert.Equal("Removing applies now", StatusText.RemovingAppliesNow);
        Assert.Equal("Nothing waiting.", StatusText.NothingWaiting);
        Assert.Equal("Can't reach SteamGate.", StatusText.NotConnected);
        Assert.Equal("Counting now: ", StatusText.CountingNow);
        Assert.Equal("left", StatusText.LeftWord);
    }

    [Fact]
    public void OffAndRemovalTexts()
    {
        Assert.Equal("SteamGate is off.", StatusText.SteamGateOff);
        Assert.Equal("Request turn on", StatusText.RequestTurnOn);
        Assert.Equal("Removal is allowed.", StatusText.RemovalAllowed);
        Assert.Equal("Stop allowing removal", StatusText.StopAllowingRemoval);
    }

    [Fact]
    public void ExtensionLines()
    {
        var extended = StateFixtures.State(nightSeconds: 4680, weekSeconds: 22200, tonightLimit: 110, extensionMinutes: 20);

        Assert.Equal("32 min", StatusText.TonightLeft(extended));
        Assert.Equal("3 h 50 min", StatusText.WeekLeft(extended));
        Assert.Equal("78 of 110 min used (20 min extension)", StatusText.TonightUsed(extended));
        Assert.Equal("6 h 10 min of 10 h used (past the old limit counts double)", StatusText.WeekUsed(extended));
        Assert.Equal(71, StatusText.FillPercent(4680, 110));

        var plain = StateFixtures.State(nightSeconds: 4680, weekSeconds: 22200, tonightLimit: 90);
        Assert.Equal("78 of 90 min used", StatusText.TonightUsed(plain));
        Assert.Equal("6 h 10 min of 10 h used", StatusText.WeekUsed(plain));

        var weekLimits = StateFixtures.State(nightSeconds: 5400, weekSeconds: 35400, tonightLimit: 110, extensionMinutes: 20);
        Assert.Equal("20 min", StatusText.TonightLeft(weekLimits));
        Assert.Equal("10 min", StatusText.WeekLeft(weekLimits));

        var unknownLimit = StateFixtures.State(tonightLimit: null, extensionMinutes: 20);
        Assert.Equal(110, StatusText.TonightLimit(unknownLimit));
        Assert.Equal(5400, StatusText.OriginalLimitSeconds(unknownLimit));
        Assert.Equal(5400, StatusText.OriginalLimitSeconds(extended));
        Assert.Equal(5400, StatusText.OriginalLimitSeconds(plain));
    }
}
