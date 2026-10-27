namespace SteamGate.Core.Accounting;

/// <summary>Names the night and the week a local time belongs to. A night ends at 4 AM and a week ends Monday at 4 AM.</summary>
public static class NightClock
{
    private const int NightEndsAtHour = 4;

    /// <summary>The calendar date of the local time minus 4 hours, so 03:59 still belongs to the night that began the evening before.</summary>
    public static DateOnly NightOf(DateTime local) => DateOnly.FromDateTime(local.AddHours(-NightEndsAtHour));

    /// <summary>The Monday on or before the night of the local time. Monday 03:59 is still in the week before.</summary>
    public static DateOnly WeekOf(DateTime local)
    {
        var night = NightOf(local);
        var daysSinceMonday = ((int)night.DayOfWeek + 6) % 7;
        return night.AddDays(-daysSinceMonday);
    }

    /// <summary>The tally with the night, and the week, set to the ones the local time names. A part from another night or week counts as 0 used, and another night's extension is dropped.</summary>
    public static Tally RollOver(Tally tally, DateTime localNow)
    {
        var night = NightOf(localNow);
        var week = WeekOf(localNow);
        return tally with
        {
            Night = night,
            NightSeconds = tally.Night == night ? tally.NightSeconds : 0,
            Week = week,
            WeekSeconds = tally.Week == week ? tally.WeekSeconds : 0,
            Extension = tally.Extension is { } extension && extension.Night == night ? extension : null,
        };
    }
}
