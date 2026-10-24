using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Tests.Rules;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class PendingTextTests
{
    // Default settings, except that wallpaper_engine is no longer on the not-counted list and is not installed; Viewfinder can be added.
    private static readonly StateMessage State = StateFixtures.State(
        GateSettings.Defaults with { NotCounted = ["Blender", "OBS Studio", "Steamworks Shared"] },
        addable: [StateFixtures.Named("Viewfinder")]);

    private static string Text(PendingChange entry) => PendingText.For(entry, State);

    [Fact]
    public void Rows()
    {
        Assert.Equal("Weeknight limit: 1 h 30 min to 2 h", Text(RuleFixtures.NumberEntry(ChangeFields.WeeknightMinutes, 120)));
        Assert.Equal("Weekend night limit: 3 h to 4 h", Text(RuleFixtures.NumberEntry(ChangeFields.WeekendNightMinutes, 240)));
        Assert.Equal("Weekly cap: 10 h to 12 h", Text(RuleFixtures.NumberEntry(ChangeFields.WeeklyCapMinutes, 720)));
        Assert.Equal("Cooldown: 48 h to 24 h", Text(RuleFixtures.NumberEntry(ChangeFields.CooldownHours, 24)));
        Assert.Equal("Weekend nights: Fri, Sat, Sun to Thu, Fri, Sat, Sun", Text(RuleFixtures.DaysEntry(ChangeFields.WeekendNightsAdd, 100, 1, DayOfWeek.Thursday)));
        Assert.Equal("Weekend nights: Fri, Sat, Sun to Fri, Sat", Text(RuleFixtures.DaysEntry(ChangeFields.WeekendNightsRemove, 100, 1, DayOfWeek.Sunday)));
        Assert.Equal("Stop counting Viewfinder", Text(RuleFixtures.FolderEntry("Viewfinder")));
        Assert.Equal("Stop counting Gone Game", Text(RuleFixtures.FolderEntry("Gone Game")));
        Assert.Equal("Stop counting Wallpaper Engine", Text(RuleFixtures.FolderEntry("wallpaper_engine")));
        Assert.Equal("Turn SteamGate off", Text(RuleFixtures.FlagEntry(ChangeFields.Enabled, false)));
        Assert.Equal("Allow SteamGate to be removed", Text(RuleFixtures.FlagEntry(ChangeFields.AllowUninstall, true)));
        Assert.Equal("enabled true", Text(RuleFixtures.FlagEntry(ChangeFields.Enabled, true)));
        Assert.Equal("allowUninstall false", Text(RuleFixtures.FlagEntry(ChangeFields.AllowUninstall, false)));
    }

    [Fact]
    public void AnEntryNoOneMakesShowsItsFieldAndValue()
    {
        var folder = new PendingChange(RuleFixtures.IdNumber(1), ChangeFields.NotCountedRemove, null, null, "Blender", null, RuleFixtures.Now, 10);
        var number = new PendingChange(RuleFixtures.IdNumber(2), "weeklyCapMinutes", null, null, null, null, RuleFixtures.Now, 10);
        var strayNumber = new PendingChange(RuleFixtures.IdNumber(3), ChangeFields.Enabled, 7, null, null, null, RuleFixtures.Now, 10);
        var days = new PendingChange(RuleFixtures.IdNumber(4), ChangeFields.WeekendNights, null, null, null, [DayOfWeek.Sunday, DayOfWeek.Monday], RuleFixtures.Now, 10);

        Assert.Equal("notCountedRemove Blender", Text(folder));
        Assert.Equal("weeklyCapMinutes", Text(number));
        Assert.Equal("enabled 7", Text(strayNumber));
        Assert.Equal("weekendNights Mon, Sun", Text(days));
    }
}
