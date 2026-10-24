using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Tests.Rules;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class VerdictLineTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 20, 21, 14, 0, DateTimeKind.Utc);

    private static (string Text, VerdictKind Kind, bool CanRequest) Verdict(GateSettings settings, ChangeRequest request) =>
        VerdictLine.For(settings, request, UtcNow, TimeZoneInfo.Utc);

    private static GateSettings FiftyWaiting(params DayOfWeek[] weekendNights) => GateSettings.Defaults with
    {
        Pending = [.. Enumerable.Range(1, 50).Select(index => RuleFixtures.FolderEntry("G" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), id: index))],
        WeekendNights = weekendNights.Length > 0 ? weekendNights : GateSettings.Defaults.WeekendNights,
    };

    [Fact]
    public void RulesEditor()
    {
        var defaults = GateSettings.Defaults;

        Assert.Equal(("This tightens the rule. It applies now.", VerdictKind.Now, true), Verdict(defaults, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 60)));
        Assert.Equal(("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", VerdictKind.Wait, true), Verdict(defaults, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120)));
        Assert.Equal(("No change yet.", VerdictKind.Wait, false), Verdict(defaults, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 90)));
        Assert.Equal(("Out of range: 0 to 1440.", VerdictKind.Wait, false), Verdict(defaults, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 2000)));
        Assert.Equal(("The cooldown can be at most 168 h (7 days).", VerdictKind.Wait, false), Verdict(defaults, RuleFixtures.Number(ChangeFields.CooldownHours, 200)));
        Assert.Equal(("This tightens the rule. It applies now.", VerdictKind.Now, true), Verdict(defaults, RuleFixtures.Days(DayOfWeek.Friday, DayOfWeek.Saturday)));
        Assert.Equal(
            ("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", VerdictKind.Wait, true),
            Verdict(defaults, RuleFixtures.Days(DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday)));

        var fridaySaturday = defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] };
        Assert.Equal(
            ("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", VerdictKind.Wait, true),
            Verdict(fridaySaturday, RuleFixtures.Days(DayOfWeek.Saturday, DayOfWeek.Sunday)));

        var noCooldown = defaults with { CooldownHours = 0 };
        Assert.Equal(
            ("This loosens the rule. It applies Tue, Oct 20, 9:14 PM (0 h).", VerdictKind.Wait, true),
            Verdict(noCooldown, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120)));

        Assert.Equal(("Not allowed.", VerdictKind.Wait, false), Verdict(FiftyWaiting(), RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120)));
        Assert.Equal(
            ("Applied now: Friday. Not allowed: Sunday.", VerdictKind.Wait, true),
            Verdict(FiftyWaiting(DayOfWeek.Friday, DayOfWeek.Saturday), RuleFixtures.Days(DayOfWeek.Saturday, DayOfWeek.Sunday)));
    }

    [Fact]
    public void CooldownIsJudgedByTheEngineUpToTheMaximum()
    {
        Assert.Equal(("This tightens the rule. It applies now.", VerdictKind.Now, true), Verdict(GateSettings.Defaults, RuleFixtures.Number(ChangeFields.CooldownHours, 168)));
        Assert.Equal(("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", VerdictKind.Wait, true), Verdict(GateSettings.Defaults, RuleFixtures.Number(ChangeFields.CooldownHours, 24)));
    }

    [Fact]
    public void TimeIsShownInTheGivenZone()
    {
        var plusFive = TimeZoneInfo.CreateCustomTimeZone("plus5", TimeSpan.FromHours(5), "plus5", "plus5");

        var verdict = VerdictLine.For(GateSettings.Defaults, RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120), UtcNow, plusFive);

        Assert.Equal("This loosens the rule. It applies Fri, Oct 23, 2:14 AM (48 h).", verdict.Text);
    }

    [Fact]
    public void RemovalOfAFolderNotOnTheListIsNotRequestable()
    {
        var verdict = Verdict(GateSettings.Defaults, RuleFixtures.Folder(ChangeFields.NotCountedRemove, "Nope"));

        Assert.Equal(("Not in the list.", VerdictKind.Wait, false), verdict);
    }

    [Fact]
    public void ForMessageJudgesWhatTheServiceWillJudge()
    {
        var weeknight = StatusRequests.Number("s1", ChangeFields.WeeknightMinutes, 120);
        var badDays = new RequestMessage("s2", ChangeFields.WeekendNights, null, null, null, ["Someday"]);

        Assert.Equal(
            ("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", VerdictKind.Wait, true),
            VerdictLine.ForMessage(GateSettings.Defaults, weeknight, UtcNow, TimeZoneInfo.Utc));
        Assert.Equal(("Not allowed.", VerdictKind.Wait, false), VerdictLine.ForMessage(GateSettings.Defaults, badDays, UtcNow, TimeZoneInfo.Utc));
    }

    [Fact]
    public void RefusedPartOfAWeekendChangeUsesTheReplyWords()
    {
        Assert.Equal("Not allowed: ", ChangeReply.NotAllowedPart);
        Assert.Equal("Applied now: Friday. Not allowed: Sunday.", ChangeReply.AppliedNowAndNotAllowed([DayOfWeek.Friday], [DayOfWeek.Sunday]));
    }

    [Fact]
    public void PanelLines()
    {
        Assert.Equal("Adding a program loosens the rules. It waits 48 h.", VerdictLine.NotCountedAdd(48));
        Assert.Equal("Adding a program loosens the rules. It waits 24 h.", VerdictLine.NotCountedAdd(24));
        Assert.Equal("Both wait 48 h.", VerdictLine.TurnOffOrRemove(48));
        Assert.Equal("Both wait 24 h.", VerdictLine.TurnOffOrRemove(24));
        Assert.Equal("Loosening a rule waits the cooldown. Tightening a rule applies now.", StatusText.RulesNote);
        Assert.Equal("Removing applies now", StatusText.RemovingAppliesNow);
        Assert.Equal("Nothing waiting.", StatusText.NothingWaiting);
        Assert.Equal("Can't reach SteamGate.", StatusText.NotConnected);
    }
}
