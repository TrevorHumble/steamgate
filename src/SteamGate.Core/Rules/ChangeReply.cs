using System.Globalization;

namespace SteamGate.Core.Rules;

/// <summary>The reply texts, and the helpers that build the ones with numbers or days in them.</summary>
public static class ChangeReply
{
    public const string AppliedNow = "Applied now.";
    public const string AlreadySet = "Already set.";
    public const string NotInList = "Not in the list.";
    public const string NoPendingChange = "No pending change with that id.";
    public const string NotAllowed = "Not allowed.";
    public const string Busy = "Busy, try again.";

    /// <summary>The words that open the refused part of a weekend change that applied in part: "Applied now: Friday. Not allowed: Sunday."</summary>
    public const string NotAllowedPart = "Not allowed: ";

    /// <summary>Appended to a reply whose change is held only in memory because the save failed.</summary>
    public const string SaveFailedSuffix = " Saved in memory; will retry.";

    /// <summary>"Out of range: 0 to 1440."</summary>
    public static string OutOfRange(int min, int max) => string.Create(CultureInfo.InvariantCulture, $"Out of range: {min} to {max}.");

    /// <summary>"Waits 48 h 0 min." The total is rounded up to whole minutes.</summary>
    public static string Waits(double seconds) => "Waits " + HoursMinutes(seconds) + ".";

    /// <summary>"Applied now: Friday. Waits 48 h 0 min: Sunday."</summary>
    public static string AppliedNowAndWaits(IEnumerable<DayOfWeek> applied, double seconds, IEnumerable<DayOfWeek> waiting) =>
        "Applied now: " + DayNames.Format(applied) + ". Waits " + HoursMinutes(seconds) + ": " + DayNames.Format(waiting) + ".";

    /// <summary>"Applied now: Friday. Not allowed: Sunday."</summary>
    public static string AppliedNowAndNotAllowed(IEnumerable<DayOfWeek> applied, IEnumerable<DayOfWeek> refused) =>
        "Applied now: " + DayNames.Format(applied) + ". " + NotAllowedPart + DayNames.Format(refused) + ".";

    /// <summary>"48 h 0 min": whole hours and the minutes left over, from the seconds rounded up to a minute.</summary>
    public static string HoursMinutes(double seconds)
    {
        var total = (long)Math.Ceiling(Math.Max(0, seconds) / 60);
        return string.Create(CultureInfo.InvariantCulture, $"{total / 60} h {total % 60} min");
    }
}
