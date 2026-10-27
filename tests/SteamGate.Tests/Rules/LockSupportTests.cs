using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using Xunit;
using static SteamGate.Tests.Rules.RuleFixtures;

namespace SteamGate.Tests.Rules;

public sealed class DayNamesTests
{
    [Fact]
    public void NamesOnly()
    {
        Assert.True(DayNames.TryParse(["saturday", "Sunday"], out var days));
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], days);

        Assert.False(DayNames.TryParse(["6"], out var six));
        Assert.Empty(six);
        Assert.False(DayNames.TryParse(["7"], out _));
        Assert.False(DayNames.TryParse(["Friday", "Funday"], out var partly));
        Assert.Empty(partly);
        Assert.False(DayNames.TryParse([null!], out _));
        Assert.False(DayNames.TryParse([" Friday"], out _));
    }

    [Fact]
    public void ParsingKeepsOrderAndRepeatsAndAcceptsAnEmptyList()
    {
        Assert.True(DayNames.TryParse(["SUNDAY", "monday", "SUNDAY"], out var days));
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Sunday], days);
        Assert.True(DayNames.TryParse([], out var none));
        Assert.Empty(none);
    }

    [Fact]
    public void MondayFirstPutsSundayLastAndDropsRepeats()
    {
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            DayNames.MondayFirst(Enum.GetValues<DayOfWeek>()));
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Sunday], DayNames.MondayFirst([DayOfWeek.Sunday, DayOfWeek.Friday, DayOfWeek.Sunday]));
        Assert.Equal("Friday, Sunday", DayNames.Format([DayOfWeek.Sunday, DayOfWeek.Friday]));
        Assert.Equal("Saturday", DayNames.Format([DayOfWeek.Saturday]));
        Assert.Equal(string.Empty, DayNames.Format([]));
    }
}

public sealed class ChangeReplyTests
{
    [Theory]
    [InlineData(172800, "48 h 0 min")]
    [InlineData(0, "0 h 0 min")]
    [InlineData(1, "0 h 1 min")]
    [InlineData(60, "0 h 1 min")]
    [InlineData(61, "0 h 2 min")]
    [InlineData(3599, "1 h 0 min")]
    [InlineData(3600, "1 h 0 min")]
    [InlineData(3601, "1 h 1 min")]
    [InlineData(86399.5, "24 h 0 min")]
    [InlineData(-30, "0 h 0 min")]
    public void HoursAndMinutesRoundUpToAMinute(double seconds, string expected)
    {
        Assert.Equal(expected, ChangeReply.HoursMinutes(seconds));
        Assert.Equal("Waits " + expected + ".", ChangeReply.Waits(seconds));
    }

    [Fact]
    public void TextsAreExact()
    {
        Assert.Equal("Applied now.", ChangeReply.AppliedNow);
        Assert.Equal("Already set.", ChangeReply.AlreadySet);
        Assert.Equal("Not in the list.", ChangeReply.NotInList);
        Assert.Equal("No pending change with that id.", ChangeReply.NoPendingChange);
        Assert.Equal("Not allowed.", ChangeReply.NotAllowed);
        Assert.Equal("Busy, try again.", ChangeReply.Busy);
        Assert.Equal(" Saved in memory; will retry.", ChangeReply.SaveFailedSuffix);
        Assert.Equal("Out of range: 0 to 168.", ChangeReply.OutOfRange(0, 168));
        Assert.Equal("Applied now: Friday, Saturday. Waits 2 h 30 min: Sunday.", ChangeReply.AppliedNowAndWaits([DayOfWeek.Saturday, DayOfWeek.Friday], 9000, [DayOfWeek.Sunday]));
        Assert.Equal("Applied now: Friday. Not allowed: Monday, Sunday.", ChangeReply.AppliedNowAndNotAllowed([DayOfWeek.Friday], [DayOfWeek.Sunday, DayOfWeek.Monday]));
    }

    [Fact]
    public void ForAnOutcomeCarriesTheTextOkFlagAndPendingId()
    {
        var pendingId = IdNumber(7);
        var waits = new ChangeOutcome(GateSettings.Defaults, ChangeResult.Pending, "Waits 48 h 0 min.", pendingId, Changed: true, Loosens: true);
        var refused = new ChangeOutcome(GateSettings.Defaults, ChangeResult.Refused, "Not allowed.", null, Changed: false, Loosens: false);
        var same = new ChangeOutcome(GateSettings.Defaults, ChangeResult.Same, "Already set.", null, Changed: false, Loosens: false);

        Assert.Equal(new ReplyMessage("r1", true, "Waits 48 h 0 min.", pendingId), ChangeMessages.Reply("r1", waits, saveFailed: false));
        Assert.Equal(new ReplyMessage("r2", false, "Not allowed.", null), ChangeMessages.Reply("r2", refused, saveFailed: false));
        Assert.Equal(new ReplyMessage("r3", true, "Already set.", null), ChangeMessages.Reply("r3", same, saveFailed: true));
        Assert.Equal(new ReplyMessage("r1", true, "Waits 48 h 0 min. Saved in memory; will retry.", pendingId), ChangeMessages.Reply("r1", waits, saveFailed: true));
        Assert.Equal(new ReplyMessage("r4", false, "Busy, try again.", null), ChangeMessages.Failure("r4", ChangeReply.Busy));
    }
}

public sealed class ChangeFieldsAndRequestTests
{
    [Fact]
    public void LogNameIsTheFixedNameOrUnknown()
    {
        foreach (var name in new[]
        {
            "weeknightMinutes", "weekendNightMinutes", "weeklyCapMinutes", "cooldownHours", "weekendNights", "notCountedAdd",
            "notCountedRemove", "enabled", "allowUninstall", "weekendNightsAdd", "weekendNightsRemove",
        })
        {
            Assert.Equal(name, ChangeFields.LogName(name));
        }

        Assert.Equal("unknown", ChangeFields.LogName("pid=1 forged"));
        Assert.Equal("unknown", ChangeFields.LogName(null));
        Assert.Equal("unknown", ChangeFields.LogName("Enabled"));
    }

    [Fact]
    public void PendingFieldsAreTheNineTheEngineMakes()
    {
        foreach (var name in new[]
        {
            "weeknightMinutes", "weekendNightMinutes", "weeklyCapMinutes", "cooldownHours", "notCountedAdd", "enabled", "allowUninstall",
            "weekendNightsAdd", "weekendNightsRemove",
        })
        {
            Assert.True(ChangeFields.IsPendingField(name), name);
        }

        Assert.False(ChangeFields.IsPendingField("weekendNights"));
        Assert.False(ChangeFields.IsPendingField("notCountedRemove"));
        Assert.False(ChangeFields.IsPendingField("bogus"));
        Assert.False(ChangeFields.IsPendingField(null));
    }

    [Theory]
    [InlineData("Blender", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("a\\b", false)]
    [InlineData("a/b", false)]
    [InlineData("a:b", false)]
    [InlineData(null, false)]
    public void FolderNames(string? folder, bool valid)
    {
        Assert.Equal(valid, ChangeFields.IsValidFolder(folder));
    }

    [Fact]
    public void LimitsAreTheApprovedOnes()
    {
        Assert.Equal(1440, ChangeFields.MaxNightMinutes);
        Assert.Equal(10080, ChangeFields.MaxWeeklyCapMinutes);
        Assert.Equal(168, ChangeFields.MaxCooldownHours);
        Assert.Equal(50, ChangeFields.MaxPending);
        Assert.Equal(50, ChangeFields.MaxNotCounted);
        Assert.Equal(260, ChangeFields.MaxFolderLength);
        Assert.True(ChangeFields.IsValidFolder(new string('x', 260)));
        Assert.False(ChangeFields.IsValidFolder(new string('x', 261)));
    }

    [Fact]
    public void ARequestMessageBecomesAChangeRequest()
    {
        var number = ChangeMessages.ToRequest(new RequestMessage("r", "weeknightMinutes", 120, null, null, null));
        Assert.Equal(new ChangeRequest("weeknightMinutes", 120, null, null, null), number);

        var flag = ChangeMessages.ToRequest(new RequestMessage("r", "enabled", null, false, null, null));
        Assert.False(flag!.Flag);

        var folder = ChangeMessages.ToRequest(new RequestMessage("r", "notCountedAdd", null, null, "Viewfinder", null));
        Assert.Equal("Viewfinder", folder!.Folder);

        var days = ChangeMessages.ToRequest(new RequestMessage("r", "weekendNights", null, null, null, ["saturday", "Sunday"]));
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], days!.Days);
        Assert.Equal("weekendNights", days.Field);
        Assert.Null(days.Number);

        Assert.Null(ChangeMessages.ToRequest(new RequestMessage("r", "weekendNights", null, null, null, ["6"])));
    }
}

public sealed class PendingChangeTests
{
    private static PendingChange Entry(string field, int? number = null, bool? flag = null, string? folder = null, DayOfWeek[]? days = null) =>
        new(IdNumber(1), field, number, flag, folder, days, Now, 100);

    [Fact]
    public void AnEntryTheEngineMakesIsValid()
    {
        Assert.True(Entry(ChangeFields.WeeknightMinutes, number: 0).IsValid());
        Assert.True(Entry(ChangeFields.WeeknightMinutes, number: 1440).IsValid());
        Assert.True(Entry(ChangeFields.WeekendNightMinutes, number: 1440).IsValid());
        Assert.True(Entry(ChangeFields.WeeklyCapMinutes, number: 10080).IsValid());
        Assert.True(Entry(ChangeFields.CooldownHours, number: 168).IsValid());
        Assert.True(Entry(ChangeFields.Enabled, flag: false).IsValid());
        Assert.True(Entry(ChangeFields.AllowUninstall, flag: true).IsValid());
        Assert.True(Entry(ChangeFields.NotCountedAdd, folder: "Viewfinder").IsValid());
        Assert.True(Entry(ChangeFields.WeekendNightsAdd, days: [DayOfWeek.Sunday]).IsValid());
        Assert.True(Entry(ChangeFields.WeekendNightsRemove, days: [DayOfWeek.Friday, DayOfWeek.Saturday]).IsValid());
    }

    [Fact]
    public void ABadEntryIsNotValid()
    {
        Assert.False(Entry("bogus", number: 5).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNights, days: [DayOfWeek.Sunday]).IsValid());
        Assert.False(Entry(ChangeFields.NotCountedRemove, folder: "Blender").IsValid());
        Assert.False(Entry(ChangeFields.WeeknightMinutes, number: -1).IsValid());
        Assert.False(Entry(ChangeFields.WeeknightMinutes, number: 1441).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightMinutes, number: 1441).IsValid());
        Assert.False(Entry(ChangeFields.WeeklyCapMinutes, number: 10081).IsValid());
        Assert.False(Entry(ChangeFields.WeeklyCapMinutes, number: -1).IsValid());
        Assert.False(Entry(ChangeFields.CooldownHours, number: 169).IsValid());
        Assert.False(Entry(ChangeFields.CooldownHours, number: -1).IsValid());
        Assert.False(Entry(ChangeFields.WeeknightMinutes).IsValid());
        Assert.False(Entry(ChangeFields.WeeknightMinutes, flag: true).IsValid());
        Assert.False(Entry(ChangeFields.Enabled, number: 1).IsValid());
        Assert.False(Entry(ChangeFields.Enabled).IsValid());
        Assert.False(Entry(ChangeFields.NotCountedAdd, folder: "a\\b").IsValid());
        Assert.False(Entry(ChangeFields.NotCountedAdd, number: 5).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightsAdd, days: []).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightsAdd, days: [DayOfWeek.Sunday, DayOfWeek.Sunday]).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightsAdd, days: [(DayOfWeek)9]).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightsRemove, number: 5).IsValid());
        Assert.False(Entry(ChangeFields.WeekendNightsAdd).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, -1).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, double.NaN).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, double.PositiveInfinity).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, 604800.5).IsValid());
        Assert.True(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, 604800).IsValid());
        Assert.True(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, null, Now, 0).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, "x", null, Now, 0).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, 1, true, null, null, Now, 0).IsValid());
        Assert.False(new PendingChange(IdNumber(1), ChangeFields.Enabled, null, true, null, [DayOfWeek.Monday], Now, 0).IsValid());
    }
}

public sealed class ChangeLogTests
{
    [Fact]
    public void RequestAndCancelLines()
    {
        Assert.Equal("request weeknightMinutes pending pid=12 session=1", ChangeLog.Request("weeknightMinutes", "pending", 12, 1));
        Assert.Equal("request unknown refused pid=12 session=-1", ChangeLog.Request("forged\nline", "refused", 12, -1));
        Assert.Equal("cancel applied pid=7 session=3", ChangeLog.Cancel("applied", 7, 3));
        Assert.Equal("applied", ChangeLog.ResultName(ChangeResult.Applied));
        Assert.Equal("pending", ChangeLog.ResultName(ChangeResult.Pending));
        Assert.Equal("split", ChangeLog.ResultName(ChangeResult.Split));
        Assert.Equal("same", ChangeLog.ResultName(ChangeResult.Same));
        Assert.Equal("notinlist", ChangeLog.ResultName(ChangeResult.NotInList));
        Assert.Equal("refused", ChangeLog.ResultName(ChangeResult.Refused));
        Assert.Equal("busy", ChangeLog.Busy);
        Assert.Equal("request weeknightMinutes busy pid=1 session=2", ChangeLog.Request("weeknightMinutes", ChangeLog.Busy, 1, 2));
    }

    [Fact]
    public void AppliedLinesNeverCarryAFolderName()
    {
        Assert.Equal("applied weeknightMinutes 120", ChangeLog.Applied(NumberEntry(ChangeFields.WeeknightMinutes, 120)));
        Assert.Equal("applied enabled false", ChangeLog.Applied(FlagEntry(ChangeFields.Enabled, false)));
        Assert.Equal("applied allowUninstall true", ChangeLog.Applied(FlagEntry(ChangeFields.AllowUninstall, true)));
        Assert.Equal("applied weekendNightsAdd Friday,Sunday", ChangeLog.Applied(DaysEntry(ChangeFields.WeekendNightsAdd, 0, 1, DayOfWeek.Sunday, DayOfWeek.Friday)));
        Assert.Equal("applied notCountedAdd", ChangeLog.Applied(FolderEntry("Secret Game")));
        Assert.Equal("skipped notCountedAdd", ChangeLog.Skipped(FolderEntry("Secret Game")));
        Assert.Equal("skipped unknown", ChangeLog.Skipped(new PendingChange(IdNumber(1), "x\ny", null, null, null, null, Now, 0)));
        Assert.Equal("applied unknown", ChangeLog.Applied(new PendingChange(IdNumber(1), "x\ny", null, null, null, null, Now, 0)));
    }

    [Fact]
    public void ExtendLog()
    {
        Assert.Equal("extend applied pid=12 session=1", ChangeLog.Extend("applied", 12, 1));
        Assert.Equal("extend refused pid=12 session=1", ChangeLog.Extend("refused", 12, 1));
        Assert.Equal("extend busy pid=12 session=1", ChangeLog.Extend(ChangeLog.Busy, 12, 1));
    }
}
