namespace SteamGate.Core.Accounting;

/// <summary>How much play time is left, from the limits and the tally.</summary>
public static class Budget
{
    /// <summary>The limit for a night: the weekend limit when the night's date falls on a weekend night, else the weeknight limit.</summary>
    public static int NightLimitMinutes(DateOnly night, GateSettings settings) =>
        settings.WeekendNights.Contains(night.DayOfWeek) ? settings.WeekendNightMinutes : settings.WeeknightMinutes;

    /// <summary>The limit of the night the local time names, in seconds, without tonight's extension. Time past it counts double toward the week once the night has an extension.</summary>
    public static double OriginalLimitSeconds(GateSettings settings, DateTime localNow) =>
        NightLimitMinutes(NightClock.NightOf(localNow), settings) * 60.0;

    /// <summary>Tonight's limit in minutes (the limit plus tonight's extension), after the tally is rolled over to the night the local time names.</summary>
    public static int TonightLimitMinutes(Tally tally, GateSettings settings, DateTime localNow)
    {
        var rolled = NightClock.RollOver(tally, localNow);
        return NightLimitMinutes(rolled.Night, settings) + rolled.ExtensionMinutes;
    }

    /// <summary>
    /// The smaller of what is left tonight (the limit plus tonight's extension) and what the week still allows, never below 0 and never rounded.
    /// What the week allows counts time past the original limit at its higher cost, so the weekly cap stays a hard limit.
    /// </summary>
    public static double RemainingSeconds(Tally tally, GateSettings settings, DateTime localNow)
    {
        var rolled = NightClock.RollOver(tally, localNow);
        var originalLimit = OriginalLimitSeconds(settings, localNow);
        var tonight = originalLimit + (rolled.ExtensionMinutes * 60.0) - rolled.NightSeconds;
        var weekLeft = (settings.WeeklyCapMinutes * 60.0) - rolled.WeekSeconds;
        var thisWeek = ExtensionCost.PlayableWeekSeconds(weekLeft, rolled.NightSeconds, originalLimit, rolled.HasExtension);
        return Math.Max(0, Math.Min(tonight, thisWeek));
    }
}
