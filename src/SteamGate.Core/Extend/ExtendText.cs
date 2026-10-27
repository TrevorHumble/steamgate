using SteamGate.Core.Accounting;

namespace SteamGate.Core.Extend;

/// <summary>The wording of the Extend section and of the answers to an extend request. Section lines and notes the window shows come from here.</summary>
public static class ExtendText
{
    public const string Heading = "Extend tonight";
    public const string RoundLabel = "Finish your round:";
    public const string ExtendButton = "Extend";

    /// <summary>The note under the buttons when one or more lengths are greyed only because of the week.</summary>
    public const string WeekNote = "Not enough time left this week.";

    /// <summary>The words that open the answer to an extension that applied.</summary>
    public const string AppliedPrefix = "Applied now. Tonight ends ";

    /// <summary>The note on the This week meter's used line while tonight has an extension.</summary>
    public const string WeekSuffix = " (past the old limit counts double)";

    private const string DoneTail = " Only the minutes you actually play past the old limit are counted double.";

    /// <summary>"20 min": the label of a length button.</summary>
    public static string Length(int minutes) => DurationText.Minutes(minutes);

    /// <summary>"Extension used tonight: 20 min."</summary>
    public static string UsedNote(int minutes) => "Extension used tonight: " + DurationText.Minutes(minutes) + ".";

    /// <summary>The note once the extension is taken and its answer is on screen.</summary>
    public static string DoneNote(int minutes) => UsedNote(minutes) + DoneTail;

    /// <summary>" (20 min extension)": after the Tonight meter's used line.</summary>
    public static string TonightSuffix(int minutes) => " (" + DurationText.Minutes(minutes) + " extension)";

    /// <summary>The note under the buttons outside the window.</summary>
    public static string NotInWindowNote => "Extend is available in the last " + WindowMinutes + " minutes before tonight's limit.";

    /// <summary>The note under the buttons while all three lengths can be chosen.</summary>
    public static string OpenNote => "Once per night, in the last " + WindowMinutes + " minutes before tonight's limit.";

    /// <summary>The line that explains the picked length: what it adds and the most it takes off the week.</summary>
    public static string Explain(int minutes, int limitMinutes) =>
        "Adds " + DurationText.Minutes(minutes) + " to tonight (limit becomes " + DurationText.Minutes(limitMinutes + minutes) + "). Up to "
        + (int)(ExtensionCost.FullExtensionWeekSeconds(minutes) / 60) + " min comes off this week, because time past tonight's limit counts double.";

    /// <summary>The answer to an extension that applied: "Applied now. Tonight ends 20 min later (limit 1 h 50 min)."</summary>
    public static string Applied(int minutes, int limitMinutes) =>
        AppliedPrefix + DurationText.Minutes(minutes) + " later (limit " + DurationText.Minutes(limitMinutes + minutes) + ").";

    private static int WindowMinutes => ExtendRules.WindowSeconds / 60;
}
