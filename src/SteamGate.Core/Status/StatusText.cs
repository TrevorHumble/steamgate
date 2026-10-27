using System.Globalization;
using SteamGate.Core.Enforcement;
using SteamGate.Core.Extend;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Status;

/// <summary>
/// The numbers and wording of the status screen that come from the state or SteamGate's answer: time left and used, the meter labels,
/// days and times, waiting-change timing, the reply colors, the rules and not-connected lines, and the two card 12 lines with their buttons
/// ("Request turn on" and "Stop allowing removal"). The Rules row names and unit notes are in StatusView. Section headings other than Extend tonight (ExtendText), the other button
/// labels, the add-box label, the title, and the day abbreviations on the check boxes are fixed text in SteamGate.Status and are not
/// machine-checked. Times use the invariant culture, which spells days, months, and AM/PM as en-US does.
/// </summary>
public static class StatusText
{
    public const string WeekLabel = "This week (resets Monday 4 AM)";
    public const string CountingNow = "Counting now: ";

    /// <summary>The small word after the big time-left number.</summary>
    public const string LeftWord = "left";

    public const string RulesNote = "Loosening a rule waits the cooldown. Tightening a rule applies now.";
    public const string RemovingAppliesNow = "Removing applies now";
    public const string NothingWaiting = "Nothing waiting.";
    public const string NotConnected = "Can't reach SteamGate.";
    public const string SteamGateOff = "SteamGate is off.";
    public const string RequestTurnOn = "Request turn on";
    public const string RemovalAllowed = "Removal is allowed.";
    public const string StopAllowingRemoval = "Stop allowing removal";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>The time left, from seconds: whole minutes rounded up, the same number a warning shows.</summary>
    public static string Left(double seconds) => DurationText.Minutes(WarnMinutes.FromRemaining(seconds));

    /// <summary>What is left tonight: the night's limit minus tonight's use, never below 0.</summary>
    public static string TonightLeft(StateMessage state) =>
        Left((TonightLimit(state) * 60.0) - state.NightSeconds);

    /// <summary>What is left this week: the weekly cap minus the week's use, never below 0.</summary>
    public static string WeekLeft(StateMessage state) =>
        Left((state.Settings.WeeklyCapMinutes * 60.0) - state.WeekSeconds);

    /// <summary>Tonight's limit in minutes: the one the service sent, else the weeknight or weekend-night limit for tonight plus tonight's extension.</summary>
    public static int TonightLimit(StateMessage state) =>
        state.TonightLimitMinutes
        ?? ((state.TonightWeekend ? state.Settings.WeekendNightMinutes : state.Settings.WeeknightMinutes) + state.ExtensionMinutes);

    /// <summary>Tonight's limit without the extension, in seconds: the line where time starts to count double toward the week.</summary>
    public static double OriginalLimitSeconds(StateMessage state) => (TonightLimit(state) - state.ExtensionMinutes) * 60.0;

    /// <summary>The Tonight meter's used line, with the extension noted when tonight has one: "78 of 110 min used (20 min extension)".</summary>
    public static string TonightUsed(StateMessage state) =>
        Used(state.NightSeconds, TonightLimit(state)) + (state.ExtensionMinutes > 0 ? ExtendText.TonightSuffix(state.ExtensionMinutes) : string.Empty);

    /// <summary>The This week meter's used line, with the doubling noted when tonight has an extension.</summary>
    public static string WeekUsed(StateMessage state) =>
        Used(state.WeekSeconds, state.Settings.WeeklyCapMinutes) + (state.ExtensionMinutes > 0 ? ExtendText.WeekSuffix : string.Empty);

    /// <summary>
    /// "47 of 90 min used" when the limit is not a whole number of hours, else "6 h 10 min of 10 h used". Used minutes round down.
    /// </summary>
    public static string Used(double usedSeconds, int limitMinutes)
    {
        var used = (int)Math.Floor(Math.Max(0, usedSeconds) / 60);
        return limitMinutes % 60 != 0
            ? string.Create(Culture, $"{used} of {limitMinutes} min used")
            : $"{DurationText.Minutes(used)} of {DurationText.Minutes(limitMinutes)} used";
    }

    /// <summary>How full the bar is: 0 to 100, rounded; 100 when the limit is 0.</summary>
    public static int FillPercent(double usedSeconds, int limitMinutes)
    {
        if (limitMinutes <= 0)
        {
            return 100;
        }

        var percent = Math.Round(100 * Math.Max(0, usedSeconds) / (limitMinutes * 60.0), MidpointRounding.AwayFromZero);
        return (int)Math.Min(100, percent);
    }

    public static string TonightLabel(bool weekend) => weekend ? "Tonight (weekend night, ends 4 AM)" : "Tonight (weeknight, ends 4 AM)";

    /// <summary>Three-letter day names, Monday first: "Fri, Sat, Sun".</summary>
    public static string Days(IEnumerable<DayOfWeek> days) =>
        string.Join(", ", DayNames.MondayFirst(days).Select(day => day.ToString()[..3]));

    /// <summary>"Applies Thu, Oct 22, 7:02 PM".</summary>
    public static string AppliesAt(DateTime local) => "Applies " + Clock(local);

    /// <summary>"Thu, Oct 22, 7:02 PM".</summary>
    public static string Clock(DateTime local) => local.ToString("ddd, MMM d, h:mm tt", Culture);

    /// <summary>"in 1 d 22 h" or "in 5 h": the hours rounded to the nearest, a day at 24.</summary>
    public static string In(double seconds)
    {
        var hours = (long)Math.Round(seconds / 3600, MidpointRounding.AwayFromZero);
        return hours >= 24
            ? string.Create(Culture, $"in {hours / 24} d {hours % 24} h")
            : string.Create(Culture, $"in {hours} h");
    }

    /// <summary>"48 h".</summary>
    public static string Hours(int hours) => string.Create(Culture, $"{hours} h");

    /// <summary>True when SteamGate's reply says the change or the extension applied now (green); every other reply is amber.</summary>
    public static bool ReplyIsNow(string text)
    {
        var withoutSuffix = text.EndsWith(ChangeReply.SaveFailedSuffix, StringComparison.Ordinal)
            ? text[..^ChangeReply.SaveFailedSuffix.Length]
            : text;
        return withoutSuffix == ChangeReply.AppliedNow || withoutSuffix.StartsWith(ExtendText.AppliedPrefix, StringComparison.Ordinal);
    }
}
