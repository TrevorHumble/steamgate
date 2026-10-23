using SteamGate.Core;
using SteamGate.Core.Rules;
using Xunit;

namespace SteamGate.Tests.Rules;

public sealed class FieldTableTests
{
    [Fact]
    public void EveryFieldNameIsListedOnceAndFound()
    {
        Assert.Equal(11, ChangeFields.All.Count);
        Assert.Equal(11, ChangeFields.All.Select(spec => spec.Name).Distinct().Count());
        foreach (var spec in ChangeFields.All)
        {
            Assert.Same(spec, ChangeFields.Find(spec.Name));
        }

        Assert.Null(ChangeFields.Find("bogus"));
        Assert.Null(ChangeFields.Find(null));
    }

    [Fact]
    public void RequestableAndPendableFieldsAreExactlyTheApprovedOnes()
    {
        Assert.Equal(
            ["weeknightMinutes", "weekendNightMinutes", "weeklyCapMinutes", "cooldownHours", "enabled", "allowUninstall", "notCountedAdd", "notCountedRemove", "weekendNights"],
            ChangeFields.All.Where(spec => spec.Requestable).Select(spec => spec.Name));
        Assert.Equal(
            ["weeknightMinutes", "weekendNightMinutes", "weeklyCapMinutes", "cooldownHours", "enabled", "allowUninstall", "notCountedAdd", "weekendNightsAdd", "weekendNightsRemove"],
            ChangeFields.All.Where(spec => spec.Pendable).Select(spec => spec.Name));
    }

    [Fact]
    public void NumberSpecsReadAndWriteTheirSettingAndSayWhichWayLoosens()
    {
        var s = GateSettings.Defaults;
        var weeknight = ChangeFields.Find(ChangeFields.WeeknightMinutes)!;
        var cooldown = ChangeFields.Find(ChangeFields.CooldownHours)!;

        Assert.Equal(90, weeknight.GetNumber!(s));
        Assert.Equal(7, weeknight.SetNumber!(s, 7).WeeknightMinutes);
        Assert.True(weeknight.HigherLoosens);
        Assert.Equal(1440, weeknight.Max);
        Assert.Equal(48, cooldown.GetNumber!(s));
        Assert.Equal(7, cooldown.SetNumber!(s, 7).CooldownHours);
        Assert.False(cooldown.HigherLoosens);
        Assert.Equal(168, cooldown.Max);
        Assert.True(weeknight.InRange(0));
        Assert.True(weeknight.InRange(1440));
        Assert.False(weeknight.InRange(-1));
        Assert.False(weeknight.InRange(1441));
    }

    [Fact]
    public void FlagSpecsReadAndWriteTheirSettingAndSayWhichValueLoosens()
    {
        var s = GateSettings.Defaults;
        var enabled = ChangeFields.Find(ChangeFields.Enabled)!;
        var allow = ChangeFields.Find(ChangeFields.AllowUninstall)!;

        Assert.True(enabled.GetFlag!(s));
        Assert.False(enabled.SetFlag!(s, false).Enabled);
        Assert.False(enabled.LoosensWhenTrue);
        Assert.False(allow.GetFlag!(s));
        Assert.True(allow.SetFlag!(s, true).AllowUninstall);
        Assert.True(allow.LoosensWhenTrue);
    }

    [Fact]
    public void ValuesAreCheckedByKind()
    {
        var number = ChangeFields.Find(ChangeFields.WeeknightMinutes)!;
        var flag = ChangeFields.Find(ChangeFields.Enabled)!;
        var folder = ChangeFields.Find(ChangeFields.NotCountedAdd)!;
        var days = ChangeFields.Find(ChangeFields.WeekendNightsAdd)!;

        Assert.True(number.IsValidValue(5, null, null, null));
        Assert.False(number.IsValidValue(1441, null, null, null));
        Assert.False(number.IsValidValue(null, true, null, null));
        Assert.True(flag.IsValidValue(null, false, null, null));
        Assert.False(flag.IsValidValue(1, false, null, null));
        Assert.True(folder.IsValidValue(null, null, "x", null));
        Assert.False(folder.IsValidValue(null, null, "x/y", null));
        Assert.True(days.IsValidValue(null, null, null, [DayOfWeek.Monday]));
        Assert.False(days.IsValidValue(null, null, null, []));
        Assert.False(days.IsValidValue(null, null, null, [DayOfWeek.Monday, DayOfWeek.Monday]));
        Assert.False(days.IsValidValue(null, null, null, [(DayOfWeek)9]));
        Assert.True(ChangeFields.AreDistinctDays([]));
        Assert.False(ChangeFields.AreDistinctDays([DayOfWeek.Friday, DayOfWeek.Friday]));
    }

    [Fact]
    public void NumbersInRangeChecksEveryNumberField()
    {
        var s = GateSettings.Defaults;

        Assert.True(ChangeFields.NumbersInRange(s));
        Assert.False(ChangeFields.NumbersInRange(s with { WeeknightMinutes = 1441 }));
        Assert.False(ChangeFields.NumbersInRange(s with { WeekendNightMinutes = -1 }));
        Assert.False(ChangeFields.NumbersInRange(s with { WeeklyCapMinutes = 10081 }));
        Assert.False(ChangeFields.NumbersInRange(s with { CooldownHours = 169 }));
        Assert.Equal(604800, ChangeFields.MaxWaitSeconds);
    }
}
