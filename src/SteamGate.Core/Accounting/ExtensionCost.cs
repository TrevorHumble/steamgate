namespace SteamGate.Core.Accounting;

/// <summary>What time past the original limit costs toward the week. The one place that holds the factor.</summary>
public static class ExtensionCost
{
    /// <summary>How many times a second played past the original limit counts toward the week while the night has an extension.</summary>
    public const int PastLimitWeight = 2;

    /// <summary>
    /// The seconds one step adds to the week. The step itself, plus its extra weight for every second of it that lies past the
    /// original limit when the night has an extension. Seconds before the limit, and any seconds on a night without an extension, count once.
    /// </summary>
    public static double WeekCharge(double oldNightSeconds, double stepSeconds, double originalLimitSeconds, bool hasExtension)
    {
        var over = hasExtension ? Math.Max(0, oldNightSeconds + stepSeconds - Math.Max(oldNightSeconds, originalLimitSeconds)) : 0;
        return stepSeconds + ((PastLimitWeight - 1) * over);
    }

    /// <summary>
    /// How many seconds of play the week still allows tonight: the seconds before the original limit count once, the ones after it
    /// count at the weight. Without an extension it is the week left as it is.
    /// </summary>
    public static double PlayableWeekSeconds(double weekLeftSeconds, double nightSeconds, double originalLimitSeconds, bool hasExtension)
    {
        var beforeLimit = Math.Max(0, originalLimitSeconds - nightSeconds);
        return !hasExtension || weekLeftSeconds <= beforeLimit
            ? weekLeftSeconds
            : beforeLimit + ((weekLeftSeconds - beforeLimit) / PastLimitWeight);
    }

    /// <summary>The week seconds that a whole extension of this many minutes would cost if all of it were played.</summary>
    public static double FullExtensionWeekSeconds(int minutes) => minutes * 60 * PastLimitWeight;
}
