using SteamGate.Core.Accounting;
using SteamGate.Core.Extend;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Status;

/// <summary>The parts of the screen that show SteamGate's answer to a request, one line each.</summary>
public enum ReplySection
{
    /// <summary>Under the Rules note.</summary>
    Rules,

    /// <summary>Under the waiting rows.</summary>
    Waiting,

    /// <summary>Under the add box.</summary>
    NotCounted,

    /// <summary>Under the turn-off buttons and their note.</summary>
    TurnOff,

    /// <summary>Under the lines that say SteamGate is off or removal is allowed.</summary>
    Head,

    /// <summary>In the Extend section, under the buttons or under its heading.</summary>
    Extend,
}

/// <summary>
/// What the user has done on the screen that a new state must not undo: the open Rules editor and what is typed in it, the chosen program
/// in the add box, each section's reply line, the chosen Extend length, whether an Extend request is waiting for its answer, whether the Extend
/// reply says it applied, and the night that reply came in. Value-compared, lists included.
/// </summary>
public sealed record EditorState(
    string? OpenField,
    int Number,
    IReadOnlyList<DayOfWeek> Days,
    string? AddFolder,
    IReadOnlyDictionary<ReplySection, string> Replies,
    int? ExtendChoice = null,
    bool ExtendWaiting = false,
    bool ExtendApplied = false,
    DateOnly? ExtendReplyNight = null)
{
    public static EditorState Empty { get; } = new(null, 0, [], null, new Dictionary<ReplySection, string>());

    public bool Equals(EditorState? other) =>
        other is not null
        && OpenField == other.OpenField && Number == other.Number && Days.SequenceEqual(other.Days) && AddFolder == other.AddFolder
        && Replies.Count == other.Replies.Count && Replies.All(pair => other.Replies.TryGetValue(pair.Key, out var text) && text == pair.Value)
        && ExtendChoice == other.ExtendChoice && ExtendWaiting == other.ExtendWaiting && ExtendApplied == other.ExtendApplied
        && ExtendReplyNight == other.ExtendReplyNight;

    public override int GetHashCode() => HashCode.Combine(OpenField, Number, Days.Count, AddFolder, Replies.Count, ExtendChoice, ExtendWaiting, ExtendReplyNight);

    /// <summary>The reply line of a section, or null when it shows none.</summary>
    public string? ReplyOf(ReplySection section) => Replies.GetValueOrDefault(section);

    /// <summary>
    /// The same state with this section's reply line replaced (null clears it; clearing the Extend line also clears whether it said applied and its night).
    /// The old state is left as it was.
    /// </summary>
    public EditorState WithReply(ReplySection section, string? text)
    {
        var copy = new Dictionary<ReplySection, string>(Replies);
        if (text is null)
        {
            copy.Remove(section);
        }
        else
        {
            copy[section] = text;
        }

        var updated = this with { Replies = copy };
        return text is null && section == ReplySection.Extend ? updated with { ExtendApplied = false, ExtendReplyNight = null } : updated;
    }
}

/// <summary>How the Extend section is laid out: the choice box with its note, or only the answer and the done note.</summary>
public enum ExtendLayout
{
    /// <summary>The box with the three length buttons, then the note.</summary>
    Choose,

    /// <summary>Only the answer line and the done note, once the extension is taken and its answer is on screen.</summary>
    Done,
}

/// <summary>One length button of the Extend section.</summary>
public sealed record ExtendButtonView(int Minutes, bool Enabled, bool Picked);

/// <summary>
/// What the Extend section shows. Buttons is empty in the Done layout. Explanation is null when no length is picked, and the Extend button shows
/// only when it is not null. Reply is the text of the reply line, or null when the line is hidden (the reply is absent or equals the note).
/// </summary>
public sealed record ExtendSectionView(
    ExtendLayout Layout,
    IReadOnlyList<ExtendButtonView> Buttons,
    string? Explanation,
    string Note,
    string? Reply);

/// <summary>One line of the Rules list and the editor it opens: a field of the lock's table, the name shown for it, and how its editor takes input.</summary>
/// <param name="Spec">The field's entry in <see cref="ChangeFields.All"/>.</param>
/// <param name="Name">The row's name.</param>
/// <param name="BoxMax">The highest number the box accepts (the lowest is 0). Not used for the weekend nights.</param>
public sealed record RuleRow(FieldSpec Spec, string Name, int BoxMax)
{
    /// <summary>The request field the Change button edits.</summary>
    public string Field => Spec.Name;

    /// <summary>True for the weekend nights, whose editor has check boxes instead of a number box.</summary>
    public bool IsDays => Spec.Kind == FieldKind.Days;

    /// <summary>The note after the number box: "min", or for the cooldown "h (most 168 h, which is 7 days)". Null when there is no number box.</summary>
    public string? UnitNote => IsDays
        ? null
        : Spec.Name == ChangeFields.CooldownHours
            ? $"h (most {ChangeFields.MaxCooldownHours} h, which is {ChangeFields.MaxCooldownHours / 24} days)"
            : "min";
}

/// <summary>The decisions behind what the status screen draws and keeps when a state arrives. The window only connects them to controls.</summary>
public static class StatusView
{
    /// <summary>The number box accepts this many hours for the cooldown, so a value over the real maximum can show its own line.</summary>
    public const int CooldownBoxMax = 9999;

    /// <summary>The rows of the Rules list, in the order the preview shows them.</summary>
    public static IReadOnlyList<RuleRow> RuleRows { get; } =
    [
        Row(ChangeFields.WeeknightMinutes, "Weeknight limit"),
        Row(ChangeFields.WeekendNightMinutes, "Weekend night limit"),
        Row(ChangeFields.WeekendNights, "Weekend nights"),
        Row(ChangeFields.WeeklyCapMinutes, "Weekly cap"),
        Row(ChangeFields.CooldownHours, "Cooldown"),
    ];

    private static RuleRow Row(string field, string name)
    {
        var spec = ChangeFields.Find(field)!;
        return new RuleRow(spec, name, field == ChangeFields.CooldownHours ? CooldownBoxMax : spec.Max);
    }

    /// <summary>The check boxes' order: Sunday first, as the first preview has them.</summary>
    public static IReadOnlyList<DayOfWeek> CheckBoxDays { get; } =
    [
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    ];

    /// <summary>How long the screen goes without a connection before it says it cannot reach SteamGate.</summary>
    public static readonly TimeSpan UnreachableAfter = TimeSpan.FromSeconds(10);

    /// <summary>How the screen waits between tries to connect.</summary>
    public static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    /// <summary>The value shown in a Rules row: minutes as "1 h 30 min", the weekend nights as "Fri, Sat, Sun", the cooldown as "48 h".</summary>
    public static string RuleValue(RuleRow row, StateMessage state) =>
        row.IsDays
            ? StatusText.Days(state.Settings.WeekendNights)
            : row.Field == ChangeFields.CooldownHours
                ? StatusText.Hours(RuleNumber(row, state))
                : DurationText.Minutes(RuleNumber(row, state));

    /// <summary>The number a Rules editor starts with: the setting's current value (for the weekend nights, 0 and unused).</summary>
    public static int RuleNumber(RuleRow row, StateMessage state) =>
        row.IsDays ? 0 : row.Spec.GetNumber!(StatusState.ToSettings(state));

    /// <summary>The editor state with this row's editor open on the setting's current value, or closed when it was the open one.</summary>
    public static EditorState Toggle(EditorState editor, RuleRow row, StateMessage state) =>
        editor.OpenField == row.Field
            ? editor with { OpenField = null }
            : editor with { OpenField = row.Field, Number = RuleNumber(row, state), Days = state.Settings.WeekendNights };

    /// <summary>
    /// The editor state to use with a new state. An open editor stays open and keeps its number, its checked days, and every reply line.
    /// The chosen program stays chosen while it is still on the add list; otherwise the first one on the list is chosen, or none.
    /// Likewise the chosen Extend length and the Extend wait are kept as they are, except that the length is dropped once the extension is taken. An Extend reply
    /// from another night goes, and so does an applied one once the state says no extension is taken. Night is tonight's night.
    /// </summary>
    public static EditorState Merge(EditorState editor, StateMessage next, DateOnly night)
    {
        var stillThere = editor.AddFolder is { } chosen && next.Addable.Any(entry => entry.Folder == chosen);
        var merged = stillThere ? editor : editor with { AddFolder = next.Addable.Count > 0 ? next.Addable[0].Folder : null };
        if (next.ExtensionMinutes > 0)
        {
            merged = merged with { ExtendChoice = null };
        }

        var staleNight = merged.ExtendReplyNight is { } replyNight && replyNight != night;
        return staleNight || (merged.ExtendApplied && next.ExtensionMinutes == 0) ? merged.WithReply(ReplySection.Extend, null) : merged;
    }

    /// <summary>
    /// The Extend section for this state. The layout is Done when an applied Extend answer is on screen and the extension is known (from the state,
    /// else from the length that was sent); otherwise it is Choose, with the lengths ExtendRules offers. A picked length counts only while it is offered;
    /// with none picked and all lengths offered, the default length is picked.
    /// </summary>
    public static ExtendSectionView ExtendSection(StateMessage state, EditorState editor)
    {
        var reply = editor.ReplyOf(ReplySection.Extend);
        var taken = state.ExtensionMinutes > 0 ? state.ExtensionMinutes : editor.ExtendChoice ?? 0;
        if (editor.ExtendApplied && reply is not null && taken > 0)
        {
            return new ExtendSectionView(ExtendLayout.Done, [], null, ExtendText.DoneNote(taken), reply);
        }

        var view = ExtendRules.View(StatusText.OriginalLimitSeconds(state), state.ExtensionMinutes, state.NightSeconds, state.Settings.WeeklyCapMinutes, state.WeekSeconds);
        int? picked = editor.ExtendChoice is { } choice && view.Offered.Contains(choice)
            ? choice
            : view.Offered.Count == NightExtension.Lengths.Count ? ExtendRules.DefaultLength : null;
        var buttons = NightExtension.Lengths.Select(length => new ExtendButtonView(length, view.Offered.Contains(length), length == picked)).ToList();
        return new ExtendSectionView(
            ExtendLayout.Choose,
            buttons,
            picked is { } pickedLength ? ExtendText.Explain(pickedLength, StatusText.TonightLimit(state)) : null,
            view.Note,
            reply is not null && reply != view.Note ? reply : null);
    }

    /// <summary>The editor with this length picked.</summary>
    public static EditorState ExtendPick(EditorState editor, int minutes) => editor with { ExtendChoice = minutes };

    /// <summary>False while an Extend request waits for its answer: clicks on the lengths and on Extend are then ignored.</summary>
    public static bool ExtendClickAllowed(EditorState editor) => !editor.ExtendWaiting;

    /// <summary>The editor once an Extend request for this length is sent: the length is kept, the old answer is cleared, and the wait starts.</summary>
    public static EditorState ExtendSent(EditorState editor, int minutes) =>
        editor.WithReply(ReplySection.Extend, null) with { ExtendChoice = minutes, ExtendWaiting = true };

    /// <summary>The editor once the answer to the Extend request arrives: the wait ends and the answer shows for this night.</summary>
    public static EditorState ExtendAnswered(EditorState editor, string text, bool ok, DateOnly night) =>
        editor.WithReply(ReplySection.Extend, text) with { ExtendWaiting = false, ExtendApplied = ok, ExtendReplyNight = night };

    /// <summary>The editor once the connection ends: no answer will come, so the wait ends.</summary>
    public static EditorState ConnectionLost(EditorState editor) => editor with { ExtendWaiting = false };

    /// <summary>True when a list must be rebuilt because its rows, or their order, changed.</summary>
    public static bool RowsChanged(IReadOnlyList<string> previousKeys, IReadOnlyList<string> nextKeys) => !previousKeys.SequenceEqual(nextKeys);

    /// <summary>The keys of the waiting rows: the pending entries' ids.</summary>
    public static IReadOnlyList<string> PendingKeys(StateMessage state) => [.. state.Settings.Pending.Select(entry => entry.Id.ToString())];

    /// <summary>The keys of the not-counted rows: their folders.</summary>
    public static IReadOnlyList<string> NotCountedKeys(StateMessage state) => [.. state.Settings.NotCounted.Select(entry => entry.Folder)];

    /// <summary>The keys of the add box's choices: their folders.</summary>
    public static IReadOnlyList<string> AddableKeys(StateMessage state) => [.. state.Addable.Select(entry => entry.Folder)];

    /// <summary>The names of the add box's choices, in order. A name can change while its folder stays, so the box compares these as well as the keys.</summary>
    public static IReadOnlyList<string> AddableNames(StateMessage state) => [.. state.Addable.Select(entry => entry.DisplayName)];

    /// <summary>The lines under the heading: SteamGate is off, removal is allowed, in that order, each only when it holds.</summary>
    public static IReadOnlyList<string> HeadLines(StateMessage state)
    {
        var lines = new List<string>();
        if (!state.Settings.Enabled)
        {
            lines.Add(StatusText.SteamGateOff);
        }

        if (state.Settings.AllowUninstall)
        {
            lines.Add(StatusText.RemovalAllowed);
        }

        return lines;
    }

    /// <summary>True when the screen has had no connection long enough to say so.</summary>
    public static bool Unreachable(TimeSpan withoutConnection) => withoutConnection >= UnreachableAfter;
}
