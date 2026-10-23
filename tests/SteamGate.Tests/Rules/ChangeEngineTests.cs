using SteamGate.Core;
using SteamGate.Core.Rules;
using Xunit;
using static SteamGate.Tests.Rules.RuleFixtures;

namespace SteamGate.Tests.Rules;

public sealed class ChangeEngineTests
{
    private static readonly GateSettings Defaults = GateSettings.Defaults;

    [Fact]
    public void LowerLimitAppliesNow()
    {
        var outcome = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 60));

        Assert.Equal(60, outcome.Updated.WeeknightMinutes);
        Assert.Empty(outcome.Updated.Pending);
        Assert.Equal(ChangeReply.AppliedNow, outcome.Text);
        Assert.Equal("Applied now.", outcome.Text);
        Assert.Equal(ChangeResult.Applied, outcome.Result);
        Assert.True(outcome.Changed);
        Assert.False(outcome.Loosens);
        Assert.True(outcome.Ok);
        Assert.Null(outcome.PendingId);
    }

    [Fact]
    public void RaiseWaitsCooldown()
    {
        var outcome = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 120));

        Assert.Equal(90, outcome.Updated.WeeknightMinutes);
        var entry = Assert.Single(outcome.Updated.Pending);
        Assert.Equal(172800, entry.RemainingWaitSeconds);
        Assert.Equal(120, entry.Number);
        Assert.Equal(ChangeFields.WeeknightMinutes, entry.Field);
        Assert.Null(entry.Flag);
        Assert.Null(entry.Folder);
        Assert.Null(entry.Days);
        Assert.Equal(Now, entry.RequestedUtc);
        Assert.Equal(IdNumber(1), entry.Id);
        Assert.Equal(entry.Id, outcome.PendingId);
        Assert.Equal("Waits 48 h 0 min.", outcome.Text);
        Assert.Equal(ChangeResult.Pending, outcome.Result);
        Assert.True(outcome.Changed);
        Assert.True(outcome.Loosens);

        var almost = ChangeEngine.ApplyDue(ChangeEngine.Countdown(outcome.Updated, 172799));
        Assert.Equal(90, almost.Updated.WeeknightMinutes);
        Assert.Single(almost.Updated.Pending);
        Assert.Equal(1, Assert.Single(almost.Updated.Pending).RemainingWaitSeconds);
        Assert.Empty(almost.Applied);

        var due = ChangeEngine.ApplyDue(ChangeEngine.Countdown(almost.Updated, 1));
        Assert.Equal(120, due.Updated.WeeknightMinutes);
        Assert.Empty(due.Updated.Pending);
        Assert.Equal(entry.Id, Assert.Single(due.Applied).Id);
        Assert.Empty(due.Skipped);
    }

    [Theory]
    [InlineData(ChangeFields.WeeknightMinutes, 91, 1440)]
    [InlineData(ChangeFields.WeekendNightMinutes, 181, 1440)]
    [InlineData(ChangeFields.WeeklyCapMinutes, 601, 10080)]
    public void EachLimitWaitsToRaiseAndAppliesToLower(string field, int higher, int max)
    {
        var raise = Ask(Defaults, Number(field, higher));
        var top = Ask(Defaults, Number(field, max));
        var over = Ask(Defaults, Number(field, max + 1));
        var lower = Ask(Defaults, Number(field, 0));

        Assert.Equal(ChangeResult.Pending, raise.Result);
        Assert.Equal(higher, Assert.Single(raise.Updated.Pending).Number);
        Assert.Equal(Defaults.WeeknightMinutes, raise.Updated.WeeknightMinutes);
        Assert.Equal(Defaults.WeekendNightMinutes, raise.Updated.WeekendNightMinutes);
        Assert.Equal(Defaults.WeeklyCapMinutes, raise.Updated.WeeklyCapMinutes);
        Assert.Equal(ChangeResult.Pending, top.Result);
        Assert.Equal(ChangeResult.Refused, over.Result);
        Assert.Equal($"Out of range: 0 to {max}.", over.Text);
        Assert.False(over.Ok);
        Assert.Equal(ChangeResult.Applied, lower.Result);
        Assert.False(lower.Loosens);
        Assert.Equal(0, field switch
        {
            ChangeFields.WeeknightMinutes => lower.Updated.WeeknightMinutes,
            ChangeFields.WeekendNightMinutes => lower.Updated.WeekendNightMinutes,
            _ => lower.Updated.WeeklyCapMinutes,
        });
    }

    [Fact]
    public void NegativeNumbersAreOutOfRange()
    {
        var outcome = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, -1));

        Assert.Equal("Out of range: 0 to 1440.", outcome.Text);
        Assert.Equal(ChangeResult.Refused, outcome.Result);
        Assert.Same(Defaults, outcome.Updated);
        Assert.False(outcome.Changed);
        Assert.False(outcome.Loosens);
    }

    [Fact]
    public void CooldownRules()
    {
        var shorter = Ask(Defaults, Number(ChangeFields.CooldownHours, 24));
        var longer = Ask(Defaults, Number(ChangeFields.CooldownHours, 72));
        var tooLong = Ask(Defaults, Number(ChangeFields.CooldownHours, 169));
        var maximum = Ask(Defaults, Number(ChangeFields.CooldownHours, 168));
        var zero = Ask(Defaults, Number(ChangeFields.CooldownHours, 0));
        var instant = Ask(Defaults with { CooldownHours = 0 }, Number(ChangeFields.WeeknightMinutes, 120));

        Assert.Equal(48, shorter.Updated.CooldownHours);
        Assert.Equal(172800, Assert.Single(shorter.Updated.Pending).RemainingWaitSeconds);
        Assert.Equal(24, Assert.Single(shorter.Updated.Pending).Number);
        Assert.Equal("Waits 48 h 0 min.", shorter.Text);
        Assert.True(shorter.Loosens);

        Assert.Equal(72, longer.Updated.CooldownHours);
        Assert.Empty(longer.Updated.Pending);
        Assert.Equal(ChangeResult.Applied, longer.Result);
        Assert.False(longer.Loosens);

        Assert.Same(Defaults, tooLong.Updated);
        Assert.Equal("Out of range: 0 to 168.", tooLong.Text);
        Assert.Equal(ChangeResult.Refused, tooLong.Result);
        Assert.False(tooLong.Ok);

        Assert.Equal(168, maximum.Updated.CooldownHours);
        Assert.Equal(ChangeResult.Applied, maximum.Result);
        Assert.Equal(ChangeResult.Pending, zero.Result);

        Assert.Equal(120, instant.Updated.WeeknightMinutes);
        Assert.Empty(instant.Updated.Pending);
        Assert.Equal("Applied now.", instant.Text);
        Assert.Equal(ChangeResult.Applied, instant.Result);
        Assert.True(instant.Loosens);
        Assert.True(instant.Changed);
    }

    [Fact]
    public void WaitTextCountsHoursAtTheCooldownInForce()
    {
        var outcome = Ask(Defaults with { CooldownHours = 168 }, Number(ChangeFields.WeeknightMinutes, 120));
        var short1 = Ask(Defaults with { CooldownHours = 1 }, Number(ChangeFields.WeeknightMinutes, 120));

        Assert.Equal("Waits 168 h 0 min.", outcome.Text);
        Assert.Equal(168 * 3600, Assert.Single(outcome.Updated.Pending).RemainingWaitSeconds);
        Assert.Equal("Waits 1 h 0 min.", short1.Text);
    }

    [Fact]
    public void WeekendNightsSplit()
    {
        var settings = Defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] };

        var outcome = Ask(settings, Days(DayOfWeek.Saturday, DayOfWeek.Sunday));

        Assert.Equal([DayOfWeek.Saturday], outcome.Updated.WeekendNights);
        var entry = Assert.Single(outcome.Updated.Pending);
        Assert.Equal(ChangeFields.WeekendNightsAdd, entry.Field);
        Assert.Equal([DayOfWeek.Sunday], entry.Days);
        Assert.Equal(172800, entry.RemainingWaitSeconds);
        Assert.Equal("Applied now: Friday. Waits 48 h 0 min: Sunday.", outcome.Text);
        Assert.Equal(ChangeResult.Split, outcome.Result);
        Assert.True(outcome.Changed);
        Assert.True(outcome.Loosens);
        Assert.Equal(entry.Id, outcome.PendingId);

        var later = ChangeEngine.ApplyDue(ChangeEngine.Countdown(outcome.Updated, 172800)).Updated;
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], later.WeekendNights);
        Assert.Empty(later.Pending);
    }

    [Fact]
    public void WeekendChangesJudgedByEffect()
    {
        var friSat = new[] { DayOfWeek.Friday, DayOfWeek.Saturday };

        // Weekend limit below the weeknight limit: taking Friday out could give it more time, so it waits.
        var unsafeRemoval = Ask(Defaults with { WeeknightMinutes = 120, WeekendNightMinutes = 60, WeekendNights = friSat }, Days(DayOfWeek.Saturday));
        Assert.Equal(friSat, unsafeRemoval.Updated.WeekendNights);
        var removal = Assert.Single(unsafeRemoval.Updated.Pending);
        Assert.Equal(ChangeFields.WeekendNightsRemove, removal.Field);
        Assert.Equal([DayOfWeek.Friday], removal.Days);
        Assert.Equal(ChangeResult.Pending, unsafeRemoval.Result);
        Assert.Equal("Waits 48 h 0 min.", unsafeRemoval.Text);
        Assert.True(unsafeRemoval.Loosens);
        Assert.Equal(removal.Id, unsafeRemoval.PendingId);

        // Equal limits: a removal cannot raise anything.
        var equal = Ask(Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 90, WeekendNights = friSat }, Days(DayOfWeek.Saturday));
        Assert.Equal([DayOfWeek.Saturday], equal.Updated.WeekendNights);
        Assert.Empty(equal.Updated.Pending);
        Assert.Equal(ChangeResult.Applied, equal.Result);
        Assert.Equal("Applied now.", equal.Text);
        Assert.False(equal.Loosens);

        // An added night waits even when the weekend limit is lowest.
        var pendingAdd = Ask(
            (Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 90, WeekendNights = friSat }).With(NumberEntry(ChangeFields.WeekendNightMinutes, 180)),
            Days(DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Wednesday));
        Assert.Equal(friSat, pendingAdd.Updated.WeekendNights);
        Assert.Equal(2, pendingAdd.Updated.Pending.Count);
        Assert.Equal([DayOfWeek.Wednesday], pendingAdd.Updated.Pending[1].Days);
        Assert.Equal(ChangeFields.WeekendNightsAdd, pendingAdd.Updated.Pending[1].Field);

        // A pending weeknight raise makes a removal unsafe, whichever weekend value ends up in force.
        var mixed = Ask(
            (Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 180, WeekendNights = friSat })
                .With(NumberEntry(ChangeFields.WeekendNightMinutes, 150, id: 1), NumberEntry(ChangeFields.WeeknightMinutes, 200, id: 2)),
            Days(DayOfWeek.Saturday));
        Assert.Equal(friSat, mixed.Updated.WeekendNights);
        Assert.Equal(ChangeFields.WeekendNightsRemove, mixed.Updated.Pending[2].Field);

        // A pending weeknight raise that the weekend limits still cover leaves the removal safe.
        var covered = Ask(
            (Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 180, WeekendNights = friSat }).With(NumberEntry(ChangeFields.WeeknightMinutes, 180)),
            Days(DayOfWeek.Saturday));
        Assert.Equal([DayOfWeek.Saturday], covered.Updated.WeekendNights);

        // Only a pending weekend value that is too low breaks the rule; a higher one is covered.
        var lowerWeekend = Ask(
            (Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 90, WeekendNights = friSat }).With(NumberEntry(ChangeFields.WeekendNightMinutes, 30)),
            Days(DayOfWeek.Saturday));
        Assert.Equal(friSat, lowerWeekend.Updated.WeekendNights);

        // An added night waits even when its weekend limit is below the weeknight limit.
        var lowAdd = Ask(Defaults with { WeeknightMinutes = 90, WeekendNightMinutes = 60, WeekendNights = friSat }, Days(DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Wednesday));
        Assert.Equal(ChangeResult.Pending, lowAdd.Result);

        // Both kinds of day: two entries with different ids, and cancelling one leaves the other.
        var both = Ask(Defaults with { WeeknightMinutes = 120, WeekendNightMinutes = 60, WeekendNights = friSat }, Days(DayOfWeek.Saturday, DayOfWeek.Sunday));
        Assert.Equal(2, both.Updated.Pending.Count);
        var add = both.Updated.Pending.Single(entry => entry.Field == ChangeFields.WeekendNightsAdd);
        var remove = both.Updated.Pending.Single(entry => entry.Field == ChangeFields.WeekendNightsRemove);
        Assert.Equal([DayOfWeek.Sunday], add.Days);
        Assert.Equal([DayOfWeek.Friday], remove.Days);
        Assert.NotEqual(add.Id, remove.Id);
        Assert.Equal(add.Id, both.PendingId);
        Assert.Equal(ChangeResult.Pending, both.Result);
        Assert.Equal("Waits 48 h 0 min.", both.Text);
        var cancelled = ChangeEngine.Cancel(both.Updated, remove.Id);
        Assert.Equal(add.Id, Assert.Single(cancelled.Updated.Pending).Id);
        Assert.Equal(friSat, cancelled.Updated.WeekendNights);
    }

    [Fact]
    public void WeekendDuplicatesAndBadDaysAreNotAllowed()
    {
        var duplicate = Ask(Defaults, Days(DayOfWeek.Friday, DayOfWeek.Friday));
        var undefined = Ask(Defaults, Days((DayOfWeek)9));
        var withNumber = Ask(Defaults, new ChangeRequest(ChangeFields.WeekendNights, 5, null, null, [DayOfWeek.Friday]));
        var noDays = Ask(Defaults, new ChangeRequest(ChangeFields.WeekendNights, null, null, null, null));
        var withFlag = Ask(Defaults, new ChangeRequest(ChangeFields.WeekendNights, null, true, null, [DayOfWeek.Friday]));
        var withFolder = Ask(Defaults, new ChangeRequest(ChangeFields.WeekendNights, null, null, "x", [DayOfWeek.Friday]));

        foreach (var outcome in new[] { duplicate, undefined, withNumber, noDays, withFlag, withFolder })
        {
            Assert.Equal("Not allowed.", outcome.Text);
            Assert.Equal(ChangeResult.Refused, outcome.Result);
            Assert.False(outcome.Ok);
            Assert.False(outcome.Changed);
            Assert.Same(Defaults, outcome.Updated);
        }
    }

    [Fact]
    public void SameWeekendNightsInAnyOrderIsAlreadySet()
    {
        var outcome = Ask(Defaults, Days(DayOfWeek.Sunday, DayOfWeek.Friday, DayOfWeek.Saturday));

        Assert.Equal(ChangeResult.Same, outcome.Result);
        Assert.Equal("Already set.", outcome.Text);
        Assert.False(outcome.Changed);
        Assert.Same(Defaults, outcome.Updated);
    }

    [Fact]
    public void RemovingEveryWeekendNightAppliesNow()
    {
        var outcome = Ask(Defaults, Days());

        Assert.Empty(outcome.Updated.WeekendNights);
        Assert.Equal(ChangeResult.Applied, outcome.Result);
        Assert.False(outcome.Loosens);
    }

    [Fact]
    public void WeekendNightsKeepMondayFirstOrder()
    {
        var outcome = Ask(Defaults with { CooldownHours = 0, WeekendNights = [DayOfWeek.Sunday] }, Days(DayOfWeek.Sunday, DayOfWeek.Friday, DayOfWeek.Monday));

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday, DayOfWeek.Sunday], outcome.Updated.WeekendNights);
        Assert.Equal("Applied now.", outcome.Text);
        Assert.True(outcome.Loosens);
        Assert.Equal(ChangeResult.Applied, outcome.Result);
    }

    [Fact]
    public void WeekendNightsAtCooldownZeroApplyEverythingNow()
    {
        var unsafeRemoval = Ask(Defaults with { CooldownHours = 0, WeeknightMinutes = 120, WeekendNightMinutes = 60 }, Days(DayOfWeek.Saturday));
        var safeRemoval = Ask(Defaults with { CooldownHours = 0 }, Days(DayOfWeek.Saturday));

        Assert.Equal([DayOfWeek.Saturday], unsafeRemoval.Updated.WeekendNights);
        Assert.True(unsafeRemoval.Loosens);
        Assert.Empty(unsafeRemoval.Updated.Pending);
        Assert.Equal([DayOfWeek.Saturday], safeRemoval.Updated.WeekendNights);
        Assert.False(safeRemoval.Loosens);
    }

    [Fact]
    public void NotCountedRules()
    {
        var added = Ask(Defaults, Folder(ChangeFields.NotCountedAdd, "Viewfinder"));
        var entry = Assert.Single(added.Updated.Pending);
        Assert.Equal("Viewfinder", entry.Folder);
        Assert.Equal(ChangeFields.NotCountedAdd, entry.Field);
        Assert.Equal(Defaults.NotCounted, added.Updated.NotCounted);
        Assert.Equal(ChangeResult.Pending, added.Result);
        Assert.Equal("Waits 48 h 0 min.", added.Text);
        Assert.True(added.Loosens);

        var missing = Ask(added.Updated, Folder(ChangeFields.NotCountedRemove, "viewfinder"));
        Assert.Equal("Not in the list.", missing.Text);
        Assert.Equal(ChangeResult.NotInList, missing.Result);
        Assert.True(missing.Ok);
        Assert.False(missing.Changed);
        Assert.False(missing.Loosens);
        Assert.Single(missing.Updated.Pending);

        var removed = Ask(added.Updated, Folder(ChangeFields.NotCountedRemove, "blender"));
        Assert.DoesNotContain("Blender", removed.Updated.NotCounted);
        Assert.Equal(3, removed.Updated.NotCounted.Count);
        Assert.Equal("Applied now.", removed.Text);
        Assert.Equal(ChangeResult.Applied, removed.Result);
        Assert.False(removed.Loosens);
        Assert.True(removed.Changed);
        Assert.Single(removed.Updated.Pending);

        Assert.Equal("Not in the list.", Ask(Defaults, Folder(ChangeFields.NotCountedRemove, "Nope")).Text);

        var same = Ask(Defaults, Folder(ChangeFields.NotCountedAdd, "OBS Studio"));
        Assert.Equal("Already set.", same.Text);
        Assert.Equal(ChangeResult.Same, same.Result);
        Assert.False(same.Changed);
        Assert.Equal("Already set.", Ask(Defaults, Folder(ChangeFields.NotCountedAdd, "obs studio")).Text);

        var bad = Ask(Defaults, Folder(ChangeFields.NotCountedAdd, "a\\b"));
        Assert.Equal("Not allowed.", bad.Text);
        Assert.Equal(ChangeResult.Refused, bad.Result);

        var twice = Defaults.With(FolderEntry("Viewfinder", id: 1), FolderEntry("Viewfinder", id: 2));
        var after = ChangeEngine.ApplyDue(ChangeEngine.Countdown(twice, 172800)).Updated;
        Assert.Equal(1, after.NotCounted.Count(name => name == "Viewfinder"));
        Assert.Equal(5, after.NotCounted.Count);
        Assert.Empty(after.Pending);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    public void BadFolderNamesAreNotAllowedToAddOrRemove(string folder)
    {
        Assert.Equal("Not allowed.", Ask(Defaults, Folder(ChangeFields.NotCountedAdd, folder)).Text);
        Assert.Equal("Not allowed.", Ask(Defaults, Folder(ChangeFields.NotCountedRemove, folder)).Text);
    }

    [Fact]
    public void FolderNameLengthLimitIs260()
    {
        var longest = new string('x', 260);
        var tooLong = new string('x', 261);

        Assert.Equal(ChangeResult.Pending, Ask(Defaults, Folder(ChangeFields.NotCountedAdd, longest)).Result);
        Assert.Equal(ChangeResult.Refused, Ask(Defaults, Folder(ChangeFields.NotCountedAdd, tooLong)).Result);
        Assert.Equal(ChangeResult.Refused, Ask(Defaults, Folder(ChangeFields.NotCountedRemove, tooLong)).Result);
    }

    [Fact]
    public void NotCountedAddAtCooldownZeroApplies()
    {
        var outcome = Ask(Defaults with { CooldownHours = 0 }, Folder(ChangeFields.NotCountedAdd, "Viewfinder"));

        Assert.Equal([.. Defaults.NotCounted, "Viewfinder"], outcome.Updated.NotCounted);
        Assert.Empty(outcome.Updated.Pending);
        Assert.True(outcome.Loosens);
        Assert.Equal("Applied now.", outcome.Text);
    }

    [Fact]
    public void DisableAndUninstallWait()
    {
        var disable = Ask(Defaults, Flag(ChangeFields.Enabled, false));
        Assert.True(disable.Updated.Enabled);
        var disableEntry = Assert.Single(disable.Updated.Pending);
        Assert.Equal(false, disableEntry.Flag);
        Assert.Equal(ChangeResult.Pending, disable.Result);
        Assert.True(disable.Loosens);
        Assert.False(ChangeEngine.ApplyDue(ChangeEngine.Countdown(disable.Updated, 172800)).Updated.Enabled);

        var enable = Ask(Defaults with { Enabled = false }, Flag(ChangeFields.Enabled, true));
        Assert.True(enable.Updated.Enabled);
        Assert.Empty(enable.Updated.Pending);
        Assert.Equal(ChangeResult.Applied, enable.Result);
        Assert.False(enable.Loosens);

        var allow = Ask(Defaults, Flag(ChangeFields.AllowUninstall, true));
        Assert.False(allow.Updated.AllowUninstall);
        Assert.Equal(true, Assert.Single(allow.Updated.Pending).Flag);
        Assert.Equal(ChangeResult.Pending, allow.Result);
        Assert.True(allow.Loosens);
        Assert.True(ChangeEngine.ApplyDue(ChangeEngine.Countdown(allow.Updated, 172800)).Updated.AllowUninstall);
        Assert.False(ChangeEngine.ApplyDue(ChangeEngine.Countdown(allow.Updated, 172799)).Updated.AllowUninstall);

        var forbid = Ask(Defaults with { AllowUninstall = true }, Flag(ChangeFields.AllowUninstall, false));
        Assert.False(forbid.Updated.AllowUninstall);
        Assert.Equal(ChangeResult.Applied, forbid.Result);
        Assert.False(forbid.Loosens);

        Assert.Equal(ChangeResult.Same, Ask(Defaults, Flag(ChangeFields.Enabled, true)).Result);
        Assert.Equal(ChangeResult.Same, Ask(Defaults, Flag(ChangeFields.AllowUninstall, false)).Result);
    }

    [Fact]
    public void FlagsAtCooldownZeroApplyNow()
    {
        var outcome = Ask(Defaults with { CooldownHours = 0 }, Flag(ChangeFields.AllowUninstall, true));

        Assert.True(outcome.Updated.AllowUninstall);
        Assert.True(outcome.Loosens);
        Assert.Equal("Applied now.", outcome.Text);
    }

    [Fact]
    public void PendingEntriesStayUntilCancelled()
    {
        var withRaise = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 120)).Updated;

        var lowered = Ask(withRaise, Number(ChangeFields.WeeknightMinutes, 60));
        Assert.Equal(60, lowered.Updated.WeeknightMinutes);
        Assert.Equal(120, Assert.Single(lowered.Updated.Pending).Number);

        var again = Ask(lowered.Updated, Number(ChangeFields.WeeknightMinutes, 60));
        Assert.Equal("Already set.", again.Text);
        Assert.Equal(ChangeResult.Same, again.Result);
        Assert.False(again.Changed);
        Assert.Equal(120, Assert.Single(again.Updated.Pending).Number);

        var ids = Ids();
        var first = ChangeEngine.Request(Defaults, Number(ChangeFields.WeeknightMinutes, 120), Now, ids);
        var second = ChangeEngine.Request(first.Updated, Number(ChangeFields.WeeknightMinutes, 100), Now.AddMinutes(1), ids);
        Assert.Equal(2, second.Updated.Pending.Count);
        var due = ChangeEngine.ApplyDue(ChangeEngine.Countdown(second.Updated, 172800));
        Assert.Equal(100, due.Updated.WeeknightMinutes);
        Assert.Empty(due.Updated.Pending);
        Assert.Equal(2, due.Applied.Count);
    }

    [Fact]
    public void FullPendingNeverBlocksTightening()
    {
        var full = Defaults.With(Filler(50));

        var lower = Ask(full, Number(ChangeFields.WeeknightMinutes, 60));
        Assert.Equal("Applied now.", lower.Text);
        Assert.Equal(60, lower.Updated.WeeknightMinutes);
        Assert.Equal(50, lower.Updated.Pending.Count);

        var raise = Ask(full, Number(ChangeFields.WeeknightMinutes, 120));
        Assert.Equal("Not allowed.", raise.Text);
        Assert.Equal(ChangeResult.Refused, raise.Result);
        Assert.Same(full, raise.Updated);
        Assert.False(raise.Changed);

        Assert.Equal("Not allowed.", Ask(full, Flag(ChangeFields.Enabled, false)).Text);
        Assert.Equal("Not allowed.", Ask(full, Folder(ChangeFields.NotCountedAdd, "Viewfinder")).Text);
        Assert.Equal("Applied now.", Ask(full, Folder(ChangeFields.NotCountedRemove, "Blender")).Text);
        Assert.Equal("Already set.", Ask(full, Number(ChangeFields.WeeknightMinutes, 90)).Text);

        var almostFull = Defaults with { WeeknightMinutes = 120, WeekendNightMinutes = 60, WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] };
        var needsTwo = Ask(almostFull.With(Filler(49)), Days(DayOfWeek.Saturday, DayOfWeek.Sunday));
        Assert.Equal("Not allowed.", needsTwo.Text);
        Assert.Equal(49, needsTwo.Updated.Pending.Count);
        Assert.Equal(ChangeResult.Refused, needsTwo.Result);
        Assert.Equal(2, needsTwo.Updated.WeekendNights.Count);

        var partly = Ask(
            (Defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] }).With(Filler(50)),
            Days(DayOfWeek.Saturday, DayOfWeek.Sunday));
        Assert.Equal([DayOfWeek.Saturday], partly.Updated.WeekendNights);
        Assert.Equal(50, partly.Updated.Pending.Count);
        Assert.Equal("Applied now: Friday. Not allowed: Sunday.", partly.Text);
        Assert.Equal(ChangeResult.Applied, partly.Result);
        Assert.True(partly.Changed);
        Assert.False(partly.Loosens);
        Assert.True(partly.Ok);
        Assert.Null(partly.PendingId);

        var oneSlot = Ask(
            (Defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] }).With(Filler(49)),
            Days(DayOfWeek.Saturday, DayOfWeek.Sunday));
        Assert.Equal(ChangeResult.Split, oneSlot.Result);
        Assert.Equal(50, oneSlot.Updated.Pending.Count);
    }

    [Fact]
    public void NotCountedListFull()
    {
        var almost = Defaults with { NotCounted = Folders(49) };
        var withPending = almost.With(FolderEntry("Viewfinder"));

        var refused = Ask(withPending, Folder(ChangeFields.NotCountedAdd, "Another"));
        Assert.Equal("Not allowed.", refused.Text);
        Assert.Equal(ChangeResult.Refused, refused.Result);

        var allowed = Ask(almost, Folder(ChangeFields.NotCountedAdd, "Another"));
        Assert.Equal(ChangeResult.Pending, allowed.Result);

        var overflow = (Defaults with { NotCounted = Folders(50) }).With(FolderEntry("Viewfinder"));
        var due = ChangeEngine.ApplyDue(ChangeEngine.Countdown(overflow, 172800));
        Assert.Equal(FolderEntry("Viewfinder").Id, Assert.Single(due.Skipped).Id);
        Assert.Empty(due.Applied);
        Assert.Empty(due.Updated.Pending);
        Assert.Equal(50, due.Updated.NotCounted.Count);

        var instant = Ask(Defaults with { CooldownHours = 0, NotCounted = Folders(50) }, Folder(ChangeFields.NotCountedAdd, "Another"));
        Assert.Equal("Not allowed.", instant.Text);
        Assert.Equal(50, instant.Updated.NotCounted.Count);

        var lastSlot = Ask(Defaults with { CooldownHours = 0, NotCounted = Folders(49) }, Folder(ChangeFields.NotCountedAdd, "Another"));
        Assert.Equal(50, lastSlot.Updated.NotCounted.Count);
        Assert.Equal("Applied now.", lastSlot.Text);
    }

    [Fact]
    public void ADueAddOfAFolderAlreadyListedAppliesAsNoOp()
    {
        var settings = Defaults.With(FolderEntry("blender"));

        var due = ChangeEngine.ApplyDue(ChangeEngine.Countdown(settings, 172800));

        Assert.Equal(Defaults.NotCounted, due.Updated.NotCounted);
        Assert.Single(due.Applied);
        Assert.Empty(due.Skipped);
    }

    [Fact]
    public void CancelAppliesNow()
    {
        var withRaise = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 120)).Updated;
        var other = ChangeEngine.Request(withRaise, Flag(ChangeFields.AllowUninstall, true), Now, () => IdNumber(2)).Updated;
        var id = withRaise.Pending[0].Id;

        var cancelled = ChangeEngine.Cancel(other, id);
        Assert.Equal("Applied now.", cancelled.Text);
        Assert.Equal(ChangeResult.Applied, cancelled.Result);
        Assert.True(cancelled.Changed);
        Assert.False(cancelled.Loosens);
        Assert.True(cancelled.Ok);
        Assert.Equal(ChangeFields.AllowUninstall, Assert.Single(cancelled.Updated.Pending).Field);
        Assert.Equal(other.WeeknightMinutes, cancelled.Updated.WeeknightMinutes);
        Assert.Equal(other.NotCounted, cancelled.Updated.NotCounted);

        var unknown = ChangeEngine.Cancel(other, IdNumber(999));
        Assert.Same(other, unknown.Updated);
        Assert.Equal(ChangeResult.Refused, unknown.Result);
        Assert.False(unknown.Changed);
        Assert.False(unknown.Ok);
        Assert.Equal("No pending change with that id.", unknown.Text);
    }

    [Fact]
    public void LengtheningDoesNotMoveExistingEntries()
    {
        var withRaise = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 120)).Updated;

        var longer = Ask(withRaise, Number(ChangeFields.CooldownHours, 72));

        Assert.Equal(72, longer.Updated.CooldownHours);
        Assert.Equal(172800, Assert.Single(longer.Updated.Pending).RemainingWaitSeconds);
    }

    [Fact]
    public void ShorteningWaitsTheCurrentCooldownAndThenAppliesTheNewOne()
    {
        var shorter = Ask(Defaults, Number(ChangeFields.CooldownHours, 24)).Updated;
        var due = ChangeEngine.ApplyDue(ChangeEngine.Countdown(shorter, 172800)).Updated;

        Assert.Equal(24, due.CooldownHours);
        var raise = Ask(due, Number(ChangeFields.WeeknightMinutes, 120));
        Assert.Equal(86400, Assert.Single(raise.Updated.Pending).RemainingWaitSeconds);
        Assert.Equal("Waits 24 h 0 min.", raise.Text);
    }

    [Fact]
    public void SameValueAndRange()
    {
        var same = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 90));
        var over = Ask(Defaults, Number(ChangeFields.WeeknightMinutes, 1441));

        Assert.Equal("Already set.", same.Text);
        Assert.Equal(ChangeResult.Same, same.Result);
        Assert.False(same.Changed);
        Assert.False(same.Loosens);
        Assert.Same(Defaults, same.Updated);
        Assert.Equal("Out of range: 0 to 1440.", over.Text);
        Assert.Same(Defaults, over.Updated);
        Assert.False(over.Changed);
    }

    [Fact]
    public void WrongKindOfValueIsNotAllowed()
    {
        var requests = new[]
        {
            new ChangeRequest(ChangeFields.WeeknightMinutes, null, null, null, null),
            new ChangeRequest(ChangeFields.WeeknightMinutes, 100, true, null, null),
            new ChangeRequest(ChangeFields.WeeknightMinutes, 100, null, "x", null),
            new ChangeRequest(ChangeFields.WeeknightMinutes, 100, null, null, [DayOfWeek.Monday]),
            new ChangeRequest(ChangeFields.WeeknightMinutes, null, true, null, null),
            new ChangeRequest(ChangeFields.Enabled, null, null, null, null),
            new ChangeRequest(ChangeFields.Enabled, 1, false, null, null),
            new ChangeRequest(ChangeFields.Enabled, null, false, "x", null),
            new ChangeRequest(ChangeFields.Enabled, null, false, null, []),
            new ChangeRequest(ChangeFields.AllowUninstall, 1, null, null, null),
            new ChangeRequest(ChangeFields.NotCountedAdd, null, null, null, null),
            new ChangeRequest(ChangeFields.NotCountedAdd, 1, null, "x", null),
            new ChangeRequest(ChangeFields.NotCountedAdd, null, true, "x", null),
            new ChangeRequest(ChangeFields.NotCountedAdd, null, null, "x", []),
            new ChangeRequest(ChangeFields.NotCountedRemove, null, null, null, null),
            new ChangeRequest(ChangeFields.NotCountedRemove, 1, null, "Blender", null),
            new ChangeRequest(ChangeFields.NotCountedRemove, null, true, "Blender", null),
            new ChangeRequest(ChangeFields.NotCountedRemove, null, null, "Blender", []),
            new ChangeRequest("bogus", 5, null, null, null),
            new ChangeRequest(ChangeFields.WeekendNightsAdd, null, null, null, [DayOfWeek.Monday]),
            new ChangeRequest(ChangeFields.WeekendNightsRemove, null, null, null, [DayOfWeek.Monday]),
            new ChangeRequest(string.Empty, null, null, null, null),
        };

        foreach (var request in requests)
        {
            var outcome = Ask(Defaults, request);
            Assert.Equal("Not allowed.", outcome.Text);
            Assert.Equal(ChangeResult.Refused, outcome.Result);
            Assert.Same(Defaults, outcome.Updated);
            Assert.False(outcome.Changed);
            Assert.False(outcome.Loosens);
        }
    }

    [Fact]
    public void TwoEntriesFromOneRequestGetDifferentIdsFromTheCaller()
    {
        var ids = Ids();

        var outcome = ChangeEngine.Request(
            Defaults with { WeeknightMinutes = 120, WeekendNightMinutes = 60, WeekendNights = [DayOfWeek.Friday] },
            Days(DayOfWeek.Saturday),
            Now,
            ids);

        Assert.Equal([IdNumber(1), IdNumber(2)], outcome.Updated.Pending.Select(entry => entry.Id));
        Assert.Equal(IdNumber(1), outcome.PendingId);
        Assert.Equal(IdNumber(3), ids());
    }

    [Fact]
    public void ARequestThatChangesNothingNeverAsksForAnId()
    {
        var asked = 0;
        Guid Ask1()
        {
            asked++;
            return Guid.NewGuid();
        }

        ChangeEngine.Request(Defaults, Number(ChangeFields.WeeknightMinutes, 60), Now, Ask1);
        ChangeEngine.Request(Defaults, Number(ChangeFields.WeeknightMinutes, 90), Now, Ask1);
        ChangeEngine.Request(Defaults, Number(ChangeFields.WeeknightMinutes, 5000), Now, Ask1);
        Assert.Equal(0, asked);

        ChangeEngine.Request(Defaults, Number(ChangeFields.WeeknightMinutes, 100), Now, Ask1);
        Assert.Equal(1, asked);
    }

    [Fact]
    public void StepSeconds()
    {
        var utc = new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);

        // While running, only the uptime change counts, whatever the clock did.
        Assert.Equal(5, ChangeEngine.StepSeconds(false, true, utc, 10_000, 15_000, utc.AddSeconds(5), 0));
        Assert.Equal(5, ChangeEngine.StepSeconds(false, true, utc, 10_000, 15_000, utc.AddDays(3), 0));
        Assert.Equal(5, ChangeEngine.StepSeconds(false, false, utc, 10_000, 15_000, utc.AddHours(-1), 0));
        Assert.Equal(5, ChangeEngine.StepSeconds(false, false, utc, 10_000, 15_000, null, null));
        Assert.Equal(0, ChangeEngine.StepSeconds(false, true, utc, 15_000, 10_000, utc, 0));
        Assert.Equal(0.5, ChangeEngine.StepSeconds(false, true, utc, 10_000, 10_500, utc, 0));

        // First tick on the same boot: uptime since the last save, never the clock.
        Assert.Equal(3600, ChangeEngine.StepSeconds(true, true, utc, 0, 5_000_000, utc.AddDays(-3), 5_000_000 - 3_600_000));
        Assert.Equal(0, ChangeEngine.StepSeconds(true, true, utc, 0, 5_000_000, utc.AddDays(-3), 6_000_000));
        Assert.Equal(0, ChangeEngine.StepSeconds(true, true, utc, 0, 5_000_000, utc.AddDays(-3), null));

        // First tick after Windows restarted: the clock.
        Assert.Equal(3600, ChangeEngine.StepSeconds(true, false, utc, 0, 5_000, utc.AddHours(-1), 4_000_000));
        Assert.Equal(0, ChangeEngine.StepSeconds(true, false, utc, 0, 5_000, utc.AddHours(1), 0));
        Assert.Equal(0, ChangeEngine.StepSeconds(true, false, utc, 0, 5_000, null, 0));
        Assert.Equal(3600, ChangeEngine.StepSeconds(true, false, utc, 0, 5_000, utc.AddHours(-1), null));
    }

    [Fact]
    public void CountdownSubtractsFromEveryEntryAndIgnoresNonsense()
    {
        var settings = Defaults.With(NumberEntry(ChangeFields.WeeknightMinutes, 120, remaining: 1000, id: 1), FlagEntry(ChangeFields.Enabled, false, remaining: 50, id: 2));

        var counted = ChangeEngine.Countdown(settings, 30.5);

        Assert.Equal([969.5, 19.5], counted.Pending.Select(entry => entry.RemainingWaitSeconds));
        Assert.Same(settings, ChangeEngine.Countdown(settings, 0));
        Assert.Same(settings, ChangeEngine.Countdown(settings, -5));
        Assert.Same(settings, ChangeEngine.Countdown(settings, double.NaN));
        Assert.Same(Defaults, ChangeEngine.Countdown(Defaults, 100));
        Assert.Equal(-100, ChangeEngine.Countdown(settings, 1100).Pending[0].RemainingWaitSeconds);
    }

    [Fact]
    public void ApplyDueAppliesOldestRequestFirstSoTheLaterWins()
    {
        var older = NumberEntry(ChangeFields.WeeknightMinutes, 100, remaining: 0, id: 9, requested: Now);
        var newer = NumberEntry(ChangeFields.WeeknightMinutes, 120, remaining: 0, id: 1, requested: Now.AddMinutes(1));
        var sameTimeLowId = NumberEntry(ChangeFields.WeeklyCapMinutes, 700, remaining: 0, id: 1, requested: Now);
        var sameTimeHighId = NumberEntry(ChangeFields.WeeklyCapMinutes, 800, remaining: 0, id: 2, requested: Now);

        var result = ChangeEngine.ApplyDue(Defaults.With(newer, sameTimeHighId, older, sameTimeLowId));

        Assert.Equal(120, result.Updated.WeeknightMinutes);
        Assert.Equal(800, result.Updated.WeeklyCapMinutes);
        Assert.Equal([700, 800, 100, 120], result.Applied.Select(entry => entry.Number!.Value));
        Assert.Equal(4, result.Applied.Count);
        Assert.Empty(result.Updated.Pending);
    }

    [Fact]
    public void ApplyDueKeepsEntriesThatAreNotDue()
    {
        var due = NumberEntry(ChangeFields.WeeknightMinutes, 120, remaining: 0, id: 1);
        var waiting = FlagEntry(ChangeFields.Enabled, false, remaining: 0.5, id: 2);

        var result = ChangeEngine.ApplyDue(Defaults.With(due, waiting));

        Assert.Equal(120, result.Updated.WeeknightMinutes);
        Assert.True(result.Updated.Enabled);
        Assert.Equal(waiting, Assert.Single(result.Updated.Pending));
        Assert.Equal(due, Assert.Single(result.Applied));
    }

    [Fact]
    public void ApplyDueWithNothingDueReturnsTheSameSettings()
    {
        var settings = Defaults.With(NumberEntry(ChangeFields.WeeknightMinutes, 120, remaining: 5));

        var result = ChangeEngine.ApplyDue(settings);

        Assert.Same(settings, result.Updated);
        Assert.Empty(result.Applied);
        Assert.Empty(result.Skipped);
        Assert.Same(Defaults, ChangeEngine.ApplyDue(Defaults).Updated);
    }

    [Fact]
    public void ApplyDueAppliesEveryKindOfEntry()
    {
        var settings = (Defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday] }).With(
            NumberEntry(ChangeFields.WeeknightMinutes, 100, remaining: 0, id: 1),
            NumberEntry(ChangeFields.WeekendNightMinutes, 200, remaining: 0, id: 2),
            NumberEntry(ChangeFields.WeeklyCapMinutes, 700, remaining: 0, id: 3),
            NumberEntry(ChangeFields.CooldownHours, 24, remaining: 0, id: 4),
            FlagEntry(ChangeFields.Enabled, false, remaining: 0, id: 5),
            FlagEntry(ChangeFields.AllowUninstall, true, remaining: 0, id: 6),
            FolderEntry("Viewfinder", remaining: 0, id: 7),
            DaysEntry(ChangeFields.WeekendNightsAdd, 0, 8, DayOfWeek.Sunday, DayOfWeek.Monday),
            DaysEntry(ChangeFields.WeekendNightsRemove, 0, 9, DayOfWeek.Friday));

        var result = ChangeEngine.ApplyDue(settings).Updated;

        Assert.Equal(100, result.WeeknightMinutes);
        Assert.Equal(200, result.WeekendNightMinutes);
        Assert.Equal(700, result.WeeklyCapMinutes);
        Assert.Equal(24, result.CooldownHours);
        Assert.False(result.Enabled);
        Assert.True(result.AllowUninstall);
        Assert.Equal("Viewfinder", result.NotCounted[^1]);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Saturday, DayOfWeek.Sunday], result.WeekendNights);
        Assert.Empty(result.Pending);
    }

    [Fact]
    public void ApplyDueNeverThrowsOnAnEntryItCannotApply()
    {
        var bad = new[]
        {
            new PendingChange(IdNumber(1), "bogus", 5, null, null, null, Now, 0),
            new PendingChange(IdNumber(2), ChangeFields.WeeknightMinutes, null, null, null, null, Now, 0),
            new PendingChange(IdNumber(3), ChangeFields.Enabled, null, null, null, null, Now, 0),
            new PendingChange(IdNumber(4), ChangeFields.NotCountedAdd, null, null, null, null, Now, 0),
            new PendingChange(IdNumber(5), ChangeFields.WeekendNightsAdd, null, null, null, null, Now, 0),
            new PendingChange(IdNumber(6), ChangeFields.WeekendNightsRemove, null, null, null, null, Now, 0),
            new PendingChange(IdNumber(7), ChangeFields.NotCountedRemove, null, null, "Blender", null, Now, 0),
        };

        var result = ChangeEngine.ApplyDue(Defaults.With(bad));

        Assert.Equal(7, result.Skipped.Count);
        Assert.Empty(result.Applied);
        Assert.Empty(result.Updated.Pending);
        Assert.Equal(Defaults.WeeknightMinutes, result.Updated.WeeknightMinutes);
        Assert.Equal(Defaults.NotCounted, result.Updated.NotCounted);
    }
}
