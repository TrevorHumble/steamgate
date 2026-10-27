using System.Globalization;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Status;

/// <summary>How a waiting change reads in the Waiting to apply list.</summary>
public static class PendingText
{
    /// <summary>
    /// "Weeknight limit: 1 h 30 min to 2 h" and its kin, "Stop counting Viewfinder", "Turn SteamGate off", "Allow SteamGate to be removed".
    /// The current value is the setting's value in the same state. An entry the engine never makes (a turn-on, a take-back of
    /// removal, which apply at once) shows "field value" in the settings file's spelling.
    /// </summary>
    public static string For(PendingChange entry, StateMessage state)
    {
        var settings = state.Settings;
        return entry.Field switch
        {
            ChangeFields.WeeknightMinutes when entry.Number is { } minutes =>
                $"Weeknight limit: {DurationText.Minutes(settings.WeeknightMinutes)} to {DurationText.Minutes(minutes)}",
            ChangeFields.WeekendNightMinutes when entry.Number is { } minutes =>
                $"Weekend night limit: {DurationText.Minutes(settings.WeekendNightMinutes)} to {DurationText.Minutes(minutes)}",
            ChangeFields.WeeklyCapMinutes when entry.Number is { } minutes =>
                $"Weekly cap: {DurationText.Minutes(settings.WeeklyCapMinutes)} to {DurationText.Minutes(minutes)}",
            ChangeFields.CooldownHours when entry.Number is { } hours =>
                $"Cooldown: {StatusText.Hours(settings.CooldownHours)} to {StatusText.Hours(hours)}",
            ChangeFields.WeekendNightsAdd when entry.Days is { } days =>
                $"Weekend nights: {StatusText.Days(settings.WeekendNights)} to {StatusText.Days(settings.WeekendNights.Concat(days))}",
            ChangeFields.WeekendNightsRemove when entry.Days is { } days =>
                $"Weekend nights: {StatusText.Days(settings.WeekendNights)} to {StatusText.Days(settings.WeekendNights.Where(night => !days.Contains(night)))}",
            ChangeFields.NotCountedAdd when entry.Folder is { } folder =>
                "Stop counting " + StatusState.DisplayName(folder, state),
            ChangeFields.Enabled when entry.Flag == false => "Turn SteamGate off",
            ChangeFields.AllowUninstall when entry.Flag == true => "Allow SteamGate to be removed",
            _ => Raw(entry),
        };
    }

    // The field and its value as settings.json spells them.
    private static string Raw(PendingChange entry)
    {
        var value = entry.Number is { } number ? number.ToString(CultureInfo.InvariantCulture)
            : entry.Flag is { } flag ? (flag ? "true" : "false")
            : entry.Folder ?? (entry.Days is { } days ? StatusText.Days(days) : string.Empty);
        return (entry.Field + " " + value).TrimEnd();
    }
}
