using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Tests.Rules;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusViewTests
{
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

        var merged = StatusView.Merge(editor, next);

        Assert.Equal(editor, merged);
        var verdict = VerdictLine.For(StatusState.ToSettings(next), RuleFixtures.Number(ChangeFields.WeeknightMinutes, 120), StateFixtures.Now, TimeZoneInfo.Utc);
        Assert.Equal("This loosens the rule. It applies Thu, Oct 22, 9:14 PM (48 h).", verdict.Text);

        var replaced = StatusView.Merge(editor, StateFixtures.State(addable: [StateFixtures.Named("Overwatch"), StateFixtures.Named("Viewfinder 2")]));
        Assert.Equal(editor with { AddFolder = "Overwatch" }, replaced);

        var emptied = StatusView.Merge(editor, StateFixtures.State(addable: []));
        Assert.Null(emptied.AddFolder);
        Assert.Equal(editor with { AddFolder = null }, emptied);

        var firstChoice = StatusView.Merge(EditorState.Empty, StateFixtures.State(addable: [StateFixtures.Named("Arc"), StateFixtures.Named("Zed")]));
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
}
