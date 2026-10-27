using SteamGate.Core;
using SteamGate.Core.Extend;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Tests.Rules;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusViewTests
{
    private static readonly DateOnly Tonight = new(2026, 10, 20);

    private static EditorState OpenWeeknight() => new EditorState(
        OpenField: ChangeFields.WeeknightMinutes,
        Number: 120,
        Days: [DayOfWeek.Friday, DayOfWeek.Saturday],
        AddFolder: "Viewfinder",
        Replies: new Dictionary<ReplySection, string>())
        .WithReply(ReplySection.Rules, "Applied now.")
        .WithReply(ReplySection.Waiting, "Already set.")
        .WithReply(ReplySection.NotCounted, "Not in the list.")
        .WithReply(ReplySection.TurnOff, "Busy, try again.")
        .WithReply(ReplySection.Head, "Not allowed.");

    [Fact]
    public void StateKeepsEdits()
    {
        var editor = OpenWeeknight();
        var next = StateFixtures.State(GateSettings.Defaults with { WeeknightMinutes = 60 }, addable: [StateFixtures.Named("Viewfinder")]);

        var merged = StatusView.Merge(editor, next, Tonight);

        Assert.Equal(editor, merged);
        var verdict = VerdictLine.For(StatusState.ToSettings(next), RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120), StateFixtures.Now, TimeZoneInfo.Utc);
        Assert.Equal("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", verdict.Text);

        var replaced = StatusView.Merge(editor, StateFixtures.State(addable: [StateFixtures.Named("Overwatch"), StateFixtures.Named("Viewfinder 2")]), Tonight);
        Assert.Equal(editor with { AddFolder = "Overwatch" }, replaced);

        var emptied = StatusView.Merge(editor, StateFixtures.State(addable: []), Tonight);
        Assert.Null(emptied.AddFolder);
        Assert.Equal(editor with { AddFolder = null }, emptied);

        var firstChoice = StatusView.Merge(EditorState.Empty, StateFixtures.State(addable: [StateFixtures.Named("Arc"), StateFixtures.Named("Zed")]), Tonight);
        Assert.Equal("Arc", firstChoice.AddFolder);
    }

    [Fact]
    public void RowsRebuiltOnlyWhenKeysChange()
    {
        Assert.False(StatusView.RowsChanged(["a", "b"], ["a", "b"]));
        Assert.True(StatusView.RowsChanged(["a", "b"], ["b", "a"]));
        Assert.True(StatusView.RowsChanged(["a", "b"], ["a"]));
        Assert.True(StatusView.RowsChanged(["a"], ["a", "b"]));
        Assert.False(StatusView.RowsChanged([], []));
    }

    [Fact]
    public void KeysNameTheRows()
    {
        var entry = RuleFixtures.NumberEntry(ChangeFields.WeeknightMinutes, 120);
        var state = StateFixtures.State(StateFixtures.WithPending(entry), addable: [StateFixtures.Named("Viewfinder"), StateFixtures.Named("Zed")]);

        Assert.Equal([entry.Id.ToString()], StatusView.PendingKeys(state));
        Assert.Equal(["Blender", "OBS Studio", "wallpaper_engine", "Steamworks Shared"], StatusView.NotCountedKeys(state));
        Assert.Equal(["Viewfinder", "Zed"], StatusView.AddableKeys(state));
    }

    [Fact]
    public void AddableNamesFollowTheStateSoARenamedProgramIsSeen()
    {
        var before = StateFixtures.State(addable: [new NamedFolder("Viewfinder", "Viewfinder"), new NamedFolder("Zed", "Zed")]);
        var renamed = StateFixtures.State(addable: [new NamedFolder("Viewfinder", "Viewfinder Deluxe"), new NamedFolder("Zed", "Zed")]);

        Assert.Equal(["Viewfinder", "Zed"], StatusView.AddableNames(before));
        Assert.False(StatusView.RowsChanged(StatusView.AddableKeys(before), StatusView.AddableKeys(renamed)));
        Assert.True(StatusView.RowsChanged(StatusView.AddableNames(before), StatusView.AddableNames(renamed)));
        Assert.False(StatusView.RowsChanged(StatusView.AddableNames(before), StatusView.AddableNames(before)));
    }

    [Fact]
    public void OffAndRemovalLines()
    {
        var both = StateFixtures.State(GateSettings.Defaults with { Enabled = false, AllowUninstall = true });
        var neither = StateFixtures.State();
        var onlyOff = StateFixtures.State(GateSettings.Defaults with { Enabled = false });
        var onlyRemoval = StateFixtures.State(GateSettings.Defaults with { AllowUninstall = true });

        Assert.Equal("SteamGate is off.", StatusText.SteamGateOff);
        Assert.Equal("Request turn on", StatusText.RequestTurnOn);
        Assert.Equal("Removal is allowed.", StatusText.RemovalAllowed);
        Assert.Equal("Stop allowing removal", StatusText.StopAllowingRemoval);
        Assert.Equal(["SteamGate is off.", "Removal is allowed."], StatusView.HeadLines(both));
        Assert.Empty(StatusView.HeadLines(neither));
        Assert.Equal(["SteamGate is off."], StatusView.HeadLines(onlyOff));
        Assert.Equal(["Removal is allowed."], StatusView.HeadLines(onlyRemoval));

        var offAndAllowed = GateSettings.Defaults with { Enabled = false, AllowUninstall = true };
        foreach (var message in new[] { StatusRequests.Flag("s1", ChangeFields.Enabled, true), StatusRequests.Flag("s2", ChangeFields.AllowUninstall, false) })
        {
            var outcome = ChangeEngine.Request(offAndAllowed, ChangeMessages.ToRequest(message)!, StateFixtures.Now, () => Guid.Empty);
            Assert.Equal(ChangeResult.Applied, outcome.Result);
            Assert.False(outcome.Loosens);
        }
    }

    [Fact]
    public void RuleRowsAreInThePreviewOrderWithTheirValues()
    {
        var state = StateFixtures.State(GateSettings.Defaults with { CooldownHours = 24, WeeknightMinutes = 75 });

        Assert.Equal(
            ["Weeknight limit", "Weekend night limit", "Weekend nights", "Weekly cap", "Cooldown"],
            StatusView.RuleRows.Select(row => row.Name));
        Assert.Equal(
            ["1 h 15 min", "3 h", "Fri, Sat, Sun", "10 h", "24 h"],
            StatusView.RuleRows.Select(row => StatusView.RuleValue(row, state)));
        Assert.Equal([75, 180, 0, 600, 24], StatusView.RuleRows.Select(row => StatusView.RuleNumber(row, state)));
        Assert.Equal([1440, 1440, 0, 10080, 9999], StatusView.RuleRows.Select(row => row.BoxMax));
        Assert.Equal(["min", "min", null, "min", "h (most 168 h, which is 7 days)"], StatusView.RuleRows.Select(row => row.UnitNote));
        Assert.Equal(
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
            StatusView.CheckBoxDays);
    }

    [Fact]
    public void TogglingARowOpensItOnTheCurrentValueAndClosesIt()
    {
        var state = StateFixtures.State();
        var weeknight = StatusView.RuleRows[0];
        var nights = StatusView.RuleRows[2];

        var opened = StatusView.Toggle(EditorState.Empty.WithReply(ReplySection.Rules, "Applied now."), weeknight, state);
        Assert.Equal(("weeknightMinutes", 90, "Applied now."), (opened.OpenField, opened.Number, opened.ReplyOf(ReplySection.Rules)));

        var other = StatusView.Toggle(opened, nights, state);
        Assert.Equal("weekendNights", other.OpenField);
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday], other.Days);

        var closed = StatusView.Toggle(other, nights, state);
        Assert.Null(closed.OpenField);
    }

    [Fact]
    public void RepliesBelongToTheirSection()
    {
        var editor = EditorState.Empty;
        foreach (var section in Enum.GetValues<ReplySection>())
        {
            Assert.Null(editor.ReplyOf(section));
            editor = editor.WithReply(section, "reply " + section);
        }

        foreach (var section in Enum.GetValues<ReplySection>())
        {
            Assert.Equal("reply " + section, editor.ReplyOf(section));
        }

        var cleared = editor.WithReply(ReplySection.Waiting, null);
        Assert.Null(cleared.ReplyOf(ReplySection.Waiting));
        Assert.Equal("reply Rules", cleared.ReplyOf(ReplySection.Rules));
        Assert.Equal("reply Head", cleared.ReplyOf(ReplySection.Head));
        Assert.NotEqual(editor, cleared);
        Assert.NotEqual(editor.GetHashCode(), cleared.GetHashCode());
        Assert.NotEqual(editor, editor.WithReply(ReplySection.Head, "other"));
        Assert.Equal(editor, editor.WithReply(ReplySection.Head, "reply Head"));
        Assert.Equal(editor.GetHashCode(), editor.WithReply(ReplySection.Head, "reply Head").GetHashCode());

        // WithReply leaves the state it was called on as it was.
        var before = EditorState.Empty;
        before.WithReply(ReplySection.Rules, "x");
        Assert.Null(before.ReplyOf(ReplySection.Rules));
    }

    [Fact]
    public void UnreachableAfterTenSeconds()
    {
        Assert.False(StatusView.Unreachable(TimeSpan.FromSeconds(9.9)));
        Assert.True(StatusView.Unreachable(TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(10), StatusView.UnreachableAfter);
        Assert.Equal(TimeSpan.FromSeconds(5), StatusView.ReconnectDelay);
    }

    private const string AppliedText = "Applied now. Tonight ends 20 min later (limit 1 h 50 min).";
    private const string UsedLine = "Extension used tonight: 20 min.";
    private const string DoneLine = "Extension used tonight: 20 min. Only the minutes you actually play past the old limit are counted double.";
    private const string OpenLine = "Once per night, in the last 30 minutes before tonight's limit.";
    private const string WeekLine = "Not enough time left this week.";

    private static StateMessage Evening(double week = 22200, double night = 4680, int? limit = 90, int extension = 0) =>
        StateFixtures.State(nightSeconds: night, weekSeconds: week, tonightLimit: limit, extensionMinutes: extension);

    private static string Picked(ExtendSectionView view) => string.Join(",", view.Buttons.Where(button => button.Picked).Select(button => button.Minutes));

    private static string Enabled(ExtendSectionView view) => string.Join(",", view.Buttons.Where(button => button.Enabled).Select(button => button.Minutes));

    [Fact]
    public void ExtendSection()
    {
        var card1 = StatusView.ExtendSection(Evening(), EditorState.Empty);
        Assert.Equal(ExtendLayout.Choose, card1.Layout);
        Assert.Equal([10, 20, 30], card1.Buttons.Select(button => button.Minutes));
        Assert.Equal("10,20,30", Enabled(card1));
        Assert.Equal("20", Picked(card1));
        Assert.Equal("Adds 20 min to tonight (limit becomes 1 h 50 min). Up to 40 min comes off this week, because time past tonight's limit counts double.", card1.Explanation);
        Assert.NotNull(card1.Explanation);
        Assert.Equal(OpenLine, card1.Note);
        Assert.Null(card1.Reply);

        var ten = StatusView.ExtendSection(Evening(), StatusView.ExtendPick(EditorState.Empty, 10));
        Assert.Equal("10", Picked(ten));
        Assert.Equal("Adds 10 min to tonight (limit becomes 1 h 40 min). Up to 20 min comes off this week, because time past tonight's limit counts double.", ten.Explanation);

        var someWeek = Evening(week: 32400);
        var none = StatusView.ExtendSection(someWeek, EditorState.Empty);
        Assert.Equal("10,20", Enabled(none));
        Assert.Equal(string.Empty, Picked(none));
        Assert.Null(none.Explanation);
        Assert.Equal(WeekLine, none.Note);
        Assert.Equal(string.Empty, Picked(StatusView.ExtendSection(someWeek, StatusView.ExtendPick(EditorState.Empty, 30))));
        var twenty = StatusView.ExtendSection(someWeek, StatusView.ExtendPick(EditorState.Empty, 20));
        Assert.Equal("20", Picked(twenty));
        Assert.NotNull(twenty.Explanation);
        Assert.Equal(WeekLine, twenty.Note);

        var onlyTen = StatusView.ExtendSection(Evening(week: 33600), EditorState.Empty);
        Assert.Equal("10", Enabled(onlyTen));
        Assert.Equal(string.Empty, Picked(onlyTen));
        Assert.Equal(WeekLine, onlyTen.Note);

        var noWeek = StatusView.ExtendSection(Evening(week: 34500), EditorState.Empty);
        Assert.Equal(string.Empty, Enabled(noWeek));
        Assert.Equal(string.Empty, Picked(noWeek));
        Assert.Null(noWeek.Explanation);
        Assert.Equal(WeekLine, noWeek.Note);

        var early = StatusView.ExtendSection(Evening(night: 3000), EditorState.Empty);
        Assert.Equal(string.Empty, Enabled(early));
        Assert.Equal("Extend is available in the last 30 minutes before tonight's limit.", early.Note);

        var noLimit = StatusView.ExtendSection(Evening(limit: null), EditorState.Empty);
        Assert.Equal("10,20,30", Enabled(noLimit));
        Assert.Equal(OpenLine, noLimit.Note);

        var used = Evening(limit: 110, extension: 20);
        var case2 = StatusView.ExtendSection(used, EditorState.Empty);
        Assert.Equal(ExtendLayout.Choose, case2.Layout);
        Assert.Equal(string.Empty, Enabled(case2));
        Assert.Equal(string.Empty, Picked(case2));
        Assert.Equal(UsedLine, case2.Note);

        var applied = StatusView.ExtendAnswered(EditorState.Empty, AppliedText, ok: true, new DateOnly(2026, 10, 20));
        var card2 = StatusView.ExtendSection(used, applied);
        Assert.Equal(ExtendLayout.Done, card2.Layout);
        Assert.Empty(card2.Buttons);
        Assert.Equal(DoneLine, card2.Note);
        Assert.NotNull(card2.Reply);

        var refused = StatusView.ExtendAnswered(EditorState.Empty, "Not allowed.", ok: false, new DateOnly(2026, 10, 20));
        var refusedView = StatusView.ExtendSection(used, refused);
        Assert.Equal(ExtendLayout.Choose, refusedView.Layout);
        Assert.NotNull(refusedView.Reply);

        var echoed = StatusView.ExtendAnswered(EditorState.Empty, UsedLine, ok: false, new DateOnly(2026, 10, 20));
        Assert.Null(StatusView.ExtendSection(used, echoed).Reply);

        var waiting = StatusView.ExtendPick(EditorState.Empty, 20) with { ExtendWaiting = true };
        Assert.Equal(StatusView.ExtendSection(Evening(), StatusView.ExtendPick(EditorState.Empty, 20)), StatusView.ExtendSection(Evening(), waiting), SectionComparer.Instance);
        Assert.False(StatusView.ExtendClickAllowed(waiting));
        Assert.True(StatusView.ExtendClickAllowed(waiting with { ExtendWaiting = false }));

        // The answer arrived before the state did: the length that was sent stands in for the extension.
        var early2 = StatusView.ExtendAnswered(StatusView.ExtendPick(EditorState.Empty, 20), AppliedText, ok: true, new DateOnly(2026, 10, 20));
        var beforeState = StatusView.ExtendSection(Evening(), early2);
        Assert.Equal(ExtendLayout.Done, beforeState.Layout);
        Assert.NotNull(beforeState.Reply);
        Assert.Equal(DoneLine, beforeState.Note);

        Assert.Equal(EditorState.Empty with { ExtendChoice = 10 }, StatusView.ExtendPick(EditorState.Empty, 10));
    }

    [Fact]
    public void MergeKeepsTheChosenLength()
    {
        var tonight = new DateOnly(2026, 10, 26);
        var waiting = EditorState.Empty with { ExtendChoice = 20, ExtendWaiting = true };

        Assert.Equal(waiting, StatusView.Merge(waiting, Evening(week: 34500), tonight));
        Assert.Null(StatusView.Merge(EditorState.Empty with { ExtendChoice = 30 }, Evening(limit: 120, extension: 30), tonight).ExtendChoice);

        var answered = StatusView.ExtendAnswered(EditorState.Empty, AppliedText, ok: true, tonight);
        var dropped = StatusView.Merge(answered, Evening(), tonight);
        Assert.Null(dropped.ReplyOf(ReplySection.Extend));
        Assert.False(dropped.ExtendApplied);
        Assert.Equal(answered, StatusView.Merge(answered, Evening(limit: 110, extension: 20), tonight));

        var notAllowed = StatusView.ExtendAnswered(EditorState.Empty, "Not allowed.", ok: false, tonight);
        Assert.Equal("Not allowed.", StatusView.Merge(notAllowed, Evening(), tonight).ReplyOf(ReplySection.Extend));
        var nextNight = StatusView.Merge(notAllowed, Evening(), tonight.AddDays(1));
        Assert.Null(nextNight.ReplyOf(ReplySection.Extend));
        Assert.Null(nextNight.ExtendReplyNight);

        // The four new members take part in equality and in the hash.
        var baseline = EditorState.Empty;
        Assert.NotEqual(baseline, baseline with { ExtendChoice = 10 });
        Assert.NotEqual(baseline, baseline with { ExtendWaiting = true });
        Assert.NotEqual(baseline, baseline with { ExtendApplied = true });
        Assert.NotEqual(baseline, baseline with { ExtendReplyNight = tonight });
        Assert.Equal((baseline with { ExtendChoice = 10 }).GetHashCode(), (EditorState.Empty with { ExtendChoice = 10 }).GetHashCode());
    }

    [Fact]
    public void ExtendWaitingLifecycle()
    {
        var tonight = new DateOnly(2026, 10, 26);
        var start = OpenWeeknight().WithReply(ReplySection.Extend, "Not allowed.") with { ExtendChoice = 10 };

        var sent = StatusView.ExtendSent(start, 20);
        Assert.Equal(start.WithReply(ReplySection.Extend, null) with { ExtendChoice = 20, ExtendWaiting = true }, sent);

        var answered = StatusView.ExtendAnswered(sent, AppliedText, ok: true, tonight);
        Assert.Equal(sent.WithReply(ReplySection.Extend, AppliedText) with { ExtendWaiting = false, ExtendApplied = true, ExtendReplyNight = tonight }, answered);
        Assert.False(StatusView.ExtendAnswered(sent, "Not allowed.", ok: false, tonight).ExtendApplied);

        var lost = StatusView.ConnectionLost(sent);
        Assert.False(lost.ExtendWaiting);
        Assert.Equal(sent with { ExtendWaiting = false }, lost);

        var cleared = answered.WithReply(ReplySection.Extend, null);
        Assert.Null(cleared.ReplyOf(ReplySection.Extend));
        Assert.False(cleared.ExtendApplied);
        Assert.Null(cleared.ExtendReplyNight);
    }

    private sealed class SectionComparer : IEqualityComparer<ExtendSectionView>
    {
        public static SectionComparer Instance { get; } = new();

        public bool Equals(ExtendSectionView? x, ExtendSectionView? y) =>
            x is not null && y is not null
            && x.Layout == y.Layout && x.Explanation == y.Explanation && x.Note == y.Note && x.Reply == y.Reply
            && x.Buttons.SequenceEqual(y.Buttons);

        public int GetHashCode(ExtendSectionView obj) => obj.Note.GetHashCode(StringComparison.Ordinal);
    }
}
