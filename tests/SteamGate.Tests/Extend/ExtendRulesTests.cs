using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Extend;
using Xunit;

namespace SteamGate.Tests.Extend;

public sealed class ExtendRulesTests
{
    private const string OutsideNote = "Extend is available in the last 30 minutes before tonight's limit.";
    private const string OpenNote = "Once per night, in the last 30 minutes before tonight's limit.";
    private const string WeekNote = "Not enough time left this week.";

    private static readonly DateTime MondayNight = new(2026, 10, 26, 21, 0, 0);
    private static readonly DateOnly Monday = new(2026, 10, 26);

    private static Tally TallyOf(double night, double week, NightExtension? extension = null) =>
        new(Monday, night, Monday, week, null, extension);

    [Fact]
    public void ViewRules()
    {
        var open = ExtendRules.View(5400, 0, 4680, 600, 22200);
        Assert.Equal(ExtendKind.Open, open.Kind);
        Assert.Equal([10, 20, 30], open.Offered);
        Assert.Equal(OpenNote, open.Note);

        Assert.Equal(ExtendKind.Open, ExtendRules.View(5400, 0, 3600, 600, 0).Kind);
        var early = ExtendRules.View(5400, 0, 3599.9, 600, 0);
        Assert.Equal(ExtendKind.NotInWindow, early.Kind);
        Assert.Equal(OutsideNote, early.Note);
        Assert.Empty(early.Offered);

        Assert.Equal(ExtendKind.Open, ExtendRules.View(5400, 0, 5399.9, 600, 0).Kind);
        foreach (var night in new[] { 5400.0, 6000.0 })
        {
            var late = ExtendRules.View(5400, 0, night, 600, 0);
            Assert.Equal(ExtendKind.NotInWindow, late.Kind);
            Assert.Equal(OutsideNote, late.Note);
            Assert.Empty(late.Offered);
        }

        var used = ExtendRules.View(5400, 20, 4680, 600, 22200);
        Assert.Equal(ExtendKind.Used, used.Kind);
        Assert.Equal("Extension used tonight: 20 min.", used.Note);
        Assert.Empty(used.Offered);
        Assert.Equal(ExtendKind.Used, ExtendRules.View(5400, 20, 5400, 600, 0).Kind);

        var some = ExtendRules.View(5400, 0, 4680, 600, 32400);
        Assert.Equal([10, 20], some.Offered);
        Assert.Equal(WeekNote, some.Note);

        Assert.Equal([10], ExtendRules.View(5400, 0, 4680, 600, 34080).Offered);
        var justOver = ExtendRules.View(5400, 0, 4680, 600, 34080.1);
        Assert.Empty(justOver.Offered);
        Assert.Equal(WeekNote, justOver.Note);

        foreach (var week in new[] { 34500.0, 36000.0, 40000.0 })
        {
            var none = ExtendRules.View(5400, 0, 4680, 600, week);
            Assert.Equal(ExtendKind.Open, none.Kind);
            Assert.Empty(none.Offered);
            Assert.Equal(WeekNote, none.Note);
        }

        Assert.Equal(1800, ExtendRules.WindowSeconds);
        Assert.Equal(30, ExtendRules.WindowSeconds / 60);
        Assert.Equal(20, ExtendRules.DefaultLength);
        Assert.Contains(ExtendRules.DefaultLength, NightExtension.Lengths);
    }

    [Fact]
    public void ApplyRules()
    {
        var tally = TallyOf(4680, 22200);

        var applied = ExtendRules.Apply(tally, GateSettings.Defaults, MondayNight, 20);
        Assert.True(applied.Applied);
        Assert.Equal("Applied now. Tonight ends 20 min later (limit 1 h 50 min).", applied.Text);
        Assert.Equal(new NightExtension(Monday, 20), applied.Updated.Extension);
        Assert.Equal(tally with { Extension = new NightExtension(Monday, 20) }, applied.Updated);

        var again = ExtendRules.Apply(applied.Updated, GateSettings.Defaults, MondayNight, 10);
        Assert.False(again.Applied);
        Assert.Equal("Extension used tonight: 20 min.", again.Text);
        Assert.Equal(applied.Updated, again.Updated);

        foreach (var wrong in new[] { 25, 0, -10, 40, int.MaxValue })
        {
            var refused = ExtendRules.Apply(tally, GateSettings.Defaults, MondayNight, wrong);
            Assert.False(refused.Applied);
            Assert.Equal("Not allowed.", refused.Text);
            Assert.Equal(tally, refused.Updated);
        }

        Assert.Equal(OutsideNote, ExtendRules.Apply(TallyOf(3000, 22200), GateSettings.Defaults, MondayNight, 20).Text);
        Assert.True(ExtendRules.Apply(TallyOf(3600, 22200), GateSettings.Defaults, MondayNight, 20).Applied);
        Assert.Equal(OutsideNote, ExtendRules.Apply(TallyOf(5400, 22200), GateSettings.Defaults, MondayNight, 20).Text);
        Assert.Equal(OutsideNote, ExtendRules.Apply(TallyOf(6000, 22200), GateSettings.Defaults, MondayNight, 20).Text);

        var tightWeek = TallyOf(4680, 32400);
        var tooLong = ExtendRules.Apply(tightWeek, GateSettings.Defaults, MondayNight, 30);
        Assert.Equal(WeekNote, tooLong.Text);
        Assert.Equal(tightWeek, tooLong.Updated);
        Assert.True(ExtendRules.Apply(tightWeek, GateSettings.Defaults, MondayNight, 20).Applied);
        Assert.Equal(WeekNote, ExtendRules.Apply(TallyOf(4680, 34100), GateSettings.Defaults, MondayNight, 10).Text);

        var stale = TallyOf(4680, 22200, new NightExtension(new DateOnly(2026, 10, 25), 20));
        var fresh = ExtendRules.Apply(stale, GateSettings.Defaults, MondayNight, 20);
        Assert.True(fresh.Applied);
        Assert.Equal(new NightExtension(Monday, 20), fresh.Updated.Extension);

        var friday = new DateOnly(2026, 10, 30);
        var weekend = ExtendRules.Apply(new Tally(friday, 10200, Monday, 22200, null), GateSettings.Defaults, new DateTime(2026, 10, 30, 21, 0, 0), 30);
        Assert.Equal("Applied now. Tonight ends 30 min later (limit 3 h 30 min).", weekend.Text);

        var small = GateSettings.Defaults with { WeeknightMinutes = 20, WeeklyCapMinutes = 50 };
        var smallTally = TallyOf(0, 0);
        Assert.Equal(WeekNote, ExtendRules.Apply(smallTally, small, MondayNight, 20).Text);
        Assert.Equal("Applied now. Tonight ends 10 min later (limit 30 min).", ExtendRules.Apply(smallTally, small, MondayNight, 10).Text);

        var yesterday = new Tally(new DateOnly(2026, 10, 25), 4680, new DateOnly(2026, 10, 19), 22200, null);
        var rolledOver = ExtendRules.Apply(yesterday, GateSettings.Defaults, MondayNight, 20);
        Assert.Equal(OutsideNote, rolledOver.Text);
        Assert.Equal(yesterday, rolledOver.Updated);
    }

    [Theory]
    [InlineData(10, 1320)]
    [InlineData(20, 1920)]
    [InlineData(30, 2520)]
    public void TonightEndsWhenTheTextSays(int minutes, double expected)
    {
        var updated = ExtendRules.Apply(TallyOf(4680, 22200), GateSettings.Defaults, MondayNight, minutes).Updated;

        Assert.Equal(expected, Budget.RemainingSeconds(updated, GateSettings.Defaults, MondayNight));
        Assert.Equal(((90 + minutes) * 60) - 4680, Budget.RemainingSeconds(updated, GateSettings.Defaults, MondayNight));
    }

    [Fact]
    public void ExactlyEnoughWeekStillTellsTheTruth()
    {
        var tally = TallyOf(4680, 34080);

        var result = ExtendRules.Apply(tally, GateSettings.Defaults, MondayNight, 10);

        Assert.True(result.Applied);
        Assert.Equal(1320, Budget.RemainingSeconds(result.Updated, GateSettings.Defaults, MondayNight));
    }
}
