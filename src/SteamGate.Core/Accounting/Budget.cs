namespace SteamGate.Core.Accounting;

/// <summary>How much play time is left, from the limits and the tally.</summary>
public static class Budget
{
    /// <summary>The limit for a night: the weekend limit when the night's date falls on a weekend night, else the weeknight limit.</summary>
    public static int NightLimitMinutes(DateOnly night, GateSettings settings) =>
        settings.WeekendNights.Contains(night.DayOfWeek) ? settings.WeekendNightMinutes : settings.WeeknightMinutes;

    /// <summary>The smaller of what is left tonight and what is left this week, never below 0 and never rounded.</summary>
    public static double RemainingSeconds(Tally tally, GateSettings settings, DateTime localNow)
    {
        var rolled = NightClock.RollOver(tally, localNow);
        var tonight = (NightLimitMinutes(rolled.Night, settings) * 60.0) - rolled.NightSeconds;
        var thisWeek = (settings.WeeklyCapMinutes * 60.0) - rolled.WeekSeconds;
        return Math.Max(0, Math.Min(tonight, thisWeek));
    }
}
